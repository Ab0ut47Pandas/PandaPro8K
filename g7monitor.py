#!/usr/bin/env python3
"""Live Logo+A monitor: SDL controls plus GameSir's hidden vendor report."""

import math
import queue
import threading
import time

import hid
import pygame

VID, PID = 0x3537, 0x10B8
WIDTH, HEIGHT = 1060, 760
BG = (17, 21, 29)
PANEL = (28, 34, 45)
TEXT = (225, 231, 239)
MUTED = (135, 145, 160)
GREEN = (59, 214, 135)
CYAN = (55, 190, 235)
ORANGE = (245, 166, 72)


def vendor_reader(out, stop):
    matches = [x for x in hid.enumerate(VID, PID)
               if x.get("interface_number") == 0
               and x.get("usage_page") == 0xFFF0 and x.get("usage") == 0x40]
    if not matches:
        out.put(("error", "Vendor HID collection not found"))
        return
    dev = hid.device()
    try:
        dev.open_path(matches[0]["path"])
        dev.set_nonblocking(True)
        while not stop.is_set():
            data = dev.read(256)
            if data:
                out.put((time.perf_counter_ns(), bytes(data)))
            else:
                time.sleep(0)
    except OSError as exc:
        out.put(("error", str(exc)))
    finally:
        dev.close()


def text(screen, font, value, x, y, color=TEXT):
    screen.blit(font.render(value, True, color), (x, y))


def panel(screen, rect):
    pygame.draw.rect(screen, PANEL, rect, border_radius=12)


def stick(screen, font, center, xy, label):
    cx, cy = center
    pygame.draw.circle(screen, (55, 64, 78), center, 72, 3)
    pygame.draw.line(screen, (47, 55, 68), (cx-66, cy), (cx+66, cy), 1)
    pygame.draw.line(screen, (47, 55, 68), (cx, cy-66), (cx, cy+66), 1)
    px = cx + int(xy[0] * 64)
    py = cy + int(xy[1] * 64)
    pygame.draw.circle(screen, CYAN, (px, py), 11)
    text(screen, font, f"{label}  x {xy[0]:+.4f}  y {xy[1]:+.4f}", cx-100, cy+82, MUTED)


def trigger(screen, font, x, y, value, label):
    # SDL axes are usually -1 released and +1 pressed.
    normalized = max(0.0, min(1.0, (value + 1.0) / 2.0))
    pygame.draw.rect(screen, (51, 59, 72), (x, y, 46, 150), border_radius=8)
    fill = int(144 * normalized)
    pygame.draw.rect(screen, ORANGE, (x+3, y+147-fill, 40, fill), border_radius=6)
    text(screen, font, f"{label} {normalized:.3f}", x-8, y+158, MUTED)


def main():
    pygame.init()
    pygame.joystick.init()
    screen = pygame.display.set_mode((WIDTH, HEIGHT))
    pygame.display.set_caption("G7 Pro 8K — Logo+A live monitor")
    font = pygame.font.SysFont("Segoe UI", 17)
    small = pygame.font.SysFont("Consolas", 15)
    title = pygame.font.SysFont("Segoe UI Semibold", 27)
    clock = pygame.time.Clock()

    joystick = None
    for i in range(pygame.joystick.get_count()):
        candidate = pygame.joystick.Joystick(i)
        candidate.init()
        if "GameSir" in candidate.get_name():
            joystick = candidate
            break

    messages = queue.Queue()
    stop = threading.Event()
    thread = threading.Thread(target=vendor_reader, args=(messages, stop), daemon=True)
    thread.start()
    current_raw = b""
    previous_raw = b""
    changed_until = {}
    raw_times = []
    error = ""
    running = True
    while running:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                running = False
        now = time.perf_counter()
        while True:
            try:
                stamp, payload = messages.get_nowait()
            except queue.Empty:
                break
            if stamp == "error":
                error = payload
                continue
            previous_raw, current_raw = current_raw, payload
            raw_times.append(stamp)
            cutoff = stamp - 2_000_000_000
            while raw_times and raw_times[0] < cutoff:
                raw_times.pop(0)
            if previous_raw:
                for i, (a, b) in enumerate(zip(previous_raw, current_raw)):
                    if a != b:
                        changed_until[i] = now + 0.35

        screen.fill(BG)
        text(screen, title, "GameSir G7 Pro 8K — Logo+A", 28, 20)
        status = "SDL connected" if joystick else "SDL controller not found"
        text(screen, font, status, 30, 58, GREEN if joystick else ORANGE)

        panel(screen, pygame.Rect(24, 92, 650, 350))
        text(screen, font, "STANDARD SDL INPUT", 44, 108, MUTED)
        axes = [joystick.get_axis(i) for i in range(joystick.get_numaxes())] if joystick else [0]*6
        axes += [0] * (6-len(axes))
        stick(screen, small, (160, 255), axes[0:2], "LEFT")
        stick(screen, small, (410, 255), axes[2:4], "RIGHT")
        trigger(screen, small, 535, 166, axes[4], "LT")
        trigger(screen, small, 600, 166, axes[5], "RT")

        panel(screen, pygame.Rect(694, 92, 342, 350))
        text(screen, font, "BUTTONS (SDL indices)", 714, 108, MUTED)
        if joystick:
            for i in range(joystick.get_numbuttons()):
                col, row = i % 3, i // 3
                active = bool(joystick.get_button(i))
                rect = pygame.Rect(714 + col*101, 145 + row*49, 86, 34)
                pygame.draw.rect(screen, GREEN if active else (48, 57, 70), rect, border_radius=8)
                text(screen, font, f"B{i:02d}", rect.x+25, rect.y+6, BG if active else TEXT)
            hat = joystick.get_hat(0) if joystick.get_numhats() else (0, 0)
            text(screen, font, f"D-pad hat: {hat}", 714, 397, CYAN if hat != (0,0) else MUTED)

        panel(screen, pygame.Rect(24, 462, 1012, 270))
        text(screen, font, "RAW VENDOR REPORT — changing bytes flash cyan", 44, 478, MUTED)
        rate = 0.0
        if len(raw_times) > 1:
            rate = (len(raw_times)-1) / ((raw_times[-1]-raw_times[0])/1e9)
        text(screen, font, f"ID 0x{current_raw[0]:02X}   {len(current_raw)} bytes   {rate:.1f} reports/s" if current_raw else "Waiting for 0x43...", 44, 508)
        if error:
            text(screen, font, error, 44, 536, ORANGE)
        for i, value in enumerate(current_raw):
            col, row = i % 13, i // 13
            x, y = 44 + col*75, 554 + row*52
            active = changed_until.get(i, 0) > now
            pygame.draw.rect(screen, (36, 125, 153) if active else (43, 51, 64), (x, y, 63, 40), border_radius=6)
            text(screen, small, f"{i:02}:{value:02X}", x+6, y+10, TEXT)

        pygame.display.flip()
        clock.tick(120)
    stop.set()
    thread.join(timeout=1)
    pygame.quit()


if __name__ == "__main__":
    main()
