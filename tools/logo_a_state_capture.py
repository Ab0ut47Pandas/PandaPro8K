#!/usr/bin/env python3
"""Passive SDL state logger for the GameSir Logo+A gamepad path."""

import argparse
import csv
import sys
import time

import pygame


def snapshot(joystick):
    axes = tuple(round(joystick.get_axis(i), 6) for i in range(joystick.get_numaxes()))
    buttons = sum(1 << i for i in range(joystick.get_numbuttons()) if joystick.get_button(i))
    hats = tuple(joystick.get_hat(i) for i in range(joystick.get_numhats()))
    return axes, buttons, hats


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--seconds", type=float, default=300)
    args = parser.parse_args()

    pygame.init()
    pygame.joystick.init()

    joystick = None
    for index in range(pygame.joystick.get_count()):
        candidate = pygame.joystick.Joystick(index)
        candidate.init()
        if "gamesir" in candidate.get_name().lower():
            joystick = candidate
            break

    if joystick is None:
        print("GameSir SDL controller not found", file=sys.stderr, flush=True)
        return 1

    writer = csv.writer(sys.stdout, lineterminator="\n")
    writer.writerow(["utc", "elapsed_us", "event", "axes", "buttons", "hats"])
    sys.stdout.flush()

    start_ns = time.perf_counter_ns()
    deadline = time.perf_counter() + args.seconds
    previous = None
    next_heartbeat = 0.0

    while time.perf_counter() < deadline:
        pygame.event.pump()
        current = snapshot(joystick)
        now = time.perf_counter()
        event = None

        if previous is None:
            event = "initial"
        else:
            old_axes, old_buttons, old_hats = previous
            trigger_changed = any(
                abs(current[0][index] - old_axes[index]) >= 0.001
                for index in range(4, min(6, len(current[0]), len(old_axes)))
            )
            if trigger_changed:
                event = "trigger-change"
            elif current[1] != old_buttons:
                event = "button-change"
            elif current[2] != old_hats:
                event = "hat-change"
            elif now >= next_heartbeat:
                event = "heartbeat"

        if event:
            elapsed_us = (time.perf_counter_ns() - start_ns) // 1000
            writer.writerow([
                time.strftime("%Y-%m-%dT%H:%M:%S", time.gmtime()),
                elapsed_us,
                event,
                ";".join(f"{value:.6f}" for value in current[0]),
                f"0x{current[1]:X}",
                repr(current[2]),
            ])
            sys.stdout.flush()
            next_heartbeat = now + 1.0

        previous = current
        time.sleep(0.0005)

    pygame.quit()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
