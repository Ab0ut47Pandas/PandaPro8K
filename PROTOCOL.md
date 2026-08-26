# Logo+A / 2.4 GHz Protocol Notes

Claims below are limited to locally observed evidence.

## USB/HID identity

- VID: `0x3537`
- PID: `0x10B8`
- Product: `GameSir-G7 Pro 8K`

## Vendor input report

- **CONFIRMED BY LOCAL HARDWARE TEST**
- Interface: 0
- Usage page / usage: `0xFFF0 / 0x0040`
- Report ID: `0x43`
- Total length returned by hidapi: 37 bytes (including report ID)
- Observed cadence: approximately 1,000 Hz

| Byte(s) | Meaning | Status |
|---|---|---|
| 0 | Report ID `0x43` | CONFIRMED BY LOCAL HARDWARE TEST |
| 1–6 | Unknown | UNKNOWN |
| 7–8 | Left-stick axis 1, signed 16-bit little-endian | CONFIRMED BY LOCAL HARDWARE TEST; X assignment INFERRED |
| 9–10 | Left-stick axis 2, signed 16-bit little-endian | CONFIRMED BY LOCAL HARDWARE TEST; Y assignment INFERRED |
| 11–12 | Right-stick axis 1, signed 16-bit little-endian | CONFIRMED BY LOCAL HARDWARE TEST; X assignment INFERRED |
| 13–14 | Right-stick axis 2, signed 16-bit little-endian | CONFIRMED BY LOCAL HARDWARE TEST; Y assignment INFERRED |
| 15–17 | Unknown | UNKNOWN |
| 18, 20, 22 | Continuously varying signed/unsigned sensor candidates | INFERRED; physical rotation test required |
| 19, 21, 23 | Possible high bytes or padding | UNKNOWN |
| 24–36 | Unknown | UNKNOWN |

Stick field grouping and signed representation are decoded. Axis order/polarity, triggers, paddles, gyro, and rumble still require confirmation.

## Stick update cadence

- HID/USB report cadence: approximately 1,000 Hz
- Fresh left-stick vector cadence during continuous circles: 100.21 Hz
- Fresh right-stick vector cadence during continuous circles: 100.18 Hz
- Median fresh-vector interval: 10.000 ms
- Median repeated HID reports per fresh vector: 10

## XInput mode enhanced report

- Transport/mode: Aimlabs 2.4 GHz dongle, XInput/configuration identity `3537:10C6`
- Activation: output heartbeat `0F F2`, padded to 64 bytes, once per second
- Input report ID: `0x12`
- Length: 64 bytes
- Observed cadence: approximately 222 Hz

| Byte | Mask | Physical control | Status |
|---|---:|---|---|
| 60 | `0x08` | Rear-left paddle | CONFIRMED BY LOCAL HARDWARE TEST |
| 60 | `0x10` | Rear-right paddle | CONFIRMED BY LOCAL HARDWARE TEST |
| 63 | `0x01` | Top-left extra button | CONFIRMED BY LOCAL HARDWARE TEST |
| 63 | `0x02` | Top-right extra button | CONFIRMED BY LOCAL HARDWARE TEST |

These physical labels are authoritative for the local captures. Protocol/UI aliases such as L4/R4/L5/R5 must not be assigned until the product's naming is independently confirmed.

### IMU fields

| Bytes | Encoding | Strongest physical response | Status |
|---|---|---|---|
| 14–15 | signed 16-bit big-endian | Pitch angular rate | CONFIRMED BY LOCAL HARDWARE TEST |
| 16–17 | signed 16-bit big-endian | Yaw angular rate | CONFIRMED BY LOCAL HARDWARE TEST |
| 18–19 | signed 16-bit big-endian | Roll angular rate | CONFIRMED BY LOCAL HARDWARE TEST |
| 20–21 | signed 16-bit big-endian | Accelerometer/gravity candidate | INFERRED |
| 22–23 | signed 16-bit big-endian | Accelerometer/gravity candidate | INFERRED |
| 24–25 | signed 16-bit big-endian | Accelerometer/gravity candidate | INFERRED |

All 12 bytes changed on every enhanced report in the isolated motion captures. Observed fresh-vector rate was approximately 217–218 Hz; median inter-vector interval was approximately 5.0 ms. Scale factors, units, polarity, and accelerometer axis assignment are not yet decoded.
