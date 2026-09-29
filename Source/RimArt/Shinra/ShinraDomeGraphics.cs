using System;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>The ground's colours for the dust, the pressed and scoured earth (the sketch's Ground dropdown).</summary>
    public struct ShinraPalette
    {
        public Color dust, lit, shade, scour;
        public static readonly ShinraPalette Soil = new ShinraPalette
        { dust = new Color(0.66f, 0.56f, 0.42f), lit = new Color(0.82f, 0.73f, 0.58f), shade = new Color(0.47f, 0.38f, 0.28f), scour = new Color(0.22f, 0.15f, 0.1f) };
        public static readonly ShinraPalette Sand = new ShinraPalette
        { dust = new Color(0.86f, 0.78f, 0.6f), lit = new Color(0.96f, 0.9f, 0.75f), shade = new Color(0.66f, 0.56f, 0.4f), scour = new Color(0.5f, 0.4f, 0.27f) };
        public static readonly ShinraPalette Snow = new ShinraPalette
        { dust = new Color(0.9f, 0.93f, 0.97f), lit = new Color(1f, 1f, 1f), shade = new Color(0.68f, 0.74f, 0.83f), scour = new Color(0.27f, 0.23f, 0.21f) };
    }

    /// <summary>
    /// Shinra Tensei v2, the port of the lab sketch pain-shinra-tensei.js (Naruto Mobile's dome); timing and geometry
    /// are <see cref="ShinraDome"/>. o is Pain's cell centre; everything is a level circle, a radial strip, a sprite
    /// or a line on the dome's surface, so there is no per-facing method.
    ///
    /// <see cref="Charge"/>: while held, the ground under his feet presses into a dark circle (0.4 -> 0.9 cells by
    /// charge) with a ragged rim, cracks past half charge, a white dust swirl and grit creeping in;
    /// <see cref="SizeRing"/> is the soft blue ring at the size a release now gives, pulsing at each step.
    /// <see cref="Burst"/>, e seconds after the burst: the scoured ground behind the front, scrape marks, the lip
    /// and clods at the edge, the pressed circle, the dust skirt, stones and leaves thrown out, the white flash,
    /// wind streaks, and the dome: a see-through ice-blue half sphere that forms in 0.16 s, overshoots and settles,
    /// holds for the shots window and swells 10 % as it fades. On it: a mottled fill, a bright blue rim, the wind
    /// low inside (a cloudy floor ring and 18 wisps under the pawns), a bright ring where it meets the floor, a
    /// highlight on the sun's side, a darker far limb, 12 lines that pour from the top and then flash in new places
    /// (never turning) and 2 ripples. A line's part facing the viewer is drawn over the pawns, the far wall faint
    /// under them. The screen warp (MoteLargeDistortionWave) is masked to the dome for the shots window.
    /// The ground marks stay and fade out by <see cref="ShinraDome.MarkSeconds"/>.
    /// <see cref="Palms"/>: the charge's pale-blue light in each open palm and the chakra glow round the body.
    ///
    /// Not here: the white-out (a screen overlay: MapComponent_ShinraCasts.MapComponentOnGUI), Pain's float (the
    /// clip), the pushed pawns (the game moves them), and the sketch's stand-ins.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ShinraDomeGraphics
    {
        private static readonly Color White = new Color(1f, 1f, 1f), Mist = new Color(0.86f, 0.9f, 0.96f), Shade = new Color(0.32f, 0.36f, 0.44f),
            IceBright = new Color(0.8f, 0.89f, 0.95f), SkyBlue = new Color(0.42f, 0.6f, 0.92f), Haze = new Color(0.55f, 0.62f, 0.72f),
            Leaf = new Color(0.36f, 0.55f, 0.2f), LeafDry = new Color(0.55f, 0.45f, 0.22f);

        private static readonly Material shellMat = MaterialPool.MatFrom("RimArt/Shinra/DomeShell", ShaderDatabase.MoteGlow);
        private static readonly Material fillMat = MaterialPool.MatFrom("RimArt/Shinra/DomeFill", ShaderDatabase.Transparent);
        private static readonly Material fillGlowMat = MaterialPool.MatFrom("RimArt/Shinra/DomeFill", ShaderDatabase.MoteGlow);
        private static readonly Material floorMat = MaterialPool.MatFrom("RimArt/Shinra/DomeFloor", ShaderDatabase.MoteGlow);
        private static readonly Material scourMat = MaterialPool.MatFrom("RimArt/Shinra/Scour", ShaderDatabase.Transparent);
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly Mesh shell = Shell();

        private static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        private static float Y => Overhead;
        private const float Tau = Mathf.PI * 2f, Lift = ShinraDome.Lift;

        private static float R(int i) => ShinraDome.Rand(i);
        private static float Smooth(float t) => ShinraDome.Smooth(t);
        private static float EaseOut(float x) => ShinraDome.EaseOut(x);
        private static float Bump(float x) => ShinraDome.Bump(x);
        /// <summary>JavaScript's Math.round: halves go up (Mathf.RoundToInt rounds them to even).</summary>
        private static int Round(float x) => (int)Math.Floor(x + 0.5);
        private static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        /// <summary>
        /// The shell: a unit quad split at the floor line, its north half <see cref="ShinraDome.DomeK"/> tall so the ring
        /// bulges north. Each half is written clockwise seen from above, as MeshPool's planes are.
        /// </summary>
        private static Mesh Shell()
        {
            float k = ShinraDome.DomeK;
            var mesh = new Mesh { name = "Shinra dome shell" };
            mesh.vertices = new[]
            {
                new Vector3(-1f, 0f, -1f), new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, -1f),
                new Vector3(-1f, 0f, 0f), new Vector3(-1f, 0f, k), new Vector3(1f, 0f, k), new Vector3(1f, 0f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0f),
                new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Line(Vector2[] pts, float width, Color colour, Material material, float altitude, GokuGraphics.Taper taper) =>
            GokuGraphics.Line(pts, width, colour, material, altitude, taper);

        // ---- charge ------------------------------------------------------------------------------------------

        /// <summary>
        /// The ground while charging: <paramref name="held"/> seconds held turn the swirl; <paramref name="c"/> is the
        /// charge 0..1.
        /// </summary>
        public static void Charge(Vector2 o, float held, float c, ShinraPalette col)
        {
            if (c <= 0f) return;
            Begin(o);
            float rp = Mathf.Lerp(0.4f, 0.9f, c);
            Pressed(o, rp, 0.45f, Mathf.Clamp01(c * 4f), col, 1f);
            RimCracks(o, rp, Mathf.Clamp01((c - 0.5f) * 2f), col, 1f);
            Color dust = Fade(Color.Lerp(col.lit, White, 0.6f), 0.4f * c);
            for (int i = 0; i < 10; i++)
            {
                float a = held * 8.5f + i / 10f * Tau, r = rp + 0.12f + 0.15f * R(i + 501);
                Sprite(o + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.9f + 0.08f), 0.32f, 0.24f, dust, puff, Y + 0.001f);
            }
            for (int k = 0; k < 2; k++)
            {
                Vector2[] pts = GokuGraphics.Points(9);
                for (int j = 0; j <= 8; j++)
                {
                    float a = held * 8.5f + k * Mathf.PI + j * 0.11f;
                    pts[j] = o + new Vector2(Mathf.Cos(a) * (rp + 0.2f), Mathf.Sin(a) * (rp + 0.2f) * 0.9f + 0.1f);
                }
                Line(pts, 0.05f, Fade(White, 0.45f * c), whiteGlow, Y + 0.002f, GokuGraphics.Taper.Both);
            }
            for (int i = 0; i < 14; i++)
            {
                float u = Mathf.Repeat(held * 1.1f + R(i + 520), 1f), r = Mathf.Lerp(1.6f, rp, Mathf.Pow(u, 1.5f));
                DrawMesh(disc, o + Dir(R(i + 540) * Tau) * r, Floor + 0.008f, 0.03f, 0.03f, 0f, Fade(col.scour, 0.8f * c * Bump(u)), solid);
            }
        }

        /// <summary>The quick version's short press under the feet in the last 0.12 s before its burst, k 0..1.</summary>
        public static void TapPress(Vector2 o, float k, ShinraPalette col)
        {
            if (k <= 0f) return;
            Begin(o);
            Pressed(o, 0.45f * k, 0.35f, k, col, 1f);
        }

        /// <summary>
        /// The soft ring on the ground at the size a release after <paramref name="held"/> s gives
        /// (<paramref name="sizes"/> by whole seconds), stepping out with a pulse.
        /// </summary>
        public static void SizeRing(Vector2 o, float held, float[] sizes)
        {
            Begin(o);
            float rr = ShinraDome.SizeRing(held, sizes, out float pulse) * (1f + 0.06f * pulse);
            float f = Mathf.Clamp01(held / 0.25f) * (0.2f + 0.35f * pulse);
            Vector2[] pts = Circle(o, rr, 72);
            Line(pts, 0.32f, Fade(SkyBlue, f), whiteGlow, Floor + 0.007f, GokuGraphics.Taper.None);
            Line(pts, 0.05f, Fade(IceBright, f * 0.5f), whiteGlow, Floor + 0.0071f, GokuGraphics.Taper.None);
            if (pulse > 0f) Sprite(o, rr * 2.1f, rr * 2.1f, Fade(SkyBlue, 0.12f * pulse), floorMat, Floor + 0.0069f);
        }

        /// <summary>
        /// The charge on Pain: a pale-blue light in each open palm and a chakra glow round the body.
        /// <paramref name="glowAt"/> and <paramref name="aura"/> are the sketch's (both 0 when not charging).
        /// Hands and body are where the clip draws them (x, z, altitude in y).
        /// </summary>
        public static void Palms(Vector3 body, Vector3 handA, Vector3 handB, float glowAt, float aura)
        {
            if (aura > 0f)
                Sprite(new Vector2(body.x, body.z), Mathf.Lerp(0.7f, 1.5f, Mathf.Min(1f, aura)), Mathf.Lerp(0.9f, 1.8f, Mathf.Min(1f, aura)),
                    Fade(SkyBlue, 0.3f * aura), glow, PawnLayer - 0.005f);
            if (glowAt <= 0f) return;
            float size = Mathf.Lerp(0.3f, 0.6f, Mathf.Min(1f, aura));
            for (int i = 0; i < 2; i++)
            {
                Vector3 h = i == 0 ? handA : handB;
                Vector2 at = new Vector2(h.x, h.z);
                Sprite(at, size, size * 0.85f, Fade(PainGraphics.PaleBlue, 0.55f * glowAt), glow, h.y + 0.002f + i * 0.0002f);
                Sprite(at, size * 0.35f, size * 0.3f, Fade(White, 0.6f * glowAt), glow, h.y + 0.0025f + i * 0.0002f);
            }
        }

        /// <summary>At the burst: the charged version's pale-blue flash at the chest, or the tap's flash at the cast hand; k 1 -> 0 over 0.1 s.</summary>
        public static void BurstFlash(Vector3 at, bool palm, float k)
        {
            if (k <= 0f) return;
            if (palm) Sprite(new Vector2(at.x, at.z), 0.7f, 0.6f, Fade(IceBright, 0.8f * k), glow, at.y + 0.003f);
            else Sprite(new Vector2(at.x, at.z + 0.04f), 0.7f, 0.6f, Fade(PainGraphics.PaleBlue, 0.35f * k), glow, at.y + 0.003f);
        }

        // ---- the burst ---------------------------------------------------------------------------------------

        /// <summary>
        /// Everything after the burst, e seconds in, round o. <paramref name="sun"/> is the map's shadow vector (cells
        /// per cell up). Draws the ground marks fading out to <see cref="ShinraDome.MarkSeconds"/>; call it until then.
        /// </summary>
        public static void Burst(Vector2 o, float e, ShinraDomeCast cast, ShinraPalette col, Vector2 sun)
        {
            if (e < 0f) return;
            Begin(o);
            float radius = cast.radius, c = cast.Look, m = ShinraDome.MarkAlpha(e);
            if (m <= 0f) return;
            // The ground: what stays.
            float F = ShinraDome.Front(e, radius);
            Sprite(o, F * 2f / 0.9f, F * 2f / 0.9f, Fade(col.scour, 0.55f * m), scourMat, Floor + 0.001f);   // the texture's edge is at 0.9
            Scrapes(o, F, radius, col, m);
            Lip(o, Smooth((e - ShinraDome.Wave * 0.7f) / 0.2f), radius, col, m);
            Pressed(o, cast.tap ? 0.45f : Mathf.Lerp(0.4f, 0.9f, c), 0.3f, 1f, col, m);
            RimCracks(o, Mathf.Lerp(0.4f, 0.9f, c), Mathf.Clamp01((c - 0.5f) * 2f), col, m);
            Stones(o, e, radius, c, col, sun, m);
            if (e > ShinraDome.Settled(cast)) return;

            // The dust skirt where the dome meets the floor, blown out and fading.
            DustSkirt(o, e, radius, c, F, col);

            // The air: flash, wind streaks, and the dome, held for the shots window, then gone.
            if (e < ShinraDome.FlashT)
            {
                float f = Mathf.Pow(1f - e / ShinraDome.FlashT, 2f);
                Sprite(o + new Vector2(0f, 0.35f), 1.6f + 2f * c, 1.4f + 1.8f * c, Fade(White, 0.9f * f), glow, Y + 0.06f);
            }
            WindStreaks(o, e, radius, ShinraDome.StreakAmount * Mathf.Lerp(0.6f, 1f, c));
            float defense = cast.Defense, fade = Smooth((e - (defense - 0.2f)) / 0.2f);
            float x = Mathf.Max(0f, e - ShinraDome.Wave), spring = 0.05f * Mathf.Exp(-7f * x) * Mathf.Sin(14f * x);   // overshoots about 0.2 cells, settles
            float rsD = F * (1f + spring + 0.1f * fade), domeA = Mathf.Clamp01(e / 0.03f) * (1f - fade);
            FloorWind(o, e, rsD, domeA * ShinraDome.DomeStrength);
            FloorRing(o, rsD, Mathf.Clamp01(e / 0.03f) * (1f - Smooth((e - (defense - 0.1f)) / 0.25f)) * ShinraDome.DomeStrength);
            Dome(o, e, cast, rsD, domeA, e < 0.2f ? Mathf.Pow(1f - e / 0.2f, 2f) : 0f, sun);
            Warp(o, e, rsD, defense);
        }

        /// <summary>The white-out's alpha e seconds after the burst (charged, not quick): a screen overlay, 0.12 s.</summary>
        public static float Whiteout(float e, ShinraDomeCast cast) =>
            cast.quick || e < 0f || e >= ShinraDome.WhiteoutT ? 0f : 0.5f * Mathf.Pow(1f - e / ShinraDome.WhiteoutT, 2f);

        // ---- the ground: pressed circle, scoured disc, scrape lines, lip ---------------------------------------

        /// <summary>The ground pressed down under Pain's feet: a dark circle with a ragged rim of pushed-up dust.</summary>
        private static void Pressed(Vector2 o, float rp, float dark, float alpha, ShinraPalette col, float m)
        {
            DrawMesh(disc, o, Floor + 0.006f, rp, rp, 0f, Fade(PainGraphics.Core, dark * alpha * m), solid);
            const int n = 48;
            Sides(n + 1, out Vector2[] a0, out Vector2[] a1);
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Tau, w = ShinraDome.Wob(a, 91);
                Vector2 d = Dir(a);
                a0[i] = o + d * (rp * 0.96f);
                a1[i] = o + d * (rp + 0.03f + 0.12f * w * w);
            }
            Strip(a0, a1, Fade(col.lit, 0.4f * alpha * m), solid, Floor + 0.0055f);
        }

        /// <summary>Cracks that run out from the pressed circle's rim once the charge passes half.</summary>
        private static void RimCracks(Vector2 o, float rp, float amount, ShinraPalette col, float m)
        {
            if (amount <= 0f) return;
            for (int i = 0; i < 7; i++)
            {
                float a = (i + R(i * 3 + 901)) / 7f * Tau, len = (0.2f + 0.35f * R(i * 3 + 902)) * amount, kink = (R(i * 3 + 903) - 0.5f) * 0.5f;
                float p0 = rp + 0.02f;
                Vector2[] pts = GokuGraphics.Points(3);
                for (int j = 0; j < 3; j++)
                {
                    float u = j * 0.5f;
                    pts[j] = o + Dir(a + kink * u * u) * (p0 + len * u);
                }
                Line(pts, 0.045f, Fade(col.scour, 0.75f * m), null, Floor + 0.0058f, GokuGraphics.Taper.End);
            }
        }

        /// <summary>
        /// The edge of the scoured circle: a ragged dark inner slope, a broken low ridge of pushed soil, and clods of
        /// earth along it. k 0..1 raises it.
        /// </summary>
        private static void Lip(Vector2 o, float k, float radius, ShinraPalette col, float m)
        {
            if (k <= 0f) return;
            const int n = 160;
            Sides(n + 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                float ang = i / (float)n * Tau;
                Vector2 d = Dir(ang);
                a[i] = o + d * (radius - (0.1f + 0.22f * ShinraDome.Wob(ang, 51)) * k);
                b[i] = o + d * (radius + (0.04f * ShinraDome.Wob(ang, 71) - 0.02f) * k);
            }
            Strip(a, b, Fade(col.scour, 0.4f * k * m), solid, Floor + 0.004f);
            Sides(n + 1, out a, out b);
            for (int i = 0; i <= n; i++)
            {
                float ang = i / (float)n * Tau, r1 = radius + (0.04f * ShinraDome.Wob(ang, 71) - 0.02f) * k;
                Vector2 d = Dir(ang);
                a[i] = o + d * r1;
                b[i] = o + d * (r1 + (0.02f + 0.2f * Mathf.Pow(ShinraDome.Wob(ang, 61), 2f)) * k);
            }
            Strip(a, b, Fade(col.lit, 0.32f * k * m), solid, Floor + 0.0042f);
            int clods = Round(64f * radius / 4f);
            for (int i = 0; i < clods; i++)
            {
                float ang = (i + R(i * 5 + 701)) / clods * Tau, r = radius + (R(i * 5 + 702) - 0.35f) * 0.34f, size = (0.07f + 0.13f * R(i * 5 + 703)) * k;
                PaperBombGraphics.Rock(o + Dir(ang) * r, size, R(i * 5 + 704) * 360f, k * m, i * 2 + 1, Floor + 0.02f);
            }
        }

        /// <summary>
        /// Scrape marks on the scoured floor: short broken grooves pointing outward at scattered radii, and grit
        /// left behind. F is the front's radius so far.
        /// </summary>
        private static void Scrapes(Vector2 o, float F, float radius, ShinraPalette col, float m)
        {
            float k = radius / 4f;
            for (int i = 0; i < ShinraDome.Scrapes; i++)
            {
                float a = R(i * 7 + 1) * Tau, r0 = (1.1f + R(i * 7 + 2) * 2.2f) * k, len = (0.45f + 1.05f * R(i * 7 + 3)) * k;
                float c = Mathf.Cos(a), s = Mathf.Sin(a), w = 0.035f + 0.045f * R(i * 7 + 4), bend = (R(i * 7 + 5) - 0.5f) * 0.12f;
                for (int d = 0; d < 2; d++)
                {
                    float u0 = d == 0 ? 0f : 0.55f, u1 = d == 0 ? 0.42f : 1f;
                    float a0 = r0 + len * u0, a1 = Mathf.Min(radius - 0.2f, Mathf.Min(r0 + len * u1, F));
                    if (a1 <= a0 + 0.05f) continue;
                    Vector2 p0 = Groove(o, c, s, bend, a0, a1, u0, u1, 0f), p1 = Groove(o, c, s, bend, a0, a1, u0, u1, 0.5f), p2 = Groove(o, c, s, bend, a0, a1, u0, u1, 1f);
                    Vector2[] pts = GokuGraphics.Points(3);
                    pts[0] = p0; pts[1] = p1; pts[2] = p2;
                    Line(pts, w, Fade(col.scour, 0.38f * m), null, Floor + 0.003f, GokuGraphics.Taper.Both);
                    Vector2 lit = new Vector2(-s * 0.045f, c * 0.045f);
                    pts[0] = p0 + lit; pts[1] = p1 + lit; pts[2] = p2 + lit;
                    Line(pts, w * 0.6f, Fade(col.lit, 0.22f * m), null, Floor + 0.0031f, GokuGraphics.Taper.Both);
                }
            }
            for (int i = 0; i < 46; i++)
            {
                float r = (1f + 2.8f * Mathf.Sqrt(R(i * 3 + 802))) * radius / 4f;
                if (r > F) continue;
                PaperBombGraphics.Rock(o + Dir(R(i * 3 + 801) * Tau) * r, 0.045f + 0.05f * R(i * 3 + 803), R(i * 3 + 804) * 360f, 0.85f * m, i, Floor + 0.019f);
            }
        }

        private static Vector2 Groove(Vector2 o, float c, float s, float bend, float a0, float a1, float u0, float u1, float u)
        {
            float r = Mathf.Lerp(a0, a1, u), q = u0 + (u1 - u0) * u, side = bend * Bump(q);
            return o + new Vector2(c * r - s * side, s * r + c * side);
        }

        // ---- dust, stones, leaves -----------------------------------------------------------------------------

        /// <summary>The grey-white dust skirt where the dome meets the floor. rs is the front's radius.</summary>
        private static void DustSkirt(Vector2 o, float e, float radius, float c, float rs, ShinraPalette col)
        {
            float A = Mathf.Clamp01(e / 0.05f) * (1f - Smooth((e - 0.5f) / ShinraDome.DustFade));
            if (A <= 0f || rs <= 0.1f) return;
            Color grey = Color.Lerp(col.dust, White, 0.5f), pale = Color.Lerp(col.lit, White, 0.7f);
            float late = Mathf.Max(0f, e - ShinraDome.Wave);
            for (int i = 0; i < 48; i++)
            {
                float a = (i + R(i * 9 + 201)) / 48f * Tau;
                float r = rs - 0.25f * R(i * 9 + 202) + ShinraDome.DustDrift * radius / 4f * EaseOut(late / 2f) * (0.5f + R(i * 9 + 206));
                float h = (0.1f + 0.5f * R(i * 9 + 203)) * Mathf.Clamp01(e / 0.9f);
                float size = (0.5f + 0.6f * R(i * 9 + 204)) * Mathf.Lerp(0.7f, 1.1f, c) * (1f + 0.6f * Mathf.Clamp01(late / 1.5f));
                Sprite(o + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r + h * Lift), size, size * 0.75f,
                    Fade(R(i * 9 + 205) < 0.5f ? grey : pale, 0.3f * A), puff, Y + 0.004f + i * 0.0001f);
            }
        }

        /// <summary>Stones thrown out as the front reaches them, landing and staying; leaves and grass bits blown out low.</summary>
        private static void Stones(Vector2 o, float e, float radius, float c, ShinraPalette col, Vector2 sun, float m)
        {
            int n = Round(ShinraDome.Debris * Mathf.Lerp(0.35f, 1f, c));
            for (int i = 0; i < n; i++)
            {
                float a = R(i * 11 + 601) * Tau, r0 = (0.5f + R(i * 11 + 602) * 2.8f) * radius / 4f;
                float r1 = Mathf.Max(r0 + 1.2f, radius + Mathf.Lerp(0.3f, 2.2f, R(i * 11 + 603)));
                float e0 = ShinraDome.Reaches(r0, radius), dur = 0.35f + 0.35f * R(i * 11 + 604), H = (0.3f + 0.9f * R(i * 11 + 605)) * Mathf.Lerp(0.6f, 1.2f, c);
                float size = 0.16f + 0.18f * R(i * 11 + 606), u = (e - e0) / dur;
                Vector2 d = Dir(a);
                if (u <= 0f) continue;
                if (u >= 1f)
                {
                    PaperBombGraphics.Rock(o + d * r1, size, R(i * 11 + 607) * 360f, m, i, Floor + 0.02f);
                    float since = (u - 1f) * dur;
                    if (since < 0.3f)
                        Sprite(o + d * r1 + new Vector2(0f, 0.05f), 0.4f + since, 0.3f + since, Fade(i % 2 == 1 ? col.lit : col.dust, 0.5f * (1f - since / 0.3f)), puff, Y + 0.003f);
                    continue;
                }
                float h = 4f * H * u * (1f - u);
                Vector2 at = o + d * Mathf.Lerp(r0, r1, u);
                Sprite(at + sun * h, size * 1.2f, size * 0.9f, Fade(Ink, 0.35f), soft, ShadowLayer);
                PaperBombGraphics.Rock(at + new Vector2(0f, h * Lift), size, R(i * 11 + 607) * 360f + u * 540f * (i % 2 == 1 ? 1f : -1f), 1f, i, Y + 0.01f);
            }
            for (int i = 0, count = Round(8f + 8f * c); i < count; i++)
            {
                float a = R(i * 11 + 901) * Tau, r0 = (1f + R(i * 11 + 902) * 2.6f) * radius / 4f, r1 = r0 + 2.4f + 2f * R(i * 11 + 903);
                float u = (e - ShinraDome.Reaches(r0, radius)) / (0.6f + 0.4f * R(i * 11 + 904));
                if (u <= 0f) continue;
                float k = Mathf.Min(1f, u), r = Mathf.Lerp(r0, r1, EaseOut(k)), h = (0.4f + 0.6f * R(i * 11 + 905)) * Bump(k);
                float sway = Mathf.Sin(e * 19f + i * 2.3f) * 0.08f * (1f - k), ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Vector2 at = o + new Vector2(ca * r - sa * sway, sa * r + ca * sway + h * Lift);
                float deg = u < 1f ? (e * 420f + i * 47f) % 360f : R(i * 11 + 906) * 360f;
                DrawMesh(disc, at, u < 1f ? Y + 0.012f : Floor + 0.021f, 0.075f, 0.035f, deg, Fade(R(i * 11 + 907) < 0.6f ? Leaf : LeafDry, u < 1f ? 1f : m), solid);
            }
        }

        // ---- air: wind streaks, the dome -----------------------------------------------------------------------

        private static void WindStreaks(Vector2 o, float e, float radius, float alpha)
        {
            if (alpha <= 0f) return;
            float late = Mathf.Max(0f, e - ShinraDome.Wave);
            for (int i = 0; i < ShinraDome.Streaks; i++)
            {
                float a = R(i * 13 + 301) * Tau, len = 0.7f + 1.1f * R(i * 13 + 302), h = 1.4f * R(i * 13 + 303) * R(i * 13 + 304);
                float head = (e < ShinraDome.Wave ? ShinraDome.Front(e, radius) : radius + late * 7f) + 0.3f * R(i * 13 + 305);
                float tail = Mathf.Max(0f, head - len * Mathf.Clamp01(e / 0.08f)), f = alpha * (0.45f + 0.55f * R(i * 13 + 306)) * (1f - Smooth(late / 0.28f));
                if (f <= 0f || head - tail < 0.05f) continue;
                Vector2 d = Dir(a), lift = new Vector2(0f, h * Lift);
                Streak(o + d * tail + lift, o + d * head + lift, 0.028f + 0.025f * R(i * 13 + 307), Fade(White, 0.38f * f), whiteGlow, Y + 0.03f, 6);
            }
        }

        /// <summary>The wind low inside the dome, seen from above: a cloudy ring on the floor and wisps racing outward along it.</summary>
        private static void FloorWind(Vector2 o, float e, float rs, float a)
        {
            if (a <= 0f || rs <= 0.1f) return;
            Sprite(o, rs * 2f, rs * 2f, Fade(SkyBlue, 0.3f * a), floorMat, PawnLayer - 0.016f);
            for (int k = 0; k < ShinraDome.Wisps; k++)
            {
                float period = 0.4f * (0.8f + 0.4f * R(k * 31 + 1)), x = e / period + R(k * 31 + 2);
                int g = Mathf.FloorToInt(x), seed = k * 71 + g * 17;
                float u = x - g;
                float ang = R(seed + 1) * Tau + (R(seed + 2) - 0.5f) * 0.6f * u;          // a slight curl, never a turn
                float r = rs * Mathf.Lerp(0.35f + 0.3f * R(seed + 3), 0.98f, EaseOut(u)), h = 0.1f + 0.35f * R(seed + 4);
                float len = (0.7f + 0.8f * R(seed + 5)) * (0.6f + 0.6f * u) * rs / 4f, wid = 0.22f + 0.15f * R(seed + 6);
                float dir = ang + Mathf.PI / 2f * 0.75f * (R(seed + 7) < 0.5f ? 1f : -1f);    // along the ring, leaning outward
                Sprite(o + Dir(ang) * r + new Vector2(0f, h * Lift), len, wid, Fade(White, 0.24f * a * Bump(u)), puff, PawnLayer - 0.015f, -dir * Mathf.Rad2Deg);
            }
        }

        /// <summary>A bright soft ring where the dome stands on the floor; it outlasts the dome by about 0.15 s.</summary>
        private static void FloorRing(Vector2 o, float rs, float a)
        {
            if (a <= 0f || rs <= 0.1f) return;
            Vector2[] pts = Circle(o, rs, 64);
            Line(pts, 0.6f, Fade(SkyBlue, 0.26f * a), whiteGlow, Y + 0.0215f, GokuGraphics.Taper.None);
            Line(pts, 0.12f, Fade(IceBright, 0.14f * a), whiteGlow, Y + 0.0216f, GokuGraphics.Taper.None);
        }

        private static void Dome(Vector2 o, float e, ShinraDomeCast cast, float rs, float alpha, float flash, Vector2 sun)
        {
            if (alpha <= 0f || rs <= 0.1f) return;
            float a = alpha * ShinraDome.DomeStrength;
            DrawMesh(shell, o, Y + 0.02f, rs, rs, 0f, Fade(Haze, Mathf.Min(1f, 0.14f * a + 0.35f * flash)), fillMat);
            if (flash > 0f) DrawMesh(shell, o, Y + 0.0205f, rs, rs, 0f, Fade(White, 0.85f * flash), fillGlowMat);
            float shimmer = 1f + 0.08f * Mathf.Sin(e * 71f) + 0.05f * Mathf.Sin(e * 113f + 1.3f);
            DrawMesh(shell, o, Y + 0.021f, rs * 1.03f, rs * 1.03f, 0f, Fade(SkyBlue, 0.42f * a * shimmer), shellMat);
            DrawMesh(shell, o, Y + 0.0211f, rs * 0.99f, rs * 0.99f, 0f, Fade(IceBright, 0.1f * a * shimmer), shellMat);
            // Light: a highlight high on the sun's side, the limb away from the sun a shade darker.
            float lightAz = Mathf.Atan2(-sun.y, -sun.x);
            Sprite(o + ShinraDome.DomeAt(rs, lightAz, 0.95f, out _), rs * 0.75f, rs * 0.55f, Fade(White, 0.4f * a), glow, Y + 0.022f);
            for (int i = -2; i <= 2; i++)
                Sprite(o + ShinraDome.DomeAt(rs * 0.9f, lightAz + Mathf.PI + i * 0.42f, 0.3f + 0.12f * Mathf.Abs(i), out _), rs * 0.55f, rs * 0.38f,
                    Fade(Shade, 0.13f * a), soft, Y + 0.0212f);
            // Two pressure ripples roll from the top down to the floor, widening as they go.
            for (int i = 0; i < ShinraDome.Ripples.Length; i++)
            {
                float u = (e - ShinraDome.Ripples[i]) / ShinraDome.RippleLife;
                if (u <= 0f || u >= 1f) continue;
                float el = Mathf.PI / 2f * (1f - EaseOut(u)) + 0.04f;
                for (int j = 0; j <= 48; j++) surface[j] = o + ShinraDome.DomeAt(rs, j / 48f * Tau, el, out front[j]);
                SurfaceLine(49, 0.5f, 0.12f, a * Bump(u) * 1.3f, false, 0.15f);
            }
            // Surface lines: for the pour they run from the top down to the floor as the dome fills; after that
            // each one flashes along a short path somewhere new, runs, and is gone.
            float pour = cast.Pour;
            for (int k = 0; k < ShinraDome.Swirls; k++)
            {
                float u, az0, el0, dAz, dEl, outward = 1f;
                if (e < pour)
                {
                    u = e / pour;
                    az0 = (k + R(k * 23 + 1)) / ShinraDome.Swirls * Tau; el0 = 1.45f; dAz = (R(k * 23 + 2) - 0.5f) * 0.9f; dEl = -(1.3f + 0.1f * R(k * 23 + 3));
                }
                else
                {
                    float period = ShinraDome.FlashLife * (0.8f + 0.4f * R(k * 23 + 4)), x = (e - pour) / period + R(k * 23 + 5);
                    int g = Mathf.FloorToInt(x), seed = k * 97 + g * 13;
                    u = x - g;
                    bool down = R(seed + 1) < 0.5f;
                    az0 = R(seed + 2) * Tau;
                    el0 = down ? 0.75f + 0.6f * R(seed + 3) : 0.15f + 0.65f * R(seed + 3);
                    dAz = down ? (R(seed + 4) - 0.5f) * 0.7f : (R(seed + 4) < 0.5f ? -1f : 1f) * (0.9f + 0.8f * R(seed + 5));
                    dEl = down ? -(el0 - 0.05f - 0.1f * R(seed + 6)) : (R(seed + 6) - 0.5f) * 0.25f;
                    outward = down ? 1f : 1.04f + 0.08f * R(seed + 7);      // a swoosh wraps just outside the dome
                }
                // The head runs out along the path, the tail follows it and they meet at the end.
                float head = EaseOut(Mathf.Clamp01(u / 0.55f)), tail = Smooth(Mathf.Clamp01((u - 0.3f) / 0.7f));
                if (head - tail < 0.03f) continue;
                for (int j = 0; j <= 16; j++)
                {
                    float w = Mathf.Lerp(tail, head, j / 16f);
                    surface[j] = o + ShinraDome.DomeAt(rs * outward, az0 + dAz * w, Mathf.Max(0.03f, Mathf.Min(1.52f, el0 + dEl * w)), out front[j]);
                }
                SurfaceLine(17, 0.16f, 0.045f, a * (0.55f + 0.45f * R(k * 23 + 7)) * Mathf.Clamp01(u / 0.08f) * (1f - tail * 0.6f), true, 1f);
            }
        }

        private static readonly Vector2[] surface = new Vector2[49];
        private static readonly bool[] front = new bool[49];

        /// <summary>
        /// The first <paramref name="count"/> points of <see cref="surface"/> as a line on the dome: the runs facing the
        /// viewer bright over the pawns, the far-wall runs faint under them. Each run after the first starts at the
        /// previous run's last point. taper false keeps the width (a closed ripple ring).
        /// </summary>
        private static void SurfaceLine(int count, float glowW, float coreW, float f, bool taper, float coreK)
        {
            if (f <= 0f) return;
            var t = taper ? GokuGraphics.Taper.Both : GokuGraphics.Taper.None;
            int start = 0;
            for (int i = 1; i <= count; i++)
            {
                if (i < count && front[i] == front[start]) continue;
                int from = start > 0 ? start - 1 : start, len = i - from;
                if (len >= 3)
                {
                    Vector2[] pts = GokuGraphics.Points(len);
                    Array.Copy(surface, from, pts, 0, len);
                    if (front[start])
                    {
                        Line(pts, glowW, Fade(White, 0.1f * f), whiteGlow, Y + 0.03f, t);
                        Line(pts, coreW, Fade(White, 0.42f * f * coreK), whiteGlow, Y + 0.031f, t);
                    }
                    else
                    {
                        Line(pts, glowW * 0.65f, Fade(Mist, 0.05f * f), whiteGlow, PawnLayer - 0.012f, t);
                        Line(pts, coreW * 0.7f, Fade(Mist, 0.2f * f * coreK), whiteGlow, PawnLayer - 0.011f, t);
                    }
                }
                start = i;
            }
        }

        private static Vector2[] Circle(Vector2 o, float r, int segments)
        {
            Vector2[] pts = GokuGraphics.Points(segments + 1);
            for (int j = 0; j <= segments; j++) pts[j] = o + Dir(j / (float)segments * Tau) * r;
            return pts;
        }

        // ---- the screen warp -------------------------------------------------------------------------------------

        private static Material distortion;
        private static bool distortionResolved;
        private static readonly MaterialPropertyBlock WarpProperties = new MaterialPropertyBlock();

        /// <summary>
        /// The game's MoteLargeDistortionWave shader with the core distortion and noise maps, masked by
        /// RimArt/Shinra/Distort. Optional: a missing shader or texture leaves the rest of the picture.
        /// </summary>
        private static Material Distortion()
        {
            if (distortionResolved) return distortion;
            distortionResolved = true;
            try
            {
                Shader shader = DefDatabase<ShaderTypeDef>.GetNamedSilentFail("MoteLargeDistortionWave")?.Shader;
                Texture2D currents = ContentFinder<Texture2D>.Get("Things/Mote/PsychicDistortionCurrents", false);
                Texture2D noise = ContentFinder<Texture2D>.Get("Things/Mote/PsycastNoise", false);
                Texture2D mask = ContentFinder<Texture2D>.Get("RimArt/Shinra/Distort", false);
                if (shader == null || currents == null || noise == null || mask == null)
                {
                    Log.Warning("[RimArt] Shinra Tensei found no distortion shader or maps; drawing without the warp.");
                    return null;
                }
                distortion = new Material(shader) { mainTexture = mask };
                distortion.SetTexture("_DistortionTex", currents);
                distortion.SetTexture("_NoiseTex", noise);
                distortion.SetFloat("_distortionIntensity", 0.12f);
                return distortion;
            }
            catch (Exception ex)
            {
                Log.Warning("[RimArt] Shinra Tensei could not build its distortion material: " + ex);
                return null;
            }
        }

        /// <summary>The warp over the dome for the shots window: in over 0.05 s, out as the dome fades.</summary>
        private static void Warp(Vector2 o, float e, float rs, float defense)
        {
            float intensity = Smooth(e / 0.05f) * (1f - Smooth((e - (defense - 0.2f)) / 0.25f));
            if (intensity <= 0f || rs <= 0.1f) return;
            Material material = Distortion();
            if (material == null) return;
            WarpProperties.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, intensity));
            WarpProperties.SetFloat(ShaderPropertyIDs.AgeSecs, e);
            float size = rs * 2.3f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(new Vector3(o.x, Y + 0.018f, o.y), Quaternion.identity,
                new Vector3(size, 1f, size)), material, 0, null, 0, WarpProperties);
        }
    }
}
