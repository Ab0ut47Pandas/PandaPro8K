# The firmware changes

The tested hardware is the GameSir G7 Pro 8K PC/Aimlabs controller and its receiver. These patches are tied to the exact stock packages below. Similar product names aren't enough to establish compatibility.

## Controller 2.2.9

Target: `GS_G7_Pro_CE_DV`.

The final controller image includes two changes:

1. At application offset `0xE4E81`, change `14` to `04`. This changes the comparison at runtime `0x00108F0E` from a minimum counter value of 10 to 2. The counter increases by 2 on the identified nominal 2 ms callback. This lets the legacy wireless state path update at about 500 Hz instead of about 100 Hz.
2. At application offset `0x265AE`, runtime `0x060266AE`, replace `01 f8 f1 00` with `00 00 00 00`. Those four bytes become two NOPs. This removes the separate native report producer's 1 ms wait while keeping its counter read/reset and the rest of the producer intact.

The configured report rate was 4,000 Hz. Removing the wait lets the existing producer run at that configured cadence. We tested 4 kHz; this release doesn't claim an 8 kHz native result.

Relative to the decoded stock application, four byte values differ across those two instruction locations. Rebuilding both flash payloads and their checksums changes 32 bytes in the full UFW package. The package length stays 2,178,368 bytes.

| Image | SHA-256 |
| --- | --- |
| Stock `GS_G7_Pro_CE_DV_V229_Key.ufw` | `4d2982df76d92f0ef7564a98005c8b01c25e0c9d1f995becbe1afe72e55882c4` |
| Final controller package | `201e5418b434a8eb21b5b8802c757ee26a6c66c912e03f864f42471cdc0550f7` |
| Stock decoded application | `fa554844a381e8f43a63aa79c8b86de412893c93c8309c533658c30c6633502d` |
| Final decoded application | `5ca952c664a069b4226fe4b5ad02d2d53990f418c039f75b2541c6dcd9f13ea0` |
| Earlier gate-2-only package | `faee94bd17317f9b26afb1d0d33c8bc2f7980ab50e9a23184bd4c124678f2cd8` |

`patches/controller.json` produces the final image. `patches/controller-gate2.json` preserves the earlier experiment for review; it isn't needed in addition to the final controller patch.

## Receiver 1.3.5

Target: `GS_G7_Pro_CE_DG`.

There are two receiver changes:

1. At runtime `0x0600418C`, application offset `0x408C`, change the instruction from `r0 = 1` to `r0 = 0`. The differing byte is at `0x408D`. This removes the forced minimum divisor of 8 in the non-mode-C rate-helper branch when the extended native flag at context `+0x518` is active. The separate mode-C and global-mode clamps remain. The existing configured divisor of 2 corresponds to the tested 4 kHz setting.
2. Replace the 40-byte axis block at runtime `0x06004DDA`. Keep `r1 = context + 0x21F`, which the following trigger code needs. Read the four fast 16-bit axes from context `+0x50`, then write the same big-endian axis bytes into the existing native report.

The high-speed whitelist is already `0x0805`, which includes native mode 11. We didn't change it. Removing the native rate clamp lets the existing speed-selection/re-enumeration code select high speed. Live Windows USB hub queries confirmed that it did, before and after a cold receiver reconnection.

The output pointer in `r5` needs care. An early linear scan suggested it was clobbered at `0x06004C6A`. That was wrong: the instruction belongs to the separate PS4 builder and exits on a different path. We decoded the native hat jump table and traced the reachable instructions. Every native path reaches the axis block with `r5 = context + 0x528`. Its redundant reload can be replaced with the fast-buffer pointer.

The replacement axes are in [receiver-native-rate.s](../analysis/receiver-native-rate.s). The vendor assembler produced the same-length block. An interpreter ran its disassembly against 625 extreme-value combinations and 1,000 deterministic random vectors. Those checks covered byte order, non-axis report bytes, the trigger pointer and preserved registers. This is an offline check in addition to the physical recordings.

There are 21 differing application bytes across the two blocks. Rebuilding the duplicated flash payloads and checksums changes 66 bytes in the full 1,851,328-byte receiver package. There is no code cave or change in package length.

| Image | SHA-256 |
| --- | --- |
| Stock `GS_G7_Pro_CE_DG_V135_Key.ufw` | `439e185b5e4dc3cf62e359aca82775a878bae9af7a05ce0c904588561c6d262a` |
| Final receiver package | `26e7e593fcfabc3c86f98e5888c61a3e6cac38e6d477c5b70b9abb9b61e55767` |
| Stock decoded application | `bf2b2b0965d6bfe11a4059be81a0acea1436dc662b0fd2f580bbf10634a66cca` |
| Final decoded application | `8242f0737f45c50755f243d62eff008311ece6a9be0b863aab63d2cea60a9449` |

## What the patcher verifies

The XOR manifests describe every changed byte in the complete packages, including the checksums. The patcher checks the input's full SHA-256 and length, validates span bounds/order, applies the delta in memory and verifies the full result hash before creating a new file. The same delta reverses the exact modified image back into its stock source.

The modified packages were independently decoded again and their nested CRCs checked. The unchanged stock packages also round-tripped byte for byte. GameSir's installed vendor file validator accepted the target/version and CRCs before the hardware updates.

That file acceptance isn't a guarantee for other units. A matching file hash also isn't a readback of running flash. We didn't dump the entire controller or receiver flash, pairing NVM or bootloader.

## Identifiers and model protection

The reported versions remain controller 2.2.9 and receiver 1.3.5. USB identities remain `3537:10C5` for wired XInput, `3537:10C6` for receiver XInput and `3537:10B8` for native HID.

We didn't disable target/model checks, change the USB PID, add a signing bypass or edit the installed GameSir application. `_No_Key` package names don't establish that arbitrary unsigned images are accepted. The published patches require the exact `_Key` sources above.

The base packages came from GameSir Connect 1.16.7. These are not claimed to be GameSir's separate tournament firmware. Reported version equality alone cannot prove two different package variants contain the same code.
