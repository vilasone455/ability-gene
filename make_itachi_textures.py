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
SusanooFlame.png   128 px. One flame tongue, pointing up: a round base narrowing to a pointed tip
                   that curls a little to the right, its edge broken by noise, brightest at the
                   base. Drawn stretched and swaying for the edge flames, the aura and the wisps.

Kit icons and flecks (added with the mechanic port, 2026-09-27), also white in the alpha:
IconFalseFace.png  128 px. A Sharingan: a ring with three tomoe round a pupil.
IconSusanoo.png    128 px. The Yata Mirror's face over a raised blade.
IconTotsuka.png    128 px. The Totsuka Blade upright, its gourd hilt at the bottom.
Glint.png          64 px. A soft disc with a bright core, for the glints and the False Face mark.
Flash.png          64 px. A wider soft disc, for the Mirror block and the seal.
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


def _hash(i, k, seed):
    n = math.sin(i * 127.1 + k * 311.7 + seed * 74.7) * 43758.5453
    return n - math.floor(n)


def _noise(x, y, seed):
    xi, yi = math.floor(x), math.floor(y)
    fx, fy = x - xi, y - yi
    sx, sy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = _hash(xi, yi, seed), _hash(xi + 1, yi, seed)
    c, d = _hash(xi, yi + 1, seed), _hash(xi + 1, yi + 1, seed)
    return (a + (b - a) * sx) + ((c + (d - c) * sx) - (a + (b - a) * sx)) * sy


def _fbm(x, y, seed, octaves=3):
    total, amp, norm = 0.0, 1.0, 0.0
    for o in range(octaves):
        total += _noise(x * 2 ** o, y * 2 ** o, seed + o) * amp
        norm += amp
        amp *= 0.5
    return total / norm


def flame(size=128):
    """A flame tongue as alpha: v = 0 is the top row. b is height from the bottom."""
    image = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    px = image.load()
    for y in range(size):
        for x in range(size):
            u, v = (x + 0.5) / size, (y + 0.5) / size
            b = (0.95 - v) / 0.9                      # 0 at the base, 1 at the tip
            if b < 0 or b > 1:
                continue
            half = 0.36 * math.sin(math.pi * b ** 0.5) ** 0.8
            centre = 0.5 + 0.07 * math.sin(b * math.pi * 1.3) * b
            edge = half * (0.8 + 0.4 * _fbm(u * 5, v * 3, 17))
            d = abs(u - centre) / max(edge, 1e-4)
            inside = max(0.0, min(1.0, (1 - d) / 0.35))
            base = min(1.0, b / 0.12)
            body = inside * base * (1.0 - 0.45 * b) * (0.8 + 0.2 * _fbm(u * 7, v * 5 - 3, 23))
            px[x, y] = (255, 255, 255, int(max(0.0, min(1.0, body)) * 255))
    # Clear border so the quad's edge never shows.
    for i in range(size):
        for j in (0, size - 1):
            px[i, j] = px[j, i] = (255, 255, 255, 0)
    return image


def icon_false_face(draw, S):
    c = S * 0.5
    r = S * 0.38
    draw.ellipse([c - r, c - r, c + r, c + r], outline=255, width=int(S * 0.045))
    p = S * 0.075
    draw.ellipse([c - p, c - p, c + p, c + p], fill=255)
    for k in range(3):
        a0 = k * math.tau / 3 - math.pi / 2
        head = (c + math.cos(a0) * S * 0.24, c + math.sin(a0) * S * 0.24)
        rr = S * 0.062
        draw.ellipse([head[0] - rr, head[1] - rr, head[0] + rr, head[1] + rr], fill=255)
        tail = []
        for i in range(22):
            u = i / 21
            a = a0 + u * 1.1
            r2 = S * (0.24 - 0.02 * u)
            tail.append((c + math.cos(a) * r2, c + math.sin(a) * r2))
        stroke(draw, tail, [S * (0.06 - 0.05 * i / 21) for i in range(22)], 255)


def icon_totsuka(draw, S):
    # A straight blade rising from a round gourd, with a short guard.
    w = int(S * 0.05)
    draw.line([(S * 0.5, S * 0.10), (S * 0.5, S * 0.66)], fill=255, width=int(S * 0.075))
    draw.polygon([(S * 0.46, S * 0.13), (S * 0.5, S * 0.05), (S * 0.54, S * 0.13)], fill=255)
    draw.line([(S * 0.36, S * 0.66), (S * 0.64, S * 0.66)], fill=255, width=w)
    r = S * 0.13
    draw.ellipse([S * 0.5 - r, S * 0.88 - r, S * 0.5 + r, S * 0.88 + r], outline=255, width=w)
    r2 = S * 0.08
    draw.ellipse([S * 0.5 - r2, S * 0.72 - r2, S * 0.5 + r2, S * 0.72 + r2], outline=255, width=w)


def icon_susanoo(draw, S):
    # The mirror, smaller and to the left, with the blade rising behind it on the right.
    c = (S * 0.40, S * 0.56)
    r = S * 0.30
    draw.ellipse([c[0] - r, c[1] - r, c[0] + r, c[1] + r], outline=255, width=int(S * 0.045))
    pts, us = spiral_points(c[0], c[1], S * 0.19, 1.8, math.pi * 0.5, n=140)
    stroke(draw, pts, [S * (0.04 - 0.025 * u) for u in us], 255)
    draw.line([(S * 0.76, S * 0.12), (S * 0.76, S * 0.80)], fill=255, width=int(S * 0.07))
    draw.polygon([(S * 0.72, S * 0.15), (S * 0.76, S * 0.06), (S * 0.80, S * 0.15)], fill=255)
    draw.line([(S * 0.66, S * 0.80), (S * 0.86, S * 0.80)], fill=255, width=int(S * 0.045))


def soft_disc(size, core, edge):
    """A radial falloff: alpha 1 inside core (share of the radius), 0 at edge."""
    image = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    px = image.load()
    for y in range(size):
        for x in range(size):
            d = math.hypot((x + 0.5) / size - 0.5, (y + 0.5) / size - 0.5) / 0.5
            if d >= edge:
                a = 0.0
            elif d <= core:
                a = 1.0
            else:
                u = (d - core) / (edge - core)
                a = 1 - u * u * (3 - 2 * u)
            px[x, y] = (255, 255, 255, int(a * 255))
    return image


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, paint in (("IconFalseFace.png", icon_false_face), ("IconSusanoo.png", icon_susanoo),
                        ("IconTotsuka.png", icon_totsuka)):
        line_art(128, paint, glow=0.25).save(OUT / name)
        print("wrote", OUT / name)
    soft_disc(64, 0.12, 0.95).save(OUT / "Glint.png")
    soft_disc(64, 0.05, 0.98).save(OUT / "Flash.png")
    print("wrote Glint.png and Flash.png")
    for name, size, paint in (("SusanooSwirl.png", 256, swirl), ("SusanooMirror.png", 256, mirror),
                              ("SusanooCurl.png", 128, curl)):
        line_art(size, paint).save(OUT / name)
        print("wrote", OUT / name)
    flame().save(OUT / "SusanooFlame.png")
    print("wrote", OUT / "SusanooFlame.png")


if __name__ == "__main__":
    main()
