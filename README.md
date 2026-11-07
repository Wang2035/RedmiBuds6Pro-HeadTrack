# REDMI Buds 6 Pro 头部追踪

[中文](#中文) | [English](#english)

## 中文

把耳机里的 IMU 姿态数据直接接到 PC 上，实时驱动一个 3D 立方体。不依赖厂商 App，不经过音频通道做姿态，纯 HID 读取。

![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078d4)
![node](https://img.shields.io/badge/node-%E2%89%A5%2018-339933)
![deps](https://img.shields.io/badge/npm%20deps-0-brightgreen)
![size](https://img.shields.io/badge/package-110%20KB-lightgrey)
![license](https://img.shields.io/badge/license-MIT-blue)

---

## 这是什么

`REDMI Buds 6 Pro`（以及部分小米 / Redmi 音频设备）在系统里挂了一个隐藏的 HID 传感器接口，Feature Report 长度 40 字节，Input Report 长度 14 字节，上报率约 17.5 Hz。

厂商只在自家 App 里用它做空间音频。这个项目把它接管过来：

- 从 HID 接口读原始 6 组 int16
- 解算成 yaw / pitch / roll
- 通过本地 HTTP + SSE 推给浏览器
- 用 canvas 2D 画一个跟着头动的立方体

附带一个静音保活进程，解决「不播音乐就没数据」这个固件门控。

## 两个硬性前提

这两条是耳机固件决定的，PC 侧绕不过去，用之前必须先满足。

| 前提 | 说明 |
|---|---|
| **两只耳机都要戴上** | 耳机用内侧的电容式佩戴检测判断。只戴一只要么完全不出数据，要么短暂出几帧就停。摘下任何一只都会立刻停止上报。 |
| **数据来自主耳机** | 两只都戴上后，上报的是**先与电脑配对的那只**（主耳机 / primary）的 IMU 姿态。另一只是从机，只负责满足「双耳都已佩戴」这个条件。所以主耳机戴得歪、或者主耳机朝向和头不一致，姿态就是错的。 |

换个说法：**没有左右耳两路数据可选**。14 字节的报告里只有一组姿态，想换只让另一只耳机的数据上桌，只能把那只重新配成主耳机 —— 在 Windows 里删掉设备重连，让它先配对。

实测依据（400 帧，两只都戴着）：

| 字段 | 标准差 | 量程 | 判定 |
|---|---|---|---|
| a / b / c | 1033 / 470 / 761 | 3946 / 2333 / 2226 | 三个姿态轴 |
| d / e / f | 3 / 4 / 6 | 48 / 40 / 88 | 计数类字段，不是第二只耳机 |

前后两组交叉相关只有 0.07 – 0.22，说明后三个不是另一只耳机的同轴数据。

## 功能

| | |
|---|---|
| 实时姿态 | 17.5 Hz 原始上报，屏幕率插值渲染，不卡顿 |
| 抗抖 | 中值 5 点 + One Euro 自适应 + 静止锁定 + 限速 |
| 五点校准 | 采正前 / 左 / 右 / 抬头 / 低头，自动选轴、解增益 |
| 视角控制 | 拖动旋转、滚轮缩放、双击复位 |
| 静音保活 | 无人播放时注入 −88 dBFS 抖动，链路不断 |
| 零依赖 | 前端无框架无 CDN，服务端零 npm 包，整套 110 KB |
| 离线 | 双击 `start.bat` 即用，不联网 |

> 使用前确认：**两只耳机都戴上**，数据来自**先配对的那只**（主耳机）。

## 前置要求

| 项 | 要求 |
|---|---|
| 系统 | Windows 10 / 11 |
| 运行时 | Node.js 18 或更新（https://nodejs.org/ LTS） |
| 编译 | .NET Framework 4 自带的 `csc.exe`（Win10/11 内置，只有改源码才用得到） |
| 硬件 | REDMI Buds 6 Pro，或同款 HID 传感器接口的设备 |
| 佩戴 | **两只必须都戴在耳朵上**，只戴一只不出数据 |
| 连接 | 蓝牙配对完成，Windows 里能看到「HID 设备」节点 |
| 权限 | 平时无需管理员；HID 绑定坏掉时 `start.bat` 会请求一次 |

## 快速开始

```
1. 装 Node.js LTS
2. 蓝牙配对耳机（先配对的那只成为主耳机，姿态数据来自它）
3. 把两只耳机都戴上
4. 双击 start.bat
5. 浏览器自动打开 http://127.0.0.1:8899/
```

`start.bat` 会先查一次 HID 节点绑定。绑定坏了会自己修，代价是弹一次 UAC 让你点「是」。绑定正常就直接启动，不弹。

首次在新电脑上跑，先右键 `diag.ps1` → 使用 PowerShell 运行，确认第 3 节里 HID 节点状态是 `OK`。

## 使用方法

### 页面操作

| 操作 | 作用 |
|---|---|
| 鼠标拖动 | 旋转视角 |
| 滚轮 | 缩放（2.6 – 16） |
| 双击 | 视角复位 |

### 快捷键

| 键 | 作用 |
|---|---|
| `C` | 采「正前方」校准点 |
| `Q` / `E` | 采「向左转」/「向右转」 |
| `W` / `S` | 采「抬 头」/「低 头」 |
| `Z` | 倾斜开关 |
| `Shift`+`Z` | 倾斜方向反相 |
| `X` / `Y` | 左右 / 上下反相 |
| `1` `2` `3` | 手动指定 yaw 轴 |

校准点采完自动选轴、解增益，存进浏览器 localStorage，下次打开直接生效。

### 滑杆

| 滑杆 | 范围 | 作用 |
|---|---|---|
| 平滑 | 0 – 100 | 截止频率 6.0 Hz → 0.25 Hz，往右更顺 |
| 跟随 | 0 – 100 | One Euro 的 beta，0.002 → 0.10，往右更跟手 |
| 灵敏 | 20 – 300 | 角度总增益 |

### 保活开关

页面右下角「保活 开/关」。开着时状态行会显示：

| 文案 | 含义 |
|---|---|
| 注入中 · 端点名 | 正在写静音流，链路维持中 |
| 常驻注入 · 混有音频 | 你在放歌，静音流仍混在里面（听不出） |
| 已关闭 | 注入停止，不播音乐时姿态数据会停 |

### 命令行

```bat
KeepAlive.exe --list        列出渲染端点，* 标出匹配项
KeepAlive.exe --sessions    列出耳机端点上的音频会话
KeepAlive.exe --off         启动即关闭
KeepAlive.exe --obey        有真实音频时让出（按端点电平判断）
KeepAlive.exe --silent      写纯 0 代替抖动
KeepAlive.exe --match=文字   换端点匹配关键词
```

## 实现原理

### 数据从哪来

耳机在蓝牙 SDP 里暴露了一个 HID 服务，L2CAP PSM `0x0011`（HID Control）/ `0x0013`（HID Interrupt）。Windows 把它枚举成一个 HID 设备：

```
HID\{00001124-0000-1000-8000-00805f9b34fb}_VID&00010094_PID&0004
Usage Page 0x0020   Usage 0x00E1
InputReportByteLength   14
FeatureReportByteLength 40
```

`HidReader.exe` 用 `SetupDiGetClassDevs(GUID_DEVINTERFACE_HID)` 枚举设备，按 VID `0x0094` / PID `0x0004` 命中，`FILE_FLAG_OVERLAPPED` 非阻塞读。

### 启用握手

设备上电后不会主动上报，要先写一条 40 字节 Feature Report：

```
02 23 41 6E 64 72 6F 69 64 48 65 61 64 54 72 61 63 6B 65 72
23 31 2E 30 00 00 00 00 00 00 00 00 00
42 54 00 BB 43 1C F9 EC
```

拆开看：

| 段 | 内容 |
|---|---|
| `02` | report ID |
| `23` | 字面量 `'#'`，不是长度 |
| `41 6E … 72` | ASCII `AndroidHeadTracker` |
| `23 31 2E 30` | ASCII `#1.0` |
| `00` ×9 | 填充 |
| `42 54` | ASCII `BT` |
| `00` | 填充 |
| `BB 43 1C F9 EC` | 本机蓝牙 MAC 后 5 字节 |

整条串 ASCII 化后：`.#AndroidHeadTracker#1.0........BT..C...`

写完约 50 ms 起流。第一帧会是 16 个全零的热身帧，跳过就行。

### 佩戴检测这道闸

握手成功、音频也在流，仍然可能一条报告都没有 —— 那就卡在佩戴检测上。

耳机内侧是**电容式**传感器，靠对地电容的跳变判断有没有贴住耳道皮肤。固件的规则是：

| 状态 | 结果 |
|---|---|
| 两只都戴着 | 正常上报 |
| 只戴一只 | 不出数据，或出几帧就停 |
| 任意一只摘下 | 立即停止上报 |
| 两只都摘下 | 立即停止上报 |

两条结论：

第一，**融合需要两只都在位**。这只和「主耳机」并不矛盾：从机的 IMU 也参与，只是最终上报的姿态以主耳机为准。

第二，**PC 侧伪造不了**。判定发生在耳机固件内部，发生在上报之前 —— 不在耳，14 字节的报告根本不会生成，HID 层拿不到任何东西来改。想让耳机以为戴着，只能从物理侧让传感器看到人体电容（导电贴片 + 接皮肤），或者从厂商 RFCOMM 通道（`FE DC BA`）找固件开关。

### 数据来自主耳机

两只都戴上之后，HID 上只有**一组**姿态，来自先与电脑配对的那只 —— 主耳机（primary）。从机不单独上报。

想换一只当数据源，做法是在 Windows 设置里删掉这个设备重新配对，让目标那只先配对成功。这个顺序由耳机固件在配对时决定，PC 侧没有切换接口。

「左右耳切换」这件事在当前固件上做不到：报告里 6 组 int16 只有前三组是姿态，后三组是计数类字段（标准差 3 / 4 / 6，量程不到 90），不是第二只耳机的数据。

### 为什么需要静音保活

固件把 IMU 上报和音频通路绑在一起：没有 A2DP 流，姿态数据就停。

`KeepAlive.exe` 用 WASAPI 在耳机端点开一个共享模式 render client，常驻写 `±2e-5` 浮点（约 −88 dBFS）。端点永不空闲，Windows 不回收 A2DP，IMU 持续上报。

几处关键实现：

| 项 | 值 | 原因 |
|---|---|---|
| 混音格式 | 44.1 kHz / 32-bit float / 2ch | 从 `GetMixFormat` 原样取，按 `WAVEFORMATEXTENSIBLE` 传指针 |
| 缓冲 | 100 ms | 蓝牙链路要留余量 |
| 喂流周期 | 10 ms 轮询 `GetCurrentPadding` | 避免欠载 |
| 写内容 | 交替 `±2e-5` | 纯 0 会被部分固件当静音丢掉 |
| 判定方式 | `IAudioMeterInformation::GetPeakValue` | 会话状态不可靠，暂停的播放器仍是 Active |

早期版本按「会话是否 Active」判断有没有真实音频，结果暂停的播放器一直挂着 Active 会话写 0，保活永远让出，端点闲置，链路被回收。改成按端点电平判断后解决。

### 姿态解算与滤波

原始数据是 6 组 int16，其中前三组是姿态，后三组幅度极小（σ 约 20–50），当噪声丢掉。

```
raw → 5 点中值 → 差分（相对校准中心）→ 乘增益 → One Euro → 限速 → 插值渲染
```

滤波链参数：

| 环节 | 参数 | 效果 |
|---|---|---|
| 中值 5 点 | 窗口 5 | 挡单帧和两帧尖刺 |
| One Euro | minCut 0.84 Hz, beta 0.036, dCut 0.7 | 静止时轻、动起来跟手 |
| 静止锁定 | 6 帧量程 < 0.6° 时截止频率 ×0.22 | 静止抖动再压一档 |
| 限速 | 500 °/s | 挡多帧跳变，不影响真实转头 |

实测 420 帧真实数据：静止帧逐帧 Δ p90 从 0.088° 降到 0.035°，p99 从 0.505° 降到 0.051°；120° 阶跃 0→90% 用 286 ms，无超调。

### 五点校准

采五个点：`C` 正前、`Q` 左、`E` 右、`W` 抬头、`S` 低头。每点采 12 帧取中位。

轴自动判定：左右转动变化最大的那根轴 = yaw，剩下两根里抬头低头变化大的 = pitch，最后一根 = roll。

增益用最小二乘解：把 `(原始 Δ, 目标角度)` 两对点过原点拟合出 度/单位。**斜率只取绝对值**，方向交给 `X`/`Y`/`Shift+Z` 开关 —— 否则校准和反相开关会双重取反，转头方向全反。

roll 默认关闭。第三根轴会被 yaw 的运动交叉污染，开着会跟着转头歪。

### 渲染

canvas 2D 手写透视，不引 three.js：

```
8 顶点 → YXZ 欧拉旋转 → 相机空间 → 透视除法
6 个面按平均深度排序（画家算法）→ 填充
12 条棱按深度调亮度 → 描边
```

相机基向量只在视角变化时重算，正常渲染零三角函数。

实测 1920×1080 @ DPR 2：每帧 94 次 ctx 调用，JS 侧 0.030 ms，占 144 Hz 预算的 0.43%。瓶颈在耳机的 17.5 Hz 采样，不在渲染。

### 进程与数据流

```
HidReader.exe ──stdout CSV──▶ server.js ──SSE──▶ 浏览器
                                   │
KeepAlive.exe ──stdout 状态──▶ ────┘
```

`server.js` 监听 `127.0.0.1:8899`，端口占了就往后退（最多试 12 个）。路由：

| 路由 | 作用 |
|---|---|
| `/` | 页面 |
| `/sse` | 姿态数据流，17.5 条/秒 |
| `/status` | 读取器状态、样本数、最后一个样本 |
| `/ka` | 保活状态 |
| `/ka/on`、`/ka/off` | 保活开关 |

带 15 s 心跳和看门狗：读取器静默 15 s 就重启，保活掉线 8 s 重拉。

## 文件结构

```
headtrack/
├─ start.bat            启动器（内含 HID 绑定自检）
├─ server.js            本地服务 + SSE，零依赖
├─ headtrack_live.html  前端，canvas 2D
├─ HidReader.cs         HID 读取器源码
├─ HidReader.exe        编译产物
├─ KeepAlive.cs         静音保活源码
├─ KeepAlive.exe        编译产物
├─ fix-hid.ps1          HID 绑定修复（start.bat 按需调用）
├─ diag.ps1             排障脚本
├─ README.md
├─ README.txt
├─ LICENSE
└─ .gitignore
```

改完源码重新编译：

```bat
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe ^
  /platform:anycpu /optimize+ /out:HidReader.exe HidReader.cs

"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe ^
  /platform:anycpu /optimize+ /out:KeepAlive.exe KeepAlive.cs
```

## 协议速查

逆向过程中确认过的数字，改设备或排查时对得上：

| 项 | 值 |
|---|---|
| 设备地址 | `00:BB:43:1C:F9:EC` |
| HID VID / PID | `0x0094` / `0x0004` |
| Usage Page / Usage | `0x0020` / `0x00E1` |
| 上报率 | 17.5 Hz，中位间隔 58.0 ms（P10 44 / P90 71） |
| Input Report | 14 B = HIDP 头 `0xA1` + report ID `0x01` + 6×int16 |
| Feature Report | 40 B，首字节 `0x02` |
| 厂商 RFCOMM | DLCI 49，帧头 `FE DC BA` |
| 厂商 TLV | `FE DC BA | cmd(1) | op(u16le) | len(u16le) | data | FCS` |

`FE DC BA` 的几个 op：`0x0002` 设备信息（含 17 项 TLV）、`0x0009`、`0x000E`、`0x0050`、`0x0051`、`0x00F3`（序列号）、`0x00F4`。

## 常见问题

**页面一直「连接中」**

看 `start.bat` 那个窗口有没有报错。想直接确认就 `curl http://127.0.0.1:8899/status`。

**页面显示「没有数据」**

按这个顺序查：

0. **两只耳机都戴了吗？** 只戴一只不会出数据，摘下任何一只都会立刻停。这是最常见的原因，先确认这个。

1. `reader.log` 里出现 `ERR device_not_found VID_0094 PID_0004` —— HID 节点被绑到了 `SensorsHIDClassDriver` 并且起不来（`pc=10`）。机器重启、耳机重新配对之后会这样。`start.bat` 已经会自动修，你也可以手动：

   ```bat
   powershell -NoProfile -ExecutionPolicy Bypass -File fix-hid.ps1 --check
   :: 退出码 0 正常 / 10 坏了 / 3 节点不存在
   ```

   真坏了就右键 `fix-hid.ps1` → 使用 PowerShell 运行，会弹一次 UAC。修完日志里能看到 `RESULT: OK`。

2. `reader.log` 里 `#ARM` 一直重试但没有 `#STREAM` —— 设备能打开但不上报。先确认声音真的进了耳机：Windows 音量合成器里那个播放器是不是在往「REDMI Buds 6 Pro Stereo」输出，峰值不为零。

3. 都没有就 `diag.ps1` 看第 3 节节点状态。

**只戴一只耳机，或者摘下一只就没数据**

正常。固件要求两只都检测到佩戴才出姿态，而且融合需要两只的 IMU 都在位。这不是程序的问题，见「佩戴检测这道闸」一节。

**想用只戴一只的方式**

当前固件做不到。能试的只有物理侧让传感器以为戴着（电容式：导电贴片 + 导线接皮肤），或者从厂商 RFCOMM 通道找固件开关。PC 侧改不了。

**数据和另一只耳机对不上**

数据来自主耳机 —— 先与电脑配对的那只。想换数据源，在 Windows 里删掉设备重配对，让目标那只先连上。

**重启电脑后失效**

就是这个。Windows 每次重新枚举会生成新的 HID 子节点实例，传感器驱动 `SensorsHIDClassDriver` 抢绑并失败（`pc=10`）。双击 `start.bat` 即可，它会检查并修好，只弹一次 UAC。

**有数据但立方体不动**

停在零度就是校准中心还没建立，等 1 秒让它收 8 帧热身。或者你已经采过 `C` 点但头没动。

**姿态数据时断时续**

保活关了。右下角把它打开。

**立方体左右 / 上下反了**

`X` 管左右，`Y` 管上下。

**立方体跟着转头歪**

`Z` 关掉倾斜。

**抖得厉害**

「平滑」往右拉，「跟随」往左拉。

**改了源码编译失败**

`.cs` 文件用 UTF-8 存，别存成 UTF-16。`csc` 那条命令里路径要写全。

**.ps1 脚本里的中文路径失效**

PowerShell 5.1 按 ANSI 读 `.ps1`，脚本里出现中文路径或中文注释会解析失败。`diag.ps1` 特意写成纯 ASCII 就是为了这个。

## 已知限制

- 只有 Windows 实现。Linux 侧可以做，BlueZ 能直接开 L2CAP HIDP，但要另起一套。
- **必须两只耳机都戴上才会出数据**，只戴一只不行。摘下任何一只都会立即停止上报。
- **只有一组姿态，来自主耳机**（先与电脑配对的那只）。没有左右耳双路可选，也没有切换接口；想换数据源只能删设备重新配对。
- 佩戴检测是电容式，判定在耳机固件内部、发生在上报之前，PC 侧无法伪造。
- 上报率锁死在 17.5 Hz，这是耳机固件给的，PC 侧改不了。
- 姿态是单轴线性模型，头转 ±30° 以内准，大角度会有耦合误差。
- 「无常驻音频」这个前提没动过，所以保活进程要一直挂着。
- HID 节点靠 Windows 自动绑定。每次重新枚举（重启、重新配对）会生成新实例，`SensorsHIDClassDriver` 抢绑并失败，需要 `start.bat` 重新强绑一次。

## 后续可做

- Linux / BlueZ 版本
- 手机端直接读，走 Wi-Fi 把姿态推到 PC
- 录一段姿态导出 CSV / 回放
- 换成四元数解算，消掉大角度耦合
- 解厂商 RFCOMM 通道（`FE DC BA`）的 17 项 TLV，找佩戴检测使能位或左右耳独立数据
- 支持其它带 HID 传感器接口的耳机

## 技术栈

C#（.NET Framework 4，Win32 PnP / HID / WASAPI 互操作）· Node.js（原生 http，无框架）· 原生 JavaScript（canvas 2D，无框架无 CDN）

## 授权

MIT

---

## English

Feeds the earbuds' IMU attitude data straight to a PC and drives a 3D cube in real time. No vendor app, no attitude over the audio channel — pure HID reads.

![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078d4)
![node](https://img.shields.io/badge/node-%E2%89%A5%2018-339933)
![deps](https://img.shields.io/badge/npm%20deps-0-brightgreen)
![size](https://img.shields.io/badge/package-110%20KB-lightgrey)
![license](https://img.shields.io/badge/license-MIT-blue)

---

## What this is

`REDMI Buds 6 Pro` (and some other Xiaomi / Redmi audio devices) expose a hidden HID sensor interface in the system, with a Feature Report length of 40 bytes, an Input Report length of 14 bytes, and a report rate of about 17.5 Hz.

The vendor only uses it for spatial audio in its own app. This project takes it over:

- Read the raw 6 groups of int16 from the HID interface
- Solve them into yaw / pitch / roll
- Push them to the browser over local HTTP + SSE
- Draw a cube that moves with your head using canvas 2D

It also comes with a silent keep-alive process, which solves the firmware gate of "no music playing means no data".

## Two hard requirements

These two are dictated by the earbud firmware; the PC side cannot get around them, and they must be satisfied before use.

| Requirement | Details |
|---|---|
| **Both earbuds must be worn** | The earbuds judge wearing with the capacitive wear detection on their inner side. Wearing only one either produces no data at all, or a few frames and then it stops. Removing either one stops reporting immediately. |
| **Data comes from the primary earbud** | When both are worn, what is reported is the IMU attitude of **the one that paired with the computer first** (the primary earbud — the earbud that paired first; the other one is the secondary). The other one is the secondary and only serves to satisfy the "both earbuds worn" condition. So if the primary earbud is worn crooked, or the primary earbud's orientation does not match the head, the attitude is wrong. |

In other words: **there is no choice between two channels of left/right earbud data**. The 14-byte report holds only one set of attitude; to bring the other earbud's data to the table instead, the only way is to make that one the primary earbud again — delete the device in Windows and reconnect, letting it pair first.

Measured basis (400 frames, both earbuds worn):

| Field | Standard deviation | Range | Verdict |
|---|---|---|---|
| a / b / c | 1033 / 470 / 761 | 3946 / 2333 / 2226 | The three attitude axes |
| d / e / f | 3 / 4 / 6 | 48 / 40 / 88 | Counter-like fields, not the second earbud |

The cross-correlation between the front and back groups is only 0.07 – 0.22, which shows the last three are not same-axis data from the other earbud.

## Features

| | |
|---|---|
| Real-time attitude | 17.5 Hz raw reporting, interpolated rendering at screen rate, no stutter |
| Anti-jitter | 5-point median + One Euro adaptive + stillness lock + rate limiting |
| Five-point calibration | Capture front / left / right / look up / look down, automatic axis selection and gain solving |
| View control | Drag to rotate, wheel to zoom, double-click to reset |
| Silent keep-alive | Injects −88 dBFS dither when nothing is playing, so the link stays up |
| Zero dependencies | No frontend framework and no CDN, zero npm packages on the server side, the whole package is 110 KB |
| Offline | Just double-click `start.bat`, no network needed |

> Before use, confirm: **both earbuds are worn**, and the data comes from **the one that paired first** (the primary earbud).

## Requirements

| Item | Requirement |
|---|---|
| System | Windows 10 / 11 |
| Runtime | Node.js 18 or newer (https://nodejs.org/ LTS) |
| Building | The `csc.exe` bundled with .NET Framework 4 (built into Win10/11, only needed if you modify the source) |
| Hardware | REDMI Buds 6 Pro, or a device with the same HID sensor interface |
| Wearing | **Both must be worn in your ears**; wearing only one produces no data |
| Connection | Bluetooth pairing completed, and an "HID device" node visible in Windows |
| Permissions | No administrator needed normally; `start.bat` will ask once when the HID binding is broken |

## Quick start

```
1. Install Node.js LTS
2. Pair the earbuds over Bluetooth (the one that pairs first becomes the primary earbud, and the attitude data comes from it)
3. Put both earbuds on
4. Double-click start.bat
5. The browser automatically opens http://127.0.0.1:8899/
```

`start.bat` first checks the HID node binding once. If the binding is broken it repairs it by itself, at the cost of one UAC prompt for you to click "Yes". If the binding is fine, it starts directly, with no prompt.

On the first run on a new computer, first right-click `diag.ps1` → Run with PowerShell, and confirm that the HID node status in section 3 is `OK`.

## Usage

### Page controls

| Action | Effect |
|---|---|
| Mouse drag | Rotate the view |
| Wheel | Zoom (2.6 – 16) |
| Double-click | Reset the view |

### Keyboard shortcuts

| Key | Effect |
|---|---|
| `C` | Capture the "straight ahead" calibration point |
| `Q` / `E` | Capture "turn left" / "turn right" |
| `W` / `S` | Capture "look up" / "look down" |
| `Z` | Tilt toggle |
| `Shift`+`Z` | Reverse the tilt direction |
| `X` / `Y` | Reverse left/right / up/down |
| `1` `2` `3` | Manually assign the yaw axis |

Once the calibration points are captured, axis selection and gain solving happen automatically, are stored in the browser's localStorage, and take effect directly on the next open.

### Sliders

| Slider | Range | Effect |
|---|---|---|
| Smoothness | 0 – 100 | Cutoff frequency 6.0 Hz → 0.25 Hz; further right is smoother |
| Tracking | 0 – 100 | The One Euro beta, 0.002 → 0.10; further right tracks more closely |
| Sensitivity | 20 – 300 | Overall angle gain |

### Keep-alive toggle

The "Keep-alive On/Off" toggle at the bottom right of the page. When it is on, the status line shows:

| Text | Meaning |
|---|---|
| Injecting · endpoint name | Writing the silent stream, the link is being maintained |
| Always-on injection · mixed with audio | You are playing music, and the silent stream is still mixed in (inaudibly) |
| Off | Injection stopped; attitude data will stop when no music is playing |

### Command line

```bat
KeepAlive.exe --list        List the render endpoints, * marks the matches
KeepAlive.exe --sessions    List the audio sessions on the earbud endpoint
KeepAlive.exe --off         Start with it off
KeepAlive.exe --obey        Yield when there is real audio (judged by endpoint level)
KeepAlive.exe --silent      Write pure 0 instead of dither
KeepAlive.exe --match=text  Change the endpoint matching keyword
```

## How it works

### Where the data comes from

The earbuds expose an HID service in the Bluetooth SDP, at L2CAP PSM `0x0011` (HID Control) / `0x0013` (HID Interrupt). Windows enumerates it as an HID device:

```
HID\{00001124-0000-1000-8000-00805f9b34fb}_VID&00010094_PID&0004
Usage Page 0x0020   Usage 0x00E1
InputReportByteLength   14
FeatureReportByteLength 40
```

`HidReader.exe` enumerates devices with `SetupDiGetClassDevs(GUID_DEVINTERFACE_HID)`, matches on VID `0x0094` / PID `0x0004`, and reads non-blockingly with `FILE_FLAG_OVERLAPPED`.

### The enabling handshake

After power-on the device does not report on its own; you first have to write a 40-byte Feature Report:

```
02 23 41 6E 64 72 6F 69 64 48 65 61 64 54 72 61 63 6B 65 72
23 31 2E 30 00 00 00 00 00 00 00 00 00
42 54 00 BB 43 1C F9 EC
```

Broken down:

| Segment | Content |
|---|---|
| `02` | report ID |
| `23` | The literal `'#'`, not a length |
| `41 6E … 72` | ASCII `AndroidHeadTracker` |
| `23 31 2E 30` | ASCII `#1.0` |
| `00` ×9 | Padding |
| `42 54` | ASCII `BT` |
| `00` | Padding |
| `BB 43 1C F9 EC` | The last 5 bytes of the local Bluetooth MAC |

The whole string, turned into ASCII: `.#AndroidHeadTracker#1.0........BT..C...`

Streaming starts about 50 ms after the write. The first frame will be 16 all-zero warm-up frames — just skip them.

### The wear-detection gate

The handshake succeeds, the audio is flowing too, and there may still be not a single report — that is where it gets stuck on wear detection.

The inside of the earbud is a **capacitive** sensor, which judges by the jump in capacitance to ground whether it is against the skin of the ear canal. The firmware's rules are:

| State | Result |
|---|---|
| Both worn | Reports normally |
| Only one worn | No data, or a few frames and then it stops |
| Either one removed | Stops reporting immediately |
| Both removed | Stops reporting immediately |

Two conclusions:

First, **fusion requires both to be in place**. This does not contradict the "primary earbud": the secondary's IMU also takes part, it is just that the finally reported attitude is based on the primary earbud.

Second, **it cannot be faked from the PC side**. The judgement happens inside the earbud firmware, before reporting — if it is not in the ear, the 14-byte report is never generated at all, and the HID layer has nothing to modify. To make the earbuds think they are worn, the only options are the physical side, letting the sensor see human capacitance (a conductive patch + a wire to the skin), or finding a firmware switch via the vendor RFCOMM channel (`FE DC BA`).

### Data comes from the primary earbud

Once both are worn, there is only **one set** of attitude on HID, from the one that paired with the computer first — the primary earbud (primary). The secondary does not report separately.

To switch which one is the data source, delete this device in Windows Settings and pair again, letting the target one pair successfully first. This order is decided by the earbud firmware at pairing time; there is no switching interface on the PC side.

"Switching between the left and right earbud" cannot be done on the current firmware: of the 6 groups of int16 in the report, only the first three groups are attitude, and the last three groups are counter-like fields (standard deviation 3 / 4 / 6, range under 90), not the second earbud's data.

### Why silent keep-alive is needed

The firmware ties IMU reporting to the audio path: with no A2DP stream, the attitude data stops.

`KeepAlive.exe` uses WASAPI to open a shared-mode render client on the earbud endpoint and constantly writes `±2e-5` floats (about −88 dBFS). The endpoint is never idle, Windows does not reclaim the A2DP, and the IMU keeps reporting.

A few key implementation details:

| Item | Value | Reason |
|---|---|---|
| Mix format | 44.1 kHz / 32-bit float / 2ch | Taken as-is from `GetMixFormat`, the pointer passed as `WAVEFORMATEXTENSIBLE` |
| Buffer | 100 ms | Leave headroom for the Bluetooth link |
| Feed cycle | Poll `GetCurrentPadding` every 10 ms | Avoid underruns |
| Written content | Alternating `±2e-5` | Pure 0 may be treated as silence and dropped by some firmware |
| Decision method | `IAudioMeterInformation::GetPeakValue` | Session state is unreliable; a paused player is still Active |

Early versions judged whether there was real audio by "whether the session is Active"; as a result, a paused player kept hanging there with an Active session writing 0, the keep-alive always yielded, the endpoint went idle, and the link was reclaimed. Switching to judging by endpoint level fixed it.

### Attitude solving and filtering

The raw data is 6 groups of int16, of which the first three groups are attitude, and the last three groups have extremely small amplitude (σ about 20–50) and are discarded as noise.

```
raw → 5-point median → difference (relative to the calibration center) → multiply by gain → One Euro → rate limiting → interpolated rendering
```

Filter chain parameters:

| Stage | Parameters | Effect |
|---|---|---|
| 5-point median | Window 5 | Blocks single-frame and two-frame spikes |
| One Euro | minCut 0.84 Hz, beta 0.036, dCut 0.7 | Light when still, tracks when moving |
| Stillness lock | When the 6-frame range is < 0.6°, the cutoff frequency is ×0.22 | Presses still jitter down another notch |
| Rate limiting | 500 °/s | Blocks multi-frame jumps, does not affect real head turns |

Measured on 420 frames of real data: the per-frame Δ of still frames went from p90 0.088° down to 0.035°, and p99 from 0.505° down to 0.051°; a 120° step takes 286 ms for 0→90%, with no overshoot.

### Five-point calibration

Capture five points: `C` front, `Q` left, `E` right, `W` look up, `S` look down. Each point captures 12 frames and takes the median.

Automatic axis determination: the axis that changes most when turning left and right = yaw; of the remaining two, the one that changes a lot when looking up and down = pitch; the last one = roll.

The gain is solved by least squares: fit the two pairs of `(raw Δ, target angle)` points through the origin to get degrees/unit. **The slope takes only its absolute value**, and the direction is left to the `X`/`Y`/`Shift+Z` switches — otherwise calibration and the reversal switches would invert twice, and the head-turn direction would be completely reversed.

roll is off by default. The third axis gets cross-contaminated by yaw motion, and with it on it will tilt along as you turn your head.

### Rendering

Hand-written perspective in canvas 2D, without bringing in three.js:

```
8 vertices → YXZ Euler rotation → camera space → perspective division
6 faces sorted by average depth (painter's algorithm) → fill
12 edges with brightness adjusted by depth → stroke
```

The camera basis vectors are only recomputed when the view changes; normal rendering uses zero trigonometric functions.

Measured at 1920×1080 @ DPR 2: 94 ctx calls per frame, 0.030 ms on the JS side, taking 0.43% of the 144 Hz budget. The bottleneck is the earbuds' 17.5 Hz sampling, not the rendering.

### Processes and data flow

```
HidReader.exe ──stdout CSV──▶ server.js ──SSE──▶ browser
                                   │
KeepAlive.exe ──stdout status▶ ────┘
```

`server.js` listens on `127.0.0.1:8899`, and if the port is taken it backs off (trying at most 12). Routes:

| Route | Purpose |
|---|---|
| `/` | The page |
| `/sse` | The attitude data stream, 17.5 messages/second |
| `/status` | Reader status, sample count, and the last sample |
| `/ka` | Keep-alive status |
| `/ka/on`, `/ka/off` | Keep-alive on/off |

It has a 15 s heartbeat and a watchdog: if the reader is silent for 15 s it is restarted, and if the keep-alive drops, it is re-pulled after 8 s.

## File structure

```
headtrack/
├─ start.bat            Launcher (includes the HID binding self-check)
├─ server.js            Local service + SSE, zero dependencies
├─ headtrack_live.html  Frontend, canvas 2D
├─ HidReader.cs         HID reader source
├─ HidReader.exe        Compiled binary
├─ KeepAlive.cs         Silent keep-alive source
├─ KeepAlive.exe        Compiled binary
├─ fix-hid.ps1          HID binding repair (called by start.bat as needed)
├─ diag.ps1             Troubleshooting script
├─ README.md
├─ README.txt
├─ LICENSE
└─ .gitignore
```

To recompile after modifying the source:

```bat
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe ^
  /platform:anycpu /optimize+ /out:HidReader.exe HidReader.cs

"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe ^
  /platform:anycpu /optimize+ /out:KeepAlive.exe KeepAlive.cs
```

## Protocol quick reference

The numbers confirmed during reverse engineering, to check against when changing devices or troubleshooting:

| Item | Value |
|---|---|
| Device address | `00:BB:43:1C:F9:EC` |
| HID VID / PID | `0x0094` / `0x0004` |
| Usage Page / Usage | `0x0020` / `0x00E1` |
| Report rate | 17.5 Hz, median interval 58.0 ms (P10 44 / P90 71) |
| Input Report | 14 B = HIDP header `0xA1` + report ID `0x01` + 6×int16 |
| Feature Report | 40 B, first byte `0x02` |
| Vendor RFCOMM | DLCI 49, frame header `FE DC BA` |
| Vendor TLV | `FE DC BA | cmd(1) | op(u16le) | len(u16le) | data | FCS` |

A few of the `FE DC BA` ops: `0x0002` device information (containing 17 TLV items), `0x0009`, `0x000E`, `0x0050`, `0x0051`, `0x00F3` (serial number), `0x00F4`.

## FAQ

**The page is stuck on "connecting"**

Look at whether the `start.bat` window shows an error. To confirm directly, `curl http://127.0.0.1:8899/status`.

**The page shows "no data"**

Check in this order:

0. **Are both earbuds worn?** Wearing only one produces no data, and removing either one stops it immediately. This is the most common cause; confirm this first.

1. `reader.log` shows `ERR device_not_found VID_0094 PID_0004` — the HID node got bound to `SensorsHIDClassDriver` and cannot start (`pc=10`). This happens after a machine reboot or after re-pairing the earbuds. `start.bat` already repairs it automatically, and you can also do it manually:

   ```bat
   powershell -NoProfile -ExecutionPolicy Bypass -File fix-hid.ps1 --check
   :: Exit code 0 normal / 10 broken / 3 node does not exist
   ```

   If it really is broken, right-click `fix-hid.ps1` → Run with PowerShell, and one UAC prompt will appear. After the repair you can see `RESULT: OK` in the log.

2. In `reader.log`, `#ARM` keeps retrying but there is no `#STREAM` — the device can be opened but does not report. First confirm the sound really goes into the earbuds: in the Windows volume mixer, is that player outputting to "REDMI Buds 6 Pro Stereo", with a non-zero peak?

3. If neither applies, look at the node status in section 3 of `diag.ps1`.

**Wearing only one earbud, or removing one, gives no data**

That is normal. The firmware requires both to be detected as worn before it outputs attitude, and the fusion needs both earbuds' IMUs in place. This is not the program's problem; see the "The wear-detection gate" section.

**I want to use it wearing only one**

It cannot be done on the current firmware. The only things to try are the physical side, making the sensor think it is worn (capacitive: a conductive patch + a wire to the skin), or finding a firmware switch via the vendor RFCOMM channel. It cannot be changed from the PC side.

**The data does not match the other earbud**

The data comes from the primary earbud — the one that paired with the computer first. To change the data source, delete the device in Windows and pair again, letting the target one connect first.

**It stops working after rebooting the computer**

That is this. Every time Windows re-enumerates, it generates a new HID child node instance, and the sensor driver `SensorsHIDClassDriver` grabs the binding and fails (`pc=10`). Just double-click `start.bat`; it checks and repairs it, with only one UAC prompt.

**There is data but the cube does not move**

Staying at zero degrees means the calibration center has not been established yet; wait 1 second and let it collect 8 warm-up frames. Or you have already captured the `C` point but your head has not moved.

**The attitude data is intermittent**

The keep-alive is off. Turn it on at the bottom right.

**The cube is reversed left/right or up/down**

`X` controls left/right, `Y` controls up/down.

**The cube tilts along as I turn my head**

Turn tilt off with `Z`.

**It jitters badly**

Pull "Smoothness" to the right, and "Tracking" to the left.

**Rebuilding modified source fails**

Save `.cs` files as UTF-8, not UTF-16. Write the full path in that `csc` command.

**Chinese paths in .ps1 scripts stop working**

PowerShell 5.1 reads `.ps1` as ANSI, so if a Chinese path or a Chinese comment appears in a script it will fail to parse. `diag.ps1` is deliberately written in pure ASCII for exactly this reason.

## Known limitations

- Only a Windows implementation. The Linux side could be done — BlueZ can open L2CAP HIDP directly — but it would be a separate set.
- **Both earbuds must be worn for data to come out**; wearing only one does not work. Removing either one stops reporting immediately.
- **There is only one set of attitude, from the primary earbud** (the one that paired with the computer first). There is no choice between two left/right channels, and no switching interface; to change the data source you can only delete the device and pair again.
- Wear detection is capacitive, and the judgement happens inside the earbud firmware, before reporting, so it cannot be faked from the PC side.
- The report rate is locked at 17.5 Hz; that is what the earbud firmware gives, and the PC side cannot change it.
- The attitude is a single-axis linear model; it is accurate within ±30° of head turning, and large angles will have coupling errors.
- The "no always-on audio" premise has not been touched, so the keep-alive process has to stay running.
- The HID node relies on Windows auto-binding. Every re-enumeration (reboot, re-pairing) generates a new instance, `SensorsHIDClassDriver` grabs the binding and fails, and `start.bat` needs to force-bind it once again.

## Future work

- A Linux / BlueZ version
- Read directly on the phone side, and push the attitude to the PC over Wi-Fi
- Record a stretch of attitude and export it to CSV / replay it
- Switch to quaternion solving, to eliminate the large-angle coupling
- Decode the 17 TLV items of the vendor RFCOMM channel (`FE DC BA`), and look for a wear-detection enable bit or independent left/right earbud data
- Support other earbuds with an HID sensor interface

## Tech stack

C# (.NET Framework 4, Win32 PnP / HID / WASAPI interop) · Node.js (native http, no framework) · Native JavaScript (canvas 2D, no framework and no CDN)

## License

MIT
