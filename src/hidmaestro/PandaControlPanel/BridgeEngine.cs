using System.Diagnostics;
using System.Runtime.InteropServices;
using HIDMaestro;

namespace PandaPro8K;

internal sealed class BridgeEngine : IAsyncDisposable
{
    private readonly object _physicalGate = new();
    private readonly object _enhancedGate = new();
    private readonly object _statusGate = new();
    private readonly object _controllerGate = new();

    private HMContext? _context;
    private HMProfile? _profile;
    private HMController? _controller;
    private HidHideManager? _hidHide;
    private Process? _enhancedProcess;
    private CancellationTokenSource? _stop;
    private Task? _enhancedTask;
    private Task? _inputTask;
    private Task? _metricsLogTask;
    private Task? _virtualRefreshTask;
    private StreamWriter? _logWriter;

    private string _state = "Stopped";
    private string? _error;
    private string? _logPath;
    private bool _physicalConnected;
    private bool _enhancedConnected;
    private string _enhancedStatus = "Stopped";
    private bool _virtualConnected;
    private XInputState _physical;
    private byte _extras;
    private readonly byte[] _imu = new byte[12];
    private readonly byte[] _enhancedRaw = new byte[64];
    private string _lastHostOutput = "None";
    private long _enhancedSequence;
    private long _sourceRescanSequence;
    private int _physicalDisconnectObserved;
    private int _virtualRefreshActive;
    private uint _xinputSlot;
    private int _virtualOutputRateHz = 1000;
    private int _swapFaceButtonPairs = 1;

    private long _xinputPackets;
    private long _stateChanges;
    private long _stickChanges;
    private long _triggerChanges;
    private long _enhancedReports;
    private long _enhancedReconnects;
    private long _lastEnhancedReportTick;
    private long _imuChanges;
    private long _virtualSubmissions;
    private long _hostOutputs;
    private long _submitLatencySamples;
    private long _submitLatencyTotalUs;
    private long _submitLatencyMaxUs;

    public bool IsRunning => _stop is not null && !_stop.IsCancellationRequested;
    public uint? PreferredXInputSlot { get; set; }
    public int VirtualOutputRateHz => Volatile.Read(ref _virtualOutputRateHz);
    public bool SwapFaceButtonPairs => Volatile.Read(ref _swapFaceButtonPairs) != 0;
    public string LogDirectory => Path.Combine(AppContext.BaseDirectory, "logs");

    public static bool IsXInputSlotConnected(uint slot) =>
        slot < 4 && Native.XInputGetState(slot, out _) == 0;

    public void SetVirtualOutputRate(int rateHz)
    {
        if (!AppSettings.IsValidRate(rateHz))
            throw new ArgumentOutOfRangeException(nameof(rateHz), "Use 0 for uncapped or a rate from 125 through 8,000 Hz.");

        int previous = Interlocked.Exchange(ref _virtualOutputRateHz, rateHz);
        if (previous == rateHz || !IsRunning) return;

        string label = rateHz == 0 ? "uncapped" : $"{rateHz:N0} Hz";
        Log($"Virtual Edge output target changed to {label}. The physical input loop is unchanged.");
    }

    public void SetFaceButtonSwap(bool enabled)
    {
        int next = enabled ? 1 : 0;
        int previous = Interlocked.Exchange(ref _swapFaceButtonPairs, next);
        if (previous == next || !IsRunning) return;

        Log(enabled
            ? "GameSir face-button correction enabled (A/B and X/Y pairs)."
            : "GameSir face-button correction disabled; using raw XInput button bits.");
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        SetStatus("Starting", null);
        ResetCounters();
        Directory.CreateDirectory(LogDirectory);
        _logPath = Path.Combine(LogDirectory, $"panda-applet-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        _logWriter = new StreamWriter(_logPath, append: false) { AutoFlush = true };
        Log("Panda Pro 8K control panel starting.");
        Log("Physical target: GameSir 3537:10C5 wired or 3537:10C6 dongle, auto-detected XInput slot + enhanced HID 0x12.");
        string outputRate = VirtualOutputRateHz == 0 ? "uncapped" : $"{VirtualOutputRateHz:N0} Hz";
        Log($"Virtual Edge output target is {outputRate}; physical input remains sampled at full device rate.");

        try
        {
            await Task.Run(StartCore);
            SetStatus("Running", null);
            Log("Bridge is ready.");
        }
        catch (Exception ex)
        {
            SetStatus("Error", ex.Message);
            Log($"FATAL {ex}");
            await StopAsync();
            throw;
        }
    }

    private void StartCore()
    {
        var stop = new CancellationTokenSource();
        _stop = stop;

        var context = new HMContext();
        _context = context;
        context.LoadDefaultProfiles();
        var profile = context.GetProfile("dualsense-edge")
            ?? throw new InvalidOperationException("HIDMaestro DualSense Edge profile is missing.");
        if (profile.ExtendedReport is not null)
            profile.ExtendedReport.AlwaysArmed = true;

        context.InstallDriver();
        _profile = profile;
        var controller = CreateVirtualController(context, profile);
        lock (_controllerGate) _controller = controller;
        lock (_statusGate) _virtualConnected = true;
        Log("Virtual DualSense Edge is live.");

        string helperPath = Path.Combine(AppContext.BaseDirectory, "panda_enhanced_stream.exe");
        if (!File.Exists(helperPath))
            throw new FileNotFoundException("Enhanced GameSir reader is missing.", helperPath);

        var hidHide = new HidHideManager(Log);
        _hidHide = hidHide;
        try
        {
            hidHide.Enable(Environment.ProcessPath ?? Application.ExecutablePath, helperPath);
        }
        catch (Exception ex)
        {
            Log($"Physical isolation unavailable: {ex.Message}");
        }

        // HidHide must allowlist this process before XInput enumeration.
        // Otherwise a Steam-created virtual slot is visible while the
        // hidden physical GameSir is absent from the source picker.
        _xinputSlot = DetectPhysicalXInputSlot(stop.Token);
        Log($"Physical XInput selected: slot {_xinputSlot}.");

        var enhanced = StartEnhancedReader(helperPath);
        Thread.Sleep(150);
        if (enhanced.HasExited)
            throw new InvalidOperationException($"Enhanced GameSir reader exited with code {enhanced.ExitCode}.");

        _enhancedProcess = enhanced;
        lock (_statusGate) _enhancedStatus = "Waiting for reports";
        Log($"Enhanced reader started (PID {enhanced.Id}).");

        _enhancedTask = Task.Run(() => EnhancedSupervisorAsync(helperPath, enhanced, stop.Token));
        _inputTask = Task.Run(() => InputPumpSafe(stop.Token));
        _metricsLogTask = Task.Run(() => MetricsLogPumpAsync(stop.Token));
    }

    public async Task StopAsync()
    {
        var stop = _stop;
        if (stop is null)
        {
            SetStatus("Stopped", _error);
            return;
        }

        SetStatus("Stopping", null);
        Log("Stopping bridge.");
        stop.Cancel();

        var enhanced = _enhancedProcess;
        StopProcess(enhanced);

        foreach (var task in new[] { _inputTask, _enhancedTask, _metricsLogTask, _virtualRefreshTask })
        {
            if (task is null) continue;
            try { await task.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { Log("A worker did not stop within three seconds."); }
            catch (Exception ex) { Log($"Worker shutdown error: {ex.Message}"); }
        }

        lock (_controllerGate)
        {
            if (_controller is not null)
            {
                try { _controller.OutputReceived -= OnOutputReceived; } catch { }
                try { _controller.Dispose(); } catch (Exception ex) { Log($"Controller shutdown: {ex.Message}"); }
            }
            _controller = null;
        }
        if (_context is not null)
        {
            try { _context.Dispose(); } catch (Exception ex) { Log($"Context shutdown: {ex.Message}"); }
        }
        _hidHide?.Restore();
        try { enhanced?.Dispose(); } catch { }
        stop.Dispose();

        _profile = null;
        _context = null;
        _hidHide = null;
        _enhancedProcess = null;
        _stop = null;
        _inputTask = null;
        _enhancedTask = null;
        _metricsLogTask = null;
        _virtualRefreshTask = null;
        lock (_statusGate)
        {
            _physicalConnected = false;
            _enhancedConnected = false;
            _enhancedStatus = "Stopped";
            _virtualConnected = false;
        }
        SetStatus("Stopped", null);
        Log("Bridge stopped cleanly.");
        _logWriter?.Dispose();
        _logWriter = null;
    }

    private static Process StartEnhancedReader(string helperPath)
    {
        var startInfo = new ProcessStartInfo(helperPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--raw");
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the enhanced GameSir reader.");
    }

    private async Task EnhancedSupervisorAsync(string helperPath, Process firstProcess, CancellationToken token)
    {
        Process process = firstProcess;
        int consecutiveFailures = 0;
        while (!token.IsCancellationRequested)
        {
            long reportsBefore = Interlocked.Read(ref _enhancedReports);
            string? failureMessage = null;
            try
            {
                _enhancedProcess = process;
                using var processStop = CancellationTokenSource.CreateLinkedTokenSource(token);
                Task pump = PumpEnhancedProcessAsync(process, processStop.Token);
                Task watchdog = WaitForEnhancedStallAsync(processStop.Token);
                Task completed = await Task.WhenAny(pump, watchdog);
                if (completed == watchdog)
                {
                    StopProcess(process);
                    processStop.Cancel();
                    try { await pump; } catch { }
                    if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                    throw new TimeoutException("no report 0x12 was received for two seconds");
                }

                processStop.Cancel();
                try { await watchdog; } catch (OperationCanceledException) { }
                await pump;
                if (!token.IsCancellationRequested)
                    Log("Enhanced reader ended; reconnecting.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                failureMessage = ex.Message;
            }
            finally
            {
                StopProcess(process);
                process.Dispose();
                _enhancedProcess = null;
                ResetEnhancedInput();
                lock (_statusGate)
                {
                    _enhancedConnected = false;
                    _enhancedStatus = token.IsCancellationRequested ? "Stopped" : "Reconnecting";
                }
            }

            if (token.IsCancellationRequested) break;
            bool producedReports = Interlocked.Read(ref _enhancedReports) > reportsBefore;
            consecutiveFailures = producedReports ? 0 : consecutiveFailures + 1;
            long attempt = Interlocked.Increment(ref _enhancedReconnects);
            bool logAttempt = ShouldLogReconnectAttempt(attempt);
            if (failureMessage is not null && logAttempt)
                Log($"Enhanced reader unavailable: {failureMessage}");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    int delayMs = Math.Min(5_000, 750 << Math.Min(consecutiveFailures, 3));
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token);
                    process = StartEnhancedReader(helperPath);
                    if (logAttempt)
                        Log($"Enhanced reader restart attempt {attempt} (PID {process.Id}, delay {delayMs} ms).");
                    break;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Log($"Enhanced reader restart failed: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
            }
        }
    }

    private async Task PumpEnhancedProcessAsync(Process process, CancellationToken token)
    {
        byte[] frame = new byte[65];
        byte[] previousImu = new byte[12];
        bool haveImu = false;
        bool announced = false;

        while (!token.IsCancellationRequested)
        {
            await process.StandardOutput.BaseStream.ReadExactlyAsync(frame, token);

            bool imuChanged = !haveImu || !frame.AsSpan(14, 12).SequenceEqual(previousImu);
            lock (_enhancedGate)
            {
                _extras = frame[0];
                Buffer.BlockCopy(frame, 1, _enhancedRaw, 0, 64);
                Buffer.BlockCopy(frame, 14, _imu, 0, 12);
                if (imuChanged)
                {
                    Buffer.BlockCopy(frame, 14, previousImu, 0, 12);
                    haveImu = true;
                }
                Interlocked.Increment(ref _enhancedSequence);
            }
            Interlocked.Exchange(ref _lastEnhancedReportTick, Stopwatch.GetTimestamp());
            Interlocked.Increment(ref _enhancedReports);
            if (imuChanged) Interlocked.Increment(ref _imuChanges);

            if (!announced)
            {
                announced = true;
                long reconnects = Interlocked.Read(ref _enhancedReconnects);
                lock (_statusGate)
                {
                    _enhancedConnected = true;
                    _enhancedStatus = reconnects == 0 ? "Connected" : $"Reconnected ({reconnects})";
                }
                if (reconnects > 0)
                {
                    Interlocked.Increment(ref _sourceRescanSequence);
                    if (Interlocked.Exchange(ref _physicalDisconnectObserved, 0) != 0)
                        ScheduleVirtualControllerRefresh(token);
                }
                Log($"Enhanced reader is live (PID {process.Id}, reconnects {reconnects}).");
            }
        }
    }

    private async Task WaitForEnhancedStallAsync(CancellationToken token)
    {
        long started = Stopwatch.GetTimestamp();
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), token);
            long latest = Interlocked.Read(ref _lastEnhancedReportTick);
            long reference = latest == 0 ? started : latest;
            if (Stopwatch.GetElapsedTime(reference) >= TimeSpan.FromSeconds(2)) return;
        }
    }

    private void ResetEnhancedInput()
    {
        lock (_enhancedGate)
        {
            _extras = 0;
            Array.Clear(_imu);
            Array.Clear(_enhancedRaw);
            Interlocked.Increment(ref _enhancedSequence);
        }
        Interlocked.Exchange(ref _lastEnhancedReportTick, 0);
    }

    private static void StopProcess(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch { }
    }

    private void InputPumpSafe(CancellationToken token)
    {
        try
        {
            InputPump(token);
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                SetWorkerError($"XInput/virtual bridge error: {ex.Message}");
        }
    }

    private void InputPump(CancellationToken token)
    {
        uint lastPacket = uint.MaxValue;
        long lastEnhanced = -1;
        bool havePrevious = false;
        XInputGamepad previous = default;
        long sensorEpoch = Stopwatch.GetTimestamp();
        int idlePasses = 0;
        bool neutralSubmitted = false;
        long seenRescan = Interlocked.Read(ref _sourceRescanSequence);
        long nextDisconnectedScan = 0;
        long nextVirtualSubmit = 0;
        int activeOutputRate = int.MinValue;
        var axes = new Dictionary<HMAxis, float>
        {
            [HMAxis.X] = 0.5f,
            [HMAxis.Y] = 0.5f,
            [HMAxis.Z] = 0.5f,
            [HMAxis.Rz] = 0.5f,
            [HMAxis.Rx] = 0.0f,
            [HMAxis.Ry] = 0.0f,
        };

        while (!token.IsCancellationRequested)
        {
            long requestedRescan = Interlocked.Read(ref _sourceRescanSequence);
            if (requestedRescan != seenRescan)
            {
                uint previousSlot = _xinputSlot;
                SubmitNeutralState(axes, sensorEpoch);
                neutralSubmitted = true;
                try
                {
                    uint detected = DetectPhysicalXInputSlot(token, previousSlot);
                    _xinputSlot = detected;
                    lastPacket = uint.MaxValue;
                    havePrevious = false;
                    Log($"Physical XInput reacquired after controller reconnect: slot {previousSlot} -> {detected}.");
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    Log($"Physical XInput rescan deferred: {ex.Message}");
                }
                seenRescan = requestedRescan;
            }

            if (Native.XInputGetState(_xinputSlot, out var physical) != 0)
            {
                Interlocked.Exchange(ref _physicalDisconnectObserved, 1);
                lock (_statusGate) _physicalConnected = false;
                if (!neutralSubmitted)
                {
                    SubmitNeutralState(axes, sensorEpoch);
                    neutralSubmitted = true;
                }

                long now = Stopwatch.GetTimestamp();
                if (now >= nextDisconnectedScan)
                {
                    try
                    {
                        uint previousSlot = _xinputSlot;
                        uint detected = DetectPhysicalXInputSlot(token, previousSlot);
                        _xinputSlot = detected;
                        lastPacket = uint.MaxValue;
                        havePrevious = false;
                        Log($"Physical XInput recovered while disconnected: slot {previousSlot} -> {detected}.");
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        Log($"Physical XInput is still unavailable: {ex.Message}");
                    }
                    nextDisconnectedScan = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                }
                Thread.Sleep(10);
                continue;
            }
            lock (_statusGate) _physicalConnected = true;
            neutralSubmitted = false;

            bool newPacket = physical.PacketNumber != lastPacket;
            long currentEnhanced = Interlocked.Read(ref _enhancedSequence);
            bool newEnhanced = currentEnhanced != lastEnhanced;

            if (newPacket)
            {
                Interlocked.Increment(ref _xinputPackets);
                if (!havePrevious || !GamepadEquals(physical.Gamepad, previous))
                {
                    Interlocked.Increment(ref _stateChanges);
                    if (!havePrevious || !SticksEqual(physical.Gamepad, previous))
                        Interlocked.Increment(ref _stickChanges);
                    if (!havePrevious || physical.Gamepad.LeftTrigger != previous.LeftTrigger ||
                        physical.Gamepad.RightTrigger != previous.RightTrigger)
                        Interlocked.Increment(ref _triggerChanges);
                    previous = physical.Gamepad;
                    havePrevious = true;
                }
                lastPacket = physical.PacketNumber;
                lock (_physicalGate) _physical = physical;
            }
            lastEnhanced = currentEnhanced;
            int rateHz = Volatile.Read(ref _virtualOutputRateHz);
            long submitTime = Stopwatch.GetTimestamp();
            if (rateHz != activeOutputRate)
            {
                activeOutputRate = rateHz;
                nextVirtualSubmit = submitTime;
            }

            if (rateHz != 0 && submitTime < nextVirtualSubmit)
            {
                if (!newPacket && !newEnhanced)
                {
                    // Capture and output share this high-rate loop. Yielding
                    // between scheduled frames keeps the UI responsive
                    // without adding a second busy worker.
                    if ((++idlePasses & 0x0F) == 0) Thread.Yield();
                    else Thread.SpinWait(24);
                }
                continue;
            }
            idlePasses = 0;

            SubmitCurrentVirtualState(in physical, axes, sensorEpoch);

            if (rateHz == 0)
            {
                nextVirtualSubmit = 0;
                continue;
            }

            long interval = Math.Max(1, Stopwatch.Frequency / rateHz);
            nextVirtualSubmit += interval;
            long submittedAt = Stopwatch.GetTimestamp();
            if (submittedAt - nextVirtualSubmit > interval * 2)
                nextVirtualSubmit = submittedAt + interval;
        }
    }

    private void SubmitCurrentVirtualState(
        in XInputState physical,
        Dictionary<HMAxis, float> axes,
        long sensorEpoch)
    {
        axes[HMAxis.X] = Normalize(physical.Gamepad.LeftThumbX);
        axes[HMAxis.Y] = NormalizeY(physical.Gamepad.LeftThumbY);
        axes[HMAxis.Z] = Normalize(physical.Gamepad.RightThumbX);
        axes[HMAxis.Rz] = NormalizeY(physical.Gamepad.RightThumbY);
        axes[HMAxis.Rx] = physical.Gamepad.LeftTrigger / 255f;
        axes[HMAxis.Ry] = physical.Gamepad.RightTrigger / 255f;

        byte extras;
        short gyroPitch, gyroYaw, gyroRoll, accelX, accelY, accelZ;
        lock (_enhancedGate)
        {
            extras = _extras;
            gyroPitch = ReadLe16(_imu, 0);
            gyroYaw = ReadLe16(_imu, 2);
            gyroRoll = ReadLe16(_imu, 4);
            accelX = ReadLe16(_imu, 6);
            accelY = ReadLe16(_imu, 8);
            accelZ = ReadLe16(_imu, 10);
        }

        HMButton buttons = MapButtons(physical.Gamepad.Buttons, SwapFaceButtonPairs);
        if ((extras & 0x01) != 0) buttons |= HMButton.LeftPaddle;
        if ((extras & 0x02) != 0) buttons |= HMButton.RightPaddle;
        if ((extras & 0x04) != 0) buttons |= HMButton.LeftPaddle2;
        if ((extras & 0x08) != 0) buttons |= HMButton.RightPaddle2;

        var state = new HMGamepadState
        {
            Axes = axes,
            Buttons = buttons,
            Hat = MapHat(physical.Gamepad.Buttons),
            GyroPitch = gyroPitch,
            GyroYaw = gyroYaw,
            GyroRoll = gyroRoll,
            AccelX = accelX,
            AccelY = accelY,
            AccelZ = accelZ,
            SensorTimestamp = unchecked((uint)((Stopwatch.GetTimestamp() - sensorEpoch) * 3_000_000L / Stopwatch.Frequency)),
            BatteryLevel = 10,
        };

        SubmitVirtualState(in state);
    }

    private async Task MetricsLogPumpAsync(CancellationToken token)
    {
        long lastX = 0, lastE = 0, lastV = 0;
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), token); }
            catch (OperationCanceledException) { break; }
            long x = Interlocked.Read(ref _xinputPackets);
            long e = Interlocked.Read(ref _enhancedReports);
            long v = Interlocked.Read(ref _virtualSubmissions);
            long reconnects = Interlocked.Read(ref _enhancedReconnects);
            int target = VirtualOutputRateHz;
            string targetLabel = target == 0 ? "uncapped" : $"{target}/s";
            Log($"Rates/5s: XInput={(x - lastX) / 5.0:F0}/s enhanced={(e - lastE) / 5.0:F0}/s virtual={(v - lastV) / 5.0:F0}/s target={targetLabel} reconnects={reconnects}");
            lastX = x; lastE = e; lastV = v;
        }
    }

    public BridgeSnapshot GetSnapshot(bool includeRaw = false)
    {
        XInputState physical;
        byte extras;
        byte[] imu = new byte[12];
        byte[]? raw = includeRaw ? new byte[64] : null;
        lock (_physicalGate) physical = _physical;
        lock (_enhancedGate)
        {
            extras = _extras;
            Buffer.BlockCopy(_imu, 0, imu, 0, 12);
            if (raw is not null) Buffer.BlockCopy(_enhancedRaw, 0, raw, 0, 64);
        }

        string state, enhancedStatus, hostOutput;
        string? error, logPath;
        bool physicalConnected, enhancedConnected, virtualConnected;
        lock (_statusGate)
        {
            state = _state;
            error = _error;
            logPath = _logPath;
            physicalConnected = _physicalConnected;
            enhancedConnected = _enhancedConnected;
            enhancedStatus = _enhancedStatus;
            virtualConnected = _virtualConnected;
            hostOutput = _lastHostOutput;
        }

        long reportTick = Interlocked.Read(ref _lastEnhancedReportTick);
        double reportAgeMs = reportTick == 0
            ? double.PositiveInfinity
            : Stopwatch.GetElapsedTime(reportTick).TotalMilliseconds;

        return new BridgeSnapshot
        {
            State = state,
            Error = error,
            LogPath = logPath,
            PhysicalConnected = physicalConnected,
            EnhancedConnected = enhancedConnected,
            EnhancedStatus = enhancedStatus,
            EnhancedReconnects = Interlocked.Read(ref _enhancedReconnects),
            EnhancedReportAgeMs = reportAgeMs,
            IsolationActive = _hidHide?.IsActive == true,
            IsolationStatus = _hidHide?.Status ?? "Not configured",
            VirtualConnected = virtualConnected,
            VirtualOutputRateHz = VirtualOutputRateHz,
            XInputSlot = _xinputSlot,
            PacketNumber = physical.PacketNumber,
            Buttons = physical.Gamepad.Buttons,
            LeftTrigger = physical.Gamepad.LeftTrigger,
            RightTrigger = physical.Gamepad.RightTrigger,
            LeftX = physical.Gamepad.LeftThumbX,
            LeftY = physical.Gamepad.LeftThumbY,
            RightX = physical.Gamepad.RightThumbX,
            RightY = physical.Gamepad.RightThumbY,
            Extras = extras,
            GyroPitch = ReadLe16(imu, 0),
            GyroYaw = ReadLe16(imu, 2),
            GyroRoll = ReadLe16(imu, 4),
            AccelX = ReadLe16(imu, 6),
            AccelY = ReadLe16(imu, 8),
            AccelZ = ReadLe16(imu, 10),
            EnhancedRawHex = raw is null ? "Raw display is paused outside the Raw reports tab."
                : raw[0] == 0x12 ? Convert.ToHexString(raw) : "Waiting for report 0x12...",
            LastHostOutput = hostOutput,
            XInputPackets = Interlocked.Read(ref _xinputPackets),
            StateChanges = Interlocked.Read(ref _stateChanges),
            StickChanges = Interlocked.Read(ref _stickChanges),
            TriggerChanges = Interlocked.Read(ref _triggerChanges),
            EnhancedReports = Interlocked.Read(ref _enhancedReports),
            ImuChanges = Interlocked.Read(ref _imuChanges),
            VirtualSubmissions = Interlocked.Read(ref _virtualSubmissions),
            HostOutputs = Interlocked.Read(ref _hostOutputs),
            SubmitLatencySamples = Interlocked.Read(ref _submitLatencySamples),
            SubmitLatencyTotalUs = Interlocked.Read(ref _submitLatencyTotalUs),
            SubmitLatencyMaxUs = Interlocked.Read(ref _submitLatencyMaxUs),
        };
    }

    private void OnOutputReceived(HMController _, HMOutputPacket packet)
    {
        Interlocked.Increment(ref _hostOutputs);
        string data = packet.Data.IsEmpty ? "(empty)" : Convert.ToHexString(packet.Data.Span);
        if (data.Length > 160) data = data[..160] + "...";
        lock (_statusGate)
            _lastHostOutput = $"{packet.Source} 0x{packet.ReportId:X2}: {data}";
    }

    private void RecordSubmitLatency(long microseconds)
    {
        Interlocked.Increment(ref _submitLatencySamples);
        Interlocked.Add(ref _submitLatencyTotalUs, microseconds);
        long observed;
        do
        {
            observed = Interlocked.Read(ref _submitLatencyMaxUs);
            if (microseconds <= observed) break;
        } while (Interlocked.CompareExchange(ref _submitLatencyMaxUs, microseconds, observed) != observed);
    }

    private void SetWorkerError(string message)
    {
        SetStatus("Error", message);
        Log(message);
    }

    private void SetStatus(string state, string? error)
    {
        lock (_statusGate)
        {
            _state = state;
            _error = error;
        }
    }

    private void Log(string message)
    {
        lock (_statusGate)
        {
            try
            {
                _logWriter?.WriteLine($"{DateTime.Now:O} {message}");
            }
            catch { }
        }
    }

    private void ResetCounters()
    {
        _xinputPackets = _stateChanges = _stickChanges = _triggerChanges = 0;
        _enhancedReports = _imuChanges = _virtualSubmissions = _hostOutputs = 0;
        _enhancedReconnects = 0;
        _lastEnhancedReportTick = 0;
        _submitLatencySamples = _submitLatencyTotalUs = _submitLatencyMaxUs = 0;
        _enhancedSequence = 0;
        _sourceRescanSequence = 0;
        _physicalDisconnectObserved = 0;
        _virtualRefreshActive = 0;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private static float Normalize(short value) => ((int)value + 32768) / 65535f;
    private static float NormalizeY(short value) => (32767 - (int)value) / 65535f;
    private static short ReadLe16(byte[] bytes, int i) => unchecked((short)(bytes[i] | (bytes[i + 1] << 8)));

    private static bool GamepadEquals(XInputGamepad a, XInputGamepad b) =>
        a.Buttons == b.Buttons && a.LeftTrigger == b.LeftTrigger && a.RightTrigger == b.RightTrigger &&
        SticksEqual(a, b);

    private static bool SticksEqual(XInputGamepad a, XInputGamepad b) =>
        a.LeftThumbX == b.LeftThumbX && a.LeftThumbY == b.LeftThumbY &&
        a.RightThumbX == b.RightThumbX && a.RightThumbY == b.RightThumbY;

    private uint DetectPhysicalXInputSlot(CancellationToken token, uint? previousSlot = null)
    {
        const int slotCount = 4;

        if (PreferredXInputSlot is uint preferred)
        {
            if (!IsXInputSlotConnected(preferred))
                throw new InvalidOperationException(
                    $"XInput slot {preferred} is not connected. Choose another controller source, then click Start bridge.");
            Log($"XInput slot {preferred} was selected manually.");
            return preferred;
        }

        uint[] lastPackets = new uint[slotCount];
        int[] packetChanges = new int[slotCount];
        bool[] connected = new bool[slotCount];

        for (uint slot = 0; slot < slotCount; slot++)
        {
            if (Native.XInputGetState(slot, out var state) != 0) continue;
            connected[slot] = true;
            lastPackets[slot] = state.PacketNumber;
        }

        long started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromMilliseconds(300))
        {
            token.ThrowIfCancellationRequested();
            for (uint slot = 0; slot < slotCount; slot++)
            {
                if (Native.XInputGetState(slot, out var state) != 0) continue;
                connected[slot] = true;
                if (state.PacketNumber == lastPackets[slot]) continue;
                lastPackets[slot] = state.PacketNumber;
                packetChanges[slot]++;
            }
            Thread.SpinWait(32);
        }

        int bestSlot = -1;
        int bestChanges = -1;
        int bestOtherSlot = -1;
        int bestOtherChanges = -1;
        for (int slot = 0; slot < slotCount; slot++)
        {
            if (!connected[slot]) continue;
            Log($"XInput candidate slot {slot}: {packetChanges[slot]} packet changes/300ms.");
            if (previousSlot.HasValue && slot != previousSlot.Value && packetChanges[slot] > bestOtherChanges)
            {
                bestOtherSlot = slot;
                bestOtherChanges = packetChanges[slot];
            }
            if (packetChanges[slot] <= bestChanges) continue;
            bestSlot = slot;
            bestChanges = packetChanges[slot];
        }

        if (bestSlot < 0)
            throw new InvalidOperationException(
                "No XInput controller is available. Turn the GameSir on in normal XInput mode, then click Start bridge.");

        // When a controller power cycle lets Steam occupy the pad's old
        // slot, both candidates can be stationary and score zero. In that
        // exact tie, prefer the newly appeared slot. A manual selection is
        // always honored above and bypasses this heuristic.
        if (previousSlot.HasValue && bestChanges == 0 && bestOtherSlot >= 0 && bestOtherChanges == 0)
            bestSlot = bestOtherSlot;

        return (uint)bestSlot;
    }

    private void SubmitNeutralState(Dictionary<HMAxis, float> axes, long sensorEpoch)
    {
        axes[HMAxis.X] = 0.5f;
        axes[HMAxis.Y] = 0.5f;
        axes[HMAxis.Z] = 0.5f;
        axes[HMAxis.Rz] = 0.5f;
        axes[HMAxis.Rx] = 0.0f;
        axes[HMAxis.Ry] = 0.0f;
        var neutral = new HMGamepadState
        {
            Axes = axes,
            Buttons = HMButton.None,
            Hat = HMHat.None,
            SensorTimestamp = unchecked((uint)((Stopwatch.GetTimestamp() - sensorEpoch) * 3_000_000L / Stopwatch.Frequency)),
            BatteryLevel = 10,
        };
        SubmitVirtualState(in neutral);
        lock (_physicalGate) _physical = default;
    }

    private HMController CreateVirtualController(HMContext context, HMProfile profile)
    {
        HMController controller = context.CreateController(profile);
        controller.OnSubmitLatencyMicros = RecordSubmitLatency;
        controller.OutputReceived += OnOutputReceived;
        return controller;
    }

    private void SubmitVirtualState(in HMGamepadState state)
    {
        lock (_controllerGate)
        {
            if (_controller is null) return;
            _controller.SubmitState(in state);
            Interlocked.Increment(ref _virtualSubmissions);
        }
    }

    private void ScheduleVirtualControllerRefresh(CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _virtualRefreshActive, 1, 0) != 0) return;

        _virtualRefreshTask = Task.Run(async () =>
        {
            try
            {
                // Let both interfaces settle before creating the PnP arrival
                // event Steam needs after a wireless power cycle.
                await Task.Delay(TimeSpan.FromMilliseconds(350), token);
                token.ThrowIfCancellationRequested();
                RefreshVirtualController(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                SetWorkerError($"Virtual controller reconnect refresh failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _virtualRefreshActive, 0);
            }
        }, token);
    }

    private void RefreshVirtualController(CancellationToken token)
    {
        HMContext context = _context ?? throw new InvalidOperationException("HIDMaestro context is unavailable.");
        HMProfile profile = _profile ?? throw new InvalidOperationException("Virtual controller profile is unavailable.");

        Log("Physical controller returned; refreshing the virtual Edge so Steam receives a new device-arrival event.");
        _hidHide?.RefreshApplications();

        lock (_controllerGate)
        {
            token.ThrowIfCancellationRequested();
            HMController? previous = _controller;
            _controller = null;
            lock (_statusGate) _virtualConnected = false;

            if (previous is not null)
            {
                try { previous.OutputReceived -= OnOutputReceived; } catch { }
                previous.Dispose();
            }

            token.ThrowIfCancellationRequested();
            HMController replacement = CreateVirtualController(context, profile);
            _controller = replacement;
            lock (_statusGate) _virtualConnected = true;
        }

        _hidHide?.RefreshIsolation();
        Log("Virtual Edge reconnect refresh completed; Steam remained open.");
    }

    private static bool ShouldLogReconnectAttempt(long attempt) => attempt <= 5 || attempt % 20 == 0;

    private static HMButton MapButtons(ushort rawButtons, bool swapFacePairs)
    {
        ushort b = GameSirButtonMapping.NormalizePhysicalLabels(rawButtons, swapFacePairs);
        HMButton r = HMButton.None;
        if ((b & GameSirButtonMapping.A) != 0) r |= HMButton.A;
        if ((b & GameSirButtonMapping.B) != 0) r |= HMButton.B;
        if ((b & GameSirButtonMapping.X) != 0) r |= HMButton.X;
        if ((b & GameSirButtonMapping.Y) != 0) r |= HMButton.Y;
        if ((b & 0x0100) != 0) r |= HMButton.LeftBumper;
        if ((b & 0x0200) != 0) r |= HMButton.RightBumper;
        if ((b & 0x0020) != 0) r |= HMButton.Back;
        if ((b & 0x0010) != 0) r |= HMButton.Start;
        if ((b & 0x0040) != 0) r |= HMButton.LeftStick;
        if ((b & 0x0080) != 0) r |= HMButton.RightStick;
        return r;
    }

    private static HMHat MapHat(ushort b)
    {
        bool u = (b & 0x0001) != 0, d = (b & 0x0002) != 0;
        bool l = (b & 0x0004) != 0, r = (b & 0x0008) != 0;
        return u && r ? HMHat.NorthEast : r && d ? HMHat.SouthEast :
               d && l ? HMHat.SouthWest : l && u ? HMHat.NorthWest :
               u ? HMHat.North : r ? HMHat.East : d ? HMHat.South :
               l ? HMHat.West : HMHat.None;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }

    private static class Native
    {
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        public static extern uint XInputGetState(uint userIndex, out XInputState state);
    }
}
