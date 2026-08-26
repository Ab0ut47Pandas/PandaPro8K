# Research captures

The original investigation produced raw HID JSON captures, USBPcap files, application logs, screenshots, and temporary process traces.

The JSON input-report captures are published as a separate sanitized release asset named `PandaPro8K-research-captures-v*-sanitized.zip`. Controller serial numbers and unique Windows HID instance paths are replaced with `<redacted>` while timestamps and report bytes remain intact.

## Included JSON sessions

- `capture-idle.json` — initial Logo+A idle enumeration and report stream
- `capture-after-8k-setting.json` — Logo+A stream after selecting 8,000 Hz in GameSir Connect
- `capture-left-stick.json` and `capture-right-stick.json` — isolated stick motion
- `capture-sticks-guided.json` — guided stick sweeps and gate rotations
- `capture-both-sticks-retest.json` and `capture-both-sticks-logo-a-steam-closed.json` — stick-rate retests
- `capture-analog-motion.json` — combined analog-control motion
- `capture-rear-left-paddle.json`, `capture-rear-right-paddle.json`, `capture-top-left-extra.json`, and `capture-top-right-extra.json` — individual extra-control isolation
- `capture-xinput-extras.json` and `capture-xinput-vendor-idle.json` — enhanced XInput-mode vendor-interface work
- `capture-gyro-pitch.json`, `capture-gyro-yaw.json`, and `capture-gyro-roll.json` — isolated controller rotations

Use `g7diag.py summarize <file>` to calculate report timing from a capture. The report layouts derived from these sessions are documented in [PROTOCOL.md](PROTOCOL.md), [TEST_RESULTS.md](TEST_RESULTS.md), and [RESEARCH_LOG.md](RESEARCH_LOG.md).

## Why the PCAPs and live logs are not public

USBPcap operates at the host-controller or hub level. A capture can include unrelated keyboards, mice, storage devices, audio devices, security hardware, and USB string descriptors. Live diagnostic logs can contain foreground-window titles and complete Windows device paths.

Those files are not automatically safe after removing a Windows username. They remain excluded from the repository and release assets. The PowerShell capture helpers and decoded controller findings are public so another researcher can reproduce a focused capture on their own hardware.

## Sanitizing an older JSON capture

```powershell
py -3 g7diag.py sanitize private-capture.json public-capture.json
```

Review the resulting file before sharing it. The sanitizer covers the known `g7diag` serial-number and HID-path fields; it cannot prove that an arbitrary file produced by another tool contains no personal information.
