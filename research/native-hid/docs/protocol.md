# The native input path

The physical native mode used in these tests is Home/Logo + A. On this hardware it enumerates as `3537:10B8`, product `GameSir-G7 Pro 8K`.

The useful input collection has usage page `0xFFF0`, usage `0x0040`, and 37-byte input reports. Other collections on the same device aren't interchangeable with it. A browser failing to show stick input doesn't mean this collection is silent.

The packet layout follows the [SDL GameSir HID driver](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_gamesir.c). With the Windows report ID included:

| Bytes | Meaning used here |
| --- | --- |
| 0–2 | `43 A1 C8` header |
| 3–6 | Four button/hat bytes |
| 7–14 | LX, LY, RX, RY as signed 16-bit, big-endian values |
| 15–16 | Left/right triggers, 0–255 |
| Remaining bytes | Auxiliary/motion fields; not used to infer fresh stick rate |

Offsets are zero-based. The decoder also accepts the `A1 C8` form where the leading Windows report ID is absent. Native Y is positive up; a screen-coordinate plot needs to reverse the sign for display.

For the four button bytes after `A1 C8`, L4 is byte 2 bit `0x40`, R4 is byte 2 bit `0x80`, L5 is byte 3 bit `0x01`, and R5 is byte 3 bit `0x02`. L5/R5 are the additional shoulder buttons, sometimes called M1/M2 elsewhere.

## Why wireless native was slower

The stock receiver has two stick sources:

```text
Fast wireless stick samples -> receiver context +0x50 -> XInput axes

Legacy 13-byte state block -> receiver context +0x21C -> native axes/buttons/triggers
```

The controller's legacy-state producer had a minimum counter gate of 10. The first patch lowered it to 2. That improved fresh wireless native axes from about 100 Hz to about 500 Hz, but it still used the slower state path.

The receiver's fast queue already reconstructs four 16-bit axes from eight-byte samples. The final receiver patch uses those axes for native output too, while leaving the other native fields on the existing state path. The physical post-flash recordings establish that the fast source is active in this tested native mode.

That is why changing only the USB report interval wouldn't have been enough: the old native report builder could keep sending an unchanged legacy snapshot.

## Stream activation

SDL's driver sends `A2 01 00` when opening native extended input. The live output report is 37 bytes including report ID A2, with unused bytes padded to zero. The wired handler was traced to a RAM flag enabling streaming; this command is not a firmware update or profile write.

The published recorder and tester only read. They don't send this activation command automatically. On the tested system, Steam/SDL had already enabled the native stream. If the device is present but no usable reports arrive, that distinction matters. Don't try unrelated vendor commands to make a read-only test work.

## The separate bridge path

PandaPro8K's existing bridge uses normal XInput identities `3537:10C5` / `3537:10C6` and enhanced report `0x12` after a nonpersistent `0F F2` heartbeat. That is a different protocol from native `43 A1 C8` input. Its button offsets and initialization shouldn't be copied into the native decoder.

This firmware work doesn't change the bridge's virtual controller or give generic XInput arbitrary extra buttons. It improves the physical native stream so a compatible native reader can receive the faster axes and independent controls together.
