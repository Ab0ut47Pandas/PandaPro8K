# G7 Pro 8K Logo+A Research Log

This is the persistent chronological work log. Raw captures are retained alongside it and named by purpose.

## 2026-08-24 — Initial Logo+A characterization

### Environment

- Windows controller mode: Logo/Home+A over 2.4 GHz dongle
- Live identity: `VID 3537 / PID 10B8`, `GameSir-G7 Pro 8K`
- HID collections: gamepad `0001:0005`; vendor `FFF0:0040`; consumer control, keyboard, mouse, and a second vendor collection

### Tools installed

- Python `hidapi 0.15.0`
- pygame `2.6.1` / SDL `2.28.4`
- Wireshark `4.6.8`, installed through winget
- USBPcap `1.5.4.0`, installed through winget

### Captures

- `capture-idle.json`: 1,977 report-ID `0x43` frames; 988.02 Hz; idle controller
- `capture-after-8k-setting.json`: 2,964 `0x43` frames; 987.83 Hz after user selected 8,000 Hz in GameSir Connect while in XInput/configuration mode and returned to Logo+A
- `capture-analog-motion.json`: 14,979 `0x43` frames; 998.58 Hz during requested stick/trigger activity

### Findings

- The directly readable Logo+A vendor stream is report ID `0x43`, 37 bytes, approximately 1,000 reports/s.
- Selecting 8,000 Hz through GameSir Connect did not alter the `0x43` cadence.
- pygame/SDL enumerates six axes, 15 buttons, and one hat, but the user confirmed its displayed sticks and triggers do not update.
- During the analog-motion capture, only bytes 17–28 of `0x43` varied. Their behavior and the fact that they react while handling the controller strongly indicate IMU/auxiliary fields, not the main stick/trigger stream. This remains an inference until controlled stationary-axis tests confirm it.
- Steam sees functional Logo+A controls through a different path/parser that generic SDL and direct opening of the top-level gamepad collection do not expose.

### Software created

- `g7diag.py`: enumerate, timestamp, capture, save, and summarize GameSir HID reports
- `g7monitor.py`: experimental SDL + vendor-byte live monitor; useful for `0x43`, but not yet a valid stick/trigger monitor
- `TEST_RESULTS.md`: evidence-labelled findings
- `PROTOCOL.md`: confirmed and inferred report structure

### Next action

USBPcap installed but no `\\.\USBPcapN` root-hub devices appeared immediately. Restart Windows to activate the filter driver. After restart:

1. Confirm USBPcap root-hub interfaces.
2. Resolve the `3537:10B8` dongle to its USBPcap root hub and device address.
3. Capture while Steam is reading Logo+A.
4. Run separate idle, left-stick, right-stick, LT, RT, and rear-button phases.
5. Extract interrupt endpoint cadence and distinguish USB transactions, HID reports, and fresh values.
6. Compare Logo+A before/after polling configuration only after establishing the true gamepad endpoint.

### Safety

No raw vendor configuration writes have been sent in Logo+A. Polling register `0x002E` is only a source-code lead and must be read/tested with reversible before/after capture evidence.

## 2026-08-24 — Post-restart USBPcap activation

- Logo+A identity reconfirmed as `3537:10B8` after initially restarting in `3537:0575` mode.
- Elevated USBPcap root-hub probe identified `\\.\USBPcap2` as the only valid capture interface. Hub 1 returned access error 5; hubs 3–12 did not exist.
- Initial probe output paths failed because the USBPcap command-line parser split a project path containing spaces. Subsequent captures used `C:\USBPcapG7` and were copied into the project after completion.
- `idle-logo-a.pcap`: five-second passive USB capture; dongle resolved to USB address 9. Only input interrupt endpoint `0x82` carried recurring input, 37 bytes per completion at about 1 ms cadence.
- `guided-controls.pcap`: 49,673 completed 37-byte endpoint-`0x82` reports over 50.0688 seconds, **992.08 Hz** across the complete capture. No separate faster input endpoint existed.
- Two USBPcap child processes outlived their intended wrappers; exact PIDs 12384 and 14348 were stopped elevated after capture. Capture files finalized normally.
- Strong conclusion: Logo+A's complete host-visible USB input transport is approximately 1,000 Hz. The prior GameSir Connect 8,000 Hz selection does not make this HID identity report at 8,000 Hz.
- `capture-sticks-guided.json`: initial spoken multi-phase stick capture; located stick activity in bytes 7–14, but prompt duration made phase boundaries unsuitable for final axis assignment.
- `capture-left-stick.json`: isolated 10-second left-stick circles. Bytes 7–10 changed; bytes 11–14 stayed zero. 1,002 fresh vector transitions, 100.21 Hz, median interval 10.0001 ms, median 10 repeated reports per vector.
- `capture-right-stick.json`: isolated 10-second right-stick circles. Bytes 11–14 changed; bytes 7–10 stayed zero. 1,002 fresh vector transitions, 100.18 Hz, median interval 10.0000 ms, median 10 repeated reports per vector.
- Stick pairs decode as signed 16-bit little-endian with near-full signed range. Pair order is probably X then Y but needs a clean horizontal-only/vertical-only confirmation. Effective bit depth remains unknown because a scaled 16-bit representation and circular samples cannot establish ADC resolution.
- Decisive stick-rate conclusion: Logo+A transports reports at ~1,000 Hz but supplies new stick vectors at ~100 Hz; approximately nine of ten transport reports repeat the previous stick vector.

## 2026-08-24 — XInput enhanced-channel direction

- User changed the Aimlabs 2.4 GHz controller to XInput/configuration mode: `3537:10C6`.
- Mode exposes an Xbox 360 controller plus a vendor HID collection `FFF0:0040`.
- Vendor collection is silent without initialization.
- Sending the documented nonpersistent heartbeat `0F F2`, padded to 64 bytes once per second, activated enhanced input report `0x12` without changing configuration.
- Eight-second probe received 1,777 64-byte `0x12` reports at 222.23 Hz, median inter-report interval 4.9939 ms.
- This supports a combined architecture: normal high-rate XInput for standard controls plus enhanced `0x12` for extras and IMU. Exact extra-button and gyro fields still require local mapping.
- The first multi-button spoken capture was invalid for naming because the user did not know which physical controls the L4/R4/L5/R5 aliases referred to. Its label assignments were discarded.
- Four replacement captures used unambiguous physical locations and one repeatedly pressed control per file:
  - `capture-rear-left-paddle.json`: byte 60 toggled `0x00/0x08`.
  - `capture-rear-right-paddle.json`: byte 60 toggled `0x00/0x10`.
  - `capture-top-left-extra.json`: byte 63 toggled `0x00/0x01`.
  - `capture-top-right-extra.json`: byte 63 toggled `0x00/0x02`.
- All four extras are independently observable binary fields in XInput mode's enhanced `0x12` report. Use physical-location labels in documentation unless official alias naming is verified.
- `capture-gyro-yaw.json`, `capture-gyro-pitch.json`, and `capture-gyro-roll.json`: isolated repeated physical rotations with no buttons/sticks/triggers.
- Enhanced IMU angular-rate mapping is signed 16-bit big-endian: pitch bytes 14–15, yaw bytes 16–17, roll bytes 18–19, assigned by strongest response in the corresponding isolated motion test.
- Bytes 20–25 are three more signed 16-bit big-endian motion channels and behave like accelerometer/gravity data; exact axis order needs six stationary orientations.
- The 12-byte IMU vector changed in every consecutive `0x12` report in all three captures. Fresh rates: yaw 217.12 Hz, pitch 218.25 Hz, roll 218.33 Hz; median intervals 4.995–4.998 ms. Units, scale, and polarity remain unknown.

## 2026-08-24 — Panda Pro 8K native implementation

- Installed WDK 10.0.26100.6584 and the Visual Studio driver build components. Driver builds must use the x64 MSBuild executable; 32-bit MSBuild cannot load the WDK x86 verification tool on this machine.
- Captured the exact 166-byte Logo+A HID report descriptor from `descriptor-switch.pcap`. The standard gamepad collection uses report ID `0x01`; the vendor collection uses input `0x43` and output `0xA2`, each with a 36-byte payload.
- Created `src/bridge/panda_probe.cpp`, a native C++ reader for XInput plus the heartbeat-activated enhanced `0x12` stream. A ten-second run reconfirmed about 217–228 enhanced/IMU reports per second.
- Created `src/driver/kmdf/PandaVhid.vcxproj` from Microsoft's KMDF virtual HID sample and replaced its identity/descriptor with `Panda Pro 8K`, VID/PID/version `3537:10B8:0134`, and the captured descriptor.
- Added a private device-interface IOCTL for event-driven input submission. It is separate from HID output reports, so Steam rumble traffic cannot be interpreted as input. A submission completes the next pending HIDCLASS read immediately and intentionally does not queue stale intermediate states.
- `Debug|x64` builds, WDK signability validation, SYS signing, and catalog generation all complete with zero errors and zero warnings. Output: `src/driver/kmdf/x64/Debug/PandaVhid/`.
- The generated driver/catalog used a local WDK development certificate. The certificate chain was not trusted. No driver was installed, and no Secure Boot or Windows test-signing setting was changed.

### Remaining decisive work

1. Generalize the private submission packet to carry both the 10-byte standard report `0x01` and 37-byte enhanced report `0x43`.
2. Capture clean Logo+A mappings for every ordinary button, triggers, four extras, and IMU fields so XInput/enhanced `0x12` can be translated without guesses.
3. Install/test the development driver only after the user chooses a safe driver-signing test route.
4. Add signed physical-device suppression only after virtual input is proven, with an obvious fail-open recovery switch.

## 2026-08-24 — Live 8 kHz XInput-mode check without reboot

- Registry reports `UEFISecureBootEnabled=1`; no boot/security setting was modified.
- Attempted a 12-second elevated USBPcap capture while the controller remained `3537:10C6` at the GameSir Connect 8,000 Hz setting. The file contained only 36 hub descriptor frames and no live interrupt transfers, so it is explicitly invalid as a rate measurement. The USBPcap filter is not attached to this re-enumerated interface in the current session.
- Updated `panda_probe.exe` to report XInput API calls separately from genuinely changed `XINPUT_GAMEPAD` states and to enumerate/select XInput users 0–3.
- XInput user 0 is the only connected slot. A one-second live run observed 5,511 distinct gamepad-state changes, alongside 211 enhanced/IMU reports. This confirms a multi-kilohertz standard-control state stream in the current 8,000 Hz configuration; it does not by itself prove exactly 8,000 fresh samples/s because unchanged/quantized samples are invisible through `XInputGetState`.
- Architectural consequence: standard report `0x01` and enhanced report `0x43` must be submitted independently. The virtual driver now accepts both 10-byte and 37-byte reports without imposing a shared timer. Driver rebuild and WDK validation again completed with zero errors and zero warnings.
- Repeated the measurement as a synchronized 15-second guided test while the user continuously circled both sticks and operated both triggers. Results: 98,203 distinct `XINPUT_GAMEPAD` states, mean 6,546.9 changes/s, per-second range 6,364–6,738. The enhanced channel delivered 3,110 reports over the same interval (mean 207.3/s, per-second range 200–212), with a fresh IMU vector in every report.
- Interpretation: the advertised 8,000 Hz setting is consistent with a genuinely multi-kilohertz standard-control path. `XInputGetState` only exposes different quantized states, so this method establishes a lower bound under motion rather than the exact USB polling cadence; repeated/unchanged 8 kHz samples cannot be counted through this API.

## 2026-08-25 — Logo+A stick-rate retest underneath Steam

- An initial requested retest was invalid because the dongle was actually enumerated as `3537:0575`, emitting 64-byte report `0x12`; it was saved as `capture-both-sticks-retest.json` but excluded from Logo+A conclusions.
- After reconfirming `3537:10B8`, Python HID reads failed immediately while Steam's dedicated GameSir HIDAPI reader was active. Used USBPcap below Steam instead, preserving Steam's normal handling.
- `C:\USBPcapG7\logo-a-steam-both-sticks-retest.pcap`: 15.126623 seconds, 15,114 successful 37-byte endpoint `0x82` reports, 999.10 reports/s.
- Both sticks were circled continuously. Left bytes 7–10 changed 1,513 times at 100.02 Hz; right bytes 11–14 changed 1,513 times at 100.02 Hz.
- Both sides had a 10.000 ms median fresh-vector interval and a median of 10 identical HID reports per vector.
- This independently reproduces the earlier isolated 100.2 Hz result while Steam is directly consuming the controller through `SDL_JOYSTICK_HIDAPI_GAMESIR`. Steam receives the 1 kHz transport but does not turn the Logo+A stick fields into 1 kHz-fresh data.
- Repeated with Steam and all `steamwebhelper` processes fully stopped. The first broad `g7diag.py` attempt failed because it tried to read every matching HID collection, including non-readable collections; this was initially but incorrectly suspected to be Steam handle ownership. Restricting capture to `FFF0:0040` fixed it.
- `capture-both-sticks-logo-a-steam-closed.json`: 15,000 report-ID `0x43` frames over 14.999537 seconds, 999.96 reports/s. During continuous circles, left bytes 7–10 changed 1,498 times at 99.87 Hz and right bytes 11–14 changed 1,499 times at 99.94 Hz. Both had 10.000 ms median intervals and 10 repeated reports per vector.
- Conclusion is unchanged and no longer depends on Steam being present: Logo+A exposes a 1 kHz transport with approximately 100 Hz host-visible stick-vector updates.

## 2026-08-25 — HIDMaestro bridge

- Replaced the unsigned custom-driver plan with HIDMaestro's existing virtual HID layer so Secure Boot could remain enabled and the project would not require a separately signed kernel driver.
- Combined standard XInput state with the heartbeat-activated enhanced stream and exposed it as a virtual DualSense Edge. The Edge identity gives Steam four independent extra controls and motion data that XInput cannot represent.
- Added HidHide isolation for the physical GameSir and virtual Edge. Steam is allowlisted and launched with an SDL ignore list for GameSir `10C5`/`10C6`, leaving other controllers alone.
- Corrected the face-button translation so Xbox A/B/X/Y semantics arrive at Steam in the expected positions.
- Added trigger normalization, disconnect cleanup, enhanced-reader restart, XInput reacquisition, and a virtual-device refresh after controller reconnect.
- Built a dark Windows control panel showing live buttons, axes, triggers, motion data, observed rates, raw enhanced reports, logs, and Windows-startup state.

## 2026-08-26 — Virtual-rate stability tests

- A 1,000 Hz virtual-output target completed the initial Battlefield test without phantom trigger input.
- Separate 5,000 Hz and 4,000 Hz targets produced intermittent stuck or repeated firing through Steam Input.
- During the high-rate failures, simultaneous XInput, virtual Edge, and auxiliary traces showed the physical right trigger neutral, the virtual Edge analog R2 value at zero, the digital R2 bit clear, and no matching mouse event.
- Bypassing Steam Input and allowing the game to read the virtual Edge directly avoided the phantom firing, but the game did not consume the Edge-only controls. The evidence therefore points to the Steam conversion path rather than the GameSir decoder or Edge report builder.
- Steam was launched without an explicit `controller_rate` argument during those tests. Matched Steam/virtual-output rate testing remains open; the distributable build keeps 1,000 Hz as its default.
- The public control panel now exposes 250, 500, 1,000, 2,000, 4,000, 5,000, and 8,000 Hz virtual-output targets plus an uncapped mode. The setting persists in local app data, updates the bridge live, appears in diagnostics, and is translated into a matching Steam `controller_rate` argument on the next launcher restart. Rates above 1,000 Hz remain explicitly marked experimental.
