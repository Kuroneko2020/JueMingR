"""Offline export of the twelve fixed geometric assets; Python standard library only.

Normal builds embed tab-icons.alpha and do not run this tool or need Python.
Coordinates are the authorized source artwork, not a runtime drawing implementation.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SIZE, SAMPLES = 72, 5


def segment(x1, y1, x2, y2, stroke):
    dx, dy = x2 - x1, y2 - y1
    length = dx * dx + dy * dy
    def inside(x, y):
        t = max(0, min(1, ((x - x1) * dx + (y - y1) * dy) / length)) if length else 0
        return (x - x1 - t * dx) ** 2 + (y - y1 - t * dy) ** 2 <= (stroke / 2) ** 2
    return inside


def polyline(stroke, closed, *points):
    pairs = list(zip(points[::2], points[1::2]))
    if closed:
        pairs.append(pairs[0])
    return [segment(*a, *b, stroke) for a, b in zip(pairs, pairs[1:])]


def arc(cx, cy, radius, start, end, stroke):
    points = []
    for i in range(9):
        angle = math.radians(start + (end - start) * i / 8)
        points += [cx + radius * math.cos(angle), cy + radius * math.sin(angle)]
    return polyline(stroke, False, *points)


def primitives(kind, a):
    if kind == 'Segment':
        return [segment(*a)]
    if kind == 'Polyline':
        return polyline(*a)
    if kind == 'CircleOutline':
        cx, cy, r, stroke = a
        return [lambda x, y: abs(math.hypot(x - cx, y - cy) - r) <= stroke / 2]
    if kind == 'FilledCircle':
        cx, cy, r = a
        return [lambda x, y: (x - cx) ** 2 + (y - cy) ** 2 <= r * r]
    if kind == 'EllipseOutline':
        cx, cy, rx, ry, stroke = a
        points = []
        for i in range(41):
            angle = 2 * math.pi * i / 40
            points += [cx + rx * math.cos(angle), cy + ry * math.sin(angle)]
        return polyline(stroke, False, *points)
    if kind == 'RoundedRectOutline':
        x, y, w, h, r, stroke = a
        right, bottom = x + w, y + h
        return [segment(x + r, y, right - r, y, stroke),
                segment(x + r, bottom, right - r, bottom, stroke),
                segment(x, y + r, x, bottom - r, stroke),
                segment(right, y + r, right, bottom - r, stroke)] + \
            arc(x + r, y + r, r, 180, 270, stroke) + \
            arc(right - r, y + r, r, 270, 360, stroke) + \
            arc(right - r, bottom - r, r, 0, 90, stroke) + \
            arc(x + r, bottom - r, r, 90, 180, stroke)
    raise ValueError(kind)


def export():
    icons = json.loads((ROOT / 'tab-icons.geometry.json').read_text(encoding='utf-8'))
    assert len(icons) == 12
    output = bytearray()
    for icon in icons:
        mask = [0] * (SIZE * SIZE)
        for kind, arguments in icon['shapes']:
            for contains in primitives(kind, arguments):
                for py in range(SIZE):
                    for px in range(SIZE):
                        count = sum(contains((px + (sx + .5) / SAMPLES) * 18 / SIZE,
                                             (py + (sy + .5) / SAMPLES) * 18 / SIZE)
                                    for sy in range(SAMPLES) for sx in range(SAMPLES))
                        index = py * SIZE + px
                        mask[index] = max(mask[index], count)
        output.extend(round(value * 255 / (SAMPLES * SAMPLES)) for value in mask)
    (ROOT / 'tab-icons.alpha').write_bytes(output)
    print('Exported twelve 72x72 alpha masks:', len(output), 'bytes')


if __name__ == '__main__':
    export()
