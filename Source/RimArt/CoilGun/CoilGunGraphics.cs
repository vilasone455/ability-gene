using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces the Coil Gun's shot and Chain Arc share: the jagged bolt, the charge at the
    /// muzzle, the hit flash, sparks, the scorch left on the floor, the EMP ring on a mechanoid, and the
    /// steam and water glints off a Soaked pawn. There is no sketch; the look follows the Raikō Kusari
    /// sketch's "Chidori blue-white" palette (rinnegan-raiko-kusari.js): a soft blue halo made of glow
    /// sprites stretched along the line, then three additive strips (halo, pale core, white thread)
    /// whose width swells and pinches, all rebuilt every <see cref="Boil"/> seconds from a hash of the
    /// redraw step. Light only: every bolt, spark and flash is additive (MoteGlow); only the scorch and
    /// its burn lines are alpha-blended dark.
    ///
    /// Heights are drawn north (SixPathsHeight.Lift). The gun is Core's and lies level, so the muzzle has
    /// no height; a pawn is hit at <see cref="ChestH"/>. Every routine takes an age and keeps no state.
    /// Call VfxDraw.Begin with the effect's ground point first.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class CoilGunGraphics
    {
        internal static readonly Color Halo = new Color(0.22f, 0.50f, 1f), Pale = new Color(0.82f, 0.93f, 1f), White = new Color(1f, 1f, 1f),
            Ember = new Color(0.45f, 0.75f, 1f), Char = new Color(0.05f, 0.045f, 0.06f), Steam = new Color(0.93f, 0.96f, 1f),
            Water = new Color(0.40f, 0.70f, 1f), Glint = new Color(0.75f, 0.92f, 1f);

        /// <summary>Seconds between two shapes of a bolt: 20 redraws a second.</summary>
        internal const float Boil = 0.05f;
        /// <summary>Where a pawn is hit, cells above its feet.</summary>
        internal const float ChestH = 0.3f;
        /// <summary>Cells from the holder's centre to the muzzle along the aim: Core draws the gun 0.4 out, and the barrel ends 0.55 past its centre at drawSize 1.25.</summary>
        internal const float MuzzleAlong = 0.95f;

        internal static readonly float Y = AltitudeLayer.MoteOverhead.AltitudeFor();
        private static readonly Mesh ringMesh = Ring(0.93f, "RimArt coil ring"), thinRing = Ring(0.985f, "RimArt coil thin ring");

        private static readonly Vector2[] P = new Vector2[MostPoints], Q = new Vector2[MostPoints];
        private static readonly float[] W = new float[MostPoints];
        private static readonly float[] ku = new float[8], kv = new float[8];

        internal static float Lifted(float h) => h * SixPathsHeight.Lift;

        /// <summary>A pawn's chest on screen, from its feet.</summary>
        internal static Vector2 Chest(Vector2 feet) => new Vector2(feet.x, feet.y + Lifted(ChestH));

        internal static Vector2 Dir(float degrees) => Turn(degrees);

        private static int Step(float s) => Mathf.FloorToInt(Mathf.Max(0f, s) / Boil);

        // ------------------------------------------------------------------ bolt shape

        /// <summary>
        /// A jagged line from a to b into <paramref name="into"/>: 2 to 4 big kinks it meanders through
        /// and fine crackle on top, both zero at the ends and smaller on a short bolt. Returns the
        /// number of points.
        /// </summary>
        private static int Jag(Vector2 a, Vector2 b, float jag, int seed, float seg, Vector2[] into)
        {
            float dx = b.x - a.x, dz = b.y - a.y, len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f) len = 1e-4f;
            int n = Mathf.Clamp(Mathf.RoundToInt(len / seg), 4, MostPoints - 1);
            float nx = -dz / len, nz = dx / len, j = jag * Mathf.Min(1f, len / 0.8f);
            int kinks = 2 + Mathf.Min(2, (int)(Rand(seed + 1) * 3f));
            ku[0] = 0f; kv[0] = 0f;
            for (int k = 1; k <= kinks; k++)
            {
                ku[k] = (k - 0.5f + (Rand(seed + 10 + k) - 0.5f) * 0.6f) / kinks;
                kv[k] = (Rand(seed + 20 + k) - 0.5f) * 3f * j;
            }
            ku[kinks + 1] = 1f; kv[kinks + 1] = 0f;
            for (int i = 0, k = 1; i <= n; i++)
            {
                float u = i / (float)n;
                while (k < kinks + 1 && u > ku[k]) k++;
                float span = Mathf.Max(1e-6f, ku[k] - ku[k - 1]);
                float meander = kv[k - 1] + (kv[k] - kv[k - 1]) * Mathf.Clamp01((u - ku[k - 1]) / span);
                float off = i > 0 && i < n ? meander + (Rand(seed + i * 7) - 0.5f) * 0.7f * j : 0f;
                into[i] = new Vector2(a.x + dx * u + nx * off, a.y + dz * u + nz * off);
            }
            return n + 1;
        }

        /// <summary>Widths along a bolt of <paramref name="count"/> points: swell and pinch between 0.5 and 1.6 times w, thinner at the ends.</summary>
        private static void Widths(int count, float w, int seed, float taper)
        {
            float c0 = 0.5f, c1 = 0.5f + 1.1f * Rand(seed + 41), c2 = 0.5f + 1.1f * Rand(seed + 42), c3 = 0.5f + 1.1f * Rand(seed + 43), c4 = 0.5f;
            int n = Mathf.Max(1, count - 1);
            for (int i = 0; i < count; i++)
            {
                float f = i / (float)n * 4f;
                int k = Mathf.Min(3, (int)f);
                float e = (1f - Mathf.Cos((f - k) * Mathf.PI)) / 2f;
                float from = k == 0 ? c0 : k == 1 ? c1 : k == 2 ? c2 : c3, to = k == 0 ? c1 : k == 1 ? c2 : k == 2 ? c3 : c4;
                float end = taper > 0f ? Mathf.Lerp(1f, 0.15f, taper * i / (float)n) : 1f;
                W[i] = w * (from + (to - from) * e) * end;
            }
        }

        /// <summary>A strip through the first <paramref name="count"/> points of <paramref name="pts"/>, W[i] × <paramref name="scale"/> across.</summary>
        private static void Stroke(Vector2[] pts, int count, float scale, Color colour, Material material, float altitude)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            int last = count - 1;
            Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < count; i++)
            {
                Vector2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(last, i + 1)];
                float dx = next.x - prev.x, dz = next.y - prev.y, len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len < 1e-6f) len = 1f;
                float w = W[i] * scale / 2f + 0.003f;
                a[i] = new Vector2(pts[i].x - dz / len * w, pts[i].y + dx / len * w);
                b[i] = new Vector2(pts[i].x + dz / len * w, pts[i].y - dx / len * w);
            }
            Strip(a, b, colour, material, altitude);
        }

        /// <summary>Soft blue light along a straight run: glow sprites stretched along it, so the light has no hard edge.</summary>
        internal static void HaloAlong(Vector2 a, Vector2 b, float width, float alpha, int seed, float altitude)
        {
            if (alpha <= 0.005f) return;
            Vector2 run = b - a;
            float len = run.magnitude;
            if (len < 0.02f) return;
            float angle = Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;
            int k = Mathf.Max(1, Mathf.CeilToInt(len / 1.1f));
            for (int i = 0; i < k; i++)
            {
                float u = (i + 0.5f) / k;
                Sprite(a + run * u, len / k * 1.9f, width * (0.8f + 0.4f * Rand(seed + i * 3)), Fade(Halo, alpha), glow, altitude, -angle);
            }
        }

        /// <summary>
        /// One lit bolt from a to b. <paramref name="grow"/> 0..1 is how far it has reached (the head
        /// glows while it grows); <paramref name="s"/> picks the shape, which changes every Boil seconds.
        /// Width <paramref name="w"/> is the pale core's, cells. With <paramref name="forks"/> it throws
        /// two short side branches. A second, fainter bolt flickers beside the first on some redraws.
        /// </summary>
        internal static void Bolt(Vector2 a, Vector2 b, float grow, float alpha, float s, int seed, float w = 0.06f, float jag = 0.16f,
            float altitude = -1f, bool forks = true, float haloWidth = 0.75f)
        {
            if (alpha <= 0.01f || grow <= 0f) return;
            if (altitude < 0f) altitude = Y + 0.03f;
            int step = Step(s);
            Vector2 head = Vector2.Lerp(a, b, Mathf.Clamp01(grow));
            HaloAlong(a, head, haloWidth, 0.45f * alpha, seed * 13 + step * 7, altitude - 0.012f);
            for (int j = 0; j < 2; j++)
            {
                int sd = seed * 7 + step * 131 + j * 17;
                if (j == 1 && Rand(sd + 999) < 0.45f) continue;
                float flick = 0.65f + 0.35f * Rand(sd + 5);
                int count = Jag(a, b, jag * (j == 1 ? 1.4f : 1f), sd, 0.15f, P);
                if (grow < 1f) count = Cut(count, grow);
                Widths(count, j == 1 ? w * 0.6f : w, sd, 0f);
                float y = altitude + j * 0.002f, aa = alpha * flick;
                // The halo in two steps, wide and faint then narrower, so its edge falls off like light.
                Stroke(P, count, 3.6f, Fade(Halo, 0.12f * aa), whiteGlow, y - 0.0002f);
                Stroke(P, count, 1.9f, Fade(Halo, 0.3f * aa), whiteGlow, y);
                Stroke(P, count, 0.7f, Fade(Pale, Mathf.Min(1f, aa)), whiteGlow, y + 0.0002f);
                if (j == 0) Stroke(P, count, 0.3f, Fade(White, Mathf.Min(1f, aa)), whiteGlow, y + 0.0003f);
                if (!forks || count < 5) continue;
                for (int f = 0; f < 2; f++)
                {
                    int fs = sd + 50 + f * 23;
                    if (Rand(fs) < 0.3f) continue;
                    int bi = 1 + (int)(Rand(fs + 1) * (count - 3));
                    Vector2 q0 = P[bi], q1 = P[bi + 1];
                    float dir = Mathf.Atan2(q1.y - q0.y, q1.x - q0.x) + (Rand(fs + 2) < 0.5f ? -1f : 1f) * (0.5f + 0.6f * Rand(fs + 3));
                    float bl = 0.18f + 0.38f * Rand(fs + 4);
                    Branch(q0, q0 + new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * bl, w * 0.55f, aa * 0.85f, fs + 5, y - 0.001f);
                }
            }
            if (grow < 1f)
            {
                Sprite(head, 0.55f, 0.55f, Fade(Halo, 0.6f * alpha), glow, altitude + 0.004f);
                Sprite(head, 0.2f, 0.2f, Fade(White, alpha), glow, altitude + 0.005f);
            }
        }

        /// <summary>Keeps the points of a bolt up to <paramref name="grow"/> of its point count, the last one interpolated.</summary>
        private static int Cut(int count, float grow)
        {
            float f = grow * (count - 1);
            int i = Mathf.FloorToInt(f);
            if (i >= count - 1) return count;
            P[i + 1] = Vector2.Lerp(P[i], P[i + 1], f - i);
            return Mathf.Max(2, i + 2);
        }

        /// <summary>A short tapering branch or crackle: halo and pale core, no thread.</summary>
        internal static void Branch(Vector2 a, Vector2 b, float w, float alpha, int seed, float altitude)
        {
            if (alpha <= 0.01f) return;
            int count = Jag(a, b, 0.05f, seed, 0.07f, Q);
            Widths(count, w, seed, 1f);
            Stroke(Q, count, 2f, Fade(Halo, 0.4f * alpha), whiteGlow, altitude);
            Stroke(Q, count, 0.8f, Fade(Pale, Mathf.Min(1f, alpha)), whiteGlow, altitude + 0.0002f);
        }

        // ------------------------------------------------------------------ the muzzle

        /// <summary>
        /// The coil charging at the muzzle over the warmup, <paramref name="u"/> 0 to 1: a core that grows
        /// and beats, a blue glow round it and on the floor under it, crackle leaping off it (re-drawn
        /// every Boil), and motes drawn in toward it.
        /// </summary>
        internal static void Charge(Vector2 muzzle, float u, float s, int seed)
        {
            if (u <= 0f) return;
            float g = Smooth(u);
            int step = Step(s);
            float beat = 0.85f + 0.3f * Rand(seed + step * 3);
            float r = Mathf.Lerp(0.08f, 0.22f, g) * beat;
            Sprite(new Vector2(muzzle.x, muzzle.y - Lifted(0.45f)), 2.0f * g, 1.5f * g, Fade(Halo, 0.2f * g), glow, Floor + 0.03f);
            Sprite(muzzle, r * 7f, r * 7f, Fade(Halo, (0.3f + 0.4f * g) * beat), glow, Y + 0.02f);
            Sprite(muzzle, r * 2.6f, r * 2.6f, Fade(Pale, 0.9f), glow, Y + 0.03f);
            Sprite(muzzle, r * 1.3f, r * 1.3f, Fade(White, 1f), glow, Y + 0.031f);
            for (int j = 0; j < 4; j++)
            {
                int sd = seed + step * 29 + j * 11;
                if (Rand(sd) < 0.25f) continue;
                float angle = Rand(sd + 1) * Mathf.PI * 2f, len = 0.2f + (0.2f + 0.35f * Rand(sd + 2)) * g;
                Branch(muzzle, muzzle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * len, 0.04f, 0.95f * Mathf.Max(0.4f, g), sd + 3, Y + 0.025f);
            }
            for (int k = 0; k < 8; k++)
            {
                float phase = Mathf.Repeat(s * 2.6f + Rand(seed + 60 + k), 1f), angle = Rand(seed + 70 + k) * Mathf.PI * 2f;
                Vector2 at = muzzle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.9f * (1f - phase));
                Sprite(at, 0.09f, 0.09f, Fade(Pale, g * phase), glow, Y + 0.024f);
            }
        }

        /// <summary>The flash when the gun lets go: a bright blue-white bloom at the muzzle, 0.12 s.</summary>
        internal static void MuzzleFlash(Vector2 muzzle, float age, float size = 1f)
        {
            if (age < 0f || age >= 0.12f) return;
            float a = 1f - age / 0.12f;
            Sprite(muzzle, 1.1f * size, 1.1f * size, Fade(Halo, 0.55f * a), glow, Y + 0.035f);
            Sprite(muzzle, 0.42f * size, 0.42f * size, Fade(White, a), glow, Y + 0.036f);
        }

        /// <summary>
        /// Charging from a battery: a faint glow at the gun, and every <see cref="RechargePeriod"/> s a
        /// thin arc jumps from the battery's top to the gun and burns for 0.12 s. <paramref name="s"/> is
        /// any running clock.
        /// </summary>
        internal static void Recharge(Vector2 battery, Vector2 gun, float s, int seed)
        {
            float beat = 0.8f + 0.2f * Mathf.Sin(s * 9f);
            Sprite(gun, 0.5f, 0.5f, Fade(Halo, 0.28f * beat), glow, Y + 0.02f);
            Sprite(gun, 0.14f, 0.14f, Fade(Pale, 0.7f * beat), glow, Y + 0.021f);
            float phase = Mathf.Repeat(s + Rand(seed) * RechargePeriod, RechargePeriod);
            if (phase >= 0.12f) return;
            int cycle = Mathf.FloorToInt((s + Rand(seed) * RechargePeriod) / RechargePeriod);
            float a = 1f - phase / 0.12f * 0.6f;
            Bolt(battery, gun, Mathf.Clamp01(phase / 0.03f), a, s, seed + cycle * 7, 0.035f, 0.1f, Y + 0.025f, false, 0.35f);
            Sprite(battery, 0.4f, 0.4f, Fade(Halo, 0.5f * a), glow, Y + 0.024f);
        }

        internal const float RechargePeriod = 0.5f;

        // ------------------------------------------------------------------ a hit

        /// <summary>The flash where a bolt lands: 0.15 s of bloom, then small arcs crawl over the body for 0.4 s.</summary>
        internal static void HitFlash(Vector2 chest, float age, float s, int seed, float size = 1f)
        {
            if (age < 0f) return;
            if (age < 0.15f)
            {
                float a = 1f - age / 0.15f;
                Sprite(chest, 1.3f * size, 1.3f * size, Fade(Halo, 0.55f * a), glow, Y + 0.04f);
                Sprite(chest, 0.55f * size, 0.55f * size, Fade(White, 0.95f * a), glow, Y + 0.041f);
            }
            if (age < 0.4f)
            {
                float a = 1f - age / 0.4f;
                int step = Step(s);
                for (int j = 0; j < 3; j++)
                {
                    int sd = seed + step * 37 + j * 13;
                    if (Rand(sd) < 0.35f) continue;
                    float angle = Rand(sd + 1) * Mathf.PI * 2f;
                    Vector2 from = chest + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.08f;
                    Vector2 to = chest + new Vector2(Mathf.Cos(angle + 1.2f), Mathf.Sin(angle + 1.2f) * 1.25f) * (0.22f + 0.14f * Rand(sd + 2)) * size;
                    Branch(from, to, 0.03f, a, sd + 3, Y + 0.038f);
                }
            }
        }

        /// <summary>
        /// Sparks thrown from a point <paramref name="height"/> cells above <paramref name="ground"/>:
        /// each flies out, slows, falls under gravity to the floor and goes out. White streaks that
        /// cool to blue.
        /// </summary>
        internal static void Sparks(Vector2 ground, float height, float age, int count, float speed, int seed)
        {
            if (age < 0f || age > 0.6f) return;
            for (int k = 0; k < count; k++)
            {
                int sd = seed + k * 17;
                float life = 0.22f + 0.3f * Rand(sd);
                if (age >= life) continue;
                float angle = Rand(sd + 1) * Mathf.PI * 2f, v = speed * (0.45f + 0.8f * Rand(sd + 2)), up = 1.2f * Rand(sd + 3);
                float t = age, t0 = Mathf.Max(0f, age - 0.035f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 now = SparkAt(ground, height, dir, v, up, t, life), before = SparkAt(ground, height, dir, v, up, t0, life);
                float heat = 1f - t / life;
                Color c = Color.Lerp(Halo, White, heat);
                Streak(before, now, 0.035f, Fade(c, 0.95f * Mathf.Min(1f, heat * 2f)), whiteGlow, Y + 0.045f, 2);
            }
        }

        private static Vector2 SparkAt(Vector2 ground, float height, Vector2 dir, float v, float up, float t, float life)
        {
            float out1 = v * t * (1f - 0.5f * t / life);
            float h = Mathf.Max(0f, height + up * t - 0.5f * 9f * t * t);
            return new Vector2(ground.x + dir.x * out1, ground.y + dir.y * out1 * 0.7f + Lifted(h));
        }

        /// <summary>
        /// The mark on the floor: a dark scorch with 4 to 6 short burn lines forking out of it and a blue ember glow
        /// that dies in 0.4 s. It grows over 0.1 s, stays <paramref name="stay"/> seconds and fades 0.6 s.
        /// </summary>
        internal static void Scorch(Vector2 ground, float age, float size, float stay, int seed)
        {
            if (age < 0f || age >= stay + 0.6f) return;
            float g = Smooth(age / 0.1f), a = age < stay ? 1f : 1f - (age - stay) / 0.6f;
            Sprite(ground, 0.9f * size * g, 0.66f * size * g, Fade(Char, 0.5f * a), soft, Floor + 0.011f);
            Sprite(ground, 0.5f * size * g, 0.36f * size * g, Fade(Char, 0.45f * a), soft, Floor + 0.0112f);
            int rays = 4 + (int)(Rand(seed + 5) * 2.99f);
            for (int k = 0; k < rays; k++)
            {
                float angle = (k + (Rand(seed + k) - 0.5f) * 0.8f) * Mathf.PI * 2f / rays, len = (0.1f + 0.26f * Rand(seed + 10 + k)) * size * g;
                Vector2 to = ground + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.75f) * len;
                int count = Jag(ground, to, 0.05f, seed + 20 + k * 7, 0.05f, Q);
                Widths(count, 0.06f * size, seed + k, 1f);
                Stroke(Q, count, 1f, Fade(Char, 0.28f * a), solid, Floor + 0.012f + k * 0.0002f);
            }
            if (age < 0.4f)
            {
                float e = 1f - age / 0.4f;
                Sprite(ground, 0.45f * size, 0.34f * size, Fade(Ember, 0.4f * e), glow, Floor + 0.014f);
            }
        }

        /// <summary>The EMP ring off a mechanoid: a blue ring that opens to 0.8 cells in 0.25 s round its chest.</summary>
        internal static void EmpRing(Vector2 chest, float age)
        {
            if (age < 0f || age >= 0.3f) return;
            float u = age / 0.3f, r = Mathf.Lerp(0.15f, 0.8f, Smooth(u));
            DrawMesh(ringMesh, chest, Y + 0.043f, r, r, 0f, Fade(Halo, 0.8f * (1f - u)), whiteGlow);
            DrawMesh(ringMesh, chest, Y + 0.0431f, r * 0.8f, r * 0.8f, 0f, Fade(Pale, 0.5f * (1f - u)), whiteGlow);
        }

        /// <summary>A faint ring on the floor at the true jump reach round a Soaked pawn a bolt leaves (6 cells), 0.35 s: the doubled reach is the rule the water adds.</summary>
        internal static void ReachRing(Vector2 ground, float radius, float age)
        {
            if (age < 0f || age >= 0.35f || radius <= 0f) return;
            float a = 1f - age / 0.35f;
            DrawMesh(thinRing, ground, Floor + 0.02f, radius, radius, 0f, Fade(Water, 0.12f * a), whiteGlow);
        }

        /// <summary>
        /// A Soaked pawn hit: steam puffs rise off it for 1 s, water flashes to white glints over its body
        /// for 0.5 s, and drops spray out and fall.
        /// </summary>
        internal static void SoakedHit(Vector2 feet, float age, int seed)
        {
            if (age < 0f || age >= 1.4f) return;
            Vector2 chest = Chest(feet);
            // Steam: 8 puffs that boil off the body and rise a cell.
            for (int k = 0; k < 8; k++)
            {
                float born = k * 0.04f, t = age - born;
                if (t < 0f || t >= 1.2f) continue;
                float u = t / 1.2f, x = (Rand(seed + k) - 0.5f) * 0.6f;
                Vector2 at = new Vector2(chest.x + x * (1f + 0.6f * u), chest.y - 0.1f + 1.0f * Smooth(u) + 0.1f * Rand(seed + 20 + k));
                float size = 0.35f + 0.6f * u;
                Sprite(at, size, size * 0.85f, Fade(Steam, 0.55f * (1f - u) * Mathf.Min(1f, t / 0.06f)), soft, Y + 0.046f + k * 0.0003f);
            }
            // The water on the body flashes: glints blink over it for 0.5 s.
            if (age < 0.5f)
            {
                int step = Step(age);
                for (int k = 0; k < 12; k++)
                {
                    int sd = seed + 100 + k * 7 + step * 3;
                    if (Rand(sd) < 0.4f) continue;
                    Vector2 at = chest + new Vector2((Rand(seed + 200 + k) - 0.5f) * 0.6f, (Rand(seed + 300 + k) - 0.5f) * 0.8f);
                    float size = 0.1f + 0.08f * Rand(sd + 1);
                    Sprite(at, size * 2.6f, size * 2.6f, Fade(Water, 0.5f), glow, Y + 0.047f);
                    Sprite(at, size, size, Fade(White, 1f), glow, Y + 0.048f);
                }
            }
            // Drops thrown off: blue, lit by the flash, falling to the floor.
            for (int k = 0; k < 14; k++)
            {
                int sd = seed + 400 + k * 11;
                float life = 0.35f + 0.2f * Rand(sd);
                if (age >= life) continue;
                float angle = Rand(sd + 1) * Mathf.PI * 2f, v = 1.8f + 2.2f * Rand(sd + 2);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 at = SparkAt(feet, ChestH, dir, v, 1.6f, age, life * 2f);
                float fade = 1f - age / life;
                Sprite(at, 0.2f, 0.2f, Fade(Water, 0.45f * fade), glow, Y + 0.043f);
                Sprite(at, 0.09f, 0.09f, Fade(Glint, 0.95f * fade), glow, Y + 0.044f);
            }
        }
    }
}
