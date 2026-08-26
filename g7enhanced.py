#!/usr/bin/env python3
"""Capture GameSir enhanced 0x12 reports while sustaining the 0F F2 heartbeat."""

import argparse
import json
import statistics
import time
from collections import Counter
from pathlib import Path

import hid

VID = 0x3537


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pid", type=lambda x: int(x, 0), default=0x10C6)
    ap.add_argument("--seconds", type=float, default=10)
    ap.add_argument("--output", default="enhanced-capture.json")
    args = ap.parse_args()
    matches = [x for x in hid.enumerate(VID, args.pid)
               if x.get("usage_page") == 0xFFF0 and x.get("usage") == 0x40]
    if not matches:
        raise SystemExit(f"No GameSir vendor collection for {VID:04X}:{args.pid:04X}")
    dev = hid.device()
    dev.open_path(matches[0]["path"])
    dev.set_nonblocking(True)
    start = time.perf_counter_ns()
    deadline = start + int(args.seconds * 1e9)
    next_heartbeat = start
    frames = []
    heartbeat = bytes((0x0F, 0xF2)) + bytes(62)
    try:
        while time.perf_counter_ns() < deadline:
            now = time.perf_counter_ns()
            if now >= next_heartbeat:
                dev.write(heartbeat)
                next_heartbeat = now + 1_000_000_000
            data = dev.read(256)
            if data:
                frames.append({"t_ns": time.perf_counter_ns() - start,
                               "data": bytes(data).hex()})
            else:
                time.sleep(0)
    finally:
        dev.close()
    Path(args.output).write_text(json.dumps({
        "vid": VID, "pid": args.pid,
        "heartbeat": "0f f2 padded to 64 bytes, once per second",
        "clock": "time.perf_counter_ns",
        "frames": frames,
    }, indent=2), encoding="utf-8")
    ids = Counter(bytes.fromhex(x["data"])[0] for x in frames)
    print(f"Saved {len(frames)} reports to {args.output}; IDs={dict(ids)}")
    if len(frames) > 1:
        times = [x["t_ns"] for x in frames]
        dt = [(b-a)/1e6 for a,b in zip(times,times[1:])]
        print(f"rate={(len(times)-1)/((times[-1]-times[0])/1e9):.2f} Hz "
              f"median_interval={statistics.median(dt):.4f} ms")


if __name__ == "__main__":
    main()
