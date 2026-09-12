#!/usr/bin/env python3
"""Deterministic, transparent VFX layers for the Shinra preview. Requires Pillow.

Run from any directory: python3 make_shinra_textures.py
The shell projects a hemisphere into map space: screen_z = .68*z + .75*height.
Those constants and the [-1.15, 1.15] extent match ShinraVfxGraphics / ShinraVfxTiming.
This paints new mathematical assets; it does not edit the generated concept image.
"""
from pathlib import Path
import math
import random

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Shinra"


def gaussian(value, width):
    return math.exp(-((value / width) ** 2))


def raster(size, colour, alpha):
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    for py in range(size):
        y = 1.0 - 2.0 * (py + 0.5) / size
        for px in range(size):
            x = 2.0 * (px + 0.5) / size - 1.0
            pixels[px, py] = (*colour, round(255 * max(0.0, min(1.0, alpha(x, y)))))
    return image


def shell_alpha(x, y):
    x, y = x * 1.15, y * 1.15
    depth, lift = 0.68, 0.75
    top = math.hypot(depth, lift)
    silhouette = math.hypot(x, y / (top if y >= 0 else depth)) - 1
    if silhouette > 0.10:
        return 0.0
    # Narrow luminous rim plus a broad, low-opacity halo. Keep the centre readable.
    alpha = 0.72 * gaussian(silhouette, 0.0035)
    alpha += 0.23 * gaussian(silhouette, 0.014)
    alpha += 0.07 * gaussian(silhouette, 0.042)
    if silhouette <= 0:
        view_normal = math.sqrt(max(0.0, 1.0 - x*x - (y/top)**2))
        alpha += 0.012 + 0.055 * (1.0 - view_normal)**3
        # Local highlights following the upper curved shell, not an opaque white fill.
        upper_edge = math.sqrt(max(0, 1.0 - x*x)) * top
        alpha += 0.58 * gaussian(x + 0.35, 0.105) * gaussian(y - upper_edge + 0.042, 0.023)
        alpha += 0.32 * gaussian(x - 0.68, 0.065) * gaussian(y - upper_edge + 0.035, 0.025)
    # Faint contact arc inside the taller shell makes the base readable.
    contact = math.hypot(x, y / depth) - 1
    alpha += (0.14 if y < 0 else 0.035) * gaussian(contact, 0.006)
    return alpha


def make_dust():
    randomizer = random.Random(1701)
    lobes = [(randomizer.uniform(-0.48, 0.48), randomizer.uniform(-0.42, 0.42),
              randomizer.uniform(0.14, 0.34), randomizer.uniform(0.2, 0.5)) for _ in range(18)]

    def alpha(x, y):
        total = sum(strength * math.exp(-((x-cx)**2 + (y-cy)**2) / width**2)
                    for cx, cy, width, strength in lobes)
        detail = 0.84 + 0.08*math.sin(x*39 + math.sin(y*21)) + 0.08*math.sin(y*47+x*17)
        edge = max(0, 1 - (x*x+y*y))**2
        return min(0.9, total) * detail * edge

    return raster(192, (255, 255, 255), alpha)


def make_rock(index):
    image = Image.new("RGBA", (256, 256))
    draw = ImageDraw.Draw(image)
    shapes = [
        [(44, 71), (126, 33), (207, 79), (214, 161), (132, 222), (58, 185)],
        [(33, 110), (82, 40), (169, 51), (220, 131), (173, 211), (65, 194)],
        [(56, 60), (156, 31), (212, 100), (181, 210), (96, 220), (38, 140)],
    ]
    points = shapes[index]
    draw.polygon(points, fill=(71, 69, 64), outline=(44, 43, 40), width=7)
    centre = (127, 121)
    draw.polygon([points[0], points[1], points[2], centre], fill=(132, 129, 120))
    draw.polygon([points[2], points[3], centre], fill=(99, 97, 90))
    draw.polygon([points[0], centre, points[5]], fill=(110, 106, 98))
    draw.line([points[0], points[1], points[2]], fill=(158, 154, 142), width=5)
    return image.resize((64, 64), Image.Resampling.LANCZOS)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    assets = {
        "Shell": raster(1024, (247, 250, 255), shell_alpha),
        "Dust": make_dust(),
        "Glow": raster(128, (255, 255, 255), lambda x, y: math.exp(-(x*x+y*y)*9) * max(0, 1-x*x-y*y)),
        "Shadow": raster(128, (0, 0, 0), lambda x, y: math.exp(-(x*x+y*y)*4) * max(0, 1-x*x-y*y)),
    }
    ribbon = Image.new("RGBA", (256, 64))
    pixels = ribbon.load()
    for y in range(64):
        across = (y + 0.5) / 64 * 2 - 1
        for x in range(256):
            along = (x + 0.5) / 256
            taper = math.sin(along * math.pi)**1.4
            alpha = taper * (0.8*gaussian(across, 0.13) + 0.17*gaussian(across, 0.55))
            pixels[x, y] = (255, 255, 255, round(alpha*255))
    assets["Ribbon"] = ribbon
    for i in range(3):
        assets[f"Rock{i}"] = make_rock(i)
    for name, asset in assets.items():
        path = OUT / f"{name}.png"
        asset.save(path)
        print(path.relative_to(OUT.parent.parent.parent))


if __name__ == "__main__":
    main()
