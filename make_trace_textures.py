#!/usr/bin/env python3
"""Textures for Unlimited Blade Works, the Trace kit's pocket world. Requires Pillow.

Run from any directory: python3 make_trace_textures.py

Flame.png    128 x 128, white, alpha the shape of one standing flame (lib/ubw.js lab/ubw-flame)
Earth.png    256 x 256, the world's ground: red-brown dust over two sizes of dry cracks, a few pale
             pebbles (lib/ubw-pocket.js lab/ubw-earth); one whole copy per 8-cell tile of the floor
Fade.png     64 x 64, white, alpha rising west to east as a smoothstep (lib/ubw-pocket.js lab/ubw-fade):
             the soft inner edge of the white and the soot outside the wall of fire
Terrain.png  64 x 64, flat, the fallback for the AG_UbwEarth terrain (the mod draws Earth over it)
TerrainAtlas.png  1024 x 1024, the world v2's ground (lib/ubw-terrain.js lab/ubw-terrain-atlas): the earth
             tile of 384 px in four shades in a 2 x 2 block at the top left (the earth without its big
             cracks: the plates are those), the face gradient (crest colour at the top, foot colour at the
             bottom) in a column right of it, and swatches of 64 px along the bottom: the lit lip, the
             crest, the solid foot, the crack floor, a hairline crack
CrestFace.png  256 x 256, the world v4 crest's face (lib/ubw-crest.js lab/ubw-crest-face): the plates' earth
             in shade, darker toward the foot, Voronoi cracks in the crack floor's colour, the lit rim along
             the top; repeats across
HorizonSky.png  128 x 128, the world v4's sky (lib/ubw-horizon.js lab/ubw-horizon-sky): the glow at the
             horizon (bottom) through orange and red to the dark top, one row per 0.32 cells of the 41-cell band
HorizonGround.png  256 x 256, the plain behind the crest from its foot (bottom) to the horizon (lab/ubw-horizon-
             ground): crack floor, earth, then haze, with faint bands closer together toward the horizon
DepthHaze.png  64 x 64, white, alpha 1 at the top falling to 0 at the bottom (lab/ubw-horizon-depth): the
             camera's haze
IconUnlimitedBladeWorks.png  128 x 128, the ability's icon: three swords standing in a ring of fire, drawn
             at 4x and scaled down, dark outlines as the Panoply icons have
IconTraceOn.png  128 x 128, Trace On's icon: a sword whose grip half is steel and whose point half is still
             the teal wire (outline, centre line, cross lines), a bright line where the steel is filling in
IconReinforcement.png  128 x 128, Reinforcement's icon: a steel sword with a teal glow along its edge and
             square circuit lines behind it

The formulas are the ones the lab sketches were tuned with, and hash, noise and fbm below are
Tools/VfxLab/web/js/standins.js's, integer overflow included, so the game draws the pixels the sketches
were judged on.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Trace"
EARTH_DARK, EARTH_LIT = (.22, .12, .08), (.58, .38, .25)


def i32(v):
    v &= 0xFFFFFFFF
    return v - 0x100000000 if v & 0x80000000 else v


def hash01(x, y, seed):
    h = i32(x * 374761393 + y * 668265263 + seed * 144665)
    h = i32((h ^ ((h & 0xFFFFFFFF) >> 13)) * 1274126177)
    return ((h ^ ((h & 0xFFFFFFFF) >> 16)) & 0xFFFFFFFF) / 4294967295


def noise(x, y, period, seed):
    xi, yi = math.floor(x), math.floor(y)
    xf, yf = x - xi, y - yi
    s = lambda t: t * t * (3 - 2 * t)
    at = lambda i, j: hash01(((i % period) + period) % period, ((j % period) + period) % period, seed)
    a, b, c, d = at(xi, yi), at(xi + 1, yi), at(xi, yi + 1), at(xi + 1, yi + 1)
    return a + (b - a) * s(xf) + (c - a) * s(yf) + (a - b - c + d) * s(xf) * s(yf)


def fbm(x, y, seed, octaves=4, period=8):
    v, amp, total = 0.0, 0.5, 0.0
    for o in range(octaves):
        v += noise(x * (1 << o), y * (1 << o), period * (1 << o), seed + o) * amp
        total += amp
        amp *= 0.5
    return v / total


def clamp(v):
    return 0.0 if v < 0 else 1.0 if v > 1 else v


def edge(lo, hi, x):
    t = clamp((x - lo) / (hi - lo))
    return t * t * (3 - 2 * t)


def border(u, v, period, seed):
    """Distance between the nearest two of a jittered grid's points: the cracks of a tileable Voronoi."""
    x, y = u * period, v * period
    xi, yi = math.floor(x), math.floor(y)
    d1 = d2 = 9.0
    for j in (-1, 0, 1):
        for i in (-1, 0, 1):
            cx, cy = xi + i, yi + j
            wx, wy = ((cx % period) + period) % period, ((cy % period) + period) % period
            d = math.hypot(cx + .15 + .7 * hash01(wx, wy, seed) - x, cy + .15 + .7 * hash01(wx, wy, seed + 1) - y)
            if d < d1:
                d2, d1 = d1, d
            elif d < d2:
                d2 = d
    return d2 - d1


def byte(v):
    # A canvas byte: clamped, rounded, as the lab writes its textures.
    return max(0, min(255, round(v * 255)))


def pixels(n, fn):
    image = Image.new("RGBA", (n, n))
    px = image.load()
    for y in range(n):
        for x in range(n):
            px[x, y] = tuple(byte(k) for k in fn(x / n, y / n))
    return image


def flame(u, v):
    y = 1 - v
    n = fbm(u * 4, v * 3, 91, 3, 4)
    hw = .38 * (math.sqrt(y / .22) if y < .22 else max(0, 1 - (y - .22) / .78) ** .9)
    dx = abs(u - .5 + (n - .5) * .22 * y)
    return (1, 1, 1, clamp((hw - dx) / (.3 * hw + .02)) * min(1, (1 - y) * 5))


def earth(u, v):
    n, fine = fbm(u * 4, v * 4, 301, 4, 4), fbm(u * 16, v * 16, 305, 2, 16)
    big, small = border(u, v, 3, 311), border(u, v, 7, 331)
    k = (1 - .36 * (1 - edge(0, .045, big))) * (1 - .15 * (1 - edge(0, .03, small))) * (.92 + .16 * fine)
    pebble = .18 if hash01(math.floor(u * 128), math.floor(v * 128), 337) > .995 else 0
    t = .2 + .6 * n
    return ((EARTH_DARK[0] + (EARTH_LIT[0] - EARTH_DARK[0]) * t) * k + pebble,
            (EARTH_DARK[1] + (EARTH_LIT[1] - EARTH_DARK[1]) * t) * k + pebble * .8,
            (EARTH_DARK[2] + (EARTH_LIT[2] - EARTH_DARK[2]) * t) * k + pebble * .6, 1)


def fade(u, v):
    return (1, 1, 1, u * u * (3 - 2 * u))


# ---- the world v2's ground and sky ----------------------------------------------------------------------
ATLAS_SIDE, TILE_PX, GRAD_X0, GRAD_X1, GRAD_H, SWATCH_Y, SWATCH_PX = 1024, 384, 800, 832, 768, 900, 64
SHADES = [(1, 1, 1), (.84, .82, .8), (1.12, 1.06, 1), (.95, .9, .86)]
CREST, FOOT, LIP_LIT, BASE = (.32, .18, .12), (.11, .06, .045), (.66, .46, .32), (.07, .045, .035)


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def earth_tile(px, py):
    """The earth once, at tile size: the dust and fine cracks of earth(), without the big cracks."""
    tu, tv = px / TILE_PX, py / TILE_PX
    n, fine, small = fbm(tu * 4, tv * 4, 301, 4, 4), fbm(tu * 16, tv * 16, 305, 2, 16), border(tu, tv, 7, 331)
    k = (1 - .15 * (1 - edge(0, .03, small))) * (.92 + .16 * fine)
    pebble = .18 if hash01(math.floor(tu * 128), math.floor(tv * 128), 337) > .995 else 0
    r, g, b = mix(EARTH_DARK, EARTH_LIT, .2 + .6 * n)
    return (r * k + pebble, g * k + pebble * .8, b * k + pebble * .6)


def terrain_atlas():
    image = Image.new("RGBA", (ATLAS_SIDE, ATLAS_SIDE), (0, 0, 0, 0))
    px = image.load()
    tile = [[earth_tile(x, y) for x in range(TILE_PX)] for y in range(TILE_PX)]
    for y in range(2 * TILE_PX):
        for x in range(2 * TILE_PX):
            k = (y // TILE_PX) * 2 + x // TILE_PX
            r, g, b = tile[y % TILE_PX][x % TILE_PX]
            sh = SHADES[k]
            px[x, y] = (byte(min(1, r * sh[0])), byte(min(1, g * sh[1])), byte(min(1, b * sh[2])), 255)
    for y in range(GRAD_H):
        t = y / GRAD_H
        c = mix(CREST, FOOT, t * t * (3 - 2 * t))
        for x in range(GRAD_X0, GRAD_X1):
            px[x, y] = tuple(byte(k) for k in c) + (255,)
    swatches = [LIP_LIT, CREST, FOOT, BASE, FOOT]
    for y in range(SWATCH_Y, SWATCH_Y + SWATCH_PX):
        for x in range(ATLAS_SIDE):
            k = min(4, x // SWATCH_PX)
            px[x, y] = tuple(byte(v) for v in swatches[k]) + (byte(.55) if k == 4 else 255,)
    return image


# ---- the world v4's crest and the sky behind it ----------------------------------------------------------
GLOW, HORIZON, MID, HIGH, TOP = (1, .89, .67), (1, .62, .3), (.86, .36, .2), (.5, .17, .16), (.24, .08, .11)
FAR_EARTH, HAZE_FAR, RIM_LIGHT = (.4, .26, .19), (.93, .61, .36), (1, .67, .35)
SKY_CELLS, KE = 40, .3


def lerp(a, b, t):
    return a + (b - a) * t


def face_crack(u, v):
    """Distance to the second-nearest of 3 x 2 jittered seeds minus the nearest, repeating across u."""
    cols, rows = 3, 2
    d1 = d2 = 9.0
    for j in range(rows):
        for i in range(-1, cols + 1):
            w = ((i % cols) + cols) % cols
            sx, sy = (i + .15 + .7 * hash01(w, j, 145)) / cols, (j + .15 + .7 * hash01(w, j, 146)) / rows
            d = math.hypot((sx - u) * cols, (sy - v) * rows * 1.6)
            if d < d1:
                d2, d1 = d1, d
            elif d < d2:
                d2 = d
    return d2 - d1


def crest_face(u, v):
    # v 0 is the top of the bank, 1 its foot.
    n, grain, gully = fbm(u * 8, v * 3, 141, 4, 8), fbm(u * 48, v * 24, 143, 2, 48), fbm(u * 16, v * .8, 147, 2, 16)
    earth = mix(EARTH_DARK, EARTH_LIT, .2 + .6 * n)
    shade = lerp(.62, .4, v ** .9) * (.9 + .2 * gully) * (.94 + .12 * grain)
    crack = face_crack(u, v)
    line, lip = clamp(1 - crack / .07), clamp(1 - abs(crack - .1) / .04) * .25
    c = [lerp(k * shade * (1 + lip), b, line) for k, b in zip(earth, BASE)]
    rim = clamp(1 - v / .12) ** 2 * .55
    return tuple(lerp(k, r * .75, rim) for k, r in zip(c, RIM_LIGHT)) + (1,)


def sky_at(e):
    if e < 6:
        return mix(GLOW, HORIZON, clamp(e / 6))
    if e < 16:
        return mix(HORIZON, MID, (e - 6) / 10)
    if e < 38:
        return mix(MID, HIGH, (e - 16) / 22)
    return mix(HIGH, TOP, clamp((e - 38) / 40))


def horizon_sky(u, v):
    return sky_at(((1 - v) * (SKY_CELLS + 1) - 1) / KE) + (1,)


def horizon_ground(u, v):
    t = 1 - v
    c = mix(BASE, FAR_EARTH, t / .05) if t < .05 else mix(FAR_EARTH, HAZE_FAR, ((t - .05) / .95) ** 2.6)
    band = fbm(u * 3, t ** .45 * 40, 83, 3, 64)
    k = .88 + .24 * band * (1 - .6 * t)
    return tuple(min(1, x * k) for x in c) + (1,)


def depth_haze(u, v):
    return (1, 1, 1, (1 - v) ** 1.7)


# ---- the ability's icon ----------------------------------------------------------------------------------
OUTLINE, STEEL, STEEL_LIT, HILT, GUARD = (40, 26, 22, 255), (150, 160, 172, 255), (215, 222, 230, 255), (92, 52, 34, 255), (120, 96, 60, 255)
FIRE_OUT, FIRE_IN = (232, 92, 30, 255), (255, 196, 90, 255)


def icon():
    k = 4
    n = 128 * k
    image = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(image)
    cx, cy, rx, ry = 64 * k, 84 * k, 54 * k, 22 * k

    def ring(width, colour, grow=0):
        d.ellipse([cx - rx - grow, cy - ry - grow, cx + rx + grow, cy + ry + grow], outline=colour, width=width)

    def flames(back):
        for i in range(18):
            a = i / 18 * 2 * math.pi
            front = math.sin(a) > 0
            if front == back:
                continue
            x, y = cx + rx * math.cos(a), cy + ry * math.sin(a)
            h = (13 + 9 * hash01(i, 3, 7)) * k
            w = 5.5 * k
            bend = (hash01(i, 5, 9) - .5) * 5 * k

            def tongue(w, h, grow):
                # A teardrop: round at the foot, narrowing to a tip bent a little sideways.
                pts = []
                for j in range(17):
                    t = j / 16 * math.pi
                    pts.append((x - (w + grow) * math.cos(t), y + grow + (w + grow) * .55 * math.sin(t)))
                for j in range(1, 12):
                    u = j / 12
                    half = (w + grow) * (1 - u) ** .8
                    pts.append((x + half + bend * u * u, y - (h + grow) * u))
                pts.append((x + bend, y - h - grow * 1.5))
                for j in range(11, 0, -1):
                    u = j / 12
                    half = (w + grow) * (1 - u) ** .8
                    pts.append((x - half + bend * u * u, y - (h + grow) * u))
                return pts

            d.polygon(tongue(w, h, 2.5 * k), fill=OUTLINE)
            d.polygon(tongue(w, h, 0), fill=FIRE_OUT)
            d.polygon(tongue(w * .5, h * .55, 0), fill=FIRE_IN)

    ring(9 * k, OUTLINE)
    ring(5 * k, FIRE_OUT)
    flames(True)
    sword(d, k, 40 * k, 88 * k, 64 * k, -.32)
    sword(d, k, 88 * k, 90 * k, 60 * k, .28)
    sword(d, k, 64 * k, 94 * k, 82 * k, .03)
    flames(False)
    return image.resize((128, 128), Image.LANCZOS)


def sword(d, k, x, y, length, lean):
    # Point in the ground at (x, y), pommel up, leaning by `lean` radians.
    dx, dy = math.sin(lean), -math.cos(lean)
    px, py = -dy, dx
    def at(t, s):
        return (x + dx * t + px * s, y + dy * t + py * s)
    blade, half, o = length * .72, 5.5 * k, 3 * k
    tip = 12 * k
    shape = [at(0, 0), at(tip, half), at(blade, half), at(blade, -half), at(tip, -half)]
    grow = [at(-o, 0), at(tip, half + o), at(blade + o, half + o), at(blade + o, -half - o), at(tip, -half - o)]
    d.polygon(grow, fill=OUTLINE)
    d.polygon(shape, fill=STEEL)
    d.polygon([at(tip * .6, 0), at(tip, half * .45), at(blade, half * .45), at(blade, 0)], fill=STEEL_LIT)
    g0, g1 = blade, blade + 5 * k
    d.polygon([at(g0 - o, 15 * k + o), at(g1 + o, 15 * k + o), at(g1 + o, -15 * k - o), at(g0 - o, -15 * k - o)], fill=OUTLINE)
    d.polygon([at(g0, 15 * k), at(g1, 15 * k), at(g1, -15 * k), at(g0, -15 * k)], fill=GUARD)
    h0, h1 = g1, length
    d.polygon([at(h0, 4 * k + o), at(h1 + o, 4 * k + o), at(h1 + o, -4 * k - o), at(h0, -4 * k - o)], fill=OUTLINE)
    d.polygon([at(h0, 4 * k), at(h1, 4 * k), at(h1, -4 * k), at(h0, -4 * k)], fill=HILT)
    cxp, cyp = at(length + 2 * k, 0)
    d.ellipse([cxp - 6.5 * k, cyp - 6.5 * k, cxp + 6.5 * k, cyp + 6.5 * k], fill=OUTLINE)
    d.ellipse([cxp - 4 * k, cyp - 4 * k, cxp + 4 * k, cyp + 4 * k], fill=GUARD)

TRACE, TRACE_HOT = (89, 255, 209, 255), (209, 255, 242, 255)


def blade_at(k, x, y, lean):
    """The (along, across) to pixel map of sword()'s blade: point at (x, y), pommel along lean."""
    dx, dy = math.sin(lean), -math.cos(lean)
    px, py = -dy, dx
    return lambda t, s: (x + dx * t + px * s, y + dy * t + py * s)


def icon_trace_on():
    """Trace On: the grip half steel, the point half the wire the copy is traced from."""
    k = 4
    n = 128 * k
    x, y, length, lean = 100 * k, 26 * k, 112 * k, -2.36
    solid = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    sword(ImageDraw.Draw(solid), k, x, y, length, lean)
    wire = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(wire)
    at = blade_at(k, x, y, lean)
    blade, half, tip = length * .72, 5.5 * k, 12 * k
    shape = [at(0, 0), at(tip, half), at(blade, half), at(blade, -half), at(tip, -half)]
    d.polygon(shape, outline=TRACE, width=3 * k)
    d.line([at(4 * k, 0), at(blade, 0)], fill=TRACE, width=2 * k)
    for t in (.25, .5, .75):
        d.line([at(blade * t, half * .85), at(blade * t, -half * .85)], fill=TRACE, width=2 * k)
    # Steel from the pommel up to `front` along the sword, wire beyond it, a bright line at it.
    front = length * .5
    mask = Image.new("L", (n, n), 0)
    far = 200 * k
    ImageDraw.Draw(mask).polygon([at(front, far), at(front, -far), at(front + far * 2, -far), at(front + far * 2, far)], fill=255)
    image = Image.composite(solid, wire, mask)
    d = ImageDraw.Draw(image)
    d.line([at(front, half + 5 * k), at(front, -half - 5 * k)], fill=TRACE_HOT, width=3 * k)
    return image.resize((128, 128), Image.LANCZOS)


def icon_reinforcement():
    """Reinforcement: steel with a teal glow along its edge, square circuit lines behind it."""
    k = 4
    n = 128 * k
    image = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(image)
    for pts in ([(14, 30), (30, 30), (30, 18), (48, 18)], [(18, 104), (18, 84), (34, 84), (34, 70)], [(80, 116), (98, 116), (98, 100), (114, 100)]):
        d.line([(a * k, b * k) for a, b in pts], fill=TRACE, width=3 * k, joint="curve")
        for a, b in pts[1:]:
            d.ellipse([(a - 3) * k, (b - 3) * k, (a + 3) * k, (b + 3) * k], fill=TRACE_HOT)
    x, y, length, lean = 100 * k, 26 * k, 112 * k, -2.36
    at = blade_at(k, x, y, lean)
    blade, half, tip, glow = length * .72, 5.5 * k, 12 * k, 7 * k
    d.polygon([at(-glow, 0), at(tip, half + glow), at(blade, half + glow), at(blade, -half - glow), at(tip, -half - glow)], fill=(89, 255, 209, 150))
    sword(d, k, x, y, length, lean)
    d.polygon([at(0, 0), at(tip, half), at(blade, half), at(blade, -half), at(tip, -half)], outline=TRACE, width=2 * k)
    return image.resize((128, 128), Image.LANCZOS)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    pixels(128, flame).save(OUT / "Flame.png")
    pixels(256, earth).save(OUT / "Earth.png")
    pixels(64, fade).save(OUT / "Fade.png")
    flat = tuple(byte((EARTH_DARK[i] + EARTH_LIT[i]) / 2 * .9) for i in range(3)) + (255,)
    Image.new("RGBA", (64, 64), flat).save(OUT / "Terrain.png")
    terrain_atlas().save(OUT / "TerrainAtlas.png")
    pixels(256, crest_face).save(OUT / "CrestFace.png")
    pixels(128, horizon_sky).save(OUT / "HorizonSky.png")
    pixels(256, horizon_ground).save(OUT / "HorizonGround.png")
    pixels(64, depth_haze).save(OUT / "DepthHaze.png")
    icon().save(OUT / "IconUnlimitedBladeWorks.png")
    icon_trace_on().save(OUT / "IconTraceOn.png")
    icon_reinforcement().save(OUT / "IconReinforcement.png")
    for f in sorted(OUT.glob("*.png")):
        print(f"{f.relative_to(OUT.parent.parent.parent)}  {f.stat().st_size} bytes")


if __name__ == "__main__":
    main()
