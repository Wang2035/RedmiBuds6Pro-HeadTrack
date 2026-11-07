using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class HidReader
{
    const uint GENERIC_READ = 0x80000000u;
    const uint GENERIC_WRITE = 0x40000000u;
    const uint FILE_SHARE_RW = 3u;
    const uint OPEN_EXISTING = 3u;
    const uint FILE_FLAG_OVERLAPPED = 0x40000000u;
    const int DIGCF_PRESENT = 0x02;
    const int DIGCF_DEVICEINTERFACE = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr hEvent;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, int detailSize, out int requiredSize, IntPtr deviceInfoData);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr h, byte[] buf, int n, out int got, ref OVERLAPPED ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetOverlappedResult(IntPtr h, ref OVERLAPPED ov, out int got, bool wait);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateEvent(IntPtr sec, bool manual, bool init, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr h, uint ms);

    [DllImport("hid.dll")] static extern bool HidD_GetAttributes(IntPtr h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll")] static extern bool HidD_SetFeature(IntPtr h, byte[] b, int len);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr pp);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr pp);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr pp, byte[] caps);
    [DllImport("hid.dll")] static extern bool HidD_SetNumInputBuffers(IntPtr h, int n);

    static readonly ushort TARGET_VID = 0x0094;
    static readonly ushort TARGET_PID = 0x0004;

    static readonly byte[] EnableBig = HexToBytes(
        "0223416E64726F696448656164547261636B657223312E300000000000000000425400BB431CF9EC");
    static readonly byte[] EnableSmall = new byte[] { 0x01, 0x00 };

    static byte[] HexToBytes(string s)
    {
        byte[] b = new byte[s.Length / 2];
        for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
        return b;
    }

    /// <summary>
    /// HidD_SetFeature 在 HIDP 通道不通时会无限挂起（重叠句柄下则假装成功）。
    /// 放到工作线程加超时，把「卡死」变成「明确的失败」。
    /// </summary>
    static bool ArmTimed(IntPtr h, byte[] payload, int timeoutMs)
    {
        bool ok = false;
        bool done = false;
        Thread t = new Thread(delegate ()
        {
            try { ok = HidD_SetFeature(h, payload, payload.Length); }
            catch { ok = false; }
            done = true;
        });
        t.IsBackground = true;
        t.Start();
        if (!t.Join(timeoutMs))
        {
            Log("#ARM timeout " + timeoutMs + "ms len=" + payload.Length);
            return false;
        }
        return done && ok;
    }

    static void Log(string s) { Console.Error.WriteLine(s); Console.Error.Flush(); }

    static string FindDevice()
    {
        Guid hidGuid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
        IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == new IntPtr(-1)) return null;
        try
        {
            SP_DEVICE_INTERFACE_DATA did = new SP_DEVICE_INTERFACE_DATA();
            did.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
            for (int i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref did); i++)
            {
                int need = 0;
                SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, out need, IntPtr.Zero);
                if (need <= 0) continue;
                IntPtr buf = Marshal.AllocHGlobal(need);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref did, buf, need, out need, IntPtr.Zero)) continue;
                    string p = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                    if (string.IsNullOrEmpty(p)) continue;

                    IntPtr h = CreateFile(p, 0, FILE_SHARE_RW, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (h == new IntPtr(-1)) continue;
                    try
                    {
                        HIDD_ATTRIBUTES at = new HIDD_ATTRIBUTES();
                        at.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                        if (HidD_GetAttributes(h, ref at) && at.VendorID == TARGET_VID && at.ProductID == TARGET_PID)
                            return p;
                    }
                    finally { CloseHandle(h); }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return null;
    }

    // one open->arm->stream attempt; returns true if any report was delivered
    static bool Session(out bool streamed)
    {
        streamed = false;
        string path = FindDevice();
        if (path == null) { Log("ERR device_not_found VID_0094 PID_0004"); Thread.Sleep(1500); return false; }

        IntPtr h = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_RW, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (h == new IntPtr(-1)) { Log("ERR open_failed " + Marshal.GetLastWin32Error()); Thread.Sleep(800); return false; }

        IntPtr ev = CreateEvent(IntPtr.Zero, true, false, null);
        try
        {
            Thread.Sleep(250);

            IntPtr pp;
            int inLen = 14;
            if (HidD_GetPreparsedData(h, out pp))
            {
                byte[] c = new byte[128];
                if (HidP_GetCaps(pp, c) >= 0)
                {
                    int l = BitConverter.ToUInt16(c, 4);
                    if (l > 0) inLen = l;
                }
                HidD_FreePreparsedData(pp);
            }
            HidD_SetNumInputBuffers(h, 256);

            /* 重叠句柄上 HidD_SetFeature 会立刻返回 True，不管设备有没有 ACK。
               另开一个同步句柄做真正的写入，才能测出 HIDP 控制通道是死是活。 */
            IntPtr hs = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_RW, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            IntPtr armH = (hs == new IntPtr(-1)) ? h : hs;
            if (armH == h) Log("#ARM sync_handle_unavailable, fallback overlapped");

            bool a = ArmTimed(armH, EnableSmall, 2000);
            Thread.Sleep(50);
            bool b = ArmTimed(armH, EnableBig, 3000);
            bool armed = a && b;
            Log("#ARM small=" + a + " big=" + b + " inLen=" + inLen);
            if (!armed)
            {
                Log("#ARM blocked HIDP control channel");
            }

            long lastRx = 0;
            long lastArm = Environment.TickCount;
            int arms = 1;
            byte[] buf = new byte[inLen];

            while (true)
            {
                long now = Environment.TickCount;
                bool have = lastRx != 0;

                if (!have && now - lastArm > 2500)
                {
                    if (arms >= 4 || !armed)
                    {
                        Log("#REOPEN no stream after " + arms + " arms");
                        return true;            // ask caller to reopen
                    }
                    bool ra = ArmTimed(armH, EnableSmall, 2000);
                    Thread.Sleep(50);
                    bool rb = ArmTimed(armH, EnableBig, 3000);
                    armed = ra && rb;
                    lastArm = Environment.TickCount;
                    arms++;
                    Log("#ARM retry=" + arms + " ok=" + armed);
                }
                else if (have && now - lastRx > 2500 && now - lastArm > 2500)
                {
                    bool ra2 = ArmTimed(armH, EnableSmall, 2000);
                    Thread.Sleep(50);
                    ArmTimed(armH, EnableBig, 3000);
                    lastArm = Environment.TickCount;
                    Log("#ARM stale_retry");
                }

                OVERLAPPED ov = new OVERLAPPED();
                ov.hEvent = ev;
                int got;
                if (!ReadFile(h, buf, inLen, out got, ref ov))
                {
                    int e = Marshal.GetLastWin32Error();
                    if (e != 997) { Log("ERR read " + e); return false; }
                    if (WaitForSingleObject(ev, 600) != 0) continue;
                    if (!GetOverlappedResult(h, ref ov, out got, false)) { Log("ERR ovl " + Marshal.GetLastWin32Error()); return false; }
                }
                if (got < 13) continue;

                if (!have) { Log("#STREAM first_report_after_arms=" + arms); streamed = true; }
                lastRx = Environment.TickCount;

                int v1 = (short)(buf[1] | (buf[2] << 8));
                int v2 = (short)(buf[3] | (buf[4] << 8));
                int v3 = (short)(buf[5] | (buf[6] << 8));
                int v4 = (short)(buf[7] | (buf[8] << 8));
                int v5 = (short)(buf[9] | (buf[10] << 8));
                int v6 = (short)(buf[11] | (buf[12] << 8));

                StringBuilder sb = new StringBuilder(64);
                sb.Append(lastRx).Append(',')
                  .Append(v1).Append(',').Append(v2).Append(',').Append(v3).Append(',')
                  .Append(v4).Append(',').Append(v5).Append(',').Append(v6);
                Console.WriteLine(sb.ToString());
                Console.Out.Flush();
            }
        }
        finally
        {
            if (ev != IntPtr.Zero) CloseHandle(ev);
            CloseHandle(h);
        }
    }

    static int Main(string[] args)
    {
        Log("#BOOT HidReader");
        while (true)
        {
            bool streamed = false;
            try { Session(out streamed); }
            catch (Exception ex) { Log("ERR exc " + ex.Message); }
            Thread.Sleep(streamed ? 200 : 400);
        }
    }
}