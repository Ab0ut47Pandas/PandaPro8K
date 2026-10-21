# Tools

Python 3.10 or newer is enough for the offline tools. The live native reader/tester requires Windows; the visual tester also needs Tkinter, included with the normal Python Windows installer. No extra Python packages are needed.

Run these commands from the `research/native-hid` directory.

## Exact offline package patcher

```powershell
py -3 tools/patch_firmware.py list
py -3 tools/patch_firmware.py identify "C:\path\to\stock.ufw"
py -3 tools/patch_firmware.py apply controller "C:\path\to\GS_G7_Pro_CE_DV_V229_Key.ufw" --output controller-native-4k.ufw
py -3 tools/patch_firmware.py apply receiver "C:\path\to\GS_G7_Pro_CE_DG_V135_Key.ufw" --output receiver-native-4k.ufw
```

`--reverse` on the `apply` command reconstructs stock from the exact patched file. The tool never opens a device, launches an updater or overwrites the input. It refuses unknown hashes and existing output files. [Recovery details](../docs/recovery.md).

## Visual native tester

```powershell
py -3 tools/live_test.py
```

Use the tested native mode, Home/Logo + A, with exactly one G7 Pro 8K connected. The test opens the 37-byte `FFF0:0040` collection at `3537:10B8`. It shows sticks, triggers, independent extras, report arrivals and fresh stick changes. Move a stick continuously to measure freshness; an idle stick should not be counted as producing thousands of new positions.

The tester reads only. It doesn't activate the stream. Steam/SDL enabled native streaming on the original test system. If you only see a waiting state, check the device mode and that initialization issue before drawing a polling conclusion.

The tester writes a local `live-status.json` with no device-instance path. Its errors log can contain local Python paths, so review it before sharing.

## Motion-triggered capture

```powershell
py -3 tools/capture_native.py --connection receiver --seconds 30 --output my-wireless-test.json
py -3 tools/analyze_capture.py my-wireless-test.json --expected-rate 4000 --output my-wireless-summary.json
```

Use `--connection wired` for a direct USB capture. The reader checks the tested native identity, collection and reported firmware version. It waits up to five minutes for an axis to exceed 4096 codes, then records for the requested duration. Keep both sticks moving for most of that window, then exercise the extra buttons and triggers. Avoid another controller connected in the same native mode.

The saved capture excludes the unique HID path. It preserves all received packet bytes and relative timestamps. The analyzer works offline on both the published native captures and the selected XInput recordings. It separates report arrivals, changed stick states and active one-second bins.

## USB link query

```powershell
py -3 tools/query_usb_connection.py --pid 10b8 --output my-native-usb.json
```

This Windows helper queries the exact GameSir's parent hub using only the two GET connection/descriptor IOCTLs. It doesn't change a driver, registry setting or USB mode. It keeps the negotiated speed and endpoint descriptors but omits the unique device/hub paths from its output. The open-source code still needs those paths internally to address the right hub.

## Offline firmware analysis

```powershell
py -3 tools/inspect_firmware.py --source "C:\path\to\stock-packages" --output "C:\path\to\analysis-output"
```

This decodes the package headers, configuration, application and known CRCs. It doesn't access hardware or write into the input directory. Decoded binaries are local analysis files, not complete flashable packages. `repack_analysis.py` contains the original offline package reconstruction functions; the exact-file patcher above is the simpler way to reproduce this release.

Original candidate-building and control-flow-check code is preserved under `../analysis/original/` for review. Those scripts retain their old research layout and require the external vendor assembler/disassembly files. They aren't a second portable installer. The public patcher reproduces the exact tested packages without those dependencies.

The tools don't measure physical sensor precision, effective sensor bits or button-to-screen latency.
