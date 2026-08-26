# Panda Pro 8K architecture

## Runtime devices

- Physical controller: GameSir G7 Pro 8K PC/Aimlabs in XInput mode, `3537:10C5` when wired or `3537:10C6` through the 2.4 GHz dongle.
- Enhanced input: vendor HID collection `FFF0:0040`, activated by the `0F F2` heartbeat and returning report `0x12`.
- Virtual controller: DualSense Edge created by HIDMaestro.
- Game-facing controller: Steam Input's normal virtual Xbox controller.

## Data path

1. A tight XInput loop keeps the newest standard button, stick, and trigger state.
2. `panda_enhanced_stream.exe` owns the GameSir vendor collection, sustains the heartbeat, and passes decoded extras plus the original report to the main process over stdout.
3. The bridge merges the newest XInput and enhanced states without maintaining a queue of old frames.
4. Complete DualSense Edge reports are submitted through HIDMaestro at the user's selected target. The default is 1,000 Hz; presets run from 250 through 8,000 Hz and an uncapped mode is available for diagnosis.
5. Steam Input reads the Edge and exposes its configured output to the active game.

The enhanced stream is supervised separately. If the controller powers down, the bridge clears the last enhanced state, retries with bounded backoff, reacquires XInput, and recreates the virtual Edge after the physical device returns.

## Visibility

HidHide isolates both the physical GameSir and the virtual Edge from ordinary applications. The bridge process, enhanced helper, GameSir Connect, and Steam are allowlisted. Steam is also launched with an SDL ignore list for the physical GameSir IDs, preventing Steam from opening both the physical and combined controllers.

The bridge records which HidHide entries it added and removes only those entries on a normal exit. It does not replace the user's existing HidHide configuration.

## Input-path rules

- No fixed-frame buffering or smoothing.
- No UI work in the XInput loop.
- Physical state is always read at the rate supplied by XInput.
- Virtual output is rate-limited independently of physical acquisition.
- Detailed logs describe connection and recovery events; they are not written for every input frame.

## Confirmed enhanced fields

- Report `0x12`, 64 bytes, approximately 217–222 reports/s.
- Rear-left paddle: byte 60 mask `0x08`.
- Rear-right paddle: byte 60 mask `0x10`.
- Top-left extra: byte 63 mask `0x01`.
- Top-right extra: byte 63 mask `0x02`.
- Pitch/yaw/roll: signed big-endian pairs at 14–15, 16–17, and 18–19.
- Accelerometer candidates: signed big-endian pairs at 20–25.

## Earlier driver prototype

The source under `src/driver` predates the HIDMaestro implementation. It proved that a custom virtual HID path was possible, but distributing a new Windows kernel driver would require a production signing and installation pipeline. It is retained for research and is not built, installed, or needed by the current application.
