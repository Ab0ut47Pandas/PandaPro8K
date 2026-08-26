using System.Runtime.InteropServices;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)]
    private struct HidAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public nint Reserved;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid guid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(nint handle, ref HidAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetInputReport(nint handle, byte[] report, int reportLength);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SetupDiGetClassDevsW(ref Guid classGuid, nint enumerator, nint window, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(nint deviceInfoSet, nint deviceInfoData,
        ref Guid interfaceClassGuid, uint memberIndex, ref DeviceInterfaceData interfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(nint deviceInfoSet,
        ref DeviceInterfaceData interfaceData, nint detailData, uint detailDataSize,
        out uint requiredSize, nint deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(nint deviceInfoSet,
        ref DeviceInterfaceData interfaceData, nint detailData, uint detailDataSize,
        nint requiredSize, nint deviceInfoData);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateFileW(string path, uint desiredAccess, uint shareMode,
        nint securityAttributes, uint creationDisposition, uint flags, nint template);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);

    private static readonly nint InvalidHandle = new(-1);
    private const uint Present = 0x02;
    private const uint DeviceInterface = 0x10;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareRead = 0x01;
    private const uint ShareWrite = 0x02;
    private const uint OpenExisting = 3;

    private static string? FindEdge()
    {
        HidD_GetHidGuid(out Guid hidGuid);
        nint devices = SetupDiGetClassDevsW(ref hidGuid, 0, 0, Present | DeviceInterface);
        if (devices == InvalidHandle) return null;

        try
        {
            for (uint index = 0; ; index++)
            {
                var entry = new DeviceInterfaceData { Size = Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(devices, 0, ref hidGuid, index, ref entry)) break;
                SetupDiGetDeviceInterfaceDetailW(devices, ref entry, 0, 0, out uint needed, 0);
                nint buffer = Marshal.AllocHGlobal((int)needed);
                try
                {
                    Marshal.WriteInt32(buffer, nint.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetailW(devices, ref entry, buffer, needed, 0, 0)) continue;
                    string? path = Marshal.PtrToStringUni(buffer + 4);
                    if (path is null) continue;
                    nint handle = Open(path);
                    if (handle == InvalidHandle) continue;
                    try
                    {
                        var attributes = new HidAttributes { Size = Marshal.SizeOf<HidAttributes>() };
                        if (HidD_GetAttributes(handle, ref attributes) &&
                            attributes.VendorId == 0x054C && attributes.ProductId == 0x0DF2)
                            return path;
                    }
                    finally { CloseHandle(handle); }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(devices); }
        return null;
    }

    private static nint Open(string path) => CreateFileW(path, GenericRead | GenericWrite,
        ShareRead | ShareWrite, 0, OpenExisting, 0, 0);

    private static int Main(string[] args)
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        int seconds = args.Select(value => int.TryParse(value, out int parsed) ? parsed : -1)
            .FirstOrDefault(value => value >= 0, 10);
        bool includeDevicePath = args.Contains("--include-device-path", StringComparer.OrdinalIgnoreCase);
        string? path = FindEdge();
        if (path is null)
        {
            Console.Error.WriteLine("Virtual DualSense Edge 054C:0DF2 was not accessible.");
            return 2;
        }

        Console.WriteLine(includeDevicePath
            ? $"path={path}"
            : "path=<redacted; use --include-device-path to print locally>");
        Console.WriteLine("utc,event,lx,ly,rx,ry,lt,rt,l2Digital,r2Digital,buttons8,buttons9,extras10,prefix");
        nint handle = Open(path);
        if (handle == InvalidHandle) return 3;
        try
        {
            DateTime end = DateTime.UtcNow.AddSeconds(seconds);
            DateTime nextHeartbeat = DateTime.MinValue;
            string? previousState = null;
            byte[] report = new byte[64];
            while (DateTime.UtcNow < end)
            {
                report[0] = 0x01;
                if (!HidD_GetInputReport(handle, report, report.Length))
                {
                    Console.Error.WriteLine($"GetInputReport failed: {Marshal.GetLastWin32Error()}");
                    return 4;
                }

                byte buttons = report[9];
                string state = $"{report[1]},{report[2]},{report[3]},{report[4]},{report[5]},{report[6]}," +
                    $"{((buttons & 0x04) != 0 ? 1 : 0)},{((buttons & 0x08) != 0 ? 1 : 0)}," +
                    $"0x{report[8]:X2},0x{buttons:X2},0x{report[10]:X2}";
                DateTime now = DateTime.UtcNow;
                bool changed = !string.Equals(state, previousState, StringComparison.Ordinal);
                if (changed || now >= nextHeartbeat)
                {
                    Console.WriteLine($"{now:O},{(changed ? "change" : "heartbeat")},{state}," +
                        Convert.ToHexString(report.AsSpan(0, 16)));
                    previousState = state;
                    nextHeartbeat = now.AddSeconds(1);
                }
                Thread.Sleep(1);
            }
        }
        finally { CloseHandle(handle); }
        return 0;
    }
}
