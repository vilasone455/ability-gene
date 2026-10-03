using UnityEngine;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using G = RimArt.EgoSolemnLamentGraphics;
using T = RimArt.EgoSolemnLamentTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Solemn Lament's two pistols and what leaves them: the white shot's crescent, blast and spikes and its
    /// smoke band with dashes and sparks; the black shot's glow, ink splat, torn smear, drips and slivers and
    /// its dark line. Everything lies level at chest height (0.05 north of the cell centre on screen), so it
    /// turns with the aim. Each one's shift argument is the shot's altitude step
    /// (<see cref="EgoSolemnLamentGraphics.ShotStep"/>).
    /// </summary>
    public static class EgoSolemnLamentShotGraphics
    {
        /// <summary>
        /// The pistol's parts as four corners (along, up) each: bottom-back, top-back, bottom-front, top-front.
        /// A grip 0.12 long raked back, a trigger guard, a slide 0.34 long and 0.065 high, a barrel tip.
        /// </summary>
        private static readonly float[][] Parts =
        {
            new[] { -0.075f, -0.115f, -0.04f, 0.005f, 0f, -0.12f, 0.04f, 0.005f },
            new[] { 0.03f, -0.045f, 0.03f, 0f, 0.1f, -0.045f, 0.1f, 0f },
            new[] { -0.05f, 0f, -0.05f, 0.065f, 0.29f, 0f, 0.29f, 0.065f },
            new[] { 0.29f, 0.012f, 0.29f, 0.052f, T.GunLen, 0.012f, T.GunLen, 0.052f },
        };
        // The frame's gun for Corner: its drawn grip point, its drawn direction and length factor, its grip side.
        private static Vector2 gunAt, gunAlong, gunUp;
        private static float gunLen;

        /// <summary>A point (along, up) on the current gun, as drawn.</summary>
        private static Vector2 Corner(float along, float up) => gunAt + gunAlong * (along * gunLen) + gunUp * up;

        /// <summary>
        /// A pistol as RimWorld draws a gun: its side view laid flat and turned to the aim <paramref name="d"/>,
        /// the grip hanging on the side toward the viewer (mirrored aiming west, as the game flips the sprite).
        /// The black gun has a grey outline 0.012 out from each part so it reads on a dark coat. Level at chest
        /// height from the hand's ground point (the top of the grip); <paramref name="slide"/> pushes it back;
        /// <paramref name="tilt"/> (radians) swings the muzzle up (cos tilt along d, Lift x sin tilt north) or,
        /// negative, down. Its shadow stays flat on the floor.
        /// </summary>
        public static void Pistol(Vector2 hand, Vector2 d, bool white, float tilt, float slide, float layer, Vector2 sun, float strength)
        {
            Vector2 foot = hand - d * slide;
            float ct = Mathf.Cos(tilt), st = Mathf.Sin(tilt);
            var drawn = new Vector2(d.x * ct, d.y * ct + G.Lift * st);
            gunLen = drawn.magnitude;
            gunAlong = drawn / gunLen;
            gunUp = T.GunUp(gunAlong);
            gunAt = new Vector2(foot.x, foot.y + PawnBody.Chest);
            var shadowAt = new Vector2(foot.x + sun.x * T.ChestH + d.x * T.GunLen * 0.5f, foot.y + PawnBody.Ground + sun.y * T.ChestH + d.y * T.GunLen * 0.5f);
            Sprite(shadowAt, T.GunLen * ct + 0.08f, T.GunW * 1.6f, Fade(G.Ink, strength * 0.45f), soft, G.ShadowLayer + (white ? 0f : 0.0001f),
                -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            if (!white)
                for (int i = 0; i < Parts.Length; i++)
                {
                    float[] c = Parts[i];
                    float ca = (c[0] + c[6]) / 2f, cb = (c[1] + c[7]) / 2f;
                    Sides(2, out Vector2[] a, out Vector2[] b);
                    a[0] = Out(c[0], c[1], ca, cb);
                    a[1] = Out(c[4], c[5], ca, cb);
                    b[0] = Out(c[2], c[3], ca, cb);
                    b[1] = Out(c[6], c[7], ca, cb);
                    Strip(a, b, Fade(G.Ash, 0.8f), solid, layer + i * 0.0002f);
                }
            for (int i = 0; i < Parts.Length; i++)
            {
                float[] c = Parts[i];
                Color colour = i == 2 ? (white ? G.Pale : G.Ink) : (white ? G.Ash : G.Soot);
                Quad(c[0], c[1], c[4], c[5], c[2], c[3], c[6], c[7], colour, layer + 0.001f + i * 0.0002f);
            }
            Quad(-0.045f, 0.05f, 0.285f, 0.05f, -0.045f, 0.064f, 0.285f, 0.064f, Fade(white ? G.White : G.Ash, 0.9f), layer + 0.002f);
        }

        /// <summary>A part's corner pushed 0.012 out from its middle (ca, cb), for the black gun's outline.</summary>
        private static Vector2 Out(float along, float up, float ca, float cb)
        {
            var n = new Vector2(along - ca, up - cb);
            float l = n.magnitude;
            if (l > 0f) n /= l;
            return Corner(along + n.x * 0.012f, up + n.y * 0.012f);
        }

        /// <summary>A quad on the current gun: one side through (a0, b0) and (a1, b1), the other through (a2, b2) and (a3, b3).</summary>
        private static void Quad(float a0, float b0, float a1, float b1, float a2, float b2, float a3, float b3, Color colour, float altitude)
        {
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = Corner(a0, b0);
            a[1] = Corner(a1, b1);
            b[0] = Corner(a2, b2);
            b[1] = Corner(a3, b3);
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>
        /// The white shot at muzzle <paramref name="m"/>: a flash with seven short spikes for 0.08 s; the blast, a
        /// ragged white splat (the ink's shapes, white) thrown forward, centred 0.3 ahead and stretched 2.6x along
        /// the shot, out to 0.22 cells in 0.03 s and shrinking away by 0.15 s; and the crescent, a white arc 0.42
        /// round the gun hand swept on its outer side (<paramref name="outward"/> -1 for the right hand) in 0.05
        /// s, thick in the middle, gone in 0.18 s.
        /// </summary>
        public static void WhiteMuzzle(Vector2 m, Vector2 dir, Vector2 hand, float outward, float age, int seed, float shift)
        {
            if (age < 0f || age > 0.18f) return;
            float f = 1f - age / 0.18f, aim = Mathf.Atan2(dir.y, dir.x);
            if (age < 0.15f)
            {
                float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.03f), 3f);
                float R = 0.22f * grow * (1f - Mathf.Pow(Mathf.Clamp01((age - 0.04f) / 0.11f), 1.5f));
                if (R > 0.01f)
                {
                    Vector2 c = m + dir * 0.3f;
                    DrawMesh(EgoSolemnLamentHitGraphics.Splats[(seed + 2) % 3], c, Overhead + 0.0605f + shift, R * 2.6f, R * 0.8f, -aim * Mathf.Rad2Deg, Fade(G.White, 0.8f), whiteGlow);
                    DrawMesh(EgoSolemnLamentHitGraphics.Splats[seed % 3], c, Overhead + 0.0606f + shift, R * 1.7f, R * 0.5f, -aim * Mathf.Rad2Deg, Fade(G.White, 0.95f), solid);
                }
            }
            if (age < 0.08f)
            {
                float g = 1f - age / 0.08f;
                Sprite(m, 0.12f + 0.4f * g, 0.11f + 0.36f * g, Fade(G.White, 0.9f * g), glow, Overhead + 0.06f + shift);
                for (int i = 0; i < 7; i++)
                {
                    float t = aim + (i - 3) * 0.17f + (Rand(i + 50) - 0.5f) * 0.1f, l = (0.25f + 0.35f * Rand(i + 60)) * (0.5f + g * 0.5f);
                    Streak(m, m + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * l, 0.035f, Fade(G.White, 0.95f * g), whiteGlow, Overhead + 0.061f + shift, 3);
                }
            }
            float sweep = Mathf.Clamp01(age / 0.05f), r = 0.42f, a0 = aim + outward * 2.6f, a1 = aim - outward * 0.25f;
            var centre = new Vector2(hand.x, hand.y + PawnBody.Chest);
            const int N = 16;
            Sides(N + 1, out Vector2[] inner, out Vector2[] outer);
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, t = a0 + (a1 - a0) * u * sweep, w = 0.09f * Mathf.Sin(u * Mathf.PI) * f;
                var way = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                inner[i] = centre + way * (r - w);
                outer[i] = centre + way * (r + w * 0.4f);
            }
            Strip(inner, outer, Fade(G.White, 0.9f * f), whiteGlow, Overhead + 0.059f + shift);
        }

        /// <summary>
        /// The black shot at muzzle <paramref name="m"/>: a soft white glow 0.9 wide 0.2 ahead for 0.12 s, so the
        /// dark flash still reads as a flash; a black ink splat centred 0.22 ahead, stretched 1.6x along the shot,
        /// out to 0.2 cells in 0.04 s, shrinking from 0.06 s, opaque, gone by 0.2 s; a torn ink smear out to 0.95
        /// cells in 0.05 s, 0.15 wide at the muzzle and tapering, ragged on both edges, its back end chasing the
        /// front from 0.06 s, gone by 0.26 s; six drips thrown sideways and eight pointed slivers flung forward in
        /// a 70-degree cone at 2 to 4 cells/s, all shrinking, gone by 0.3 s.
        /// </summary>
        public static void BlackMuzzle(Vector2 m, Vector2 dir, float age, int seed, float shift)
        {
            if (age < 0f || age > 0.3f) return;
            float aim = Mathf.Atan2(dir.y, dir.x);
            Vector2 across = G.Left(dir);
            if (age < 0.12f)
            {
                float g = 1f - age / 0.12f;
                Sprite(m + dir * 0.2f, 0.9f * g, 0.75f * g, Fade(G.White, 0.8f * g), glow, Overhead + 0.061f + shift);
            }
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.04f), 3f), R = 0.2f * grow * (1f - Mathf.Pow(Mathf.Clamp01((age - 0.06f) / 0.14f), 1.5f));
            if (R > 0.01f)
                DrawMesh(EgoSolemnLamentHitGraphics.Splats[(seed + 1) % 3], m + dir * 0.22f, Overhead + 0.063f + shift, R * 1.6f, R * 0.9f, -aim * Mathf.Rad2Deg, Fade(G.Ink, 0.95f), solid);
            float reach = 0.95f * (1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.05f), 2f)), tail = Mathf.Pow(Mathf.Clamp01((age - 0.06f) / 0.2f), 1.3f) * reach;
            if (reach - tail > 0.02f)
            {
                const int N = 14;
                Sides(N + 1, out Vector2[] a, out Vector2[] b);
                for (int i = 0; i <= N; i++)
                {
                    float u = i / (float)N, d = tail + (reach - tail) * u, w = 0.075f * Mathf.Pow(Mathf.Max(0f, 1f - u), 0.6f) + 0.008f;
                    Vector2 q = m + dir * d;
                    a[i] = q + across * (w * (0.5f + 0.9f * Rand(seed * 41 + i)));
                    b[i] = q - across * (w * (0.5f + 0.9f * Rand(seed * 43 + i + 7)));
                }
                Strip(a, b, Fade(G.Ink, 0.95f), solid, Overhead + 0.062f + shift);
            }
            for (int i = 0; i < 6; i++)
            {
                float u = age / (0.2f + 0.1f * Rand(seed * 47 + i));
                if (u >= 1f) continue;
                Vector2 q = m + dir * (0.15f + 0.7f * Rand(seed * 53 + i)) + across * ((i % 2 == 1 ? 1f : -1f) * (0.05f + (1f + Rand(seed * 59 + i)) * age));
                float r = 0.028f * (1f - u * u);
                DrawMesh(disc, new Vector2(q.x, q.y - 1.5f * age * age), Overhead + 0.0625f + shift, r, r, 0f, G.Ink, solid);
            }
            for (int i = 0; i < 8; i++)
            {
                float u = age / (0.22f + 0.08f * Rand(seed * 61 + i));
                if (u >= 1f) continue;
                float t = aim + (Rand(seed * 67 + i) - 0.5f) * 1.2f, v = 2f + 2f * Rand(seed * 71 + i), d0 = 0.15f + v * age;
                EgoSolemnLamentHitGraphics.Chunk(m + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * d0, t, 0.05f * (1f - u), seed * 73 + i, Fade(G.Ink, 0.95f), Overhead + 0.064f + shift);
            }
        }

        /// <summary>
        /// The white shot's line, as Skill 1 draws it: the round's core, a white line 0.03 wide in a glow 0.12
        /// wide drawn out at <see cref="EgoSolemnLamentTiming.Speed"/>, gone 0.1 s after it arrives; a white-grey
        /// smoke band 0.18 wide (narrower in the first sixth) with ragged edges, thinning to 0.06 over 0.45 s after
        /// the round arrives and drifting sideways in two uneven waves, gone between 0.45 and 0.8 s; fourteen
        /// white dashes 0.2 to 0.6 long flying on along it at 8 to 12 cells/s for 0.14 to 0.3 s; eight sparks
        /// drifting up for 0.6 s.
        /// </summary>
        public static void WhiteTrail(Vector2 from, Vector2 to, float age, int seed, float shift)
        {
            if (age < 0f || age > 0.8f) return;
            Vector2 run = to - from;
            float dist = run.magnitude;
            Vector2 d = dist > 0f ? run / dist : Vector2.zero, acr = G.Left(d);
            float flight = dist / T.Speed, reach = Mathf.Clamp01(age / flight), thin = Mathf.Clamp01((age - flight) / 0.45f), fade = 1f - Smooth((age - 0.45f) / 0.35f);
            const int N = 24;
            Sides(N + 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N * reach;
                Vector2 q = from + d * (dist * u);
                float off = 0.03f * thin * (Mathf.Sin(i * 0.9f + age * 5f + seed) + 0.7f * Mathf.Sin(i * 2.3f - age * 3f + seed * 2) + 0.5f * (Rand(seed * 79 + i) - 0.5f));
                float w = (0.09f - 0.06f * thin) * (0.35f + 0.65f * Mathf.Min(1f, u * 6f)) * (0.75f + 0.25f * Mathf.Sin(i * 1.7f + age * 4f + seed));
                a[i] = q + acr * (off + w);
                b[i] = q + acr * (off - w * (0.7f + 0.3f * Mathf.Sin(i * 2.3f + seed)));
            }
            Strip(a, b, Fade(G.Smoke, 0.5f * fade * (1f - 0.35f * thin)), solid, Overhead + 0.05f + shift);
            float core = 1f - Mathf.Clamp01((age - flight) / 0.1f);
            if (core > 0f)
            {
                Vector2 head = from + d * (dist * reach);
                Segment(from, head, 0.12f, Fade(G.White, 0.3f * core), whiteGlow, Overhead + 0.0505f + shift, Taper.None);
                Segment(from, head, 0.03f, Fade(G.White, 0.95f * core), whiteGlow, Overhead + 0.051f + shift, Taper.None);
                if (reach < 1f) Sprite(head, 0.12f, 0.1f, Fade(G.White, 0.95f), glow, Overhead + 0.052f + shift);
            }
            for (int i = 0; i < 14; i++)
            {
                float a0 = age - Rand(seed * 37 + i) * 0.12f, life = 0.14f + 0.16f * Rand(seed * 41 + i);
                if (a0 < 0f || a0 > life) continue;
                float along = dist * (0.05f + 0.85f * Rand(seed * 43 + i)) + a0 * (8f + 4f * Rand(seed * 47 + i)), len = 0.2f + 0.4f * Rand(seed * 59 + i);
                if (along > dist + 0.6f) continue;
                Vector2 p0 = from + d * along + acr * ((Rand(seed * 53 + i) - 0.5f) * 0.5f);
                Streak(p0, p0 + d * len, 0.035f, Fade(G.White, 0.95f * (1f - a0 / life)), whiteGlow, Overhead + 0.052f + shift, 3);
            }
            for (int i = 0; i < 8; i++)
            {
                float a0 = age - 0.05f - Rand(seed * 61 + i) * 0.1f;
                if (a0 < 0f || a0 > 0.6f) continue;
                Vector2 q = from + d * (dist * Rand(seed * 67 + i)) + acr * ((Rand(seed * 71 + i) - 0.5f) * 0.6f);
                Sprite(new Vector2(q.x, q.y + 0.15f * a0), 0.05f, 0.05f, Fade(G.White, 0.9f * (1f - a0 / 0.6f)), glow, Overhead + 0.053f + shift);
            }
        }

        /// <summary>The black shot's line from the muzzle to the target: drawn out at <see cref="EgoSolemnLamentTiming.Speed"/>, then thinned and faded over <see cref="EgoSolemnLamentTiming.TrailLife"/> s.</summary>
        public static void BlackTrail(Vector2 from, Vector2 to, float age, float shift)
        {
            float flight = Vector2.Distance(from, to) / T.Speed, u = Mathf.Clamp01(age / flight), fade = 1f - Mathf.Clamp01((age - flight) / T.TrailLife);
            if (age < 0f || fade <= 0f) return;
            Vector2 head = Vector2.Lerp(from, to, u);
            float w = 0.5f + 0.5f * fade;
            Segment(from, head, 0.15f * w, Fade(G.Soot, 0.3f * fade), solid, Overhead + 0.05f + shift, Taper.None);
            Segment(from, head, 0.04f * w, Fade(G.Ink, 0.85f * fade), solid, Overhead + 0.051f + shift, Taper.None);
            if (u < 1f) Sprite(head, 0.12f, 0.1f, Fade(G.Ink, 0.95f), soft, Overhead + 0.052f + shift);
        }

        /// <summary>The lab's line through two points (GokuGraphics.Line).</summary>
        internal static void Segment(Vector2 a, Vector2 b, float width, Color colour, Material material, float altitude, Taper taper)
        {
            Vector2[] pts = GokuGraphics.Points(2);
            pts[0] = a;
            pts[1] = b;
            GokuGraphics.Line(pts, width, colour, material, altitude, taper);
        }
    }
}
