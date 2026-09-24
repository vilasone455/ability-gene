#!/usr/bin/env python3
"""The Infinity Castle's four textures. Requires Pillow.

Run from any directory: python3 make_castle_textures.py

Void.png    the void terrain: flat VoidDeep, opaque. The mod draws the rooms at other depths and
            the void's fog just above it.
Floor.png   the castle floor terrain: flat WoodFloor. The mod draws every room's floor over it.
Wall.png    the castle wall: flat WallWood. The mod draws the walls over it.
Blank.png   the lantern: nothing. Its light is a glower; the mod draws the lantern itself.

All four are fallbacks under the mod's own drawing (Source/RimArt/InfinityCastle), in the colours of
the lab's lib/infinity-castle.js, so a spot the mod does not draw still reads as the castle.
"""
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parent / "Textures/RimArt/InfinityCastle"
SIZE = 64

# lib/infinity-castle.js: VoidDeep, WoodFloor, WallWood.
COLOURS = {
    "Void": (0.010, 0.008, 0.016, 1.0),
    "Floor": (0.40, 0.25, 0.14, 1.0),
    "Wall": (0.14, 0.08, 0.045, 1.0),
    "Blank": (0.0, 0.0, 0.0, 0.0),
}


def write(name, rgba):
    OUT.mkdir(parents=True, exist_ok=True)
    pixel = tuple(round(v * 255) for v in rgba)
    image = Image.new("RGBA", (SIZE, SIZE), pixel)
    path = OUT / f"{name}.png"
    image.save(path)
    print(f"{path} {SIZE}x{SIZE} {pixel}")


if __name__ == "__main__":
    for name, rgba in COLOURS.items():
        write(name, rgba)
