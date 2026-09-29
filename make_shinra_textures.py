#!/usr/bin/env python3
"""Shinra Tensei's textures. Requires Pillow.

Run from any directory: python3 make_shinra_textures.py

The dome's four (DomeShell, DomeFill, DomeFloor, Scour) are the lab sketch's textures
(Tools/VfxLab/web/sketches/pain-shinra-tensei.js, lab/shinra-shell, -fill, -floor, -scour), painted
with the lab's own noise (make_six_paths_textures.fbm) so the game matches the sketch. Distort is the
screen-warp mask for the dome's outline (ShinraDomeGraphics.Warp); IconPush is the ability's icon.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

from make_six_paths_textures import fbm

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Shinra"
# The dome's outline: a circle to the south, sqrt(1 + Lift^2) tall to the north (ShinraDome.DomeK).
DOME_K = math.sqrt(1 + 0.6 * 0.6)


def clamp(v):
    return max(0.0, min(1.0, v))


def lab_pixels(size, alpha):
    """The lab's pixels(): u, v from the top-left corner, white, alpha from the function."""
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    for py in range(size):
        for px in range(size):
            pixels[px, py] = (255, 255, 255, round(255 * clamp(alpha(px / size, py / size))))
    return image


def radial(u, v):
    return math.hypot(u - 0.5, v - 0.5) * 2


def shell(u, v):
    """A soft ring: brightest at 0.84 of the radius, fading inward over about 0.26 and out to nothing by 1."""
    r = radial(u, v)
    if r >= 0.99:
        return 0.0
    peak, edge = math.exp(-(((r - 0.84) / 0.26) ** 2)), clamp((0.99 - r) / 0.12)
    return min(1.0, (peak * 0.85 + 0.08 * r) * edge)


def scour(u, v):
    """Scoured ground: a disc broken by noise, with a ragged edge at about 0.9."""
    r, n, m = radial(u, v), fbm(u * 10, v * 10, 311, 4, 10), fbm(u * 5, v * 5, 97, 3, 5)
    edge = 0.9 + (m - 0.5) * 0.12
    if r >= edge:
        return 0.0
    patch = fbm(u * 3, v * 3, 53, 2, 3)
    return clamp(0.2 + 0.9 * n * n + 0.35 * (patch - 0.5)) * clamp((edge - r) / 0.04)


def fill(u, v):
    """The dome's body: filled to 0.85 of the radius, then soft to nothing, mottled like Storm 4's wind."""
    r, n = radial(u, v), fbm(u * 6, v * 6, 211, 3, 6)
    if r >= 0.99:
        return 0.0
    return (0.55 + 0.45 * n) * (0.7 + 0.3 * r * r) * clamp((0.99 - r) / 0.14)


def floor(u, v):
    """The wind low inside the dome, seen from above: a cloudy ring brightest at 0.78 of the radius."""
    r, n = radial(u, v), fbm(u * 7, v * 7, 419, 3, 7)
    if r >= 0.99:
        return 0.0
    return (math.exp(-(((r - 0.78) / 0.22) ** 2)) * 0.9 + 0.12) * (0.5 + 0.5 * n) * clamp((0.99 - r) / 0.1)


def distort(size):
    """Mask for the screen-warp quad (2.3 dome radii across, on the dome's centre): the dome's outline, feathered.

    Painted black, because the shader may composite this texture as well as read it as a mask.
    Black at low alpha reads as a faint pressure shadow either way, never as a bright square.
    """
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    for py in range(size):
        y = (1.0 - 2.0 * (py + 0.5) / size) * 1.15
        for px in range(size):
            x = (2.0 * (px + 0.5) / size - 1.0) * 1.15
            silhouette = math.hypot(x, y / (DOME_K if y >= 0 else 1.0))
            alpha = 0.0 if silhouette >= 1.0 else 0.55 * min(1.0, (1.0 - silhouette) / 0.22)
            pixels[px, py] = (0, 0, 0, round(255 * alpha))
    return image


def icon():
    """A simple pressure-wave icon, with two palms at its base."""
    image = Image.new("RGBA", (512, 512))
    draw = ImageDraw.Draw(image)
    for inset, opacity in [(38, 100), (76, 165), (116, 240)]:
        draw.arc((inset, inset, 512 - inset, 512 - inset), 190, 350, fill=(235, 244, 255, opacity), width=12)
    for x in (175, 337):
        draw.rounded_rectangle((x - 23, 239, x + 23, 329), radius=18, fill=(236, 241, 247, 255))
        draw.line((x, 323, x, 393), fill=(236, 241, 247, 255), width=21)
    return image.resize((128, 128), Image.Resampling.LANCZOS)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    assets = {
        "DomeShell": lab_pixels(128, shell),
        "DomeFill": lab_pixels(128, fill),
        "DomeFloor": lab_pixels(128, floor),
        "Scour": lab_pixels(256, scour),
        "Distort": distort(256),
        "IconPush": icon(),
    }
    for name, asset in assets.items():
        path = OUT / f"{name}.png"
        asset.save(path)
        print(path.relative_to(OUT.parent.parent.parent))


if __name__ == "__main__":
    main()
