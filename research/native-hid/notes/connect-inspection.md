> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# GameSir Connect code inspection

Inspected October 20, 2026. Installed app version: **1.16.7**, confirmed from the archived `package.json`.

## Result

GameSir Connect is an Electron application. Its main application and UI code can be extracted as JavaScript from `resources/app.asar`; an initial native-code decompilation is unnecessary for these parts. This follows Electron's documented [ASAR packaging model](https://www.electronjs.org/docs/latest/tutorial/asar-archives).

Read-only extraction produced a 944,692-byte `main.js`, a 5,138,902-byte `renderer.js`, and the package metadata. The files are minified but retain useful property names, model definitions, protocol constructors and UI strings. No source maps were listed for these bundles.

All inspection was performed on extracted workspace files. No app code was executed, controller commands sent, settings changed, or firmware flashed.

## Concrete G7 Pro 8K findings

The model definition is `G7ProCE`, product name `G7 Pro 8K`, with separate wired and receiver identities for multiple editions. The Aimlabs configuration identities remain VID 3537 / PID 10C5 (wired) and 10C6 (receiver).

The G7-specific code translates UI values to profile fields and constructs HID reads and writes in JavaScript. For the 1,070-byte G7 profile layout:

| Profile byte offset | Field | Evidence |
|---|---|---|
| `0x002E` | `report_rates_gears` | 32-byte name followed by the function block; report-rate selector is function-block byte 14 |
| `0x002F` | `sensor_quick_mode` | Mapped to UI `motionControlPrecision` |
| `0x0030` | `stick_resolution` | UI conversion is `stickResolution = 4 - rawValue` |

These offsets are established from the app's parser and serializer. They do not establish which settings the controller applies in native Logo+A HID mode.

The inspected polling UI includes choices for 250, 1,000, 4,000 and 8,000 Hz. One UI hint describes holding **M + Home for five seconds** to fall back to 1,000 Hz when a higher-rate device is not recognized. This is an app instruction; the shortcut was not physically tested, and there is no evidence here that it overrides the previously measured 100 Hz fresh-stick cadence in Logo+A mode.

The G7 proxy's quick-update handler constructs `0F 17 55 88` and sends it through the app's HID write path. The common updater entry function separately constructs `0F FC`. These are firmware-update paths, not established high-rate HID gameplay unlocks. Neither command was sent during this inspection.

A raw-button test option was also traced. In the inspected G7 handler, toggling it changes the app-side `rawKeyMode` flag; that handler contains no device write. Its name therefore does not establish a hidden firmware output mode.

## What remains unknown

- No separate control for raising Logo+A HID fresh-stick updates above 100 Hz was established in the inspected paths.
- No additional factory-mode button combination was established.
- The app only needs to know the modes and commands it uses. Controller startup combinations and any unused factory modes may exist exclusively in firmware.
- Some device/DFU operations live in native DLLs, which remain a separate reverse-engineering target.
- An app UI or setting write cannot make the firmware produce data that its active mode does not provide.

## Evidence locations

Extracted bundles and archive inventory: `work/gamesir_connect_inspection/` in this workspace.

Character offsets in the extracted UTF-8 `main.js` identify the inspected code:

- G7 layout constants: approximately 752,863.
- G7 command constructors: approximately 770,483.
- G7 profile field conversion: approximately 772,323–773,575.
- G7 function-block decoder: approximately 774,160–775,160.
- G7 profile serializer: approximately 776,558.
- G7 model declaration: approximately 786,113.
- G7 quick-update and raw-button-mode handlers: approximately 799,181–799,270.
- G7 profile write and HID packet construction: approximately 815,479.

Bundle SHA-256 hashes:

- `main.js`: `8e92492a9009e333bfb860a2c5251dc4417c2b6d0fe1adfc9c617775096c4bb2`
- `renderer.js`: `b54a872361856bb54b56288ad122f46b551457bb1fa9f1258c3cbc32cd44535b`
