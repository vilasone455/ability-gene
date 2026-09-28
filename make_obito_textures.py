#!/usr/bin/env python3
"""Obito's ability icons, placeholders drawn from the pictures' own shapes. Requires Pillow.

Run from any directory: python3 make_obito_textures.py

IconPhase.png    the Kamui swirl (dark arms, pale leading lines) over a see-through figure
IconWarp.png     the swirl with a figure winding into it
IconStore.png    the swirl with a white hand reaching into it
IconRelease.png  the swirl with a figure coming out of it
IconWood.png     three bark branches racing out with pale points

The swirl is lib/obito.js swirl(): 4 arms from 8 % to the rim, 0.85 turns, the Kamui dimension's void
(10, 16, 22) and edge line (196, 222, 244) colours.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Obito"
SIZE = 128
SS = 4  # drawn at 4x and scaled down for smooth edges
VOID = (10, 16, 22)
EDGE = (196, 222, 244)
BARK, LIT, DARK, HEART = (92, 64, 40), (146, 108, 68), (50, 34, 22), (226, 202, 150)
ZETSU = (232, 229, 218)
CLOAK = (30, 27, 36)


def canvas():
    return Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))


def finish(image, name):
    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((SIZE, SIZE), Image.LANCZOS).save(OUT / name)


def swirl(draw, cx, cy, radius, arms=4, alpha=255, spin=0.0):
    s = SS
    for i in range(arms):
        phase = spin + i * 2 * math.pi / arms
        dark, edge = [], []
        for j in range(19):
            u = j / 18
            r = radius * (0.08 + 0.92 * u ** 1.15)
            a = phase + 0.85 * 2 * math.pi * u
            dark.append((cx + math.cos(a) * r, cy - math.sin(a) * r))
            al, rl = a - 0.2 - 0.1 * u, r * 1.03
            edge.append((cx + math.cos(al) * rl, cy - math.sin(al) * rl))
        for k in range(len(dark) - 1):
            w = max(1, int(math.sin((k + 0.5) / 18 * math.pi) * radius * 0.16 * s))
            draw.line([(p[0] * s, p[1] * s) for p in dark[k:k + 2]], fill=VOID + (int(alpha * 0.9),), width=w)
        for k in range(len(edge) - 1):
            w = max(1, int(math.sin((k + 0.5) / 18 * math.pi) * max(1.2, radius * 0.07) * s))
            draw.line([(p[0] * s, p[1] * s) for p in edge[k:k + 2]], fill=EDGE + (alpha,), width=w)
    cr = radius * 0.13
    draw.ellipse([(cx - cr) * s, (cy - cr) * s, (cx + cr) * s, (cy + cr) * s], fill=VOID + (255,))


def figure(draw, cx, cy, scale, alpha):
    s = SS
    draw.ellipse([(cx - 14 * scale) * s, (cy - 4 * scale) * s, (cx + 14 * scale) * s, (cy + 26 * scale) * s], fill=CLOAK + (alpha,))
    draw.ellipse([(cx - 10 * scale) * s, (cy - 26 * scale) * s, (cx + 10 * scale) * s, (cy - 6 * scale) * s], fill=(236, 128, 36, alpha))
    draw.ellipse([(cx - 6 * scale) * s, (cy - 19 * scale) * s, (cx - 1 * scale) * s, (cy - 14 * scale) * s], fill=(222, 30, 24, alpha))


def phase():
    image = canvas()
    draw = ImageDraw.Draw(image)
    figure(draw, 64, 70, 1.6, 120)
    swirl(draw, 50, 52, 30, arms=3)
    finish(image, "IconPhase.png")


def warp():
    image = canvas()
    draw = ImageDraw.Draw(image)
    swirl(draw, 64, 64, 54)
    figure(draw, 92, 94, 0.7, 200)
    finish(image, "IconWarp.png")


def store():
    image = canvas()
    draw = ImageDraw.Draw(image)
    swirl(draw, 74, 56, 46)
    s = SS
    draw.line([(10 * s, 118 * s), (58 * s, 70 * s)], fill=(176, 172, 160, 255), width=15 * s)
    draw.line([(10 * s, 118 * s), (58 * s, 70 * s)], fill=ZETSU + (255,), width=11 * s)
    draw.ellipse([(52) * s, (62) * s, (68) * s, (78) * s], fill=ZETSU + (255,))
    finish(image, "IconStore.png")


def release():
    image = canvas()
    draw = ImageDraw.Draw(image)
    swirl(draw, 50, 70, 42, spin=1.0)
    figure(draw, 84, 56, 1.1, 255)
    finish(image, "IconRelease.png")


def wood():
    image = canvas()
    draw = ImageDraw.Draw(image)
    s = SS
    for k, (dy, w) in enumerate([(0, 13), (-14, 7), (14, 7)]):
        pts = []
        for i in range(21):
            x = 10 + i * 5.4
            y = 64 + dy * (0.3 + 0.7 * i / 20) + 5 * math.sin(i * 0.6 + k * 1.7)
            pts.append((x, y))
        for i in range(len(pts) - 1):
            width = max(2, int(w * (1 - 0.45 * i / 20) * min(1, (20 - i) / 4) * s))
            colour = HEART if i >= 17 else BARK
            draw.line([(pts[i][0] * s, pts[i][1] * s), (pts[i + 1][0] * s, pts[i + 1][1] * s)], fill=DARK + (255,), width=width + 2 * s)
            draw.line([(pts[i][0] * s, pts[i][1] * s), (pts[i + 1][0] * s, pts[i + 1][1] * s)], fill=colour + (255,), width=width)
        draw.line([(pts[2][0] * s, (pts[2][1] - 2) * s), (pts[15][0] * s, (pts[15][1] - 2) * s)], fill=LIT + (255,), width=max(1, w // 3) * s)
    finish(image, "IconWood.png")


if __name__ == "__main__":
    phase()
    warp()
    store()
    release()
    wood()
    print("wrote", ", ".join(sorted(p.name for p in OUT.glob("Icon*.png"))))
