#!/usr/bin/env python3
"""Generate tintable water-sheet and foam sprites for Water Ninja (requires Pillow)."""
from pathlib import Path
import math
from PIL import Image

OUT = Path(__file__).resolve().parent / 'Textures/RimArt/WaterNinja'
SIZE = 256


def smooth(x):
    x = max(0, min(1, x))
    return x * x * (3 - 2 * x)


def sheet(u, v, variant):
    x = (u - 0.5) * 2
    phase = variant * 1.9
    spine = 0.11 * math.sin(x * 3.4 + phase) + 0.045 * math.sin(x * 7 - phase)
    width = (max(0, 1 - x * x) ** 0.65) * (0.48 + 0.09 * math.sin(x * 9 + phase))
    y = (v - 0.5) * 2 - spine
    edge = width - abs(y)
    body = smooth(edge / 0.10) * smooth((0.94 - abs(x)) / 0.14)
    # Open scoops in the trailing edge make each sheet a liquid shape, not an oval puff.
    for cx, cy, radius in [(-0.44, 0.24, 0.20), (-0.05, -0.36, 0.18), (0.42, 0.29, 0.15)]:
        cx += 0.07 * math.sin(phase)
        hole = math.hypot((x - cx) * 0.8, y - cy)
        body *= smooth((hole - radius) / 0.045)
    depth = 0.48 + 0.24 * smooth((y + width) / max(0.01, 2 * width))
    # Broken curved foam ridges; broad surface highlights rather than fine parallel wires.
    crest = math.exp(-((y - width * 0.58) / 0.045) ** 2)
    inner = math.exp(-((y + width * 0.12 + 0.05 * math.sin(x * 5)) / 0.027) ** 2)
    breaks = smooth((math.sin(x * 13 + phase) + 0.35) / 0.5)
    return body * depth, body * (crest * 0.90 + inner * breaks * 0.48)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for variant in range(3):
        body, foam = Image.new('RGBA', (SIZE, SIZE)), Image.new('RGBA', (SIZE, SIZE))
        bp, fp = body.load(), foam.load()
        for y in range(SIZE):
            for x in range(SIZE):
                a, b = sheet(x / (SIZE - 1), y / (SIZE - 1), variant)
                bp[x, y] = (255, 255, 255, round(max(0, min(1, a)) * 255))
                fp[x, y] = (255, 255, 255, round(max(0, min(1, b)) * 255))
        body.save(OUT / f'Wake{variant}.png')
        foam.save(OUT / f'Foam{variant}.png')
    drop = Image.new('RGBA', (SIZE, SIZE))
    pixels = drop.load()
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / (SIZE - 1), y / (SIZE - 1)
            radius = math.hypot((u - 0.5) / 0.39, (v - 0.5) / (0.18 + u * 0.20))
            alpha = smooth((1 - radius) / 0.15)
            pixels[x, y] = (255, 255, 255, round(alpha * 255))
    drop.save(OUT / 'Droplet.png')
    print(f'Generated 7 water sprites in {OUT}')


if __name__ == '__main__':
    main()
