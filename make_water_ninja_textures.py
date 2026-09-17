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
    spine = 0.10 * math.sin(x * 2.8 + phase)
    width = max(0, 1 - x * x) ** 0.65 * (0.48 + 0.045 * math.sin(x * 4 + phase))
    y = (v - 0.5) * 2 - spine
    body = smooth((width - abs(y)) / 0.20) * smooth((0.97 - abs(x)) / 0.22)
    depth = 0.46 + 0.22 * math.exp(-((y + 0.07) / 0.24) ** 2)
    crest = math.exp(-((y - width * 0.40) / 0.085) ** 2)
    # Broad, curved highlights without edge cutouts or zigzag frequency.
    breaks = smooth((math.cos(x * 5 + phase) + 0.7) / 1.3)
    inner = math.exp(-((y + width * 0.22) / 0.12) ** 2) * 0.2
    return body * depth, body * (crest * breaks * 0.65 + inner)


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
    for name, highlight in [('Bow', False), ('BowFoam', True)]:
        image = Image.new('RGBA', (SIZE, SIZE))
        px = image.load()
        for y in range(SIZE):
            for x in range(SIZE):
                u, v = x / (SIZE - 1) * 2 - 1, y / (SIZE - 1) * 2 - 1
                # Curved forward face on +x, with long tapered shoulders behind it.
                front = 0.62 - 1.25 * v * v
                thickness = 0.30 * max(0, 1 - v * v)
                edge = front - u
                a = smooth(edge / 0.10) * smooth((thickness - edge) / 0.16)
                a *= smooth((0.92 - abs(v)) / 0.20)
                if highlight:
                    a *= math.exp(-((edge - 0.075) / 0.06) ** 2)
                px[x, y] = (255, 255, 255, round(a * 255))
        image.save(OUT / f'{name}.png')
    print(f'Generated 9 water sprites in {OUT}')


if __name__ == '__main__':
    main()
