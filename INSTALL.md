# Installing Panda Pro 8K

These instructions are for the GameSir G7 Pro 8K PC/Aimlabs controller on Windows 11 x64. The current build recognizes `3537:10C5` over USB and `3537:10C6` through the 2.4 GHz dongle.

## 1. Install the prerequisites

Install the following software before installing Panda Pro 8K:

- [HidHide](https://github.com/nefarius/HidHide/releases). Restart Windows if its installer asks you to. Panda configures its own HidHide entries; you do not need to hide devices manually.
- [.NET 10 Desktop Runtime for Windows x64](https://dotnet.microsoft.com/download/dotnet/10.0).
- [GameSir Connect](https://www.gamesir.hk/pages/gamesir-connect-software).
- [Steam](https://store.steampowered.com/about/).

Secure Boot can remain enabled. The Xbox Extended Feature Support Driver is not required.

## 2. Configure the controller

1. Turn on the controller in its normal XInput/GameSir Connect mode. Do not use Logo+A mode with the bridge.
2. Open GameSir Connect and choose the hardware report rate you want.
3. In the active GameSir profile, leave L4, R4, M1, and M2 completely unassigned.
4. Save or apply the profile.

The bridge reads those four controls from GameSir's enhanced HID report. Assigning them in GameSir Connect can replace their independent bits with cloned standard buttons.

## 3. Install the release

1. Download the newest `PandaPro8K-v*-win-x64.zip` from [GitHub Releases](https://github.com/Ab0ut47Pandas/PandaPro8K/releases).
2. Open the ZIP's Properties. If Windows shows an **Unblock** checkbox, select it and choose **Apply**.
3. Extract the complete ZIP. Do not run files from inside the archive.
4. In the extracted folder, right-click `Install.ps1` and choose **Run with PowerShell**.
5. Wait for the confirmation that Panda Pro 8K was installed.

The installer copies the package to `%LOCALAPPDATA%\Programs\Panda Pro 8K` and creates a **Panda Pro 8K** desktop shortcut for the current Windows account.

The executable is not commercially code-signed, so Windows may display a SmartScreen warning. The complete source and release build scripts are available in this repository.

## 4. First launch

1. Close any running game.
2. Make sure the GameSir controller is on.
3. Double-click the **Panda Pro 8K** desktop shortcut.
4. If Steam is open, choose **Yes** when the launcher asks to restart it.
5. Approve the Windows UAC prompt.
6. Leave the Panda Pro 8K window open while playing.

Do not start `PandaPro8K.exe` directly. The shortcut launches Steam with the GameSir device filter and controller interval needed for the current Panda settings.

The first launch also installs or checks the HIDMaestro virtual HID component. A normal installation does not require disabling Secure Boot or enabling Windows test mode.

## 5. Verify the bridge

The Overview tab should report all four paths as connected:

- Physical XInput
- Enhanced HID `0x12`
- Input isolation
- Virtual DualSense Edge

Move both sticks and triggers, then press the standard and extra buttons. The live controls should respond, including L4, R4, M1/L5, and M2/R5.

The header defaults to **Face: GameSir labels**, which corrects the observed A/B and X/Y XInput pairs so the monitor and Steam follow the letters printed on the controller. If a different firmware or GameSir profile already reports conventional XInput face bits, select **Face: Raw XInput** instead.

Steam should receive the combined virtual DualSense Edge instead of opening the physical GameSir as a second controller. Games using Steam Input normally receive Steam's Xbox output and keep Xbox prompts. Open a game's Steam controller layout to assign the four Edge inputs and gyro.

## 6. Output rate

Use the **Output** menu in Panda's header to select 250, 500, 1,000, 2,000, 4,000, 5,000, or 8,000 Hz, or uncapped diagnostic mode.

Changing the menu updates Panda immediately. For a formal rate test, close the game and restart Panda through its desktop shortcut afterward so Steam also starts with the matching `controller_rate` interval.

Rates above 1,000 Hz are experimental. **Virtual submits** measures complete state submissions from Panda into HIDMaestro; it is not proof that every submission reached a game as a separate USB report. See [Steam Input rate notes](https://github.com/Ab0ut47Pandas/PandaPro8K/blob/main/STEAM_INPUT_RATE.md) and [rate testing](https://github.com/Ab0ut47Pandas/PandaPro8K/blob/main/RATE_TESTING.md).

## Everyday use

After the first installation:

1. Turn on the controller.
2. Double-click the Panda Pro 8K desktop shortcut.
3. Allow the Steam restart and UAC prompt.
4. Leave Panda running while playing.

The optional **Start with Windows** checkbox creates an elevated task for the current user so Panda starts before an ordinary Steam session.

## Stopping the bridge

Choose **Stop bridge** or close the Panda window. Restart Steam normally if you want to use the physical GameSir without Panda's controller filter.

## Getting diagnostic information

Choose **Copy diagnostics** in Panda or **Open logs** and attach only the relevant log to a GitHub issue. Review diagnostic files before sharing them. The separate research tools and their privacy behavior are documented in the [diagnostic tools guide](https://github.com/Ab0ut47Pandas/PandaPro8K/blob/main/tools/README.md).
