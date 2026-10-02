using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoMimicryGraphics;
using T = RimArt.EgoMimicryTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// What Mimicry leaves on the world, the port of ego-mimicry.js's crescent, cut, slamImpact, the downed
    /// pawn's pool, the lunge's dust and the shot that tears a stage off. Every routine takes an age and keeps
    /// no state, so what lands is drawn from the same numbers every frame and stays.
    ///
    /// The crescent (Ruina's swing trail) is a level arc at hand height round the wielder: red flesh 0.42 cells
    /// thick at the blade, thinning to the tail, a white streak on its leading edge, four white eyes. A hit: a
    /// white slash across the chest (0.12 s), a red cut (0.6 s), six blood drops thrown along the swing that land
    /// and stay. The grown hit: the crack along the blade's footprint (opens in 0.06 s, stays), the split light
    /// (0.45 s), twelve red streaks (0.3 s), seven flesh lumps with eyes (out in 0.08 s, gone by 0.55 s), dust,
    /// ten rocks thrown that land and stay. Flat shapes and level circles only, so there is no per-facing method.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoMimicryStrikeGraphics
    {
        /// <summary>The seed every grown hit's scatter is drawn from.</summary>
        public const int SlamSeed = 900;
        private const int CrescentN = 22;
        private static readonly Vector2[] Inner = new Vector2[CrescentN + 1], Outer = new Vector2[CrescentN + 1], Lit = new Vector2[CrescentN + 1],
            LitIn = new Vector2[CrescentN + 1], Edge = new Vector2[CrescentN + 1], EdgeIn = new Vector2[CrescentN + 1];
        private static readonly float[] CrescentEyes = { 0.3f, 0.5f, 0.68f, 0.86f };

        private static Vector2 centre;
        private static float aim, sign;

        private static Vector2 Arc(float rel, float r) => centre + T.Dir(aim + sign * rel) * r;

        /// <summary>
        /// The flesh crescent behind a swing <paramref name="age"/> s in, centred on the wielder at hand height,
        /// its outer edge <paramref name="reach"/> cells out. <paramref name="mirror"/> (-1) mirrors it when aiming west.
        /// </summary>
        internal static void Crescent(Vector2 at, float aimDegrees, float mirror, float age, float reach)
        {
            if (!T.CrescentSpan(age, out float head, out float tail, out float fade)) return;
            centre = at;
            aim = aimDegrees;
            sign = mirror;
            for (int i = 0; i <= CrescentN; i++)
            {
                float u = i / (float)CrescentN, rel = Mathf.Lerp(tail, head, u), th = T.CrescentThick * Mathf.Pow(u, 0.8f) * (0.55f + 0.45f * fade);
                Outer[i] = Arc(rel, reach);
                Inner[i] = Arc(rel, reach - th);
                Lit[i] = Arc(rel, reach - th * 0.55f);
                LitIn[i] = Arc(rel, reach - th * 0.85f);
                float e = Mathf.Clamp01((u - 0.35f) / 0.25f);
                Edge[i] = Arc(rel, reach + 0.01f);
                EdgeIn[i] = Arc(rel, reach - 0.05f * e);
            }
            Band(Inner, Outer, Fade(Flesh, 0.75f * fade), solid, Overhead + 0.02f);
            Band(LitIn, Lit, Fade(Fibre, 0.45f * fade), solid, Overhead + 0.0202f);
            Band(EdgeIn, Edge, Fade(White, 0.9f * fade), whiteGlow, Overhead + 0.0204f);
            for (int i = 0; i < CrescentEyes.Length; i++)
            {
                float u = CrescentEyes[i], rel = Mathf.Lerp(tail, head, u), th = T.CrescentThick * Mathf.Pow(u, 0.8f);
                Vector2 c = Arc(rel, reach - th * 0.5f);
                float r = (0.028f + 0.018f * Rand(i + 40)) * Mathf.Min(1f, th / 0.25f) * fade;
                if (r < 0.01f) continue;
                Disc(c, Overhead + 0.0206f, r + 0.01f, r * 0.8f + 0.01f, 0f, Fade(Outline, fade));
                Disc(c, Overhead + 0.0207f, r, r * 0.8f, 0f, Fade(Sclera, fade));
                Disc(new Vector2(c.x + (Rand(i + 50) - 0.5f) * r * 0.6f, c.y), Overhead + 0.0208f, r * 0.35f, r * 0.35f, 0f, Fade(Pupil, fade));
            }
        }

        /// <summary>
        /// A normal hit on a pawn <paramref name="age"/> s ago: the slash across <paramref name="chest"/> along the
        /// swing's direction at contact <paramref name="along"/>, the red cut, and six blood drops thrown from the
        /// chest along the aim <paramref name="d"/> that land round <paramref name="foot"/> (the pawn's DrawPos) and stay.
        /// <paramref name="pawnAltitude"/> is the struck pawn's DrawPos.y in game; the previews leave it to the pawn layer.
        /// </summary>
        public static void Cut(Vector2 chest, Vector2 foot, Vector2 d, Vector2 along, float age, int seed, Map map, float pawnAltitude = float.NaN)
        {
            if (age < 0f || !Shown(foot, map)) return;
            Begin(foot);
            if (age < 0.12f)
                Streak(chest - along * 0.3f + d * 0.05f, chest + along * 0.3f - d * 0.05f, 0.09f * (1f - age / 0.12f) + 0.02f, White, whiteGlow, Overhead + 0.09f);
            // Over the struck pawn's own parts (up to 0.037 above its body), which sits at its own height in game.
            if (age < 0.6f) Streak(chest - along * 0.2f, chest + along * 0.2f, 0.04f, Fade(RedStreak, 1f - age / 0.6f), solid,
                (float.IsNaN(pawnAltitude) ? PawnLayer : pawnAltitude) + 0.05f);
            for (int i = 0; i < 6; i++)
            {
                float r = Rand(seed * 13 + i), up = 1f + Rand(seed * 19 + i);
                const float gr = 7f;
                Vector2 v = d * (0.5f + 0.6f * r) + along * ((Rand(seed * 17 + i) - 0.3f) * 0.9f);
                float land = (up + Mathf.Sqrt(up * up + 2f * gr * T.ChestH)) / gr, tt = Mathf.Min(age, land);
                Vector2 g = foot + v * tt;
                float h = Mathf.Max(0f, T.ChestH + up * tt - 0.5f * gr * tt * tt), sz = 0.035f + 0.03f * r;
                if (age < land) Disc(new Vector2(g.x, g.y + PawnBody.Ground + h * T.Lift), Overhead + 0.08f, sz, sz, 0f, Blood);
                else Disc(new Vector2(g.x, g.y + PawnBody.Ground), Floor + 0.02f + i * 0.0002f, sz * 1.6f, sz * 1.1f, 0f, Fade(Blood, 0.85f));
            }
        }

        /// <summary>The pool of blood that spreads under a downed pawn at <paramref name="at"/> over 1.2 s and stays.</summary>
        public static void Pool(Vector2 at, float age, Map map)
        {
            if (age < 0f || !Shown(at, map)) return;
            float k = Smooth(age / 1.2f);
            Sprite(new Vector2(at.x + 0.05f, at.y - 0.05f), 1f * k, 0.6f * k, Fade(Blood, 0.75f), soft, Floor + 0.015f);
        }

        /// <summary>Five puffs of dust round a lunge's start or end <paramref name="at"/>, for 0.5 s.</summary>
        public static void LungeDust(Vector2 at, float age, Map map)
        {
            if (age < 0f || age >= 0.5f || !Shown(at, map)) return;
            float u = age / 0.5f, r = 0.2f + 0.35f * u;
            for (int i = 0; i < 5; i++)
            {
                float ang = i / 5f * Mathf.PI * 2f + Rand(i + 330);
                var c = new Vector2(at.x + Mathf.Cos(ang) * r, at.y + PawnBody.Ground + 0.1f + Mathf.Sin(ang) * r * 0.4f + 0.1f * u);
                Sprite(c, 0.25f + 0.3f * u, 0.2f + 0.25f * u, Fade(Dust, 0.45f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), puff, Overhead + 0.008f);
            }
        }

        /// <summary>
        /// The shot that takes a stage back, <paramref name="age"/> s after it hits: a white flash on the arm, and a
        /// chunk of flesh torn off and thrown away from the shooter that lands, with blood under it, and stays.
        /// <paramref name="wielder"/> is the wielder's DrawPos, <paramref name="from"/> the shooter's, <paramref name="hs"/> the hand side.
        /// </summary>
        public static void Torn(Vector2 wielder, Vector2 from, Vector2 hs, float age, Map map)
        {
            if (age < 0f || !Shown(wielder, map)) return;
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 dd = T.Unit(wielder - from), hit = new Vector2(wielder.x + hs.x * 0.2f, wielder.y + hs.y * 0.2f + 0.12f);
            if (age < 0.1f) Sprite(hit, 0.45f, 0.4f, Fade(White, 0.8f * (1f - age / 0.1f)), glow, Overhead + 0.1f);
            const float up = 1.4f, gr = 7f, h0 = 0.9f;
            float land = (up + Mathf.Sqrt(up * up + 2f * gr * h0)) / gr, tt = Mathf.Min(age, land);
            Vector2 g = wielder + hs * 0.25f + dd * (1.1f * tt) + T.Side(dd) * (0.3f * tt);
            float h = Mathf.Max(0f, h0 + up * tt - 0.5f * gr * tt * tt);
            if (age < land) Sprite(Shadow(new Vector3(g.x, h, g.y), sun), 0.14f, 0.09f, Fade(Outline, strength * 0.6f), soft, ShadowLayer);
            var c = new Vector2(g.x, g.y + PawnBody.Ground + h * T.Lift);
            float L = age < land ? Overhead + 0.06f : Floor + 0.04f, spin = age * 400f;
            if (age >= land) Disc(c, Floor + 0.035f, 0.26f, 0.16f, 0f, Fade(Blood, 0.8f));
            Disc(c, L, 0.17f, 0.13f, spin, Outline);
            Disc(c, L + 0.0005f, 0.15f, 0.11f, spin, Meat);
            Disc(new Vector2(c.x - 0.03f, c.y + 0.025f), L + 0.001f, 0.07f, 0.04f, spin, Fade(MeatLit, 0.7f));
        }

        /// <summary>
        /// The grown hit <paramref name="age"/> s after the slam lands. <paramref name="p0"/> to <paramref name="p1"/>
        /// is the blade's footprint (EgoMimicryTiming.SlamFootprint, the floor at y + Ground); <paramref name="d"/> the aim.
        /// </summary>
        public static void Slam(Vector2 p0, Vector2 p1, Vector2 d, float age, int seed, Map map)
        {
            if (age < 0f || !Shown(p0, map)) return;
            Begin(p0);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 f0 = new Vector2(p0.x, p0.y + PawnBody.Ground), f1 = new Vector2(p1.x, p1.y + PawnBody.Ground), n = T.Side(d);
            float len = (f1 - f0).magnitude;
            // The crack: a jagged dark line along the footprint with branches; it opens in 0.06 s and stays.
            float open = Mathf.Clamp01(age / 0.06f);
            Vector2[] pts = GokuGraphics.Points(13);
            for (int i = 0; i <= 12; i++)
            {
                float u = i / 12f * open;
                pts[i] = Vector2.LerpUnclamped(f0, f1, u) + n * ((Rand(seed + i * 3) - 0.5f) * 0.09f * Mathf.Sin(u * Mathf.PI + 0.2f));
            }
            GokuGraphics.Line(pts, 0.07f, Fade(Outline, 0.75f), solid, Floor + 0.03f, Taper.Both);
            for (int i = 0; i < 6; i++)
            {
                float u = 0.15f + 0.7f * Rand(seed + 60 + i), sgn = i % 2 == 1 ? 1f : -1f, bl = (0.15f + 0.2f * Rand(seed + 70 + i)) * open;
                Vector2[] branch = GokuGraphics.Points(2);
                branch[0] = Vector2.LerpUnclamped(f0, f1, u * open);
                branch[1] = branch[0] + n * (sgn * bl) + d * (bl * 0.5f);
                GokuGraphics.Line(branch, 0.035f, Fade(Outline, 0.6f), solid, Floor + 0.031f, Taper.End);
            }
            Sprite(Vector2.LerpUnclamped(f0, f1, 0.5f), len + 0.6f, 0.5f, Fade(Outline, 0.25f * open), soft, Floor + 0.025f, -T.DegOf(d));
            // The split light (Ruina's card art): a yellow-white core in an orange glow along the footprint.
            if (age < 0.45f)
            {
                float u = age / 0.45f, k = Mathf.Pow(1f - u, 1.5f);
                Vector2 a = f0 - d * 0.2f, b = f1 + d * 0.3f;
                for (int layer = 0; layer < 2; layer++)
                {
                    Vector2[] split = GokuGraphics.Points(3);
                    split[0] = a;
                    split[1] = Vector2.LerpUnclamped(a, b, 0.5f);
                    split[2] = b;
                    if (layer == 0) GokuGraphics.Line(split, 0.55f * (1f + 0.4f * u), Fade(SplitGlow, 0.75f * k), whiteGlow, Overhead + 0.01f, Taper.Both);
                    else GokuGraphics.Line(split, 0.07f, Fade(SplitCore, k), whiteGlow, Overhead + 0.011f, Taper.Both);
                }
                if (age < 0.12f) Sprite(Vector2.LerpUnclamped(f0, f1, 0.3f), 2.2f, 1.6f, Fade(SplitCore, 0.6f * (1f - age / 0.12f)), glow, Overhead + 0.012f);
            }
            // Red streaks along the cut (the card art's diagonal strokes), sliding forward.
            if (age < 0.3f)
            {
                float k = 1f - age / 0.3f;
                for (int i = 0; i < 12; i++)
                {
                    float off = (i % 2 == 1 ? 1f : -1f) * (0.15f + 0.85f * Rand(seed + 100 + i)), sl = 0.5f + 0.7f * Rand(seed + 110 + i), st = Rand(seed + 120 + i) * len;
                    Vector2 a = f0 + d * (st + 0.6f * age * 4f) + n * off, b = a + d * sl;
                    Streak(a, b, 0.05f + 0.04f * Rand(seed + 130 + i), Fade(i % 3 != 0 ? RedStreak : DarkStreak, 0.9f * k), solid, Overhead + 0.013f + i * 0.00001f);
                }
            }
            // Flesh with eyes bursting out of the blade (Ruina): out in 0.08 s, gone by 0.55 s.
            float fl = age < 0.08f ? T.EaseOut(age / 0.08f) : 1f - Smooth((age - 0.25f) / 0.3f);
            if (fl > 0f)
                for (int i = 0; i < 7; i++)
                {
                    float u = 0.12f + i * 0.13f, L = Overhead + 0.03f + i * 0.001f;
                    Vector2 c = Vector2.LerpUnclamped(f0, f1, u) + n * ((Rand(seed + 140 + i) - 0.5f) * 0.4f) + new Vector2(0f, 0.15f + 0.25f * Rand(seed + 150 + i));
                    float r = (0.13f + 0.12f * Rand(seed + 160 + i)) * fl, er = r * 0.4f;
                    var iris = new Vector2(c.x + (Rand(seed + 170 + i) - 0.5f) * er * 0.7f, c.y);
                    Disc(c, L, r + 0.02f, r * 0.85f + 0.02f, 0f, Outline);
                    Disc(c, L + 0.0002f, r, r * 0.85f, 0f, Flesh);
                    Disc(new Vector2(c.x - r * 0.2f, c.y + r * 0.25f), L + 0.0004f, r * 0.5f, r * 0.3f, 0f, Fade(FleshLit, 0.6f));
                    Disc(c, L + 0.0006f, er, er * 0.85f, 0f, Sclera);
                    Disc(iris, L + 0.0008f, er * 0.45f, er * 0.45f, 0f, i % 3 != 0 ? IrisGreen : IrisBlue);
                    Disc(iris, L + 0.0009f, er * 0.2f, er * 0.2f, 0f, Pupil);
                }
            // Dust along the footprint, and rocks thrown out that land and stay.
            if (age < 0.8f)
                for (int i = 0; i < 10; i++)
                {
                    float u = age / 0.8f, sz = 0.3f + 0.6f * u;
                    Vector2 c = Vector2.LerpUnclamped(f0, f1, i / 9f) + n * ((Rand(seed + 200 + i) - 0.5f) * 0.6f * (1f + u));
                    Sprite(new Vector2(c.x, c.y + 0.25f * u), sz, sz * 0.8f, Fade(Dust, 0.5f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), puff, Overhead + 0.005f + i * 0.0003f);
                }
            for (int i = 0; i < 10; i++)
            {
                float r = Rand(seed + 220 + i), sgn = i % 2 == 1 ? 1f : -1f;
                Vector2 v = n * (sgn * (0.8f + 1.2f * r)) + d * ((Rand(seed + 230 + i) - 0.3f) * 0.8f);
                float up = 1.6f + 1.4f * Rand(seed + 240 + i), land = 2f * up / 9f, tt = Mathf.Min(age, land);
                Vector2 g = Vector2.LerpUnclamped(p0, p1, Rand(seed + 250 + i)) + v * tt;
                float h = Mathf.Max(0f, up * tt - 4.5f * tt * tt), sz = 0.07f + 0.07f * Rand(seed + 260 + i);
                if (age < land) Sprite(Shadow(new Vector3(g.x, h, g.y), sun), sz, sz * 0.7f, Fade(Outline, strength * 0.6f), soft, ShadowLayer);
                PaperBombGraphics.Rock(new Vector2(g.x, g.y + PawnBody.Ground + h * T.Lift), sz, i * 47f + tt * 600f, 1f, i, age < land ? Overhead + 0.04f : Floor + 0.035f);
            }
        }

        private static void Band(Vector2[] from, Vector2[] to, Color colour, Material material, float altitude)
        {
            Sides(from.Length, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < from.Length; i++) { a[i] = from[i]; b[i] = to[i]; }
            Strip(a, b, colour, material, altitude);
        }
    }
}
