"""Read-only motion-triggered native capture. No activation or device writes."""
import argparse
from collections import deque
import ctypes as C
import json
from pathlib import Path
import time

from native_report import decode_state


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--connection', choices=('wired', 'receiver'), required=True)
    parser.add_argument('--seconds', type=float, default=30)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if not 1 <= args.seconds <= 120: parser.error('Use 1–120 seconds')
    if args.output.exists(): parser.error('Choose a new output file')
    import windows_hid_read as h
    expected = 0x229 if args.connection == 'wired' else 0x135
    infos = [h.describe(path) for path in h.paths() if 'vid_3537&pid_10b8' in path.lower()]
    choices = [info for info in infos if info.get('bcd_device') == expected and
               info.get('caps', {}).get('InputReportByteLength') == 37 and
               info['caps'].get('UsagePage') == 0xfff0 and info['caps'].get('Usage') == 0x40]
    if len(choices) != 1: parser.error('Connect exactly one matching native GameSir. See tools/README.md')
    info = choices[0]
    handle = h.openfile(info['path'], 0x80000000, 3, None, 3, 0x40000000, None)
    if handle == h.INVALID: raise C.WinError(C.get_last_error())
    event = h.event(None, True, False, None)
    if not event:
        h.close(handle)
        raise C.WinError(C.get_last_error())
    ov = h.OVERLAPPED(hEvent=event)
    buffer = C.create_string_buffer(37)
    armed = time.perf_counter_ns(); motion = None; rows = []; pre = deque(maxlen=250)
    print(json.dumps({'status': 'waiting for stick movement', 'timeout_seconds': 300,
                      'record_seconds': args.seconds}), flush=True)
    try:
        while True:
            now = time.perf_counter_ns()
            if motion is not None and now-motion >= args.seconds*1e9: break
            if motion is None and now-armed >= 300e9: raise TimeoutError('No movement detected in five minutes')
            h.reset(event); count = h.W.DWORD()
            if not h.read(handle, buffer, 37, C.byref(count), C.byref(ov)):
                error = C.get_last_error()
                if error != 997: raise C.WinError(error)
                rc = h.wait(event, 100)
                if rc == 258:
                    h.cancel(handle, C.byref(ov)); h.getover(handle, C.byref(ov), C.byref(count), True)
                    continue
                if rc != 0 or not h.getover(handle, C.byref(ov), C.byref(count), False):
                    raise RuntimeError('HID input completion failed')
            if count.value:
                now = time.perf_counter_ns(); packet = buffer.raw[:count.value]; state = decode_state(packet)
                if motion is None:
                    pre.append((now, packet.hex()))
                    if state is not None and any(abs(axis) > 4096 for axis in state['axes']):
                        motion = now; rows = list(pre)
                        print(json.dumps({'status': 'recording', 'seconds': args.seconds}), flush=True)
                else: rows.append((now, packet.hex()))
    finally:
        h.cancel(handle, C.byref(ov)); count = h.W.DWORD()
        h.getover(handle, C.byref(ov), C.byref(count), True); h.close(handle); h.close(event)
    origin = rows[0][0]
    record = {'label': args.connection+'-native', 'connection': args.connection,
              'device': {key: info[key] for key in ('vid', 'pid', 'bcd_device', 'product', 'caps') if key in info},
              'reports': [(stamp-origin, packet) for stamp, packet in rows],
              'elapsed_seconds': (rows[-1][0]-origin)/1e9}
    with args.output.open('x', encoding='utf-8') as target: json.dump(record, target)
    print(json.dumps({'status': 'saved', 'reports': len(rows), 'file': args.output.name,
                      'identifiers_saved': False}), flush=True)


if __name__ == '__main__': main()
