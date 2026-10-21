> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# 8BitDo Ultimate 2 Wireless: live mode comparison

Test date: October 20, 2026. Windows PC; same controller and included 2.4 GHz receiver in both modes.

## Confirmed result

The connected controller reports coarser stick positions in native DInput mode than in XInput mode.
DInput declares 8-bit fields with logical range 0–255 for all four stick axes. A movement capture exercised all 256 levels on the left vertical axis.

| Axis | DInput: distinct values captured | XInput: distinct values captured |
| --- | ---: | ---: |
| Left horizontal | 254 | 3,218 |
| Left vertical | 256 | 2,990 |
| Right horizontal | 255 | 2,441 |
| Right vertical | 255 | 2,468 |

XInput counts are observed values during the user’s sweeps, not a measured maximum resolution or proof of sensor accuracy. Sampling duration and movement differ between passes. The DInput 256-level ceiling is independently confirmed by the controller’s HID descriptor.

## Report delivery rate

DInput capture: 60,003 reports over 60.001 seconds; 1000.02 reports/second between the first and last reports.
Median host read interval: 0.9994 ms. 95th percentile: 1.0311 ms.
DInput was therefore delivering approximately 1,000 reports/second over the dongle during this test. Report delivery rate does not measure physical button latency, game processing delay, or screen response.

## Method and identification

- Steam logs identified native 8BitDo DInput using HIDAPI, USB VID/PID 2DC8:6012, with four paddle mappings.
- Windows HID capability queries confirmed four 8-bit stick fields, logical range 0–255, and 34-byte input reports.
- A shared read-only HID handle captured incoming reports while Steam remained running.
- After the user switched modes, Windows identified the same receiver in XInput mode as VID/PID 2DC8:310B; Steam logged XInput usage.
- XInputGetState sampled slot 0 for 50.000 seconds while the user swept the sticks.
- Probe scripts did not write controller reports, inject input, modify Steam settings, install drivers, or update firmware.

## Limits and current state

- This confirms a difference in reported stick granularity; it does not quantify aiming performance or analog sensor accuracy.
- Physical latency, vibration behavior, game compatibility, and long-term stability were not tested.
- Trigger operation and independent paddle presses were not established by these recordings.
- The controller was left in XInput mode after the comparison. To return to native DInput, turn it fully off, then hold B while pressing Home with the rear switch on 2.4G.
