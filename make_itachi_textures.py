#!/usr/bin/env python3
"""Textures for Itachi's Susanoo line art. Requires Pillow.

Run from any directory: python3 make_itachi_textures.py

All three are white with the shape in the alpha, so the sketch (and later the C#) colours them
with the line colour and draws them additively (MoteGlow) over the flat fill. Each line has a
bright core and a soft glow either side, as the anime's pale line art on red does.

SusanooSwirl.png   256 px. One tapering spiral curl, 2.6 turns, thick at the outer end and thin
                   at the centre. Placed on the shoulder plates, the chest and the cape.
SusanooMirror.png  256 px. The Yata Mirror's face: an outer ring, a large central spiral and three
                   comma curls round it, as the anime's round shield shows.
SusanooCurl.png    128 px. A flame curl: a hook-shaped stroke rising and rolling over at the top,
                   for the arm, cape and armour edges.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Itachi"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges
CLEAR = (0, 0, 0, 0)


def stroke(draw, pts, widths, value):
    """A polyline with a width per point, drawn as round-capped segments into an L image."""
    for (x0, y0), (x1, y1), w0, w1 in zip(pts, pts[1:], widths, widths[1:]):
        w = (w0 + w1) / 2
        draw.line([(x0, y0), (x1, y1)], fill=value, width=max(1, int(round(w))))
        r = w / 2
        draw.ellipse([x1 - r, y1 - r, x1 + r, y1 + r], fill=value)


def line_art(size, paint, core=1.0, glow_radius=0.022, glow=0.45):
    """paint(draw, S, width_scale) draws strokes on an L canvas of S = size * SCALE pixels. The
    result is white RGBA whose alpha is the sharp line plus a blurred glow of it."""
    S = size * SCALE
    sharp = Image.new("L", (S, S), 0)
    paint(ImageDraw.Draw(sharp), S)
    halo = sharp.filter(ImageFilter.GaussianBlur(radius=S * glow_radius))
    alpha = Image.new("L", (S, S), 0)
    px_a, px_s, px_h = alpha.load(), sharp.load(), halo.load()
    for y in range(S):
        for x in range(S):
            a = px_s[x, y] * core + px_h[x, y] * glow * 2.2
            px_a[x, y] = min(255, int(a))
    alpha = alpha.resize((size, size), Image.LANCZOS)
    # Keep the outermost pixels clear so the quad's edge never shows.
    d = ImageDraw.Draw(alpha)
    d.rectangle([0, 0, size - 1, size - 1], outline=0)
    image = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    image.putalpha(alpha)
    return image


def spiral_points(cx, cy, r_out, turns, start_angle, n=220, r_in_share=0.06):
    """An Archimedean spiral from the outer end inward: (x, y) points and 0..1 along it."""
    pts, us = [], []
    for i in range(n + 1):
        u = i / n
        r = r_out * (1 - u * (1 - r_in_share))
        a = start_angle + u * turns * math.tau
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        us.append(u)
    return pts, us


def swirl(draw, S):
    pts, us = spiral_points(S * 0.5, S * 0.5, S * 0.40, 2.6, math.pi * 0.15)
    widths = [S * (0.034 - 0.022 * u) for u in us]
    stroke(draw, pts, widths, 255)


def mirror(draw, S):
    c = S * 0.5
    rr, ww = S * 0.435, S * 0.026
    draw.ellipse([c - rr, c - rr, c + rr, c + rr], outline=255, width=int(ww))
    # Central spiral, most of the face.
    pts, us = spiral_points(c, c, S * 0.28, 2.3, math.pi * 0.5)
    stroke(draw, pts, [S * (0.028 - 0.016 * u) for u in us], 255)
    # Three comma curls between the rings, each a short hooked arc with a round head.
    for k in range(3):
        a0 = k * math.tau / 3 + 0.4
        head = (c + math.cos(a0) * S * 0.37, c + math.sin(a0) * S * 0.37)
        rr = S * 0.030
        draw.ellipse([head[0] - rr, head[1] - rr, head[0] + rr, head[1] + rr], fill=255)
        tail = []
        for i in range(24):
            u = i / 23
            a = a0 + u * 1.05
            r = S * (0.37 + 0.022 * u)
            tail.append((c + math.cos(a) * r, c + math.sin(a) * r))
        stroke(draw, tail, [S * (0.030 - 0.026 * i / 23) for i in range(24)], 255)


def curl(draw, S):
    # A stem rising from the bottom middle and leaning right, then rolling back over to the left
    # at the top and ending inside the roll, as the anime's flame curls do.
    pts, widths = [], []
    n = 90
    for i in range(n):
        u = i / (n - 1)
        if u < 0.5:
            v = u / 0.5
            e = v * v * (3 - 2 * v)
            x, y = 0.46 + 0.09 * e, 0.94 - 0.50 * v
        else:
            v = (u - 0.5) / 0.5
            a = -v * 1.35 * math.pi
            r = 0.18 * (1 - 0.45 * v)
            x, y = 0.37 + math.cos(a) * r + 0.18 * 0.45 * v, 0.44 + math.sin(a) * r
        pts.append((x * S, y * S))
        widths.append(S * (0.016 + 0.036 * math.sin(min(1.0, u * 1.1) * math.pi) + 0.012 * (1 - u)))
    stroke(draw, pts, widths, 255)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, size, paint in (("SusanooSwirl.png", 256, swirl), ("SusanooMirror.png", 256, mirror),
                              ("SusanooCurl.png", 128, curl)):
        line_art(size, paint).save(OUT / name)
        print("wrote", OUT / name)


if __name__ == "__main__":
    main()
