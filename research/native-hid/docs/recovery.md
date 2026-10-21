# Reverting and recovery

Keep an untouched copy of both stock packages before doing anything with the hardware. The controller and receiver have different targets and different files. A regular G7 Pro is not the G7 Pro 8K PC target used here.

The patches require these exact stock sources:

- Controller: `GS_G7_Pro_CE_DV_V229_Key.ufw`, SHA-256 `4d2982df76d92f0ef7564a98005c8b01c25e0c9d1f995becbe1afe72e55882c4`.
- Receiver: `GS_G7_Pro_CE_DG_V135_Key.ufw`, SHA-256 `439e185b5e4dc3cf62e359aca82775a878bae9af7a05ce0c904588561c6d262a`.

These were bundled with the tested GameSir Connect 1.16.7 installation under `%LOCALAPPDATA%\Programs\GameSir Connect\resources\dfu\packages`. Another installation or newer release may have different files. The patcher will refuse a mismatch. Changing the filename doesn't make another firmware compatible.

## Reverse a patched file

The offline patcher can reconstruct the original stock package from the exact modified file:

```powershell
py -3 tools/patch_firmware.py apply controller controller-native-4k.ufw --reverse --output controller-stock-restored.ufw
py -3 tools/patch_firmware.py apply receiver receiver-native-4k.ufw --reverse --output receiver-stock-restored.ufw
```

Those operations were checked against the originals byte for byte. They create firmware **files**; they do not restore a connected device. A successful file reversal doesn't prove a future hardware recovery will succeed.

## GameSir's recovery route

[GameSir documents forced updates](https://gamesir.com/support/manuals/gamesir-g7-pro-aimlabs-edition) for the controller and receiver. Its normal recovery flow uses GameSir Connect to install stock firmware. That flow is described as installing the latest firmware, not selecting any historical release.

The controller combination that actually entered recovery on our test unit was **Home/Logo + Menu while connecting USB**, held for about five seconds. The receiver entered recovery by holding its **pairing button while inserting it into USB**. The receiver recovery is separate from the controller recovery. The official page also contains a Mode + X + B controller procedure; that wasn't the combination that established recovery on this unit.

During research, Connect stayed closed so its automatic stock update wouldn't race the guarded vendor-DLL updater. Recovery exposed a BR27 `UBOOT1.00` device. We checked the physical USB/drive mapping and queried the target with GameSir's DLL before each write. The controller target was `GS_G7_Pro_CE_DV`; the receiver target was `GS_G7_Pro_CE_DG`.

The vendor updater returned success once for each candidate. Its receiver worker uses dual-backup burning 0; the controller had the vendor's G7-specific dual-backup setting. Neither establishes automatic rollback or protection against every interrupted write.

If Windows offers to format a recovery drive, **cancel**. It isn't an ordinary storage drive. Don't assume someone else's drive letter, USB instance path or device PID identifies your recovery target.

## What we have and haven't verified

We preserved and hash-checked the original stock packages. The vendor file validator accepted the stock receiver package. Both devices entered recovery, accepted the modified images and booted back into normal operation. The receiver paired again without a manual re-pair, then passed another cold reconnection test.

We have **not** reflashed stock after these changes, tested a downgrade to an older release, interrupted a flash, or recovered a deliberately broken image. There is no tested one-click restore in this release. The original one-off flashing helpers were tied to this PC's live USB identity and are not distributed as a general-purpose flasher.

A version number alone won't identify the modified firmware. Controller 2.2.9 and receiver 1.3.5 remain the reported labels. For review and restoration, use the complete file hashes.

The release ships the reviewed patch deltas and offline tools, not full vendor firmware or GameSir's updater DLLs. This lets other people inspect and reproduce the files without turning a one-unit experiment into an unattended flashing utility.
