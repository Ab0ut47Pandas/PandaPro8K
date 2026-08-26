PANDA PRO 8K BRIDGE
===================

REQUIREMENTS

- Windows 11 x64
- HidHide
- .NET 10 Desktop Runtime (x64)
- Steam and GameSir Connect

FIRST INSTALL

1. Extract the complete release ZIP.
2. Right-click Install.ps1 and choose "Run with PowerShell".
3. Use the new Panda Pro 8K shortcut on the Desktop.

BEFORE STARTING

1. Put the GameSir G7 Pro 8K in its normal XInput/GameSir Connect mode.
2. Select the hardware report rate you want in GameSir Connect.
3. Leave L4, R4, M1, and M2 unassigned in the active GameSir profile.
4. Close any running game. Steam itself may remain open; the launcher asks
   before restarting it with the required controller filter.

TO START

1. Double-click the Panda Pro 8K desktop shortcut.
2. If Steam is open, choose Yes when asked to restart it.
3. Approve the Windows UAC prompt.
4. Leave the Panda Pro 8K window open while playing.

The Output menu in the app header changes the virtual report target. Rates
above 1,000 Hz are experimental. Restart through this shortcut after changing
the rate so Steam is launched with the matching controller_rate interval.

Do not open PandaPro8K.exe directly. The launcher prepares Steam so it sees
the combined virtual controller instead of both the virtual and physical pads.

TO STOP

Click "Stop bridge" or close the control-panel window. Restart Steam normally
if you want to use the physical GameSir without the bridge.

NOTES

- Secure Boot can remain enabled.
- HidHide isolates the physical GameSir and virtual DualSense Edge from games
  while keeping them available to the bridge and Steam. Other controller
  models are not hidden.
- Games using Steam Input normally receive Steam's Xbox output and keep Xbox
  prompts.
- The Xbox Extended Feature Support Driver is not required.
- A normal controller power cycle should release the old state, restart the
  enhanced reader, reacquire XInput, and recreate the virtual Edge without a
  Steam restart.
- "Start with Windows" creates an elevated per-user logon task. Existing SDL
  ignore-list entries are preserved.
- Logs are written to the logs folder beside the executable.
- The selected output target is stored under the current user's local app data.
- The application executable is not commercially code-signed, so Windows may
  show a SmartScreen warning for a downloaded release.
