using System.Diagnostics;
using System.Runtime.InteropServices;
using HIDMaestro;

// Steam-facing single-collection form of the captured GameSir vendor protocol.
// SDL on Windows uses its selected "gamepad" collection as both the input and
// output handle. Keeping 0x43 in a second top-level collection makes SDL wait
// on the wrong handle, so this descriptor places 0x43/A2 in the collection it
// opens while retaining the real VID/PID and exact 36-byte payload sizes.
byte[] descriptor = Convert.FromHexString(
    "05010905A1010600FF09238543150026FF00350046FF00750895248102" +
    "85A2092495249102C0");

var profile = new HMProfileBuilder()
    .Id("panda-pro-8k-gamesir")
    .Name("Panda Pro 8K")
    .Vendor("GameSir / Panda")
    .Vid(0x3537)
    .Pid(0x10B8)
    .ProductString("Panda Pro 8K")
    .ManufacturerString("GameSir / Panda")
    .Type("gamepad")
    .Connection("usb")
    .Descriptor(descriptor)
    .InputReportSize(37)
    .Notes("Exact G7 Pro 8K Logo+A descriptor; HIDMaestro proof of concept")
    .Build();

Console.WriteLine("Panda Pro 8K — HIDMaestro virtual GameSir proof");
Console.WriteLine($"Descriptor: {descriptor.Length} bytes, VID:PID 3537:10B8");

using var ctx = new HMContext();
ctx.LoadDefaultProfiles();
Console.Write("Installing/checking HIDMaestro... ");
ctx.InstallDriver();
Console.WriteLine("OK");
Console.Write("Creating virtual controller... ");
using var controller = ctx.CreateController(profile);
Console.WriteLine("OK");

controller.OutputReceived += (_, packet) =>
{
    string hex = Convert.ToHexString(packet.Data.Span);
    Console.WriteLine($"HOST OUTPUT source={packet.Source} report=0x{packet.ReportId:X2} data={hex}");
};

Console.WriteLine("Virtual Panda is live. Steam may now be opened.");
Console.WriteLine("Bridging physical XInput slot 0 into GameSir 0x43 reports.");
Console.WriteLine("Press Enter to remove it cleanly.");

// Data-only body: HIDMaestro prepends report ID 0x43. This is a real neutral
// frame captured from the physical controller, including GameSir A1 C8 magic.
byte[] body = Convert.FromHexString(
    "A1C8000000000000000000000000000000A42166FF2A0001000100030000000000000000");

using var stop = new CancellationTokenSource();
byte latestExtras = 0;
byte[] latestImu = new byte[12];
long latestEnhancedSequence = 0;
object enhancedLock = new();
string helperPath = Path.Combine(AppContext.BaseDirectory, "panda_enhanced_stream.exe");
using var enhanced = Process.Start(new ProcessStartInfo(helperPath)
{
    UseShellExecute = false,
    RedirectStandardOutput = true,
    CreateNoWindow = true,
}) ?? throw new InvalidOperationException("Could not start enhanced GameSir reader.");
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
                Interlocked.Increment(ref latestEnhancedSequence);
            }
        }
    }
    catch (OperationCanceledException) { }
    catch (EndOfStreamException) { }
});
var pump = Task.Run(() =>
{
    uint lastPacket = uint.MaxValue;
    long lastEnhanced = -1;
    long submitted = 0;
    long started = Stopwatch.GetTimestamp();
    while (!stop.IsCancellationRequested)
    {
        if (Native.XInputGetState(0, out var physical) != 0)
        {
            Thread.Sleep(10);
            continue;
        }
        long enhancedSequence = Interlocked.Read(ref latestEnhancedSequence);
        if (physical.PacketNumber == lastPacket && enhancedSequence == lastEnhanced)
        {
            Thread.SpinWait(12);
            continue;
        }
        lastPacket = physical.PacketNumber;
        lastEnhanced = enhancedSequence;
        PackXInput(body, physical.Gamepad);
        lock (enhancedLock)
            OverlayEnhanced(body, latestExtras, latestImu);
        controller.SubmitRawReport(0x43, body);
        submitted++;
        if (submitted % 10000 == 0)
        {
            double seconds = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
            Console.WriteLine($"BRIDGE input reports={submitted:N0} average={submitted / seconds:F0}/s packet={lastPacket}");
        }
    }
});

Console.ReadLine();
stop.Cancel();
await pump;
if (!enhanced.HasExited) enhanced.Kill(entireProcessTree: true);
await enhancedPump;
Console.WriteLine("Removing virtual Panda.");

static void PackXInput(byte[] report, XInputGamepad pad)
{
    // body[0..1] are A1 C8. SDL's payload begins at body[2].
    byte b0 = 0;
    if ((pad.Buttons & 0x1000) != 0) b0 |= 0x01; // A
    if ((pad.Buttons & 0x2000) != 0) b0 |= 0x02; // B
    if ((pad.Buttons & 0x4000) != 0) b0 |= 0x08; // X
    if ((pad.Buttons & 0x8000) != 0) b0 |= 0x10; // Y
    if ((pad.Buttons & 0x0100) != 0) b0 |= 0x40; // LB
    if ((pad.Buttons & 0x0200) != 0) b0 |= 0x80; // RB
    report[2] = b0;

    byte b1 = 0;
    if (pad.LeftTrigger  > 0) b1 |= 0x01;
    if (pad.RightTrigger > 0) b1 |= 0x02;
    if ((pad.Buttons & 0x0020) != 0) b1 |= 0x04; // Back
    if ((pad.Buttons & 0x0010) != 0) b1 |= 0x08; // Start
    if ((pad.Buttons & 0x0040) != 0) b1 |= 0x20; // L3
    if ((pad.Buttons & 0x0080) != 0) b1 |= 0x40; // R3
    report[3] = b1;

    bool up = (pad.Buttons & 0x0001) != 0, down = (pad.Buttons & 0x0002) != 0;
    bool left = (pad.Buttons & 0x0004) != 0, right = (pad.Buttons & 0x0008) != 0;
    report[4] = (byte)(up && right ? 2 : right && down ? 4 : down && left ? 6 : left && up ? 8 :
                            up ? 1 : right ? 3 : down ? 5 : left ? 7 : 0);
    // report[4] upper bits and report[5] are reserved here for enhanced extras.
    report[5] = 0;

    PutI16BE(report, 6,  pad.LeftThumbX);
    PutI16BE(report, 8,  pad.LeftThumbY);
    PutI16BE(report, 10, pad.RightThumbX);
    PutI16BE(report, 12, pad.RightThumbY);
    report[14] = pad.LeftTrigger;
    report[15] = pad.RightTrigger;
}

static void PutI16BE(byte[] buffer, int offset, short value)
{
    buffer[offset] = (byte)((ushort)value >> 8);
    buffer[offset + 1] = (byte)value;
}

static void OverlayEnhanced(byte[] report, byte extras, byte[] imu)
{
    // SDL GameSir payload byte 2 upper bits are L4/R4; payload byte 3 low
    // bits are L5/R5. Physical mapping comes from four isolated captures.
    if ((extras & 0x01) != 0) report[4] |= 0x40;
    if ((extras & 0x02) != 0) report[4] |= 0x80;
    if ((extras & 0x04) != 0) report[5] |= 0x01;
    if ((extras & 0x08) != 0) report[5] |= 0x02;

    // Physical 0x12 is gyro then accel; Steam's GameSir packet is accel then gyro.
    Buffer.BlockCopy(imu, 6, report, 16, 6); // payload 14..19 accel
    Buffer.BlockCopy(imu, 0, report, 22, 6); // payload 20..25 gyro
}

[StructLayout(LayoutKind.Sequential)]
struct XInputState
{
    public uint PacketNumber;
    public XInputGamepad Gamepad;
}

[StructLayout(LayoutKind.Sequential)]
struct XInputGamepad
{
    public ushort Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short LeftThumbX;
    public short LeftThumbY;
    public short RightThumbX;
    public short RightThumbY;
}

static class Native
{
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    public static extern uint XInputGetState(uint userIndex, out XInputState state);
}
