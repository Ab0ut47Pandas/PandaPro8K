> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# GameSir HID investigation: starting points

Inspected October 20, 2026. This pass read decoded application binaries only; it sent no controller commands and made no firmware changes.

**Follow-up:** disassembly has now traced the controller's 13-byte wireless state to the receiver's extended HID builder and identified a minimum gate consistent with 10 ms / 100 Hz. An offline threshold experiment and package roundtrip checks are complete. See [the follow-up research report](wireless-gate.md). A fresh stock-firmware measurement and device acceptance of modified firmware remain unverified. The string anchors below record the earlier investigation's starting points.

## First question to resolve

Find where the native HID path refreshes its outgoing stick values, comparing its behavior with XInput. USB packet frequency and the frequency of fresh stick values are separate measurements.

The archived August captures showed roughly 1,000 received HID reports per second and roughly 100 fresh stick updates per second through the receiver. The capture firmware version was not established. Before treating this as a limitation of current controller firmware 2.2.9, repeat native HID measurements on the current firmware, wired and through the receiver.

- If wired HID is fast and receiver HID is slow, prioritize the receiver's wireless input and HID output paths, while allowing for a controller mode difference between wired and wireless operation.
- If both are slow, prioritize the controller's mode-dependent stick/report update path and look for a shared scheduling decision.
- If both are fast now, the old behavior no longer establishes a firmware problem to patch.

## Verified binary anchors

Offsets below are byte offsets in the extracted `app.bin`, not offsets in an update package or known patch locations.

| Image | String | String file offset | Raw candidate pointer occurrence |
| --- | --- | --- | --- |
| Controller 2.2.9 | `ScanStickInit` | `0xA262F` | `0x24106` |
| Controller 2.2.9 | `usb change xinput mode!` | `0xA6A40` | `0x3422` |
| Controller 2.2.9 | `usb change dinput mode!` | `0xA6A60` | `0x3474` |
| Controller 2.2.9 | `joypad task init` | `0xA6C30` | `0x4D356` |
| Receiver 1.3.5 | `usb change xinput mode!` | `0x872C0` | `0x13CA` |
| Receiver 1.3.5 | `usb change dinput mode!` | `0x87300` | `0x13EA` |
| Receiver 1.3.5 | `joypad task init` | `0x873F0` | `0x2E764` |
| Receiver 1.3.5 | `[HS24G]24g input rate=%d` | `0x911BA` | Not found with the simple literal scan |
| Receiver 1.3.5 | `[HJC_TIMER]stick_packet_cnt = %d` | `0x93C1F` | Not found with the simple literal scan |

Candidate pointer addresses were calculated as `0x06000100 + string_file_offset`, consistent with the package entry address, then searched as four little-endian bytes. Some string references may require relocation or instruction decoding; absence from this simple scan does not mean the strings are unused. Disassembly must establish the actual referencing routines and their behavior.

The receiver's input-rate and stick-packet-counter logs are particularly relevant leads because they may help distinguish wireless input processing from USB report generation. Their names alone do not establish what is counted, how often the counter changes, or which mode uses the code.

## Trace to build in disassembly

`stick scan / wireless receive -> latest stick state -> mode-specific report builder -> USB endpoint sender`

Follow the XInput and DInput mode-switch paths to the callbacks or mode variables they select. Identify the two report builders, their source state buffers, and their scheduling triggers. Look for timer callbacks, counters, mode-specific divisors, and conditions that permit a buffer refresh. Determine whether repeated HID packets use old data even while a newer state exists elsewhere.

Search for a 10 ms schedule or one-in-ten update condition only as a hypothesis suggested by the historical measurement. A literal `10` or a USB polling setting by itself is not evidence of the cause.

After freshness is understood, trace whether the extra buttons are included in the HID report and exposed in its descriptor. Changing advertised report fields or a resolution setting alone does not supply missing button data or fresh stick samples.

## Analysis files

The binaries, hashes, and offline decoder are in `gamesir-firmware-research/`. The extraction report is `gamesir-firmware-inspection.md`; subsequent disassembly evidence, the timing hypothesis, and offline patch validation are documented in `gamesir-firmware-research-progress.md`. No modified firmware has been installed or tested on the device.
