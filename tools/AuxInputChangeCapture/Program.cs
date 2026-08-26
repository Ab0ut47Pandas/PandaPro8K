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
    private const uint RimTypeMouse = 0;
    private const uint RimTypeKeyboard = 1;
    private static readonly nint HwndMessage = new(-3);

    private static readonly Dictionary<nint, string> DeviceNames = new();
    private static readonly WndProc WindowProcedure = HandleWindowMessage;
    private static DateTime _endUtc;
    private static nint _window;

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
    private static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] devices,
        uint count,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        nint rawInput,
        uint command,
        nint data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        nint device,
        uint command,
        StringBuilder? data,
        ref uint size);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out Message message, nint window, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int maximum);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);

    private static int Main(string[] args)
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        int seconds = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 900;
        _endUtc = DateTime.UtcNow.AddSeconds(seconds);

        string className = $"PandaAuxInputCapture_{Environment.ProcessId}";
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
            new() { UsagePage = 0x01, Usage = 0x02, Flags = RidevInputSink, Target = _window },
            new() { UsagePage = 0x01, Usage = 0x06, Flags = RidevInputSink, Target = _window }
        ];
        if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            return Fail("RegisterRawInputDevices");
        }

        Console.WriteLine("utc,type,event,device,foregroundProcess,foregroundTitle,details");
        Thread asyncButtonThread = new(PollAsyncMouseButtons)
        {
            IsBackground = true,
            Name = "Async mouse-button trace"
        };
        asyncButtonThread.Start();
        using Timer stopTimer = new(_ => PostMessage(_window, WmClose, 0, 0), null,
            TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);

        while (DateTime.UtcNow < _endUtc && GetMessage(out Message message, 0, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        return 0;
    }

    private static void PollAsyncMouseButtons()
    {
        const int virtualKeyLeftButton = 0x01;
        const int virtualKeyRightButton = 0x02;
        const int virtualKeyMiddleButton = 0x04;
        const int virtualKeyXButton1 = 0x05;
        const int virtualKeyXButton2 = 0x06;
        (int Key, string Name, bool Down)[] buttons =
        [
            (virtualKeyLeftButton, "left", false),
            (virtualKeyRightButton, "right", false),
            (virtualKeyMiddleButton, "middle", false),
            (virtualKeyXButton1, "button4", false),
            (virtualKeyXButton2, "button5", false)
        ];

        timeBeginPeriod(1);
        try
        {
            while (DateTime.UtcNow < _endUtc)
            {
                for (int index = 0; index < buttons.Length; index++)
                {
                    short state = GetAsyncKeyState(buttons[index].Key);
                    bool down = (state & 0x8000) != 0;
                    bool pressedSinceLastPoll = (state & 0x0001) != 0;
                    if (down != buttons[index].Down || pressedSinceLastPoll)
                    {
                        buttons[index].Down = down;
                        (string process, string title) = GetForegroundContext();
                        Write("async-mouse", down ? $"{buttons[index].Name}-down" : $"{buttons[index].Name}-up",
                            "system-async-state", process, title,
                            $"vk=0x{buttons[index].Key:X2};pressedSinceLastPoll={(pressedSinceLastPoll ? 1 : 0)}");
                    }
                }
                Thread.Sleep(1);
            }
        }
        finally
        {
            timeEndPeriod(1);
        }
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
        if (GetRawInputData(rawInput, RidInput, 0, ref size, headerSize) != 0 || size < headerSize)
        {
            return;
        }

        byte[] buffer = new byte[size];
        fixed (byte* data = buffer)
        {
            if (GetRawInputData(rawInput, RidInput, (nint)data, ref size, headerSize) != size)
            {
                return;
            }

            uint type = BitConverter.ToUInt32(buffer, 0);
            nint device = IntPtr.Size == 8
                ? (nint)BitConverter.ToInt64(buffer, 8)
                : (nint)BitConverter.ToInt32(buffer, 8);
            string deviceName = GetDeviceName(device);
            (string process, string title) = GetForegroundContext();
            int dataOffset = (int)headerSize;

            if (type == RimTypeMouse && buffer.Length >= dataOffset + 24)
            {
                ushort flags = BitConverter.ToUInt16(buffer, dataOffset);
                ushort buttonFlags = BitConverter.ToUInt16(buffer, dataOffset + 4);
                ushort buttonData = BitConverter.ToUInt16(buffer, dataOffset + 6);
                int x = BitConverter.ToInt32(buffer, dataOffset + 12);
                int y = BitConverter.ToInt32(buffer, dataOffset + 16);
                string eventName = MouseEventName(buttonFlags, x, y);
                if (eventName.Length > 0)
                {
                    Write("mouse", eventName, deviceName, process, title,
                        $"flags=0x{flags:X4};buttons=0x{buttonFlags:X4};data={buttonData};x={x};y={y}");
                }
            }
            else if (type == RimTypeKeyboard && buffer.Length >= dataOffset + 16)
            {
                ushort makeCode = BitConverter.ToUInt16(buffer, dataOffset);
                ushort flags = BitConverter.ToUInt16(buffer, dataOffset + 2);
                ushort virtualKey = BitConverter.ToUInt16(buffer, dataOffset + 6);
                uint keyMessage = BitConverter.ToUInt32(buffer, dataOffset + 8);
                string eventName = (flags & 0x01) != 0 ? "up" : "down";
                Write("keyboard", eventName, deviceName, process, title,
                    $"vk=0x{virtualKey:X2};scan=0x{makeCode:X2};flags=0x{flags:X4};message=0x{keyMessage:X4}");
            }
        }
    }

    private static string MouseEventName(ushort flags, int x, int y)
    {
        List<string> names = [];
        if ((flags & 0x0001) != 0) names.Add("left-down");
        if ((flags & 0x0002) != 0) names.Add("left-up");
        if ((flags & 0x0004) != 0) names.Add("right-down");
        if ((flags & 0x0008) != 0) names.Add("right-up");
        if ((flags & 0x0010) != 0) names.Add("middle-down");
        if ((flags & 0x0020) != 0) names.Add("middle-up");
        if ((flags & 0x0040) != 0) names.Add("button4-down");
        if ((flags & 0x0080) != 0) names.Add("button4-up");
        if ((flags & 0x0100) != 0) names.Add("button5-down");
        if ((flags & 0x0200) != 0) names.Add("button5-up");
        if ((flags & 0x0400) != 0) names.Add("wheel");
        if ((flags & 0x0800) != 0) names.Add("horizontal-wheel");
        if (names.Count == 0 && (x != 0 || y != 0)) names.Add("move");
        return string.Join('+', names);
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

    private static (string Process, string Title) GetForegroundContext()
    {
        nint foreground = GetForegroundWindow();
        StringBuilder title = new(512);
        GetWindowText(foreground, title, title.Capacity);
        GetWindowThreadProcessId(foreground, out uint processId);
        string process = "unknown";
        try
        {
            process = Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
        }
        return (process, title.ToString());
    }

    private static void Write(
        string type,
        string eventName,
        string device,
        string process,
        string title,
        string details)
    {
        lock (DeviceNames)
        {
            Console.WriteLine($"{DateTime.UtcNow:O},{Csv(type)},{Csv(eventName)},{Csv(device)},{Csv(process)},{Csv(title)},{Csv(details)}");
        }
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static int Fail(string operation)
    {
        Console.Error.WriteLine($"{operation} failed: {Marshal.GetLastWin32Error()}");
        return 1;
    }
}
