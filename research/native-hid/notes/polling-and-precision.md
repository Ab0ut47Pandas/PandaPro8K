> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# GameSir: XInput, polling rate, and useful precision

Checked October 20, 2026. This follow-up used public technical documentation and the firmware files already decoded locally. It sent no controller commands and changed no firmware, controller profiles, drivers, or Steam settings.

XInput already has enough numerical range for a 4,096-level stick. High polling rates depend on the hardware, firmware, transport, and host path. Neither capability requires finding unused bits in the public XInput stick format.

## What “8K” and “4K” mean here

The official [GameSir G7 Pro 8K product page](https://gamesir.com/products/gamesir-g7-pro-8k-pc) advertises 8,000 Hz polling, a 12-bit ADC, and 4,096 joystick levels. These are manufacturer specifications, not measurements of this particular controller in its present mode.

- 8,000 Hz is a frequency: one opportunity every 0.125 ms.
- 4,096 levels is a count of positions along an axis: 12 bits because 2^12 = 4,096.
- A configured 4,000 Hz report rate is a third number, separate from resolution. The earlier live profile read selected 4,000 Hz and the app's highest `>12` resolution option. Those selections do not establish effective throughput or usable sensor precision.

Microsoft's [XINPUT_GAMEPAD](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_gamepad) format provides a signed 16-bit field for each stick axis, covering -32,768 through 32,767. A 12-bit measurement fits easily in that space. Expanding 4,096 sensor codes into a 65,536-code output range does not create additional independent measurements.

## Why fast XInput is possible

There are separate stages:

`physical movement -> sensor conversion -> firmware processing -> radio/USB -> Windows input state -> game read -> simulation/rendering`

The [XInputGetState API](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputgetstate) retrieves the current controller state. It does not specify a universal 125 Hz or 1,000 Hz limit in that function's contract. This absence is not proof that every driver or device supports 8,000 fresh states per second.

High-speed USB interrupt endpoints can be serviced once per 125-microsecond microframe. Full-speed USB interrupt endpoints have a minimum 1 ms interval. This provides a normal USB mechanism for 8,000 transfer opportunities per second when supported by the device and host. The endpoint descriptor is firmware-defined; this pass did not verify the active endpoint configuration or prove every stage of this controller's 8K mode. See [Microsoft's USB endpoint documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbspec/ns-usbspec-_usb_endpoint_descriptor).

Calling XInputGetState 8,000 times per second is not itself evidence of an 8,000 Hz controller. It can return the same state repeatedly. Microsoft documents [dwPacketNumber](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_state) as a state-change indicator, but a rigorous test should also inspect changing stick/button data and distinguish API calls, USB reports, fresh samples, and physical latency. Noise can also create changing numbers without useful intentional movement.

The firmware inspection supports a concrete explanation for the GameSir's mode difference. Its receiver has one 16-bit stick-state source used by XInput and a separate 13-byte state block used by extended HID. The latter has the minimum gate discussed in [the firmware research report](wireless-gate.md). A fresh stock-firmware test is still needed to confirm the active native HID path on this setup.

The additional pass checked the receiver's fast-state queue against the same-chip SDK ROM symbols: `0x1FD38` is `cbuf_get_data_size`, and `0x1FD00` is `cbuf_read`. The routine reads eight-byte samples and reconstructs four 16-bit values into context `+0x50`. This strengthens the identification of the separate stick-data path. It does not establish its runtime rate or availability while native HID mode is active. Reference: [JL703N ROM symbols](https://gitlab.zh-jieli.com/JL-Studio/SoundBox/703N/JL703N_SoundBox_1.0.0/-/blob/SDK_1.0.0_patch_00.13/SDK/cpu/br27/maskrom_stubs.ld).

## Where polling gains become small

For a simplified model with periodic fresh reports, a randomly timed input change, and no other delays, average waiting time until the next report is half the report interval:

| Fresh update rate | Interval | Average wait in this model |
| ---: | ---: | ---: |
| 100 Hz | 10 ms | 5 ms |
| 250 Hz | 4 ms | 2 ms |
| 500 Hz | 2 ms | 1 ms |
| 1,000 Hz | 1 ms | 0.5 ms |
| 2,000 Hz | 0.5 ms | 0.25 ms |
| 4,000 Hz | 0.25 ms | 0.125 ms |
| 8,000 Hz | 0.125 ms | 0.0625 ms |

These are calculations, not measured controller-to-screen latency. Sampling, filtering, radio scheduling, host buffering, game timing, and display timing all remain outside this model. Combining two independently paced stages also needs more than this one-stage calculation.

Under these assumptions, 100 to 1,000 Hz removes 4.5 ms of average polling wait. Moving from 1,000 to 8,000 Hz removes another 0.4375 ms; 4,000 to 8,000 Hz removes 0.0625 ms. The diminishing return is substantial.

An 8K report rate can still make the most recent state slightly newer when a game reads once per frame. A 144 Hz display does not automatically erase every benefit above 144 Hz input. Conversely, 8K reports do not mean the game simulates 8,000 inputs separately each second.

My practical judgment for a thumbstick is to prioritize a stable 1,000 fresh updates per second, low filtering delay, and clean behavior before chasing 8K. Higher rates can be useful for fine optimization if the whole path handles them reliably. This is an engineering priority, not a measured human-perception cutoff.

## How much stick precision is useful?

For an ideal uniform quantizer spanning the entire axis:

| Bits | Available codes | One step as a percentage of full axis span |
| ---: | ---: | ---: |
| 8 | 256 | 0.3922% |
| 10 | 1,024 | 0.09775% |
| 12 | 4,096 | 0.02442% |
| 16 | 65,536 | 0.001526% |

These step sizes use `100 / (2^bits - 1)`. Actual calibration, dead zones, saturation, and response curves can change the usable distribution.

As a scale illustration, if the stick cap moved 20 mm across its full axis, 12-bit spacing would correspond to about 0.00488 mm, or 4.88 micrometers. At 16 bits it would be about 0.305 micrometers. This is a hypothetical travel distance and an ideal linear mapping, not a measurement of this stick or a statement about human hand accuracy.

The useful limit is reached when smaller code steps are outweighed by sensor noise, mechanical variation, calibration error, the game's dead zone/response curve, or movement that the player cannot control repeatably. More bits in the packet do not prove more useful precision. ADC manufacturers distinguish nominal resolution from noise-limited effective and noise-free resolution for this reason; see [Analog Devices' explanation](https://www.analog.com/en/resources/technical-articles/noise-enob-and-effective-resoluition-in-analog-to-digital-converter-circuits--maxim-integrated.html).

There is no universal bit count that becomes “too precise.” Extra genuinely useful bits are fine; a larger number alone is a weak design target. In this investigation I would preserve the GameSir's existing higher-resolution fields and measure their actual behavior before reducing precision. Moving from 8 to 12 bits, with identical calibration and curves, subdivides the position grid; it does not inherently increase maximum turn speed. A vendor setting may change other processing too, so its label alone cannot establish that comparison.

Stick resolution also does not correspond directly to display pixels. A game may use stick position to select a rotation speed, and then integrate that movement over time.

## Why one universal “elite” driver cannot fix everything

Software can decode vendor protocols, expose extra buttons, preserve available precision, timestamp samples, and translate input into formats games understand. There are already projects addressing parts of this: [SDL's GameSir driver](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_gamesir.c) decodes the extended report, and [HIDMaestro](https://github.com/hifihedgehog/HIDMaestro) implements virtual controller interfaces. Their existence does not verify an 8K end-to-end path on this setup.

A host driver cannot reconstruct exact physical positions discarded by controller firmware or recover fresh measurements that never crossed the radio. Interpolation can estimate intermediate values, but cannot guarantee what the stick actually did between samples. A legacy game also cannot receive arbitrary extra controls through a fixed API layout it never learned to use: the public XInput gamepad button field has a defined set of buttons and reserves its remaining bits.

Newer designs can retain more information. Microsoft's [GameInput input stream](https://learn.microsoft.com/en-us/gaming/gdk/docs/features/common/input/overviews/input-fundamentals) supports timestamp-ordered readings and access to recent history, but the device must supply suitable data and the application must use the API. Firmware, host software, and game support all contribute to the result.

For this controller, the most valuable next step remains the stock native HID baseline, followed by a verified route that preserves independent extra buttons and delivers fresh higher-resolution sticks. The one-byte gate experiment remains offline and untested on hardware. No further firmware edit was made during this follow-up.
