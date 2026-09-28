#!/usr/bin/env python3
"""The two round textures Judgement Cut's ball of cut space is drawn from. Requires Pillow.

Run from any directory: python3 make_vergil_textures.py

Ball.png   white, opaque to 80 % of the radius then soft to nothing at the rim: the dark inside
Shell.png  white, clear in the middle, alpha rising as radius^3.2 toward the rim and soft over
           the last 7 %: the blue shell that brightens toward its edge
Yamato.png the katana, bare, hilt at the lower left and point at the upper right as vanilla melee
           weapons are drawn: the gear tab and inspect icon (the game never draws it in the hand;
           Source/RimArt/Vergil/Kit/YamatoDraw.cs does)
Icon*.png  the four ability icons, placeholders drawn from the pictures' own shapes

Both are the formulas of the VFX lab's "lab/vergil-ball" and "lab/vergil-shell" stand-ins
(Tools/VfxLab/web/sketches/lib/vergil.js), which the Judgement Cut sketch was tuned with, so
the game draws the pixels the sketch was judged on. A flat disc and a ring cannot make this
shape: the ball needs a soft edge and the shell needs a falloff that is clear at the centre.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Vergil"
SIZE = 256


def ease(a, b, x):
    """The lab's smoothstep, clamped."""
    v = min(1.0, max(0.0, (x - a) / (b - a)))
    return v * v * (3 - 2 * v)


def write(name, alpha_at):
    image = Image.new("RGBA", (SIZE, SIZE))
    pixels = image.load()
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = (x + 0.5) / SIZE, (y + 0.5) / SIZE
            r = math.hypot(u - 0.5, v - 0.5) * 2
            pixels[x, y] = (255, 255, 255, max(0, min(255, round(alpha_at(r) * 255))))
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / name)
    print(f"{OUT / name}  {SIZE}x{SIZE}")


write("Ball.png", lambda r: 1 - ease(0.8, 1, r))
write("Shell.png", lambda r: 0 if r >= 1 else pow(r, 3.2) * (1 - ease(0.93, 1, r)))


# ---- the katana and the ability icons ---------------------------------------------------------------
# Drawn at 4x and scaled down, so the edges are smooth. Colours are lib/vergil.js's.
STEEL, WHITE, GOLD, GRIP, WRAP = (230, 242, 255, 255), (255, 255, 255, 255), (217, 178, 76, 255), (12, 12, 20, 255), (140, 153, 190, 255)
BLUE, ICE, DEEP, VOID = (64, 128, 255), (199, 230, 255), (13, 31, 128), (3, 4, 18)


def canvas(size):
    return Image.new("RGBA", (size * 4, size * 4), (0, 0, 0, 0))


def save(image, size, name):
    image = image.resize((size, size), Image.LANCZOS)
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / name)
    print(f"{OUT / name}  {size}x{size}")


def along(a, b, u, side=0.0):
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    nx, ny = -dy / length, dx / length
    return (a[0] + dx * u + nx * side, a[1] + dy * u + ny * side)


def katana(draw, hilt, tip, scale, glow=False):
    """A slightly curved katana from the pommel at hilt to the point at tip."""
    guard_u = 0.24
    guard = along(hilt, tip, guard_u)
    blade = []
    for i in range(21):
        u = guard_u + (1 - guard_u) * i / 20
        bow = math.sin((u - guard_u) / (1 - guard_u) * math.pi) * 0.03 * scale * 100
        width = 0.028 * scale * 100 * (1 if u < 0.9 else max(0.05, (1 - u) / 0.1))
        blade.append((along(hilt, tip, u, bow + width / 2), along(hilt, tip, u, bow - width / 2)))
    outline = [b[0] for b in blade] + [b[1] for b in reversed(blade)]
    if glow:
        draw.polygon(outline, fill=ICE + (255,))
    draw.polygon(outline, fill=STEEL)
    edge = [along(hilt, tip, guard_u + (1 - guard_u) * i / 20, math.sin(i / 20 * math.pi) * 0.03 * scale * 100 - 0.008 * scale * 100) for i in range(21)]
    draw.line(edge, fill=WHITE, width=max(1, int(0.008 * scale * 100)))
    grip_w = 0.034 * scale * 100
    draw.line([hilt, guard], fill=GRIP, width=int(grip_w))
    for k in range(5):
        at = along(hilt, guard, 0.12 + 0.17 * k)
        a, b = along(hilt, guard, 0.12 + 0.17 * k, grip_w / 2), along(hilt, guard, 0.12 + 0.17 * k, -grip_w / 2)
        draw.line([a, b], fill=WRAP, width=max(1, int(grip_w * 0.3)))
    ga, gb = along(hilt, tip, guard_u, 0.05 * scale * 100), along(hilt, tip, guard_u, -0.05 * scale * 100)
    draw.line([ga, gb], fill=GOLD, width=int(0.022 * scale * 100))
    r = 0.02 * scale * 100
    draw.ellipse([hilt[0] - r, hilt[1] - r, hilt[0] + r, hilt[1] + r], fill=GOLD)


def yamato():
    size = 128
    image = canvas(size)
    draw = ImageDraw.Draw(image)
    s = size * 4
    katana(draw, (s * 0.14, s * 0.86), (s * 0.9, s * 0.1), s / 100)
    save(image, size, "Yamato.png")


def glowing(image, radius):
    halo = image.filter(ImageFilter.GaussianBlur(radius))
    return Image.alpha_composite(halo, image)


def icon_judgement_cut():
    size, image = 128, canvas(128)
    s = size * 4
    draw = ImageDraw.Draw(image)
    c, r = s / 2, s * 0.36
    draw.ellipse([c - r * 1.15, c - r * 1.15, c + r * 1.15, c + r * 1.15], fill=BLUE + (70,))
    draw.ellipse([c - r, c - r, c + r, c + r], fill=VOID + (255,), outline=ICE + (255,), width=int(s * 0.02))
    for k in range(9):
        angle = k * 67 * math.pi / 180
        off = (k % 3 - 1) * r * 0.3
        dx, dy = math.cos(angle), math.sin(angle)
        a = (c - dx * r * 1.1 - dy * off, c - dy * r * 1.1 + dx * off)
        b = (c + dx * r * 1.1 - dy * off, c + dy * r * 1.1 + dx * off)
        draw.line([a, b], fill=WHITE, width=int(s * 0.012))
    save(glowing(image, 6), size, "IconJudgementCut.png")


def icon_yamato_dash():
    size, image = 128, canvas(128)
    s = size * 4
    draw = ImageDraw.Draw(image)
    y = s * 0.55
    for k, (x0, x1, dy) in enumerate([(0.08, 0.5, -0.16), (0.18, 0.62, 0.14), (0.05, 0.4, 0.26), (0.3, 0.7, -0.27)]):
        draw.line([(s * x0, y + s * dy), (s * x1, y + s * dy)], fill=ICE + (150,), width=int(s * 0.015))
    draw.line([(s * 0.04, y), (s * 0.96, y)], fill=BLUE + (255,), width=int(s * 0.05))
    draw.line([(s * 0.04, y), (s * 0.96, y)], fill=WHITE, width=int(s * 0.016))
    for k, x in enumerate([0.22, 0.44, 0.66]):
        cx, cy = s * x, s * 0.4
        draw.ellipse([cx - s * 0.06, cy - s * 0.12, cx + s * 0.06, cy + s * 0.14], fill=BLUE + (60 + 50 * k,))
        draw.ellipse([cx - s * 0.045, cy - s * 0.22, cx + s * 0.045, cy - s * 0.12], fill=ICE + (60 + 50 * k,))
    katana(draw, (s * 0.62, s * 0.62), (s * 0.98, s * 0.5), s / 190, glow=True)
    save(glowing(image, 5), size, "IconYamatoDash.png")


def icon_summoned_swords():
    size, image = 128, canvas(128)
    s = size * 4
    draw = ImageDraw.Draw(image)
    c = s / 2
    draw.ellipse([c - s * 0.4, c - s * 0.4, c + s * 0.4, c + s * 0.4], outline=BLUE + (160,), width=int(s * 0.012))
    for k in range(8):
        angle = k * math.pi / 4 + 0.2
        dx, dy = math.cos(angle), math.sin(angle)
        root, point = (c + dx * s * 0.16, c + dy * s * 0.16), (c + dx * s * 0.47, c + dy * s * 0.47)
        w = s * 0.035
        blade = [along(root, point, 0, w), along(root, point, 0.7, w), point, along(root, point, 0.7, -w), along(root, point, 0, -w)]
        draw.polygon(blade, fill=ICE + (235,))
        draw.line([root, point], fill=WHITE, width=int(s * 0.01))
        draw.line([along(root, point, 0, s * 0.05), along(root, point, 0, -s * 0.05)], fill=ICE + (255,), width=int(s * 0.016))
    save(glowing(image, 7), size, "IconSummonedSwords.png")


def icon_judgement_cut_end():
    size, image = 128, canvas(128)
    s = size * 4
    draw = ImageDraw.Draw(image)
    c, r = s / 2, s * 0.46
    draw.ellipse([c - r, c - r, c + r, c + r], fill=DEEP + (200,), outline=ICE + (220,), width=int(s * 0.012))
    for k in range(14):
        angle = (k * 137 + 20) * math.pi / 180
        off = ((k * 37) % 11 / 11 - 0.5) * r * 1.2
        dx, dy = math.cos(angle), math.sin(angle)
        half = math.sqrt(max(0.0, r * r - off * off))
        a = (c - dx * half - dy * off, c - dy * half + dx * off)
        b = (c + dx * half - dy * off, c + dy * half + dx * off)
        draw.line([a, b], fill=WHITE, width=int(s * 0.01))
    save(glowing(image, 6), size, "IconJudgementCutEnd.png")


yamato()
icon_judgement_cut()
icon_yamato_dash()
icon_summoned_swords()
icon_judgement_cut_end()
