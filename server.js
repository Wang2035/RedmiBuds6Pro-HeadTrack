'use strict';

const http = require('http');
const fs = require('fs');
const path = require('path');
const { spawn, exec } = require('child_process');

const ROOT = __dirname;
let PORT = 8899;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.csv': 'text/csv; charset=utf-8',
  '.ico': 'image/x-icon'
};

let reader = null;
let readerReady = false;
let lastErr = '';
let sampleCount = 0;
let lastSample = null;
let lastSampleAt = 0;
let lastRestart = 0;
let ka = null;
let kaState = { on: false, inj: false, ep: '', why: '未启动', err: '', others: 0, dither: true };
const clients = new Set();
const logStream = fs.createWriteStream(path.join(ROOT, 'reader.log'), { flags: 'a' });

function rlog(s) {
  const line = new Date().toISOString() + ' ' + s;
  console.log('  ' + s);
  try { logStream.write(line + '\n'); } catch (e) { }
}

function startReader() {
  if (reader && reader.exitCode === null && !reader.killed) return;
  const exe = path.join(ROOT, 'HidReader.exe');
  if (!fs.existsSync(exe)) {
    lastErr = 'HidReader.exe missing';
    rlog('[!] ' + lastErr);
    return;
  }
  rlog('[*] starting HidReader.exe');
  reader = spawn(exe, [], { cwd: ROOT, windowsHide: true });
  reader.spawnedAt = Date.now();

  let buf = '';
  const onLine = (line) => {
    if (!line) return;
    if (line.indexOf('ERR ') === 0) {
      lastErr = line;
      rlog('[!] ' + line);
      push('error', { message: line });
      return;
    }
    if (line[0] === '#') {
      rlog(line);
      if (line.indexOf('#READY') === 0 || line.indexOf('#ARM') === 0 || line.indexOf('#STREAM') === 0) {
        readerReady = true;
        push('status', { ready: true, info: line });
      }
      if (line.indexOf('#STREAM') === 0) {
        /* 数据恢复就把上一次的错误清掉，避免 /status 里挂着历史报错 */
        lastErr = '';
        rlog('[+] streaming');
      }
      return;
    }
    const p = line.split(',');
    if (p.length < 4) return;
    const s = {
      t: +p[0], a: +p[1], b: +p[2], c: +p[3],
      d: +(p[4] || 0), e: +(p[5] || 0), f: +(p[6] || 0)
    };
    if (!isFinite(s.a)) return;
    sampleCount++;
    lastSampleAt = Date.now();
    lastSample = s;
    if (!readerReady) { readerReady = true; push('status', { ready: true, info: 'streaming' }); }
    push('sample', s);
  };

  const attach = (stream) => {
    let b = '';
    stream.on('data', (d) => {
      b += d.toString('utf8');
      let i;
      while ((i = b.indexOf('\n')) >= 0) {
        onLine(b.slice(0, i).replace(/\r$/, ''));
        b = b.slice(i + 1);
      }
    });
  };
  attach(reader.stdout);
  attach(reader.stderr);

  reader.on('exit', (code) => {
    readerReady = false;
    rlog('[*] HidReader exited code=' + code);
    push('status', { ready: false, code: code });
    if (code !== 0 && code !== null && Date.now() - lastRestart > 12000) {
      lastErr = 'reader exit ' + code;
      setTimeout(startReader, 1500);
    }
  });
}

function startKeeper() {
  if (ka && ka.exitCode === null && !ka.killed) return;
  const exe = path.join(ROOT, 'KeepAlive.exe');
  if (!fs.existsSync(exe)) {
    rlog('[!] KeepAlive.exe missing');
    return;
  }
  rlog('[*] starting KeepAlive.exe');
  ka = spawn(exe, [], { cwd: ROOT, windowsHide: true });

  const onLine = (line) => {
    if (!line) return;
    if (line.indexOf('#ST') === 0) {
      try {
        kaState = JSON.parse(line.slice(3));
        push('ka', kaState);
      } catch (e) { }
      return;
    }
    if (line[0] === '#') { rlog('  ' + line); return; }
  };
  const attach = (stream) => {
    let b = '';
    stream.on('data', (d) => {
      b += d.toString('utf8');
      let i;
      while ((i = b.indexOf('\n')) >= 0) {
        onLine(b.slice(0, i).replace(/\r$/, ''));
        b = b.slice(i + 1);
      }
    });
  };
  attach(ka.stdout);
  attach(ka.stderr);

  ka.on('exit', (code) => {
    rlog('[*] KeepAlive exited code=' + code);
    kaState = { on: false, inj: false, ep: '', why: '已退出', err: 'code ' + code, others: 0, dither: true };
    push('ka', kaState);
    setTimeout(startKeeper, 3000);
  });
}

function kaCmd(s) {
  if (ka && ka.exitCode === null && ka.stdin) {
    try { ka.stdin.write(s + '\n'); return true; } catch (e) { }
  }
  return false;
}

function push(event, obj) {
  const line = 'event: ' + event + '\ndata: ' + JSON.stringify(obj) + '\n\n';
  for (const res of clients) {
    try { res.write(line); } catch (e) { clients.delete(res); }
  }
}

const server = http.createServer((req, res) => {
  const url = req.url.split('?')[0];

  if (url === '/sse') {
    res.writeHead(200, {
      'Content-Type': 'text/event-stream; charset=utf-8',
      'Cache-Control': 'no-cache, no-transform',
      'Connection': 'keep-alive',
      'X-Accel-Buffering': 'no',
      'Access-Control-Allow-Origin': '*'
    });
    res.write('retry: 500\n\n');
    res.write('event: status\ndata: ' + JSON.stringify({ ready: readerReady, info: 'hello' }) + '\n\n');
    if (lastSample) res.write('event: sample\ndata: ' + JSON.stringify(lastSample) + '\n\n');
    clients.add(res);
    req.on('close', () => clients.delete(res));
    return;
  }

  if (url === '/status') {
    res.writeHead(200, { 'Content-Type': MIME['.json'], 'Access-Control-Allow-Origin': '*' });
    res.end(JSON.stringify({
      ready: readerReady, error: lastErr, samples: sampleCount,
      last: lastSample, clients: clients.size
    }));
    return;
  }

  if (url === '/ka' || url === '/ka/on' || url === '/ka/off') {
    let ok = true;
    if (url === '/ka/on') ok = kaCmd('on');
    else if (url === '/ka/off') ok = kaCmd('off');
    if (url !== '/ka') {
      if (ok && kaState) kaState.on = (url === '/ka/on');
      push('ka', kaState);
    }
    if (url === '/ka') kaCmd('state');
    res.writeHead(200, { 'Content-Type': MIME['.json'], 'Access-Control-Allow-Origin': '*', 'Cache-Control': 'no-store' });
    res.end(JSON.stringify({ alive: !!(ka && ka.exitCode === null), state: kaState }));
    return;
  }

  let rel = url === '/' ? '/headtrack_live.html' : url;
  rel = decodeURIComponent(rel).replace(/^\/+/, '');
  const file = path.join(ROOT, rel);
  if (!file.startsWith(ROOT)) { res.writeHead(403); res.end('no'); return; }

  fs.readFile(file, (err, data) => {
    if (err) { res.writeHead(404, { 'Content-Type': 'text/plain' }); res.end('404 ' + rel); return; }
    res.writeHead(200, {
      'Content-Type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream',
      'Cache-Control': 'no-store'
    });
    res.end(data);
  });
});

function listen(port, tries) {
  server.once('error', (e) => {
    if (e.code === 'EADDRINUSE' && tries > 0) {
      console.log('[!] port ' + port + ' busy, trying ' + (port + 1));
      listen(port + 1, tries - 1);
    } else {
      console.error('[!] ' + e.message);
      process.exit(1);
    }
  });
  server.listen(port, '127.0.0.1', () => {
    PORT = port;
    const u = 'http://127.0.0.1:' + PORT + '/';
    console.log('');
    console.log('  headtrack live  ->  ' + u);
    console.log('  Ctrl+C 退出');
    console.log('');
    exec('start "" "' + u + '"');
    startReader();
    startKeeper();
  });
}

setInterval(() => {
  for (const res of clients) { try { res.write(': ping\n\n'); } catch (e) { clients.delete(res); } }
}, 15000);

// watchdog: reader alive but silent -> restart it
setInterval(() => {
  if (!reader || reader.exitCode !== null) return;
  const now = Date.now();
  const quiet = now - (lastSampleAt || Date.now());
  if (lastSampleAt && quiet < 15000) return;
  if (!lastSampleAt && now - (reader.spawnedAt || now) < 20000) return;
  if (now - lastRestart < 20000) return;
  lastRestart = now;
  rlog('[watchdog] no samples for ' + Math.round(quiet / 1000) + 's -> restart reader');
  try { reader.kill(); } catch (e) { }
  setTimeout(startReader, 1000);
}, 4000);

// watchdog: keeper 掉线就重拉
setInterval(() => {
  if (!ka || ka.exitCode !== null) startKeeper();
}, 8000);

listen(PORT, 12);

process.on('SIGINT', () => {
  if (reader) try { reader.kill(); } catch (e) { }
  if (ka) try { ka.kill(); } catch (e) { }
  process.exit(0);
});