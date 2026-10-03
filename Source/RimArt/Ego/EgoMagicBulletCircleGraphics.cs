using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoMagicBulletGraphics;
using T = RimArt.EgoMagicBulletTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Magic Bullet's magic circle, as the Limbus frames draw it, and the corroded look that reuses it flat on
    /// the floor. A circle is a soft blue fill and glow, an outer double ring, a ring of 36 rune ticks, a
    /// hexagram, an inner ring and a centre sigil, all thin bright lines, spinning. Every point of it is
    /// c + H r cos(t) + V r sin(t), where H and V are the circle's two axes on screen, so one drawing serves a
    /// flat ring (H east, V north) and a gate standing across the aim (<see cref="GateAxes"/>). A standing one
    /// gets a ground shadow along the sun, a dark back rim 0.04 north for thickness and a lit top rim.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class EgoMagicBulletCircleGraphics
    {
        /// <summary>Points round a ring: 48 segments, as the sketch.</summary>
        private const int N = 48;
        /// <summary>The gate's face on screen, |H x V|: what a 40-degree lean gives facing east.</summary>
        private static readonly float GateFace = Mathf.Sin(40f * Mathf.Deg2Rad);

        // The circle being drawn, as locals for On.
        private static Vector2 centre, axisH, axisV;

        private static Vector2 On(float t, float rad) => On(t, rad, centre, axisH, axisV);

        private static Vector2 On(float t, float rad, Vector2 q, Vector2 h, Vector2 v) =>
            new Vector2(q.x + h.x * rad * Mathf.Cos(t) + v.x * rad * Mathf.Sin(t), q.y + h.y * rad * Mathf.Cos(t) + v.y * rad * Mathf.Sin(t));

        /// <summary>
        /// The screen axes of a gate facing <paramref name="dir"/>: H across the aim, V up. Facing east or west H is
        /// pure north, which the projection would fold into V, so the gate leans back, its top toward the shooter.
        /// The lean is solved per aim so the face on screen is always <see cref="GateFace"/>: sin(lean) - k cos(lean)
        /// = GateFace with k = Lift x north part of the aim, which gives about 2 degrees facing south, 40 facing east
        /// or west and 64 facing north (there leaning toward the shooter works against the lift). One formula for
        /// every aim, so nothing flips between facings. Capped at 80 degrees.
        /// </summary>
        internal static void GateAxes(Vector2 dir, out Vector2 H, out Vector2 V)
        {
            H = new Vector2(-dir.y, dir.x);
            float k = T.Lift * dir.y;
            float lean = Mathf.Min(80f * Mathf.Deg2Rad, Mathf.Max(0f, Mathf.Asin(Mathf.Min(1f, GateFace / Mathf.Sqrt(1f + k * k))) + Mathf.Atan(k)));
            float cl = Mathf.Cos(lean), sl = Mathf.Sin(lean);
            V = new Vector2(-sl * dir.x, T.Lift * cl - sl * dir.y);
        }

        /// <summary>
        /// One circle at <paramref name="c"/>, radius <paramref name="r"/> x <paramref name="open"/>. <paramref name="k"/>
        /// sets its spin direction (odd 0.9 rad/s, even -0.7) and phase, and seeds its sparks. <paramref name="hTrue"/> is
        /// the true across direction, for the shadow. Everything is drawn from <paramref name="altitude"/> up to
        /// +0.003; <paramref name="dim"/> scales every alpha.
        /// </summary>
        internal static void Draw(Vector2 c, float r, float open, float s, int k, Vector2 H, Vector2 V, Vector2 hTrue, bool stands,
            Vector2 sun, float strength, float altitude, float dim = 1f)
        {
            if (open <= 0f) return;
            float rr = r * open, a = Mathf.Min(1f, open * 1.5f) * dim, spin = s * (k % 2 == 1 ? 0.9f : -0.7f) + k, L = altitude;
            centre = c;
            axisH = H;
            axisV = V;
            // The glow sprite's box round the ellipse.
            float gw = 3f * rr * Mathf.Sqrt(H.x * H.x + V.x * V.x), gh = 3f * rr * Mathf.Sqrt(H.y * H.y + V.y * V.y);
            if (stands)
            {
                Disc(new Vector2(c.x + sun.x * 0.5f, c.y - PawnBody.Chest + sun.y * 0.5f), rr, 0.9f * strength * a, ShadowLayer, ChainSickleGraphics.Body, hTrue, new Vector2(0f, 0.25f));
                var back = new Vector2(c.x, c.y + 0.04f);
                Ring(back, rr, 0.04f, 0.7f * a, L + 0.0005f, CircleDeep);
                Ring(back, rr * 0.80f, 0.03f, 0.5f * a, L + 0.0005f, CircleDeep);
            }
            Sprite(c, gw, gh, Fade(CircleDeep, 0.30f * a), glow, L);
            Disc(c, rr * 0.98f, 0.28f * a, L + 0.0001f, CircleBlue, H, V);   // the translucent fill
            Ring(c, rr, 0.035f, 1.0f * a, L + 0.001f, CircleBright);
            Ring(c, rr * 0.955f, 0.02f, 0.55f * a, L + 0.001f, CircleBlue);
            Ring(c, rr * 0.80f, 0.03f, 0.9f * a, L + 0.001f, CircleBlue);    // the rune ring
            Ring(c, rr * 0.62f, 0.03f, 0.8f * a, L + 0.001f, CircleBright);
            Ring(c, rr * 0.36f, 0.03f, 0.8f * a, L + 0.001f, CircleBright);
            Ring(c, rr * 0.30f, 0.02f, 0.5f * a, L + 0.001f, CircleBlue);
            if (stands) Sprite(On(Mathf.PI / 2f, rr * 0.88f), gw * 0.4f, rr * 0.3f, Fade(White, 0.35f * a), glow, L + 0.0011f);   // the lit top rim
            // The rune ticks between 0.80 and 0.955 of the radius, every third one long.
            for (int i = 0; i < 36; i++)
            {
                float t = spin + i / 36f * Tau, outer = rr * (i % 3 != 0 ? 0.90f : 0.955f);
                Streak(On(t, rr * 0.80f), On(t, outer), 0.02f, Fade(CircleBright, 0.6f * a), whiteGlow, L + 0.002f, 3);
            }
            // The hexagram: two triangles inscribed in the 0.62 ring.
            for (int tri = 0; tri < 2; tri++)
            {
                Vector2[] pts = GokuGraphics.Points(4);
                for (int i = 0; i <= 3; i++) pts[i] = On(-spin * 0.5f + tri * Mathf.PI / 3f + i * Tau / 3f + Mathf.PI / 2f, rr * 0.62f);
                GokuGraphics.Line(pts, 0.025f, Fade(CircleBright, 0.6f * a), whiteGlow, L + 0.0015f, Taper.None);
            }
            // The centre sigil: a square in the inner ring, and a dot.
            Vector2[] sq = GokuGraphics.Points(5);
            for (int i = 0; i <= 4; i++) sq[i] = On(spin + i * Tau / 4f + Mathf.PI / 4f, rr * 0.30f);
            GokuGraphics.Line(sq, 0.022f, Fade(CircleBright, 0.6f * a), whiteGlow, L + 0.0015f, Taper.None);
            Sprite(c, gw * 0.1f, gh * 0.1f, Fade(CircleBlue, 0.5f * a), glow, L + 0.0025f);
            Sprite(c, gw * 0.035f, gh * 0.035f, Fade(White, 0.7f * a), glow, L + 0.0026f);
            // Six rune dots on the 0.71 ring, spinning the other way.
            for (int i = 0; i < 6; i++) Sprite(On(-spin * 0.7f + i / 6f * Tau, rr * 0.71f), 0.04f, 0.036f, Fade(White, 0.6f * a), glow, L + 0.0025f);
            // Four white sparks drifting out from the rim.
            for (int i = 0; i < 4; i++)
            {
                float ph = (s * 1.7f + Rand(i + 200 + k)) % 1f, t = Rand(i + 210 + k) * Tau + s * 0.8f;
                Sprite(On(t, rr * (1f + ph * 0.3f)), 0.07f, 0.06f, Fade(White, a * (1f - ph)), glow, L + 0.003f);
            }
        }

        /// <summary>A ring round <paramref name="q"/> on the circle's axes, from radius rad - thick to rad.</summary>
        private static void Ring(Vector2 q, float rad, float thick, float alpha, float altitude, Color colour)
        {
            Sides(N + 1, out Vector2[] inner, out Vector2[] outer);
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N * Tau;
                inner[i] = On(t, rad - thick, q, axisH, axisV);
                outer[i] = On(t, rad, q, axisH, axisV);
            }
            Strip(inner, outer, Fade(colour, alpha), solid, altitude);
        }

        /// <summary>A filled ellipse round <paramref name="q"/> on the axes <paramref name="h"/> and <paramref name="v"/>, as a fan from the centre.</summary>
        private static void Disc(Vector2 q, float rad, float alpha, float altitude, Color colour, Vector2 h, Vector2 v)
        {
            Sides(N + 1, out Vector2[] inner, out Vector2[] outer);
            for (int i = 0; i <= N; i++)
            {
                inner[i] = q;
                outer[i] = On(i / (float)N * Tau, rad, q, h, v);
            }
            Strip(inner, outer, Fade(colour, alpha), solid, altitude);
        }

        /// <summary>
        /// The corroded look, made for the game rather than the source: the pawn stays readable and the weapon's
        /// magic shows on him. A dim contract circle (the shot circle's drawing, flat, at a quarter speed and 55 %
        /// alpha) turns on the floor under his feet, four dark violet veins run 0.2 to 0.3 cells from the chest and
        /// pulse, the eyes glow blue, three thin wisps rise 0.4 cells off the shoulders, and the barrel is lit blue
        /// from chamber to muzzle. <paramref name="stand"/> is the pawn's drawn point; the eyes are placed for a
        /// pawn facing south. Everything on the pawn starts <see cref="PawnBody.Over"/> above <paramref name="body"/>,
        /// its DrawPos.y, so no pawn's head or hair covers it.
        /// </summary>
        internal static void Corroded(Vector2 stand, Vector2 chamber, Vector2 muzzle, float body, Vector2 sun, float strength, float s)
        {
            var head = new Vector2(stand.x, stand.y + PawnBody.Head);
            var neck = new Vector2(stand.x, stand.y + PawnBody.Neck);
            var chest = new Vector2(stand.x, stand.y + PawnBody.Chest);
            float pulse = 0.7f + 0.3f * Mathf.Sin(s * 3f), over = body + PawnBody.Over;
            Draw(stand, 0.6f, 1f, s * 0.25f, 9, Vector2.right, Vector2.up, Vector2.right, false, sun, strength, Floor + 0.05f, 0.55f);
            for (int i = 0; i < 4; i++)
            {
                float t = i / 4f * Tau + 0.6f, reach = 0.2f + 0.1f * Rand(i + 400);
                Vector2[] pts = GokuGraphics.Points(5);
                pts[0] = chest;
                for (int k = 1; k <= 4; k++)
                {
                    float u = k / 4f, off = (Rand(i * 7 + k * 3) - 0.5f) * reach * 0.5f;
                    pts[k] = new Vector2(chest.x + Mathf.Cos(t) * reach * u - Mathf.Sin(t) * off, chest.y + Mathf.Sin(t) * reach * u * 0.8f + Mathf.Cos(t) * off);
                }
                GokuGraphics.Line(pts, 0.022f, Fade(Vein, 0.9f * pulse), solid, over, Taper.End);
            }
            for (int i = 0; i < 2; i++)
            {
                var eye = new Vector2(head.x + (i == 0 ? -0.07f : 0.07f), head.y - 0.02f);
                Sprite(eye, 0.1f, 0.08f, Fade(Eye, 0.9f * pulse), glow, over + 0.001f);
                DrawMesh(disc, eye, over + 0.002f, 0.02f, 0.016f, 0f, White, solid);
            }
            for (int i = 0; i < 3; i++)
            {
                float ph = (s * 0.5f + Rand(i + 300)) % 1f, x = neck.x + (i - 1) * 0.16f + Mathf.Sin(s * 2f + i) * 0.03f;
                Sprite(new Vector2(x, neck.y + 0.05f + ph * 0.4f), 0.12f + ph * 0.1f, 0.14f + ph * 0.12f,
                    Fade(Smoke, 0.35f * Mathf.Sin(ph * Mathf.PI)), Puff, over + 0.01f);
            }
            Streak(chamber, muzzle, 0.07f, Fade(CircleBlue, 0.35f + 0.25f * pulse), whiteGlow, over + 0.04f, 3);
        }
    }
}
