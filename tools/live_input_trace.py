"""Log game-visible XInput and mouse-button transitions for live diagnosis."""

from __future__ import annotations

import argparse
import csv
import ctypes
import ctypes.wintypes as wt
from datetime import datetime
from pathlib import Path
import time


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


MOUSE_BUTTONS = {
    0x01: "mouse_left",
    0x02: "mouse_right",
    0x04: "mouse_middle",
    0x05: "mouse_x1",
    0x06: "mouse_x2",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seconds", type=float, default=90.0)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    output = args.output or Path("logs") / f"input-trace-{datetime.now():%Y%m%d-%H%M%S}.csv"
    output.parent.mkdir(parents=True, exist_ok=True)

    xinput = ctypes.WinDLL("xinput1_4.dll")
    xinput.XInputGetState.argtypes = [wt.DWORD, ctypes.POINTER(XInputState)]
    xinput.XInputGetState.restype = wt.DWORD
    user32 = ctypes.WinDLL("user32.dll")
    user32.GetAsyncKeyState.argtypes = [ctypes.c_int]
    user32.GetAsyncKeyState.restype = ctypes.c_short

    started = time.perf_counter()
    last_xinput: tuple[int, int, int] | None = None
    last_mouse: dict[int, bool] = {}

    print(f"Tracing game-visible input to {output.resolve()}", flush=True)
    with output.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream)
        writer.writerow(["seconds", "source", "buttons", "left_trigger", "right_trigger", "control", "pressed"])

        while time.perf_counter() - started < args.seconds:
            elapsed = time.perf_counter() - started
            state = XInputState()
            if xinput.XInputGetState(0, ctypes.byref(state)) == 0:
                current = (
                    state.gamepad.buttons,
                    state.gamepad.left_trigger,
                    state.gamepad.right_trigger,
                )
                if current != last_xinput:
                    writer.writerow([f"{elapsed:.6f}", "xinput", *current, "", ""])
                    stream.flush()
                    print(
                        f"{elapsed:7.3f} XInput buttons=0x{current[0]:04X} "
                        f"LT={current[1]:3} RT={current[2]:3}",
                        flush=True,
                    )
                    last_xinput = current

            for virtual_key, name in MOUSE_BUTTONS.items():
                pressed = bool(user32.GetAsyncKeyState(virtual_key) & 0x8000)
                if last_mouse.get(virtual_key) != pressed:
                    writer.writerow([f"{elapsed:.6f}", "mouse", "", "", "", name, int(pressed)])
                    stream.flush()
                    print(f"{elapsed:7.3f} {name}={int(pressed)}", flush=True)
                    last_mouse[virtual_key] = pressed

            time.sleep(0.0005)

    print("Trace complete.", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
