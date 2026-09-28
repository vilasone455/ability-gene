#!/usr/bin/env python3
"""Sasuke's Rinnegan kit textures. Requires Pillow.

Run from any directory: python3 make_sasuke_textures.py

Icon*.png   the four ability icons and the Let go and Release buttons, 128 px placeholders drawn from
            the sketches' own shapes and colours (Tools/VfxLab/web/sketches/rinnegan-*.js)
BlackBlot.png, BlackShred.png
            Amaterasu's blotches and torn flecks, 128 px, white with the shape in the alpha: the same
            formulas and pixels as the lab's lab/black-blot and lab/black-shred (rinnegan-amaterasu.js,
            noise from Tools/VfxLab/web/js/standins.js)
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Rinnegan"
SIZE = 128
S = 4  # drawn at 4x, scaled down for smooth edges

LAVENDER, DEEP, PUPIL = (204, 189, 250, 255), (117, 92, 199, 255), (30, 22, 48, 255)
STEEL, STEEL_DARK, RING = (176, 186, 204, 255), (92, 100, 118, 255), (60, 64, 76, 255)
BOLT, BOLT_CORE = (40, 40, 60, 255), (240, 244, 255, 255)
BLACK, VIOLET = (8, 6, 12, 255), (150, 110, 230, 255)


def canvas():
    return Image.new("RGBA", (SIZE * S, SIZE * S), (0, 0, 0, 0))


def save(image, name):
    OUT.mkdir(parents=True, exist_ok=True)
    image = image.resize((SIZE, SIZE), Image.LANCZOS)
    image.save(OUT / name)
    print(f"{OUT / name}  {SIZE}x{SIZE}")


def p(x, y):
    """Icon coordinates 0..1 to pixels."""
    return (x * SIZE * S, y * SIZE * S)


def rinnegan(draw, cx, cy, r, tomoe=False):
    """The ripple eye: lavender iris, 4 dark rings, a small pupil; tomoe on rings 1 and 2 if asked."""
    draw.ellipse([*p(cx - r, cy - r), *p(cx + r, cy + r)], fill=LAVENDER)
    for i in range(4):
        rr = r * (0.2 + 0.2 * i)
        draw.ellipse([*p(cx - rr, cy - rr), *p(cx + rr, cy + rr)], outline=DEEP, width=int(S * 3))
    pr = r * 0.12
    draw.ellipse([*p(cx - pr, cy - pr), *p(cx + pr, cy + pr)], fill=PUPIL)
    if tomoe:
        for ring, start in ((0.4, 0), (0.6, 60)):
            for k in range(3):
                a = math.radians(start + k * 120)
                tx, ty = cx + math.cos(a) * r * ring, cy + math.sin(a) * r * ring
                tr = r * 0.075
                draw.ellipse([*p(tx - tr, ty - tr), *p(tx + tr, ty + tr)], fill=PUPIL)


def kunai(draw, x0, y0, x1, y1, width=0.07):
    """A kunai from its ring (x0, y0) to its point (x1, y1)."""
    dx, dy = x1 - x0, y1 - y0
    length = math.hypot(dx, dy)
    ux, uy = dx / length, dy / length
    nx, ny = -uy, ux
    handle = 0.36
    hx, hy = x0 + ux * length * handle, y0 + uy * length * handle
    widest = 0.7
    wx, wy = x0 + dx * (handle + (1 - handle) * widest * 0.35), y0 + dy * (handle + (1 - handle) * widest * 0.35)
    blade = [p(hx + nx * width * 0.35, hy + ny * width * 0.35), p(wx + nx * width, wy + ny * width), p(x1, y1),
             p(wx - nx * width, wy - ny * width), p(hx - nx * width * 0.35, hy - ny * width * 0.35)]
    draw.polygon(blade, fill=STEEL, outline=STEEL_DARK)
    draw.line([p(x0 + ux * 0.06, y0 + uy * 0.06), p(hx, hy)], fill=STEEL_DARK, width=int(S * 5))
    rr = 0.045
    draw.ellipse([*p(x0 - rr, y0 - rr), *p(x0 + rr, y0 + rr)], outline=RING, width=int(S * 3))


def bolt(draw, a, b, seed, kinks=5):
    rnd = (math.sin(seed * 12.9898) * 43758.5453) % 1
    pts = [a]
    for i in range(1, kinks):
        t = i / kinks
        x = a[0] + (b[0] - a[0]) * t
        y = a[1] + (b[1] - a[1]) * t
        off = (((rnd * (i + 3) * 7.13) % 1) - 0.5) * 0.14
        nx, ny = -(b[1] - a[1]), b[0] - a[0]
        n = math.hypot(nx, ny)
        pts.append((x + nx / n * off, y + ny / n * off))
    pts.append(b)
    draw.line([p(*q) for q in pts], fill=BOLT, width=int(S * 7), joint="curve")
    draw.line([p(*q) for q in pts], fill=BOLT_CORE, width=int(S * 3), joint="curve")


def flame(draw, cx, base, height, width, colour):
    """A black flame: three tongues rising from a rounded base."""
    for dx, h, w in ((-0.55, 0.62, 0.5), (0.5, 0.7, 0.5), (0, 1, 0.7)):
        x = cx + dx * width * 0.5
        top = base - height * h
        pts = []
        for i in range(21):
            t = i / 20
            y = base - (base - top) * t
            half = width * 0.5 * w * (1 - t) ** 0.8 * (0.8 + 0.2 * math.sin(t * 9))
            sway = math.sin(t * 3.1 + dx * 2) * width * 0.08 * t
            pts.append((x - half + sway, y))
        for i in range(20, -1, -1):
            t = i / 20
            y = base - (base - top) * t
            half = width * 0.5 * w * (1 - t) ** 0.8 * (0.8 + 0.2 * math.sin(t * 9 + 1))
            sway = math.sin(t * 3.1 + dx * 2) * width * 0.08 * t
            pts.append((x + half + sway, y))
        draw.polygon([p(*q) for q in pts], fill=colour)


def icon_amenoyodomi():
    image = canvas()
    draw = ImageDraw.Draw(image)
    rinnegan(draw, 0.5, 0.36, 0.26)
    for i, x in enumerate((0.26, 0.5, 0.74)):
        kunai(draw, x - 0.12, 0.92, x + 0.02, 0.66)
        draw.ellipse([*p(x - 0.08, 0.95), *p(x + 0.08, 0.99)], outline=LAVENDER, width=int(S * 2))
    save(image, "IconAmenoyodomi.png")


def icon_amenotejikara():
    image = canvas()
    draw = ImageDraw.Draw(image)
    rinnegan(draw, 0.5, 0.5, 0.36, tomoe=True)
    for sx in (-1, 1):
        y = 0.5 + sx * 0.44
        draw.polygon([p(0.5 - 0.2 * sx, y - 0.03 * sx), p(0.5 + 0.2 * sx, y - 0.03 * sx), p(0.5 + 0.2 * sx, y + 0.02 * sx)],
                     fill=LAVENDER)
    save(image, "IconAmenotejikara.png")


def icon_raiko():
    image = canvas()
    draw = ImageDraw.Draw(image)
    corners = [(0.18, 0.3), (0.82, 0.24), (0.7, 0.82), (0.24, 0.76)]
    for i in range(4):
        bolt(draw, corners[i], corners[(i + 1) % 4], i + 1)
    for (x, y) in corners:
        kunai(draw, x - 0.08, y + 0.1, x + 0.05, y - 0.07, 0.05)
    save(image, "IconRaikoKusari.png")


def icon_amaterasu():
    image = canvas()
    draw = ImageDraw.Draw(image)
    glow = canvas()
    flame(ImageDraw.Draw(glow), 0.5, 0.92, 0.8, 0.62, VIOLET)
    glow = glow.filter(ImageFilter.GaussianBlur(S * 6))
    image.alpha_composite(glow)
    flame(draw, 0.5, 0.9, 0.74, 0.56, BLACK)
    save(image, "IconAmaterasu.png")


def icon_let_go():
    image = canvas()
    draw = ImageDraw.Draw(image)
    for i, y in enumerate((0.28, 0.5, 0.72)):
        kunai(draw, 0.14, y, 0.58, y, 0.06)
        for k in range(3):
            x = 0.66 + k * 0.1
            draw.line([p(x, y - 0.05), p(x + 0.05, y), p(x, y + 0.05)], fill=LAVENDER, width=int(S * 4))
    save(image, "IconLetGo.png")


def icon_release():
    image = canvas()
    draw = ImageDraw.Draw(image)
    flame(draw, 0.5, 0.86, 0.6, 0.5, BLACK)
    draw.ellipse([*p(0.14, 0.14), *p(0.86, 0.86)], outline=LAVENDER, width=int(S * 7))
    draw.line([p(0.24, 0.76), p(0.76, 0.24)], fill=LAVENDER, width=int(S * 7))
    save(image, "IconRelease.png")


icon_amenoyodomi()
icon_amenotejikara()
icon_raiko()
icon_amaterasu()
icon_let_go()
icon_release()


# ---- Amaterasu's sprite textures: the lab's generators, pixel for pixel ------------------------------------------
# Tools/VfxLab/web/js/standins.js: hash() is a 32-bit integer hash, noise() tileable value noise, fbm() octaves of it.
# pixels(n, fn) calls fn(x / n, y / n) for every pixel, row 0 at the top, and stores round(v * 255) clamped, as a
# canvas ImageData (Uint8ClampedArray: round half to even, which Python's round() also does).

def _i32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v >= (1 << 31) else v


def lab_hash(x, y, seed):
    h = _i32(x * 374761393 + y * 668265263 + seed * 144665)
    u = h & 0xFFFFFFFF
    h = _i32((u ^ (u >> 13)) * 1274126177)
    u = h & 0xFFFFFFFF
    return (u ^ (u >> 16)) / 4294967295


def lab_noise(x, y, period, seed):
    xi, yi = math.floor(x), math.floor(y)
    xf, yf = x - xi, y - yi

    def s(t):
        return t * t * (3 - 2 * t)

    def at(i, j):
        return lab_hash(((i % period) + period) % period, ((j % period) + period) % period, seed)

    a, b, c, d = at(xi, yi), at(xi + 1, yi), at(xi, yi + 1), at(xi + 1, yi + 1)
    return a + (b - a) * s(xf) + (c - a) * s(yf) + (a - b - c + d) * s(xf) * s(yf)


def lab_fbm(x, y, seed, octaves=4, period=8):
    v, amp, total = 0.0, 0.5, 0.0
    for o in range(octaves):
        v += lab_noise(x * (1 << o), y * (1 << o), period * (1 << o), seed + o) * amp
        total += amp
        amp *= 0.5
    return v / total


def clamp01(v):
    return 0.0 if v < 0 else 1.0 if v > 1 else v


def lab_pixels(n, fn, name):
    image = Image.new("RGBA", (n, n))
    data = []
    for y in range(n):
        for x in range(n):
            data.append(tuple(int(round(min(255.0, max(0.0, c * 255)))) for c in fn(x / n, y / n)))
    image.putdata(data)
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / name)
    print(f"{OUT / name}  {n}x{n}")


def black_shred(u, v):
    """A torn black scrap, taller than wide: two lopsided lumps with a notched edge and a hole."""
    x, y = (u - 0.5) * 2, (0.5 - v) * 2
    low = math.hypot(x / 0.5, (y + 0.2) / 0.62)
    high = math.hypot((x - 0.14) / 0.3, (y - 0.38) / 0.38)
    n = lab_fbm(u * 6, v * 6, 41, 3, 6) - 0.5
    notch = lab_fbm(u * 13, v * 13, 7, 2, 13) - 0.5
    a = clamp01((1 + n * 0.7 + notch * 0.4 - min(low, high)) / 0.08)
    hole = lab_fbm(u * 7, v * 7, 97, 2, 7)
    if hole > 0.7:
        a *= clamp01((0.78 - hole) / 0.08)
    return (1, 1, 1, a * clamp01(min(u, 1 - u, v, 1 - v) / 0.04))


def black_blot(u, v):
    """A ragged round blot with a few specks thrown off its edge."""
    r = math.hypot(u - 0.5, v - 0.5) * 2
    rag = lab_fbm(u * 4, v * 4, 13, 3, 4)
    a = clamp01((0.52 + (rag - 0.5) * 1.1 - r) / 0.07)
    speck = lab_fbm(u * 10, v * 10, 57, 2, 10)
    if 0.5 < r < 0.88 and speck > 0.66:
        a = max(a, clamp01((speck - 0.66) / 0.05))
    return (1, 1, 1, a * clamp01((0.98 - r) / 0.06))


lab_pixels(128, black_blot, "BlackBlot.png")
lab_pixels(128, black_shred, "BlackShred.png")
