#!/usr/bin/env python3
"""Suit pips for the Anchor organ's card marks and clap teleport. Requires Pillow.

Run from any directory: python3 make_anchor_textures.py

SuitSpade.png, SuitHeart.png, SuitClub.png  white, the suit in the alpha. The draw call colours
them: red for the heart, near-black for the other two. One suit per mark slot, so two marks can
be told apart when picking for Double Clap.

These are the formulas the Clap teleport sketch was judged on
(Tools/VfxLab/web/sketches/anchor-clap-teleport.js had them as lab/suit-* generators): the same
64 pixels, the same four samples per pixel, the same sample positions. The puff and the soft
disc the effect also uses are the Six Paths ones (RimArt/SixPaths/Puff, SoftDisc); the mod
ships them already, so there is no second copy here.
"""
from pathlib import Path
import math

from PIL import Image

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Anchor"
SIZE = 64
# Four samples per pixel, a quarter of a pixel either side of the lab's sample point.
SUB = [(-0.25, -0.25), (0.25, -0.25), (-0.25, 0.25), (0.25, 0.25)]


# Shapes take x, y from -1.2 to 1.2 with y up.
def heart(x, y):
    X, Y = x * 1.15, y * 1.15 + 0.2
    return (X * X + Y * Y - 1) ** 3 - X * X * Y ** 3 < 0


def stem(x, y):
    return -1.05 < y < -0.35 and abs(x) < 0.08 + (-0.35 - y) * 0.38


def spade(x, y):
    # The heart upside down, a little smaller and lower so its point stays inside the image.
    return heart(x * 1.1, (-y + 0.05) * 1.1) or stem(x, y)


def club(x, y):
    lobes = any(math.hypot(x - cx, y - cy) < 0.43 for cx, cy in ((0, 0.48), (-0.47, -0.12), (0.47, -0.12)))
    return lobes or stem(x, y) or (abs(x) < 0.2 and abs(y) < 0.4)


def raster(inside):
    image = Image.new("RGBA", (SIZE, SIZE))
    pixels = image.load()
    for py in range(SIZE):
        for px in range(SIZE):
            u, v = px / SIZE, py / SIZE
            a = sum(0.25 for du, dv in SUB
                    if inside(((u + du / SIZE) - 0.5) * 2.4, (0.5 - (v + dv / SIZE)) * 2.4))
            # The lab writes a canvas byte as value * 255, truncated.
            pixels[px, py] = (255, 255, 255, int(a * 255))
    return image


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, inside in (("SuitSpade", spade), ("SuitHeart", heart), ("SuitClub", club)):
        image = raster(inside)
        image.save(OUT / f"{name}.png")
        edge = max(image.getpixel(p)[3] for i in range(SIZE) for p in ((i, 0), (i, SIZE - 1), (0, i), (SIZE - 1, i)))
        print(f"wrote {OUT / (name + '.png')}; largest alpha on the outer pixels: {edge}")


if __name__ == "__main__":
    main()
