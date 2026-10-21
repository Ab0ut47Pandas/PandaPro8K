> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# Steam Input and HID limits

Checked October 20, 2026. This pass read local binaries, existing captures/reports, Steam logs, public source code, and documentation. No Steam settings, controller settings, or firmware were changed.

## A Steam-side polling setting exists

The installed `<Steam>/steamclient64.dll` contains this help string at file offset `0x12D2010`:

> SteamInput Active Polling interval in microseconds. Default 2000us = 500hz

The adjacent setting name is `controller_rate` at offset `0x12D2060`. The client also describes an idle polling default of 50,000 microseconds, or 20 Hz.

This confirms how this installed binary describes the setting and its default. It does not establish the current runtime value, effective rate for every device/backend, output rate to a game, permitted adjustment range, or successful 8,000 Hz processing. None of those was measured in this pass.

Binary size: 26,544,280 bytes. Full hash and selected strings are recorded in the working evidence file `work/steam_input_limits/steam_polling_strings.json`.

## No verified universal 100 Hz / 8-bit DInput rule

Valve's public documentation supports DirectInput gamepads but does not establish a universal 100 Hz or 8-bit ceiling. The label DInput should not be taken as proof of the exact host API/backend Steam uses. Local earlier Steam logs identified the 8BitDo's DInput mode as a native HIDAPI path. The current public [SDL GameSir driver](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_gamesir.c) similarly uses direct HID reads; that source is not proof of the precise version or behavior embedded in the user's Steam client.

The relevant layers must be measured separately: physical reports and fresh samples, Steam's processing of those samples, and the game-facing output. A device's transport rate alone does not prove its effective rate through Steam.

## Relevant actual limits

- USB full-speed interrupt endpoints have a minimum polling period of 1 ms; high-speed interrupt endpoints can use 125 microseconds. Those correspond to 1,000 and 8,000 service intervals per second. The active device speed and endpoint configuration matter. See [Microsoft's endpoint descriptor documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbspec/ns-usbspec-_usb_endpoint_descriptor).
- Xbox-style gamepad output has signed 16-bit stick axes, and Steam's native analog-action API uses floating-point values. These formats do not impose an 8-bit stick ceiling. They do not themselves guarantee that a particular input backend preserves all available precision. See [XINPUT_GAMEPAD](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_gamepad), [Steam gamepad emulation](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs), and [ISteamInput](https://partner.steamgames.com/doc/api/ISteamInput).
- The GameSir's archived native HID capture contained repeated stick values in the physical input reports. That specific observation occurs before Steam's gamepad emulation and cannot be explained solely by an emulated-output limit. Its firmware version was not established; repeat it before applying the historical result to current firmware.

## Next useful measurement

Compare fresh stick updates in native HID reports against the game-facing output through Steam, under the same movement and device mode. Record Steam's runtime polling setting. Do this on current firmware, wired and through the receiver, before choosing a firmware patch or a Steam-side adjustment.
