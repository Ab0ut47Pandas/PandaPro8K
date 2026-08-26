namespace PandaPro8K;

internal sealed class BridgeSnapshot
{
    public string State { get; init; } = "Stopped";
    public string? Error { get; init; }
    public string? LogPath { get; init; }
    public bool PhysicalConnected { get; init; }
    public bool EnhancedConnected { get; init; }
    public string EnhancedStatus { get; init; } = "Stopped";
    public long EnhancedReconnects { get; init; }
    public double EnhancedReportAgeMs { get; init; }
    public bool IsolationActive { get; init; }
    public string IsolationStatus { get; init; } = "Not configured";
    public bool VirtualConnected { get; init; }
    public int VirtualOutputRateHz { get; init; } = 1000;
    public uint XInputSlot { get; init; }
    public uint PacketNumber { get; init; }
    public ushort Buttons { get; init; }
    public byte LeftTrigger { get; init; }
    public byte RightTrigger { get; init; }
    public short LeftX { get; init; }
    public short LeftY { get; init; }
    public short RightX { get; init; }
    public short RightY { get; init; }
    public byte Extras { get; init; }
    public short GyroPitch { get; init; }
    public short GyroYaw { get; init; }
    public short GyroRoll { get; init; }
    public short AccelX { get; init; }
    public short AccelY { get; init; }
    public short AccelZ { get; init; }
    public string EnhancedRawHex { get; init; } = "Waiting for report 0x12...";
    public string LastHostOutput { get; init; } = "None";
    public long XInputPackets { get; init; }
    public long StateChanges { get; init; }
    public long StickChanges { get; init; }
    public long TriggerChanges { get; init; }
    public long EnhancedReports { get; init; }
    public long ImuChanges { get; init; }
    public long VirtualSubmissions { get; init; }
    public long HostOutputs { get; init; }
    public long SubmitLatencySamples { get; init; }
    public long SubmitLatencyTotalUs { get; init; }
    public long SubmitLatencyMaxUs { get; init; }
}
