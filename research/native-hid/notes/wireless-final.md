> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# GameSir receiver native high-rate experiment

October 21, 2026. The requested wireless native HID improvement passed physical testing and a cold receiver reconnection. Fresh stick changes increased from about 493/sec to about 3,979/sec during motion. The median fresh interval changed from 2.00 ms to 0.2498 ms. All four independent extra buttons remain available. Normal wireless XInput and configuration reads also passed.

The controller already has the validated gate-2 and wired-native-rate changes. They are being preserved. This experiment changes the receiver only.

The fresh receiver native baseline has about 999.3 reports/sec and 492.9 changed stick states/sec during the moving span, with a 1.9996-ms median fresh interval. All axes reached -32768 to 32767. The raw capture and summary are named `gate-2-receiver-native-motion-wireless4k-preflight.json` and `receiver-native-baseline-wireless4k-preflight.summary.json` under the existing capture and 4k-investigation folders. The fresh USB hub query confirms full-speed native mode, endpoint 0x82 bInterval 1: `outputs/gamesir-4k-investigation/wireless4k-receiver-usb-before.json`.

## Measured results

| Check | Before receiver patch | After patch and cold reconnection |
| --- | --- | --- |
| Fresh stick changes during moving span | 492.9/sec | 3,978.5/sec |
| Median fresh interval | 1.9996 ms | 0.2498 ms |
| Fresh interval p95 after reconnection | Not used for comparison | 0.2961 ms |
| Active one-second bins | About 500 changes/sec | 12 bins, 3,820–4,000 changes/sec; mean 3,966.7 |
| Receiver native negotiated USB speed | Full speed | High speed, confirmed before and after cold reconnection |
| Native endpoint 0x82 interval | 1 ms | 0.125 ms |
| Stick ranges | All four -32768 to 32767 | All four -32768 to 32767 |
| Observed distinct axis codes after reconnection, LX / LY / RX / RY | 8,766–8,993 per axis in baseline | 30,444 / 32,060 / 32,240 / 31,056 |
| A/B, full trigger travel, independent L4/R4/L5/R5 | Captured in baseline | All captured in first native post-flash test; A and all four extras recaptured after reconnection |
| Normal wireless XInput | Separate earlier sanity test, not a fresh stock A/B | 20 active bins, mean 3,962.4 changes/sec, 0.2499-ms median, full axes/triggers and A/B |
| Normal configuration protocol | Available before patch | Version, active profile and rate/resolution reads passed; still configured for 4,000 Hz |

The first 45-second post-flash native capture observed 12 active one-second bins averaging 3,892.8 fresh changes/sec and a 0.2498-ms median interval. Its manual direction holds and pauses lower the whole-span fresh average; the complete per-second counts are preserved. The second recording, after turning the controller off and unplugging/reinserting the receiver normally, observed 49,378 changing stick states over the moving span at 3,978.5/sec. The whole 25-second capture contains 95,858 report arrivals at 3,824.7/sec and includes an idle portion used for buttons. These figures distinguish fresh states, report arrivals and active-motion windows rather than treating them as interchangeable.

The user confirmed correct in-game behavior. The live tester had plotted raw positive-up Y as downward; its display sign was corrected without altering firmware, captured input or the native decoder. The tester now uses the native axis convention. Its source backup and pre-fix status are preserved.

The report format and native PID remain unchanged. This improves the existing extended GameSir native stream; it does not make the standard browser Gamepad API expose that stream, establish Steam's downstream processing rate, or create a new generic HID descriptor.

## Candidate

Based on the unchanged installed receiver package `GS_G7_Pro_CE_DG_V135_Key.ufw`, version 1.3.5. Package stock SHA-256: `439e185b5e4dc3cf62e359aca82775a878bae9af7a05ce0c904588561c6d262a`.

Two receiver changes:

1. At runtime 0x0600418C, set r0 to zero instead of one in the non-mode-C rate helper branch. This removes the forced minimum divisor 8 when extended native reports are active, retaining the advertised configured rate and the separate existing mode-C/global clamps.
2. Replace the 40-byte axis-copy block at 0x06004DDA. Read signed-16-bit axes from the existing XInput fast buffer at receiver context +0x50 and emit the same big-endian native axis bytes. Keep the original context +0x21F trigger pointer and all button/IMU encoding.

The existing high-speed USB mode whitelist already includes native mode 11 and was not edited. The candidate uses the existing speed-selection and re-enumeration logic after removing the rate clamp. Hardware queries now confirm high-speed native operation, including after cold reconnection. This is a short functional result, not extended reliability testing.

An earlier linear scan raised concern that r5 was clobbered at 0x06004C6A before native axes. Decoding the native hat jump table and tracing reachable branches resolved that concern: the clobber belongs to the separate PS4 builder, which exits independently. Every native hat case reaches the axis block with r5 still equal to context +0x528. The redundant r5 reload can therefore supply space for a separate fast-axis pointer. This control-flow check is recorded in `candidate-receiver-fast-native/validation.json`.

The vendor assembler generated the replacement block without paired instructions. A small interpreter executed its actual disassembly for 625 extreme-value combinations and 1,000 deterministic random vectors. It verified native byte order, the legacy trigger pointer, preserved registers, and every other report byte. Candidate length is unchanged. Package roundtrip, nested CRC checks and re-decoding passed; non-flash payloads and installed package files remain unchanged.

Candidate package SHA-256: `26e7e593fcfabc3c86f98e5888c61a3e6cac38e6d477c5b70b9abb9b61e55767`. Candidate application SHA-256: `8242f0737f45c50755f243d62eff008311ece6a9be0b863aab63d2cea60a9449`. The vendor file validator accepts both the candidate and stock restore as `GS_G7_Pro_CE_DG`, version 1.35. File acceptance was followed by the separate physical tests above. The preparation-time `UNTESTED` filename is retained to preserve exact audit references.

The verified stock restore is preserved in this folder. No firmware target/PID override, key fallback or installed GameSir app edits are used. The installed vendor worker sets dual-backup burning to zero for this receiver (its special dual-backup list contains the DV controller and XREAL only) and calls the updater with arguments `false, true, null`.

## Update and recovery record

Receiver recovery was freshly mapped to BR27 UBOOT1.00 on I:, USB parent `4C4A:3442` at `<device-instance>`. The vendor query confirmed receiver model `GS_G7_Pro_CE_DG`, version 1.35 and description `br27uboot1.00`. The guarded updater set dual-backup mode to zero, used the installed vendor's `false, true, null` call pattern, and returned zero. See `receiver-native-recovery-identity.json`, `receiver-native-flash.jsonl`, and `receiver-native-flash-process-result.json`.

With the controller still powered off after the update, the receiver enumerated as `3537:0575` on the same physical USB instance/port. That post-update transition identifies it as this receiver's disconnected/default identity in this observation; do not assume a different device from an older diagnostic solely because of that PID. No input result is inferred until the controller reconnects and the expected native identity appears.

The receiver paired again without a manual re-pair. Cold reconnection and subsequent movement/extras checks passed. The final connection is receiver native HID 3537:10B8, firmware label 1.3.5, with the corrected read-only visual tester left running. No further update is pending.

The receiver recovery button procedure follows [GameSir's official manual](https://gamesir.com/support/manuals/gamesir-g7-pro-aimlabs-edition). Connect is deliberately kept closed while using the guarded vendor DLL helper so its automatic stock updater does not run.

## Evidence and limits

- Final combined evidence and nine matching file hashes: `receiver-native-final-validation.json`. All recorded target/regression/reconnection checks passed. Hash checks cover both candidates, both stock restores, prior controller rollback, and all four installed original firmware packages. They are file-preservation checks, not a readback of running flash.
- First native comparison: `receiver-native-before-after.json`; its remaining-check note describes that earlier checkpoint and is superseded by the final validation.
- Native USB queries: `receiver-native-usb-after.json` and `receiver-native-usb-reconnect.json`. Both confirm high speed from the live USB hub independently of bcdUSB 0x0111.
- Configuration reads: `wireless-xinput-profile-info-after.json`. Controller label 2.2.9, receiver label 1.3.5, active profile 1 and 4,000 Hz. Full Connect GUI editing, calibration and updating were not exercised.
- Raw captures: `work/gamesir_firmware_inspection/preflight/gate-2-receiver-native-motion-wireless4k-{preflight,postflash,reconnect}.json` and `gate-2-wireless-xinput-receiver-native-postflash.json`. Native summaries are under `outputs/gamesir-4k-investigation/`; the XInput summary is under `outputs/gamesir-firmware-test/`.
- Stock receiver restore: `GS_G7_Pro_CE_DG_V135_Key-STOCK-RESTORE.ufw`, SHA-256 `439e185b5e4dc3cf62e359aca82775a878bae9af7a05ce0c904588561c6d262a`.

Observed distinct output codes do not establish effective sensor bits, accuracy, or physical input latency. The 0.25-ms figure is a median interval between host-observed changing axis states, not button-to-screen latency. Independent extra buttons and triggers still use their existing slower radio state path; the fast-buffer patch accelerates axes. Battery life, extended reliability, rumble, IMU behavior and end-to-end Steam/game timing were not measured. No full flash readback or pairing/NVM dump was made. The controller firmware, model checks, USB PID and installed GameSir application were not changed during this receiver experiment.
