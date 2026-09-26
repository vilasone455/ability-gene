using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces of the Frost Gun: the bolt and its frost trail, the frost puff of a hit, the
    /// haze and marks of a Chilled pawn, the charge at the muzzle, the freezing beam, the frost on the
    /// floor, the ice block, its shards, and the thaw. There is no sketch; the look is designed here
    /// and checked in the lab's recordings.
    ///
    /// Rules kept: everything rises out of the floor or stays on it; the beam, the charge, the glows
    /// and the edge highlights are additive light (MoteGlow); the ice faces are translucent
    /// alpha-blended fills so the frozen pawn shows through; shards are irregular polygons that land
    /// and melt; frost on the floor stays after the ice is gone. Height is drawn as a shift north
    /// (<see cref="SixPathsHeight.Lift"/>). The ice block, the frost and the haze are level or
    /// target-centred shapes, so nothing here has a per-facing method; only the beam and the bolt
    /// follow the aim, and they lie flat.
    ///
    /// Every routine takes ages and keeps no state. Strips come from VfxDraw's pool: call
    /// <see cref="VfxDraw.Begin"/> with the effect's ground point first.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class FrostGunGraphics
    {
        internal static readonly Color IceWhite = new Color(0.94f, 0.98f, 1f), IceLit = new Color(0.74f, 0.93f, 1f),
            Ice = new Color(0.50f, 0.80f, 0.97f), IceDark = new Color(0.20f, 0.46f, 0.74f), IceDeep = new Color(0.08f, 0.24f, 0.50f),
            Frost = new Color(0.86f, 0.94f, 1f), Cold = new Color(0.42f, 0.76f, 1f), Wet = new Color(0.16f, 0.26f, 0.38f);

        internal static readonly float Y = AltitudeLayer.MoteOverhead.AltitudeFor();
        /// <summary>Just under the pawn: the far faces of the ice and the spikes behind the pawn, so the pawn stands inside the block.</summary>
        internal static readonly float Behind = AltitudeLayer.Pawn.AltitudeFor() - 0.03f;

        // The ice block around a standing pawn. The pawn's draw point is its body centre; the block's
        // floor footprint is centred FeetBack south of it, where the feet are. A humanlike draws from
        // about 0.55 below that point to 0.65 above it and 0.7 wide; the block covers 0.66 below to
        // 0.85 above and 1.0 wide.
        internal const int Faces = 7;
        internal const float BlockRx = 0.50f, BlockRz = 0.36f, BlockH = 1.36f, TopShrink = 0.78f, FeetBack = 0.30f;
        /// <summary>Muzzle of the held gun: Core draws a gun 0.4 cells along the aim from the pawn and this one's muzzle is 0.45 further.</summary>
        internal const float MuzzleAlong = 0.85f;
        /// <summary>The latest a shard can land after the shatter (the highest, fastest-rising one).</summary>
        internal const float ShardLanding = 0.9f;
        internal const float Gravity = 9f;
        private const int Shards = 18, SpikeCount = 6;

        // Key light for the facets: from the west and a little south, so the south-west faces are lit.
        private static readonly Vector2 Light = new Vector2(-0.92f, -0.39f);

        private static readonly Vector2[] Base = new Vector2[Faces], Top = new Vector2[Faces];
        private static readonly float[] BaseH = new float[Faces], TopH = new float[Faces];
        private static readonly Vector2[] Pts = new Vector2[VfxDraw.MostPoints];
        private static readonly float[] Wid = new float[VfxDraw.MostPoints];

        internal static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;
        private static float Frac(float x) => x - Mathf.Floor(x);
        private static Vector2 Lift(Vector2 ground, float h) => new Vector2(ground.x, ground.y + h * SixPathsHeight.Lift);
        private static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));

        /// <summary>The held gun's muzzle for a pawn drawn at <paramref name="pawn"/> aiming along <paramref name="aim"/> (unit).</summary>
        internal static Vector2 Muzzle(Vector2 pawn, Vector2 aim) => pawn + aim * MuzzleAlong;

        // ------------------------------------------------------------------ primitives

        /// <summary>A filled convex polygon through the first <paramref name="n"/> points of <paramref name="p"/>, as a fan.</summary>
        internal static void Poly(Vector2[] p, int n, Color colour, Material material, float altitude)
        {
            if (n < 3 || colour.a <= 0.001f) return;
            Vector2 c = Vector2.zero;
            for (int i = 0; i < n; i++) c += p[i];
            c /= n;
            Sides(n + 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++) { a[i] = c; b[i] = p[i % n]; }
            Strip(a, b, colour, material, altitude);
        }

        /// <summary>A quad p0 → p1 → p2 → p3 (in order round it).</summary>
        internal static void Quad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color colour, Material material, float altitude)
        {
            if (colour.a <= 0.001f) return;
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = p0; a[1] = p3; b[0] = p1; b[1] = p2;
            Strip(a, b, colour, material, altitude);
        }

        /// <summary>A straight bar of even width from a to b.</summary>
        internal static void Bar(Vector2 a, Vector2 b, float width, Color colour, Material material, float altitude)
        {
            Vector2 d = b - a;
            float l = d.magnitude;
            if (l < 1e-5f || colour.a <= 0.001f) return;
            Vector2 n = new Vector2(-d.y, d.x) / l * (width / 2f);
            Quad(a + n, b + n, b - n, a - n, colour, material, altitude);
        }

        /// <summary>A ribbon through the first <paramref name="n"/> points of Pts, Wid[i] cells wide at each.</summary>
        private static void Ribbon(int n, Color colour, Material material, float altitude)
        {
            if (n < 2 || colour.a <= 0.001f) return;
            Sides(n, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Pts[Mathf.Min(n - 1, i + 1)] - Pts[Mathf.Max(0, i - 1)];
                float l = d.magnitude;
                Vector2 side = l < 1e-6f ? Vector2.zero : new Vector2(-d.y, d.x) / l * (Wid[i] / 2f);
                a[i] = Pts[i] + side;
                b[i] = Pts[i] - side;
            }
            Strip(a, b, colour, material, altitude);
        }

        /// <summary>A line from a to b that is <paramref name="width"/> wide at a and nothing at b.</summary>
        internal static void Taper(Vector2 a, Vector2 b, float width, Color colour, Material material, float altitude, int steps = 4)
        {
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;
                Pts[i] = Vector2.Lerp(a, b, u);
                Wid[i] = width * (1f - u);
            }
            Ribbon(steps + 1, colour, material, altitude);
        }

        /// <summary>A four-point glint: two crossed thin bars and a soft glow.</summary>
        internal static void Glint(Vector2 at, float size, float alpha, float altitude, float turn = 0f)
        {
            if (alpha <= 0.01f || size <= 0f) return;
            Sprite(at, size * 1.3f, size * 1.3f, Fade(Cold, alpha * 0.45f), glow, altitude);
            Sprite(at, size, size * 0.16f, Fade(IceWhite, alpha), whiteGlow, altitude + 0.0002f, turn);
            Sprite(at, size, size * 0.16f, Fade(IceWhite, alpha), whiteGlow, altitude + 0.0003f, turn + 90f);
            Sprite(at, size * 0.55f, size * 0.12f, Fade(IceWhite, alpha * 0.7f), whiteGlow, altitude + 0.0004f, turn + 45f);
            Sprite(at, size * 0.55f, size * 0.12f, Fade(IceWhite, alpha * 0.7f), whiteGlow, altitude + 0.0005f, turn + 135f);
        }

        // ------------------------------------------------------------------ the shot

        /// <summary>
        /// The bolt in flight: a pale core, a cold glow, a tapering frost trail and flecks of frost it
        /// leaves behind that drift and fade. <paramref name="origin"/> is where it left, <paramref name="head"/>
        /// where it is now, <paramref name="speed"/> cells a second (the flecks' ages come from it).
        /// </summary>
        internal static void Bolt(Vector2 origin, Vector2 head, float speed, float altitude, float seconds)
        {
            Vector2 run = head - origin;
            float travelled = run.magnitude;
            if (travelled < 0.01f) return;
            Vector2 dir = run / travelled, side = new Vector2(-dir.y, dir.x);
            float angle = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float trail = Mathf.Min(travelled, FrostGunShotTiming.TrailLength);

            // Flecks: one every FleckStep cells along the path, each fading FleckLife after the head passed it.
            int first = Mathf.Max(0, Mathf.FloorToInt((travelled - FrostGunShotTiming.FleckLife * speed) / FrostGunShotTiming.FleckStep));
            int last = Mathf.FloorToInt(travelled / FrostGunShotTiming.FleckStep);
            for (int k = first; k <= last; k++)
            {
                float d = k * FrostGunShotTiming.FleckStep + (Rand(k + 40) - 0.5f) * 0.12f;
                float age = (travelled - d) / speed;
                if (d < 0.2f || age < 0f || age > FrostGunShotTiming.FleckLife) continue;
                float u = age / FrostGunShotTiming.FleckLife;
                Vector2 at = origin + dir * d + side * ((Rand(k + 41) - 0.5f) * 0.2f + (Rand(k + 42) - 0.5f) * 0.25f * u) + new Vector2(0f, -0.18f * u);
                float size = 0.05f * (1f - 0.6f * u);
                Sprite(at, size, size, Fade(IceWhite, 0.85f * (1f - u)), whiteGlow, altitude + 0.0001f, 45f + k * 37f);
            }

            // The trail: a wide cold glow and a thin white line, both narrowing to nothing behind.
            Taper(head, head - dir * trail, 0.22f, Fade(Cold, 0.6f), glow, altitude + 0.0002f, 6);
            Taper(head, head - dir * Mathf.Min(trail, 1.0f), 0.05f, Fade(IceWhite, 0.7f), whiteGlow, altitude + 0.0003f, 4);

            // The head.
            Sprite(head - dir * 0.05f, 0.62f, 0.30f, Fade(Cold, 0.55f), glow, altitude + 0.0004f, angle);
            Sprite(head, 0.42f, 0.13f, Fade(IceWhite, 1f), glow, altitude + 0.0005f, angle);
            Sprite(head, 0.2f, 0.06f, Fade(IceWhite, 1f), glow, altitude + 0.00055f, angle);
            Glint(head + dir * 0.08f, 0.12f, 0.5f + 0.3f * Mathf.Sin(seconds * 40f), altitude + 0.0006f, seconds * 400f);
        }

        /// <summary>
        /// A hit: a frost puff where the bolt struck, ice chips thrown off that land and fade, and a
        /// rime spot on the floor that stays <see cref="FrostGunShotTiming.RimeLife"/>. <paramref name="at"/>
        /// is the struck point (a pawn's draw point, or the floor); <paramref name="onPawn"/> puts the
        /// puff at the body and the rime at the feet.
        /// </summary>
        internal static void Puff(Vector2 at, float age, int seed, bool onPawn, Vector2 aim, Vector2 sun)
        {
            if (age < 0f) return;
            Vector2 feet = onPawn ? at + new Vector2(0f, -FeetBack - 0.05f) : at;

            // Rime on the floor.
            float rime = 1f - Smooth((age - (FrostGunShotTiming.RimeLife - 0.8f)) / 0.8f);
            if (rime > 0f)
            {
                float g = Smooth(age / 0.2f);
                Sprite(feet, 0.62f * g, 0.40f * g, Fade(Frost, 0.32f * rime), soft, Floor + 0.012f);
                for (int i = 0; i < 5; i++)
                {
                    float a = (i / 5f + Rand(seed + i) * 0.15f) * Mathf.PI * 2f, l = (0.16f + 0.18f * Rand(seed + i + 10)) * g;
                    Taper(feet, feet + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * l, 0.035f, Fade(IceWhite, 0.5f * rime), solid, Floor + 0.013f + i * 0.0001f);
                }
            }

            // Ice chips: short ballistic flights from the struck point, then they lie and fade.
            float h0 = onPawn ? 0.35f : 0.05f;
            for (int i = 0; i < 6; i++)
            {
                int s = seed * 7 + i;
                float theta = Mathf.Atan2(-aim.y, -aim.x) + (Rand(s + 20) - 0.5f) * 2.6f;
                float v = 0.9f + 1.1f * Rand(s + 21), v0 = 0.6f + 0.9f * Rand(s + 22), size = 0.045f + 0.03f * Rand(s + 23);
                Shard(feet, theta, 0.05f, h0, v, v0, 9f + 8f * Rand(s + 24), size, 3 + i % 2, s, age, FrostGunShotTiming.ChipLife - 0.3f, sun, 1f);
            }

            // The puff.
            float u = age / FrostGunShotTiming.PuffLife;
            if (u < 1f)
            {
                Vector2 c = onPawn ? at + new Vector2(0f, 0.05f) : Lift(at, 0.15f);
                Sprite(c, 0.7f * (1f - u), 0.7f * (1f - u), Fade(Cold, 0.7f * (1f - u)), glow, Y + 0.010f);
                for (int i = 0; i < 4; i++)
                {
                    float a = Rand(seed + 30 + i) * Mathf.PI * 2f, r = 0.2f * Smooth(u) * (0.6f + Rand(seed + 34 + i));
                    float size = 0.25f + 0.45f * Smooth(u);
                    Sprite(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r + new Vector2(0f, 0.12f * u), size, size * 0.85f,
                        Fade(i % 2 == 0 ? Frost : IceLit, 0.55f * Mathf.Pow(1f - u, 1.3f)), soft, Y + 0.011f + i * 0.0001f);
                }
                Glint(c, 0.4f * (1f - u), 1f - u, Y + 0.012f, 20f);
            }
        }

        /// <summary>
        /// One irregular ice shard thrown from <paramref name="ground"/>: <paramref name="r0"/> cells out along
        /// <paramref name="theta"/> (radians) at height <paramref name="h0"/>, flying out at <paramref name="v"/> and
        /// up at <paramref name="v0"/> cells a second. It tumbles in flight, skids when it lands, lies, and
        /// melts from 0.25 s after landing to <paramref name="melt"/> s after landing, leaving a wet spot.
        /// </summary>
        internal static void Shard(Vector2 ground, float theta, float r0, float h0, float v, float v0, float spin, float size, int corners,
            int seed, float t, float melt, Vector2 sun, float alpha)
        {
            if (t < 0f) return;
            float land = (v0 + Mathf.Sqrt(v0 * v0 + 2f * Gravity * h0)) / Gravity;
            float tf = Mathf.Min(t, land);
            float h = Mathf.Max(0f, h0 + v0 * tf - 0.5f * Gravity * tf * tf);
            float skid = t > land ? v * 0.07f * Smooth((t - land) / 0.12f) : 0f;
            Vector2 dir = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            Vector2 floor = ground + dir * (r0 + v * tf + skid);
            float m = Smooth((t - land - 0.25f) / Mathf.Max(0.05f, melt - 0.25f));
            float a = alpha * (1f - m);
            if (a <= 0.01f && m >= 1f)
            {
                // The water it left, drying.
                float dry = 1f - Smooth((t - land - melt) / 0.8f);
                if (dry > 0f) Sprite(floor, size * 2.2f, size * 1.5f, Fade(Wet, 0.20f * dry), soft, Floor + 0.008f);
                return;
            }
            if (t > land) Sprite(floor, size * 2.4f * (0.4f + m), size * 1.6f * (0.4f + m), Fade(Wet, 0.22f * Mathf.Min(1f, (t - land) * 3f)), soft, Floor + 0.008f);
            if (h > 0.01f) Sprite(floor + sun * h, size * 1.8f, size * 1.4f, Fade(Color.black, 0.18f * a), soft, Floor + 0.009f);

            float turn = t < land ? spin * t : spin * land;
            float squash = t < land ? 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(turn * 0.7f)) : 1f;
            bool lit = Mathf.Cos(turn * 0.7f) >= 0f || t >= land;
            float scale = size * (1f - 0.55f * m);
            Vector2 c = Lift(floor, h);
            float ca = Mathf.Cos(turn), sa = Mathf.Sin(turn);
            for (int i = 0; i < corners; i++)
            {
                float ang = (i + (Rand(seed * 5 + i) - 0.5f) * 0.6f) / corners * Mathf.PI * 2f;
                float r = scale * (0.6f + 0.7f * Rand(seed * 5 + i + 50));
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r * squash;
                Pts[i] = c + new Vector2(x * ca - z * sa, x * sa + z * ca);
            }
            float alt = (h > 0.01f ? Y : Floor + 0.02f) + (seed % 50) * 0.00002f;
            // A white rim: the same shape a little larger behind the face.
            for (int i = 0; i < corners; i++) Pts[corners + i] = c + (Pts[i] - c) * 1.3f;
            PolyFrom(corners, corners, Fade(IceWhite, 0.6f * a), whiteGlow, alt);
            PolyFrom(0, corners, Fade(lit ? IceLit : Ice, 0.85f * a), solid, alt + 0.00001f);
            PolyFrom(0, corners, Fade(Cold, 0.25f * a), whiteGlow, alt + 0.00002f);
        }

        private static readonly Vector2[] PolyBuffer = new Vector2[16];

        private static void PolyFrom(int start, int n, Color colour, Material material, float altitude)
        {
            for (int i = 0; i < n; i++) PolyBuffer[i] = Pts[start + i];
            Poly(PolyBuffer, n, colour, material, altitude);
        }

        /// <summary>
        /// A Chilled pawn: a faint cold haze round the body, rime at the feet, a few frost motes
        /// falling, and one frost mark per stack in a row over the head.
        /// </summary>
        internal static void ChillHaze(Vector2 pawn, int stacks, float seconds, int seed)
        {
            if (stacks <= 0) return;
            float pulse = 0.85f + 0.15f * Mathf.Sin(seconds * 4.4f + seed);
            Sprite(pawn + new Vector2(0f, 0.05f), 1.05f, 1.35f, Fade(Cold, (0.05f + 0.035f * stacks) * pulse), glow, Y + 0.001f);
            Sprite(pawn + new Vector2(0f, -FeetBack - 0.08f), 0.95f, 0.48f, Fade(Frost, 0.09f * stacks), soft, Floor + 0.011f);
            for (int i = 0; i < 2 * stacks; i++)
            {
                float u = Frac(seconds / 1.4f + Rand(seed + i));
                float x = (Rand(seed + i + 20) - 0.5f) * 0.7f, h = 0.75f * (1f - u);
                Sprite(new Vector2(pawn.x + x, pawn.y - FeetBack + h * SixPathsHeight.Lift + 0.1f), 0.05f, 0.05f, Fade(IceWhite, 0.65f * Bump(u)), whiteGlow, Y + 0.002f + i * 0.0001f, 45f);
            }
            for (int k = 0; k < stacks; k++)
            {
                Vector2 at = pawn + new Vector2((k - (stacks - 1) / 2f) * 0.2f, 0.82f);
                Glint(at, 0.17f, 0.85f, Y + 0.004f + k * 0.001f);
            }
        }

        // ------------------------------------------------------------------ Flash Freeze

        /// <summary>
        /// The charge at the muzzle over the warmup: frost motes spiral in, a cold glow and a white core
        /// grow, a ring closes in. <paramref name="u"/> is the warmup's share done.
        /// </summary>
        internal static void Charge(Vector2 muzzle, float u, float seconds)
        {
            if (u <= 0f) return;
            float g = Smooth(u), pulse = 0.85f + 0.15f * Mathf.Sin(seconds * 30f);
            Sprite(muzzle, 0.2f + 0.6f * g, 0.2f + 0.6f * g, Fade(Cold, (0.3f + 0.45f * g) * pulse), glow, Y + 0.020f);
            DrawMesh(disc, muzzle, Y + 0.021f, 0.03f + 0.07f * g, 0.03f + 0.07f * g, 0f, Fade(IceWhite, 0.6f + 0.4f * g), whiteGlow);
            for (int i = 0; i < 10; i++)
            {
                float v = Frac(seconds / 0.45f + Rand(i + 60));
                float r = 0.75f * (1f - v) * (1f - v), a = Rand(i + 61) * Mathf.PI * 2f + 2.5f * v;
                Vector2 at = muzzle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Sprite(at, 0.05f, 0.05f, Fade(IceWhite, 0.8f * Bump(v) * Mathf.Min(1f, u * 3f)), whiteGlow, Y + 0.022f + i * 0.0001f, 45f);
            }
            float ring = Frac(seconds / 0.4f);
            Circle(muzzle, 0.55f * (1f - ring) + 0.05f, 0.45f * ring * g, Y + 0.023f, IceLit);
        }

        /// <summary>
        /// The beam from the muzzle toward <paramref name="to"/> (the target's chest). <paramref name="head"/> 0 to 1
        /// is how far its front has gone; <paramref name="fade"/> dims it out after it lands. Layers of light:
        /// a wide cold glow, a pale middle, a white core, and two thin frost filaments wound round it.
        /// </summary>
        internal static void Beam(Vector2 from, Vector2 to, float head, float fade, float seconds)
        {
            if (head <= 0f || fade <= 0.01f) return;
            Vector2 end = Vector2.Lerp(from, to, head), run = end - from;
            float length = run.magnitude;
            if (length < 0.01f) return;
            Vector2 dir = run / length, side = new Vector2(-dir.y, dir.x);
            Bar(from, end, 0.40f * fade, Fade(Cold, 0.22f * fade), glow, Y + 0.030f);
            Bar(from, end, 0.14f, Fade(IceLit, 0.55f * fade), whiteGlow, Y + 0.031f);
            Bar(from, end, 0.05f, Fade(IceWhite, 0.95f * fade), whiteGlow, Y + 0.032f);
            int n = Mathf.Clamp(Mathf.CeilToInt(length / 0.12f) + 1, 2, 120);
            for (int f = 0; f < 2; f++)
            {
                for (int i = 0; i < n; i++)
                {
                    float d = length * i / (n - 1);
                    Pts[i] = from + dir * d + side * (0.07f * Mathf.Sin(d * 5f - seconds * 24f + f * Mathf.PI));
                    Wid[i] = 0.022f;
                }
                Ribbon(n, Fade(IceWhite, 0.45f * fade), whiteGlow, Y + 0.033f + f * 0.0001f);
            }
            Sprite(from, 0.5f, 0.5f, Fade(Cold, 0.6f * fade), glow, Y + 0.034f);
            Sprite(end, 0.6f, 0.6f, Fade(Cold, 0.7f * fade), glow, Y + 0.035f);
            Glint(end, 0.3f, fade, Y + 0.036f, seconds * 200f);
        }

        /// <summary>
        /// Frost on the floor round the frozen pawn's feet: a pale patch, ice rays with side branches,
        /// and glitter. <paramref name="grow"/> 0 to 1 spreads it; <paramref name="alpha"/> fades it out.
        /// </summary>
        internal static void Stain(Vector2 feet, float grow, float alpha, int seed, float seconds)
        {
            if (grow <= 0f || alpha <= 0.01f) return;
            float g = Smooth(grow);
            Sprite(feet, 2.5f * g, 1.9f * g, Fade(Frost, 0.26f * alpha), soft, Floor + 0.014f);
            Sprite(feet, 1.35f * g, 1.0f * g, Fade(IceLit, 0.22f * alpha), soft, Floor + 0.015f);
            for (int i = 0; i < 12; i++)
            {
                float a = (i + (Rand(seed + 100 + i) - 0.5f) * 0.7f) / 12f * Mathf.PI * 2f;
                float l = (0.55f + 0.8f * Rand(seed + 120 + i)) * g;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.85f), start = feet + dir * 0.2f, tip = feet + dir * l;
                float alt = Floor + 0.016f + i * 0.0002f;
                Taper(start, tip, 0.045f, Fade(IceWhite, 0.38f * alpha), solid, alt);
                for (int b = 0; b < 2; b++)
                {
                    float at = 0.45f + 0.25f * b, turn = (b == 0 ? 1f : -1f) * (0.6f + 0.3f * Rand(seed + 140 + i));
                    Vector2 root = Vector2.Lerp(start, tip, at);
                    Vector2 bd = new Vector2(dir.x * Mathf.Cos(turn) - dir.y * Mathf.Sin(turn), dir.x * Mathf.Sin(turn) + dir.y * Mathf.Cos(turn));
                    Taper(root, root + bd * l * 0.28f, 0.028f, Fade(IceWhite, 0.3f * alpha), solid, alt + 0.0001f);
                }
            }
            for (int i = 0; i < 7; i++)
            {
                float a = Rand(seed + 160 + i) * Mathf.PI * 2f, r = (0.3f + 0.9f * Rand(seed + 170 + i)) * g;
                float tw = Bump(Frac(seconds * 0.9f + Rand(seed + 180 + i)));
                Glint(feet + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * r, 0.12f, 0.7f * tw * alpha, Floor + 0.02f + i * 0.001f, 15f);
            }
        }

        /// <summary>
        /// The ice block round a pawn drawn at <paramref name="pawn"/>. A 7-sided faceted prism stands on
        /// the floor round the feet and rises 1.36 cells, with a jagged top and six spikes round its foot.
        /// <paramref name="rise"/> 0 to 1 grows it out of the floor; <paramref name="shrink"/> 0 to 1 melts it
        /// back down (the thaw); <paramref name="cap"/> above 0 cuts every column to a stump that tall
        /// (what is left after a shatter). Faces facing north are drawn under the pawn, faces facing south
        /// and the top over it, all translucent, so the pawn stands inside. Faces are shaded by a west light,
        /// edges carry white highlights, and the whole block has a cold glow.
        /// </summary>
        internal static void IceBlock(Vector2 pawn, float rise, float shrink, float alpha, int seed, float seconds, float cap = 0f)
        {
            if (rise <= 0f || alpha <= 0.01f) return;
            float g = Smooth(rise), melt = Smooth(shrink);
            float hScale = g * (1f - 0.8f * melt), rScale = (0.62f + 0.38f * g) * (1f - 0.22f * melt);
            Vector2 c = pawn + new Vector2(0f, -FeetBack);
            for (int i = 0; i < Faces; i++)
            {
                float a = (-90f + i * 360f / Faces + (i == 0 ? 0f : (Rand(seed + i) - 0.5f) * 16f)) * Mathf.Deg2Rad;
                float rx = BlockRx * (0.9f + 0.2f * Rand(seed + 10 + i)) * rScale, rz = BlockRz * (0.9f + 0.2f * Rand(seed + 20 + i)) * rScale;
                Base[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz);
                BaseH[i] = 0f;
                float at = a + (Rand(seed + 30 + i) - 0.5f) * 0.2f;
                Top[i] = c + new Vector2(Mathf.Cos(at) * rx * TopShrink, Mathf.Sin(at) * rz * TopShrink);
                float h = BlockH * (0.84f + 0.24f * Rand(seed + 40 + i)) * hScale;
                if (cap > 0f) h = Mathf.Min(h, cap * (0.55f + 0.9f * Rand(seed + 50 + i)));
                TopH[i] = h;
            }

            // Behind the pawn: the cold glow, the north faces and the north spikes.
            float pulse = 0.85f + 0.15f * Mathf.Sin(seconds * 4.4f + seed);
            if (cap <= 0f)
                Sprite(pawn + new Vector2(0f, 0.18f * hScale), 1.9f * rScale, 2.3f * hScale + 0.4f, Fade(Cold, 0.20f * alpha * pulse), glow, Behind - 0.004f);
            Sprite(c, 1.25f * rScale, 0.9f * rScale, Fade(Frost, 0.35f * alpha), soft, Floor + 0.02f);
            int back = 0, front = 0;
            for (int i = 0; i < Faces; i++)
            {
                int j = (i + 1) % Faces;
                Vector2 n = Normal(i, j, c);
                float lit = Mathf.Clamp01(0.5f + 0.6f * Vector2.Dot(n, Light));
                if (n.y > 0f)
                {
                    Quad(Base[i], Base[j], P(Top[j], TopH[j]), P(Top[i], TopH[i]), Fade(Mix(IceDark, Ice, lit), 0.24f * alpha), solid, Behind - 0.003f + back * 0.0001f);
                    back++;
                }
            }
            Spikes(pawn, c, rise, melt, alpha, seed, cap > 0f, true);

            // In front of the pawn: south faces in two bands (frosted foot, clear upper), edges, top, highlights.
            for (int i = 0; i < Faces; i++)
            {
                int j = (i + 1) % Faces;
                Vector2 n = Normal(i, j, c);
                if (n.y > 0f) continue;
                float lit = Mathf.Clamp01(0.5f + 0.6f * Vector2.Dot(n, Light));
                Color face = Mix(IceDark, IceLit, lit);
                float alt = Y + 0.040f + front * 0.0004f;
                Vector2 ti = P(Top[i], TopH[i]), tj = P(Top[j], TopH[j]);
                Vector2 mi = Vector2.Lerp(Base[i], ti, 0.28f), mj = Vector2.Lerp(Base[j], tj, 0.28f);
                Quad(Base[i], Base[j], mj, mi, Fade(Mix(face, IceWhite, 0.5f), (0.50f + 0.10f * lit) * alpha), solid, alt);
                Quad(mi, mj, tj, ti, Fade(face, (0.40f - 0.08f * lit) * alpha), solid, alt + 0.0001f);
                // A darker inner facet on each face, off-centre, so a face reads as cut ice rather than a pane.
                Vector2 fc = (Base[i] + Base[j] + ti + tj) / 4f;
                Vector2 q0 = Vector2.Lerp(mi, fc, 0.35f), q1 = Vector2.Lerp(Vector2.Lerp(mi, mj, 0.6f), fc, 0.2f), q2 = Vector2.Lerp(tj, fc, 0.3f), q3 = Vector2.Lerp(ti, fc, 0.45f);
                Quad(q0, q1, q2, q3, Fade(lit > 0.5f ? IceWhite : IceDeep, (lit > 0.5f ? 0.26f : 0.22f) * alpha), solid, alt + 0.0002f);
                // Cold light through the face: additive, so the ice stays blue over any floor or skin.
                Quad(Base[i], Base[j], tj, ti, Fade(Cold, (0.16f - 0.06f * lit) * alpha), whiteGlow, alt + 0.00025f);
                // Frost cracks.
                if (Rand(seed + 60 + i) > 0.35f && cap <= 0f)
                {
                    Vector2 k0 = Vector2.Lerp(Vector2.Lerp(Base[i], Base[j], 0.3f + 0.4f * Rand(seed + 61 + i)), fc, 0.3f);
                    Vector2 k1 = Vector2.Lerp(Vector2.Lerp(ti, tj, 0.2f + 0.6f * Rand(seed + 62 + i)), fc, 0.25f);
                    Vector2 k2 = Vector2.Lerp(k0, k1, 0.55f) + new Vector2((Rand(seed + 63 + i) - 0.5f) * 0.25f, 0.05f);
                    Bar(k0, Vector2.Lerp(k0, k1, 0.55f), 0.014f, Fade(IceWhite, 0.30f * alpha), whiteGlow, alt + 0.0003f);
                    Bar(Vector2.Lerp(k0, k1, 0.55f), k1, 0.012f, Fade(IceWhite, 0.24f * alpha), whiteGlow, alt + 0.0003f);
                    Bar(Vector2.Lerp(k0, k1, 0.55f), k2, 0.010f, Fade(IceWhite, 0.22f * alpha), whiteGlow, alt + 0.0003f);
                }
                front++;
            }
            // Vertical edges between two south faces, brightest at the middle of the front.
            for (int i = 0; i < Faces; i++)
            {
                int h = (i + Faces - 1) % Faces, j = (i + 1) % Faces;
                bool westFront = Normal(h, i, c).y <= 0f, eastFront = Normal(i, j, c).y <= 0f;
                if (!westFront && !eastFront) continue;
                float strength = westFront && eastFront ? 0.55f : 0.3f;
                Vector2 top = P(Top[i], TopH[i]);
                Taper(top, Vector2.Lerp(top, Base[i], 0.85f), westFront && eastFront ? 0.024f : 0.016f, Fade(IceWhite, strength * alpha), whiteGlow, Y + 0.046f + i * 0.0001f);
            }
            // The top: a lit polygon, a brighter inner facet, and a white rim along its south edge.
            for (int i = 0; i < Faces; i++) Pts[i] = P(Top[i], TopH[i]);
            System.Array.Copy(Pts, PolyBuffer, Faces);
            Poly(PolyBuffer, Faces, Fade(IceLit, 0.46f * alpha), solid, Y + 0.048f);
            Poly(PolyBuffer, Faces, Fade(Cold, 0.12f * alpha), whiteGlow, Y + 0.0481f);
            Vector2 tc = Vector2.zero;
            for (int i = 0; i < Faces; i++) tc += PolyBuffer[i];
            tc /= Faces;
            for (int i = 0; i < 4; i++) PolyBuffer[i] = Vector2.Lerp(Pts[(i + 3) % Faces], tc, 0.35f + 0.1f * i);
            Poly(PolyBuffer, 4, Fade(IceWhite, 0.22f * alpha), solid, Y + 0.0485f);
            for (int i = 0; i < Faces; i++)
            {
                int j = (i + 1) % Faces;
                bool south = Normal(i, j, c).y <= 0f;
                Bar(Pts[i], Pts[j], south ? 0.02f : 0.012f, Fade(IceWhite, (south ? 0.6f : 0.25f) * (cap > 0f ? 0.35f : 1f) * alpha), whiteGlow, Y + 0.049f + i * 0.0001f);
            }
            // Glints on the lit side and a soft white bloom at the foot.
            if (cap <= 0f)
            {
                Glint(Vector2.Lerp(Pts[5], Pts[6], 0.4f), 0.2f, (0.55f + 0.45f * Bump(Frac(seconds * 0.6f + 0.2f))) * alpha * g, Y + 0.051f, 10f);
                Glint(Vector2.Lerp(Base[6], Pts[6], 0.62f), 0.15f, 0.7f * Bump(Frac(seconds * 0.45f)) * alpha * g, Y + 0.052f, 30f);
                Glint(Vector2.Lerp(Base[1], Pts[1], 0.45f), 0.13f, 0.6f * Bump(Frac(seconds * 0.5f + 0.55f)) * alpha * g, Y + 0.053f, 5f);
            }
            Sprite(c + new Vector2(0f, -0.28f * rScale), 1.3f * rScale, 0.42f, Fade(IceWhite, 0.30f * alpha), soft, Y + 0.0395f);
            if (cap <= 0f) Sprite(pawn + new Vector2(0f, 0.05f), 0.95f * rScale, 1.25f * hScale, Fade(Frost, 0.22f * alpha), soft, Y + 0.0392f);
            Spikes(pawn, c, rise, melt, alpha, seed, cap > 0f, false);
        }

        private static Vector2 P(Vector2 ground, float h) => Lift(ground, h);

        /// <summary>Outward horizontal normal of the side between base corners i and j.</summary>
        private static Vector2 Normal(int i, int j, Vector2 c)
        {
            Vector2 mid = (Base[i] + Base[j]) / 2f - c, edge = Base[j] - Base[i];
            Vector2 n = new Vector2(edge.y, -edge.x);
            if (Vector2.Dot(n, mid) < 0f) n = -n;
            return n.sqrMagnitude < 1e-8f ? Vector2.down : n.normalized;
        }

        /// <summary>
        /// Spikes round the block's foot: each a leaning pyramid of two visible faces (lit and dark) and a
        /// white ridge. <paramref name="behind"/> draws the ones north of the feet (under the pawn), else
        /// the rest. They come up <see cref="FrostGunFreezeTiming.SpikeDelay"/> after the block and go first
        /// in a thaw; a shattered block keeps only their stubs.
        /// </summary>
        private static void Spikes(Vector2 pawn, Vector2 c, float rise, float melt, float alpha, int seed, bool stubs, bool behind)
        {
            float riseSeconds = rise * FrostGunFreezeTiming.Grow;
            float up = Smooth((riseSeconds - FrostGunFreezeTiming.SpikeDelay) / 0.22f) * (1f - Smooth(melt * 1.6f));
            if (up <= 0.01f) return;
            for (int k = 0; k < SpikeCount; k++)
            {
                float a = (-150f + k * 60f + (Rand(seed + 200 + k) - 0.5f) * 30f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool north = dir.y > 0.25f;
                if (north != behind) continue;
                float hs = (0.5f + 0.45f * Rand(seed + 210 + k)) * up, lean = 0.2f + 0.2f * Rand(seed + 220 + k), hw = 0.11f + 0.05f * Rand(seed + 230 + k);
                if (stubs) hs = Mathf.Min(hs, 0.12f);
                Vector2 foot = c + new Vector2(dir.x * BlockRx * 0.95f, dir.y * BlockRz * 0.95f);
                Vector2 across = new Vector2(-dir.y, dir.x) * hw;
                Vector2 tip = P(foot + dir * lean * (hs / 0.95f), hs);
                Vector2 l = foot + across, r = foot - across;
                // The half facing the west light is lit.
                bool leftLit = Vector2.Dot(across, Light) > 0f;
                float alt = (north ? Behind - 0.002f : Y + 0.054f) + k * 0.0003f;
                Quad(l, foot, tip, tip, Fade(leftLit ? IceLit : IceDark, 0.78f * alpha), solid, alt);
                Quad(foot, r, tip, tip, Fade(leftLit ? IceDark : IceLit, 0.78f * alpha), solid, alt + 0.0001f);
                Bar(Vector2.Lerp(foot, tip, 0.15f), tip, 0.012f, Fade(IceWhite, 0.6f * alpha), whiteGlow, alt + 0.0002f);
            }
        }

        /// <summary>
        /// The shatter, <paramref name="age"/> seconds after it: a white flash, a burst of frost mist and
        /// ice dust, eighteen irregular shards thrown out that land, skid, lie and melt, and the
        /// stumps of the block left standing, melting too.
        /// </summary>
        internal static void Shatter(Vector2 pawn, float age, int seed, float seconds, Vector2 sun)
        {
            if (age < 0f) return;
            Vector2 c = pawn + new Vector2(0f, -FeetBack), body = pawn + new Vector2(0f, 0.15f);
            float stump = 1f - Smooth((age - 0.3f) / (FrostGunFreezeTiming.ShardMelt - 0.3f));
            IceBlock(pawn, 1f, 0f, 0.85f * stump, seed, seconds, 0.2f);

            for (int k = 0; k < Shards; k++)
            {
                int s = seed * 13 + k;
                float theta = (k + (Rand(s) - 0.5f) * 0.8f) / Shards * Mathf.PI * 2f;
                float h0 = 0.1f + 1.2f * Rand(s + 1), v = 1.6f + 1.8f * Rand(s + 2), v0 = 0.8f + 1.8f * Rand(s + 3);
                float size = 0.07f + 0.09f * Rand(s + 4);
                Vector2 from = c + new Vector2(0f, 0f);
                Shard(from, theta, 0.28f + 0.15f * Rand(s + 5), h0, v, v0, 7f + 10f * Rand(s + 6), size, 3 + k % 3, s, age, FrostGunFreezeTiming.ShardMelt, sun, 1f);
            }

            // Dust: tiny bright specks thrown out fast.
            for (int i = 0; i < 14; i++)
            {
                float u = age / 0.4f;
                if (u >= 1f) break;
                float a = Rand(seed + 300 + i) * Mathf.PI * 2f, sp = 2.5f + 2.5f * Rand(seed + 301 + i), h = 0.2f + 1.1f * Rand(seed + 302 + i);
                Vector2 at = P(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.3f + sp * age), h + 0.6f * age);
                Sprite(at, 0.05f, 0.05f, Fade(IceWhite, 0.9f * (1f - u)), whiteGlow, Y + 0.060f + i * 0.0001f, 45f);
            }

            // Mist.
            float mu = age / 0.9f;
            if (mu < 1f)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = (i + Rand(seed + 320 + i) * 0.5f) / 8f * Mathf.PI * 2f, h = 0.2f + 1.0f * Rand(seed + 321 + i);
                    Vector2 at = P(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.25f + 0.9f * Smooth(mu)), h + 0.2f * mu);
                    float size = 0.45f + 0.7f * Smooth(mu);
                    Sprite(at, size, size * 0.85f, Fade(i % 2 == 0 ? Frost : IceLit, 0.5f * Mathf.Pow(1f - mu, 1.4f)), soft, Y + 0.062f + i * 0.0001f);
                }
            }

            // Flash.
            float fu = age / FrostGunFreezeTiming.Flash;
            if (fu < 1f)
            {
                Sprite(body, 1.6f + 1.2f * fu, 2.0f + 1.2f * fu, Fade(Cold, 0.8f * (1f - fu)), glow, Y + 0.064f);
                Sprite(body, 0.9f, 1.3f, Fade(IceWhite, 0.9f * (1f - fu)), glow, Y + 0.065f);
                Glint(body, 0.9f * (1f - fu) + 0.2f, 1f - fu, Y + 0.066f, 20f);
            }
        }

        /// <summary>The thaw's water: drops running off the melting block, and the puddle it leaves.</summary>
        internal static void Thaw(Vector2 pawn, float age, float puddleAlpha, int seed)
        {
            if (age < 0f) return;
            Vector2 feet = pawn + new Vector2(0f, -FeetBack - 0.05f);
            WaterGunGraphics.Puddle(feet, age, 1.15f, 0.45f * puddleAlpha);
            float drip = 1f - Smooth((age - FrostGunFreezeTiming.Thaw) / 0.4f);
            if (drip <= 0f) return;
            WaterGunGraphics.Drips(pawn + new Vector2(-0.25f, -0.2f), age, drip, seed);
            WaterGunGraphics.Drips(pawn + new Vector2(0.22f, -0.15f), age + 0.3f, drip, seed + 5);
            WaterGunGraphics.Drips(pawn + new Vector2(0f, -0.3f), age + 0.6f, drip, seed + 9);
        }
    }
}
