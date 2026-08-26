using System.Runtime.InteropServices;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftX;
        public short LeftY;
        public short RightX;
        public short RightY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint Packet;
        public Gamepad Gamepad;
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint slot, out State state);

    private static int Main(string[] args)
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        int seconds = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 600;
        DateTime end = DateTime.UtcNow.AddSeconds(seconds);
        DateTime[] heartbeat = new DateTime[4];
        string?[] previous = new string?[4];
        Console.WriteLine("utc,event,slot,connected,packet,buttons,lx,ly,rx,ry,lt,rt");

        while (DateTime.UtcNow < end)
        {
            DateTime now = DateTime.UtcNow;
            for (uint slot = 0; slot < 4; slot++)
            {
                bool connected = XInputGetState(slot, out State value) == 0;
                string state = connected
                    ? $"1,{value.Packet},0x{value.Gamepad.Buttons:X4},{value.Gamepad.LeftX},{value.Gamepad.LeftY}," +
                      $"{value.Gamepad.RightX},{value.Gamepad.RightY},{value.Gamepad.LeftTrigger},{value.Gamepad.RightTrigger}"
                    : "0,0,0x0000,0,0,0,0,0,0";
                bool changed = !string.Equals(previous[slot], state, StringComparison.Ordinal);
                if (changed || now >= heartbeat[slot])
                {
                    Console.WriteLine($"{now:O},{(changed ? "change" : "heartbeat")},{slot},{state}");
                    previous[slot] = state;
                    heartbeat[slot] = now.AddSeconds(1);
                }
            }
            Thread.Sleep(1);
        }
        return 0;
    }
}
