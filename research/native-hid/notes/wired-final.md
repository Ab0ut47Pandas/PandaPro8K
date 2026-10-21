> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# Native GameSir HID target: 4,000 fresh updates/sec, at least 4,096 output levels

October 21, 2026. Wired target achieved in the saved physical movement test: approximately 4,000 fresh native HID stick states/sec, more than 4,096 exposed codes per axis, all four extra buttons, full axis/trigger travel, and unchanged saved profile/lighting bytes. Normal wired XInput also passed the post-update movement test. The controller-only image preserves gate-2 and removes the native 1-ms report wait.

Later October 21, the separately authorized receiver experiment also passed: wireless native HID now delivers about 3,979 fresh stick changes/sec, 0.2498-ms median intervals, full ranges and four independent extra buttons after cold reconnection. Wireless XInput and configuration reads passed. See [the current wireless results](wireless-final.md). The wired results below predate that receiver update.

## Final wired results

| Check | Before native-rate patch | After native-rate patch |
| --- | --- | --- |
| Fresh native stick states in sustained moving 1-second bins | 1,000/sec in 22 bins | 3,999–4,001/sec in 22 bins; mean 3,999.8/sec |
| Median interval between changed native stick states | 0.9995 ms | 0.2491 ms |
| Observed distinct codes LX / LY / RX / RY | 17,630 / 17,847 / 17,308 / 16,795 | 44,284 / 45,430 / 46,057 / 45,140 |
| Axis range, all four | -32768 to 32767 | -32768 to 32767 |
| Four independent extra buttons | All observed | All observed individually |
| Both trigger endpoints | Not swept in native baseline | Both 0–255; 244 and 232 distinct codes captured |
| A and B | A captured | A and B captured |
| Saved profiles, Shift profile, lighting, active profile | Backed up | Byte-identical |
| Normal wired XInput after update | Pre-update sustained motion about 4 kHz | 3,991.5 changed states/sec; median 0.2483 ms; A/B and 0–255 triggers captured |

The native post-update capture contains 175,323 packets over roughly 45 seconds. Overall report arrivals average 3,890.6/sec; the changed-state span includes a physical pause and averages 3,370.9/sec. These whole-span averages are not the sustained-motion ceiling. Each of the 22 full moving 1-second bins has 3,999–4,001 changed stick states. Host scheduling gives the changed-state interval a p95 of 0.3268 ms; this is not a guarantee of perfectly uniform 250-us delivery.

These are physical native HID reports observed on Windows. They do not establish Steam/game output rate, button-to-screen latency, or effective sensor accuracy. High-resolution axes are in the existing extended native report; the separate standard generic gamepad descriptor and browser recognition were not changed. The IMU was not modified to sample at 4 kHz. This was a short functional test, not extended stability testing.

Evidence: `native-rate-before-after.json`, `wired-native-baseline-native-rate-postflash.summary.json`, `gate-2-wired-xinput-postnative-rate.summary.json`, and `native-rate-profile-comparison.json`. The post-update native raw capture filename includes `native-rate-postflash`; its inherited internal label still says baseline, so use filename and the explicit before/after mapping rather than that historical label.

## Live transport findings

| Connection, native VID/PID 3537:10B8 | Negotiated speed reported by Windows USB hub | Controller-data interrupt IN 0x82 interval |
| --- | --- | --- |
| Receiver, firmware 1.3.5 before receiver patch | Full speed | 1 ms / maximum 1,000 transactions/sec |
| Receiver, modified 1.3.5 after receiver patch and cold reconnection | High speed | 0.125 ms / maximum 8,000 transactions/sec |
| Direct USB, firmware 2.2.9 | High speed | 0.125 ms / maximum 8,000 transactions/sec |

Both connections return a device descriptor with bcdUSB 0x0111. This descriptor field alone therefore cannot establish the actual negotiated bus speed; the hub's connection information was queried independently. On the wired connection, the separate auxiliary interface's endpoints 0x84 and 0x04 use high-speed bInterval 4, or 1 ms. They are distinct from the controller-data endpoint 0x82.

Evidence: `native-usb-connection.json`, `wired-native-usb-connection.json`. The helper performs only the documented GET_NODE_CONNECTION_INFORMATION_EX and GET_DESCRIPTOR_FROM_NODE_CONNECTION queries against the single verified GameSir hub port. It does not change drivers, USB settings, or firmware.

The first 60-second wired input capture contained approximately 988.6 reports/sec, but only zeroed sticks/buttons. It is valid for report arrival cadence only. A subsequent movement-triggered capture and the user's requested restart both captured physical movement successfully.

## Confirmed wired native movement baseline

The restarted 45-second recording contains 45,120 native reports at 997.1 arrivals/sec. During the moving portion, changed stick states have a median interval of 0.9995 ms and approximately 999.7 changes/sec. This is usable native input at about 1 kHz, not the requested 4 kHz.

Each axis spans -32768 to 32767. The observed distinct-code counts are LX 17,630, LY 17,847, RX 17,308 and RY 16,795. The exposed axis codes exceed the 4,096-value target. These are distinct output codes from manual movement, not an effective sensor-bit or accuracy measurement.

A and all four extra-button bits were observed independently. Evidence: `wired-native-baseline-restart-20261021-a.summary.json` and `wired-native-confirmed-motion.json`; the raw capture is preserved under `work/gamesir_firmware_inspection/preflight/`.

The USB-speed query measures the negotiated hardware link; it does not establish that software produces or transfers fresh state at every allowed microframe. Merely editing an endpoint interval would not prove 4 kHz fresh input.

## Resolution interpretation

4,096 distinct axis codes correspond to 12 output bits. The existing native extended packet carries four 16-bit axes. The prior gate-2 receiver capture observed 8,014–8,374 distinct codes per axis. That establishes that the report format is not restricted to 4,096 output codes; it does not prove 13 effective sensor bits, physical accuracy, linearity, or noise performance. The target is to preserve the existing high-resolution axis readings while increasing fresh-state delivery.

## Wired XInput comparison

The normal wired controller is VID/PID 3537:10C5, version 2.2.9. A fresh read of active profile 1 confirms saved report-rate code 4 (4,000 Hz) and stick-resolution setting 4. The motion capture has a median changed-stick interval of 0.2496 ms. Sustained moving 1-second bins contain approximately 3,996–4,001 changed stick states, confirming about 4 kHz at the host through XInput. The whole recording includes idle pauses; its overall 2,730 Hz changed-state average should not be presented as the active-motion ceiling. A/B and full trigger travel were not captured in this particular comparison.

Both modes negotiate high-speed USB on the same hub port, with controller endpoint 0x82 bInterval 1. XInput returns bcdUSB 0x0200; native returns 0x0111 despite the independently verified high-speed link. This descriptor difference alone is not proof of a polling limit. The Windows native HID GET_POLL_FREQUENCY query returned Win32 error 1 (unsupported); no HID polling setting or driver was changed.

Evidence: `wired-xinput-profile-info.json`, `wired-xinput-usb-connection.json`, `gate-2-wired-xinput-comparison-20261021.summary.json`, and `wired-native-windows-poll-query.json`.

## Controller native report wait and new candidate

The producer's mode table directs mode 11 to 0x0602669A. If extended streaming is enabled, 0x060266AE skips report construction while the 32-bit counter at context +0x210C is zero. The counter increment helper at 0x0602DE76 has one direct caller, 0x00120502 in the timer ISR. That call runs after four 250-us ticks or eight 125-us ticks, giving the native report gate a 1-ms cadence. The physical baseline has exactly 1,000 changed states in each of 22 moving 1-second bins, consistent with that gate.

The candidate replaces the four-byte conditional branch `01 f8 f1 00` at runtime address 0x060266AE (application offset 0x265AE) with two two-byte NOPs, `00 00 00 00`. It retains the counter read/reset and all other producer logic. Relative to the tested gate-2 image, three application bytes change; relative to stock, the previous one-byte gate-2 change is also retained. This lets the existing configured USB producer cadence control extended native reports. It does not increase the IMU sampling rate.

The vendor assembler independently reproduced the original branch and the replacement instructions. The package was re-decoded and checked, all known nested CRCs passed, every non-flash payload was preserved, and the vendor's file validator accepted the exact controller target and version. Installed GameSir package files were not modified.

Candidate package SHA-256: `201e5418b434a8eb21b5b8802c757ee26a6c66c912e03f864f42471cdc0550f7`. Candidate application SHA-256: `5ca952c664a069b4226fe4b5ad02d2d53990f418c039f75b2541c6dcd9f13ea0`.

Fresh settings were saved in `profile-native-rate-preflash/`. Recovery on I: was independently mapped to USB VID/PID 4C4A:3442 and BR27 UBOOT1.00, then the vendor DLL confirmed controller target `GS_G7_Pro_CE_DV`, version 2.29. The guarded single update returned 0 after approximately 33.5 seconds. The controller reconnected as normal wired 3537:10C5. All six profile/lighting blobs and the active profile match exactly in `native-rate-profile-comparison.json`. The subsequent physical movement test above confirms the new 4 kHz native behavior. No complete flash readback was performed. The candidate's preparation-time `UNTESTED` filename is retained for exact audit references; the final hardware result is recorded separately.

Evidence: `candidate-gate2-native-rate/validation.json`, `candidate-gate2-native-rate/disassembly.txt`, `candidate-gate2-native-rate/vendor-file-validation.json`, `native-rate-recovery-identity.json`, `native-rate-flash.jsonl`, `native-rate-flash-process-result.json`.

The previous tested gate-2 package is preserved as `GS_G7_Pro_CE_DV_V229_Key-GATE2-ROLLBACK.ufw`; its SHA-256 remains `faee94bd17317f9b26afb1d0d33c8bc2f7980ab50e9a23184bd4c124678f2cd8`. The verified stock restore remains in `outputs/gamesir-firmware-test/`.

## Receiver investigation, subsequently validated

The receiver speed-selection helper at 0x06001540 checks a mode whitelist mask 0x0805 and a rate helper at 0x0600416E. The rate helper clamps its result to 8 while the extended HID flag at receiver context +0x518 is set. A receiver-only patch now removes that native clamp in the non-mode-C branch, using the existing high-speed selection logic. Hardware native queries confirm high speed, including after cold reconnection. The existing whitelist already includes native mode 11 and was not changed.

The receiver's fast stick queue updates signed axes at context +0x50. Its stock native extended builder instead reads the legacy state at +0x221. The validated receiver patch now reads the fast axes while preserving native big-endian output and the legacy trigger pointer. The earlier linear-scan concern about r5 being overwritten at 0x06004C6A was incorrect: that instruction belongs to the separate PS4 builder, which exits independently. Decoding the native hat jump table and tracing reachable branches established that every native path preserves r5=context+0x528. This permits replacing its redundant reload with a separate fast-axis pointer. Details and hardware evidence are in the wireless report.

The controller's direct-USB HID builder is at 0x060269BE, with four 16-bit axes copied directly from the controller context around +0x170 at 0x0602711C. This avoids the receiver's legacy wireless snapshot. Its common USB report producer starts at 0x06026160 and consults profile-rate helper 0x0600363E. The wired high-speed transport is already available, so this is currently the more direct 4 kHz investigation path.

## Native input activation

SDL's GameSir driver sends `A2 01 00` when opening the extended native interface. The wired 2.2.9 handler was traced at 0x06039202: it validates A2 and command 01, then writes 0x0101 to controller context +0x2100 in RAM. This enables the extended state and requests initialization. The live HID output descriptor independently confirms report ID A2 with 36 payload bytes, or 37 bytes including the ID.

The standard activation command was sent once, padded to the live descriptor's 37-byte report length. It completed successfully. Evidence: `wired-stream-activation.json`. It does not flash firmware, modify profiles/calibration, or itself select a new polling rate. Fresh input still requires a physical movement capture before claiming success.

A known Connect firmware-version read (`0F 09`) sent to the native mode's separate 64-byte vendor collection timed out. No further native Connect commands or setting writes were attempted, and the earlier 4,000 Hz setting is not being presented as a new native-mode configuration read.

The movement-triggered capture is `work/gamesir_firmware_inspection/preflight/capture_wired_on_motion.py`. It waits up to five minutes for an axis to exceed 4096 counts, then records 45 seconds. Its intended result is `wired-native-baseline.summary.json`; no result should be inferred if this file is absent or the process timed out.

## Next checks

The requested wired and subsequent wireless native targets passed. The final connection is wireless native HID with the corrected read-only visual tester available. No further firmware update is pending. The user reports correct in-game behavior; downstream Steam/game timing, battery life and extended reliability remain unmeasured. See the wireless report and `receiver-native-final-validation.json` for the final state.

USB interval interpretation follows [Microsoft's endpoint descriptor documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbspec/ns-usbspec-_usb_endpoint_descriptor).
