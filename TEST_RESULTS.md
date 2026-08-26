# GameSir G7 Pro 8K PC/Aimlabs results

## Scope

This file covers the original Logo/Home+A investigation and the later XInput enhanced-channel work. Claims are labelled by the strongest available evidence.

## Device identity

- **CONFIRMED BY LOCAL HARDWARE TEST:** Windows enumerates `VID 3537 / PID 10B8`, product `GameSir-G7 Pro 8K`, release `0x0134`. The unit serial is intentionally omitted.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Interface 0 exposes a standard game-controller collection (`usage page 0x0001`, usage `0x0005`) and a vendor collection (`0xFFF0:0x0040`). Interface 1 exposes consumer-control, keyboard, mouse, and vendor collections.
- **INFERRED:** `10B8` is a distinct Logo+A runtime identity. It is not one of the reference project's documented Aimlabs configuration identities (`10C5` wired, `10C6` dongle).

## Polling measurement (2026-08-24)

- **CONFIRMED BY LOCAL HARDWARE TEST:** The active vendor collection emitted 4,941 reports over about five seconds.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Observed rate: **988.08 reports/s**.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Median interval: **0.9997 ms**; mean 1.0121 ms; min 0.6300 ms; max 4.0127 ms; population standard deviation 0.1333 ms.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Reports are 37 bytes long with report ID `0x43`.
- **CONFIRMED BY LOCAL HARDWARE TEST:** The stream continues while the controller is untouched. Bytes 18, 20, 22 and nearby values fluctuate, consistent with live sensor noise.
- **UNKNOWN:** Fresh stick sample rate. A controlled moving-stick capture is required.
- **UNKNOWN:** Whether the 1 kHz cadence is selected in controller firmware, imposed by this mode's USB endpoint, or is an intentional rate for the `0x43` auxiliary/IMU stream while another path carries faster gamepad data.
- **UNKNOWN:** The standard game-controller collection's raw cadence. Opening it through hidapi returned no reports during this run, plausibly because Steam had the input path open or Windows routes it through another API; this has not been proven.

### After selecting 8,000 Hz in GameSir Connect

- **CONFIRMED BY LOCAL HARDWARE TEST:** The controller was placed in XInput/configuration mode, set to 8,000 Hz in GameSir Connect, and returned to Logo+A mode by the user.
- **CONFIRMED BY LOCAL HARDWARE TEST:** A new three-second capture contained 2,964 `0x43` reports at **987.83 reports/s**, median interval **0.9998 ms**.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Selecting 8,000 Hz in GameSir Connect therefore did **not** change the Logo+A `0x43` vendor/IMU stream from approximately 1 kHz.
- **UNKNOWN:** Whether the setting changed the separate standard gamepad stream; SDL exposes that stream's controls but does not expose transport timestamps.

### USB bus capture beneath Steam

- **CONFIRMED BY LOCAL HARDWARE TEST:** USBPcap captured the Logo+A dongle at USB address 9 on root hub 2 while Steam was running.
- **CONFIRMED BY LOCAL HARDWARE TEST:** The only active input interrupt endpoint was `0x82`, returning 37-byte reports. No separate faster stick/gamepad input endpoint was present.
- **CONFIRMED BY LOCAL HARDWARE TEST:** A 50.069-second capture contained 49,673 completed 37-byte input reports: **992.08 reports/s** over the full span.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Logo+A's complete host-visible USB input transport is therefore approximately **1,000 Hz**, including after selecting 8,000 Hz in GameSir Connect before returning to Logo+A.
- **UNKNOWN:** Fresh stick sample rate within the 1,000 Hz reports. It cannot exceed the host-visible 1,000 Hz report rate but may be lower if values repeat.

### Isolated stick captures

- **CONFIRMED BY LOCAL HARDWARE TEST:** In a 9,988-report capture where only the left stick was circled, bytes 7–10 changed while bytes 11–14 remained zero. The four-byte left-stick vector changed 1,002 times at **100.21 fresh vectors/s**.
- **CONFIRMED BY LOCAL HARDWARE TEST:** In a 9,980-report capture where only the right stick was circled, bytes 11–14 changed while bytes 7–10 remained zero. The four-byte right-stick vector changed 1,002 times at **100.18 fresh vectors/s**.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Fresh-stick intervals had a median of **10.000 ms** for both sticks. Each fresh vector was repeated for a median of **10 HID reports**.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Candidate axes are signed 16-bit little-endian values: left at byte pairs 7–8 and 9–10; right at 11–12 and 13–14. Observed ranges approached the complete signed range (`-32768..+32767`).
- **INFERRED:** The first pair for each stick is X and the second is Y, following the isolated field grouping and conventional layout. A separately timed horizontal/vertical capture should confirm axis order and polarity.
- **UNKNOWN:** Effective sensing bit depth. A 16-bit report representation does not establish 16-bit sensing, and circular sweeps are insufficient for a defensible 12-bit quantization estimate.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Logo+A therefore has approximately **1,000 Hz HID transport but 100 Hz fresh stick updates** on the tested controller/firmware.

## Advertised capability and configuration lead

- **CONFIRMED BY SOURCE CODE:** `gamesir-linux-tools` documents the G7 Pro 8K PC polling ladder as 250/500/1000/2000/4000/8000 Hz and maps it to the Cyclone polling register at `0x002E`, codes 0 through 5.
- **CONFIRMED BY SOURCE CODE:** The reference project documents G7 Pro 8K configuration mode as a 64-byte GameSir vendor protocol using output report `0x0F`, but does not document Logo+A PID `10B8` or prove that register writes are accepted in this mode.
- **UNKNOWN:** Current value of register `0x002E` in Logo+A mode.
- **UNKNOWN:** Whether changing it in configuration mode persists into Logo+A and changes this mode's endpoint/report cadence.

## Logo+A bottom line

On the tested controller and firmware, Logo+A exposes a genuine **~1 kHz raw HID transport** but only about **100 Hz of fresh stick vectors**. Roughly nine of every ten reports repeat the previous stick values. Selecting 8,000 Hz in GameSir Connect before returning to Logo+A did not change either result.

## XInput enhanced side channel

- **CONFIRMED BY LOCAL HARDWARE TEST:** Aimlabs dongle XInput/configuration mode is `3537:10C6` and exposes a vendor collection in parallel with the Xbox controller.
- **CONFIRMED BY LOCAL HARDWARE TEST:** A nonpersistent `0F F2` heartbeat activates 64-byte enhanced report `0x12` at approximately 222 Hz.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Four isolated repeated-press captures mapped the physical extras without relying on ambiguous L4/R4/L5/R5 names:
  - Rear-left paddle: byte 60 mask `0x08`
  - Rear-right paddle: byte 60 mask `0x10`
  - Top-left extra button: byte 63 mask `0x01`
  - Top-right extra button: byte 63 mask `0x02`
- **CONFIRMED BY LOCAL HARDWARE TEST:** Each extra control is a distinct binary field in the enhanced report.
- **UNKNOWN:** Simultaneous combinations have not yet been explicitly tested, although the fields' distinct bit masks strongly suggest combinations are representable.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Three isolated motion captures mapped signed 16-bit big-endian angular-rate channels:
  - Bytes 14–15 respond most strongly to physical pitch.
  - Bytes 16–17 respond most strongly to physical yaw.
  - Bytes 18–19 respond most strongly to physical roll.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Bytes 20–25 form three additional signed 16-bit big-endian motion channels whose gravity/orientation response is consistent with accelerometer data.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Every captured `0x12` report had a different 12-byte IMU vector in all three motion tests—no duplicated IMU vectors.
- **CONFIRMED BY LOCAL HARDWARE TEST:** Observed fresh-vector rates were 217.12 Hz (yaw test), 218.25 Hz (pitch), and 218.33 Hz (roll), with median intervals of approximately 4.995–4.998 ms.
- **UNKNOWN:** Physical units, scale factors, sign conventions, and exact accelerometer axis order require stationary six-orientation calibration.

## Phantom-trigger control reproduction (2026-08-26)

- **CONFIRMED BY LIVE PROCESS AND PNP INSPECTION:** Panda Pro 8K was not running and no virtual DualSense Edge was present.
- **CONFIRMED BY LOCAL HARDWARE TEST:** The controller was connected directly to Steam in Logo+A mode as `3537:10B8` while Battlefield was running.
- **CONFIRMED BY USER OBSERVATION:** Battlefield repeatedly fired without a physical trigger press, reproducing the symptom previously seen during 4,000 and 5,000 Hz bridge sessions.
- **CONFIRMED BY SYNCHRONIZED TRACE:** Battlefield remained foreground during the reported firing interval while SDL exposed LT and RT as exactly `0.000000`, all buttons released, and no trigger, button, or hat transitions.
- **CONFIRMED BY SYNCHRONIZED TRACE:** No GameSir keyboard or mouse raw-input event occurred during the reproduction.
- **CONFIRMED BY STEAM LOG:** Steam loaded its generic gamepad configuration for the Logo+A controller and mapped HID axes 4 and 5 to its left- and right-trigger groups.
- **CONCLUSION:** Panda, HIDMaestro, the virtual DualSense Edge, and high virtual-output rate are not necessary for the phantom firing. The failure occurs downstream of a neutral physical controller state.
- **CONFIRMED BY EXTERNAL REPORTS:** Battlefield players have reproduced the same firing, sprint failure, and input-glyph switching with multiple unrelated controller brands. EA acknowledged the reports and said they were forwarded to the game team.
- **TESTING CONSEQUENCE:** Battlefield 6 is excluded from Panda rate pass/fail testing. The adjustable-rate matrix must be run in unaffected games.
- **UNKNOWN:** The exact Battlefield-side mechanism. Steam Input interaction and the game's controller handling remain plausible; anti-cheat explanations posted by players are unconfirmed.
