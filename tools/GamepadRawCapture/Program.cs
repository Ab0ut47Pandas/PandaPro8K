using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private const uint WmInput = 0x00FF;
    private const uint WmClose = 0x0010;
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidevInputSink = 0x00000100;
    private const uint RimTypeHid = 2;
    private static readonly nint HwndMessage = new(-3);
    private static readonly Dictionary<nint, string> DeviceNames = new();
    private static readonly Dictionary<nint, byte[]> LastReports = new();
    private static readonly WndProc WindowProcedure = HandleWindowMessage;
    private static readonly long StartTicks = Stopwatch.GetTimestamp();
    private static DateTime _endUtc;
    private static nint _window;
    private static bool _includeDevicePath;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public nint WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int PointX;
        public int PointY;
        public uint Private;
    }

    private delegate nint WndProc(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices([In] RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(nint rawInput, uint command, nint data, ref uint size, uint headerSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(nint device, uint command, StringBuilder? data, ref uint size);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out Message message, nint window, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);

    private static int Main(string[] args)
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        int seconds = args.Select(value => int.TryParse(value, out int parsed) ? parsed : -1)
            .FirstOrDefault(value => value >= 0, 300);
        _includeDevicePath = args.Contains("--include-device-path", StringComparer.OrdinalIgnoreCase);
        _endUtc = DateTime.UtcNow.AddSeconds(seconds);

        string className = $"PandaGamepadRawCapture_{Environment.ProcessId}";
        nint instance = GetModuleHandle(null);
        WindowClass windowClass = new()
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Instance = instance,
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProcedure),
            ClassName = className
        };

        if (RegisterClassEx(ref windowClass) == 0)
        {
            return Fail("RegisterClassEx");
        }

        _window = CreateWindowEx(0, className, className, 0, 0, 0, 0, 0, HwndMessage, 0, instance, 0);
        if (_window == 0)
        {
            return Fail("CreateWindowEx");
        }

        RawInputDevice[] devices =
        [
            new() { UsagePage = 0x01, Usage = 0x04, Flags = RidevInputSink, Target = _window },
            new() { UsagePage = 0x01, Usage = 0x05, Flags = RidevInputSink, Target = _window }
        ];
        if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            return Fail("RegisterRawInputDevices");
        }

        Console.WriteLine("utc,elapsedUs,device,size,count,changedBytes,reportHex");
        using Timer stopTimer = new(_ => PostMessage(_window, WmClose, 0, 0), null,
            TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);

        while (DateTime.UtcNow < _endUtc && GetMessage(out Message message, 0, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        return 0;
    }

    private static nint HandleWindowMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        if (message == WmInput)
        {
            ReadRawInput(lParam);
            return 0;
        }

        if (message == WmClose)
        {
            Environment.Exit(0);
        }

        return DefWindowProc(window, message, wParam, lParam);
    }

    private static unsafe void ReadRawInput(nint rawInput)
    {
        uint size = 0;
        uint headerSize = (uint)(IntPtr.Size == 8 ? 24 : 16);
        if (GetRawInputData(rawInput, RidInput, 0, ref size, headerSize) != 0 || size < headerSize + 8)
        {
            return;
        }

        byte[] buffer = new byte[size];
        fixed (byte* data = buffer)
        {
            if (GetRawInputData(rawInput, RidInput, (nint)data, ref size, headerSize) != size ||
                BitConverter.ToUInt32(buffer, 0) != RimTypeHid)
            {
                return;
            }

            nint device = IntPtr.Size == 8
                ? (nint)BitConverter.ToInt64(buffer, 8)
                : (nint)BitConverter.ToInt32(buffer, 8);
            string devicePath = GetDeviceName(device);
            if (!devicePath.Contains("VID_3537", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            string deviceName = _includeDevicePath ? devicePath : PublicDeviceName(devicePath);

            int dataOffset = (int)headerSize;
            uint reportSize = BitConverter.ToUInt32(buffer, dataOffset);
            uint reportCount = BitConverter.ToUInt32(buffer, dataOffset + 4);
            int reportsOffset = dataOffset + 8;
            if (reportSize == 0 || reportCount == 0 ||
                (ulong)reportsOffset + (ulong)reportSize * reportCount > (ulong)buffer.Length)
            {
                return;
            }

            for (uint reportIndex = 0; reportIndex < reportCount; reportIndex++)
            {
                int offset = reportsOffset + checked((int)(reportIndex * reportSize));
                byte[] report = buffer.AsSpan(offset, checked((int)reportSize)).ToArray();
                LastReports.TryGetValue(device, out byte[]? previous);
                string changed = ChangedIndexes(previous, report);
                if (previous is not null && changed.Length == 0)
                {
                    continue;
                }

                LastReports[device] = report;
                long elapsedUs = (Stopwatch.GetTimestamp() - StartTicks) * 1_000_000 / Stopwatch.Frequency;
                Console.WriteLine($"{DateTime.UtcNow:O},{elapsedUs},{Csv(deviceName)},{reportSize},{reportCount},{Csv(changed)},{Convert.ToHexString(report)}");
            }
        }
    }

    private static string ChangedIndexes(byte[]? previous, byte[] current)
    {
        if (previous is null)
        {
            return "initial";
        }

        List<string> changed = [];
        int shared = Math.Min(previous.Length, current.Length);
        for (int index = 0; index < shared; index++)
        {
            if (previous[index] != current[index])
            {
                changed.Add($"{index}:{previous[index]:X2}>{current[index]:X2}");
            }
        }
        if (previous.Length != current.Length)
        {
            changed.Add($"length:{previous.Length}>{current.Length}");
        }
        return string.Join(';', changed);
    }

    private static string GetDeviceName(nint device)
    {
        if (DeviceNames.TryGetValue(device, out string? cached))
        {
            return cached;
        }

        uint characters = 0;
        GetRawInputDeviceInfo(device, RidiDeviceName, null, ref characters);
        if (characters == 0)
        {
            return DeviceNames[device] = $"handle:{device}";
        }

        StringBuilder name = new((int)characters + 1);
        uint copied = GetRawInputDeviceInfo(device, RidiDeviceName, name, ref characters);
        return DeviceNames[device] = copied == uint.MaxValue ? $"handle:{device}" : name.ToString();
    }

    private static string PublicDeviceName(string path)
    {
        string[] pieces = path.Split('#');
        if (pieces.Length > 1 && pieces[1].Contains("VID_", StringComparison.OrdinalIgnoreCase))
        {
            return $"HID#{pieces[1]}";
        }
        return "GameSir HID <instance redacted>";
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static int Fail(string operation)
    {
        Console.Error.WriteLine($"{operation} failed: {Marshal.GetLastWin32Error()}");
        return 1;
    }
}
