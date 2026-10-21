> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# PandaPro8K proposal review

Reviewed October 21, 2026 against the local source under `<PandaPro8K-checkout>`, its historical test records, and current primary documentation. This was a static review: no controller commands, software launches, device isolation changes, Steam changes, or source edits were performed.

The architecture is credible and the existing bridge supplies much of the foundation. The two unresolved performance constraints are the virtual output format's stick precision and the extra-control stream's source cadence. The most consequential product milestone is proving that Steam accepts a suitable virtual format with four independently bindable extra inputs.

## A concrete precision limit in the existing bridge

- `src/hidmaestro/PandaControlPanel/BridgeEngine.cs:129` selects the `dualsense-edge` profile; line 132 arms its extended report.
- Lines 570–573 normalize the four XInput stick coordinates into floating-point values.
- `vendor/HIDMaestro/profiles/sony/dualsense-edge.json` declares bytes 1–4 as `uint8-axis` for the four stick coordinates, each centered at 128. Its standard descriptor also declares eight-bit analog fields.
- `vendor/HIDMaestro/sdk/HIDMaestro.Core/HMController.cs` selects the armed extended report and calls `VendorBlobCodec.EncodeInput`.
- `vendor/HIDMaestro/sdk/HIDMaestro.Core/Internal/VendorBlobCodec.cs:92` computes `round(clamp(v, 0, 1) * 255)` and writes one byte at line 95.

The current source therefore quantizes each virtual Edge stick axis to at most 256 position codes. Carrying the physical XInput values in floats before serialization does not preserve all of their original position information through this virtual report. This is a static code finding, not a fresh measurement of the installed executable or Steam output.

An alternative virtual format must preserve higher-resolution axes **and** be interpreted correctly by Steam. Simply enlarging Sony's stick fields would change that protocol's report layout; it is not a compatible fix for an existing Sony parser.

## An independent source-rate constraint

The repository's `PROTOCOL.md`, `TEST_RESULTS.md`, and `STEAM_INPUT_RATE.md` record the XInput-mode enhanced report `0x12`, activated by the nonpersistent heartbeat. Historical receiver tests observed approximately 217–222 reports per second, carrying the extras and motion data.

A virtual 8,000 Hz output clock can repeat the latest extras at that rate, but it does not create 8,000 independently refreshed extra-button observations. Report cadence alone also does not establish physical button latency or prove a permanent firmware maximum. Repeat the test on current firmware before claiming an active hard cap.

The proposal's specification should list standard-control freshness, extra-button freshness, IMU freshness, virtual submissions, and Steam/game-facing observations separately. Its existing source/output cadence distinction is a useful foundation.

## Steam recognition needs an early proof

Valve documents support for generic DirectInput gamepads, and SDL supports custom mappings and paddle semantics. That establishes available mechanisms, not a guarantee that this installed Steam client will expose every input from a new Panda identity. Updating mappings in our own SDL process also does not automatically update Steam's internal input handling.

Relevant primary documentation:

- [Valve's supported input devices](https://partner.steamgames.com/doc/features/steam_controller/device)
- [SDL custom gamepad mappings](https://wiki.libsdl.org/SDL3/SDL_AddGamepadMapping)
- [SDL paddle/button semantics](https://wiki.libsdl.org/SDL3/SDL_GamepadButton)

The proposed statement that this requires validation is correct. Treat the proof as an early dependency, before building a polished custom-controller UI.

## Other changes to scope and wording

**GameSir Connect:** start with launch/access integration. Embedding its window adds focus, scaling, modal-dialog, and update-lifecycle work. Configuration or firmware-update operations should temporarily release the bridge's device access when necessary and then rediscover the controller. An allowlist alone does not establish safe concurrent access.

**Isolation:** preserve the existing device filtering design rather than reducing it to one HidHide checkbox. HidHide's application allowlist gives a process visibility of hidden devices; allowing Steam to access a hidden virtual controller can also make the hidden physical device visible to Steam. The local implementation combines isolation with Steam-side filtering. Verify that Steam and games each see the intended devices, including reconnect and normal-exit recovery. See [HidHide's setup documentation](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/).

**Latency:** the current `OnSubmitLatencyMicros` callback times work inside `HMController.SubmitState`; it does not time physical actuation through the screen. Distinguish report timing, bridge submission duration, source-to-output delay where measurable, and a separate physical latency experiment. Avoid promising complete input latency measurement from report-arrival timestamps alone.

**Performance wording:** “preserve available precision and minimize added delay” is a defensible objective. “Without compromise” remains a target, especially while the current virtual format reduces stick precision and the extra-control stream runs at a different source rate. The README also records substantial CPU cost at its 8K setting; performance validation should include CPU use and game stability.

**Additional controllers:** adapter boundaries are a sound design. Support still depends on obtaining each device's independent controls in the chosen mode. The GameSir heartbeat/report protocol is not a generic way to recover every manufacturer's hidden paddles. Keep the first supported-device scope explicit.

## Suggested first acceptance milestone

1. Produce one virtual device with the intended identity and genuinely higher-resolution stick fields.
2. Show four distinct, independently configurable extra inputs in Steam Input and verify different bindings in an ordinary test game.
3. Feed a known fine-grained axis sequence through the virtual device and Steam with controlled linear settings; check for added quantization instead of relying on the advertised descriptor width.
4. Measure physical source freshness, extra-control arrivals, virtual submissions, and game-facing behavior separately. Establish a stable 1,000 Hz target before assessing the benefit and cost of higher rates.
5. Verify device isolation, reconnect behavior, and cleanup without changing unrelated devices or leaving duplicate game inputs.

This milestone can validate the key technical proposition before embedded configuration software or multi-manufacturer support expands the project.
