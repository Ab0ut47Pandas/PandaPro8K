"""Compare the active XInput slot with the virtual DualSense Edge report."""

from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes as wt
import time

import hid


class XInputGamepad(ctypes.Structure):
    _fields_ = [
        ("buttons", wt.WORD),
        ("left_trigger", wt.BYTE),
        ("right_trigger", wt.BYTE),
        ("left_x", ctypes.c_short),
        ("left_y", ctypes.c_short),
        ("right_x", ctypes.c_short),
        ("right_y", ctypes.c_short),
    ]


class XInputState(ctypes.Structure):
    _fields_ = [("packet", wt.DWORD), ("gamepad", XInputGamepad)]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seconds", type=float, default=25.0)
    args = parser.parse_args()

    xinput = ctypes.WinDLL("xinput1_4.dll")
    xinput.XInputGetState.argtypes = [wt.DWORD, ctypes.POINTER(XInputState)]
    xinput.XInputGetState.restype = wt.DWORD

    devices = hid.enumerate(0x054C, 0x0DF2)
    if not devices:
        raise SystemExit("Virtual DualSense Edge 054C:0DF2 was not found.")

    virtual = hid.device()
    virtual.open_path(devices[0]["path"])
    virtual.set_nonblocking(True)

    started = time.perf_counter()
    deadline = started + args.seconds
    last: tuple[int, ...] | None = None
    virtual_state = (0, 0, 0, 0)
    print("time    XInput LT/RT     virtual LT/RT    virtual L2/R2 digital")
    try:
        while time.perf_counter() < deadline:
            report = virtual.read(64)
            if len(report) >= 10 and report[0] == 0x01:
                virtual_state = (
                    report[5],
                    report[6],
                    1 if report[9] & 0x04 else 0,
                    1 if report[9] & 0x08 else 0,
                )

            xinput_state = XInputState()
            result = xinput.XInputGetState(0, ctypes.byref(xinput_state))
            if result == 0:
                current = (
                    xinput_state.gamepad.left_trigger,
                    xinput_state.gamepad.right_trigger,
                    *virtual_state,
                )
                if current != last:
                    elapsed = time.perf_counter() - started
                    print(
                        f"{elapsed:6.3f}       {current[0]:3}/{current[1]:3}"
                        f"           {current[2]:3}/{current[3]:3}"
                        f"               {current[4]}/{current[5]}",
                        flush=True,
                    )
                    last = current
            time.sleep(0.001)
    finally:
        virtual.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
