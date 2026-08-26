#!/usr/bin/env python3
"""Small Windows raw-HID diagnostic for GameSir controllers."""

import argparse
import json
import statistics
import sys
import time
from collections import Counter
from pathlib import Path

try:
    import hid
except ImportError:
    raise SystemExit("Missing dependency. Run: py -3 -m pip install hidapi")

GAMESIR_VID = 0x3537


def devices(pid=None):
    return hid.enumerate(GAMESIR_VID, pid or 0)


def path_text(value):
    return value.decode(errors="replace") if isinstance(value, bytes) else value


def list_devices(args):
    found = devices(args.pid)
    if not found:
        print("No GameSir HID interfaces found.")
        return 1
    for index, item in enumerate(found):
        print(f"[{index}] {item.get('product_string') or 'GameSir HID'}")
        print(f"  VID:PID       {item['vendor_id']:04X}:{item['product_id']:04X}")
        print(f"  interface     {item.get('interface_number')}")
        print(f"  usage         {item.get('usage_page', 0):04X}:{item.get('usage', 0):04X}")
        print(f"  path          {path_text(item['path'])}")
    return 0


def percentile(values, p):
    if not values:
        return None
    ordered = sorted(values)
    position = (len(ordered) - 1) * p
    lo = int(position)
    hi = min(lo + 1, len(ordered) - 1)
    fraction = position - lo
    return ordered[lo] * (1 - fraction) + ordered[hi] * fraction


def select_interfaces(args):
    selected = []
    for item in devices(args.pid):
        if args.index is not None and len(selected) == 0:
            # Index is over the complete enumeration, handled below.
            continue
        if args.usage_page is not None and item.get("usage_page") != args.usage_page:
            continue
        if args.usage is not None and item.get("usage") != args.usage:
            continue
        selected.append(item)
    if args.index is not None:
        all_items = devices(args.pid)
        if args.index < 0 or args.index >= len(all_items):
            raise SystemExit(f"--index must be between 0 and {len(all_items)-1}")
        selected = [all_items[args.index]]
    return selected


def capture(args):
    selected = select_interfaces(args)
    if not selected:
        raise SystemExit("No matching GameSir HID interface.")
    output = Path(args.output)
    start_ns = time.perf_counter_ns()
    frames = []
    opened = []
    try:
        for item in selected:
            dev = hid.device()
            try:
                dev.open_path(item["path"])
                dev.set_nonblocking(True)
                opened.append((item, dev))
            except OSError as exc:
                print(f"Skipping {path_text(item['path'])}: {exc}", file=sys.stderr)
        if not opened:
            raise SystemExit("None of the selected interfaces could be opened.")
        deadline = time.perf_counter() + args.seconds
        while time.perf_counter() < deadline:
            received = False
            for item, dev in opened:
                data = dev.read(args.max_length)
                if data:
                    received = True
                    frames.append({
                        "t_ns": time.perf_counter_ns() - start_ns,
                        "interface": item.get("interface_number"),
                        "usage_page": item.get("usage_page"),
                        "usage": item.get("usage"),
                        "data": bytes(data).hex(),
                    })
            if not received:
                time.sleep(0)
    finally:
        for _, dev in opened:
            dev.close()
    document = {
        "clock": "time.perf_counter_ns (Windows QueryPerformanceCounter-backed)",
        "duration_requested_s": args.seconds,
        "devices": [{**{k: v for k, v in item.items() if k != "path"},
                     "path": path_text(item["path"])} for item, _ in opened],
        "frames": frames,
    }
    output.write_text(json.dumps(document, indent=2), encoding="utf-8")
    print(f"Saved {len(frames)} reports to {output}")
    summarize_frames(frames)
    return 0


def summarize_frames(frames):
    groups = {}
    for frame in frames:
        key = (frame["interface"], frame["usage_page"], frame["usage"], frame["data"][:2])
        groups.setdefault(key, []).append(frame)
    for key, rows in groups.items():
        times = [row["t_ns"] for row in rows]
        intervals = [(b-a)/1_000_000 for a, b in zip(times, times[1:])]
        lengths = Counter(len(bytes.fromhex(row["data"])) for row in rows)
        print(f"interface={key[0]} usage={key[1]:04X}:{key[2]:04X} report_id=0x{key[3].upper()}")
        print(f"  reports={len(rows)} lengths={dict(lengths)}")
        if intervals:
            span_s = (times[-1] - times[0]) / 1_000_000_000
            print(f"  rate={len(intervals)/span_s:.2f} Hz")
            print(f"  interval_ms median={statistics.median(intervals):.4f} mean={statistics.mean(intervals):.4f} "
                  f"min={min(intervals):.4f} p95={percentile(intervals,.95):.4f} "
                  f"p99={percentile(intervals,.99):.4f} max={max(intervals):.4f} "
                  f"stdev={statistics.pstdev(intervals):.4f}")


def summarize(args):
    document = json.loads(Path(args.input).read_text(encoding="utf-8"))
    summarize_frames(document["frames"])
    return 0


def number(value):
    return int(value, 0)


def main():
    parser = argparse.ArgumentParser(prog="g7diag")
    sub = parser.add_subparsers(dest="command", required=True)
    lp = sub.add_parser("list")
    lp.add_argument("--pid", type=number)
    lp.set_defaults(func=list_devices)
    cp = sub.add_parser("capture")
    cp.add_argument("--seconds", type=float, default=10)
    cp.add_argument("--output", default="capture.json")
    cp.add_argument("--pid", type=number)
    cp.add_argument("--index", type=int)
    cp.add_argument("--usage-page", type=number)
    cp.add_argument("--usage", type=number)
    cp.add_argument("--max-length", type=int, default=256)
    cp.set_defaults(func=capture)
    sp = sub.add_parser("summarize")
    sp.add_argument("input")
    sp.set_defaults(func=summarize)
    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
