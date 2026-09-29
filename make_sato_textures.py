#!/usr/bin/env python3
"""Satō's textures. Requires Pillow.

Run from any directory: python3 make_sato_textures.py

Blank.png              a clear 4x4: the graphic of things and pawns whose picture is drawn in code
                       (anchors, remains, torn limbs, the Black Ghost; Source/RimArt/Sato)
Icon*.png              the five ability icons and the Black Ghost's three buttons, placeholders
                       drawn from flat shapes: sever (a blade across a hand), headshot reset (a
                       pistol at a head), grenade reset (a grenade), the game (a cap over a
                       crosshair), black ghost (the IBM's wedge head), go here (an arrow), attack
                       (three claw marks), tear (an arm torn at the shoulder)
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Sato"
SIZE = 128
S = SIZE * 4

INK = (22, 22, 26, 255)
GREY = (70, 72, 78, 255)
PALE = (214, 214, 206, 255)
SKIN = (222, 184, 150, 255)
STEEL = (170, 176, 184, 255)
RED = (190, 40, 36, 255)
CAP = (46, 57, 60, 255)
OLIVE = (74, 82, 52, 255)


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def save(image, name, size=SIZE):
    OUT.mkdir(parents=True, exist_ok=True)
    image = image.resize((size, size), Image.LANCZOS)
    image.save(OUT / name)
    print(f"{OUT / name}  {size}x{size}")


def p(x, y):
    """A point from 0..1 fractions of the canvas."""
    return (x * S, y * S)


def blank():
    OUT.mkdir(parents=True, exist_ok=True)
    Image.new("RGBA", (4, 4), (0, 0, 0, 0)).save(OUT / "Blank.png")
    print(f"{OUT / 'Blank.png'}  4x4")


def hand(draw, cx, cy, r, colour):
    draw.ellipse([p(cx - r, cy - r * 0.8), p(cx + r, cy + r * 0.8)], fill=colour)
    for k in range(4):
        x = cx - r * 0.75 + k * r * 0.5
        draw.rounded_rectangle([p(x - r * 0.18, cy - r * 1.9), p(x + r * 0.18, cy - r * 0.4)], radius=r * S * 0.18, fill=colour)
    draw.rounded_rectangle([p(cx + r * 0.7, cy - r * 0.9), p(cx + r * 1.5, cy - r * 0.55)], radius=r * S * 0.15, fill=colour)


def icon_sever():
    image = canvas()
    draw = ImageDraw.Draw(image)
    hand(draw, 0.42, 0.62, 0.17, SKIN)
    draw.rectangle([p(0.30, 0.74), p(0.54, 0.95)], fill=SKIN)
    draw.polygon([p(0.12, 0.80), p(0.90, 0.46), p(0.93, 0.52), p(0.16, 0.86)], fill=STEEL)
    draw.polygon([p(0.28, 0.73), p(0.60, 0.60), p(0.62, 0.66), p(0.30, 0.79)], fill=RED)
    save(image, "IconSever.png")


def icon_headshot():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.ellipse([p(0.20, 0.22), p(0.66, 0.72)], fill=PALE)
    draw.rounded_rectangle([p(0.18, 0.16), p(0.68, 0.34)], radius=0.06 * S, fill=CAP)
    draw.rectangle([p(0.62, 0.40), p(0.92, 0.50)], fill=INK)
    draw.polygon([p(0.80, 0.50), p(0.90, 0.50), p(0.94, 0.72), p(0.84, 0.72)], fill=INK)
    for k in range(5):
        a = math.radians(170 + k * 12)
        draw.line([p(0.22, 0.46), p(0.22 + math.cos(a) * 0.2, 0.46 + math.sin(a) * 0.2)], fill=RED, width=int(0.03 * S))
    save(image, "IconHeadshotReset.png")


def icon_grenade():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.ellipse([p(0.24, 0.30), p(0.72, 0.86)], fill=OLIVE)
    for k in range(3):
        y = 0.44 + k * 0.13
        draw.line([p(0.27, y), p(0.69, y)], fill=INK, width=int(0.02 * S))
    draw.rectangle([p(0.40, 0.20), p(0.56, 0.32)], fill=STEEL)
    draw.polygon([p(0.54, 0.22), p(0.80, 0.30), p(0.78, 0.36), p(0.54, 0.30)], fill=STEEL)
    draw.ellipse([p(0.22, 0.10), p(0.38, 0.26)], outline=STEEL, width=int(0.025 * S))
    save(image, "IconGrenadeReset.png")


def icon_game():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.ellipse([p(0.16, 0.26), p(0.84, 0.94)], outline=RED, width=int(0.04 * S))
    draw.line([p(0.50, 0.22), p(0.50, 0.98)], fill=RED, width=int(0.03 * S))
    draw.line([p(0.12, 0.60), p(0.88, 0.60)], fill=RED, width=int(0.03 * S))
    draw.chord([p(0.24, 0.02), p(0.76, 0.40)], 180, 360, fill=CAP)
    draw.polygon([p(0.24, 0.21), p(0.86, 0.21), p(0.80, 0.28), p(0.24, 0.28)], fill=CAP)
    save(image, "IconTheGame.png")


def ghost_head(draw, open_jaw=True):
    draw.polygon([p(0.14, 0.40), p(0.62, 0.18), p(0.92, 0.36), p(0.60, 0.52)], fill=INK)
    if open_jaw:
        draw.polygon([p(0.20, 0.50), p(0.60, 0.56), p(0.86, 0.66), p(0.52, 0.70)], fill=INK)
        for k in range(6):
            x = 0.40 + k * 0.08
            draw.polygon([p(x, 0.50), p(x + 0.03, 0.57), p(x + 0.06, 0.51)], fill=PALE)
        draw.line([p(0.86, 0.60), p(0.97, 0.56), p(0.99, 0.50)], fill=RED, width=int(0.015 * S))
        draw.line([p(0.97, 0.56), p(0.99, 0.62)], fill=RED, width=int(0.015 * S))


def icon_ghost():
    image = canvas()
    draw = ImageDraw.Draw(image)
    ghost_head(draw)
    draw.rounded_rectangle([p(0.18, 0.66), p(0.58, 0.98)], radius=0.08 * S, fill=INK)
    for k in range(4):
        y = 0.72 + k * 0.07
        draw.line([p(0.18, y), p(0.58, y + 0.02)], fill=GREY, width=int(0.02 * S))
    save(image, "IconBlackGhost.png")


def icon_go():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.polygon([p(0.12, 0.42), p(0.58, 0.42), p(0.58, 0.24), p(0.90, 0.50), p(0.58, 0.76), p(0.58, 0.58), p(0.12, 0.58)], fill=INK)
    draw.ellipse([p(0.06, 0.78), p(0.34, 0.92)], fill=GREY)
    save(image, "IconGhostGo.png")


def icon_attack():
    image = canvas()
    draw = ImageDraw.Draw(image)
    for k in range(3):
        x = 0.24 + k * 0.2
        draw.polygon([p(x, 0.10), p(x + 0.1, 0.12), p(x + 0.02, 0.90), p(x - 0.04, 0.86)], fill=INK)
        draw.line([p(x + 0.03, 0.18), p(x - 0.01, 0.80)], fill=RED, width=int(0.02 * S))
    save(image, "IconGhostAttack.png")


def icon_tear():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([p(0.10, 0.20), p(0.44, 0.80)], radius=0.08 * S, fill=GREY)
    draw.polygon([p(0.52, 0.30), p(0.84, 0.18), p(0.90, 0.30), p(0.58, 0.42)], fill=PALE)
    hand(draw, 0.90, 0.20, 0.06, SKIN)
    for k in range(4):
        y = 0.32 + k * 0.04
        draw.line([p(0.46, y), p(0.54, y - 0.02)], fill=RED, width=int(0.02 * S))
    draw.polygon([p(0.60, 0.60), p(0.96, 0.64), p(0.94, 0.72), p(0.62, 0.70)], fill=INK)
    save(image, "IconGhostTear.png")


blank()
icon_sever()
icon_headshot()
icon_grenade()
icon_game()
icon_ghost()
icon_go()
icon_attack()
icon_tear()
