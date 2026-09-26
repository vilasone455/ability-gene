using UnityEngine;
using Verse;
using static RimArt.GokuGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using G = RimArt.GokuTiming;
using T = RimArt.GokuSpiritBombTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Spirit Bomb. The channel: the ball high over the caster, shaded as a sphere and edged by
    /// a ring of flame (orbit lines, arcs, a spiral, crawling lightning, flashes, a halo, motes, a
    /// beating core), wisps rising from the ground into it, a ribbon of light from each lender's
    /// raised hand with the lender's glow, a ring and a swell as each one joins, and for a heavy bomb
    /// wind rings and pebbles round the caster; the blast ring on the floor at the true radius growing
    /// with the power. The flight, slow and heavy: the flames on the ball's back half sweep back,
    /// wisps stream off them over a soft glow trail, a soft bright front, falling sparks, a pool of
    /// light under it that tightens as it comes down, soft rings pressed out over the floor and dust.
    /// The grind: the ball flattening, sparks, cracks of light, lines running inward, chunks of ground
    /// lifting. The detonation: a white frame, then the ball grown into a half-sphere dome that stands
    /// and beats like a heart (rays, boiling light, crawling lightning, embers, a ring of flame round
    /// its outline, shock rings, low discharges and sparks on the floor behind it), a column and
    /// pillars of soft light, thrown rocks, dust rings. It bursts into flecks of light over a rising
    /// haze; what it spares stands in a blue shell; the scorch and the cooling cracks stay.
    ///
    /// The ball is a sphere; the dome is a half sphere drawn as its outline (a fan strip built each
    /// frame) with sprites and strips placed on it; the flecks sit on the same half sphere and the
    /// column's rings are level circles, so there is no per-facing method. Height is drawn 0.6 cells
    /// north per cell up; anything inside the dome is drawn under its body.
    ///
    /// Not drawn (the sketch's stand-ins): the pawns, their shadows and arms, the tints on the
    /// caster, lenders and enemies, the enemies turning white-hot and falling (hidden under the dome
    /// in the sketch too), the animal, the colony wall, and the spared pawns drawn again inside their
    /// shells. The shells themselves are drawn, pawn-sized, at every spared pawn and round the row of
    /// wall cells.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GokuSpiritBombGraphics
    {
        private static readonly Mesh orbit = VfxDraw.Ring(0.93f, "Spirit Bomb orbit");
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly Color Smoke = new Color(0.28f, 0.28f, 0.3f);
        private static readonly Vector2[] lenders = new Vector2[6], spared = new Vector2[2], walls = new Vector2[3];
        private static readonly int[] joined = new int[6];
        private static readonly SpiritBombPulse[] pulses = new SpiritBombPulse[T.MostPulses], beating = new SpiritBombPulse[T.MostPulses];
        private const float TAU = Mathf.PI * 2f;

        // The dome's outline at radius 1 round its centre, as drawn: for an outward direction az the edge lies on the level
        // circle atan(Lift x sin az) up, and facing south on the floor circle. The body is a fan of it, churned and scaled.
        private const int OutlineParts = 96;
        private static readonly Vector2[] Outline = new Vector2[OutlineParts];

        // A flame ring's tips and how far the flame reaches at each step round it.
        private static readonly float[] tipAng = new float[T.FlameTips], tipU = new float[T.FlameTips], tipH = new float[T.FlameTips];
        private static readonly float[] reach = new float[T.FlameTips * 6 + 1];
        private static readonly float[] ArcShare = { 0.8f, 0.62f, 0.45f, 0.28f }, ArcSpeed = { 40f, -65f, 90f, -130f }, ArcSpan = { 140f, 170f, 200f, 160f };

        static GokuSpiritBombGraphics()
        {
            for (int i = 0; i < OutlineParts; i++)
            {
                float az = i / (float)OutlineParts * TAU, el = Mathf.Atan(Lift * Mathf.Max(0f, Mathf.Sin(az)));
                Outline[i] = new Vector2(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(az) * Mathf.Cos(el) + Lift * Mathf.Sin(el));
            }
        }

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
            spared[0] = target + T.FriendAt;
            spared[1] = target + T.BeastAt;
            for (int k = 0; k < 3; k++) walls[k] = target + T.WallFrom + new Vector2(k, 0f);
            Draw(new SpiritBombShot
            {
                Caster = caster, Target = target, Seconds = seconds, Plan = plan, Lenders = lenders,
                Spared = spared, SparedCount = 2, Walls = walls, WallCount = 3,
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

            int cracks = t.Count(T.CrackCount.x, T.CrackCount.y), pillars = t.Count(T.PillarCount.x, T.PillarCount.y);
            int rocks = t.Count(T.RockCount.x, T.RockCount.y), burns = t.Count(T.StreakCount.x, T.StreakCount.y);
            float grindTime = t.Dome - t.Hit, cool = t.Gone - t.Open + T.Tail * 0.8f, whiteFrame = t.By(T.WhiteTime.x, T.WhiteTime.y);
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
            // The dome is the ball grown from its size at the throw (r, fixed once it is thrown) to the blast radius: its front
            // reaches R, fast then slow.
            float domeAge = s - t.Dome, opened = Mathf.Clamp01(domeAge / T.Open), R = Mathf.Lerp(r, blast, 1f - Mathf.Pow(1f - opened, 3f));
            // The dome is gone Pop seconds after the burst; the column and the pillars thin out over Fade.
            float burstAge = s - t.Burst, popped = Smooth(burstAge / T.Pop), domeAlpha = domeAge < 0f ? 0f : 1f - popped;
            float fading = Smooth(burstAge / T.Fade), lightAlpha = domeAge < 0f ? 0f : 1f - fading;
            // The dome's radius as drawn, and whether it still hides what is inside it (until it is half gone in the burst).
            float domeR = R * T.DomeFill * (1f + T.PopSwell * popped);
            bool hiding = domeAge >= 0f && domeAlpha > 0.5f;
            // Inside the dome: a point h cells up over a spot d cells from the centre. Drawn under its body so the dome covers it.
            bool InDome(float d, float h) => hiding && d * d + h * h < domeR * domeR;
            // A point on the dome's surface, as drawn: az is the direction from the centre, el the angle up from the floor.
            Vector2 OnDome(float az, float el, float rad) => Up(target + new Vector2(Mathf.Cos(az), Mathf.Sin(az)) * (rad * Mathf.Cos(el)), rad * Mathf.Sin(el));
            // The blue shell round a colonist, animal or wall inside. The dome hides them while it stands, so the shell shows as
            // it bursts: the light clears and they are standing in blue, untouched. Gone 0.5 s after the dome.
            float ShieldAt(float d) => domeAge >= 0f && d <= blast ? (1f - domeAlpha) * (1f - Smooth((burstAge - T.Pop) / 0.5f)) : 0f;
            // Surge: the swell each time a lender joins.
            float surge = 0f;
            for (int i = 0; i < t.Lenders; i++)
            {
                float age = s - t.Joins(i);
                if (age >= 0f && age < T.SurgeTime && s < t.Release) surge = Mathf.Max(surge, Mathf.Sin(Mathf.PI * age / T.SurgeTime));
            }

            // --- the floor ---
            if (s >= t.Cast && s < t.Dome)
            {
                float pulse = 0.45f + 0.2f * Mathf.Sin(s * 5f), strobe = grind > 0f ? 0.5f + 0.5f * Mathf.Sin(s * (30f + 40f * grind)) : 0f;
                PaperBombGraphics.RingAt(target, blast, Fade(KiSky, Mathf.Min(1f, pulse + 0.4f * grind) * formed), Floor + 0.02f);   // what a throw would cover right now
                DrawMesh(disc, target, Floor + 0.006f, blast, blast, 0f, Fade(KiDeep, (0.1f + 0.3f * grind * strobe) * formed), whiteGlow);
                // The pool of light on the floor under the ball: wide and faint while it is high, tight and bright as it comes
                // down, so the gap between the ball and its light shows its height.
                float low = 1f - Mathf.Clamp01(height / (hang + 0.01f));
                Sprite(ground, (r * 3f + 2f) * (1.6f - 0.8f * low), (r * 3f + 2f) * (1.3f - 0.6f * low), Fade(Ki, (0.18f + 0.5f * low) * formed), glow, Floor + 0.008f);
                Sprite(ground, r * (1f + 0.6f * low), r * (0.8f + 0.5f * low), Fade(KiIce, 0.45f * low * low * formed), glow, Floor + 0.0085f);
            }
            // The scorch: burnt ground that stays. A ragged edge of soft dark blotches round the blast radius with a few gaps,
            // uneven blackening inside that is darkest at the middle, and blast streaks of uneven spacing, length and width,
            // some past the edge. It burns in as the light clears, and thin smoke rises off it for about 2 s.
            float burnt = domeAge >= 0f ? Smooth(burstAge / 0.8f) * (1f - 0.35f * Smooth((s - t.Gone) / T.Tail)) : 0f;
            if (burnt > 0f)
            {
                Sprite(target, blast * 1.3f, blast * 1.2f, Fade(Ink, 0.32f * burnt), soft, Floor + 0.01f);   // darkest at the middle
                for (int i = 0; i < T.ScorchPatches; i++)
                {
                    float d = blast * 0.9f * Mathf.Sqrt(Rand(i + 500)), size = blast * (0.18f + 0.22f * Rand(i + 502));
                    Sprite(Polar(Rand(i + 501) * TAU, d), size, size * (0.7f + 0.3f * Rand(i + 503)), Fade(Ink, (0.12f + 0.14f * Rand(i + 504)) * (1f - 0.5f * d / blast) * burnt),
                        puff, Floor + 0.0102f, Rand(i + 505) * 180f);
                }
                for (int i = 0; i < T.ScorchEdge; i++)
                {
                    if (Rand(i + 513) < 0.15f) continue;   // a gap
                    float ang = (i + Rand(i + 510) * 0.8f) / T.ScorchEdge * TAU, size = blast * (0.1f + 0.12f * Rand(i + 512));
                    Sprite(Polar(ang, blast * (0.9f + 0.14f * Rand(i + 511))), size * 1.4f, size, Fade(Ink, (0.2f + 0.2f * Rand(i + 514)) * burnt), puff, Floor + 0.0104f,
                        -(ang + Mathf.PI / 2f) * Mathf.Rad2Deg);
                }
                for (int i = 0; i < burns; i++)
                {
                    float ang = (i + (Rand(i + 90) - 0.5f) * 0.9f) / burns * TAU, from = blast * (0.15f + 0.25f * Rand(i + 91)), to = blast * (0.6f + 0.55f * Rand(i + 92));
                    Streak(Polar(ang, from), Polar(ang, to), 0.15f + 0.35f * Rand(i + 93) * Rand(i + 94), Fade(Ink, (0.22f + 0.2f * Rand(i + 95)) * burnt), solid, Floor + 0.012f, 5);
                }
                for (int i = 0; i < 12; i++)
                {
                    float u = (burstAge - Rand(i + 520) * 1.2f) / (1.6f + 0.8f * Rand(i + 521));
                    if (u < 0f || u > 1f) continue;
                    Vector2 from = Polar(Rand(i + 522) * TAU, blast * 0.7f * Mathf.Sqrt(Rand(i + 523)));
                    float size = 0.8f + 1.6f * u;
                    Sprite(new Vector2(from.x + Mathf.Sin(u * 3f + i) * 0.3f, from.y + u * (1.5f + 1.5f * Rand(i + 524))), size, size * 0.85f,
                        Fade(Smoke, 0.22f * Mathf.Sin(u * Mathf.PI)), puff, Overhead + 0.05f, u * 40f + i * 30f);
                }
            }
            // Jagged cracks of light. They grow through the grind, are hidden while the dome stands, show again when it bursts,
            // and cool afterwards.
            if (s >= t.Hit && !hiding)
            {
                float grown = domeAge >= 0f ? 1f : Smooth(grind) * 0.75f, heat = domeAge < 0f ? 1f : 1f - 0.85f * Smooth((s - t.Open) / cool);
                Color hot = Color.Lerp(KiDeep, White, heat);
                for (int i = 0; i < cracks; i++)
                {
                    float ang = i * TAU / cracks + Rand(i + 3) * 0.35f, crackReach = blast * (0.5f + 0.45f * Rand(i + 8));
                    Vector2 way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)), normal = new Vector2(-way.y, way.x);
                    Vector2 At(int k) => target + way * (crackReach * k / 8f) + normal * (k > 0 ? (Rand(i * 13 + k) - 0.5f) * 0.7f : 0f);
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
                            fork[k] = new Vector2(root.x + Mathf.Cos(turn) * crackReach * 0.1f * k + (Rand(i * 7 + k + 50) - 0.5f) * 0.4f,
                                root.y + Mathf.Sin(turn) * crackReach * 0.1f * k + (Rand(i * 7 + k + 60) - 0.5f) * 0.4f);
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

            // --- structures inside the blast: they take nothing, and a blue shell round the row shows it as the light clears ---
            // The shell's lines are solid blue, not light, so they still show where the light has gone white.
            if (shot.WallCount > 0)
            {
                Vector2 low = shot.Walls[0], high = shot.Walls[0];
                for (int k = 1; k < shot.WallCount; k++) { low = Vector2.Min(low, shot.Walls[k]); high = Vector2.Max(high, shot.Walls[k]); }
                float wallShield = ShieldAt(Vector2.Distance((low + high) / 2f, target));
                if (wallShield > 0f)
                {
                    low -= new Vector2(0.62f, 0.62f);
                    high += new Vector2(0.62f, 0.62f);
                    Vector2 a = low, b = new Vector2(high.x, low.y), c = high, d = new Vector2(low.x, high.y);
                    Color edge = Fade(Ki, 0.95f * wallShield);
                    Streak(a, b, 0.08f, edge, solid, Overhead + 0.2f, 2);
                    Streak(b, c, 0.08f, edge, solid, Overhead + 0.2f, 2);
                    Streak(c, d, 0.08f, edge, solid, Overhead + 0.2f, 2);
                    Streak(d, a, 0.08f, edge, solid, Overhead + 0.2f, 2);
                    for (int k = 0; k < shot.WallCount; k++) Sprite(shot.Walls[k], 1.4f, 1.4f, Fade(Ki, 0.25f * wallShield), glow, Overhead + 0.199f);
                }
            }
            // A shell over a colonist or an animal: a solid blue ring round its body and a soft blue light over it.
            for (int g = 0; g < shot.SparedCount; g++)
            {
                Vector2 pos = shot.Spared[g];
                float shielded = ShieldAt(Vector2.Distance(pos, target));
                if (shielded <= 0f) continue;
                var at = new Vector2(pos.x, pos.y + 0.36f);
                Sprite(at, 0.62f * 2.4f, 0.62f * 2.4f, Fade(Ki, 0.3f * shielded), glow, Overhead + 0.199f);
                PaperBombGraphics.RingAt(at, 0.62f + 0.03f * Mathf.Sin(s * 9f), Fade(Ki, 0.95f * shielded), Overhead + 0.2f);
            }

            // --- the lenders' glow ---
            int joinedCount = 0;
            for (int i = 0; i < t.Lenders; i++)
            {
                Vector2 at = shot.Lenders[i];
                float lending = Lending(t, i, s);
                if (s >= t.Joins(i) && s < t.Release + 0.3f) joined[joinedCount++] = i;
                Sprite(new Vector2(at.x, at.y + 0.35f), 1.3f, 1.6f, Fade(KiSky, 0.4f * lending * (0.8f + 0.2f * Mathf.Sin(s * 11f))), glow, PawnLayer - 0.01f);
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

            // --- the ball in flight: a trail of its own flames, falling sparks, a soft bright front, the air pressed down under it ---
            // It is slow and heavy, so nothing here says speed: no tail strip, no rings across the path, no hard bow arc.
            bool flying = flight > 0f && grind <= 0f;
            Vector2 ahead = SeenAt(Mathf.Min(1f, flight + 0.01f)), before = SeenAt(Mathf.Max(0f, flight - 0.01f));
            float vl = Vector2.Distance(ahead, before);
            if (vl == 0f) vl = 1f;
            Vector2 v2 = flight > 0f ? (ahead - before) / vl : new Vector2(0f, -1f);
            if (flight > 0f && s < t.Dome)
                for (int i = 0; i < T.FallSparks; i++)
                {
                    float w = (i + 0.5f) / T.FallSparks, age = s - (t.Fly + t.FlyTime * w), life = 0.5f + 0.4f * Rand(i + 70);
                    if (age < 0f || age >= life) continue;
                    float u = age / life;
                    Vector2 c = SeenAt(w);
                    Glint(new Vector2(c.x + (Rand(i + 71) - 0.5f) * r * 1.6f, c.y + (Rand(i + 72) - 0.5f) * r * 1.6f - u * 0.8f), 0.1f + 0.08f * Rand(i), 1f - u, KiIce, 45f);
                }
            if (flying)
            {
                float speed = Mathf.Clamp01(flight * 3f), face = Mathf.Atan2(v2.y, v2.x);
                // A soft glow along the path behind it, shrinking and dimming: light, with no edge.
                for (int j = 1; j <= 8; j++)
                {
                    Vector2 c = SeenAt(Mathf.Max(0f, flight - T.TailSpan * flight * j / 8f));
                    float k = 1f - j / 9f;
                    Sprite(c, r * (0.5f + 1.1f * k), r * (0.5f + 1.1f * k), Fade(Ki, 0.35f * k * speed), glow, Overhead + 0.133f);
                }
                // Wisps break off its swept flames and stream behind, each for 0.4 s, drifting back and up as they fade.
                for (int i = 0; i < T.TrailWisps; i++)
                {
                    float u = ((s - t.Fly) / 0.4f + Rand(i + 600)) % 1f, ang = face + Mathf.PI + (Rand(i + 601) - 0.5f) * 2.2f;
                    Vector2 then = SeenAt(Mathf.Clamp01((s - u * 0.4f - t.Fly) / t.FlyTime));
                    var at = new Vector2(then.x + Mathf.Cos(ang) * r * 1.05f - v2.x * u * r * 0.5f, then.y + Mathf.Sin(ang) * r * 1.05f - v2.y * u * r * 0.5f + u * 0.4f);
                    float size = r * (0.12f + 0.1f * Rand(i + 602)) * (1f - 0.5f * u);
                    Sprite(at, size, size, Fade(KiIce, 0.6f * (1f - u) * speed), glow, Overhead + 0.1335f);
                }
                // A soft bright front where it pushes the air.
                for (int j = 0; j <= 6; j++)
                {
                    float ang = face + (j / 6f - 0.5f) * 1.7f;
                    Sprite(ball + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r * 1.2f), r * 0.6f, r * 0.6f, Fade(KiIce, 0.35f * speed * (1f - Mathf.Abs(j / 6f - 0.5f))), glow, Overhead + 0.153f);
                }
                // Once it is low it presses the air down: a soft ring spreads over the floor from under it every 0.2 s, and dust is blown out.
                if (flight > 0.55f)
                {
                    float lowIn = Mathf.Clamp01((flight - 0.55f) / 0.2f);
                    for (int n = 0; n < 3; n++)
                    {
                        float u = ((s - t.Fly) / 0.6f + n / 3f) % 1f;
                        PaperBombGraphics.RingAt(ground, r * (0.8f + 1.4f * Smooth(u)), Fade(KiIce, 0.45f * (1f - u) * lowIn), Floor + 0.025f, false, whiteGlow);
                    }
                    for (int i = 0; i < 14; i++)
                    {
                        float u = ((s - t.Fly) * 2.2f + Rand(i + 33)) % 1f, side = i % 2 == 1 ? 1f : -1f, d = r * 0.5f + u * (1.5f + Rand(i + 34) * 1.5f), back = Rand(i + 35) * 1.5f;
                        Vector2 at = G.Place(ground, toward, -back, side * d);
                        Sprite(new Vector2(at.x, at.y + u * 0.3f), 0.7f + u, 0.5f + u * 0.8f, Fade(Dust, 0.45f * Mathf.Sin(u * Mathf.PI) * lowIn), soft, Overhead + 0.005f);
                    }
                }
            }
            // Up to the detonation; from then on the ball is drawn as the dome.
            if (s >= t.Cast && s < t.Dome)
            {
                float swell = 1f + T.SurgeSwell * surge + 0.18f * grind + 0.05f * Mathf.Sin(s * 60f) * grind;
                if (grind > 0f) Bomb(ball, r * formed * swell, s, 1f, new Vector2(0f, -1f), -0.3f * sink, T.FlightSpin + 3f * grind, 0f, sun, t.Pace, 0f);
                else Bomb(ball, r * formed * swell, s, 1f, v2, T.Stretch * Mathf.Clamp01(flight * 3f), 1f + (T.FlightSpin - 1f) * Mathf.Clamp01(flight * 3f) + 1.5f * surge, surge,
                    sun, t.Pace, 1.5f * Mathf.Clamp01(flight * 3f));
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
            // Inside the dome it is drawn under the body, so it shows only once it flies out.
            if (domeAge >= 0f)
            {
                float rockHeight = t.By(T.RockHeight.x, T.RockHeight.y);
                for (int i = 0; i < rocks; i++)
                {
                    float air = 0.9f + 0.9f * Rand(i + 51), u = domeAge / air, ang = Rand(i + 52) * TAU, big = Rand(i + 55), size = 0.24f + 0.75f * big * big;
                    float from = blast * (0.15f + 0.5f * Rand(i + 53)), rockReach = blast * 0.75f, peak = (3f + 5f * Rand(i + 54)) * rockHeight * (1.15f - 0.5f * big);
                    float Along(float w) => from + rockReach * Mathf.Min(1f, w);
                    float H(float w) => w < 1f ? peak * 4f * w * (1f - w) : 0f;
                    Vector2 foot = Polar(ang, Along(u));
                    float h = H(u);
                    bool landed = u >= 1f;
                    float turn = i * 47f + Mathf.Min(u, 1f) * air * (300f + 300f * Rand(i + 56));
                    Sprite(foot + sun * h, size * 2.2f, size * 1.1f, Fade(Ink, 0.32f), soft, Floor + 0.05f);
                    if (!landed)
                        for (int k = 1; k <= 3; k++)
                        {
                            float w = u - k * 0.05f;
                            if (w <= 0f) continue;
                            Sprite(Up(Polar(ang, Along(w)), H(w)), size * (1.2f + k * 0.5f), size * (1f + k * 0.4f), Fade(Dust, 0.3f * (1f - k / 4f)), soft,
                                InDome(Along(w), H(w)) ? Overhead + 0.134f : Overhead + 0.174f);
                        }
                    PaperBombGraphics.Rock(Up(foot, h), size, turn, 1f, i, landed ? Floor + 0.06f : InDome(Along(u), h) ? Overhead + 0.134f : Overhead + 0.175f);
                    float since = domeAge - air;
                    if (since >= 0f && since < 0.5f)
                    {
                        float v = since / 0.5f;
                        Sprite(new Vector2(foot.x, foot.y + v * 0.25f), size * (2f + 3f * v), size * (1.5f + 2.2f * v), Fade(Dust, 0.5f * Mathf.Sin(v * Mathf.PI)), soft, Overhead + 0.006f);
                    }
                }
            }

            // --- the detonation ---
            if (domeAge >= 0f && domeAge < whiteFrame * 2f)   // soft-edged, so no hard white circle
                Sprite(target, blast * 3.4f, blast * 3.4f, Fade(White, domeAge < whiteFrame ? 1f : 1f - (domeAge - whiteFrame) / whiteFrame), glow, Overhead + 0.19f);
            if (domeAge >= 0f && domeAge < 0.4f)   // whiteout
                Sprite(target, blast * 5f, blast * 5f, Fade(KiIce, (0.3f + 0.4f * t.Charge) * Mathf.Pow(1f - domeAge / 0.4f, 2f)), glow, Overhead + 0.15f);
            // The dome: the ball grown from its size at the throw to the blast radius, in its explosion. It hides what is
            // inside; the result shows when it bursts. It builds up through the hold and swells a little as it bursts.
            if (domeAlpha > 0f)
            {
                int all = T.Pulses(t, pulses), count = 0;
                for (int i = 0; i < all; i++)
                {
                    float age = s - pulses[i].At;
                    if (age >= 0f && age * t.Pace < 1.2f) beating[count++] = new SpiritBombPulse { At = age * t.Pace, Strength = pulses[i].Strength };
                }
                DrawDome(target, domeR, s, domeAlpha, Smooth((s - t.Open) / (t.Burst - t.Open)), sun, beating, count, t.Pace);
            }
            // The column of light. It is light, not an object: no mesh with an edge, only soft sprites that overlap up its
            // height, each one rippling in width and dimmer than the one below, so it has no outline and no tip. Streaks run
            // up through it, a pool of light sits at its foot, and rings climb it. It narrows to a thread after the burst.
            if (lightAlpha > 0f)
            {
                float tall = (2.5f + 1.9f * blast + T.ColumnExtra * t.Charge) * Smooth(domeAge / 0.25f), girth = 1f - 0.85f * fading, piece = tall / 16f;
                float WidthAt(float u) => blast * (0.24f - 0.13f * Mathf.Pow(u, 0.7f)) * girth * (1f + 0.16f * Mathf.Sin(u * 11f - s * 15f) + 0.08f * Mathf.Sin(u * 23f - s * 27f));
                Sprite(target, blast * 1.2f * girth, blast * 0.7f * girth, Fade(KiIce, 0.4f * lightAlpha), glow, hiding ? Overhead + 0.134f : Overhead + 0.1695f);
                for (int j = 0; j < 16; j++)
                {
                    // Full to 60% of the height, then it thins out to nothing.
                    float u = (j + 0.5f) / 16f, w = WidthAt(u), dim = Mathf.Clamp01((1f - u) / 0.4f) * (1f - 0.3f * u) * lightAlpha * (0.85f + 0.15f * Mathf.Sin(s * 33f + j * 1.7f));
                    Vector2 c = Up(target, tall * u);
                    float layer = InDome(0f, tall * u) ? Overhead + 0.134f : Overhead + 0.17f;   // inside the dome it is under its body: it comes out of the top
                    Sprite(c, w * 3.2f, piece * Lift * 3.2f, Fade(Ki, 0.6f * dim), glow, layer);
                    Sprite(c, w * 1.7f, piece * Lift * 2.8f, Fade(KiIce, 0.75f * dim), glow, layer + 0.0005f);
                    Sprite(c, w * 0.8f, piece * Lift * 2.6f, Fade(White, 0.85f * dim), glow, layer + 0.001f);
                }
                for (int i = 0; i < 18; i++)
                {
                    float u = (domeAge * (1.4f + Rand(i + 61)) + Rand(i + 62)) % 1f, x = (Rand(i + 63) - 0.5f) * WidthAt(u) * 1.8f, longer = tall * (0.1f + 0.12f * Rand(i + 64));
                    Vector2 from = Up(target, tall * u), to = Up(target, Mathf.Min(tall, tall * u + longer));
                    Streak(new Vector2(from.x + x, from.y), new Vector2(to.x + x, to.y), 0.09f, Fade(White, 0.8f * Mathf.Sin(u * Mathf.PI) * (1f - u) * lightAlpha), whiteGlow,
                        InDome(0f, tall * u) ? Overhead + 0.1345f : Overhead + 0.1715f, 4);
                }
                for (int n = 0; n < 5; n++)
                {
                    float u = (domeAge * 0.9f + n / 5f) % 1f;
                    PaperBombGraphics.RingAt(Up(target, tall * u), WidthAt(u) * 1.4f + 0.1f, Fade(KiIce, 0.4f * Mathf.Sin(u * Mathf.PI) * (1f - u) * lightAlpha),
                        InDome(0f, tall * u) ? Overhead + 0.1345f : Overhead + 0.172f, false, whiteGlow);
                }
                // The thin pillars, the same way: a soft shaft that flickers, brightest at the floor, with a bead running up it.
                for (int i = 0; i < pillars; i++)
                {
                    float ang = i * 2.399f, d = blast * (0.3f + 0.6f * Rand(i + 60));
                    if (R < d) continue;
                    Vector2 foot = Polar(ang, d);
                    float h = (3f + 4f * Rand(i + 70)) * Smooth((s - t.Passes(d)) / 0.3f), flick = (0.7f + 0.3f * Mathf.Sin(s * 30f + i * 2f)) * lightAlpha;
                    for (int j = 0; j < 5; j++)
                    {
                        float u = (j + 0.5f) / 5f, layer = InDome(d, h * u) ? Overhead + 0.134f : Overhead + 0.166f;
                        Vector2 c = Up(foot, h * u);
                        Sprite(c, 0.9f - 0.4f * u, h * Lift * 0.5f, Fade(Ki, 0.4f * (1f - u) * flick), glow, layer);
                        Sprite(c, 0.3f - 0.12f * u, h * Lift * 0.45f, Fade(White, 0.75f * (1f - u) * flick), glow, layer + 0.001f);
                    }
                    Sprite(foot, 1.3f, 0.8f, Fade(KiIce, 0.6f * flick), glow, hiding ? Overhead + 0.134f : Overhead + 0.1665f);
                    float bead = (s * 1.6f + Rand(i + 75)) % 1f;
                    Sprite(Up(foot, h * bead), 0.3f, 0.5f, Fade(White, 0.9f * (1f - bead) * flick), glow, InDome(d, h * bead) ? Overhead + 0.134f : Overhead + 0.168f);
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

            // --- the burst: the dome breaks into flecks of light that drift up and a little out, twinkle, and fade ---
            if (burstAge >= 0f && burstAge < T.Scatter)
            {
                float rad = blast * (1f + T.PopSwell), haze = 1f - Smooth(burstAge / 1.2f);
                if (burstAge < 0.15f) Sprite(Up(target, rad * 0.35f), rad * 2.8f, rad * 2.8f, Fade(White, 0.45f * (1f - burstAge / 0.15f)), glow, Overhead + 0.185f);   // the flash as it bursts
                for (int k = 0; k < 7; k++)   // a thin haze where the dome was, rising
                    Sprite(Up(k > 0 ? OnDome(k * TAU / 6f, 0.45f, rad) : Up(target, rad * 0.8f), burstAge * 0.8f), rad * 0.9f, rad * 0.9f, Fade(KiSky, 0.2f * haze), glow, Overhead + 0.176f);
                int flecks = t.Count(T.FleckCount.x, T.FleckCount.y);
                for (int i = 0; i < flecks; i++)
                {
                    float a = burstAge - Rand(i + 309) * 0.12f, life = 1.1f + 0.9f * Rand(i + 303), u = a / life;
                    if (u < 0f || u >= 1f) continue;
                    // Born where the dome was: a little over half on its surface (spread evenly: the sine of the angle up is even),
                    // the rest through its inside, so the burst fills the whole blast. Then it drifts out and rises, easing off.
                    float az = Rand(i + 300) * TAU, el = Mathf.Asin(Rand(i + 301)), deep = Rand(i + 312) < 0.55f ? 1f : Mathf.Pow(Rand(i + 313), 1f / 3f), ease = 1f - (1f - u) * (1f - u);
                    float d = rad * deep * Mathf.Cos(el) + blast * (0.03f + 0.07f * Rand(i + 304)) * ease, h = rad * deep * Mathf.Sin(el) + (1f + 2f * Rand(i + 305)) * ease;
                    Vector2 at = Up(new Vector2(target.x + Mathf.Cos(az) * d + Mathf.Sin(a * 3f + i) * 0.15f, target.y + Mathf.Sin(az) * d), h);
                    float twinkle = 0.55f + 0.45f * Mathf.Sin(a * (9f + 8f * Rand(i + 306)) + i * 1.7f), alpha = twinkle * Mathf.Pow(1f - u, 1.2f) * Mathf.Clamp01(a / 0.06f);
                    float size = 0.1f + 0.14f * Rand(i + 307);
                    Color colour = Rand(i + 308) < 0.25f ? KiSky : Color.Lerp(KiIce, White, Rand(i + 310));
                    Sprite(at, size, size, Fade(colour, alpha), whiteGlow, Overhead + 0.18f, 45f + a * 120f * (Rand(i + 311) - 0.5f));
                    if (i % 6 == 0) Glint(at, size * 2.4f, 0.85f * alpha, KiIce, 45f);
                }
            }
        }

        /// <summary>How much lender <paramref name="i"/> gives now: up over 0.3 s from joining, down over 0.3 s from the throw.</summary>
        private static float Lending(in SpiritBombPlan t, int i, float s) => Smooth((s - t.Joins(i)) / 0.3f) * (1f - Smooth((s - t.Release) / 0.3f));

        /// <summary>The churn of the dome's outline at direction <paramref name="az"/>: two bulges travelling round it, 1% and 0.7% of the radius.</summary>
        private static float Churn(float az, float s) => 1f + 0.01f * Mathf.Sin(2f * az - s * 3f) + 0.007f * Mathf.Sin(3f * az + s * 4.3f);

        /// <summary>
        /// The outline a flame ring runs round: the ball's (round, stretched along <see cref="V"/>) or
        /// the dome's (the half sphere's outline, churning). <see cref="Point"/> is the outline's point
        /// at an angle (0 east, anticlockwise) and a share of the radius.
        /// </summary>
        private struct Rim
        {
            public Vector2 At, V;
            public float R, Sx, Sz, S;
            public bool Dome;

            public Vector2 Point(float ang, float k)
            {
                if (Dome)
                {
                    float el = Mathf.Atan(Lift * Mathf.Max(0f, Mathf.Sin(ang)));
                    k *= Churn(ang, S);
                    return new Vector2(At.x + Mathf.Cos(ang) * Mathf.Cos(el) * R * k, At.y + (Mathf.Sin(ang) * Mathf.Cos(el) + Lift * Mathf.Sin(el)) * R * k);
                }
                return Stretched(At, V, Sx, Sz, Mathf.Cos(ang) * R * k, Mathf.Sin(ang) * R * k);
            }
        }

        /// <summary>A point <paramref name="dx"/>, <paramref name="dz"/> from the centre of a round ball, stretched along <paramref name="v"/>.</summary>
        private static Vector2 Stretched(Vector2 at, Vector2 v, float sx, float sz, float dx, float dz)
        {
            float al = (dx * v.x + dz * v.y) * sx, ac = (-dx * v.y + dz * v.x) * sz;
            return new Vector2(at.x + al * v.x - ac * v.y, at.y + al * v.y + ac * v.x);
        }

        /// <summary>
        /// A ring of flame round an outline, shared by the ball and the dome so they are made of the
        /// same stuff. Its outer edge is a row of <paramref name="count"/> pointed tips; each rises and
        /// falls on its own, flickers, surges out once every 0.28 to 0.4 s, and is up to 1.6 times
        /// taller on the top edge, where flames rise. Three nested strips, the widest and faintest blue
        /// outside and narrower and whiter inside, over a soft blue glow; at the top of each surge a
        /// wisp breaks off the tip and drifts out and up. The strips start just inside the edge, under
        /// the body. <paramref name="boost"/> multiplies the tips' height; with
        /// <paramref name="sweep"/> above 0 the tips on the back half of <paramref name="sweepAng"/>
        /// are up to 1 + sweep times longer, swept back.
        /// </summary>
        private static void FlameRing(in Rim rim, float r, float s, float alpha, int count, float boost, float layer, float sweepAng = 0f, float sweep = 0f)
        {
            for (int i = 0; i < 48; i++) Sprite(rim.Point(i / 48f * TAU, 1f), r * 0.24f, r * 0.24f, Fade(Ki, 0.24f * alpha), glow, layer - 0.0001f);
            for (int k = 0; k < count; k++)
            {
                float phase = s / (0.28f + 0.12f * Rand(k + 71)) + Rand(k + 72), u = phase - Mathf.Floor(phase), flicker = 0.7f + 0.3f * Mathf.Sin(s * (19f + 7f * Rand(k + 73)) + k * 2.1f);
                float ang = (k + 0.5f + (Rand(k + 70) - 0.5f) * 0.8f) / count * TAU;
                tipAng[k] = ang;
                tipU[k] = u;
                tipH[k] = (0.025f + 0.05f * Rand(k + 74)) * flicker * (1f + 0.8f * Mathf.Sin(u * Mathf.PI)) * (1f + 0.6f * Mathf.Max(0f, Mathf.Sin(ang))) * boost
                    * (sweep > 0f ? 1f + sweep * Mathf.Max(0f, -Mathf.Cos(ang - sweepAng)) : 1f);
            }
            // How far the flame reaches past the edge at an angle: the tallest tip there, each a point with hollow sides, so
            // neighbours leave a dip between them. A tip reaches 0.9 of the spacing each way and sits within 0.4 of its slot, so
            // only the two tips on each side of a step can reach it.
            float half = TAU / count * 0.9f;
            int steps = count * 6;
            for (int i = 0; i <= steps; i++)
            {
                float ang = i / (float)steps * TAU, h = 0f;
                int near = i / 6;
                for (int n = near - 2; n <= near + 2; n++)
                {
                    int k = (n % count + count) % count;
                    float d = Mathf.Abs(Mathf.Repeat(ang - tipAng[k] + Mathf.PI, TAU) - Mathf.PI) / half;
                    if (d < 1f) h = Mathf.Max(h, tipH[k] * Mathf.Pow(1f - d, 1.8f));
                }
                reach[i] = h;
            }
            for (int j = 0; j < 3; j++)
            {
                float share = j == 0 ? 1f : j == 1 ? 0.6f : 0.3f, a = j == 0 ? 0.35f : j == 1 ? 0.45f : 0.5f;
                Color colour = j == 0 ? Ki : j == 1 ? KiSky : White;
                Sides(steps + 1, out Vector2[] inner, out Vector2[] outer);
                for (int i = 0; i <= steps; i++)
                {
                    float ang = i / (float)steps * TAU;
                    inner[i] = rim.Point(ang, 0.97f);
                    outer[i] = rim.Point(ang, 1.01f + reach[i] * share);
                }
                Strip(inner, outer, Fade(colour, a * alpha), whiteGlow, layer + j * 0.0002f);
            }
            for (int k = 0; k < count; k++)
            {
                if (tipU[k] <= 0.5f) continue;
                float v = (tipU[k] - 0.5f) / 0.5f, size = r * (0.07f - 0.035f * v);
                Vector2 from = rim.Point(tipAng[k], 1.01f + tipH[k]), to = rim.Point(tipAng[k], 1.01f + tipH[k] * 1.8f);
                Sprite(new Vector2(Mathf.Lerp(from.x, to.x, v), Mathf.Lerp(from.y, to.y, v) + 0.4f * v), size, size, Fade(KiIce, 0.6f * (1f - v) * alpha), glow, layer + 0.0006f);
            }
        }

        /// <summary>
        /// The ball. <paramref name="r"/> is its radius in cells. <paramref name="v"/> is the unit
        /// direction it is stretched along and <paramref name="stretch"/> how much (negative flattens
        /// it), <paramref name="spin"/> multiplies every turning speed, <paramref name="surge"/> 0 to 1
        /// is the swell of a lender joining. Its flame ring runs on the dome's clock
        /// (<paramref name="pace"/>); in flight its back half sweeps back by <paramref name="sweep"/>.
        /// </summary>
        private static void Bomb(Vector2 at, float r, float s, float alpha, Vector2 v, float stretch, float spin, float surge, Vector2 sun, float pace, float sweep)
        {
            if (r <= 0.01f || alpha <= 0f) return;
            float beat = 1f + (0.06f + 0.1f * surge) * Mathf.Sin(s * 9f), breathe = 1f + 0.05f * Mathf.Sin(s * 3.1f), turn = -Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            float sx = 1f + stretch, sz = 1f - 0.35f * stretch, time = s * spin;
            Vector2 On(float dx, float dz) => Stretched(at, v, sx, sz, dx, dz);
            Vector2 Round(float ang, float rad) => On(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);
            // Toward the light: opposite to where shadows fall.
            float light = sun.magnitude;
            if (light == 0f) light = 1f;
            float lx = -sun.x / light, lz = -sun.y / light, sunAz = Mathf.Atan2(lz, lx);

            Sprite(at, r * 4.6f * breathe, r * 4.6f * breathe, Fade(Ki, (0.5f + 0.3f * surge) * alpha), glow, Overhead + 0.14f);
            // The same ring of flame as the dome's, with fewer tips and taller for its size; it flares when a lender joins.
            var rim = new Rim { At = at, V = v, R = r, Sx = sx, Sz = sz };
            FlameRing(rim, r, s * pace, alpha, T.BallTips, 1.6f + 1.2f * surge, Overhead + 0.1405f, Mathf.Atan2(v.y, v.x), sweep);
            // Shaded as a sphere, like the dome: the body, a pale middle as a soft gradient a little toward the light, a deep
            // blue limb darker on the side away from the light, and a highlight toward it.
            DrawMesh(disc, at, Overhead + 0.141f, r * sx, r * sz, turn, Fade(KiSky, alpha), solid);
            Sprite(On(lx * r * 0.15f, lz * r * 0.15f), r * 1.75f * sx, r * 1.75f * sz, Fade(KiIce, 0.85f * alpha), soft, Overhead + 0.142f, turn);
            for (int i = 0; i < 48; i++)
            {
                float ang = i / 48f * TAU, shade = 0.5f - 0.5f * Mathf.Cos(ang - sunAz);
                Sprite(Round(ang, r * 0.9f), r * 0.34f, r * 0.34f, Fade(KiDeep, (0.08f + 0.24f * shade) * alpha), soft, Overhead + 0.143f);
            }
            Sprite(On(lx * r * 0.45f, lz * r * 0.45f), r * 0.5f, r * 0.4f, Fade(White, 0.4f * alpha), glow, Overhead + 0.1432f);
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

        /// <summary>
        /// The dome: the ball in its explosion, as a half sphere standing on the floor. The ball's
        /// palette and parts with nothing turning and everything pushing outward, drawn with depth:
        /// its outline is the half sphere's (the floor circle to the south, bulging north where its
        /// top is), it is shaded (a deep blue limb, darker away from the sun, and a highlight toward
        /// the sun near the top), rays of light run down its curved surface to the floor, and what is
        /// on the floor behind it (lightning, shock rings, sparks) is drawn under its body so it hides.
        /// While it stands it beats like a heart: each pulse in <paramref name="beats"/> (the
        /// <see cref="SpiritBombPulse.At"/> field holds its age on the dome's clock) kicks it out in
        /// 0.05 s and eases back, sends a bright band down its surface to the floor in 0.15 s, and when
        /// the band lands throws a shock ring and makes its flames flare. <paramref name="build"/> 0 to
        /// 1 is how far the hold has gone (in the last third it swells, brightens and its flames
        /// surge). Everything runs on its own clock, <paramref name="pace"/> times real time.
        /// </summary>
        private static void DrawDome(Vector2 at, float r, float s, float alpha, float build, Vector2 sun, SpiritBombPulse[] beats, int beatCount, float pace)
        {
            if (r <= 0.01f || alpha <= 0f) return;
            s *= pace;
            float rush = build * build * build, push = 0f, flash = 0f, flare = 0f;
            for (int n = 0; n < beatCount; n++)
            {
                float age = beats[n].At, kick = Kick(age);
                push += beats[n].Strength * kick;
                flash = Mathf.Max(flash, kick);
                flare += age < 0.15f ? 0f : Mathf.Exp(-(age - 0.15f) / 0.12f);
            }
            r *= 1f + push + 0.04f * rush;
            float beat = 1f + (0.07f + 0.05f * build) * Mathf.Sin(s * 14f);
            Vector2 Floor2(float ang, float rad) => at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            // A point on the half sphere as drawn: az the direction from the centre, el the angle up from the floor.
            Vector2 On(float az, float el, float rad) => new Vector2(at.x + Mathf.Cos(az) * rad * Mathf.Cos(el), at.y + Mathf.Sin(az) * rad * Mathf.Cos(el) + Lift * rad * Mathf.Sin(el));
            // The lowest angle up at which the surface faces the viewer: 0 facing south, up to the outline facing north.
            float Low(float az) => Mathf.Atan(Lift * Mathf.Max(0f, Mathf.Sin(az)));
            Vector2 mid = new Vector2(at.x, at.y + Lift * r * 0.42f);
            float sunAz = Mathf.Atan2(-sun.y, -sun.x);
            Sprite(mid, r * 3.2f, r * 3.2f, Fade(Ki, (0.5f + 0.2f * flash + 0.25f * rush) * alpha), glow, Overhead + 0.1385f);
            // On the floor, behind the body where the dome covers it: the shock ring each pulse throws when its band lands (out
            // to 1.45 radii in 0.5 s), lightning jumping out from the foot and back (0.16 s each), sparks thrown off (0.5 s each).
            for (int n = 0; n < beatCount; n++)
            {
                float u = (beats[n].At - 0.15f) / 0.5f;
                if (u >= 0f && u < 1f) PaperBombGraphics.RingAt(at, r * (1.02f + 0.43f * Smooth(u)), Fade(KiIce, 0.75f * (1f - u) * alpha), Overhead + 0.139f, false, whiteGlow);
            }
            for (int b = 0; b < 7; b++)
            {
                int cycle = Mathf.FloorToInt(s / 0.16f + b * 0.37f), seed = cycle * 31 + b * 7;
                float from = Rand(seed) * TAU, span = 0.12f + Rand(seed + 1) * 0.2f, lift = 0.05f + 0.12f * Rand(seed + 2);
                Vector2[] pts = Points(13);
                for (int j = 0; j <= 12; j++)
                {
                    float k = j / 12f, bow = Mathf.Sin(k * Mathf.PI);
                    pts[j] = Floor2(from + span * k, r * (1f + lift * bow + 0.06f * (Rand(seed + 3 + j) - 0.5f) * bow));
                }
                Line(pts, 0.05f + r * 0.012f, Fade(White, 0.95f * alpha), whiteGlow, Overhead + 0.1395f, Taper.Both);
            }
            for (int i = 0; i < 20; i++)
            {
                float phase = s / 0.5f + Rand(i + 40), u = phase - Mathf.Floor(phase);
                int cycle = Mathf.FloorToInt(phase);
                Glint(Floor2(Rand(cycle * 13 + i) * TAU, r * (0.98f + 0.35f * u)), 0.1f + 0.06f * Rand(i), 0.9f * (1f - u) * alpha, KiIce, 45f);
            }
            // The flame ring round the churning outline: it flares when a pulse lands and surges in the rush.
            var rim = new Rim { At = at, R = r, S = s, Dome = true };
            FlameRing(rim, r, s, alpha, T.FlameTips, 1f + 0.9f * flare + 0.5f * rush, Overhead + 0.1405f);
            // The body: the half sphere's churning outline filled, a fan from a point a third of the way up, opaque so nothing
            // under it shows through; then the pale inside as a soft gradient round its middle, then the limb: deep blue just
            // inside the outline, darkest on the side away from the sun.
            Sides(OutlineParts + 1, out Vector2[] hub, out Vector2[] edge);
            var centre = new Vector2(at.x, at.y + Lift * 0.35f * r);
            for (int i = 0; i <= OutlineParts; i++)
            {
                int q = i % OutlineParts;
                hub[i] = centre;
                edge[i] = at + Outline[q] * (r * Churn(q / (float)OutlineParts * TAU, s));
            }
            Strip(hub, edge, Fade(KiSky, alpha), solid, Overhead + 0.141f);
            Sprite(mid, r * 1.8f, r * 1.7f, Fade(KiIce, 0.8f * alpha), soft, Overhead + 0.1415f);
            for (int i = 0; i < OutlineParts; i++)
            {
                float az = i / (float)OutlineParts * TAU, shade = 0.5f - 0.5f * Mathf.Cos(az - sunAz);
                Sprite(at + Outline[i] * (r * 0.9f * Churn(az, s)), r * 0.34f, r * 0.34f, Fade(KiDeep, (0.05f + 0.16f * shade) * alpha), soft, Overhead + 0.142f);
            }
            // Rays of light run down the surface from near the top to the floor, over and over, curving with it.
            for (int i = 0; i < T.DomeRays; i++)
            {
                float phase = s / 0.45f + Rand(i + 5), u = phase - Mathf.Floor(phase);
                int cycle = Mathf.FloorToInt(phase);
                float az = (i + (Rand(cycle * 7 + i) - 0.5f) * 0.6f) / T.DomeRays * TAU, head = Mathf.Lerp(1.45f, Low(az), u), tail = Mathf.Min(1.45f, head + 0.5f);
                Vector2[] pts = Points(9);
                for (int j = 0; j <= 8; j++) pts[j] = On(az, Mathf.Lerp(tail, head, j / 8f), r);
                Line(pts, r * 0.02f + 0.04f, Fade(White, 0.5f * Mathf.Sin(u * Mathf.PI) * alpha), whiteGlow, Overhead + 0.143f, Taper.Both);
            }
            // Each pulse sends a bright band down the surface, a level circle from near the top to the floor in 0.15 s, drawn
            // where it faces the viewer: the whole circle up high, only the near side lower down.
            for (int n = 0; n < beatCount; n++)
            {
                float age = beats[n].At;
                if (age > 0.2f) continue;
                float el = Mathf.Lerp(1.45f, 0.02f, age / 0.15f), k = Mathf.Tan(el) / Lift, hw = k >= 1f ? Mathf.PI : Mathf.PI / 2f + Mathf.Asin(k);
                Vector2[] pts = Points(41);
                for (int j = 0; j <= 40; j++) pts[j] = On(1.5f * Mathf.PI - hw + 2f * hw * j / 40f, el, r);
                float fade = (1f - Smooth((age - 0.15f) / 0.05f)) * alpha;
                Taper ends = hw >= Mathf.PI ? Taper.None : Taper.Both;
                Line(pts, 0.5f + r * 0.06f, Fade(KiIce, 0.35f * fade), whiteGlow, Overhead + 0.1439f, ends);    // a wide soft band of light
                Line(pts, 0.05f + r * 0.008f, Fade(White, 0.6f * fade), whiteGlow, Overhead + 0.14392f, ends);  // and a thin bright line in it
            }
            // Light boils up the surface: soft blobs rise from low on the side that shows to near the top, growing as they go,
            // each 0.6 s and started again somewhere else.
            for (int i = 0; i < T.DomeBoils; i++)
            {
                float phase = s / 0.6f + Rand(i + 80), u = phase - Mathf.Floor(phase);
                int seed = Mathf.FloorToInt(phase) * 19 + i * 3;
                float az = Rand(seed) * TAU, size = r * (0.1f + 0.12f * u) * (0.8f + 0.4f * Rand(seed + 1));
                Sprite(On(az, Mathf.Lerp(Low(az) + 0.05f, 1.35f, u), r), size, size * 0.85f, Fade(Color.Lerp(KiIce, White, Rand(seed + 2)), 0.35f * Mathf.Sin(u * Mathf.PI) * alpha), glow, Overhead + 0.1435f);
            }
            // Lightning crawls over the surface: short bolts that wander across the side that shows, each rolled again every 0.12 s.
            for (int b = 0; b < 4; b++)
            {
                int seed = Mathf.FloorToInt(s / 0.12f + b * 0.29f) * 37 + b * 11;
                float az = Rand(seed) * TAU, floorEl = Low(az) + 0.02f;
                float el = Mathf.Lerp(Low(az) + 0.1f, 1.2f, Rand(seed + 1)), daz = (Rand(seed + 2) - 0.5f) * 1.4f, del = (Rand(seed + 3) - 0.5f) * 0.9f;
                Vector2[] pts = Points(11);
                for (int j = 0; j <= 10; j++)
                {
                    float k = j / 10f;
                    pts[j] = On(az + daz * k + (Rand(seed + 4 + j) - 0.5f) * 0.08f, Mathf.Max(floorEl, Mathf.Min(1.5f, el + del * k + (Rand(seed + 20 + j) - 0.5f) * 0.08f)), r);
                }
                Line(pts, 0.3f + r * 0.03f, Fade(KiIce, 0.35f * alpha), whiteGlow, Overhead + 0.1437f, Taper.Both);
                Line(pts, 0.07f + r * 0.012f, Fade(White, 0.95f * alpha), whiteGlow, Overhead + 0.1438f, Taper.Both);
            }
            // Embers rise off the top and drift up, each 0.8 s.
            for (int i = 0; i < 24; i++)
            {
                float phase = s / 0.8f + Rand(i + 90), u = phase - Mathf.Floor(phase);
                int seed = Mathf.FloorToInt(phase) * 29 + i * 7;
                Vector2 from = On(Rand(seed) * TAU, Mathf.Lerp(0.7f, 1.4f, Rand(seed + 1)), r);
                float size = 0.12f + 0.1f * Rand(seed + 2);
                var ember = new Vector2(from.x + Mathf.Sin(u * 5f + i) * 0.2f, from.y + u * (1.5f + 2f * Rand(seed + 3)));
                if (i % 3 == 0) Glint(ember, size * 2.2f, 0.9f * (1f - u) * alpha, KiIce, 45f);
                else Sprite(ember, size, size, Fade(Color.Lerp(White, KiIce, u), 0.9f * (1f - u) * alpha), whiteGlow, Overhead + 0.1448f, 45f + u * 90f);
            }
            // The white-hot middle, a highlight toward the sun near the top, and the flash of each pulse.
            Sprite(mid, r * 1.25f * beat, r * 1.2f * beat, Fade(White, 0.7f * alpha), glow, Overhead + 0.144f);
            Sprite(mid, r * 0.55f * beat, r * 0.5f * beat, Fade(White, 0.8f * alpha), glow, Overhead + 0.1442f);
            Sprite(On(sunAz, 0.8f, r), r * 0.7f, r * 0.55f, Fade(White, 0.4f * alpha), glow, Overhead + 0.1445f);
            Sprite(mid, r * 2f, r * 1.9f, Fade(White, (0.18f * flash + 0.2f * rush) * alpha), glow, Overhead + 0.1446f);
        }

        /// <summary>A heartbeat's kick at <paramref name="age"/> on the dome's clock: out over 0.05 s, then easing back.</summary>
        private static float Kick(float age) => age < 0f ? 0f : age < 0.05f ? Smooth(age / 0.05f) : Mathf.Exp(-(age - 0.05f) / 0.12f);
    }
}
