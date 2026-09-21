using UnityEngine;
using Verse;
using static RimArt.GokuGraphics;
using static RimArt.ThunderGodGraphics;
using G = RimArt.GokuTiming;
using T = RimArt.GokuSpiritBombTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Spirit Bomb. The channel: the ball high over the caster (a flame edge, orbit lines,
    /// arcs, a spiral, crawling lightning, flashes, a halo, motes, a beating core), wisps rising from
    /// the ground into it, a ribbon of light from each lender's raised hand with the lender's glow, a
    /// ring and a swell as each one joins, and for a heavy bomb wind rings and pebbles round the
    /// caster; the blast ring on the floor at the true radius growing with the power. The throw and
    /// flight: rings across the path, falling sparks, a wavy tail, a bow arc, dust blown from under
    /// it. The grind: the ball flattening, sparks, cracks of light, lines running inward, chunks of
    /// ground lifting. The detonation: a white frame, a dome of level circles with arcs and lightning
    /// veins, a column and pillars of soft light, thrown rocks, dust rings; enemies inside break into
    /// flecks; what it spares glows blue; then sparkles, a scorch with burn streaks, cooling cracks.
    /// The ball is a sphere and the dome and column rings are level circles, so there is no
    /// per-facing method. Height is drawn 0.6 cells north per cell up.
    ///
    /// Not drawn (the sketch's stand-ins): the pawns, their shadows and arms, the tints on the
    /// caster, lenders and enemies, the enemies lying down afterwards, the animal, the colony wall,
    /// and the white outlines on the colonist and the animal the bomb spares. In their place a
    /// spared pawn gets the glow the sketch gives the spared wall (1.5 cells, sky blue at 0.35),
    /// centred on its body.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GokuSpiritBombGraphics
    {
        private static readonly Mesh rim = SixPathsBurstGraphics.Band(0.95f, "Spirit Bomb rim"), orbit = SixPathsBurstGraphics.Band(0.93f, "Spirit Bomb orbit");
        private static readonly Vector2[] lenders = new Vector2[6], foes = new Vector2[6], spared = new Vector2[2], walls = new Vector2[3];
        private static readonly int[] joined = new int[6];
        private const float TAU = Mathf.PI * 2f;

        /// <summary>
        /// The preview. <paramref name="centre"/> is halfway between the caster and the target, as in
        /// the lab's sketch. The lenders stand behind the caster; the scene at the target does not turn.
        /// </summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, int lenderCount, float seconds, Map map)
        {
            SpiritBombPlan plan = T.Plan(lenderCount);
            var o = new Vector2(centre.x, centre.z);
            Vector2 caster = o - toward * (T.ScriptDistance / 2f), target = o + toward * (T.ScriptDistance / 2f);
            for (int i = 0; i < lenderCount; i++) lenders[i] = G.Place(caster, toward, -T.LenderAt[i].x, T.LenderAt[i].y);
            for (int i = 0; i < T.FoeAt.Length; i++) foes[i] = target + T.FoeAt[i];
            float outside = plan.BlastAt(plan.Release) + T.OutsideBy;
            foes[T.FoeAt.Length] = target + new Vector2(Mathf.Cos(T.OutsideBearing), Mathf.Sin(T.OutsideBearing)) * outside;
            spared[0] = target + T.FriendAt;
            spared[1] = target + T.BeastAt;
            for (int k = 0; k < 3; k++) walls[k] = target + T.WallFrom + new Vector2(k, 0f);
            Draw(new SpiritBombShot
            {
                Caster = caster, Target = target, Seconds = seconds, Plan = plan, Lenders = lenders,
                Foes = foes, FoeCount = T.FoeAt.Length + 1, Spared = spared, SparedCount = 2, Walls = walls, WallCount = 3,
            }, map);
        }

        public static void Draw(in SpiritBombShot shot, Map map)
        {
            SpiritBombPlan t = shot.Plan;
            float s = shot.Seconds;
            Vector2 caster = shot.Caster, target = shot.Target;
            if (s < 0f || s >= t.End || !Shown(caster, map) && !Shown(target, map)) return;
            Begin(target);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            Vector2 toward = (target - caster).normalized;
            if (toward.sqrMagnitude < 0.5f) toward = Vector2.right;
            Vector2 Up(Vector2 ground, float h) => new Vector2(ground.x, ground.y + h * Lift);
            Vector2 Polar(float ang, float d) => target + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d;

            int cracks = t.Count(T.CrackCount.x, T.CrackCount.y), veins = t.Count(T.VeinCount.x, T.VeinCount.y), pillars = t.Count(T.PillarCount.x, T.PillarCount.y);
            int rocks = t.Count(T.RockCount.x, T.RockCount.y), burns = t.Count(T.StreakCount.x, T.StreakCount.y), pathRings = t.Count(T.PathRingCount.x, T.PathRingCount.y);
            float grindTime = t.Dome - t.Hit, hold = t.Fade - t.Open, whiteFrame = t.By(T.WhiteTime.x, T.WhiteTime.y);
            float power = t.PowerAt(s), chargeNow = Mathf.Clamp01(power / T.FullPower), r = T.StartSize + t.SizePer * power, blast = T.BaseRadius + t.BlastPer * power;
            bool channelling = s >= t.Cast && s < t.Release;
            float formed = Smooth((s - t.Cast) / 0.5f);
            // Where the ball is at w of the way through its flight: ground position and height.
            float hang = T.Hang + T.Climb * r;
            Vector2 PathGround(float w) => Vector2.Lerp(caster, target, w * w);
            float PathHeight(float w) => Mathf.Lerp(hang, r * 0.5f, w * w);
            Vector2 SeenAt(float w) => Up(PathGround(w), PathHeight(w));
            float flight = Mathf.Clamp01((s - t.Fly) / t.FlyTime), grind = Mathf.Clamp01((s - t.Hit) / grindTime), sink = Smooth(grind * 2.5f);
            float windup = Mathf.Sin(Mathf.PI * Mathf.Clamp01((s - t.Release) / T.Swing));
            Vector2 ground = PathGround(flight) - toward * (0.4f * windup);
            float height = (PathHeight(flight) + 0.5f * windup) * (1f - 0.7f * sink);
            Vector2 shake = grind > 0f && grind < 1f ? new Vector2(Mathf.Sin(s * 97f) * 0.05f * (0.4f + grind), Mathf.Sin(s * 131f) * 0.03f * (0.4f + grind)) : Vector2.zero;
            var ball = new Vector2(ground.x + shake.x, ground.y + height * Lift + shake.y);
            float domeAge = s - t.Dome, opened = Mathf.Clamp01(domeAge / T.Open), R = blast * (1f - Mathf.Pow(1f - opened, 3f));
            float fading = Smooth((s - t.Fade) / T.Fade), domeAlpha = domeAge < 0f ? 0f : 1f - fading;
            // Surge: the swell each time a lender joins.
            float surge = 0f;
            for (int i = 0; i < t.Lenders; i++)
            {
                float age = s - t.Joins(i);
                if (age >= 0f && age < T.SurgeTime && s < t.Release) surge = Mathf.Max(surge, Mathf.Sin(Mathf.PI * age / T.SurgeTime));
            }
            // When the front of the dome passes a point d cells from the centre.
            float Passes(float d) => t.Dome + T.Open * (1f - Mathf.Pow(Mathf.Max(0f, 1f - d / blast), 1f / 3f));

            // --- the floor ---
            if (s >= t.Cast && s < t.Dome)
            {
                float pulse = 0.45f + 0.2f * Mathf.Sin(s * 5f), strobe = grind > 0f ? 0.5f + 0.5f * Mathf.Sin(s * (30f + 40f * grind)) : 0f;
                PaperBombGraphics.RingAt(target, blast, Fade(KiSky, Mathf.Min(1f, pulse + 0.4f * grind) * formed), Floor + 0.02f);   // what a throw would cover right now
                DrawMesh(disc, target, Floor + 0.006f, blast, blast, 0f, Fade(KiDeep, (0.1f + 0.3f * grind * strobe) * formed), whiteGlow);
                float low = 1f - Mathf.Clamp01(height / (hang + 0.01f));
                Sprite(ground, (r * 3f + 2f) * (1f + low), (r * 3f + 2f) * (0.8f + low), Fade(Ki, (0.32f + 0.4f * low) * formed), glow, Floor + 0.008f);   // light under the ball
            }
            // The scorch: a burnt edge, radial burn streaks, a dark middle. It stays.
            if (domeAge >= 0f)
            {
                float burnt = Smooth(domeAge / 0.8f) * (1f - 0.35f * Smooth((s - t.Gone) / T.Tail));
                Sprite(target, blast * 2.2f, blast * 2.2f, Fade(Ink, 0.34f * burnt), soft, Floor + 0.01f);
                PaperBombGraphics.RingAt(target, blast * 0.98f, Fade(Ink, 0.5f * burnt), Floor + 0.011f, true);
                for (int i = 0; i < burns; i++)
                {
                    float ang = i * TAU / burns + Rand(i + 90) * 0.2f, from = blast * (0.25f + 0.2f * Rand(i + 91)), to = blast * (0.8f + 0.18f * Rand(i + 92));
                    Streak(Polar(ang, from), Polar(ang, to), 0.22f + 0.2f * Rand(i + 93), Fade(Ink, 0.4f * burnt), solid, Floor + 0.012f, 5);
                }
            }
            // Jagged cracks of light. They grow through the grind, blaze in the blast, and cool afterwards.
            if (s >= t.Hit)
            {
                float grown = domeAge >= 0f ? 1f : Smooth(grind) * 0.75f, heat = domeAge < 0f ? 1f : 1f - 0.85f * Smooth((s - t.Open) / (hold + T.Fade + T.Tail * 0.8f));
                Color hot = Color.Lerp(KiDeep, White, heat);
                for (int i = 0; i < cracks; i++)
                {
                    float ang = i * TAU / cracks + Rand(i + 3) * 0.35f, reach = blast * (0.5f + 0.45f * Rand(i + 8));
                    Vector2 way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)), normal = new Vector2(-way.y, way.x);
                    Vector2 At(int k) => target + way * (reach * k / 8f) + normal * (k > 0 ? (Rand(i * 13 + k) - 0.5f) * 0.7f : 0f);
                    int shownPoints = 0;
                    for (int k = 0; k <= 8; k++) if (k / 8f <= grown) shownPoints++;
                    Vector2[] pts = Points(shownPoints);
                    for (int k = 0; k < shownPoints; k++) pts[k] = At(k);
                    Line(pts, 0.5f, Fade(Ki, 0.6f * heat), whiteGlow, Floor + 0.03f);
                    Line(pts, 0.14f, Fade(hot, 0.35f + 0.6f * heat), whiteGlow, Floor + 0.031f);
                    if (i % 5 < 2 && grown > 0.6f)   // a fork from the middle
                    {
                        Vector2 root = At(4);
                        float turn = ang + (i % 2 == 1 ? 0.7f : -0.7f);
                        Vector2[] fork = Points(5);
                        fork[0] = root;
                        for (int k = 1; k <= 4; k++)
                            fork[k] = new Vector2(root.x + Mathf.Cos(turn) * reach * 0.1f * k + (Rand(i * 7 + k + 50) - 0.5f) * 0.4f, root.y + Mathf.Sin(turn) * reach * 0.1f * k + (Rand(i * 7 + k + 60) - 0.5f) * 0.4f);
                        Line(fork, 0.1f, Fade(hot, 0.3f + 0.6f * heat), whiteGlow, Floor + 0.031f);
                    }
                }
            }
            // The grind: lines of light run inward along the floor, sparks spray out from under the ball.
            if (grind > 0f && grind < 1f)
            {
                for (int i = 0; i < 14; i++)
                {
                    float u = ((s - t.Hit) * 2.6f + Rand(i + 20)) % 1f, ang = i * TAU / 14f + Rand(i + 25) * 0.3f, d0 = blast * (1f - u * 0.85f), d1 = Mathf.Min(blast, d0 + blast * 0.18f);
                    Streak(Polar(ang, d0), Polar(ang, d1), 0.09f, Fade(KiIce, 0.8f * Mathf.Sin(u * Mathf.PI) * grind), whiteGlow, Floor + 0.032f, 3);
                }
                for (int i = 0; i < T.GrindSparks; i++)
                {
                    float u = ((s - t.Hit) * 5f + Rand(i + 12)) % 1f, ang = i * TAU / T.GrindSparks + s * 3f + Rand(i) * 0.4f, d0 = r * 0.7f + u * (1.5f + 2f * Rand(i + 30)), d1 = d0 + 0.5f + Rand(i + 31) * 0.8f;
                    Streak(Polar(ang, d0), Up(Polar(ang + 0.12f, d1), u * 0.5f), 0.09f, Fade(White, 1f - u), whiteGlow, Overhead + 0.13f, 3);
                }
            }

            // --- structures inside the blast: they take nothing, and glow while the dome stands ---
            for (int k = 0; k < shot.WallCount; k++) Spared(shot.Walls[k], domeAlpha * opened);

            // --- the pawns: the lenders' glow, the enemies' flecks, the glow on what it spares ---
            int joinedCount = 0;
            for (int i = 0; i < t.Lenders; i++)
            {
                Vector2 at = shot.Lenders[i];
                float lending = Lending(t, i, s);
                if (s >= t.Joins(i) && s < t.Release + 0.3f) joined[joinedCount++] = i;
                Sprite(new Vector2(at.x, at.y + 0.35f), 1.3f, 1.6f, Fade(KiSky, 0.4f * lending * (0.8f + 0.2f * Mathf.Sin(s * 11f))), glow, PawnLayer - 0.01f);
            }
            for (int g = 0; g < shot.FoeCount; g++)
            {
                Vector2 pos = shot.Foes[g];
                float d = Vector2.Distance(pos, target), since = s - Passes(d);
                if (!(d <= blast) || domeAge < 0f || since < 0f) continue;
                // It breaks into flecks that rise.
                for (int k = 0; k < T.Flecks; k++)
                {
                    float u = (since - 0.1f - k * 0.03f) / (0.7f + 0.4f * Rand(g * 11 + k));
                    if (u < 0f || u > 1f) continue;
                    var at = new Vector2(pos.x + (Rand(g * 5 + k) - 0.5f) * 0.5f + Mathf.Sin(u * 4f + k) * 0.12f, pos.y + Rand(g * 3 + k + 40) * 0.7f + u * 1.6f);
                    Sprite(at, 0.09f * (1f - u * 0.5f), 0.09f * (1f - u * 0.5f), Fade(White, 0.95f * (1f - u)), solid, Overhead + 0.17f, u * 200f + k * 40f);
                }
            }
            for (int g = 0; g < shot.SparedCount; g++)
            {
                Vector2 pos = shot.Spared[g];
                float d = Vector2.Distance(pos, target);
                bool inside = domeAge >= 0f && d <= blast && s >= Passes(d);
                if (inside) Spared(new Vector2(pos.x, pos.y + 0.3f), domeAlpha);
            }

            // --- a heavy bomb works on the ground under the caster: wind rings run out, pebbles lift and hang ---
            if (channelling && chargeNow > 0.3f)
            {
                float heavy = Mathf.Clamp01((chargeNow - 0.3f) / 0.4f);
                for (int n = 0; n < 3; n++)
                {
                    float u = ((s - t.Cast) / 0.7f + n / 3f) % 1f;
                    PaperBombGraphics.RingAt(caster, 0.4f + u * (2f + 3f * heavy), Fade(KiIce, 0.5f * (1f - u) * heavy), Floor + 0.02f);
                }
                int pebbles = G.Round(T.MaxPebbles * heavy);
                for (int i = 0; i < pebbles; i++)
                {
                    float u = ((s - t.Cast) * (0.3f + 0.2f * Rand(i + 2)) + Rand(i + 1)) % 1f, ang = i * 2.399f, d = 0.7f + Rand(i + 7) * 2.6f, size = 0.06f + 0.07f * Rand(i + 9);
                    float h = u * (0.8f + 1.4f * Rand(i + 4)), show = Mathf.Sin(u * Mathf.PI);
                    var foot = new Vector2(caster.x + Mathf.Cos(ang) * d, caster.y + Mathf.Sin(ang) * d * 0.8f);
                    Sprite(foot + sun * h, size * 2.4f, size * 1.4f, Fade(Ink, 0.35f * show), soft, Floor + 0.05f);
                    PaperBombGraphics.Rock(new Vector2(foot.x, foot.y + h * Lift), size * 1.8f, i * 50f + s * 60f, show, i, Overhead + 0.005f);
                }
            }

            // --- energy rising into the ball: wisps from the ground, ribbons from the lenders ---
            if (channelling)
            {
                int count = T.Wisps + T.WispsPerLender * joinedCount;
                Vector2 centre = Up(caster, hang);
                for (int i = 0; i < count; i++)
                {
                    float v = ((s - t.Cast) * (0.45f + 0.2f * Rand(i + 50)) + Rand(i)) % 1f, ang = Rand(i + 17) * TAU, far = 1.5f + Rand(i + 31) * (T.WispReach - 1.5f);
                    var root = new Vector2(caster.x + Mathf.Cos(ang) * far, caster.y + Mathf.Sin(ang) * far * 0.8f);
                    float curl = (Rand(i + 5) - 0.5f) * 2.5f;
                    Vector2 Point(float w)
                    {
                        Vector2 q = Up(Vector2.Lerp(root, caster, w * w), hang * Mathf.Pow(w, 1.4f));
                        return new Vector2(q.x + Mathf.Sin(w * Mathf.PI) * curl * 0.4f, q.y);
                    }
                    Vector2 head = Point(v), tail = Point(Mathf.Max(0f, v - 0.09f));
                    float show = Mathf.Sin(v * Mathf.PI) * formed;
                    Streak(tail, head, 0.07f, Fade(KiIce, 0.85f * show), whiteGlow, Overhead + 0.12f, 3);
                    Sprite(head, 0.22f, 0.22f, Fade(White, 0.8f * show), glow, Overhead + 0.121f);
                    if (v < 0.15f) Sprite(root, 0.6f, 0.4f, Fade(KiSky, 0.5f * (1f - v / 0.15f) * formed), glow, Floor + 0.03f);   // where it left the ground
                }
                for (int j = 0; j < joinedCount; j++)
                {
                    int i = joined[j];
                    Vector2 at = shot.Lenders[i], hand = new Vector2(at.x + 0.22f, at.y + 0.92f);
                    float lending = Lending(t, i, s), bend = i % 2 == 1 ? 0.6f : -0.6f;
                    Vector2 Point(float w) => new Vector2(Mathf.Lerp(hand.x, centre.x, w) + Mathf.Sin(w * Mathf.PI) * bend, Mathf.Lerp(hand.y, centre.y, Smooth(w) * 0.6f + w * 0.4f));
                    Vector2[] pts = Points(17);
                    for (int k = 0; k <= 16; k++) pts[k] = Point(k / 16f);
                    Line(pts, 0.16f, Fade(KiSky, 0.6f * lending), whiteGlow, Overhead + 0.11f, Taper.None);
                    for (int k = 0; k < 3; k++) Sprite(Point(((s - t.Cast) * 1.1f + k / 3f + i * 0.17f) % 1f), 0.3f, 0.3f, Fade(White, 0.9f * lending), glow, Overhead + 0.111f);
                    Sprite(hand, 0.4f, 0.4f, Fade(KiIce, 0.8f * lending), glow, Overhead + 0.112f);
                }
                // Each lender that joins makes the ball throw a ring.
                for (int i = 0; i < t.Lenders; i++)
                {
                    float age = s - t.Joins(i);
                    if (age < 0f || age >= T.SurgeTime) continue;
                    float u = age / T.SurgeTime;
                    PaperBombGraphics.RingAt(centre, r * (1f + 1.6f * Smooth(u)), Fade(White, 0.9f * (1f - u)), Overhead + 0.138f, true, whiteGlow);
                }
            }

            // --- the ball in flight: rings left across its path, tail, falling sparks, bow arc, dust under it ---
            bool flying = flight > 0f && grind <= 0f;
            Vector2 ahead = SeenAt(Mathf.Min(1f, flight + 0.01f)), before = SeenAt(Mathf.Max(0f, flight - 0.01f));
            float vl = Vector2.Distance(ahead, before);
            if (vl == 0f) vl = 1f;
            Vector2 v2 = flight > 0f ? (ahead - before) / vl : new Vector2(0f, -1f);
            if (flight > 0f && s < t.Dome)
            {
                for (int n = 0; n < pathRings; n++)
                {
                    float w = (n + 0.7f) / (pathRings + 0.7f), age = s - (t.Fly + t.FlyTime * w);
                    if (age < 0f || age >= 0.55f) continue;
                    float u = age / 0.55f;
                    Vector2 c = SeenAt(w), e0 = SeenAt(w - 0.01f), e1 = SeenAt(w + 0.01f);
                    float deg = Mathf.Atan2(e1.y - e0.y, e1.x - e0.x) * Mathf.Rad2Deg;
                    DrawMesh(orbit, c, Overhead + 0.132f, r * (0.18f + 0.25f * u), r * (1.1f + 0.9f * u), -deg, Fade(KiIce, 0.8f * (1f - u)), solid);
                    DrawMesh(orbit, c, Overhead + 0.131f, r * (0.3f + 0.3f * u), r * (1.25f + 1f * u), -deg, Fade(Ki, 0.5f * (1f - u)), whiteGlow);
                }
                for (int i = 0; i < T.FallSparks; i++)
                {
                    float w = (i + 0.5f) / T.FallSparks, age = s - (t.Fly + t.FlyTime * w), life = 0.5f + 0.4f * Rand(i + 70);
                    if (age < 0f || age >= life) continue;
                    float u = age / life;
                    Vector2 c = SeenAt(w);
                    Glint(new Vector2(c.x + (Rand(i + 71) - 0.5f) * r * 1.6f, c.y + (Rand(i + 72) - 0.5f) * r * 1.6f - u * 0.8f), 0.1f + 0.08f * Rand(i), 1f - u, KiIce, 45f);
                }
            }
            if (flying)
            {
                float speed = Mathf.Clamp01(flight * 3f);
                Vector2[] pts = Points(17);
                for (int j = 0; j <= 16; j++)
                {
                    float w = Mathf.Max(0f, flight - T.TailSpan * flight * j / 16f), wave = Mathf.Sin(j * 1.1f - s * 28f) * r * 0.12f * j / 16f;
                    Vector2 c = SeenAt(w);
                    pts[j] = new Vector2(c.x - v2.y * wave, c.y + v2.x * wave);
                }
                Line(pts, r * 2.3f, Fade(Ki, 0.5f * speed), whiteGlow, Overhead + 0.133f);
                Line(pts, r * 1.2f, Fade(KiIce, 0.55f * speed), whiteGlow, Overhead + 0.134f);
                Vector2[] bow = Points(13);
                float face = Mathf.Atan2(v2.y, v2.x);
                for (int j = 0; j <= 12; j++)
                {
                    float ang = face + (j / 12f - 0.5f) * 1.9f;
                    bow[j] = ball + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r * 1.35f);
                }
                Line(bow, r * 0.16f + 0.04f, Fade(White, 0.75f * speed), whiteGlow, Overhead + 0.153f, Taper.Both);
                // Dust blown out from under it once it is low.
                if (flight > 0.55f)
                    for (int i = 0; i < 14; i++)
                    {
                        float u = ((s - t.Fly) * 2.2f + Rand(i + 33)) % 1f, side = i % 2 == 1 ? 1f : -1f, d = r * 0.5f + u * (1.5f + Rand(i + 34) * 1.5f), back = Rand(i + 35) * 1.5f;
                        Vector2 at = G.Place(ground, toward, -back, side * d);
                        Sprite(new Vector2(at.x, at.y + u * 0.3f), 0.7f + u, 0.5f + u * 0.8f, Fade(Dust, 0.45f * Mathf.Sin(u * Mathf.PI) * Mathf.Clamp01((flight - 0.55f) / 0.2f)), soft, Overhead + 0.005f);
                    }
            }
            if (s >= t.Cast && s < t.Dome + 0.12f)
            {
                float swell = 1f + T.SurgeSwell * surge + 0.18f * grind + 0.05f * Mathf.Sin(s * 60f) * grind, alpha = 1f - Mathf.Clamp01(domeAge / 0.12f);
                if (grind > 0f) Bomb(ball, r * formed * swell, s, alpha, new Vector2(0f, -1f), -0.3f * sink, T.FlightSpin + 3f * grind, 0f);
                else Bomb(ball, r * formed * swell, s, alpha, v2, T.Stretch * Mathf.Clamp01(flight * 3f), 1f + (T.FlightSpin - 1f) * Mathf.Clamp01(flight * 3f) + 1.5f * surge, surge);
            }

            // --- chunks of ground that lift in the grind, and the rocks the blast throws ---
            if (grind > 0f && domeAge < 0f)
                for (int i = 0; i < T.Chunks; i++)
                {
                    float ang = i * 2.399f, d = r * 0.9f + Rand(i + 44) * blast * 0.45f, h = Smooth(grind * 1.4f - Rand(i + 45) * 0.3f) * (0.5f + 0.7f * Rand(i + 46)), size = 0.16f + 0.16f * Rand(i + 47);
                    Vector2 foot = Polar(ang, d);
                    Sprite(foot + sun * h, size * 2.2f, size * 1.2f, Fade(Ink, 0.35f), soft, Floor + 0.05f);
                    PaperBombGraphics.Rock(new Vector2(foot.x + Mathf.Sin(s * 70f + i) * 0.02f, foot.y + h * Lift), size * 1.7f, i * 50f + s * 40f, 1f, i, Overhead + 0.02f);
                }
            // Thrown rocks: a few big, many small. Each leaves dust behind it in the air, lands with a puff and stays as rubble.
            if (domeAge >= 0f)
            {
                float rockHeight = t.By(T.RockHeight.x, T.RockHeight.y);
                for (int i = 0; i < rocks; i++)
                {
                    float air = 0.9f + 0.9f * Rand(i + 51), u = domeAge / air, ang = Rand(i + 52) * TAU, big = Rand(i + 55), size = 0.24f + 0.75f * big * big;
                    float from = blast * (0.15f + 0.5f * Rand(i + 53)), reach = blast * 0.75f, peak = (3f + 5f * Rand(i + 54)) * rockHeight * (1.15f - 0.5f * big);
                    Vector2 Foot(float w) => Polar(ang, from + reach * Mathf.Min(1f, w));
                    float H(float w) => w < 1f ? peak * 4f * w * (1f - w) : 0f;
                    Vector2 foot = Foot(u);
                    float h = H(u);
                    bool landed = u >= 1f;
                    float turn = i * 47f + Mathf.Min(u, 1f) * air * (300f + 300f * Rand(i + 56));
                    Sprite(foot + sun * h, size * 2.2f, size * 1.1f, Fade(Ink, 0.32f), soft, Floor + 0.05f);
                    if (!landed)
                        for (int k = 1; k <= 3; k++)
                        {
                            float w = u - k * 0.05f;
                            if (w <= 0f) continue;
                            Sprite(Up(Foot(w), H(w)), size * (1.2f + k * 0.5f), size * (1f + k * 0.4f), Fade(Dust, 0.3f * (1f - k / 4f)), soft, Overhead + 0.174f);
                        }
                    PaperBombGraphics.Rock(Up(foot, h), size, turn, 1f, i, landed ? Floor + 0.06f : Overhead + 0.175f);
                    float since = domeAge - air;
                    if (since >= 0f && since < 0.5f)
                    {
                        float v = since / 0.5f;
                        Sprite(new Vector2(foot.x, foot.y + v * 0.25f), size * (2f + 3f * v), size * (1.5f + 2.2f * v), Fade(Dust, 0.5f * Mathf.Sin(v * Mathf.PI)), soft, Overhead + 0.006f);
                    }
                }
            }

            // --- the detonation ---
            if (domeAge >= 0f && domeAlpha > 0f)
            {
                float first = Mathf.Clamp01(1f - domeAge / 0.4f), rise = 1f + 0.6f * fading, thin = 1f - 0.25f * fading;
                if (domeAge < whiteFrame * 2f)   // soft-edged, so no hard white circle
                    Sprite(target, blast * 3.4f, blast * 3.4f, Fade(White, domeAge < whiteFrame ? 1f : 1f - (domeAge - whiteFrame) / whiteFrame), glow, Overhead + 0.19f);
                Sprite(target, blast * 5f, blast * 5f, Fade(KiIce, (0.3f + 0.4f * t.Charge) * first * first), glow, Overhead + 0.15f);             // whiteout
                // The dome: level circles from the floor to the top, brighter toward the top. It lifts and thins as it fades.
                for (int k = 0; k < T.Levels; k++)
                {
                    float tilt = k / (float)T.Levels * 84f * Mathf.Deg2Rad, rad = R * Mathf.Cos(tilt) * thin;
                    Vector2 c = Up(target, R * 0.6f * Mathf.Sin(tilt) * rise);
                    DrawMesh(disc, c, Overhead + 0.151f + k * 0.001f, rad, rad, 0f, Fade(Color.Lerp(KiDeep, KiSky, k / (float)(T.Levels - 1)), 0.24f * domeAlpha), whiteGlow);
                    DrawMesh(rim, c, Overhead + 0.16f + k * 0.0005f, rad, rad, 0f, Fade(KiSky, 0.3f * domeAlpha * (1f - k / (float)T.Levels)), whiteGlow);
                    // An arc turning on each level: the surface is moving.
                    Vector2[] arc = Points(15);
                    float start = s * (k % 2 == 1 ? 90f : -70f) + k * 70f;
                    for (int j = 0; j <= 14; j++) arc[j] = c + Turn(start + 110f * j / 14f) * (rad * 0.97f);
                    Line(arc, 0.12f + R * 0.03f, Fade(White, 0.65f * domeAlpha), whiteGlow, Overhead + 0.164f, Taper.Both);
                }
                // Lightning veins from the top of the dome down to its rim, rolled again every 0.1 s.
                for (int i = 0; i < veins; i++)
                {
                    int seed = Mathf.FloorToInt(s / 0.1f) * 19 + i * 5;
                    float az = i * TAU / veins + Rand(seed) * 0.6f + s * 0.4f;
                    Vector2[] pts = Points(10);
                    for (int j = 0; j <= 9; j++)
                    {
                        float tilt = 84f * (1f - j / 9f) * Mathf.Deg2Rad, wob = az + (Rand(seed + j + 1) - 0.5f) * 0.5f, rad = R * Mathf.Cos(tilt) * thin;
                        pts[j] = Up(target + new Vector2(Mathf.Cos(wob), Mathf.Sin(wob)) * rad, R * 0.6f * Mathf.Sin(tilt) * rise);
                    }
                    Line(pts, 0.1f + R * 0.015f, Fade(White, 0.9f * domeAlpha), whiteGlow, Overhead + 0.166f, Taper.Both);
                }
                PaperBombGraphics.RingAt(target, R, Fade(KiIce, 0.9f * domeAlpha), Overhead + 0.168f, true, whiteGlow);                        // the front, on the true radius
                // The column of light. It is light, not an object: no mesh with an edge, only soft sprites that overlap up its
                // height, each one rippling in width and dimmer than the one below, so it has no outline and no tip. Streaks
                // run up through it, a pool of light sits at its foot, and rings climb it.
                float tall = (2.5f + 1.9f * blast + T.ColumnExtra * t.Charge) * Smooth(domeAge / 0.25f), girth = 1f - 0.85f * fading, piece = tall / 16f;
                float WidthAt(float u) => blast * (0.24f - 0.13f * Mathf.Pow(u, 0.7f)) * girth * (1f + 0.16f * Mathf.Sin(u * 11f - s * 15f) + 0.08f * Mathf.Sin(u * 23f - s * 27f));
                Sprite(target, blast * 1.2f * girth, blast * 0.7f * girth, Fade(KiIce, 0.7f * domeAlpha), glow, Overhead + 0.1695f);
                for (int j = 0; j < 16; j++)
                {
                    // Full to 60% of the height, then it thins out to nothing.
                    float u = (j + 0.5f) / 16f, w = WidthAt(u), dim = Mathf.Clamp01((1f - u) / 0.4f) * (1f - 0.3f * u) * domeAlpha * (0.85f + 0.15f * Mathf.Sin(s * 33f + j * 1.7f));
                    Vector2 c = Up(target, tall * u);
                    Sprite(c, w * 3.2f, piece * Lift * 3.2f, Fade(Ki, 0.6f * dim), glow, Overhead + 0.17f);
                    Sprite(c, w * 1.7f, piece * Lift * 2.8f, Fade(KiIce, 0.75f * dim), glow, Overhead + 0.1705f);
                    Sprite(c, w * 0.8f, piece * Lift * 2.6f, Fade(White, 0.85f * dim), glow, Overhead + 0.171f);
                }
                for (int i = 0; i < 18; i++)
                {
                    float u = (domeAge * (1.4f + Rand(i + 61)) + Rand(i + 62)) % 1f, x = (Rand(i + 63) - 0.5f) * WidthAt(u) * 1.8f, longer = tall * (0.1f + 0.12f * Rand(i + 64));
                    Vector2 from = Up(target, tall * u), to = Up(target, Mathf.Min(tall, tall * u + longer));
                    Streak(new Vector2(from.x + x, from.y), new Vector2(to.x + x, to.y), 0.09f, Fade(White, 0.8f * Mathf.Sin(u * Mathf.PI) * (1f - u) * domeAlpha), whiteGlow, Overhead + 0.1715f, 4);
                }
                for (int n = 0; n < 5; n++)
                {
                    float u = (domeAge * 0.9f + n / 5f) % 1f;
                    PaperBombGraphics.RingAt(Up(target, tall * u), WidthAt(u) * 1.9f + 0.1f, Fade(KiIce, 0.7f * Mathf.Sin(u * Mathf.PI) * (1f - u) * domeAlpha), Overhead + 0.172f, false, whiteGlow);
                }
                // The thin pillars, the same way: a soft shaft that flickers, brightest at the floor, with a bead running up it.
                for (int i = 0; i < pillars; i++)
                {
                    float ang = i * 2.399f, d = blast * (0.3f + 0.6f * Rand(i + 60));
                    if (R < d) continue;
                    Vector2 foot = Polar(ang, d);
                    float h = (3f + 4f * Rand(i + 70)) * Smooth((s - Passes(d)) / 0.3f), flick = (0.7f + 0.3f * Mathf.Sin(s * 30f + i * 2f)) * domeAlpha;
                    for (int j = 0; j < 5; j++)
                    {
                        float u = (j + 0.5f) / 5f;
                        Vector2 c = Up(foot, h * u);
                        Sprite(c, 0.9f - 0.4f * u, h * Lift * 0.5f, Fade(Ki, 0.4f * (1f - u) * flick), glow, Overhead + 0.166f);
                        Sprite(c, 0.3f - 0.12f * u, h * Lift * 0.45f, Fade(White, 0.75f * (1f - u) * flick), glow, Overhead + 0.167f);
                    }
                    Sprite(foot, 1.3f, 0.8f, Fade(KiIce, 0.6f * flick), glow, Overhead + 0.1665f);
                    float bead = (s * 1.6f + Rand(i + 75)) % 1f;
                    Sprite(Up(foot, h * bead), 0.3f, 0.5f, Fade(White, 0.9f * (1f - bead) * flick), glow, Overhead + 0.168f);
                }
            }
            // Two dust rings run out past the radius. They are dust, not light: the light stops on the true radius.
            if (domeAge >= 0f)
                for (int n = 0; n < (t.Charge > 0.4f ? 2 : 1); n++)
                {
                    float u = (domeAge - n * 0.18f) / 1.1f;
                    if (u < 0f || u > 1f) continue;
                    float d = blast * (0.9f + 0.9f * Smooth(u));
                    for (int i = 0; i < 26; i++)
                    {
                        float ang = i * TAU / 26f + Rand(i + n * 30) * 0.2f;
                        Sprite(new Vector2(target.x + Mathf.Cos(ang) * d, target.y + Mathf.Sin(ang) * d + u * 0.5f), 1.1f + u * 1.6f, 0.85f + u * 1.2f, Fade(Dust, 0.5f * Mathf.Sin(u * Mathf.PI)), soft, Overhead + 0.005f);
                    }
                }

            // --- after: sparkles rise from the whole blast ---
            if (s >= t.Fade)
                for (int i = 0; i < T.Sparkles; i++)
                {
                    float born = t.Fade + Rand(i + 3) * (T.Fade + 0.6f), life = 0.9f + Rand(i + 12) * 0.9f, u = (s - born) / life;
                    if (u < 0f || u > 1f) continue;
                    Vector2 at = Up(Polar(Rand(i + 40) * TAU, blast * Mathf.Sqrt(Rand(i + 80))), u * (1.5f + 2.5f * Rand(i + 9)));
                    Glint(at, 0.09f + 0.06f * Rand(i), 0.9f * Mathf.Sin(u * Mathf.PI), KiIce, 45f);
                }
        }

        /// <summary>How much lender <paramref name="i"/> gives now: up over 0.3 s from joining, down over 0.3 s from the throw.</summary>
        private static float Lending(in SpiritBombPlan t, int i, float s) => Smooth((s - t.Joins(i)) / 0.3f) * (1f - Smooth((s - t.Release) / 0.3f));

        /// <summary>The glow on something the bomb spares: sky blue, 1.5 cells, the sketch's glow on the spared wall.</summary>
        private static void Spared(Vector2 at, float amount) => Sprite(at, 1.5f, 1.5f, Fade(KiSky, 0.35f * amount), glow, Overhead + 0.01f);

        /// <summary>
        /// The ball. <paramref name="r"/> is its radius in cells. <paramref name="v"/> is the unit
        /// direction it is stretched along and <paramref name="stretch"/> how much (negative flattens
        /// it), <paramref name="spin"/> multiplies every turning speed, <paramref name="surge"/> 0 to 1
        /// is the swell of a lender joining.
        /// </summary>
        private static void Bomb(Vector2 at, float r, float s, float alpha, Vector2 v, float stretch, float spin, float surge)
        {
            if (r <= 0.01f || alpha <= 0f) return;
            float beat = 1f + (0.06f + 0.1f * surge) * Mathf.Sin(s * 9f), breathe = 1f + 0.05f * Mathf.Sin(s * 3.1f), turn = -Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            float sx = 1f + stretch, sz = 1f - 0.35f * stretch, time = s * spin;
            // A point of the round ball, stretched along v.
            Vector2 On(float dx, float dz)
            {
                float al = (dx * v.x + dz * v.y) * sx, ac = (-dx * v.y + dz * v.x) * sz;
                return new Vector2(at.x + al * v.x - ac * v.y, at.y + al * v.y + ac * v.x);
            }
            Vector2 Round(float ang, float rad) => On(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);

            Sprite(at, r * 4.6f * breathe, r * 4.6f * breathe, Fade(Ki, (0.5f + 0.3f * surge) * alpha), glow, Overhead + 0.14f);
            // The flame edge: an outline that licks in and out round the rim.
            const int steps = 56;
            Sides(steps + 1, out Vector2[] inner, out Vector2[] outer);
            for (int i = 0; i <= steps; i++)
            {
                float ang = i / (float)steps * TAU;
                float lick = 1.07f + 0.06f * Mathf.Sin(ang * 7f + time * 6f) + 0.045f * Mathf.Sin(ang * 13f - time * 9.5f) + 0.03f * Mathf.Sin(ang * 3f + time * 2.2f) + 0.08f * surge;
                inner[i] = Round(ang, r * 0.9f);
                outer[i] = Round(ang, r * lick);
            }
            Strip(inner, outer, Fade(Ki, 0.75f * alpha), whiteGlow, Overhead + 0.1405f);
            DrawMesh(disc, at, Overhead + 0.141f, r * sx, r * sz, turn, Fade(KiSky, 0.92f * alpha), solid);
            DrawMesh(disc, at, Overhead + 0.142f, r * 0.86f * sx, r * 0.86f * sz, turn, Fade(KiIce, 0.9f * alpha), solid);
            DrawMesh(rim, at, Overhead + 0.143f, r * sx, r * sz, turn, Fade(Ki, alpha), solid);
            // Orbit lines that tilt and turn: the ball reads as a sphere that is turning.
            for (int k = 0; k < T.OrbitTilt.Length; k++)
            {
                float q = Mathf.Cos(time * T.OrbitTilt[k] + k * 1.3f), flat = Mathf.Max(0.08f, Mathf.Abs(q));
                DrawMesh(orbit, at, Overhead + 0.1435f + k * 0.0002f, r * 0.97f, r * 0.97f * flat, T.OrbitStart[k] + time * T.OrbitSpeed[k], Fade(KiSky, (0.35f + 0.4f * flat) * alpha), solid);
            }
            // Four arcs turning on its face: share of the radius, speed (degrees per second), span (degrees).
            for (int k = 0; k < 4; k++)
            {
                float share = ArcShare[k], start = time * ArcSpeed[k] + k * 120f;
                Sides(15, out Vector2[] a0, out Vector2[] a1);
                for (int i = 0; i <= 14; i++)
                {
                    float u = i / 14f, ang = (start + ArcSpan[k] * u) * Mathf.Deg2Rad, w = Mathf.Sin(u * Mathf.PI) * r * 0.06f, d = r * share;
                    a0[i] = Round(ang, d + w);
                    a1[i] = Round(ang, d - w);
                }
                Strip(a0, a1, Fade(White, 0.7f * alpha), solid, Overhead + 0.144f + k * 0.001f);
            }
            for (int arm = 0; arm < 2; arm++)
            {
                Vector2[] pts = Points(23);
                for (int i = 0; i <= 22; i++)
                {
                    float u = i / 22f;
                    pts[i] = Round((u * 400f + time * 150f + arm * 180f) * Mathf.Deg2Rad, r * (0.12f + 0.78f * u));
                }
                Line(pts, r * 0.09f, Fade(White, 0.5f * alpha), whiteGlow, Overhead + 0.1485f, Taper.Both);
            }
            // Lightning that crawls round the rim. Each bolt lives 0.22 s and is then rolled again somewhere else.
            int bolts = 3 + Mathf.Min(4, Mathf.FloorToInt(r * 2f));
            for (int b = 0; b < bolts; b++)
            {
                int cycle = Mathf.FloorToInt(s / 0.22f + b * 0.37f), seed = cycle * 31 + b * 7;
                float from = Rand(seed) * TAU, span = 0.6f + Rand(seed + 1) * 0.7f;
                Vector2[] pts = Points(10);
                for (int j = 0; j <= 9; j++) pts[j] = Round(from + span * j / 9f, r * (1.02f + 0.16f * Rand(seed + 3 + j) * Mathf.Sin(j / 9f * Mathf.PI)));
                Line(pts, 0.035f + r * 0.02f, Fade(White, 0.95f * alpha), whiteGlow, Overhead + 0.1495f, Taper.Both);
            }
            // Flashes on the face where energy lands.
            for (int i = 0; i < 6; i++)
            {
                float phase = s / 0.3f + i * 0.41f, u = phase % 1f;
                int seed = Mathf.FloorToInt(phase) * 17 + i * 5;
                Sprite(Round(Rand(seed) * TAU, r * (0.35f + 0.55f * Rand(seed + 2))), r * 0.5f + 0.15f, r * 0.5f + 0.15f, Fade(White, 0.75f * (1f - u) * alpha), glow, Overhead + 0.1498f);
            }
            DrawMesh(disc, at, Overhead + 0.15f, r * 0.34f * beat, r * 0.34f * beat, 0f, Fade(White, alpha), solid);
            Sprite(at, r * 1.5f * beat, r * 1.5f * beat, Fade(White, 0.85f * alpha), glow, Overhead + 0.151f);
            // A halo ring leaves the rim every 0.9 s.
            for (int n = 0; n < 2; n++)
            {
                float u = (s / 0.9f + n / 2f) % 1f;
                PaperBombGraphics.RingAt(at, r * (1.05f + 0.7f * u), Fade(KiIce, 0.55f * (1f - u) * alpha), Overhead + 0.139f, false, whiteGlow);
            }
            int motes = 6 + Mathf.Min(8, Mathf.FloorToInt(r * 3f));
            for (int i = 0; i < motes; i++)
            {
                float ang = time * (0.9f + 0.5f * Rand(i)) * (i % 2 == 1 ? 1f : -1f) + i * 2.399f, d = r * (1.2f + 0.25f * Mathf.Sin(s * 3f + i));
                Glint(Round(ang, d), 0.08f + r * 0.04f, 0.8f * alpha, KiIce, 45f);
            }
        }

        private static readonly float[] ArcShare = { 0.8f, 0.62f, 0.45f, 0.28f }, ArcSpeed = { 40f, -65f, 90f, -130f }, ArcSpan = { 140f, 170f, 200f, 160f };
    }
}
