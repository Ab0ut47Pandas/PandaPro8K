# The other questions we checked

## 8BitDo Ultimate 2 Wireless

This started with an Ultimate 2 Wireless and its own 2.4 GHz receiver. We wanted the independent extra controls in Steam without losing the finer stick output.

On the tested unit, native DInput used four 8-bit axis fields with range 0–255. The descriptor confirmed that limit; the sweep captured all 256 values on one axis. XInput sweeps captured thousands of different codes per axis.

| Axis | DInput codes observed | XInput codes observed |
| --- | ---: | ---: |
| LX | 254 | 3,218 |
| LY | 256 | 2,990 |
| RX | 255 | 2,441 |
| RY | 255 | 2,468 |

The DInput path still delivered about 1,000 reports/sec. Rate and resolution are separate. Those XInput counts came from different manual sweeps, so they aren't maximum-resolution specifications or a measure of sensor accuracy.

In a separate XInput test, PL, PR, L4 and R4 produced no independent states in the monitored XInput, Raw Input, keyboard, mouse or consumer-control paths. A/B controls did register. The passively opened vendor collection was silent. That limits a straightforward remapper; it doesn't rule out an undiscovered vendor request or firmware route.

Making an 8-bit number bigger in software can't recover positions discarded before transmission. Circularity correction can change the response shape, but it doesn't restore lost resolution. No 8BitDo firmware was changed.

## Is DInput inherently limited to 8 bits or 100 Hz?

We didn't establish a universal limit like that. The firmware, actual HID report and selected backend matter. The 8BitDo's 8-bit fields and the GameSir's slower native state were specific observations on those devices.

The GameSir extended native report already has four signed 16-bit axes. The patched physical native recordings demonstrate roughly 4,000 fresh changed states/sec on this setup. Calling the mode DInput doesn't describe every stage Steam uses to read it.

## Steam

The inspected Steam client binary contained `controller_rate` and a help string describing a default active interval of 2,000 microseconds / 500 Hz. That finding establishes the string in that build. It doesn't establish the runtime value, a hard ceiling for every backend, or the game-facing output rate in this firmware test.

The existing PandaPro8K bridge has its own submission-rate work documented elsewhere in the repository. Virtual submissions, physical fresh state, Steam processing and a game's reads are different measurements. This firmware release measures the physical side.

## Why fast XInput doesn't need a loophole

[XINPUT_GAMEPAD](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_gamepad) already has signed 16-bit stick fields. A 4,096-code axis fits without changing that format. Calling XInputGetState very often doesn't prove the controller produced that many new states; the returned values need to change.

High-speed USB can service interrupt endpoints in 125-microsecond microframes. Full speed has a minimum 1 ms interrupt interval. The controller, firmware, radio and host still need to supply fresh data at that rate. [Microsoft's endpoint documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbspec/ns-usbspec-_usb_endpoint_descriptor) explains the speed-dependent interval rules.

## When does more become overkill?

For a simple model where changes arrive randomly relative to a periodic report, average waiting time is half a report interval:

| Fresh rate | Interval | Average wait in that model |
| --- | ---: | ---: |
| 100 Hz | 10 ms | 5 ms |
| 500 Hz | 2 ms | 1 ms |
| 1,000 Hz | 1 ms | 0.5 ms |
| 4,000 Hz | 0.25 ms | 0.125 ms |
| 8,000 Hz | 0.125 ms | 0.0625 ms |

That makes 100 to 1,000 Hz a much bigger change than 4,000 to 8,000 Hz. It's a timing model, not a measured controller latency or a claim about what every person can feel.

Useful stick precision depends on noise, mechanics, calibration, dead zones, filtering and the game's response curve. A 16-bit packet field doesn't establish 16 useful sensor bits. The sensible goal here was to preserve the existing axes and remove a measurable firmware bottleneck, then check the resulting reports.

## Battery and Connect

Battery life hasn't been measured. The receiver patch reuses an existing fast radio buffer, but that alone doesn't prove zero battery impact. The earlier controller timing change increases legacy wireless state opportunities. A controlled stock/patched runtime comparison is still needed.

Connect's known version, active-profile and bounded settings reads still worked in normal XInput mode after the receiver patch. Full GUI editing, calibration and update behavior weren't tested. A firmware update from Connect could overwrite these modifications.
