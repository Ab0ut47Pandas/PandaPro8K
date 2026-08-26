using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace PandaPro8K;

internal sealed class MainForm : Form
{
    private static readonly int[] OutputRatePresets = [250, 500, 1000, 2000, 4000, 5000, 8000];
    private static readonly Color Accent = Color.FromArgb(35, 197, 178);
    private static readonly Color Active = Color.FromArgb(0, 196, 140);
    private static readonly Color Inactive = Color.FromArgb(51, 59, 68);
    private static readonly Color WindowBackground = Color.FromArgb(18, 22, 27);
    private static readonly Color PanelBackground = Color.FromArgb(26, 31, 37);
    private static readonly Color CardBackground = Color.FromArgb(34, 40, 48);
    private static readonly Color InputBackground = Color.FromArgb(13, 17, 21);
    private static readonly Color HeaderBackground = Color.FromArgb(15, 18, 23);
    private static readonly Color PrimaryText = Color.FromArgb(232, 236, 240);
    private static readonly Color MutedText = Color.FromArgb(164, 174, 184);
    private static readonly Color Border = Color.FromArgb(68, 78, 90);
    private static readonly Color Warning = Color.FromArgb(231, 163, 72);

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly BridgeEngine _engine = new();
    private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 50 };
    private readonly Dictionary<string, Label> _buttonIndicators = new();
    private readonly Dictionary<string, Label> _rateValues = new();
    private readonly ToolTip _toolTip = new();
    private readonly bool _steamFilterReady;

    private readonly Label _bridgeStateLabel = new();
    private readonly Label _physicalStatus = new();
    private readonly Label _enhancedStatus = new();
    private readonly Label _isolationStatus = new();
    private readonly Label _virtualStatus = new();
    private readonly Label _footerStatus = new();
    private readonly Label _leftStickValue = new();
    private readonly Label _rightStickValue = new();
    private readonly Label _leftTriggerValue = new();
    private readonly Label _rightTriggerValue = new();
    private readonly ProgressBar _leftX = NewAxisBar();
    private readonly ProgressBar _leftY = NewAxisBar();
    private readonly ProgressBar _rightX = NewAxisBar();
    private readonly ProgressBar _rightY = NewAxisBar();
    private readonly ProgressBar _leftTrigger = NewTriggerBar();
    private readonly ProgressBar _rightTrigger = NewTriggerBar();
    private readonly Label _gyroValue = new();
    private readonly Label _accelValue = new();
    private readonly TextBox _rawXInput = NewRawBox(multiline: false);
    private readonly TextBox _rawEnhanced = NewRawBox(multiline: true);
    private readonly TextBox _rawDecoded = NewRawBox(multiline: true);
    private readonly TextBox _rawHostOutput = NewRawBox(multiline: true);
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _inputSourceButton = new();
    private readonly ContextMenuStrip _inputSourceMenu = new();
    private readonly Button _outputRateButton = new();
    private readonly ContextMenuStrip _outputRateMenu = new();
    private readonly Button _overviewNav = new();
    private readonly Button _rawNav = new();
    private readonly Panel _overviewPage = new();
    private readonly Panel _rawPage = new();
    private readonly CheckBox _startupCheck = new();

    private BridgeSnapshot _previousRates = new();
    private BridgeSnapshot _latest = new();
    private long _lastRateTick = Stopwatch.GetTimestamp();
    private bool _busy;
    private bool _allowClose;
    private bool _updatingStartup;
    private bool _lastEnhancedConnected;
    private bool _rawPageVisible;
    private uint? _selectedInputSlot;

    public MainForm(bool startMinimized = false, bool steamFilterReady = false)
    {
        _steamFilterReady = steamFilterReady;
        _engine.SetVirtualOutputRate(_settings.VirtualOutputRateHz);
        Text = "Panda Pro 8K";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 700);
        Size = new Size(1080, 760);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = WindowBackground;
        ForeColor = PrimaryText;
        Font = new Font("Segoe UI", 9F);

        Controls.Add(BuildTabs());
        Controls.Add(BuildFooter());
        Controls.Add(BuildHeader());
        LoadStartupState();
        RefreshInputSources();
        DarkWindow.Apply(this);

        _uiTimer.Tick += (_, _) => RefreshDashboard();
        Shown += async (_, _) =>
        {
            if (startMinimized) WindowState = FormWindowState.Minimized;
            _uiTimer.Start();
            if (!_steamFilterReady)
            {
                _bridgeStateLabel.Text = "FILTER REQUIRED";
                _bridgeStateLabel.BackColor = Warning;
                _footerStatus.Text = "Use the Start Panda Pro 8K desktop shortcut so Steam sees only the virtual controller.";
                MessageBox.Show(
                    this,
                    "Panda Pro 8K was opened directly. Starting the bridge this way can let Steam see both the physical GameSir and the virtual controller.\n\nClose this window and use the Start Panda Pro 8K desktop shortcut. It will restart Steam safely and apply the controller filter.",
                    "Steam controller filter required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            await StartBridgeAsync();
        };
        FormClosing += MainForm_FormClosing;
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = HeaderBackground, Padding = new Padding(22, 12, 18, 8) };
        var title = new Label
        {
            Text = "Panda Pro 8K",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 19F),
            AutoSize = true,
            Location = new Point(20, 8),
        };
        var subtitle = new Label
        {
            Text = "GameSir G7 Pro 8K  •  XInput + enhanced HID → virtual DualSense Edge",
            ForeColor = MutedText,
            AutoSize = true,
            Location = new Point(23, 47),
        };
        _bridgeStateLabel.Text = "STARTING";
        _bridgeStateLabel.ForeColor = Color.White;
        _bridgeStateLabel.BackColor = Color.FromArgb(100, 105, 112);
        _bridgeStateLabel.Font = new Font("Segoe UI Semibold", 9F);
        _bridgeStateLabel.AutoSize = false;
        _bridgeStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        _bridgeStateLabel.Size = new Size(110, 30);
        _bridgeStateLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _bridgeStateLabel.Location = new Point(panel.Width - 140, 22);

        _outputRateButton.Size = new Size(165, 30);
        _outputRateButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        ConfigureActionButton(_outputRateButton);
        _outputRateMenu.BackColor = InputBackground;
        _outputRateMenu.ForeColor = PrimaryText;
        _outputRateMenu.ShowImageMargin = false;
        _outputRateMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
        _outputRateButton.Click += (_, _) =>
        {
            RefreshOutputRateMenu();
            _outputRateMenu.Show(_outputRateButton, new Point(0, _outputRateButton.Height));
        };
        _toolTip.SetToolTip(
            _outputRateButton,
            "Set the virtual Edge submission target. The physical GameSir input loop always runs at full speed. Steam uses the matching interval after the next launcher restart.");
        UpdateOutputRateButton();

        void PositionHeaderActions()
        {
            _bridgeStateLabel.Left = panel.ClientSize.Width - _bridgeStateLabel.Width - 22;
            _outputRateButton.Left = _bridgeStateLabel.Left - _outputRateButton.Width - 12;
            _outputRateButton.Top = 22;
        }

        PositionHeaderActions();
        panel.Resize += (_, _) => PositionHeaderActions();
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(_outputRateButton);
        panel.Controls.Add(_bridgeStateLabel);
        return panel;
    }

    private Control BuildTabs()
    {
        var container = new Panel { Dock = DockStyle.Fill, BackColor = WindowBackground };
        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = HeaderBackground,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 4, 0, 4),
        };
        ConfigureNavigationButton(_overviewNav, "Overview");
        ConfigureNavigationButton(_rawNav, "Raw reports");
        _overviewNav.Click += (_, _) => ShowPage(raw: false);
        _rawNav.Click += (_, _) => ShowPage(raw: true);
        nav.Controls.Add(_overviewNav);
        nav.Controls.Add(_rawNav);

        var host = new Panel { Dock = DockStyle.Fill, BackColor = WindowBackground };
        _overviewPage.Dock = DockStyle.Fill;
        _overviewPage.BackColor = WindowBackground;
        _overviewPage.Padding = new Padding(12);
        _overviewPage.Controls.Add(BuildOverview());
        _rawPage.Dock = DockStyle.Fill;
        _rawPage.BackColor = WindowBackground;
        _rawPage.Padding = new Padding(12);
        _rawPage.Controls.Add(BuildRawPage());
        host.Controls.Add(_rawPage);
        host.Controls.Add(_overviewPage);

        container.Controls.Add(host);
        container.Controls.Add(nav);
        ShowPage(raw: false);
        return container;
    }

    private Control BuildOverview()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(0),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

        var status = BuildStatusGroup();
        grid.Controls.Add(status, 0, 0);
        grid.SetColumnSpan(status, 2);
        var inputs = BuildInputsGroup();
        grid.Controls.Add(inputs, 0, 1);
        grid.SetRowSpan(inputs, 2);
        grid.Controls.Add(BuildRatesGroup(), 1, 1);
        grid.Controls.Add(BuildMotionGroup(), 1, 2);
        return grid;
    }

    private GroupBox BuildStatusGroup()
    {
        var group = NewGroup("Connection status");
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(8, 4, 8, 8) };
        for (int i = 0; i < 4; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        ConfigureStatusLabel(_physicalStatus, "Physical XInput");
        ConfigureStatusLabel(_enhancedStatus, "Enhanced HID 0x12");
        ConfigureStatusLabel(_isolationStatus, "Input isolation");
        ConfigureStatusLabel(_virtualStatus, "Virtual DualSense Edge");
        row.Controls.Add(_physicalStatus, 0, 0);
        row.Controls.Add(_enhancedStatus, 1, 0);
        row.Controls.Add(_isolationStatus, 2, 0);
        row.Controls.Add(_virtualStatus, 3, 0);
        group.Controls.Add(row);
        return group;
    }

    private GroupBox BuildInputsGroup()
    {
        var group = NewGroup("Live controls");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(8),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, Padding = new Padding(2) };
        foreach (string name in new[]
        {
            "A", "B", "X", "Y", "LB", "RB", "Back", "Start", "L3", "R3",
            "Up", "Down", "Left", "Right", "L4", "R4", "M1/L5", "M2/R5"
        })
        {
            var indicator = new Label
            {
                Text = name,
                Size = new Size(name.Length > 3 ? 70 : 52, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Inactive,
                ForeColor = PrimaryText,
                Margin = new Padding(3),
                Font = new Font("Segoe UI Semibold", 8.5F),
            };
            _buttonIndicators[name] = indicator;
            buttons.Controls.Add(indicator);
        }
        layout.Controls.Add(buttons, 0, 0);

        var sticks = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 6, Padding = new Padding(4) };
        sticks.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        sticks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sticks.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        AddAxisRow(sticks, 0, "Left X", _leftX, _leftStickValue);
        AddAxisRow(sticks, 1, "Left Y", _leftY, null);
        AddAxisRow(sticks, 2, "Right X", _rightX, _rightStickValue);
        AddAxisRow(sticks, 3, "Right Y", _rightY, null);
        sticks.Controls.Add(new Label { Text = "Values are raw signed XInput coordinates", AutoSize = true, ForeColor = MutedText, Padding = new Padding(0, 5, 0, 0) }, 1, 4);
        sticks.SetColumnSpan(sticks.GetControlFromPosition(1, 4)!, 2);
        layout.Controls.Add(sticks, 0, 1);

        var triggers = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(4) };
        triggers.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        triggers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        triggers.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        AddTriggerRow(triggers, 0, "LT", _leftTrigger, _leftTriggerValue);
        AddTriggerRow(triggers, 1, "RT", _rightTrigger, _rightTriggerValue);
        layout.Controls.Add(triggers, 0, 2);

        group.Controls.Add(layout);
        return group;
    }

    private GroupBox BuildRatesGroup()
    {
        var group = NewGroup("Observed rates");
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        AddRateCard(grid, 0, 0, "xinput", "XInput packets", "Packet-number changes reported by XInput. This is not the API polling-loop count.");
        AddRateCard(grid, 1, 0, "state", "Control changes", "Frames where any standard button, stick or trigger value changed.");
        AddRateCard(grid, 0, 1, "sticks", "Stick changes", "Frames where at least one stick coordinate changed; this depends on movement and resolution.");
        AddRateCard(grid, 1, 1, "triggers", "Trigger changes", "Frames where either analog trigger value changed.");
        AddRateCard(grid, 0, 2, "enhanced", "Enhanced HID", "Complete 0x12 reports received from the GameSir vendor HID interface.");
        AddRateCard(grid, 1, 2, "imu", "Fresh IMU", "Enhanced reports where at least one gyro or accelerometer byte changed.");
        AddRateCard(grid, 0, 3, "virtual", "Virtual submits", "Latest combined state sent to HIDMaestro. The selectable target does not change the physical input rate and is not a guaranteed USB endpoint rate.");
        AddRateCard(grid, 1, 3, "latency", "Submit overhead", "Average encode/shared-memory submission time. This is not end-to-end input latency.");
        group.Controls.Add(grid);
        return group;
    }

    private GroupBox BuildMotionGroup()
    {
        var group = NewGroup("Motion and output");
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 8, 12, 8) };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        _gyroValue.Dock = DockStyle.Fill;
        _gyroValue.Font = new Font("Consolas", 10F);
        _gyroValue.Text = "Gyro   P:      0  Y:      0  R:      0";
        _accelValue.Dock = DockStyle.Fill;
        _accelValue.Font = new Font("Consolas", 10F);
        _accelValue.Text = "Accel  X:      0  Y:      0  Z:      0";
        var note = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Raw signed firmware units • gyro and acceleration are not independently resampled",
            ForeColor = MutedText,
            AutoEllipsis = true,
        };
        var pollingNote = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Rate figures are observations—not a guaranteed USB endpoint polling interval.",
            ForeColor = Warning,
            Font = new Font("Segoe UI Semibold", 8.5F),
            AutoEllipsis = true,
        };
        grid.Controls.Add(_gyroValue, 0, 0);
        grid.Controls.Add(_accelValue, 0, 1);
        grid.Controls.Add(note, 0, 2);
        grid.Controls.Add(pollingNote, 0, 3);
        group.Controls.Add(grid);
        return group;
    }

    private Control BuildRawPage()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        grid.Controls.Add(NewSectionLabel("Selected physical XInput — decoded standard state"), 0, 0);
        grid.Controls.Add(_rawXInput, 0, 1);
        grid.Controls.Add(NewSectionLabel("GameSir enhanced HID — complete 64-byte report 0x12"), 0, 2);
        grid.Controls.Add(_rawEnhanced, 0, 3);
        grid.Controls.Add(NewSectionLabel("Decoded enhanced fields"), 0, 4);
        grid.Controls.Add(_rawDecoded, 0, 5);
        grid.Controls.Add(NewSectionLabel("Most recent host output / feature request"), 0, 6);
        grid.Controls.Add(_rawHostOutput, 0, 7);
        return grid;
    }

    private Control BuildFooter()
    {
        var panel = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = PanelBackground, Padding = new Padding(12, 10, 12, 8) };
        _footerStatus.Text = "Preparing bridge...";
        _footerStatus.AutoEllipsis = true;
        _footerStatus.Dock = DockStyle.Fill;
        _footerStatus.TextAlign = ContentAlignment.MiddleLeft;
        _footerStatus.ForeColor = PrimaryText;

        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 875, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _startButton.Text = "Start bridge";
        _startButton.Width = 100;
        _startButton.Height = 32;
        _startButton.Click += async (_, _) => await StartBridgeAsync();
        _stopButton.Text = "Stop bridge";
        _stopButton.Width = 100;
        _stopButton.Height = 32;
        _stopButton.Enabled = false;
        _stopButton.Click += async (_, _) => await StopBridgeAsync();
        var logs = new Button { Text = "Open logs", Width = 90, Height = 32 };
        logs.Click += (_, _) => OpenLogs();
        var copy = new Button { Text = "Copy diagnostics", Width = 120, Height = 32 };
        copy.Click += (_, _) => CopyDiagnostics();
        foreach (Button button in new[] { _startButton, _stopButton, logs, copy })
            ConfigureActionButton(button);
        _inputSourceButton.Width = 205;
        _inputSourceButton.Height = 32;
        _inputSourceButton.Margin = new Padding(10, 4, 8, 0);
        ConfigureActionButton(_inputSourceButton);
        _inputSourceMenu.BackColor = InputBackground;
        _inputSourceMenu.ForeColor = PrimaryText;
        _inputSourceMenu.ShowImageMargin = false;
        _inputSourceMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
        _inputSourceButton.Click += (_, _) =>
        {
            RefreshInputSources();
            _inputSourceMenu.Show(_inputSourceButton, new Point(0, _inputSourceButton.Height));
        };
        _toolTip.SetToolTip(
            _inputSourceButton,
            "Choose the physical XInput source. Auto selects the connected device whose packets update fastest.");
        _startupCheck.Text = "Start with Windows";
        _startupCheck.Appearance = Appearance.Button;
        _startupCheck.AutoSize = false;
        _startupCheck.Size = new Size(140, 32);
        _startupCheck.TextAlign = ContentAlignment.MiddleCenter;
        _startupCheck.FlatStyle = FlatStyle.Flat;
        _startupCheck.FlatAppearance.BorderColor = Border;
        _startupCheck.Margin = new Padding(12, 4, 10, 0);
        _startupCheck.ForeColor = PrimaryText;
        _startupCheck.CheckedChanged += StartupCheck_CheckedChanged;
        _toolTip.SetToolTip(
            _startupCheck,
            "Starts the bridge at user logon and adds only the wired 3537/10C5 and dongle 3537/10C6 devices to the user SDL ignore list. Existing entries are preserved.");
        actions.Controls.Add(_stopButton);
        actions.Controls.Add(_startButton);
        actions.Controls.Add(logs);
        actions.Controls.Add(copy);
        actions.Controls.Add(_startupCheck);
        actions.Controls.Add(_inputSourceButton);
        panel.Controls.Add(_footerStatus);
        panel.Controls.Add(actions);
        return panel;
    }

    private async Task StartBridgeAsync()
    {
        if (_busy || _engine.IsRunning) return;
        if (!_steamFilterReady)
        {
            _footerStatus.Text = "Bridge not started: launch it through the desktop shortcut.";
            MessageBox.Show(
                this,
                "Use the Start Panda Pro 8K desktop shortcut. Direct EXE startup cannot guarantee that Steam will ignore the physical GameSir controller.",
                "Steam controller filter required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        _busy = true;
        _startButton.Enabled = false;
        _stopButton.Enabled = false;
        _inputSourceButton.Enabled = false;
        _footerStatus.Text = "Starting virtual controller and enhanced HID reader...";
        try
        {
            _engine.PreferredXInputSlot = _selectedInputSlot;
            await _engine.StartAsync();
            _footerStatus.Text = "Bridge running. Leave this window open while using the virtual controller.";
            _stopButton.Enabled = true;
            _previousRates = _engine.GetSnapshot();
            _lastRateTick = Stopwatch.GetTimestamp();
        }
        catch (Exception ex)
        {
            _footerStatus.Text = "Bridge failed to start. Open the logs for details.";
            MessageBox.Show(this, ex.Message, "Panda Pro 8K startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _startButton.Enabled = true;
            _inputSourceButton.Enabled = true;
        }
        finally
        {
            _busy = false;
        }
    }

    private void LoadStartupState()
    {
        _updatingStartup = true;
        try
        {
            _startupCheck.Checked = StartupManager.IsEnabled(Application.ExecutablePath);
        }
        catch (Exception ex)
        {
            _startupCheck.Enabled = false;
            _toolTip.SetToolTip(_startupCheck, ex.Message);
        }
        finally
        {
            _updatingStartup = false;
            UpdateStartupToggleAppearance();
        }
    }

    private async void StartupCheck_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateStartupToggleAppearance();
        if (_updatingStartup) return;

        bool enable = _startupCheck.Checked;
        _startupCheck.Enabled = false;
        try
        {
            await Task.Run(() =>
            {
                if (enable) StartupManager.Enable(Application.ExecutablePath);
                else StartupManager.Disable();
            });

            _footerStatus.Text = enable
                ? "Windows startup enabled. Restart Steam once if it is currently open."
                : "Windows startup disabled. Restart Steam once to remove the active session filter.";
        }
        catch (Exception ex)
        {
            _updatingStartup = true;
            _startupCheck.Checked = !enable;
            _updatingStartup = false;
            MessageBox.Show(this, ex.Message, "Windows startup setting", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _startupCheck.Enabled = true;
        }
    }

    private async Task StopBridgeAsync()
    {
        if (_busy) return;
        _busy = true;
        _startButton.Enabled = false;
        _stopButton.Enabled = false;
        _footerStatus.Text = "Stopping and removing the virtual controller...";
        try
        {
            await _engine.StopAsync();
            _footerStatus.Text = "Bridge stopped.";
            _startButton.Enabled = true;
            _inputSourceButton.Enabled = true;
            RefreshInputSources();
        }
        finally
        {
            _busy = false;
        }
    }

    private void RefreshDashboard()
    {
        _latest = _engine.GetSnapshot(includeRaw: _rawPageVisible);
        UpdateState(_latest);
        UpdateControls(_latest);
        UpdateRaw(_latest);

        long now = Stopwatch.GetTimestamp();
        double elapsed = (now - _lastRateTick) / (double)Stopwatch.Frequency;
        if (elapsed >= 1.0)
        {
            UpdateRates(_latest, _previousRates, elapsed);
            _previousRates = _latest;
            _lastRateTick = now;
        }
    }

    private void UpdateState(BridgeSnapshot s)
    {
        _bridgeStateLabel.Text = s.State.ToUpperInvariant();
        _bridgeStateLabel.BackColor = s.State switch
        {
            "Running" => Accent,
            "Error" => Color.FromArgb(190, 55, 55),
            "Starting" or "Stopping" => Color.FromArgb(210, 130, 35),
            _ => Color.FromArgb(100, 105, 112),
        };
        SetStatusLabel(_physicalStatus, $"Physical XInput (slot {s.XInputSlot})", s.PhysicalConnected);
        SetStatusLabel(_enhancedStatus, "Enhanced HID 0x12", s.EnhancedStatus, s.EnhancedConnected);
        SetStatusLabel(_isolationStatus, "Input isolation", s.IsolationStatus, s.IsolationActive);
        SetStatusLabel(_virtualStatus, "Virtual DualSense Edge", s.VirtualConnected);
        if (!string.IsNullOrWhiteSpace(s.Error)) _footerStatus.Text = s.Error;
        else if (s.State == "Running" && !s.EnhancedConnected)
            _footerStatus.Text = "Paddles and gyro are temporarily unavailable. Reconnecting the enhanced HID reader automatically...";
        else if (s.State == "Running" && !s.IsolationActive)
            _footerStatus.Text = $"Controller isolation is inactive: {s.IsolationStatus}. A game may receive duplicate input paths.";
        else if (s.EnhancedConnected && !_lastEnhancedConnected && s.EnhancedReconnects > 0)
            _footerStatus.Text = $"Enhanced HID reconnected successfully after {s.EnhancedReconnects} restart attempt(s).";
        _lastEnhancedConnected = s.EnhancedConnected;
    }

    private void UpdateControls(BridgeSnapshot s)
    {
        ushort b = s.Buttons;
        SetButton("A", (b & 0x1000) != 0);
        SetButton("B", (b & 0x2000) != 0);
        SetButton("X", (b & 0x4000) != 0);
        SetButton("Y", (b & 0x8000) != 0);
        SetButton("LB", (b & 0x0100) != 0);
        SetButton("RB", (b & 0x0200) != 0);
        SetButton("Back", (b & 0x0020) != 0);
        SetButton("Start", (b & 0x0010) != 0);
        SetButton("L3", (b & 0x0040) != 0);
        SetButton("R3", (b & 0x0080) != 0);
        SetButton("Up", (b & 0x0001) != 0);
        SetButton("Down", (b & 0x0002) != 0);
        SetButton("Left", (b & 0x0004) != 0);
        SetButton("Right", (b & 0x0008) != 0);
        SetButton("L4", (s.Extras & 0x01) != 0);
        SetButton("R4", (s.Extras & 0x02) != 0);
        SetButton("M1/L5", (s.Extras & 0x04) != 0);
        SetButton("M2/R5", (s.Extras & 0x08) != 0);

        _leftX.Value = ToAxisBar(s.LeftX);
        _leftY.Value = ToAxisBar(s.LeftY);
        _rightX.Value = ToAxisBar(s.RightX);
        _rightY.Value = ToAxisBar(s.RightY);
        _leftTrigger.Value = s.LeftTrigger;
        _rightTrigger.Value = s.RightTrigger;
        _leftStickValue.Text = $"({s.LeftX,6}, {s.LeftY,6})";
        _rightStickValue.Text = $"({s.RightX,6}, {s.RightY,6})";
        _leftTriggerValue.Text = s.LeftTrigger.ToString();
        _rightTriggerValue.Text = s.RightTrigger.ToString();
        _gyroValue.Text = $"Gyro   P:{s.GyroPitch,7}  Y:{s.GyroYaw,7}  R:{s.GyroRoll,7}";
        _accelValue.Text = $"Accel  X:{s.AccelX,7}  Y:{s.AccelY,7}  Z:{s.AccelZ,7}";
    }

    private void UpdateRates(BridgeSnapshot current, BridgeSnapshot previous, double seconds)
    {
        SetRate("xinput", (current.XInputPackets - previous.XInputPackets) / seconds, "Hz");
        SetRate("state", (current.StateChanges - previous.StateChanges) / seconds, "Hz");
        SetRate("sticks", (current.StickChanges - previous.StickChanges) / seconds, "Hz");
        SetRate("triggers", (current.TriggerChanges - previous.TriggerChanges) / seconds, "Hz");
        SetRate("enhanced", (current.EnhancedReports - previous.EnhancedReports) / seconds, "Hz");
        SetRate("imu", (current.ImuChanges - previous.ImuChanges) / seconds, "Hz");
        SetRate("virtual", (current.VirtualSubmissions - previous.VirtualSubmissions) / seconds, "Hz");
        long latencySamples = current.SubmitLatencySamples - previous.SubmitLatencySamples;
        long latencyTotal = current.SubmitLatencyTotalUs - previous.SubmitLatencyTotalUs;
        double latency = latencySamples > 0 ? latencyTotal / (double)latencySamples : 0;
        SetRate("latency", latency, "µs");
    }

    private void UpdateRaw(BridgeSnapshot s)
    {
        if (!_rawPageVisible) return;
        _rawXInput.Text = $"packet={s.PacketNumber} buttons=0x{s.Buttons:X4} LT={s.LeftTrigger} RT={s.RightTrigger} " +
                          $"LX={s.LeftX} LY={s.LeftY} RX={s.RightX} RY={s.RightY}";
        _rawEnhanced.Text = FormatHex(s.EnhancedRawHex);
        _rawDecoded.Text =
            $"extras=0x{s.Extras:X2}  L4={Bit(s.Extras, 0x01)} R4={Bit(s.Extras, 0x02)} " +
            $"M1/L5={Bit(s.Extras, 0x04)} M2/R5={Bit(s.Extras, 0x08)}{Environment.NewLine}" +
            $"gyro  pitch={s.GyroPitch} yaw={s.GyroYaw} roll={s.GyroRoll}{Environment.NewLine}" +
            $"accel x={s.AccelX} y={s.AccelY} z={s.AccelZ}{Environment.NewLine}" +
            "layout: report[13..18] gyro XYZ int16-le, report[19..24] accel XYZ int16-le";
        _rawHostOutput.Text = s.LastHostOutput;
    }

    private void CopyDiagnostics()
    {
        BridgeSnapshot s = _engine.GetSnapshot();
        var text = new StringBuilder()
            .AppendLine("Panda Pro 8K diagnostics")
            .AppendLine($"state={s.State} physical={s.PhysicalConnected} enhanced={s.EnhancedConnected} virtual={s.VirtualConnected}")
            .AppendLine($"isolation={s.IsolationActive} isolationStatus={s.IsolationStatus}")
            .AppendLine($"enhancedStatus={s.EnhancedStatus} reconnects={s.EnhancedReconnects} reportAge={s.EnhancedReportAgeMs:F0}ms")
            .AppendLine($"packet={s.PacketNumber} buttons=0x{s.Buttons:X4} extras=0x{s.Extras:X2}")
            .AppendLine($"sticks=({s.LeftX},{s.LeftY}) ({s.RightX},{s.RightY}) triggers=({s.LeftTrigger},{s.RightTrigger})")
            .AppendLine($"gyro=({s.GyroPitch},{s.GyroYaw},{s.GyroRoll}) accel=({s.AccelX},{s.AccelY},{s.AccelZ})")
            .AppendLine($"virtual target={FormatOutputRate(s.VirtualOutputRateHz)}")
            .AppendLine($"totals xinput={s.XInputPackets} state={s.StateChanges} sticks={s.StickChanges} enhanced={s.EnhancedReports} imu={s.ImuChanges} virtual={s.VirtualSubmissions}")
            .AppendLine($"submit latency max={s.SubmitLatencyMaxUs}us host outputs={s.HostOutputs}")
            .AppendLine($"log={s.LogPath}")
            .ToString();
        try
        {
            Clipboard.SetText(text);
            _footerStatus.Text = "Diagnostics copied to the clipboard.";
        }
        catch (Exception ex)
        {
            _footerStatus.Text = $"Could not copy diagnostics: {ex.Message}";
        }
    }

    private void OpenLogs()
    {
        Directory.CreateDirectory(_engine.LogDirectory);
        Process.Start(new ProcessStartInfo(_engine.LogDirectory) { UseShellExecute = true });
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        _uiTimer.Stop();
        Enabled = false;
        while (_busy) await Task.Delay(100);
        try { await _engine.StopAsync(); } catch { }
        await _engine.DisposeAsync();
        _allowClose = true;
        Close();
    }

    private static GroupBox NewGroup(string title) => new DarkGroupBox
    {
        Text = title,
        Dock = DockStyle.Fill,
        BackColor = PanelBackground,
        ForeColor = PrimaryText,
        Padding = new Padding(8),
        Margin = new Padding(6),
        Font = new Font("Segoe UI Semibold", 9F),
    };

    private static Label NewSectionLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI Semibold", 9F),
        ForeColor = PrimaryText,
        TextAlign = ContentAlignment.BottomLeft,
    };

    private static TextBox NewRawBox(bool multiline) => new()
    {
        Dock = DockStyle.Fill,
        Multiline = multiline,
        ReadOnly = true,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
        Font = new Font("Consolas", 9F),
        BackColor = InputBackground,
        ForeColor = PrimaryText,
        BorderStyle = BorderStyle.FixedSingle,
        WordWrap = true,
    };

    private static ProgressBar NewAxisBar() => new DarkProgressBar { Minimum = 0, Maximum = 65535, Value = 32768, Dock = DockStyle.Fill, Margin = new Padding(4, 7, 4, 7) };
    private static ProgressBar NewTriggerBar() => new DarkProgressBar { Minimum = 0, Maximum = 255, Value = 0, Dock = DockStyle.Fill, Margin = new Padding(4, 7, 4, 7) };

    private static void ConfigureStatusLabel(Label label, string text)
    {
        label.Text = $"●  {text}\nWaiting";
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Font = new Font("Segoe UI Semibold", 10F);
        label.ForeColor = MutedText;
    }

    private static void SetStatusLabel(Label label, string title, bool connected)
    {
        label.Text = $"●  {title}{Environment.NewLine}{(connected ? "Connected" : "Not connected")}";
        label.ForeColor = connected ? Active : MutedText;
    }

    private static void SetStatusLabel(Label label, string title, string status, bool connected)
    {
        label.Text = $"●  {title}{Environment.NewLine}{status}";
        label.ForeColor = connected
            ? Active
            : status is "Reconnecting" or "Waiting for reports"
                ? Warning
                : MutedText;
    }

    private static void AddAxisRow(TableLayoutPanel table, int row, string name, ProgressBar bar, Label? value)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        table.Controls.Add(bar, 1, row);
        if (value is not null)
        {
            value.Dock = DockStyle.Fill;
            value.Font = new Font("Consolas", 9F);
            value.TextAlign = ContentAlignment.MiddleRight;
            table.Controls.Add(value, 2, row);
        }
    }

    private static void AddTriggerRow(TableLayoutPanel table, int row, string name, ProgressBar bar, Label value)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        table.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        table.Controls.Add(bar, 1, row);
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleRight;
        table.Controls.Add(value, 2, row);
    }

    private void AddRateCard(TableLayoutPanel table, int column, int row, string key, string caption, string tooltip)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(3), BackColor = CardBackground, Padding = new Padding(8, 4, 8, 3) };
        var value = new Label
        {
            Text = "0 Hz",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 14F),
            ForeColor = Accent,
        };
        var label = new Label { Text = caption, Dock = DockStyle.Top, Height = 18, ForeColor = MutedText };
        panel.Controls.Add(value);
        panel.Controls.Add(label);
        _rateValues[key] = value;
        _toolTip.SetToolTip(panel, tooltip);
        _toolTip.SetToolTip(value, tooltip);
        _toolTip.SetToolTip(label, tooltip);
        table.Controls.Add(panel, column, row);
    }

    private void SetButton(string name, bool down)
    {
        if (!_buttonIndicators.TryGetValue(name, out var label)) return;
        label.BackColor = down ? Active : Inactive;
        label.ForeColor = down ? Color.White : PrimaryText;
    }

    private static void ConfigureActionButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = CardBackground;
        button.ForeColor = PrimaryText;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Border;
        button.FlatAppearance.MouseDownBackColor = Inactive;
    }

    private static void ConfigureNavigationButton(Button button, string text)
    {
        button.Text = text;
        button.Size = new Size(108, 30);
        button.Margin = new Padding(0, 0, 4, 0);
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.ForeColor = MutedText;
        button.BackColor = HeaderBackground;
    }

    private void ShowPage(bool raw)
    {
        _rawPageVisible = raw;
        _overviewPage.Visible = !raw;
        _rawPage.Visible = raw;
        if (raw) _rawPage.BringToFront();
        else _overviewPage.BringToFront();
        _overviewNav.BackColor = raw ? HeaderBackground : PanelBackground;
        _overviewNav.ForeColor = raw ? MutedText : PrimaryText;
        _rawNav.BackColor = raw ? PanelBackground : HeaderBackground;
        _rawNav.ForeColor = raw ? PrimaryText : MutedText;
    }

    private void UpdateStartupToggleAppearance()
    {
        _startupCheck.UseVisualStyleBackColor = false;
        _startupCheck.BackColor = _startupCheck.Checked ? Active : CardBackground;
        _startupCheck.ForeColor = _startupCheck.Checked ? Color.White : PrimaryText;
        _startupCheck.Text = _startupCheck.Checked ? "Start with Windows: On" : "Start with Windows: Off";
    }

    private void SetRate(string key, double value, string unit)
    {
        if (_rateValues.TryGetValue(key, out var label))
            label.Text = $"{value:N0} {unit}";
    }

    private static int ToAxisBar(short value) => Math.Clamp((int)value + 32768, 0, 65535);
    private static int Bit(byte value, byte mask) => (value & mask) != 0 ? 1 : 0;

    private void RefreshInputSources()
    {
        _inputSourceMenu.Items.Clear();
        AddInputSourceChoice(new InputSourceChoice(null, "Auto (fastest)"));
        for (uint slot = 0; slot < 4; slot++)
        {
            string state = BridgeEngine.IsXInputSlotConnected(slot) ? "connected" : "offline";
            AddInputSourceChoice(new InputSourceChoice(slot, $"Slot {slot} ({state})"));
        }
        UpdateInputSourceButton();
    }

    private void AddInputSourceChoice(InputSourceChoice choice)
    {
        var item = new ToolStripMenuItem(choice.Label)
        {
            BackColor = InputBackground,
            ForeColor = PrimaryText,
            Checked = choice.Slot == _selectedInputSlot,
            CheckOnClick = false,
        };
        item.Click += (_, _) =>
        {
            _selectedInputSlot = choice.Slot;
            RefreshInputSources();
        };
        _inputSourceMenu.Items.Add(item);
    }

    private void UpdateInputSourceButton()
    {
        _inputSourceButton.Text = _selectedInputSlot.HasValue
            ? $"Source: Slot {_selectedInputSlot.Value}  ▾"
            : "Source: Auto (fastest)  ▾";
    }

    private void RefreshOutputRateMenu()
    {
        _outputRateMenu.Items.Clear();
        foreach (int rateHz in OutputRatePresets)
        {
            string suffix = rateHz == 1000 ? " (default)" : rateHz > 1000 ? " (experimental)" : "";
            var item = new ToolStripMenuItem($"{rateHz:N0} Hz{suffix}")
            {
                BackColor = InputBackground,
                ForeColor = PrimaryText,
                Checked = _settings.VirtualOutputRateHz == rateHz,
                CheckOnClick = false,
            };
            item.Click += (_, _) => SelectOutputRate(rateHz);
            _outputRateMenu.Items.Add(item);
        }

        _outputRateMenu.Items.Add(new ToolStripSeparator());
        var uncapped = new ToolStripMenuItem("Uncapped (experimental)")
        {
            BackColor = InputBackground,
            ForeColor = PrimaryText,
            Checked = _settings.VirtualOutputRateHz == 0,
            CheckOnClick = false,
        };
        uncapped.Click += (_, _) => SelectOutputRate(0);
        _outputRateMenu.Items.Add(uncapped);
    }

    private void SelectOutputRate(int rateHz)
    {
        if (rateHz == _settings.VirtualOutputRateHz) return;

        if (rateHz == 0 || rateHz > 1000)
        {
            string selected = FormatOutputRate(rateHz);
            DialogResult result = MessageBox.Show(
                this,
                $"{selected} is an experimental virtual output target. Battlefield 6 is excluded from rate testing because its phantom firing was reproduced with Panda completely stopped. High-rate output still needs validation in unaffected games and on more PCs.\n\nThe change takes effect immediately. Restart Panda Pro 8K afterward if you also want Steam launched with the matching controller_rate interval.\n\nUse {selected} anyway?",
                "Experimental output rate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes) return;
        }

        int previous = _settings.VirtualOutputRateHz;
        try
        {
            _settings.VirtualOutputRateHz = rateHz;
            _settings.Save();
            _engine.SetVirtualOutputRate(rateHz);
            UpdateOutputRateButton();
            RefreshOutputRateMenu();
            _footerStatus.Text = $"Virtual output target changed to {FormatOutputRate(rateHz)}. Restart through the desktop shortcut to match Steam's processing interval.";
        }
        catch (Exception ex)
        {
            _settings.VirtualOutputRateHz = previous;
            _engine.SetVirtualOutputRate(previous);
            MessageBox.Show(this, ex.Message, "Output rate setting", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateOutputRateButton() =>
        _outputRateButton.Text = $"Output: {FormatOutputRate(_settings.VirtualOutputRateHz)}  ▾";

    private static string FormatOutputRate(int rateHz) =>
        rateHz == 0 ? "Uncapped" : $"{rateHz:N0} Hz";

    private sealed record InputSourceChoice(uint? Slot, string Label)
    {
        public override string ToString() => Label;
    }

    private static string FormatHex(string compact)
    {
        if (compact.StartsWith("Waiting", StringComparison.Ordinal)) return compact;
        var result = new StringBuilder(compact.Length + 80);
        int byteCount = compact.Length / 2;
        for (int i = 0; i < byteCount; i++)
        {
            if (i > 0)
            {
                if (i % 16 == 0) result.AppendLine();
                else result.Append(' ');
            }
            result.Append(compact, i * 2, 2);
        }
        return result.ToString();
    }
}
