> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# Ultimate 2 Wireless: extra buttons in XInput

Tested on October 20, 2026, over the included 2.4 GHz receiver in XInput mode (USB VID 2DC8, PID 310B).

The user pressed PL, PR, L4 and R4 separately several times, followed by A and B as controls. The read-only capture lasted 41.91 seconds.

## Observations

- XInput captured A press/release and B press/release. No other button or axis state changes appeared.
- Windows Raw Input captured 6,184 gamepad reports. Their three distinct payloads represented idle, A and B. No additional button bits changed.
- No input events arrived from the receiver's keyboard, mouse or consumer-control interfaces during the capture.
- A passive read of the receiver's vendor HID interface received no reports.
- The probe reported no errors. All diagnostic readers were stopped afterward.

## What this establishes

With the current controller configuration, the monitored XInput and HID input paths did not expose independent PL, PR, L4 or R4 states. A straightforward remapper therefore has no independent extra-button signals to combine with the finer stick output previously measured in XInput.

This does not rule out a vendor command that requests button state, another transport path, or firmware changes. Those possibilities remain unverified. A silent vendor interface during passive reading does not establish that it cannot return state when queried.

The earlier DInput capture exposed 8-bit stick fields, while the earlier XInput sweep produced thousands of distinct values per stick axis. Scaling the captured DInput values to a wider number format would not recover missing stick positions. Circularity correction is a separate operation and does not restore resolution.

No controller settings, firmware or drivers were changed by this test. Raw capture: `work/xinput_extra_inputs_ready2.json` in this workspace.
