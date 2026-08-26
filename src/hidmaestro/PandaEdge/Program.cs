using System.Diagnostics;
using System.Runtime.InteropServices;
using HIDMaestro;

using var instanceMutex = new Mutex(
    initiallyOwned: true,
    name: @"Global\PandaPro8KBridge-SingleInstance",
    createdNew: out bool isFirstInstance);
if (!isFirstInstance)
{
    Console.WriteLine("Panda Pro 8K Bridge is already running.");
    Thread.Sleep(2500);
    return;
}

string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDirectory);
string logPath = Path.Combine(logDirectory, $"panda-edge-{DateTime.Now:yyyyMMdd-HHmmss}.log");
using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
object logLock = new();
void Log(string message)
{
    lock (logLock)
    {
        string line = $"{DateTime.Now:O} {message}";
        Console.WriteLine(message);
        logWriter.WriteLine(line);
    }
}
AppDomain.CurrentDomain.UnhandledException += (_, e) => Log($"FATAL {e.ExceptionObject}");

Log("Panda Edge — GameSir XInput/enhanced to virtual DualSense Edge");
Log($"Log: {logPath}");
using var ctx = new HMContext();
ctx.LoadDefaultProfiles();
var profile = ctx.GetProfile("dualsense-edge")
    ?? throw new InvalidOperationException("HIDMaestro DualSense Edge profile is missing.");
// Use the profile's full 64-byte Sony codec from the first frame. The basic
// descriptor encoder leaves touch contacts zeroed (which means "finger down"
// in Sony's protocol) and cannot populate IMU fields.
if (profile.ExtendedReport is not null)
    profile.ExtendedReport.AlwaysArmed = true;
ctx.InstallDriver();
using var controller = ctx.CreateController(profile);

controller.OutputReceived += (_, packet) =>
    Log($"HOST OUTPUT source={packet.Source} report=0x{packet.ReportId:X2} bytes={Convert.ToHexString(packet.Data.Span)}");

Log("Virtual DualSense Edge is live.");
Log("Physical source remains GameSir XInput mode. Press Enter to stop.");

byte latestExtras = 0;
byte[] latestImu = new byte[12];
long enhancedSequence = 0;
object enhancedLock = new();
using var stop = new CancellationTokenSource();

string helperPath = Path.Combine(AppContext.BaseDirectory, "panda_enhanced_stream.exe");
using var enhanced = Process.Start(new ProcessStartInfo(helperPath)
{
    UseShellExecute = false,
    RedirectStandardOutput = true,
    CreateNoWindow = true,
}) ?? throw new InvalidOperationException("Could not start enhanced GameSir reader.");
Thread.Sleep(150);
if (enhanced.HasExited)
    throw new InvalidOperationException($"Enhanced GameSir reader exited during startup with code {enhanced.ExitCode}.");
Log($"Enhanced reader is live (PID {enhanced.Id}).");

var enhancedPump = Task.Run(async () =>
{
    byte[] frame = new byte[13];
    try
    {
        while (!stop.IsCancellationRequested)
        {
            await enhanced.StandardOutput.BaseStream.ReadExactlyAsync(frame, stop.Token);
            lock (enhancedLock)
            {
                latestExtras = frame[0];
                Buffer.BlockCopy(frame, 1, latestImu, 0, 12);
                Interlocked.Increment(ref enhancedSequence);
            }
        }
    }
    catch (OperationCanceledException) { }
    catch (EndOfStreamException) { }
});

var axes = new Dictionary<HMAxis, float>
{
    [HMAxis.X] = 0.5f, [HMAxis.Y] = 0.5f,
    [HMAxis.Z] = 0.5f, [HMAxis.Rz] = 0.5f,
    [HMAxis.Rx] = 0.0f, [HMAxis.Ry] = 0.0f,
};

var inputPump = Task.Run(() =>
{
    uint lastPacket = uint.MaxValue;
    long lastEnhanced = -1;
    long submitted = 0;
    long started = Stopwatch.GetTimestamp();
    long sensorEpoch = started;
    while (!stop.IsCancellationRequested)
    {
        if (Native.XInputGetState(0, out var physical) != 0)
        {
            Thread.Sleep(10);
            continue;
        }
        long currentEnhanced = Interlocked.Read(ref enhancedSequence);
        if (physical.PacketNumber == lastPacket && currentEnhanced == lastEnhanced)
        {
            Thread.SpinWait(12);
            continue;
        }
        lastPacket = physical.PacketNumber;
        lastEnhanced = currentEnhanced;

        axes[HMAxis.X]  = Normalize(physical.Gamepad.LeftThumbX);
        axes[HMAxis.Y]  = NormalizeY(physical.Gamepad.LeftThumbY);
        axes[HMAxis.Z]  = Normalize(physical.Gamepad.RightThumbX);
        axes[HMAxis.Rz] = NormalizeY(physical.Gamepad.RightThumbY);
        axes[HMAxis.Rx] = physical.Gamepad.LeftTrigger / 255f;
        axes[HMAxis.Ry] = physical.Gamepad.RightTrigger / 255f;

        HMButton buttons = MapButtons(physical.Gamepad.Buttons);
        short gyroPitch, gyroYaw, gyroRoll, accelX, accelY, accelZ;
        byte extras;
        lock (enhancedLock)
        {
            extras = latestExtras;
            gyroPitch = ReadLe16(latestImu, 0);
            gyroYaw   = ReadLe16(latestImu, 2);
            gyroRoll  = ReadLe16(latestImu, 4);
            accelX    = ReadLe16(latestImu, 6);
            accelY    = ReadLe16(latestImu, 8);
            accelZ    = ReadLe16(latestImu, 10);
        }
        if ((extras & 0x01) != 0) buttons |= HMButton.LeftPaddle;
        if ((extras & 0x02) != 0) buttons |= HMButton.RightPaddle;
        if ((extras & 0x04) != 0) buttons |= HMButton.LeftPaddle2;
        if ((extras & 0x08) != 0) buttons |= HMButton.RightPaddle2;

        var state = new HMGamepadState
        {
            Axes = axes,
            Buttons = buttons,
            Hat = MapHat(physical.Gamepad.Buttons),
            GyroPitch = gyroPitch, GyroYaw = gyroYaw, GyroRoll = gyroRoll,
            AccelX = accelX, AccelY = accelY, AccelZ = accelZ,
            // A USB DualSense uses 1/3-microsecond sensor ticks, not
            // microseconds. Use an app-relative epoch to avoid overflowing
            // the intermediate multiplication; the 32-bit value itself is
            // expected to wrap and consumers account for that.
            SensorTimestamp = unchecked((uint)((Stopwatch.GetTimestamp() - sensorEpoch) * 3_000_000L / Stopwatch.Frequency)),
            BatteryLevel = 10,
        };
        controller.SubmitState(in state);
        submitted++;
        if (submitted % 10000 == 0)
        {
            double seconds = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
            Log($"EDGE reports={submitted:N0} average={submitted / seconds:F0}/s extras=0x{extras:X}");
        }
    }
});

Console.ReadLine();
stop.Cancel();
await inputPump;
if (!enhanced.HasExited) enhanced.Kill(entireProcessTree: true);
await enhancedPump;

static float Normalize(short value) => ((int)value + 32768) / 65535f;
static float NormalizeY(short value) => (32767 - (int)value) / 65535f;
static short ReadLe16(byte[] bytes, int i) => unchecked((short)(bytes[i] | (bytes[i + 1] << 8)));

static HMButton MapButtons(ushort b)
{
    HMButton r = HMButton.None;
    // Preserve physical positions across Xbox and PlayStation labelling.
    // The observed HIDMaestro Edge mapping is mirrored on this build, so
    // swap each horizontal pair at the source boundary.
    // Sony profiles translate these semantic buttons to their physical
    // Cross/Circle/Square/Triangle positions, so do not pre-swap them here.
    if ((b & 0x1000) != 0) r |= HMButton.A;
    if ((b & 0x2000) != 0) r |= HMButton.B;
    if ((b & 0x4000) != 0) r |= HMButton.X;
    if ((b & 0x8000) != 0) r |= HMButton.Y;
    if ((b & 0x0100) != 0) r |= HMButton.LeftBumper;
    if ((b & 0x0200) != 0) r |= HMButton.RightBumper;
    if ((b & 0x0020) != 0) r |= HMButton.Back;
    if ((b & 0x0010) != 0) r |= HMButton.Start;
    if ((b & 0x0040) != 0) r |= HMButton.LeftStick;
    if ((b & 0x0080) != 0) r |= HMButton.RightStick;
    return r;
}

static HMHat MapHat(ushort b)
{
    bool u = (b & 0x0001) != 0, d = (b & 0x0002) != 0;
    bool l = (b & 0x0004) != 0, r = (b & 0x0008) != 0;
    return u && r ? HMHat.NorthEast : r && d ? HMHat.SouthEast :
           d && l ? HMHat.SouthWest : l && u ? HMHat.NorthWest :
           u ? HMHat.North : r ? HMHat.East : d ? HMHat.South :
           l ? HMHat.West : HMHat.None;
}

[StructLayout(LayoutKind.Sequential)]
struct XInputState { public uint PacketNumber; public XInputGamepad Gamepad; }

[StructLayout(LayoutKind.Sequential)]
struct XInputGamepad
{
    public ushort Buttons;
    public byte LeftTrigger, RightTrigger;
    public short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;
}

static class Native
{
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    public static extern uint XInputGetState(uint userIndex, out XInputState state);
}
