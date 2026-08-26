# Panda Pro 8K

Panda Pro 8K is an experimental Windows bridge for the GameSir G7 Pro 8K PC/Aimlabs controller. It keeps the controller in its normal XInput/GameSir Connect mode, reads the extra controls and motion data from GameSir's enhanced HID report, and presents the combined state to Steam as a virtual DualSense Edge.

The immediate goal is simple: retain the controller's fast standard-control path while making the two rear paddles, two top buttons, and gyro available to Steam Input.

This is an independent community project. It is not affiliated with or supported by GameSir, Valve, Sony, Nefarius, or the HIDMaestro project.

## Current status

The bridge is usable on the development controller, but it should still be treated as an early test release.

- Normal controls come from XInput.
- L4, R4, M1/L5, M2/R5, gyro, and accelerometer data come from enhanced report `0x12`.
- The virtual controller is a DualSense Edge created through HIDMaestro.
- HidHide keeps the physical GameSir and virtual Edge away from games while Steam remains allowlisted.
- Steam Input can then expose its normal Xbox output to games, so games generally retain Xbox button prompts.
- Physical XInput is read at the full rate supplied by the controller. Virtual output can be switched live between 250, 500, 1,000, 2,000, 4,000, 5,000, and 8,000 Hz, or left uncapped. The selected rate is saved for the next launch.
- Since v0.4.1, virtual output has its own fixed-rate clock. It repeats the latest complete state at the selected target instead of waiting for another changed source packet. The **Virtual submits** card and logs show this actual submission cadence.
- Disconnect/reconnect handling recreates the virtual controller and restarts the enhanced reader without intentionally restarting Steam.

Battlefield 6 is no longer being used as a bridge-stability test. Its phantom firing occurred with Panda stopped, no virtual DualSense present, and the physical controller connected directly to Steam in Logo+A mode. An independent SDL trace held both physical triggers at zero with no button transitions during the failure. Players have reported the same Battlefield behavior with [Steam, PlayStation, Xbox, Nacon, 8BitDo, and GameSir controllers](https://forums.ea.com/idea/battlefield-6-bug-reports-en/steam-controller-randomly-shoots-on-its-own-/13521083), and EA has said those reports were forwarded to the game team. The 4,000 and 5,000 Hz sessions happened to expose the Battlefield bug, but they did not cause it.

Rates above 1,000 Hz remain experimental because the complete matrix still needs to be repeated in unaffected games and on more PCs. That is a validation gap, not evidence that high-rate Panda output caused Battlefield's phantom trigger.

Browser polling pages and stick-rate testers often report fewer events because they count changed values after Steam Input rather than every virtual HID submission. A lower number there does not override Panda's submission counter, and Panda's counter does not claim that the physical stick produced that many distinct positions. Source freshness and output cadence are separate measurements.

The 8,000 Hz preset is intentionally CPU-heavy: the development PC used about one logical CPU core to poll XInput and service the virtual clock. Lower targets and future scheduler work may reduce that cost; testers should record CPU use alongside rate results.

## Requirements

- Windows 11 x64
- GameSir G7 Pro 8K PC/Aimlabs model
- Steam
- [HidHide](https://github.com/nefarius/HidHide/releases)
- [.NET 10 Desktop Runtime, x64](https://dotnet.microsoft.com/download/dotnet/10.0)
- GameSir Connect for controller firmware, polling-rate, and profile configuration

Secure Boot can remain enabled. The old Xbox Extended Feature Support driver is not required for this bridge.

## Installing a release

1. Install HidHide and restart Windows if its installer asks you to.
2. In GameSir Connect, select the controller's normal XInput mode and preferred hardware report rate.
3. Leave L4, R4, M1, and M2 unassigned in the active GameSir profile. The bridge needs their independent report bits.
4. Download the latest ZIP from [Releases](https://github.com/Ab0ut47Pandas/PandaPro8K/releases).
5. Extract it, right-click `Install.ps1`, and choose **Run with PowerShell**.
6. Start the bridge with the **Panda Pro 8K** desktop shortcut.
7. Approve the UAC prompt and leave the control-panel window open while playing.

Use the **Output** menu in the app header to change the virtual submission target. A live change affects Panda immediately. Restart through the desktop shortcut before a formal test so Steam is also launched with the corresponding `controller_rate` interval.

If Steam is already open, the launcher asks before closing and reopening it. Close any running game first. The restart gives Steam the correct device filter from the beginning of its session.

Windows may show a SmartScreen warning because the application executable is not code-signed. The source and release build recipe are included so the package can be inspected or built locally.

## What the bridge does

```text
GameSir XInput ----------- standard buttons, sticks, triggers ---+
                                                                 |
GameSir HID report 0x12 -- four extras, gyro, accelerometer ------+--> Panda Pro 8K
                                                                        |
                                                                        v
                                                               virtual DualSense Edge
                                                                        |
                                                                  Steam Input
                                                                        |
                                                                 virtual Xbox pad
                                                                        |
                                                                       game
```

HidHide performs the device isolation. Panda Pro 8K, GameSir Connect, and Steam are allowlisted; other programs see only the controller Steam exposes. Existing HidHide entries are preserved, and entries added by the bridge are removed on a normal exit.

## Device and report mapping

The current build recognizes GameSir VID `3537` with PID `10C5` (wired) or `10C6` (2.4 GHz dongle) in XInput mode.

The bridge sends the nonpersistent `0F F2` heartbeat to the vendor collection and reads 64-byte report `0x12`:

| Enhanced field | Physical control | Virtual Edge control |
|---|---|---|
| Byte 60, bit `0x08` | Rear-left paddle | Left paddle |
| Byte 60, bit `0x10` | Rear-right paddle | Right paddle |
| Byte 63, bit `0x01` | Top-left extra | Left secondary paddle/Fn input |
| Byte 63, bit `0x02` | Top-right extra | Right secondary paddle/Fn input |

The motion fields and capture evidence are documented in [PROTOCOL.md](PROTOCOL.md) and [TEST_RESULTS.md](TEST_RESULTS.md). Repeatable high-rate testing is laid out in [RATE_TESTING.md](RATE_TESTING.md).

## Building

Clone the repository with its HIDMaestro submodule:

```powershell
git clone --recurse-submodules https://github.com/Ab0ut47Pandas/PandaPro8K.git
cd PandaPro8K
.\scripts\build-release.ps1
```

The build requires Visual Studio 2022 Build Tools with the C++ toolchain, Windows 11 SDK/WDK `10.0.26100`, and the .NET 10 SDK. Output is written to `artifacts`.

The HIDMaestro submodule points to a small project fork. Its added `SubmitRawReport` overload allows a caller to select an explicit HID report ID for descriptors containing more than one input report.

## Repository layout

- `src/hidmaestro/PandaControlPanel` — current Windows control panel and bridge
- `src/bridge/panda_enhanced_stream.cpp` — enhanced GameSir HID reader
- `src/hidmaestro/PandaEdge` and `PandaHidMaestro` — earlier command-line prototypes
- `tools` — capture and timing utilities used during diagnosis
- `src/driver` — abandoned custom KMDF prototype retained as research; the current bridge does not install it
- `PROTOCOL.md`, `TEST_RESULTS.md`, `RESEARCH_LOG.md` — hardware findings and the test chronology

Raw captures and local diagnostic logs are deliberately excluded. They can contain machine-specific USB topology and controller identifiers; the decoded findings needed to reproduce the work are kept in the repository.

## Known limitations

- Only one physical G7 Pro 8K PC/Aimlabs unit has been characterized in depth.
- GameSir may change report behavior in future firmware.
- Motion scale, polarity, and exact accelerometer axis order still need controlled calibration.
- The virtual controller is a DualSense Edge because Steam already understands its extra inputs. Native games that bypass Steam Input may show PlayStation prompts or ignore Edge-only controls.
- Steam's `controller_rate` setting and virtual rates above 1,000 Hz still need controlled testing across several unaffected games and systems. Battlefield 6 is excluded from pass/fail rate testing. See [STEAM_INPUT_RATE.md](STEAM_INPUT_RATE.md).
- There is no signed MSI/MSIX installer yet.

## Credits

- [HIDMaestro](https://github.com/hifihedgehog/HIDMaestro) provides the virtual HID layer.
- [HidHide](https://github.com/nefarius/HidHide) provides device isolation.
- [gamesir-linux-tools](https://github.com/broroeror/gamesir-linux-tools) provided useful configuration-protocol references.
- The unused KMDF prototype began from Microsoft's [virtual HID miniport sample](https://github.com/microsoft/Windows-driver-samples/tree/main/hid/vhidmini2).

## License

Panda Pro 8K is released under the MIT License. The experimental Microsoft-derived driver files under `src/driver` remain covered by the Microsoft Public License. Dependencies and copied sample material retain their original notices; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
