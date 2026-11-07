using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace KeepAlive
{
    #region COM 互操作

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPVARIANT
    {
        public ushort vt;
        public ushort r1;
        public ushort r2;
        public ushort r3;
        public IntPtr p1;
        public IntPtr p2;
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        void GetCount(out int count);
        void Item(int index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                      [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        void OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out int state);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PROPERTYKEY key);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        void Commit();
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        void Initialize(int shareMode, int streamFlags, long bufferDuration,
                        long periodicity, IntPtr format, IntPtr sessionGuid);
        void GetBufferSize(out uint frames);
        void GetStreamLatency(out long latency);
        void GetCurrentPadding(out uint padding);
        void IsFormatSupported(int shareMode, ref WAVEFORMATEX format, out IntPtr closest);
        void GetMixFormat(out IntPtr format);
        void GetDevicePeriod(out long defaultPeriod, out long minPeriod);
        void Start();
        void Stop();
        void Reset();
        void SetEventHandle(IntPtr handle);
        void GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioRenderClient
    {
        void GetBuffer(uint framesRequested, out IntPtr buffer);
        void ReleaseBuffer(uint framesWritten, uint flags);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        void GetCount(out int count);
        void GetSession(int index, out IAudioSessionControl2 session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        void GetState(out int state);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid eventContext);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid eventContext);
        void GetGroupingParam(out Guid param);
        void SetGroupingParam(ref Guid param, ref Guid eventContext);
        void RegisterAudioSessionNotification(IntPtr notify);
        void UnregisterAudioSessionNotification(IntPtr notify);
        void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        void SetDuckingPreference(bool optOut);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        void GetAudioSessionControl(ref Guid guid, uint flags, out IntPtr control);
        void GetSimpleAudioVolume(ref Guid guid, uint flags, out IntPtr volume);
        void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        void RegisterSessionNotification(IntPtr notify);
        void UnregisterSessionNotification(IntPtr notify);
        void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr notify);
        void UnregisterDuckNotification(IntPtr notify);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeterInformation
    {
        void GetPeakValue(out float peak);
    }

    #endregion

    /// <summary>
    /// 空闲时向耳机端点推静音流，维持 A2DP 通路；真实音频一出现立刻让位。
    /// stdin 命令: on / off / state / quit
    /// stdout 状态: #ST{json}
    /// </summary>
    internal static class Program
    {
        const int E_RENDER = 0;
        const int DEVICE_STATE_ACTIVE = 1;
        const int CLSCTX_ALL = 23;
        const int STGM_READ = 0;
        const int SESSION_ACTIVE = 0;

        static volatile bool enabled = true;
        static volatile bool quit = false;
        static volatile bool injecting = false;
        static string match = "REDMI Buds 6 Pro";
        static string epName = "";
        static string reason = "启动";
        static int othersActive = 0;
        static int lastEmitted = -1;
        static string lastJson = "";

        static IAudioClient client = null;
        static IAudioRenderClient render = null;
        static uint bufFrames = 0;
        static int channels = 2;
        static int bytesPerSample = 4;
        static bool isFloat = true;
        static bool dither = true;

        static string lastErr = "";
        static bool alwaysInject = true;
        static float amp = 2e-5f;   // 抖动幅度，--amp= 可调
        static float peak = 0f;
        static string fmtDesc = "";
        static Mutex instanceMutex = null;
        static long fedFrames = 0;
        static long fedRate = 0;
        static long fedT0 = 0;
        const float PEAK_TH = 0.003f;   // 约 -50 dBFS，低于此视为无内容

        static void Main(string[] args)
        {
            bool wantList = false, wantSessions = false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("--match=")) match = a.Substring(8);
                else if (a.StartsWith("--amp=")) float.TryParse(a.Substring(6), out amp);
                else if (a == "--off") enabled = false;
                else if (a == "--silent") dither = false;
                else if (a == "--dither") dither = true;
                else if (a == "--obey") alwaysInject = false;
                else if (a == "--always") alwaysInject = true;
                else if (a == "--list") wantList = true;
                else if (a == "--sessions") wantSessions = true;
            }
            if (wantList) { ListAll(); return; }
            if (wantSessions) { DumpSessions(); return; }

            /* 单实例：两个保活同时注入会互相计入 others，且重复打开端点。
               Local\ 前缀避免普通用户建 Global 对象时的权限问题 */
            bool createdNew = true;
            try
            {
                instanceMutex = new Mutex(true, "Local\\BudsHeadTrackerKeepAlive", out createdNew);
            }
            catch { createdNew = true; }
            if (!createdNew)
            {
                Console.Out.WriteLine("#ST{\"on\":false,\"inj\":false,\"ep\":\"\",\"why\":\"已有实例在运行\",\"err\":\"\",\"others\":0,\"peak\":0,\"fed\":0,\"fmt\":\"\",\"always\":true,\"dither\":true}");
                Console.Out.Flush();
                return;
            }

            Thread t = new Thread(StdinLoop);
            t.IsBackground = true;
            t.Start();

            Emit(true);
            Run();
        }

        static void StdinLoop()
        {
            try
            {
                string line;
                while ((line = Console.ReadLine()) != null)
                {
                    line = line.Trim().ToLower();
                    if (line == "quit" || line == "exit") { quit = true; return; }
                    else if (line == "on") { enabled = true; reason = "手动开启"; Emit(true); }
                    else if (line == "off") { enabled = false; reason = "手动关闭"; Emit(true); }
                    else if (line == "state") Emit(true);
                }
            }
            catch { }
        }

        static void Emit(bool force)
        {
            string json = "{\"on\":" + (enabled ? "true" : "false")
                        + ",\"inj\":" + (injecting ? "true" : "false")
                        + ",\"ep\":\"" + JsonEsc(epName) + "\""
                        + ",\"why\":\"" + JsonEsc(reason) + "\""
                        + ",\"err\":\"" + JsonEsc(lastErr) + "\""
                        + ",\"others\":" + othersActive
                        + ",\"peak\":" + peak.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture)
                        + ",\"fed\":" + fedRate
                        + ",\"fmt\":\"" + fmtDesc + "\""
                        + ",\"always\":" + (alwaysInject ? "true" : "false")
                        + ",\"dither\":" + (dither ? "true" : "false") + "}";
            if (!force && json == lastJson) return;
            lastJson = json;
            Console.Out.WriteLine("#ST" + json);
            Console.Out.Flush();
        }

        static string JsonEsc(string s)
        {
            if (s == null) return "";
            StringBuilder b = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') { b.Append('\\'); b.Append(c); }
                else if (c < ' ') b.Append(' ');
                else b.Append(c);
            }
            return b.ToString();
        }

        static void Run()
        {
            int idleMs = 0;
            long lastBeat = 0;
            long lastEval = -1000;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            while (!quit)
            {
                long now = sw.ElapsedMilliseconds;

                if (now - lastBeat > 2000)
                {
                    lastBeat = now;
                    if (fedT0 > 0 && now > fedT0) fedRate = (fedFrames * 1000) / (now - fedT0);
                    fedT0 = now;
                    fedFrames = 0;
                    Emit(true);
                }

                if (!enabled)
                {
                    if (injecting) { StopClient(); injecting = false; reason = "已关闭"; Emit(true); }
                    Thread.Sleep(200);
                    continue;
                }

                /* 喂流：独立于状态评估，保证缓冲区不断粮 */
                if (injecting) Feed();

                /* 状态评估：每 250 ms 一次 */
                if (now - lastEval >= 250)
                {
                    lastEval = now;

                    IMMDevice dev = FindEndpoint(match);
                    if (dev == null)
                    {
                        if (injecting) { StopClient(); injecting = false; }
                        epName = "";
                        reason = "找不到耳机端点";
                        Emit(false);
                        Thread.Sleep(500);
                        continue;
                    }

                    othersActive = CountOtherActive(dev);
                    peak = Peak(dev);

                    /* 真实内容判定：用端点电平，不用会话状态。
                       播放器暂停时仍会挂着 Active 会话写 0，光看会话会误判 */
                    bool realAudio = (peak >= PEAK_TH);

                    if (alwaysInject)
                    {
                        /* 常驻：A2DP 端点不支持独占模式，混入 -94 dBFS 不改变听感，
                           但能保证端点永不空闲，链路与 IMU 不会掉 */
                        if (!injecting)
                        {
                            if (StartClient(dev))
                            {
                                injecting = true;
                                reason = "常驻注入";
                                Emit(true);
                            }
                            else
                            {
                                reason = "启动失败，重试";
                                Emit(false);
                            }
                        }
                        else if (realAudio)
                        {
                            reason = "常驻注入 · 混有音频";
                        }
                        else
                        {
                            reason = "常驻注入";
                        }
                    }
                    else
                    {
                        /* --obey：真有声音就让位 */
                        if (realAudio)
                        {
                            idleMs = 0;
                            if (injecting)
                            {
                                StopClient();
                                injecting = false;
                                reason = "让位给真实音频";
                                Emit(true);
                            }
                        }
                        else
                        {
                            idleMs += 250;
                            if (!injecting && idleMs >= 300)
                            {
                                if (StartClient(dev))
                                {
                                    injecting = true;
                                    reason = "静音，注入保活";
                                    Emit(true);
                                }
                                else
                                {
                                    reason = "启动失败，重试";
                                    Emit(false);
                                }
                            }
                        }
                    }

                    try { Marshal.ReleaseComObject(dev); } catch { }
                }

                Thread.Sleep(10);
            }

            StopClient();
        }

        static float Peak(IMMDevice dev)
        {
            object o = null;
            try
            {
                Guid iid = typeof(IAudioMeterInformation).GUID;
                dev.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o);
                IAudioMeterInformation m = (IAudioMeterInformation)o;
                float p;
                m.GetPeakValue(out p);
                if (p < 0f) p = 0f;
                if (p > 1f) p = 1f;
                return p;
            }
            catch { return 0f; }
            finally { if (o != null) { try { Marshal.ReleaseComObject(o); } catch { } } }
        }

        static void Feed()
        {
            try
            {
                uint pad;
                client.GetCurrentPadding(out pad);
                if (pad >= bufFrames) return;
                uint avail = bufFrames - pad;
                if (avail == 0) return;
                IntPtr p;
                render.GetBuffer(avail, out p);
                Write(p, avail);
                render.ReleaseBuffer(avail, 0);
                fedFrames += avail;
            }
            catch
            {
                StopClient();
                injecting = false;
                reason = "写出失败，重建";
                Emit(true);
            }
        }

        static void Write(IntPtr p, uint frames)
        {
            int total = (int)frames * channels;
            if (!dither)
            {
                ZeroBytes(p, total * bytesPerSample);
                return;
            }
            if (isFloat)
            {
                // 交替 ±2e-5 浮点，约 -94 dBFS，听觉不可闻，规避固件的静音判定
                unsafeWriteFloat(p, total);
            }
            else if (bytesPerSample == 2)
            {
                writeShort(p, total);
            }
            else
            {
                ZeroBytes(p, total * bytesPerSample);
            }
        }

        static void ZeroBytes(IntPtr p, int bytes)
        {
            byte[] zeros = new byte[4096];
            int off = 0;
            while (off < bytes)
            {
                int n = bytes - off;
                if (n > 4096) n = 4096;
                Marshal.Copy(zeros, 0, (IntPtr)(p.ToInt64() + off), n);
                off += n;
            }
        }

        static byte[] scratchF = null;
        static byte[] scratchS = null;

        static void unsafeWriteFloat(IntPtr p, int samples)
        {
            int bytes = samples * 4;
            if (scratchF == null || scratchF.Length < bytes) scratchF = new byte[bytes];
            for (int i = 0; i < samples; i++)
            {
                float v = (i & 1) == 0 ? 2e-5f : -2e-5f;
                byte[] b = BitConverter.GetBytes(v);
                int o = i * 4;
                scratchF[o] = b[0]; scratchF[o + 1] = b[1];
                scratchF[o + 2] = b[2]; scratchF[o + 3] = b[3];
            }
            Marshal.Copy(scratchF, 0, p, bytes);
        }

        static void writeShort(IntPtr p, int samples)
        {
            int bytes = samples * 2;
            if (scratchS == null || scratchS.Length < bytes) scratchS = new byte[bytes];
            for (int i = 0; i < samples; i++)
            {
                short v = (short)(((i & 1) == 0) ? 1 : -1);
                scratchS[i * 2] = (byte)(v & 0xFF);
                scratchS[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            Marshal.Copy(scratchS, 0, p, bytes);
        }

        static IMMDeviceEnumerator NewEnumerator()
        {
            Type t = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            return (IMMDeviceEnumerator)Activator.CreateInstance(t);
        }

        static string FriendlyName(IMMDevice dev)
        {
            IPropertyStore ps = null;
            try
            {
                dev.OpenPropertyStore(STGM_READ, out ps);
                PROPERTYKEY key = new PROPERTYKEY();
                key.fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0");
                key.pid = 14;
                PROPVARIANT pv;
                ps.GetValue(ref key, out pv);
                if (pv.vt == 31 && pv.p1 != IntPtr.Zero) return Marshal.PtrToStringUni(pv.p1);
            }
            catch { }
            finally { if (ps != null) Marshal.ReleaseComObject(ps); }
            return "";
        }

        static IMMDevice FindEndpoint(string pat)
        {
            IMMDeviceEnumerator en = null;
            IMMDeviceCollection col = null;
            try
            {
                en = NewEnumerator();
                en.EnumAudioEndpoints(E_RENDER, DEVICE_STATE_ACTIVE, out col);
                int n;
                col.GetCount(out n);
                IMMDevice best = null;
                for (int i = 0; i < n; i++)
                {
                    IMMDevice d;
                    col.Item(i, out d);
                    string nm = FriendlyName(d);
                    bool hit = nm.IndexOf(pat, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool hfp = nm.IndexOf("Hands-Free", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (hit && !hfp)
                    {
                        if (best != null) Marshal.ReleaseComObject(best);
                        best = d;
                        epName = nm;
                    }
                    else Marshal.ReleaseComObject(d);
                }
                return best;
            }
            catch { return null; }
            finally
            {
                if (col != null) Marshal.ReleaseComObject(col);
                if (en != null) Marshal.ReleaseComObject(en);
            }
        }

        static int CountOtherActive(IMMDevice dev)
        {
            object o = null;
            IAudioSessionEnumerator se = null;
            int count = 0;
            uint mypid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            try
            {
                Guid iid = typeof(IAudioSessionManager2).GUID;
                dev.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o);
                IAudioSessionManager2 sam = (IAudioSessionManager2)o;
                sam.GetSessionEnumerator(out se);
                int n;
                se.GetCount(out n);
                for (int i = 0; i < n; i++)
                {
                    IAudioSessionControl2 c;
                    try { se.GetSession(i, out c); } catch { continue; }
                    if (c == null) continue;
                    try
                    {
                        if (IsSystemSession(c)) continue;

                        int st;
                        c.GetState(out st);
                        if (st != SESSION_ACTIVE) continue;
                        uint pid = 0;
                        try { c.GetProcessId(out pid); } catch { }
                        if (pid == mypid) continue;
                        if (pid == 0) continue;
                        count++;
                    }
                    catch { }
                    finally { try { Marshal.ReleaseComObject(c); } catch { } }
                }
            }
            catch { }
            finally
            {
                if (se != null) { try { Marshal.ReleaseComObject(se); } catch { } }
                if (o != null) { try { Marshal.ReleaseComObject(o); } catch { } }
            }
            return count;
        }

        /// <summary>系统音效会话永远在场，必须排除</summary>
        static bool IsSystemSession(IAudioSessionControl2 c)
        {
            try { if (c.IsSystemSoundsSession() == 0) return true; } catch { }
            try
            {
                string sid;
                c.GetSessionIdentifier(out sid);
                if (sid != null && sid.IndexOf("#%b", StringComparison.Ordinal) >= 0) return true;
            }
            catch { }
            return false;
        }

        static bool StartClient(IMMDevice dev)
        {
            object o = null;
            try
            {
                Guid iid = typeof(IAudioClient).GUID;
                dev.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o);
                IAudioClient ac = (IAudioClient)o;

                IntPtr fmtPtr;
                ac.GetMixFormat(out fmtPtr);
                WAVEFORMATEX fmt = (WAVEFORMATEX)Marshal.PtrToStructure(fmtPtr, typeof(WAVEFORMATEX));
                channels = fmt.nChannels;
                int bits = fmt.wBitsPerSample;
                bytesPerSample = bits / 8;
                isFloat = (fmt.wFormatTag == 3) || (fmt.wFormatTag == 0xFFFE && bits == 32);
                fmtDesc = "ch=" + channels + " bits=" + bits + " sr=" + fmt.nSamplesPerSec + " float=" + isFloat;
                Console.Out.WriteLine("#FMT " + fmtDesc);
                Console.Out.Flush();

                // 100 ms 缓冲，蓝牙链路上留足余量；format 直接传原始指针以保留 EXTENSIBLE 尾部
                try
                {
                    ac.Initialize(0, 0, 1000000, 0, fmtPtr, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(fmtPtr);
                }

                uint frames;
                ac.GetBufferSize(out frames);
                bufFrames = frames;

                Guid rid = typeof(IAudioRenderClient).GUID;
                object ro;
                ac.GetService(ref rid, out ro);
                IAudioRenderClient rc = (IAudioRenderClient)ro;

                client = ac;
                render = rc;
                ac.Start();
                return true;
            }
            catch (Exception ex)
            {
                lastErr = ex.Message;
                if (o != null) { try { Marshal.ReleaseComObject(o); } catch { } }
                client = null;
                render = null;
                return false;
            }
        }

        static void StopClient()
        {
            try { if (client != null) client.Stop(); } catch { }
            try { if (render != null) Marshal.ReleaseComObject(render); } catch { }
            try { if (client != null) Marshal.ReleaseComObject(client); } catch { }
            render = null;
            client = null;
        }

        static void DumpSessions()
        {
            IMMDevice dev = FindEndpoint(match);
            if (dev == null) { Console.WriteLine("endpoint not found for match=" + match); return; }
            Console.WriteLine("endpoint: " + epName);
            uint mypid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            Console.WriteLine("self pid = " + mypid);
            object o = null;
            IAudioSessionEnumerator se = null;
            try
            {
                Guid iid = typeof(IAudioSessionManager2).GUID;
                dev.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o);
                IAudioSessionManager2 sam = (IAudioSessionManager2)o;
                sam.GetSessionEnumerator(out se);
                int n;
                se.GetCount(out n);
                Console.WriteLine("session count = " + n);
                for (int i = 0; i < n; i++)
                {
                    IAudioSessionControl2 c;
                    try { se.GetSession(i, out c); }
                    catch (Exception ex) { Console.WriteLine("  [" + i + "] GET FAIL " + ex.GetType().Name); continue; }
                    if (c == null) { Console.WriteLine("  [" + i + "] null"); continue; }
                    int st = -1; uint pid = 0;
                    string sid = "", nm = "";
                    bool sys = IsSystemSession(c);
                    try { c.GetState(out st); } catch { }
                    try { c.GetProcessId(out pid); } catch { }
                    try { c.GetSessionIdentifier(out sid); } catch { }
                    try { c.GetDisplayName(out nm); } catch { }
                    Console.WriteLine("  [" + i + "] state=" + st + " pid=" + pid
                        + (pid == mypid ? " (SELF)" : "")
                        + (sys ? " (SYSTEM)" : "")
                        + " name=" + nm
                        + " id=" + sid);
                    try { Marshal.ReleaseComObject(c); } catch { }
                }
            }
            catch (Exception ex) { Console.WriteLine("DUMP FAIL: " + ex.Message); }
            finally
            {
                if (se != null) try { Marshal.ReleaseComObject(se); } catch { }
                if (o != null) try { Marshal.ReleaseComObject(o); } catch { }
            }
        }

        static void ListAll()
        {
            IMMDeviceEnumerator en = NewEnumerator();
            IMMDeviceCollection col;
            en.EnumAudioEndpoints(E_RENDER, DEVICE_STATE_ACTIVE, out col);
            int n;
            col.GetCount(out n);
            for (int i = 0; i < n; i++)
            {
                IMMDevice d;
                col.Item(i, out d);
                string nm = FriendlyName(d);
                bool hit = nm.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0;
                Console.WriteLine("[" + i + "] " + (hit ? "* " : "  ") + nm);
                if (hit) Console.WriteLine("      otherActive=" + CountOtherActive(d));
                Marshal.ReleaseComObject(d);
            }
        }
    }
}