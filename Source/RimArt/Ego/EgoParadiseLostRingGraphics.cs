using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoParadiseLostGraphics;
using T = RimArt.EgoParadiseLostTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>One firing of WhiteNight's ring as the picture needs it.</summary>
    public struct EgoParadiseLostRingShot
    {
        /// <summary>The wielder's DrawPos: the ring's centre.</summary>
        public Vector2 Wielder;
        /// <summary>The ring's radius in cells, the rule's (the weapon's ringRadius in XML, placeholder 6): the disc spreads to exactly this.</summary>
        public float Radius;
    }

    /// <summary>
    /// WhiteNight's ring, the port of ring() in ego-paradise-lost-v2.js (Ruina's big move, shortened, with
    /// Lobotomy's spiked breach ring as its edge). A white cross of light on the wielder, a column 7 cells up and a
    /// bar 4.8 cells each side at chest height in a red glow, with a white flash, for 0.45 s. 40 red spikes shoot
    /// out along the floor from 0.7 cells to 2.4-4 cells and fade over 0.45 s. A filled red disc spreads from 0.4
    /// cells to the ring's radius in 0.5 s, brightest just inside its edge (the rim glow), with a red and a white
    /// ring and 28 spikes on its edge, and fades 0.35 s after. Then a pink-white flash over the area for 0.3 s.
    ///
    /// The cross is fixed to the screen (a column up, a bar across) and the rest are level circles and shapes on
    /// the floor, so there is no per-facing method.
    ///
    /// The rim glow is the sketch's lab texture lab/pl2-rim-glow: clear inside 0.35 of the radius, alpha
    /// ((r - 0.35) / 0.62)^2.2 out to 0.97, then down to 0 at the edge. It is a ring mesh built once whose UVs
    /// read the shipped SoftDisc texture (alpha (1 - q)^1.8 at UV radius q) at q = 1 - alpha^(1/1.8), so it takes
    /// the sketch's alpha at each of its 15 vertex rings and nearly that between them. No new texture: a
    /// Texture2D made in C# records in the VFX lab as "generated" (a magenta checker), and RimWorld's MoteGlow
    /// ignores vertex colours.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoParadiseLostRingGraphics
    {
        /// <summary>Radii of the rim glow's vertex rings, as a share of the ring's radius.</summary>
        private static readonly float[] RimRadii =
            { 0.35f, 0.42f, 0.49f, 0.56f, 0.63f, 0.70f, 0.77f, 0.84f, 0.90f, 0.94f, 0.97f, 0.9775f, 0.985f, 0.9925f, 1f };
        private const int RimSegments = 64;
        private static readonly Mesh rimGlow = RimGlowMesh();

        /// <summary>The ring <paramref name="age"/> s after it fired.</summary>
        public static void Draw(in EgoParadiseLostRingShot shot, float age, Map map)
        {
            if (age < 0f || age >= T.RingLength() || !Shown(shot.Wielder, map)) return;
            Begin(shot.Wielder);
            Vector2 c = shot.Wielder;
            float R = shot.Radius;
            var chest = new Vector2(c.x, c.y + 0.05f);
            if (age < T.CrossLife)
            {
                float a = age < 0.06f ? age / 0.06f : 1f - Smooth((age - 0.12f) / (T.CrossLife - 0.12f));
                var foot = new Vector2(c.x, c.y + PawnBody.Ground);
                var top = new Vector2(c.x, c.y + PawnBody.Ground + T.CrossUp * T.Lift);
                var side = new Vector2(T.CrossSide, 0f);
                Streak(foot, top, 1.6f, Fade(CrossRed, 0.5f * a), whiteGlow, Overhead + 0.2f);
                Streak(foot, top, 0.44f, Fade(White, a), whiteGlow, Overhead + 0.201f);
                Streak(chest - side, chest + side, 1.2f, Fade(CrossRed, 0.5f * a), whiteGlow, Overhead + 0.202f);
                Streak(chest - side, chest + side, 0.32f, Fade(White, a), whiteGlow, Overhead + 0.203f);
                Sprite(chest, 2.6f * a, 2.6f * a, Fade(White, 0.8f * a), glow, Overhead + 0.204f);
            }

            float sa = age - T.SpikeStart;
            if (sa >= 0f && sa < T.SpikeLife)
            {
                float grow = T.EaseOut(sa / 0.18f), fade = 1f - Smooth((sa - (T.SpikeLife - 0.25f)) / 0.25f), k = Mathf.Min(1f, R / T.SpikeRadius);
                for (int i = 0; i < T.Spikes; i++)
                {
                    Vector2 d = Turn(i * 360f / T.Spikes + 4f + 6f * (Rand(i + 500) - 0.5f));
                    float length = Mathf.Lerp(0.9f, (2.4f + 1.6f * Rand(i + 510)) * k, grow);
                    Vector2[] pts = GokuGraphics.Points(3);
                    pts[0] = c + d * 0.7f;
                    pts[1] = c + d * ((0.7f + length) / 2f);
                    pts[2] = c + d * length;
                    GokuGraphics.Line(pts, 0.7f, Fade(StarGlow, 0.55f * fade), whiteGlow, Floor + 0.06f + i * 0.00002f, Taper.End);
                    GokuGraphics.Line(pts, 0.3f, Fade(Star, 0.95f * fade), solid, Floor + 0.061f + i * 0.00002f, Taper.End);
                }
            }

            float ra = age - T.RingDelay;
            if (ra >= 0f && ra < T.Spread + T.RingLinger)
            {
                float x = ra / T.Spread, r = T.Front(ra, R), a = x < 1f ? 1f : 1f - (ra - T.Spread) / T.RingLinger;
                Disc(c, Floor + 0.063f, r, r, 0f, Fade(Star, 0.24f * a));
                DrawMesh(rimGlow, c, Floor + 0.068f, r, r, 0f, Fade(StarGlow, 0.85f * a), glow);
                PaperBombGraphics.RingAt(c, r, Fade(StarGlow, 0.5f * a), Floor + 0.07f, true, whiteGlow);
                PaperBombGraphics.RingAt(c, r, Fade(White, 0.85f * a), Floor + 0.071f, false, whiteGlow);
                for (int i = 0; i < T.EdgeSpikes; i++)
                {
                    Vector2 d = Turn(i * 360f / T.EdgeSpikes);
                    Spike(c + d * r, Left(d), 0.05f, c + d * (r + 0.28f * a), Fade(Star, 0.9f * a), Floor + 0.072f + i * 0.00002f);
                }
            }

            float fa = ra - T.Spread;
            if (fa >= 0f && fa < T.FlashLife)
            {
                float f = 1f - fa / T.FlashLife;
                Sprite(c, 2.4f * R, 2.4f * R, Fade(Pink, 0.6f * f), glow, Overhead + 0.19f);
                Sprite(c, 1.2f * R, 1.2f * R, Fade(White, 0.4f * f), glow, Overhead + 0.191f);
            }
        }

        /// <summary>The rim glow's alpha at a share <paramref name="r"/> of the radius: the sketch's lab/pl2-rim-glow.</summary>
        public static float RimAlpha(float r) =>
            r <= 0.97f ? Mathf.Pow(Mathf.Clamp01((r - 0.35f) / 0.62f), 2.2f) : Mathf.Max(0f, 1f - (r - 0.97f) / 0.03f);

        // A flat ring of radius 1 from 0.35 out, its UVs on the SoftDisc texture chosen so each vertex reads RimAlpha.
        // Triangles wound as VfxDraw.Ring's, which draws in game.
        private static Mesh RimGlowMesh()
        {
            int rings = RimRadii.Length, across = RimSegments + 1;
            var vertices = new Vector3[rings * across];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(rings - 1) * RimSegments * 6];
            for (int k = 0; k < rings; k++)
            {
                float rr = RimRadii[k], q = 1f - Mathf.Pow(RimAlpha(rr), 1f / 1.8f);
                for (int i = 0; i <= RimSegments; i++)
                {
                    float angle = i * Mathf.PI * 2f / RimSegments, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    vertices[k * across + i] = new Vector3(cos * rr, 0f, sin * rr);
                    uv[k * across + i] = new Vector2(0.5f + 0.5f * q * cos, 0.5f + 0.5f * q * sin);
                }
            }
            int t = 0;
            for (int k = 0; k + 1 < rings; k++)
                for (int i = 0; i < RimSegments; i++)
                {
                    int inner = k * across + i, outer = inner + across;
                    triangles[t++] = inner; triangles[t++] = inner + 1; triangles[t++] = outer;
                    triangles[t++] = outer; triangles[t++] = inner + 1; triangles[t++] = outer + 1;
                }
            var mesh = new Mesh { name = "Paradise Lost rim glow", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
