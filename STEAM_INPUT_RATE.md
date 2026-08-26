# Steam Input rate notes

There are three different rates in this project, and treating them as one number led to misleading results early on:

1. The physical controller's standard XInput state rate.
2. The rate at which Panda Pro 8K submits complete reports to the virtual DualSense Edge.
3. The rate at which Steam Input processes the virtual controller and produces its game-facing output.

The GameSir controller remains configured for its advertised high-rate mode. Panda reads XInput continuously and keeps the latest state. The public bridge defaults to 1,000 Hz virtual output but exposes 250, 500, 1,000, 2,000, 4,000, 5,000, and 8,000 Hz presets plus an uncapped mode. The enhanced HID side channel is slower, about 217–222 fresh reports per second, so paddles and motion data update at that source rate.

## What was tested

- A 1,000 Hz virtual-output target remained stable during the original Battlefield test.
- A 5,000 Hz session produced intermittent phantom right-trigger behavior.
- A 4,000 Hz session reproduced the same symptom.
- During the failures, simultaneous traces showed the physical GameSir right trigger at zero, the virtual Edge analog R2 value at zero, and the virtual Edge digital R2 bit clear.
- The same native virtual Edge path was stable when Steam Input was bypassed, although the game then ignored Edge-specific inputs.
- The phantom firing was subsequently reproduced with Panda stopped, no virtual Edge present, and the physical GameSir connected directly to Steam in Logo+A mode.
- During the direct Logo+A reproduction, Battlefield was foreground while an independent SDL monitor reported both trigger axes at exactly zero, no buttons held, and no trigger or button transitions. A simultaneous raw-input trace contained no GameSir keyboard or mouse events.

The evidence clears Panda's decoder, virtual report construction, and high virtual submission rate as necessary causes. It points downstream of the neutral hardware state, but does not yet distinguish Steam Input's generic-HID trigger translation from Battlefield's handling of Steam's translated controller state.

## `controller_rate`

Steam exposes a `controller_rate` console variable expressed in microseconds. A commonly reported default is `2000`, or a 500 Hz active polling interval. The original high-rate tests launched Steam normally and did not set this value.

That leaves an important controlled test unfinished: match Steam's processing interval to the bridge output before increasing either rate.

| Virtual output target | Steam argument to test |
|---:|---:|
| 1,000 Hz | `+controller_rate 1000` |
| 2,000 Hz | `+controller_rate 500` |
| 4,000 Hz | `+controller_rate 250` |
| 5,000 Hz | `+controller_rate 200` |
| 8,000 Hz | `+controller_rate 125` |

`+controller_rate 0` removes the interval limit, but it also introduces an unbounded variable. It should come after the fixed-rate tests, not before them.

The release launcher reads Panda's saved output target and starts Steam with the corresponding `+controller_rate` value. Changing the rate while the bridge is already running updates Panda immediately, but Steam keeps the interval from the beginning of its current session. Restart through the Panda desktop shortcut before recording a matched-rate test.

Every preset is a target rather than a promise that the complete path will achieve that number. The app's **Virtual submits** card and copied diagnostics record what Panda actually delivered. Testers should also record the game, connection type, GameSir firmware and hardware setting, Steam version, frame rate, and whether the failure survives in a second game.

Until the matched-rate tests are repeated across more than one game and PC, 1,000 Hz remains the conservative default.
