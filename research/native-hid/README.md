# GameSir G7 Pro 8K: fixing the native HID stick rate

I wanted the high-resolution sticks and all four extra buttons in the same mode. Native HID gave me the buttons, but the sticks were updating much slower than they did in XInput. So we traced the firmware and changed that path.

On my G7 Pro 8K PC/Aimlabs controller, native HID now produces about 4,000 fresh stick updates a second over USB and the wireless receiver. The four extra buttons still work independently. Normal XInput still works too.

These results are from one controller and one receiver, tested on October 21, 2026. I'd like other people to review the changes. I don't have enough testing to call this safe for everyone's hardware.

## What we measured

| Connection and firmware state | Fresh stick updates during movement | Median interval |
| --- | ---: | ---: |
| Wireless, stock controller 2.2.9 and receiver 1.3.5 | About 100/sec | About 10 ms |
| Wireless, first controller timing patch | About 500/sec | About 2 ms |
| Wired native HID, before the wired patch | About 1,000/sec | About 1 ms |
| Wired native HID, patched controller | About 4,000/sec | 0.2491 ms |
| Wireless native HID, patched controller and receiver, after reconnecting | About 3,979/sec | 0.2498 ms |

Those are changing stick values in physical reports received by Windows. Repeated reports aren't counted as fresh stick positions. The 0.25 ms number is an update interval, not measured controller-to-screen latency.

After the wireless patch, we turned the controller off, unplugged and reinserted the receiver, and repeated the movement test. It kept the higher rate. All four axes reached their full signed range, and the extra buttons still registered separately. A/B and full trigger travel were captured in the first post-flash native test. Normal wired and wireless XInput checks passed. GameSir configuration reads still work.

## What changed

The controller patch keeps an earlier wireless timing change and removes a separate 1 ms wait from the wired native report producer.

The receiver patch removes a rate clamp in native mode and takes the stick axes from the fast buffer already used by XInput. It keeps the native report format and the existing trigger and extra-button fields.

The thing to note here is that the **axes** now use the fast path. We haven't made the extra buttons, triggers or IMU sample at 4 kHz.

The reported versions are still controller **2.2.9** and receiver **1.3.5**. Native USB remains **3537:10B8**. We didn't change model checks or unlock cross-model flashing. You need the file hashes to distinguish these images from stock.

The final controller package differs from stock by 32 bytes and the receiver package by 66 bytes, including checksum changes and the duplicated flash payloads. [The exact edits and hashes are here.](docs/patches.md)

## Files you can use

- [Results and how we counted them](docs/results.md)
- [Firmware changes, offsets and hashes](docs/patches.md)
- [Native packet layout and the two wireless paths](docs/protocol.md)
- [Restoring stock and what recovery actually does](docs/recovery.md)
- [8BitDo, Steam limits, polling and resolution](docs/other-findings.md)
- [Tools and commands](tools/README.md)
- [Earlier research notes](notes/README.md)
- [Capture index](evidence/capture-index.json)

The patches are small XOR deltas, not complete GameSir firmware files. You supply the exact stock packages from GameSir Connect. The patcher checks the whole input file, applies the delta to a copy, then checks that the result matches the exact image we tested. It refuses other versions and existing output files.

From this directory, with Python 3.10 or newer:

```powershell
py -3 tools/patch_firmware.py list
py -3 tools/patch_firmware.py identify "C:\path\to\GS_G7_Pro_CE_DV_V229_Key.ufw"
py -3 tools/patch_firmware.py apply controller "C:\path\to\GS_G7_Pro_CE_DV_V229_Key.ufw" --output controller-native-4k.ufw
py -3 tools/patch_firmware.py apply receiver "C:\path\to\GS_G7_Pro_CE_DG_V135_Key.ufw" --output receiver-native-4k.ufw
```

**These commands create files. They do not flash anything.** Read [recovery.md](docs/recovery.md) before considering a hardware update. A bad flash can leave the device unusable. We preserved stock packages and entered recovery successfully, but have not tested flashing stock back after these modifications. There is no tested automatic rollback.

The release includes the tools and a separate ZIP of sanitized input recordings. Controller serials, Windows device paths and USB topology were removed. The timestamps and input payloads in those recordings are unchanged.

## What still needs testing

Battery life, long sessions, interference, sleep/wake, rumble, IMU behavior and other hardware revisions need more work. The GameSir Connect version/profile reads passed; that doesn't verify every GUI operation, calibration or future updater.

We also haven't measured Steam's output rate or end-to-end game latency. I tested it in a game and the controls behaved correctly. That isn't a latency benchmark.

The captures contain tens of thousands of distinct axis output codes. That doesn't prove a 16-bit sensor or that every code adds useful precision. The existing extended report already has 16-bit axis fields; we're preserving that output and delivering fresh values faster.

For now, this is firmware research with a working result on my hardware. Review it before flashing it, and don't use these packages on a regular G7 Pro, another edition with a different firmware target, or a different controller.

The code written for this project is covered by the repository's MIT license. GameSir firmware and vendor tools aren't included. [Sources and attribution](NOTICE.md).
