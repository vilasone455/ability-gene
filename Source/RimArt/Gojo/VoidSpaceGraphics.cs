using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.GojoGraphics;

namespace RimArt
{
    /// <summary>
    /// The three heights the void is drawn at: its back (the navy), its deep (haze, galaxies, stars, the
    /// black hole) and the fog over it (the fog, the white patches, the speed lines, the dust, the light
    /// at the vanishing point). Only the order matters: everything in the deep group stays under Fog.
    /// </summary>
    internal readonly struct VoidLayers
    {
        public readonly float Back, Deep, Fog;

        public VoidLayers(float back, float fog)
        {
            Back = back;
            Deep = back + 0.01f;
            Fog = fog;
        }

        /// <summary>
        /// The pocket map, as the sketch draws it: the space under the terrain and the fog group at the
        /// void terrain's own height, which will need a see-through texture.
        /// </summary>
        public static readonly VoidLayers Pocket = new VoidLayers(AltitudeLayer.BelowTerrain.AltitudeFor(), AltitudeLayer.Terrain.AltitudeFor());

        /// <summary>
        /// A home map, for the preview: over items and under lying pawns (the whole void ends 0.19 over
        /// Back), so the navy covers the ground, plants, buildings and items and pawns stay on top.
        /// </summary>
        public static readonly VoidLayers Preview = new VoidLayers(AltitudeLayer.ItemImportant.AltitudeFor() + 0.1f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.25f);
    }

    /// <summary>
    /// The inside of Unlimited Void: the space (deep navy, drifting haze, 180 stars, 6 far galaxies, the
    /// anime's 7 white ink patches, a thin fog), the speed-line tunnel at the opening with its glitter
    /// dust and the white light at the vanishing point, and the black hole. The port of the space,
    /// tunnel and hole parts of the lab's lib/unlimited-void.js, in the Cursed Clash colours (the
    /// user's pick, 2026-09-24; the anime ep. 7 set stays in the lab).
    ///
    /// Everything is a level circle, a quad or a strip, so nothing needs a per-facing method. Heights
    /// come from a <see cref="VoidLayers"/>. The four textures are made by make_gojo_textures.py from
    /// the lab's formulas.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class VoidSpaceGraphics
    {
        internal static readonly Color Navy = new Color(0.02f, 0.03f, 0.08f), Haze = new Color(0.16f, 0.55f, 0.62f);
        internal static readonly Color Indigo = new Color(0.26f, 0.22f, 0.72f), Dusk = new Color(0.45f, 0.28f, 0.72f);
        internal static readonly Color GasBody = new Color(0.3f, 0.32f, 0.44f), GasLight = new Color(0.85f, 0.9f, 1f), Smoke = new Color(0.7f, 0.92f, 0.95f);

        // The Cursed Clash colours, from mxw3_ujSYDo 14-20 s: indigo (15, 5, 46), violet glow (45, 35, 80),
        // lavender-white cores (230, 212, 235). TunnelTint is added over the view while the lines run,
        // PointBloom sits faintly round the vanishing point, glows are a streak's wide dim body and cores
        // its thin bright middle; Cored is the share of streaks that have a core.
        private static readonly Color TunnelTint = new Color(0.05f, 0.01f, 0.12f), PointBloom = new Color(0.4f, 0.35f, 0.85f);
        private static readonly Color LightHalo = new Color(0.86f, 0.82f, 1f), DustHalo = new Color(0.55f, 0.5f, 0.95f);
        private static readonly Color[] GlowColours =
        {
            new Color(0.45f, 0.38f, 0.8f), new Color(0.36f, 0.36f, 0.9f), new Color(0.6f, 0.45f, 0.82f), new Color(0.42f, 0.33f, 0.75f),
        };
        private static readonly Color[] CoreColours = { new Color(0.92f, 0.86f, 0.96f), new Color(1f, 1f, 1f), new Color(0.84f, 0.86f, 1f) };
        private const float GlowAlpha = 0.32f, CoreAlpha = 0.85f, Cored = 0.8f;

        private static readonly Material Splat = MaterialPool.MatFrom("RimArt/Gojo/Splatter", ShaderDatabase.MoteGlow);
        private static readonly Material HoleGas = MaterialPool.MatFrom("RimArt/Gojo/HoleGas", ShaderDatabase.MoteGlow);
        private static readonly Material HoleWisps = MaterialPool.MatFrom("RimArt/Gojo/HoleWisps", ShaderDatabase.MoteGlow);
        private static readonly Material HoleRing = MaterialPool.MatFrom("RimArt/Gojo/HoleRing", ShaderDatabase.MoteGlow);

        /// <summary>How far round the centre the stars, galaxies and patches lie.</summary>
        private const float SpaceReach = 24f;
        /// <summary>The speed lines: head from 0.6 cells out to at least 60, flickering 12 times a second.</summary>
        internal const float TunnelReach = 60f;
        private const float From = 0.6f, Flicker = 12f;
        private const int RaySteps = 12;
        // The dust clouds: 16, each with 12 glitter specks, 2 of them crossed, streaming out 1.6 x slower than the lines.
        private const int DustClouds = 16, DustGlitter = 12, DustCrossed = 2;
        private const float DustFrom = 1.5f, DustSlower = 1.6f;
        // The black hole: the gas texture's edge is GasOut x R, the ring sits 2.2 R out on a quad RingQuad x R each way.
        private const float GasOut = 2.15f, RingQuad = 2.6f;
        private static readonly Vector2 RingShift = new Vector2(-0.05f, 0.06f);

        private static readonly RayBatch glowRays = new RayBatch(4, "Unlimited Void glow rays");
        private static readonly RayBatch coreRays = new RayBatch(3, "Unlimited Void core rays");

        /// <summary>
        /// How far the speed lines reach from <paramref name="point"/>: to the farthest corner of the view,
        /// so the tunnel fills the screen zoomed out, and at least the 60 cells the sketch was judged with.
        /// Worked out once when the effect starts: recomputed during the camera push, it would make the
        /// whole tunnel pulse with the zoom.
        /// </summary>
        internal static float ReachFor(Vector2 point) => Mathf.Max(TunnelReach, FarCorner(point));

        private static float FarCorner(Vector2 p)
        {
            CellRect view = Find.CameraDriver.CurrentViewRect;
            float dx = Mathf.Max(Mathf.Abs(view.minX - p.x), Mathf.Abs(view.maxX + 1 - p.x));
            float dz = Mathf.Max(Mathf.Abs(view.minZ - p.y), Mathf.Abs(view.maxZ + 1 - p.y));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static Vector2 Place(Vector2 p, Vector2 point, float g) => point + (p - point) * g;

        /// <summary>
        /// The space round <paramref name="c"/>: deep navy, drifting haze, stars, far galaxies and the white
        /// ink patches, with a thin fog over them. <paramref name="fade"/> 0..1 brings everything but the
        /// navy in. The fly-in: the stars, galaxies and patches sit <paramref name="g"/> of their distance
        /// from <paramref name="point"/> and are g of their size, stars dimmer while far; every third star
        /// that has moved since <paramref name="g0"/> (a moment before) is drawn as a trail from where it
        /// was. g = 1 is landed. The haze stays as it is.
        /// </summary>
        internal static void SpaceFloor(Vector2 c, float s, float fade, in VoidLayers layers, Vector2 point, float g, float g0)
        {
            float cover = Mathf.Max(SpaceReach * 5f, 2f * FarCorner(c) + 4f);
            DrawMesh(MeshPool.plane10, c, layers.Back, cover, cover, 0f, Navy, solid);
            if (fade <= 0f) return;
            float far = Mathf.Sqrt(g), tau = Mathf.PI * 2f;
            for (int i = 0; i < 8; i++)
            {
                float ang = Rand(i + 700) * tau, d = SpaceReach * (0.1f + 0.75f * Rand(i + 710)), size = 7f + 10f * Rand(i + 720), drift = 0.7f * Mathf.Sin(s * 0.07f + i);
                Color colour = i % 3 == 0 ? Haze : i % 3 == 1 ? Indigo : Dusk;
                Sprite(new Vector2(c.x + Mathf.Cos(ang) * d + drift, c.y + Mathf.Sin(ang) * d), size * 1.4f, size,
                    Fade(colour, (0.14f + 0.1f * Rand(i + 730)) * fade), glow, layers.Deep + 0.001f * i, Rand(i + 740) * 180f);
            }
            for (int i = 0; i < 6; i++)
            {
                float ang = Rand(i + 800) * tau, d = 7f + (SpaceReach - 7f) * Rand(i + 810);
                Color colour = i % 3 == 0 ? Violet : i % 3 == 1 ? Ice : Peach;
                Galaxy(Place(new Vector2(c.x + Mathf.Cos(ang) * d, c.y + Mathf.Sin(ang) * d), point, g), (0.8f + 1.2f * Rand(i + 820)) * g,
                    0.4f + 0.35f * Rand(i + 830), Rand(i + 840) * 180f, s * (5f + 5f * Rand(i + 850)) * (i % 2 == 1 ? 1f : -1f), colour, fade * far, layers);
            }
            for (int i = 0; i < 180; i++)
            {
                var star = new Vector2(c.x + (Rand(i + 900) - 0.5f) * SpaceReach * 2f, c.y + (Rand(i + 1900) - 0.5f) * SpaceReach * 2f);
                float cube = Rand(i + 2900), size = (0.035f + 0.1f * cube * cube * cube) * (0.4f + 0.6f * g);
                float twinkle = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(s * (1f + 2.2f * Rand(i + 3900)) + i)), a = twinkle * fade * far;
                Vector2 at = Place(star, point, g);
                if (i % 3 == 0)
                {
                    Vector2 was = Place(star, point, g0);
                    if ((at - was).magnitude > size * 3f) Streak(was, at, size * 1.6f, Fade(White, 0.55f * a), whiteGlow, layers.Deep + 0.029f, 2);
                }
                Sprite(at, size * 2.6f, size * 2.6f, Fade(i % 9 != 0 ? White : Ice, 0.9f * a), glow, layers.Deep + 0.03f);
                if (i % 15 == 0)
                    for (int k = 0; k < 2; k++) Sprite(at, 0.03f, size * 9f, Fade(White, 0.5f * a), glow, layers.Deep + 0.031f, k * 90f + 45f * (i % 2));
            }
            DrawMesh(MeshPool.plane10, c, layers.Fog, cover, cover, 0f, Fade(Navy, 0.22f * fade), solid);
            // The white patches: light breaking through, not things lying in the space, so they sit over the fog.
            for (int i = 0; i < 7; i++)
            {
                float ang = i / 7f * tau + Rand(i + 1000) * 0.6f, d = 11f + 8f * Rand(i + 1010), size = (2.5f + 3.5f * Rand(i + 1020)) * g;
                float breathe = 0.8f + 0.2f * Mathf.Sin(s * 0.8f + i * 2.1f);
                Vector2 at = Place(new Vector2(c.x + Mathf.Cos(ang) * d, c.y + Mathf.Sin(ang) * d), point, g);
                Sprite(at, size * 1.9f, size * 1.6f, Fade(Ice, 0.18f * breathe * fade), glow, layers.Fog + 0.01f);
                Sprite(at, size, size * (0.75f + 0.35f * Rand(i + 1030)), Fade(White, breathe * fade), Splat, layers.Fog + 0.011f + i * 0.0005f, Rand(i + 1040) * 360f);
            }
        }

        /// <summary>A far galaxy: a soft oval, a bright core and two arms, turning slowly. Tilt and turn in degrees.</summary>
        private static void Galaxy(Vector2 at, float size, float flat, float tilt, float turn, Color colour, float fade, in VoidLayers layers)
        {
            Sprite(at, size * 1.7f, size * 1.7f * flat, Fade(colour, 0.2f * fade), glow, layers.Deep + 0.02f, tilt);
            Sprite(at, size * 0.45f, size * 0.45f * flat, Fade(White, 0.55f * fade), glow, layers.Deep + 0.021f, tilt);
            float ct = Mathf.Cos(tilt * Mathf.Deg2Rad), st = Mathf.Sin(tilt * Mathf.Deg2Rad);
            for (int arm = 0; arm < 2; arm++)
            {
                Vector2[] pts = GokuGraphics.Points(9);
                for (int k = 0; k <= 8; k++)
                {
                    float v = k / 8f, r = size * (0.12f + 0.8f * v), a = arm * Mathf.PI + v * 2.6f + turn * Mathf.Deg2Rad;
                    float lx = Mathf.Cos(a) * r, lz = Mathf.Sin(a) * r * flat;
                    pts[k] = new Vector2(at.x + lx * ct + lz * st, at.y - lx * st + lz * ct);   // turned clockwise by tilt, as the sprites are
                }
                GokuGraphics.Line(pts, size * 0.18f, Fade(colour, 0.32f * fade), whiteGlow, layers.Deep + 0.022f, GokuGraphics.Taper.Both);
            }
        }

        /// <summary>
        /// The black hole at <paramref name="h"/>, disc radius <paramref name="R"/>, as anime ep. 7 draws it
        /// (the frame with Jogo in front of it; colours sampled from it): a black disc with a light rim of
        /// gas hugging it, grey-blue feathery gas out to 2.15 R turning slowly, lit upper right and left and
        /// dark along the bottom, a thin ring at 2.2 R, and a pale smoke cloud with sparkles off its east
        /// side. <paramref name="live"/> 0..1 fades it.
        /// </summary>
        internal static void Hole(Vector2 h, float R, float s, float live, in VoidLayers layers)
        {
            if (R <= 0.02f || live <= 0f) return;
            float y = layers.Deep + 0.08f, gas = R * GasOut * 2f;
            Sprite(h, R * 5.5f, R * 5.5f, Fade(Indigo, 0.2f * live), glow, y);
            Sprite(h, gas, gas, Fade(GasBody, 0.95f * live), HoleGas, y + 0.005f, -s * 5f);
            Sprite(h, gas, gas, Fade(GasLight, 0.8f * live), HoleWisps, y + 0.01f, -s * 9f);
            // Light and shade that stay put while the gas turns: lit upper right and left, dark bottom.
            Sprite(new Vector2(h.x + R * 0.1f, h.y - R * 1.55f), R * 3.6f, R * 1.9f, Fade(Navy, 0.72f * live), soft, y + 0.011f);
            Sprite(new Vector2(h.x + R * 1.3f, h.y - R * 0.9f), R * 1.8f, R * 1.8f, Fade(Navy, 0.5f * live), soft, y + 0.0112f);
            Sprite(new Vector2(h.x - R * 0.1f, h.y + R * 1.8f), R * 1.6f, R * 0.7f, Fade(Navy, 0.4f * live), soft, y + 0.0114f);
            Sprite(new Vector2(h.x + R * 1.05f, h.y + R * 1.05f), R * 1.6f, R, Fade(GasLight, 0.2f * live), glow, y + 0.012f, -45f);
            Sprite(new Vector2(h.x - R * 1.65f, h.y + R * 0.1f), R * 0.9f, R * 2.6f, Fade(GasLight, 0.2f * live), glow, y + 0.0122f);
            // A few loose streaks on top, turning faster near the disc.
            for (int i = 0; i < 18; i++)
            {
                float r0 = R * (1.1f + 0.75f * Rand(i + 1100)), spin = 0.5f * Mathf.Pow(R / r0, 1.5f);
                float a0 = Rand(i + 1105) * Mathf.PI * 2f + s * spin, span = 0.35f + 0.6f * Rand(i + 1110);
                Vector2[] pts = GokuGraphics.Points(9);
                for (int k = 0; k <= 8; k++)
                {
                    float v = k / 8f, r = r0 * (1f - 0.06f * v), a = a0 + v * span;
                    pts[k] = new Vector2(h.x + Mathf.Cos(a) * r, h.y + Mathf.Sin(a) * r);
                }
                GokuGraphics.Line(pts, R * (0.02f + 0.025f * Rand(i + 1120)), Fade(GasLight, (0.1f + 0.18f * Rand(i + 1130)) * live), whiteGlow, y + 0.015f, GokuGraphics.Taper.Both);
            }
            PaperBombGraphics.RingAt(h, R * 1.2f, Fade(GasLight, 0.2f * live), y + 0.016f, true, whiteGlow);   // the light rim hugging the disc
            PaperBombGraphics.RingAt(h, R * 1.13f, Fade(GasLight, 0.16f * live), y + 0.017f, true, whiteGlow);
            DrawMesh(disc, h, y + 0.02f, R, R, 0f, Fade(VergilGraphics.Void, live), solid);
            float q = R * RingQuad * 2f;
            Sprite(new Vector2(h.x + R * RingShift.x, h.y + R * RingShift.y), q, q, Fade(White, live), HoleRing, y + 0.03f);
            Plume(h, R, s, live, y + 0.04f);
        }

        /// <summary>The smoke streaming off the ring's east side: soft puffs drifting out and growing, and sparkles.</summary>
        private static void Plume(Vector2 h, float R, float s, float live, float y)
        {
            for (int i = 0; i < 18; i++)
            {
                float v = (s * 0.05f + i / 18f) % 1f, wob = R * 0.18f * Mathf.Sin(i * 1.7f + s * 0.4f);
                Vector2 p = PlumeAt(h, R, v);
                float size = R * (0.45f + 1.25f * v) * (0.8f + 0.4f * Rand(i + 1600)), a = live * 0.3f * Ease(0f, 0.12f, v) * (1f - Ease(0.65f, 1f, v));
                Color colour = i % 3 == 0 ? White : i % 3 == 1 ? GasLight : Smoke;
                Sprite(new Vector2(p.x, p.y + wob), size * 1.35f, size, Fade(colour, a), PuffGlow, y + i * 0.0004f,
                    Rand(i + 1610) * 360f + s * 7f * (i % 2 == 1 ? 1f : -1f));
            }
            for (int i = 0; i < 12; i++)
            {
                float v = 0.25f + 0.7f * Rand(i + 1650), twinkle = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(s * (1.5f + Rand(i + 1660) * 2f) + i));
                Vector2 p = PlumeAt(h, R, v);
                var at = new Vector2(p.x + R * (Rand(i + 1670) - 0.5f) * 1.4f * v, p.y + R * (Rand(i + 1680) - 0.5f) * 1.1f * v);
                Sprite(at, 0.16f, 0.16f, Fade(Smoke, 0.9f * twinkle * live), glow, y + 0.01f);
            }
        }

        private static Vector2 PlumeAt(Vector2 h, float R, float v) => new Vector2(h.x + R * (2.25f + 2.2f * v), h.y + R * (0.05f + 0.55f * v));

        /// <summary>The lab's ss(): smoothstep from a to b.</summary>
        private static float Ease(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// The speed-line tunnel at the opening (anime ep. 7 22-28 s, Cursed Clash 14-20 s): streaks rushing
        /// out of the vanishing point <paramref name="h"/>, where the black hole will open. A head moves out
        /// by the same factor each second from 0.6 cells to <paramref name="reach"/> (a warp tunnel seen from
        /// above: streaks speed up as they go), each of the <paramref name="count"/> streaks takes
        /// <paramref name="trip"/> s (0.8-1.2 x) and reaches back <paramref name="length"/> of its distance
        /// (0.75-1.25 x), sharp at the head, thin at the point end, wider with distance. They stop
        /// <paramref name="hollow"/> short of the point, so the far end of the tunnel is dark, and flicker 12
        /// times a second as redrawn anime speed lines do. <paramref name="alpha"/> 0..1.
        /// </summary>
        internal static void Tunnel(Vector2 h, float s, float alpha, int count, float length, float trip, float reach, float hollow, in VoidLayers layers)
        {
            if (alpha <= 0f) return;
            float k = Mathf.Log(reach / From);
            int frame = Mathf.FloorToInt(s * Flicker);
            DrawMesh(MeshPool.plane10, h, layers.Fog + 0.016f, reach * 2.2f, reach * 2.2f, 0f, Fade(TunnelTint, alpha), whiteGlow);   // the tint over the view
            Sprite(h, 14f, 14f, Fade(PointBloom, 0.14f * alpha), glow, layers.Fog + 0.017f);                                     // a faint bloom round the point
            glowRays.Clear();
            coreRays.Clear();
            for (int i = 0; i < count; i++)
            {
                float phase = (s / trip * (0.8f + 0.4f * Rand(i + 1920)) + Rand(i + 1930)) % 1f;
                float r1 = From * Mathf.Exp(k * phase), r0 = Mathf.Max(hollow * (0.85f + 0.3f * Rand(i + 1945)), r1 * (1f - length * (0.75f + 0.5f * Rand(i + 1940))));
                float a = alpha * Smooth(phase / 0.12f) * (1f - Smooth((phase - 0.9f) / 0.1f)) * (0.75f + 0.25f * Rand(i * 131 + frame * 7919 + 17));
                if (a <= 0.01f || r1 - r0 < 0.15f) continue;
                float ang = Rand(i + 1910) * Mathf.PI * 2f, w = (0.02f + 0.0065f * r1) * (0.6f + 0.8f * Rand(i + 1960)), bright = Rand(i + 1950);
                glowRays.Add(i % 4, GlowAlpha * (0.45f + 0.55f * bright) * a, ang, r0, r1, w * 3.2f);
                if (Rand(i + 1970) < Cored) coreRays.Add(i % 3, CoreAlpha * (0.5f + 0.5f * bright) * a, ang, r0, r1, w);
            }
            glowRays.Draw(h, GlowColours, layers.Fog + 0.02f);
            coreRays.Draw(h, CoreColours, layers.Fog + 0.021f);
        }

        /// <summary>
        /// The glitter dust between the streaks (Cursed Clash): 16 clouds stream out from the point more
        /// slowly than the lines and grow as they come, each a soft cloud stretched along its path with
        /// glitter twinkling in it and two specks crossed like stars.
        /// </summary>
        internal static void TunnelDust(Vector2 h, float s, float alpha, float trip, float reach, in VoidLayers layers)
        {
            if (alpha <= 0f) return;
            float k = Mathf.Log(reach / DustFrom);
            for (int c = 0; c < DustClouds; c++)
            {
                float phase = (s / (trip * DustSlower) + Rand(c + 2110)) % 1f, ang = Rand(c + 2100) * Mathf.PI * 2f;
                float r = DustFrom * Mathf.Exp(k * phase), size = 0.7f + 0.24f * r, grow = 0.7f + 0.09f * r;
                float fade = alpha * Smooth(phase / 0.15f) * (1f - Smooth((phase - 0.8f) / 0.2f));
                if (fade <= 0.01f) continue;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                Sprite(Along(h, ca, sa, r, 0f, 0f), size * 2.2f, size * 1.1f, Fade(DustHalo, 0.38f * fade), PuffGlow, layers.Fog + 0.022f,
                    -ang * Mathf.Rad2Deg + Rand(c + 2120) * 30f - 15f);
                for (int g = 0; g < DustGlitter; g++)
                {
                    int n = c * 31 + g;
                    Vector2 q = Along(h, ca, sa, r, (Rand(n + 2200) - 0.5f) * size * 1.9f, (Rand(n + 2300) - 0.5f) * size * 0.9f);
                    float twinkle = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(s * (5f + 6f * Rand(n + 2400)) + g * 1.7f)), gs = (0.06f + 0.1f * Rand(n + 2500)) * grow;
                    Sprite(q, gs * 2.4f, gs * 2.4f, Fade(White, twinkle * fade), glow, layers.Fog + 0.023f);
                    if (g < DustCrossed)
                        for (int x = 0; x < 2; x++) Sprite(q, gs * 0.35f, gs * 7f, Fade(White, 0.8f * twinkle * fade), glow, layers.Fog + 0.0232f, 45f + 90f * x);
                }
            }
        }

        private static Vector2 Along(Vector2 h, float ca, float sa, float r, float along, float across) =>
            new Vector2(h.x + ca * (r + along) - sa * across, h.y + sa * (r + along) + ca * across);

        /// <summary>
        /// The white light at the vanishing point (Cursed Clash 19 s): it grows as the lines finish and the
        /// black hole opens under it as it fades. <paramref name="a"/> 0..1.
        /// </summary>
        internal static void TunnelLight(Vector2 h, float a, in VoidLayers layers)
        {
            if (a <= 0f) return;
            Sprite(h, 22f * a, 22f * a, Fade(PointBloom, 0.22f * a), glow, layers.Fog + 0.03f);
            Sprite(h, 9f * a, 9f * a, Fade(LightHalo, 0.6f * a), glow, layers.Fog + 0.031f);
            Sprite(h, 1.2f + 3.2f * a, 1.2f + 3.2f * a, Fade(White, a), glow, layers.Fog + 0.032f);
            Sprite(h, 0.6f + 1.4f * a, 0.6f + 1.4f * a, Fade(White, a), glow, layers.Fog + 0.033f);
        }

        /// <summary>
        /// Streaks batched as the sketch batches them: one mesh per colour and brightness step (alpha in
        /// twelfths), about 40 meshes for 360 streaks instead of 360 draws. Each mesh is rewritten every
        /// frame with exactly that frame's streaks, relative to the point it is drawn at, so each is drawn
        /// once a frame.
        /// </summary>
        private sealed class RayBatch
        {
            private readonly List<Vector3>[] vertices;
            private readonly List<int>[] triangles;
            private readonly Mesh[] meshes;

            public RayBatch(int colours, string name)
            {
                int buckets = colours * RaySteps;
                vertices = new List<Vector3>[buckets];
                triangles = new List<int>[buckets];
                meshes = new Mesh[buckets];
                for (int b = 0; b < buckets; b++)
                {
                    vertices[b] = new List<Vector3>();
                    triangles[b] = new List<int>();
                    meshes[b] = new Mesh { name = name + " " + b };
                }
            }

            public void Clear()
            {
                for (int b = 0; b < vertices.Length; b++)
                {
                    vertices[b].Clear();
                    triangles[b].Clear();
                }
            }

            /// <summary>
            /// A streak straight out from the point at <paramref name="ang"/> radians, from r0 to r1: sharp
            /// at the head, widest just behind it, thinning to nothing at the point end.
            /// </summary>
            public void Add(int colour, float alpha, float ang, float r0, float r1, float width)
            {
                int step = GokuTiming.Round(alpha * RaySteps);
                if (step <= 0) return;
                if (step > RaySteps) step = RaySteps;
                int bucket = colour * RaySteps + step - 1;
                List<Vector3> v = vertices[bucket];
                List<int> tri = triangles[bucket];
                float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                int first = v.Count;
                for (int j = 0; j <= 5; j++)
                {
                    float u = j / 5f, r = r1 - (r1 - r0) * u, half = width / 2f * (j > 0 ? Mathf.Pow(1f - u, 0.8f) : 0.25f) + 0.003f;
                    v.Add(new Vector3(c * r - sn * half, 0f, sn * r + c * half));
                    v.Add(new Vector3(c * r + sn * half, 0f, sn * r - c * half));
                    if (j == 0) continue;
                    // Clockwise on screen, so the faces survive backface culling (the sketch's run the other way).
                    int n = first + j * 2;
                    tri.Add(n - 2); tri.Add(n - 1); tri.Add(n);
                    tri.Add(n - 1); tri.Add(n + 1); tri.Add(n);
                }
            }

            public void Draw(Vector2 at, Color[] palette, float altitude)
            {
                for (int b = 0; b < meshes.Length; b++)
                {
                    if (triangles[b].Count == 0) continue;
                    Mesh mesh = meshes[b];
                    mesh.Clear();
                    mesh.SetVertices(vertices[b]);
                    mesh.SetTriangles(triangles[b], 0);
                    DrawMesh(mesh, at, altitude, 1f, 1f, 0f, Fade(palette[b / RaySteps], (b % RaySteps + 1) / (float)RaySteps), whiteGlow);
                }
            }
        }
    }
}
