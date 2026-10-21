> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# GameSir G7 Pro 8K: running firmware identified

Inspection date: October 20, 2026.

## Live controller results

The wired Aimlabs controller is currently enumerated as USB VID/PID `3537:10C5`. A version query on its vendor HID collection returned **firmware 2.2.9**.

| Item | Result | Evidence |
|---|---|---|
| Controller firmware | **2.2.9** | Live `0F 09` version read, decoded using the installed GameSir Connect parser |
| Active profile | **Profile 1** | Live `0F 0B` current-profile read |
| Configured report rate | **4,000 Hz** | Live profile 1 byte `0x002E` = 4, interpreted with the app's polling selector |
| Stick-resolution selection | **Highest option, labeled `>12` in the app** | Live profile byte `0x0030` = 0; app converts this to slider value 4, with labels `[8,9,10,11,">12"]` |
| Receiver version | **1.3.5, last recorded earlier today** | GameSir Connect log at October 20, 18:45:29; the current wired version response leaves receiver version empty |

These are firmware identity and configuration reads. Configured 4,000 Hz does not establish measured throughput or fresh input cadence. The highest resolution setting does not establish actual effective sensor precision, and its behavior in other controller modes has not been confirmed.

## Matching local firmware package

The app maps firmware 2.2.9 to `GS_G7_Pro_CE_DV_V229_Key.ufw`, which is present locally alongside its `_No_Key` variant. Its decoded container targets `JL703N`; its header and entry-table CRCs validate.

This establishes a local package with the same reported version. It is not a dump of the running flash and does not establish a byte-for-byte match to the installed firmware.

Subsequent offline work decoded and disassembled the application. It identified a wireless state-update gate consistent with approximately 10 ms / 100 Hz and prepared an offline threshold experiment; see [the firmware research report](wireless-gate.md). No modified firmware has been installed or tested. An undocumented high-rate factory-mode button combination has not been established.

## Historical measurements need a current retest

The saved GameSir native Logo+A HID measurements date to August 24–25, 2026. They establish approximately 1,000 reports per second with approximately 100 fresh stick updates per second for the controller/firmware used in those tests.

GameSir Connect logs separately show controller version 2.2.4 on October 14, then 2.2.9 later that day. The installed firmware version used during the August HID captures was not established from those capture records in this inspection. Consequently the old 100 Hz result must not be presented as a newly measured limit of today's 2.2.9 firmware. A new controlled native HID movement capture is needed to determine its present behavior.

## Read-only procedure

The diagnostic sent only the app's known version, current-profile and register-read requests. The settings read was restricted to four bytes starting at profile offset `0x002E`. It did not send profile writes, calibration commands, heartbeats, mode switches or firmware updates. All diagnostic handles were closed afterward.

Script and raw query evidence: `work/gamesir_connect_inspection/firmware_info_probe.py` and `live_firmware_info.json` in this workspace. Device serial numbers and Windows HID instance identifiers are omitted from the results.
