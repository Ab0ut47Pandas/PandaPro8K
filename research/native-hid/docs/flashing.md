# Flashing the experimental GameSir G7 Pro 8K PC firmware

> [!WARNING]
> **Experimental firmware. A failed flash can leave the controller or receiver unusable.** The firmware was tested on one G7 Pro 8K PC Aimlabs controller and its receiver. Returning the patched hardware to stock has not been tested. Read the [recovery notes](recovery.md) before proceeding.
> The reusable flashing helper follows the updater sequence used in that test. It has passed offline checks and has yet to be used for a physical flash.

## Prepare the files on Windows

1. Install Python 3.10 or newer. Clone the repository or choose **Code → Download ZIP** on GitHub, then open PowerShell in `research/native-hid`. The helper uses the 32-bit runtime and updater DLL from GameSir Connect. The original flash used Connect **1.16.7**. The helper checks the DLL against the one used for that test.
2. Save untouched copies of `GS_G7_Pro_CE_DV_V229_Key.ufw` (controller 2.2.9) and `GS_G7_Pro_CE_DG_V135_Key.ufw` (receiver 1.3.5) in a folder named `stock` under `research/native-hid`. The tested Connect installation had them in `%LOCALAPPDATA%\Programs\GameSir Connect\resources\dfu\packages`. The [recovery notes](recovery.md) list their SHA-256 hashes. If either file differs, stop for that component. These patches are specific to the listed targets and base versions.
3. Save any controller settings you want to keep. Run the following in PowerShell from `research/native-hid` to generate the patched copies.

```powershell
py -3 tools/patch_firmware.py apply controller stock/GS_G7_Pro_CE_DV_V229_Key.ufw --output controller-native-4k.ufw
py -3 tools/patch_firmware.py apply receiver stock/GS_G7_Pro_CE_DG_V135_Key.ufw --output receiver-native-4k.ufw
```

The two commands create files only. The receiver step is for wireless use. You can stop after verifying the controller over USB if you only play wired.

## Flash the controller

1. Close GameSir Connect completely before entering recovery. Unplug the receiver and other GameSir devices.
2. With the controller off, hold **Home/Logo + Menu** while plugging it into the PC with a USB data cable. Hold for about five seconds. Note the recovery drive letter assigned by Windows. If Windows asks to format it, **cancel**.
3. Run the preflight command below. Replace `I` with the actual recovery drive letter. For a custom GameSir Connect installation location, add `--connect-dir "C:\path\to\GameSir Connect"` to both commands.

```powershell
py -3 tools/flash_firmware.py controller --drive I --patched controller-native-4k.ufw --stock stock/GS_G7_Pro_CE_DV_V229_Key.ufw
```

The helper should identify `GS_G7_Pro_CE_DV`, version `2.29` (the updater's label for 2.2.9), and print `Preflight complete`. It checks the Windows recovery drive mapping and the package hashes. Stop if any check fails.

> [!WARNING]
> **The next command writes experimental firmware to the controller.** A failed or interrupted write can leave it unusable. Leave it connected and keep the terminal open until the updater returns.

```powershell
py -3 tools/flash_firmware.py controller --drive I --patched controller-native-4k.ufw --stock stock/GS_G7_Pro_CE_DV_V229_Key.ufw --write
```

After the updater reports success, reconnect the controller normally. Check sticks, triggers and buttons over USB. Start native HID with **Home/Logo + A** and check the four extra buttons. If the updater returns an error or the controller fails to boot normally, stop and consult the [recovery notes](recovery.md) before doing anything with the receiver.

## Flash the receiver for wireless use

1. Finish testing the controller over USB. Then disconnect and turn it off. Keep GameSir Connect closed.
2. Hold the receiver's **pairing button** while plugging it into the PC. Note the recovery drive letter. Cancel any Windows format prompt.
3. Run the preflight command, using the receiver's actual recovery drive letter.

```powershell
py -3 tools/flash_firmware.py receiver --drive I --patched receiver-native-4k.ufw --stock stock/GS_G7_Pro_CE_DG_V135_Key.ufw
```

The helper should identify `GS_G7_Pro_CE_DG`, version `1.35` (the updater's label for 1.3.5), and print `Preflight complete`. Stop if any check fails.

> [!WARNING]
> **The next command writes experimental firmware to the receiver.** A failed or interrupted write can leave it unusable. Leave it connected and keep the terminal open until the updater returns.

```powershell
py -3 tools/flash_firmware.py receiver --drive I --patched receiver-native-4k.ufw --stock stock/GS_G7_Pro_CE_DG_V135_Key.ufw --write
```

After the updater reports success, reconnect the receiver normally and turn the controller on. Check wireless input in normal mode and native HID. The version labels stay at 2.2.9 and 1.3.5. Use the [capture tools](../tools/README.md#motion-triggered-capture) to check whether the input behavior matches the [recorded results](results.md). If the updater returns an error or the receiver fails to reconnect, stop and consult the [recovery notes](recovery.md) before trying again.
