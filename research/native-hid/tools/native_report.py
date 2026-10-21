"""Read-only GameSir native HID recording and fresh-state analysis.

No output/feature reports, mode switches, firmware writes, or input injection.
Captures only an explicitly selected VID 3537 / PID 10b8 interface.
The 43 A1 C8 layout follows SDL_hidapi_gamesir.c; report arrival and fresh
state changes are reported separately. This does not measure physical latency.
"""
import argparse
from collections import Counter
import json
from pathlib import Path
import statistics
import struct
import time


def decode_state(packet):
    if packet.startswith(b'\x43\xa1\xc8'):
        payload=packet[3:]
    elif packet.startswith(b'\xa1\xc8'):
        payload=packet[2:]
    else:
        return None
    if len(payload)<14:
        return None
    axes=struct.unpack_from('>hhhh',payload,4)
    return {'axes':axes,'buttons':payload[:4].hex(),'triggers':list(payload[12:14])}


def interval_stats(times):
    if len(times)<2:return {'samples':len(times)}
    dt=[(b-a)/1e6 for a,b in zip(times,times[1:])]
    return {'samples':len(times),'rate_over_span_hz':(len(times)-1)*1e9/(times[-1]-times[0]),'median_interval_ms':statistics.median(dt),'p95_interval_ms':sorted(dt)[min(len(dt)-1,int(len(dt)*.95))]}


def summarize(record):
    reports=[(int(t),bytes.fromhex(raw)) for t,raw in record['reports']]
    native=[(t,decode_state(raw)) for t,raw in reports]
    native=[(t,s) for t,s in native if s is not None]
    result={'label':record.get('label'),'connection':record.get('connection'),'report_count':len(reports),'report_ids':dict(Counter(raw[0] if raw else -1 for _,raw in reports)),'all_report_arrivals':interval_stats([t for t,_ in reports]),'recognized_native_reports':len(native)}
    if not native:
        result['note']='No recognized 16-bit GameSir SDL reports. Do not interpret this as a stick-rate result.'
        return result
    result['native_report_arrivals']=interval_stats([t for t,_ in native])
    result['axes']={name:{'min':min(s['axes'][i] for _,s in native),'max':max(s['axes'][i] for _,s in native),'observed_unique_values':len({s['axes'][i] for _,s in native})} for i,name in enumerate(('lx','ly','rx','ry'))}
    for name,indices in [('left_stick',(0,1)),('right_stick',(2,3)),('both_sticks',(0,1,2,3))]:
        last=tuple(native[0][1]['axes'][i] for i in indices)
        changed=[]; repeats=[]; run=1
        for t,state in native[1:]:
            now=tuple(state['axes'][i] for i in indices)
            if now!=last:
                changed.append(t);repeats.append(run);run=1;last=now
            else:run+=1
        repeats.append(run)
        result[name]={'state_changes':len(changed),'change_intervals':interval_stats(changed),'repeated_report_run_lengths':dict(sorted(Counter(repeats).items()))}
    result['button_field_states']=dict(Counter(s['buttons'] for _,s in native))
    result['limits']=['Movement and quantization affect observed state changes; these are not sensor-sampling measurements.','Host buffering may bunch read completions.','This records physical HID reports, not verified Steam-processed output.','No maximum sensor bit depth or button-to-screen latency is inferred.']
    return result
