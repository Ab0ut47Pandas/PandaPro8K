# Diagnostic and reverse-engineering tools

This directory contains the small programs used to characterize the GameSir G7 Pro 8K, distinguish physical input from virtual output, and isolate the Battlefield phantom-input report. They are research utilities, not required for ordinary Panda Pro 8K use.

Prebuilt versions are attached to releases as `PandaPro8K-tools-v*-win-x64.zip`. Developers can build the same bundle with:

```powershell
.\scripts\build-tools.ps1
```

The commands below assume the repository root. In the prebuilt bundle, the Python programs are under `python`, the PowerShell helpers are under `powershell`, and compiled programs are under `bin`.

## Privacy first

Controller captures can contain identifiers that should not be posted publicly without review:

- HID enumeration can expose a controller serial number and unique Windows device-instance path.
- Raw Input device names can contain per-machine instance IDs.
- Foreground-window titles can contain document names, browser tab titles, or private conversations.
- USBPcap records every USB transfer on the selected host controller or hub, which can include unrelated keyboards, mice, storage devices, audio devices, or security tokens.

The maintained JSON and CSV tools redact unique identifiers by default. Options that reveal identifiers are explicitly named `--include-identifiers`, `--include-device-path`, `--include-device-paths`, or `--include-context`.

Do not publish a PCAP merely because it was produced during a controller test. Extract the relevant controller traffic or share it privately with someone you trust. Historical raw captures and local logs are intentionally excluded from Git.

## Original Python tools

Install their dependencies with:

```powershell
py -3 -m pip install -r requirements.txt
```

### `g7diag.py`

The first raw-HID diagnostic created for the project. It enumerates GameSir HID collections, captures reports with QueryPerformanceCounter-backed timestamps, and summarizes report intervals.

```powershell
py -3 g7diag.py list
py -3 g7diag.py capture --seconds 30 --output capture.json
py -3 g7diag.py summarize capture.json
```

Serial numbers and Windows HID instance paths are redacted by default. To sanitize a capture produced by an older build:

```powershell
py -3 g7diag.py sanitize private-capture.json public-capture.json
```

`--include-identifiers` exists for local debugging. Do not use it for a file you intend to upload publicly.

### `g7monitor.py`

The original Pygame window used to visualize Logo+A mode. It displays buttons, sticks, triggers, report activity, and the vendor report side by side.

```powershell
py -3 g7monitor.py
```

It targets Logo+A identity `3537:10B8`; it is not the current bridge.

### `g7enhanced.py`

Sends the nonpersistent `0F F2` heartbeat and captures GameSir enhanced report `0x12` from the `FFF0:0040` vendor collection.

```powershell
py -3 g7enhanced.py --pid 0x10C6 --seconds 10 --output enhanced-capture.json
```

### `logo_a_state_capture.py`

Passive SDL/Pygame logger for Logo+A controls. It writes changed axes, buttons, hats, and trigger events to standard output.

```powershell
py -3 tools\logo_a_state_capture.py --seconds 60 > logo-a-state.csv
```

### `live_input_trace.py`

Logs game-visible XInput trigger/button transitions and mouse-button state. It was used to separate bridge input from application-side phantom input.

```powershell
py -3 tools\live_input_trace.py --seconds 90 --output logs\input-trace.csv
```

### `trigger_probe.py`

Compares the active XInput trigger values with the hidden virtual DualSense Edge trigger report. The process must be allowed through HidHide to open the virtual Edge.

```powershell
py -3 tools\trigger_probe.py --seconds 25
```

## Native and .NET capture tools

All positional durations below are seconds.

### `panda_probe.exe`

Early native XInput-plus-enhanced-HID proof. It finds GameSir report `0x12`, decodes the four extras and motion fields, and samples the XInput path. Source: `src/bridge/panda_probe.cpp`.

### `XInputChangeCapture.exe`

Logs every observable XInput packet/state change across slots 0 through 3, with a one-second heartbeat while idle.

```powershell
XInputChangeCapture.exe 60 > xinput.csv
```

### `PhysicalXInputChangeCapture.exe`

The same capture source published under a separate executable name so HidHide can allowlist it during physical-versus-virtual isolation tests.

### `GamepadRawCapture.exe`

Receives GameSir gamepad Raw Input and logs report bytes plus changed byte offsets. Unique device-instance components are removed by default.

```powershell
GamepadRawCapture.exe 60 > gamepad-raw.csv
```

Use `--include-device-path` only for a private local trace.

### `EdgeWireCapture.exe`

Reads the virtual DualSense Edge `054C:0DF2` report and logs control changes. The path is redacted by default.

```powershell
EdgeWireCapture.exe 60 > edge.csv
```

Use `--include-device-path` only for a private local trace.

### `AuxInputChangeCapture.exe`

Logs Raw Input mouse/keyboard transitions and asynchronous mouse-button state. Foreground process/title and unique device-instance paths are redacted by default.

```powershell
AuxInputChangeCapture.exe 60 > auxiliary-input.csv
```

`--include-context` and `--include-device-paths` are private diagnostic options. Their output may reveal personal information.

### `run-live-capture.ps1`

Runs one of the console capture programs for a fixed duration and writes its combined output to a chosen file.

## USBPcap helpers

The root scripts `probe-usbpcap.ps1`, `capture-usbpcap.ps1`, and `capture-descriptor-switch.ps1` automate short captures with [USBPcap](https://desowin.org/usbpcap/). Wireshark or `tshark` was used to inspect the resulting PCAP files.

USBPcap generally requires elevation. Select the correct USBPcap hub before a long capture and disconnect unrelated sensitive USB devices where practical. The scripts default to local ignored output directories; raw PCAPs are not suitable for an ordinary public GitHub issue.

## Tools and services not redistributed here

The following third-party programs were used as references or comparison points. Their binaries and licenses belong to their publishers, so this repository links to them instead of repackaging them:

- [ControllerMeta](https://github.com/HK560/ControllerMeta) for downstream controller visualization and polling observations. Its public repository states that only the frontend is currently open source.
- [Gamepadla Polling Rate Tester](https://github.com/cakama3a/Gamepadla) for change-driven XInput comparisons.
- [GameSir Connect](https://www.gamesir.hk/pages/gamesir-connect-software) for controller firmware, profile, and hardware report-rate configuration.
- [HidHide](https://github.com/nefarius/HidHide) for physical/virtual device isolation.
- [HIDMaestro](https://github.com/hifihedgehog/HIDMaestro) for the virtual HID layer. Panda builds against the repository submodule and its small project-specific fork.
- [USBPcap](https://desowin.org/usbpcap/) and [Wireshark](https://www.wireshark.org/) for USB traffic capture and inspection.
- Steam and Steam Input for DualSense Edge recognition, mapping, and game-facing Xbox output.

## What to attach to an issue

For ordinary failures, attach Panda's copied diagnostics and the newest Panda log. For a new controller support request, include:

- Exact controller model and firmware version
- Wired, dongle, or Bluetooth transport
- Windows VID/PID and HID usage page/usage
- Which mode was active
- Whether the extras are unassigned in the manufacturer's software
- A short sanitized `g7diag`-style capture, if the device uses accessible HID reports

Never attach account credentials, private keys, full USB-hub captures, controller serial numbers, unique device-instance paths, or unreviewed foreground-window traces.
