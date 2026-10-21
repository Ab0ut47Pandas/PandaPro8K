> Earlier research notes. Some status statements predate later tests. See [current results](../docs/results.md).

# GameSir G7 Pro 8K firmware experiment, 2026-10-21

The experiment increased fresh native HID stick updates from approximately 100 Hz to 472–476 Hz over the wireless receiver. Median changed-state interval fell from 10.0007 ms to 2.0000 ms. The four independent extra-button bits remained present. This is a physical HID capture result, not a Steam/game latency measurement or an 8 kHz HID result.

The experimental package was written using GameSir Connect's installed vendor updater library. The library returned success after approximately 33.5 seconds, and the controller reappeared in normal wired XInput mode. The post-flash timing change provides runtime evidence consistent with the intended patch; a full firmware readback was not performed.

The user requested preparation and testing of the existing firmware change. No receiver firmware was changed, no installed GameSir package was overwritten, and the update was attempted once with no forced identity override or key fallback.

## Fresh stock baseline

Controller firmware 2.2.9, receiver 1.3.5; receiver connection, native HID mode. The 90-second recording contains 89,527 recognized 37-byte reports, approximately 994.7 reports/second. While the sticks were moving, distinct stick states arrived at approximately 100 Hz with a median interval of 10.0007 ms. The four axes covered the signed 16-bit range and produced 3,356–3,422 distinct observed values each. Observed unique counts are movement dependent and do not establish effective sensor resolution.

Evidence: `stock-receiver-baseline.summary.json`. The complete raw capture is in the workspace's `work/gamesir_firmware_inspection/preflight/` directory.

## Change being tested

Application byte offset `0xE4E81`: `0x14` to `0x04`, changing the minimum changed-state gate from counter value 10 to 2. The identified callback increments by 2 on a nominal 2 ms period, suggesting an increase from about 100 toward 500 fresh states/second. Other scheduler and radio limits may prevent that result. This is not an 8 kHz HID experiment.

Candidate Key package SHA-256:
`faee94bd17317f9b26afb1d0d33c8bc2f7980ab50e9a23184bd4c124678f2cd8`

The application change was reproduced in both flash payloads and understood package integrity fields updated. All non-flash payloads were preserved. The vendor library accepted the candidate file and identified the expected model. That file check alone does not establish complete authentication or successful execution.

## Recovery and update

M/Mode + X + B did not enter recovery during this session. Home/Logo + Menu while reconnecting USB did. Windows showed a BR27 UBOOT1.00 device at I:, with USB parent `4C4A:3442`. A query using the hash-verified stock Key package returned `GS_G7_Pro_CE_DV`, version `2.29`, description `br27uboot1.00`, with success codes.

The flash helper independently checked the exact device/drive mapping, closed GameSir Connect, candidate and restore hashes, and vendor DLL hash. It then queried the model again, enabled the vendor's G7-specific dual-backup setting, and called `JL_upgradeDevice('I', false, true, null)` once. Return code was 0. Both banks may have been written; automatic rollback is not guaranteed.

Logs: `recovery-query.json`, `gate-2-flash.jsonl`, `gate-2-flash-process-result.json`.

The controller subsequently enumerated as wired `3537:10C5` and answered profile reads as firmware 2.2.9, which is expected because the patch does not change version metadata.

## Preserved data

The four normal profiles, Shift profile, lighting profile, and active-profile selection were read before and after the update. All six profile payloads are byte-identical. Evidence: `profile-comparison.json`, `profile-backup-2026-10-21/`, and `profile-gate-2-postflash-2026-10-21/`.

This is not a complete flash, calibration, pairing, or NVM backup. No calibration write was performed.

The stock restore package is `GS_G7_Pro_CE_DV_V229_Key-STOCK-RESTORE.ufw`, SHA-256:
`4d2982df76d92f0ef7564a98005c8b01c25e0c9d1f995becbe1afe72e55882c4`

## Native HID result

| Measurement | Stock 2.2.9 | Gate-2 experiment |
| --- | ---: | ---: |
| Receiver HID reports per second | 994.7 | 993.6 |
| Left stick changing states per second during movement span | 99.6 | 475.9 |
| Right stick changing states per second during movement span | 100.0 | 472.2 |
| Median changing-state interval | 10.0007 ms | 2.0000 ms |
| 95th-percentile changing-state interval | about 11.0 ms | about 4.0 ms |
| Most common repeated-state run | 10 reports | 2 reports |
| All four extra-button bits observed | Yes | Yes |
| All four axes reached -32768 and +32767 | Yes | Yes |

Each recording lasted 90 seconds, with manual movement occupying only part of the recording. State-change rates above use the span between changing samples, not the entire session including idle time. The median count in one-second bins with at least 50 changes rose from 100 to 477.

Post-flash axes produced 8,014–8,374 distinct observed codes each. This is not a calibrated bit-depth result or proof that sensor resolution increased; faster reporting and different manual sweeps expose more codes. The application change does not modify the axis encoding.

Evidence: `gate-2-receiver-after-flash.summary.json` and `timing-comparison.json`. No disconnection or malformed report was seen in the 90-second post-flash capture. That short observation does not establish long-term reliability.

## Wireless XInput sanity check

The controller switched back to wireless XInput as `3537:10C6`. XInputGetCapabilitiesEx identified that physical device in slot 1, distinct from another connected Xbox device in slot 0. A read-only 60-second capture sampled only the GameSir slot, without a sleep between API queries.

- Changing stick states: approximately 3,858 per second over the moving-state span, with 0.2501 ms median and 0.2743 ms 95th-percentile intervals.
- All four axes reached -32768 and +32767, with 46,408–48,866 observed distinct values per axis and one-unit minimum observed steps.
- Both triggers reached 0 and 255 and exposed intermediate values; A and B were observed separately.
- No XInput disconnection occurred during the capture.

The profile's previously verified configured rate was 4,000 Hz. These post-flash API observations are consistent with that high-rate path continuing to operate. There was no fresh stock XInput A/B recording in this session, so small performance differences cannot be determined. This is not a direct USB, sensor-resolution, or physical-latency measurement. Distinct output codes do not establish effective sensor bit depth.

Evidence: `gate-2-wireless-xinput.summary.json`; raw data is in `work/gamesir_firmware_inspection/preflight/gate-2-wireless-xinput.json`.

## End state and limits

The experimental controller firmware remains installed. The receiver firmware and installed GameSir packages were not changed. The controller is currently connected through its receiver in normal XInput mode after the final check. GameSir Connect remains closed. All recording handles have been closed.

The version still reports 2.2.9 because version metadata was deliberately preserved. A subsequent ordinary firmware installation would replace this experimental application change. The tested package has not been distributed or presented as a generally safe release.

Long-term stability, battery impact, performance with simultaneous audio/gyro traffic, and complete game-to-screen latency remain unmeasured. Steam's actual processing cadence and per-game behavior were not measured. The HID and XInput results are separate mode tests; this does not provide simultaneous XInput-rate sticks and independent extras to Steam.

Recovery combinations are documented by [GameSir](https://gamesir.com/support/manuals/gamesir-g7-pro-aimlabs-edition). This session verifies entry and model query, not recovery from a failed boot.
