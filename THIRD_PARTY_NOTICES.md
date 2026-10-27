# Third-party notices

## HIDMaestro

The current bridge uses HIDMaestro as a Git submodule. HIDMaestro is licensed under the MIT License. Release ZIPs include its complete copyright and license text in `HIDMaestro.LICENSE.txt`, beside `HIDMaestro.Core.dll`. The source copy is `vendor/HIDMaestro/LICENSE`.

Upstream project: <https://github.com/hifihedgehog/HIDMaestro>

The submodule points to a fork containing an explicit-report-ID submission overload and a Visual Studio Build Tools discovery fix. The rest of the HIDMaestro history and attribution remain intact.

## HidHide

HidHide is an external prerequisite and is not redistributed in this repository.

Project: <https://github.com/nefarius/HidHide>

## Microsoft virtual HID sample

The unused prototype under `src/driver` was derived from Microsoft's `vhidmini2` sample and retains the Microsoft copyright headers. Those files are covered by the Microsoft Public License rather than this repository's MIT License.

Original sample: <https://github.com/microsoft/Windows-driver-samples/tree/main/hid/vhidmini2>

License: <https://opensource.org/license/ms-pl-html>

## gamesir-linux-tools

No source from gamesir-linux-tools is bundled. Its published protocol work was used as a reference during the hardware investigation.

Project: <https://github.com/broroeror/gamesir-linux-tools>
