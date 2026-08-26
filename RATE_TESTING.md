# Output-rate test plan

The rate selector is intended to turn the earlier one-off builds into a repeatable test matrix. A result is useful only when the physical controller setting, Panda target, Steam interval, game, and connection type are recorded together.

## Before each run

1. Close the game.
2. Set the GameSir hardware rate in GameSir Connect and leave all four extra controls unassigned.
3. Select the Panda output target.
4. Close Panda and restart it through the desktop shortcut. This restarts Steam with the matching `controller_rate` interval.
5. Open Panda's logs folder and note the new log filename.
6. Launch the game through Steam.

Do not switch rates during the actual run. Live switching is available for diagnosis, but it leaves Steam at the interval selected when its current session began.

## Matrix

| Panda target | Steam interval | Battlefield | Second Steam Input game | Reconnect test |
|---:|---:|---|---|---|
| 250 Hz | 4,000 µs | Open | Open | Open |
| 500 Hz | 2,000 µs | Open | Open | Open |
| 1,000 Hz | 1,000 µs | Previously stable; repeat matched | Open | Open |
| 2,000 Hz | 500 µs | Open | Open | Open |
| 4,000 Hz | 250 µs | Phantom symptom seen; also reproduced without Panda | Open | Open |
| 5,000 Hz | 200 µs | Phantom symptom seen; also reproduced without Panda | Open | Open |
| 8,000 Hz | 125 µs | Open | Open | Open |
| Uncapped | 0 | Open | Open | Open |

## Minimum run

- Five minutes of normal movement and aiming.
- Repeated full and partial analog trigger pulls.
- Both trigger-stop positions.
- All four extra controls.
- One controller power cycle or cable reconnect while Steam remains open.
- At least one respawn or level transition, since the original phantom trigger often appeared when entering gameplay.

Stop the run immediately if a trigger or another control sticks. Alt-tab without touching the controller, copy Panda diagnostics, and preserve the newest log. Note the wall-clock time as closely as possible so the event can be lined up with the five-second rate entries.

## Result template

```text
Controller firmware:
Connection: wired / 2.4 GHz
GameSir hardware rate:
Panda version:
Panda output target:
Observed virtual-submit rate while moving:
Steam version/channel:
Game and version:
Average game frame rate:
Duration:
Reconnect performed: yes/no
Result: pass/fail
Failure time and symptom:
Log filename:
```

Stick-change and trigger-change counters are movement-dependent. They are useful for comparing the source data during the same motion, but they are not substitutes for the **Virtual submits** rate or a raw USB endpoint measurement.
