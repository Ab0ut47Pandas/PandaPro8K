> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# GameSir G7 Pro 8K: offline firmware and mode inspection

Inspection date: October 20, 2026.

## Files inspected

The installed GameSir Connect cache contains controller firmware `GS_G7_Pro_CE_DV_V229_Key.ufw` and `_No_Key.ufw`, and receiver firmware `GS_G7_Pro_CE_DG_V135_Key.ufw` and `_No_Key.ufw`.

All four container headers and entry tables were successfully decoded using the container scrambling algorithm documented by the public-domain [gamesir-fw project](https://github.com/xsyetopz/gamesir-fw). All four header CRCs and entry-table CRCs validate. The decoded target label is `JL703N`.

Each container has ten entries, including `flash.bin`, `flash2.bin`, `isd_config.ini`, `uboot.version`, and OTA/update components. Controller packages are 2,178,368 bytes each; receiver packages are 1,851,328 bytes each.

The configuration and main application have now been decoded from **all four packages**. Both configuration CRCs and application CRCs validate in every package. The original decoder assumed that the configuration began with 32 `#` characters. These configurations instead begin with newlines followed by `[EXTRA_CFG_PARAM]`; correcting that assumption allowed recovery of the package cipher keys and extraction of `app.bin`.

For each component, the `_Key` and `_No_Key` packages produce byte-identical application binaries:

| Component | Application size | Application SHA-256 |
| --- | ---: | --- |
| Controller 2.2.9 | 969,512 bytes | `fa554844a381e8f43a63aa79c8b86de412893c93c8309c533658c30c6633502d` |
| Receiver 1.3.5 | 806,872 bytes | `bf2b2b0965d6bfe11a4059be81a0acea1436dc662b0fd2f580bbf10634a66cca` |

This establishes that the executable content is accessible for reverse engineering. It does not establish that either device accepts modified firmware. `_No_Key` names and successfully decoded checksums do not prove the absence of signature or authorization checks.

Original source-file SHA-256 hashes were checked again after inspection and were unchanged. The inspection did not open a controller, send USB commands, enter update mode, or flash anything.

## Mode information

- **Home/Logo + A:** native HID mode previously tested on this controller. Saved August 24-25 tests measured approximately 1,000 HID reports per second but approximately 100 fresh stick updates per second over its receiver. The firmware version for those captures was not established. They are not a fresh measurement of the currently installed 2.2.9 firmware.
- **View + Menu, held for two seconds:** the [official G7 Pro 8K manual](https://gamesir.com/support/manuals/gamesir-g7-pro-aimlabs-edition) documents cycling XInput/Switch modes over wired or receiver connections. Mode availability can vary with firmware.
- **M/Mode + X + B while plugging in USB:** the same official manual documents forced controller firmware update mode. Its FAQ identifies M/Mode as the button below the D-pad. This is firmware-update access, with no evidence that it unlocks faster HID gameplay or permits modified firmware.
- **Hold receiver sync while plugging in the receiver:** the same official manual documents forced receiver update mode. It says GameSir Connect can automatically install firmware when this mode is detected.

No additional undocumented high-rate native HID mode or factory button combination has been established. Locating one requires analysis of the decoded application's button handling or evidence of an alternate vendor command.

## Processor and useful findings

The chip vendor's public [JL703N SDK Makefile](https://gitlab.zh-jieli.com/JL-Studio/SoundBox/703N/JL703N_SoundBox_1.0.0/-/blob/SDK_1.0.0_patch_00.13/SDK/Makefile) targets `pi32v2` with `-mcpu=r3` and `CONFIG_CPU_BR27`. This is a vendor SDK for the chip family, not GameSir's controller source. The community [Ghidra processor module](https://github.com/kagaimiq/ghidra-jieli) describes its pi32v2 support as very early stage; complete decompilation is not established.

The controller application contains USB descriptors and identifiable logging strings, including `usb change dinput mode!`, `wire pc start dinput mode`, and `STATUS_USB_DINPUT_MODE`. These are useful starting points for locating mode-handling routines. Other strings mention Steam/Horipad and additional controller protocols, but shared library strings alone do not prove those modes are usable on this product.

For follow-up analysis, the controller's `usb change dinput mode!` string begins at application file offset `0xA6A60`. The configured application entry address is `0x06000100`; a matching candidate pointer `0x060A6B60` appears at file offset `0x3474`. This is a code-analysis lead, not a verified patch location.

## What remains before a functional modification

Follow-up disassembly found a 10-unit gate on the controller's 13-byte wireless state block, feeding the receiver's extended HID report. The identified timer pattern is consistent with 10 ms / 100 Hz. A one-byte offline experiment lowers the gate to 2, targeting approximately 500 Hz under that timing interpretation. See [the follow-up research report](wireless-gate.md) for the evidence and limits.

All four unmodified packages now rebuild byte for byte. Experimental controller packages re-decode to the intended application edit and pass every integrity check identified so far. This establishes offline package reconstruction, not device acceptance or runtime correctness.

1. Capture current stock-firmware native HID over both wired and receiver connections to confirm the active report path and timing.
2. Establish the device's image-acceptance checks, recovery, and calibration preservation before attempting a physical update.
3. Test any eventual modification for fresh-state rate, independent buttons, wireless stability, other modes, and shared audio/gyro traffic. A report descriptor change alone does not create faster sampling or fresh reports.

No modified firmware has been installed, and no functional high-rate HID improvement has been measured.

## Reproduction materials

The user-facing analysis files are in `gamesir-firmware-research/` beside this report:

- `decode_firmware.py`: offline decoder, with optional `--source` and `--output` arguments. It has no device-access or flashing code and refuses an output directory inside the original package directory.
- `controller-2.2.9-app.bin` and `receiver-1.3.5-app.bin`: extracted compiled applications for analysis, not complete update packages.
- `controller-2.2.9-isd_config.ini` and `receiver-1.3.5-isd_config.ini`: decoded configurations.
- `inspection.json`: package metadata, validated CRC results, hashes, directory entries, and selected application strings.

Working materials remain in `work/gamesir_firmware_inspection/`. The source references are `reference/ufw.rs` and `reference/cipher.rs` from gamesir-fw; the device flasher from that project was not run.
