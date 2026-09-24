#!/usr/bin/env python3
"""The reference weapon textures the Trace sketches plant in the ground. Requires Pillow.

Run from any directory: python3 make_trace_trial_textures.py [--source DIR]

These are other people's art: the vanilla knife, longsword, spear and monosword (Ludeon) and two
modded swords, as copied into Melee Animation's Unity project. The output folder,
Textures/RimArt/TraceTrial/, is local only. This script adds it to .git/info/exclude so it cannot be
committed by accident, and nothing in it ships. In game the Trace kit reads each weapon's own
texture and derives the outline and mask at load.

Per weapon it writes:
  <Name>.png         the weapon picture (on LongSword, the editor's red arrow painted out)
  <Name>Outline.png  white line on the silhouette edge plus a fainter inset line: the trace wire
  <Name>Mask.png     the silhouette in white: the scan line and flashes

and Atlas.png, every weapon in the shades the Unlimited Blade Works world draws its standing
swords with, plus flat swatches for their ground marks (see atlas()). With --atlas-only it rebuilds
just the atlas from the PNGs already in the output folder, without the Melee Animation source.

It prints the tip and pommel in uv (v up) and the blade's extent across its axis in 16 steps
from the tip, which is the Weapons table in Tools/VfxLab/web/sketches/lib/trace.js. The tip is the
end with the larger u + v, which holds for these six; a texture drawn tip-down-left would need its
tip given by hand.

--source defaults to ~/projects/Melee-Animation/Source/Animations/Assets/Resources, a checkout of
https://github.com/Epicguru/Melee-Animation.
"""
import argparse, json, math, os, subprocess
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Textures/RimArt/TraceTrial"
NAMES = ["Knife", "LongSword", "Spear", "MonoSword", "LargeSword", "Wyrmslayer"]
WORK = 256          # outline and mask are drawn at this size
BINS = 16

# The atlas (Tools/VfxLab/web/sketches/lib/ubw-pocket.js reads it with the same numbers). Cells of
# ATLAS_CELL pixels on an ATLAS_GRID square; each picture fills the cell but for ATLAS_PAD clear
# pixels round it, so mipmaps do not bleed between cells. Row r is NAMES[r] with its colour times
# SHADES[c] in column c: the blade's two edge greys, the face lit three ways, and the two dark bands
# low on the blade (the middle face under one and two 24 % black layers). Row 6 holds flat swatches
# for the ground marks: crack, slit, the three soils, the soft contact shadow, white.
ATLAS_CELL, ATLAS_PAD, ATLAS_GRID = 128, 8, 8
SHADES = [.28, .38, .82, .91, 1.0, .91 * .76, .91 * .76 * .76]
SWATCHES = [(0, 0, 0, .5), (.05, .04, .03, .92), (.2, .14, .09, 1), (.36, .27, .18, 1), (.55, .43, .3, 1), "soft", (1, 1, 1, 1)]


def exclude_output():
    """Keep the output folder out of git in every worktree of this clone."""
    common = subprocess.run(["git", "rev-parse", "--git-common-dir"], cwd=ROOT, capture_output=True, text=True)
    if common.returncode:
        return
    exclude = (ROOT / common.stdout.strip()).resolve() / "info" / "exclude"
    line = "Textures/RimArt/TraceTrial/"
    text = exclude.read_text() if exclude.exists() else ""
    if line not in text.split("\n"):
        exclude.parent.mkdir(parents=True, exist_ok=True)
        exclude.write_text(text + ("" if text.endswith("\n") or not text else "\n") + line + "\n")


def load(source, name):
    im = Image.open(source / f"{name}.png").convert("RGBA")
    if name == "LongSword":
        # The editor copy carries a red arrow (columns 31-41, from row 32 down) over the blade's
        # lower edge. Inside the blade band (rows 25-34) the blade is the same along its length, so
        # a red pixel takes its row's pixel at column 29; below the band it is cleared.
        px = im.load()
        reddish = lambda p: p[3] > 20 and p[0] > p[1] + 50 and p[0] > p[2] + 50
        for y in range(im.size[1]):
            for x in range(im.size[0]):
                if reddish(px[x, y]):
                    px[x, y] = px[29, y] if 25 <= y <= 34 else (0, 0, 0, 0)
    return im


def white(alpha):
    """White pixels with the given L image as alpha, outermost pixels clear."""
    a = alpha.copy()
    px, (w, h) = a.load(), a.size
    for i in range(w):
        px[i, 0] = px[i, h - 1] = 0
    for j in range(h):
        px[0, j] = px[w - 1, j] = 0
    out = Image.new("RGBA", a.size, (255, 255, 255, 0))
    out.putalpha(a)
    return out


def axis(mask):
    """Tip, pommel (uv, v up) and width profile of the silhouette's principal axis."""
    mp = mask.load()
    pts = [((x + .5) / WORK, 1 - (y + .5) / WORK) for y in range(WORK) for x in range(WORK) if mp[x, y]]
    n = len(pts)
    mu = (sum(p[0] for p in pts) / n, sum(p[1] for p in pts) / n)
    sxx = sum((p[0] - mu[0]) ** 2 for p in pts) / n
    syy = sum((p[1] - mu[1]) ** 2 for p in pts) / n
    sxy = sum((p[0] - mu[0]) * (p[1] - mu[1]) for p in pts) / n
    ang = .5 * math.atan2(2 * sxy, sxx - syy)
    a = (math.cos(ang), math.sin(ang))
    s = [(p[0] - mu[0]) * a[0] + (p[1] - mu[1]) * a[1] for p in pts]
    e1 = (mu[0] + a[0] * min(s), mu[1] + a[1] * min(s))
    e2 = (mu[0] + a[0] * max(s), mu[1] + a[1] * max(s))
    tip, pommel = (e1, e2) if e1[0] + e1[1] > e2[0] + e2[1] else (e2, e1)
    length = math.hypot(pommel[0] - tip[0], pommel[1] - tip[1])
    along = ((pommel[0] - tip[0]) / length, (pommel[1] - tip[1]) / length)
    across = (-along[1], along[0])
    lo, hi = [9.0] * BINS, [-9.0] * BINS
    for p in pts:
        d = (p[0] - tip[0], p[1] - tip[1])
        k = min(BINS - 1, max(0, int((d[0] * along[0] + d[1] * along[1]) / length * BINS)))
        c = d[0] * across[0] + d[1] * across[1]
        lo[k], hi[k] = min(lo[k], c), max(hi[k], c)
    width = [[round(lo[k], 4), round(hi[k], 4)] if hi[k] > -9 else [0, 0] for k in range(BINS)]
    return {"tip": [round(tip[0], 4), round(tip[1], 4)], "pommel": [round(pommel[0], 4), round(pommel[1], 4)], "width": width}


def atlas():
    """Atlas.png from the pictures already in OUT: every weapon in every shade, then the swatches."""
    side, inner = ATLAS_CELL * ATLAS_GRID, ATLAS_CELL - 2 * ATLAS_PAD
    out = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    for row, name in enumerate(NAMES):
        # premultiplied while resizing, so clear pixels do not darken the edge
        pic = Image.open(OUT / f"{name}.png").convert("RGBA").convert("RGBa").resize((inner, inner), Image.LANCZOS).convert("RGBA")
        r, g, b, a = pic.split()
        a = a.point(lambda v: 0 if v < 8 else v)     # two of the copies carry a 1 % black background
        for col, shade in enumerate(SHADES):
            dim = lambda v, k=shade: int(round(v * k))
            out.paste(Image.merge("RGBA", (r.point(dim), g.point(dim), b.point(dim), a)),
                      (col * ATLAS_CELL + ATLAS_PAD, row * ATLAS_CELL + ATLAS_PAD))
    soft = Image.open(ROOT / "Textures/RimArt/SixPaths/SoftDisc.png").convert("RGBA").resize((inner, inner), Image.LANCZOS)
    for col, swatch in enumerate(SWATCHES):
        at = (col * ATLAS_CELL + ATLAS_PAD, 6 * ATLAS_CELL + ATLAS_PAD)
        if swatch == "soft":
            cell = Image.new("RGBA", (inner, inner), (0, 0, 0, 0))
            cell.putalpha(soft.split()[3].point(lambda v: int(round(v * .34))))
        else:
            cell = Image.new("RGBA", (inner, inner), tuple(int(round(c * 255)) for c in swatch))
        out.paste(cell, at)
    out.save(OUT / "Atlas.png")
    print(f"wrote {OUT / 'Atlas.png'} ({side} x {side}, {len(NAMES)} weapons x {len(SHADES)} shades, {len(SWATCHES)} swatches)")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--source", default=os.path.expanduser("~/projects/Melee-Animation/Source/Animations/Assets/Resources"))
    ap.add_argument("--atlas-only", action="store_true", help="rebuild Atlas.png from the PNGs already written")
    args = ap.parse_args()
    exclude_output()
    if args.atlas_only:
        atlas()
        return
    source = Path(args.source)
    OUT.mkdir(parents=True, exist_ok=True)
    for name in NAMES:
        im = load(source, name)
        im.save(OUT / f"{name}.png")
        mask = im.resize((WORK, WORK), Image.BICUBIC).split()[3].point(lambda a: 255 if a > 127 else 0)
        ring = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(5)), mask.filter(ImageFilter.MinFilter(3)))
        inset = mask.filter(ImageFilter.MinFilter(13))
        inner = ImageChops.subtract(inset, inset.filter(ImageFilter.MinFilter(3))).point(lambda a: int(a * .45))
        white(ImageChops.lighter(ring, inner).filter(ImageFilter.GaussianBlur(.8))).save(OUT / f"{name}Outline.png")
        white(mask.filter(ImageFilter.GaussianBlur(.6))).save(OUT / f"{name}Mask.png")
        print(f"  {name}: {json.dumps(axis(mask), separators=(',', ':'))},")
    print(f"wrote {len(NAMES) * 3} textures to {OUT} (git-excluded)")
    atlas()


if __name__ == "__main__":
    main()
