# Results and measurement limits

All of these firmware tests used the same G7 Pro 8K PC/Aimlabs controller and receiver on Windows. The report-rate profile was set to 4,000 Hz. Manual stick sweeps, pauses and button presses differ between recordings. This is a one-unit functional test, not a broad hardware validation.

## Stock and the first wireless change

A fresh stock 2.2.9 controller / 1.3.5 receiver recording on October 21 measured about 100 fresh stick updates/sec, despite about 1,000 native HID reports/sec. The median fresh interval was about 10 ms.

The first controller-only change lowered a counter gate from 10 to 2. The first post-flash capture measured about 472–476 fresh updates/sec with 2 ms median intervals. A later baseline for the receiver experiment measured 492.9/sec. These are separate movement passes, not contradictory maximum-rate claims.

The saved controller profile and lighting blobs matched byte for byte before and after the controller updates. That covers the backed-up fields; we didn't dump every calibration or NVM region.

## Wired native HID

Before removing the native producer wait, 22 full moving one-second bins each contained about 1,000 changed stick states. The median fresh interval was 0.9995 ms.

After the controller native-rate patch:

| Measurement | Result |
| --- | ---: |
| Moving one-second bins | 22 |
| Fresh changes in those bins | 3,999–4,001/sec |
| Mean across those bins | 3,999.8/sec |
| Median fresh interval | 0.2491 ms |
| p95 fresh interval | 0.3268 ms |
| Observed codes LX / LY / RX / RY | 44,284 / 45,430 / 46,057 / 45,140 |
| Axis endpoints | All four -32768 to 32767 |
| Trigger endpoints | Both 0–255 |
| Independent extras | L4, R4, L5, R5 captured |

The 45-second capture contains 175,323 packets. Overall arrivals averaged 3,890.6/sec; the whole changed-state span averaged 3,370.9/sec and includes a pause. The full per-second counts are published so the active bins can be checked rather than inferred from a headline.

Normal wired XInput after the controller update measured about 3,991.5 changing states/sec with a 0.2483-ms median interval. Full stick/trigger travel and A/B were captured.

## Wireless native HID

The fresh baseline immediately before the receiver update used the already-patched controller with the stock receiver. It measured 999.3 report arrivals/sec and 492.9 fresh stick changes/sec, median 1.9996 ms.

The first native recording after the receiver update captured full stick and trigger travel, A/B and all four extras. Twelve active one-second bins averaged 3,892.8 fresh changes/sec; the median fresh interval was 0.2498 ms. Direction holds and pauses lower its full-span average.

Then we turned the controller off, unplugged and reinserted the receiver normally, powered the controller back into native HID, and recorded again:

| Measurement after cold reconnection | Result |
| --- | ---: |
| Fresh changes during the moving span | 3,978.5/sec |
| Median fresh interval | 0.2498 ms |
| p95 fresh interval | 0.2961 ms |
| Active one-second bins | 12 |
| Fresh changes in those bins | 3,820–4,000/sec |
| Mean across those bins | 3,966.7/sec |
| Observed codes LX / LY / RX / RY | 30,444 / 32,060 / 32,240 / 31,056 |
| Axis endpoints | All four -32768 to 32767 |
| A and the four extras | Captured again |

The complete 25-second reconnect recording contains 95,858 native reports at 3,824.7 arrivals/sec. It includes the idle portion used for buttons. Both native USB queries after flashing, including the reconnect query, confirm **high speed**. Before the receiver patch the native link was **full speed**.

The endpoint descriptor's `bInterval = 1` means 0.125 ms at high speed and 1 ms at full speed. Both native device descriptors still say `bcdUSB = 0x0111`, so that field alone was not used to infer speed. The hub's live connection information supplied the negotiated speed.

## Wireless XInput and GameSir Connect reads

After the receiver update, wireless XInput had 20 active one-second bins averaging 3,962.4 changing stick states/sec. The median fresh interval was 0.2499 ms. The capture reached full stick and trigger ranges and recorded A/B. This is a post-update sanity check; we didn't record a fresh stock-receiver XInput A/B immediately beforehand.

In normal receiver XInput mode, the known `0F 09`, `0F 0B` and bounded `0F 04` reads still worked. They reported controller 2.2.9, receiver 1.3.5, active profile 1, report-rate code 4 / 4,000 Hz, and the existing resolution setting. We did not exercise all Connect GUI edits or recalibration after the receiver patch.

## How the numbers were counted

We recorded physical native reports through a shared, overlapped, read-only Windows HID handle. Native axes are decoded directly from the report. A fresh state means at least one of the four axis codes differs from the previous recognized state.

The analyzer preserves every observed one-second bin. It separately summarizes bins at or above 87.5% of the stated target rate. That explicit threshold is useful for separating deliberate pauses from sustained movement, but it isn't a promise that delivery is uniform. The moving-span rate and interval distribution are included too.

XInput captures used the selected physical GameSir identity, not another connected Xbox controller. API query count was not treated as device polling rate.

The public recordings keep the input payloads and relative nanosecond timestamps unchanged. Device-instance paths, serials and USB topology were removed. Download the captures ZIP from the research release, then run:

```powershell
py -3 tools/analyze_capture.py wireless-native-reconnect.json --expected-rate 4000
```

## Things these results don't establish

Tens of thousands of distinct output codes don't prove tens of thousands of independent physical sensor positions. Filtering, calibration, scaling and noise can all affect those codes. The tests show the fields and changing values we received.

We haven't measured physical sensor sampling, button-to-screen latency, battery life, extended stability, interference tolerance or downstream Steam output timing. Extra buttons and triggers retain their slower radio-state path. The axes patch doesn't make every input a 4 kHz measurement.

The live tester initially plotted native positive-up Y as downward. We corrected the graph, not the firmware or decoder. The controls behaved correctly in the user's game test. Browser Gamepad API recognition was not fixed by these patches.
