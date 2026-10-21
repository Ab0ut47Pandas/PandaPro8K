"""Recompute native HID or XInput timing from saved JSON. No device access."""
import argparse
import json
from pathlib import Path

from native_report import decode_state, interval_stats, summarize

BUTTONS = {'A': (0, 1), 'B': (0, 2), 'L4': (2, 0x40), 'R4': (2, 0x80), 'L5': (3, 1), 'R5': (3, 2)}


def analyze(record, rate=4000):
    bins = {}
    changed = []
    previous = None
    presses = {name: 0 for name in BUTTONS}
    old_buttons = set()
    native = not record.get('columns')
    states = []
    for row in record['reports']:
        if native:
            stamp, packet = row
            state = decode_state(bytes.fromhex(packet))
            if state is None: continue
            axes = tuple(state['axes'])
            fields = bytes.fromhex(state['buttons'])
            buttons = {name for name, (index, mask) in BUTTONS.items() if fields[index] & mask}
            for name in buttons - old_buttons: presses[name] += 1
            old_buttons = buttons
        else:
            stamp = row[0]
            axes = tuple(row[4:8])
        states.append((stamp, axes))
        if previous is not None and axes != previous:
            changed.append(stamp)
            index = stamp // 1_000_000_000
            bins[index] = bins.get(index, 0) + 1
        previous = axes
    if not states: raise ValueError('No recognized states in this capture')
    active = [value for value in bins.values() if value >= rate * .875]
    result = {
        'kind': 'native HID' if native else 'XInput API observations',
        'fresh_stick_changes': interval_stats(changed),
        'changed_states_per_1s_bin': bins,
        'active_1s_bins': {'threshold_hz': rate * .875, 'count': len(active),
                           'min': min(active) if active else None, 'max': max(active) if active else None,
                           'mean': sum(active) / len(active) if active else None},
        'axes': {name: {'min': min(axes[i] for _, axes in states), 'max': max(axes[i] for _, axes in states),
                        'distinct_codes': len({axes[i] for _, axes in states})}
                 for i, name in enumerate(('lx', 'ly', 'rx', 'ry'))},
        'limits': ['Changed output codes do not establish effective sensor bits or physical latency.',
                   'Active bins use an explicit threshold; all observed bins are included for review.'],
    }
    if native:
        result['native_summary'] = summarize(record)
        result['button_press_counts'] = presses
    else:
        result['a_observed'] = any(row[8] & 0x1000 for row in record['reports'])
        result['b_observed'] = any(row[8] & 0x2000 for row in record['reports'])
        result['triggers'] = {name: {'min': min(row[i] for row in record['reports']), 'max': max(row[i] for row in record['reports'])}
                              for name, i in (('left', 2), ('right', 3))}
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path)
    parser.add_argument('--expected-rate', type=float, default=4000)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    if args.expected_rate <= 0: parser.error('Expected rate must be positive')
    result = analyze(json.loads(args.input.read_text(encoding='utf-8')), args.expected_rate)
    text = json.dumps(result, indent=2)
    if args.output:
        with args.output.open('x', encoding='utf-8') as target: target.write(text + '\n')
    else: print(text)


if __name__ == '__main__': main()
