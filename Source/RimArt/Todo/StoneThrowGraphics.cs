using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.TodoGraphics;
using S = RimArt.StoneThrow;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Draws the stone throw, the port of the lab's todo-stone-throw.js: the stone in the hand in a
    /// teal flame outline, the blade of light on release, the stone flying in its spin ring with a
    /// streak and a shadow, the skid into its cell with the floor ring and dashes, the charged stone
    /// at rest, and a stone taken back to the hand. The clip and the stand-in pawn are not drawn here.
    ///
    /// Everything is a point on a path or a level shape, so there is one drawing for every facing.
    /// The source's spin ring stands upright; here it is level, so it stays a ring from every side.
    /// Every routine takes times and keeps no state.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class StoneThrowGraphics
    {
        private const int AuraPoints = 16;
        private static readonly Vector2[] auraRing = new Vector2[AuraPoints], auraEdge = new Vector2[AuraPoints];
        private static readonly Vector2[] arcPoints = new Vector2[7], blade = new Vector2[9], streak = new Vector2[9];

        /// <summary>
        /// Todo's cursed energy on an object (the S2 hanger in his hand): a teal flame outline with a
        /// dark edge round it, the tongues long on the upper side and leaning in, flickering.
        /// </summary>
        internal static void Aura(Vector2 pos, float size, float seconds, float alpha, int seed, float altitude)
        {
            if (alpha <= 0.01f || size <= 0.01f) return;
            int step = Mathf.FloorToInt(seconds * S.Flicker + Rand(seed));
            for (int i = 0; i < AuraPoints; i++)
            {
                float ang = i / (float)AuraPoints * Mathf.PI * 2f, up = Mathf.Max(0f, Mathf.Sin(ang)), flick = Rand(seed + i + step * 29);
                bool tongue = i % 2 == 1;
                float r = tongue ? (0.5f + 0.3f * flick) * (1f + 1.4f * up * up) : 0.42f + 0.1f * up;
                float lean = tongue ? -Mathf.Cos(ang) * 0.35f * up : 0f;
                auraRing[i] = new Vector2(pos.x + Mathf.Cos(ang + lean) * size * r, pos.y + Mathf.Sin(ang + lean) * size * r);
                auraEdge[i] = pos + (auraRing[i] - pos) * 1.18f;
            }
            Fan(pos, auraEdge, AuraPoints, Fade(Outline, 0.75f * alpha), null, altitude);
            Fan(pos, auraRing, AuraPoints, Fade(Teal, 0.9f * alpha), whiteGlow, altitude + 0.0005f);
            Sprite(pos, size * 1.1f, size * 1.1f, Fade(TealPale, 0.7f * alpha), glow, altitude + 0.001f);
        }

        /// <summary>
        /// A stone at rest: a faint teal glow that breathes and a glint now and then. <paramref name="charge"/>
        /// brightens it and wraps it in the aura. <paramref name="texture"/> draws the stone itself, which
        /// the game leaves to the item except while it skids in; <paramref name="spin"/> turns it.
        /// </summary>
        internal static void Resting(Vector2 pos, float seconds, float alpha, float charge, int seed, bool texture, float spin = 0f)
        {
            if (alpha <= 0f) return;
            Begin(pos);
            float breath = 0.5f + 0.5f * Mathf.Sin((seconds / S.Breath + Rand(seed)) * Mathf.PI * 2f);
            Sprite(pos, S.StoneRest * 2.4f, S.StoneRest * 2.4f, Fade(Teal, alpha * (0.3f + 0.15f * breath + 0.5f * charge)), glow, Floor + 0.03f);
            if (texture) Sprite(pos, S.StoneRest, S.StoneRest, Fade(White, alpha), stone, ItemLayer, spin);
            if (charge > 0f) Aura(pos, S.StoneRest * 0.8f, seconds, charge, seed + 3, ItemLayer - 0.01f);
            float g = (seconds + Rand(seed + 1) * S.GlintEvery) % S.GlintEvery;
            if (g < 0f || g >= S.GlintLife) return;
            float u = g / S.GlintLife, a = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * alpha;
            var c = new Vector2(pos.x + 0.08f, pos.y + 0.08f);
            Sprite(c, 0.18f, 0.18f, Fade(TealPale, 0.8f * a), glow, Overhead + 0.01f);
            Line(Dash(new Vector2(c.x - 0.12f, c.y), new Vector2(c.x + 0.12f, c.y)), 0.03f, Fade(TealPale, a), whiteGlow, Overhead + 0.011f, Taper.Both);
            Line(Dash(new Vector2(c.x, c.y - 0.09f), new Vector2(c.x, c.y + 0.09f)), 0.03f, Fade(TealPale, a), whiteGlow, Overhead + 0.011f, Taper.Both);
        }

        /// <summary>
        /// The stone in the air or in the hand, in its aura; <paramref name="ring"/> above 0 adds the spin
        /// ring (S2: a glowing ring with a bright rim, a lit inside and two bright arcs running round).
        /// </summary>
        internal static void Flying(Vector2 pos, float size, float spin, float seconds, float auraAlpha, float ring, float ringSize = 1f)
        {
            Begin(pos);
            Aura(pos, size * (ring > 0f ? 0.9f : 1.25f), seconds, auraAlpha * (ring > 0f ? 0.7f : 1f), 40, Overhead + 0.018f);
            if (size > 0f) Sprite(pos, size, size, White, stone, Overhead + 0.021f, spin);
            if (ring <= 0f) return;
            float r = S.SpinRing * ringSize;
            Sprite(pos, r * 2.2f, r * 2.2f, Fade(Teal, 0.18f * ring), glow, Overhead + 0.0215f);
            PaperBombGraphics.RingAt(pos, r * 1.08f, Fade(Outline, 0.6f * ring), Overhead + 0.0216f);
            PaperBombGraphics.RingAt(pos, r, Fade(Teal, 0.9f * ring), Overhead + 0.0217f, false, whiteGlow);
            PaperBombGraphics.RingAt(pos, r * 0.9f, Fade(TealPale, 0.5f * ring), Overhead + 0.0218f, false, whiteGlow);
            for (int k = 0; k < 2; k++)
            {
                for (int i = 0; i <= 6; i++)
                {
                    float a = spin * Mathf.Deg2Rad + k * Mathf.PI + i / 6f * 1.4f;
                    arcPoints[i] = new Vector2(pos.x + Mathf.Cos(a) * r, pos.y + Mathf.Sin(a) * r);
                }
                Line(arcPoints, 0.06f, Fade(White, ring), whiteGlow, Overhead + 0.023f, Taper.Both);
            }
        }

        /// <summary>
        /// A throw from Todo standing at <paramref name="todo"/> to <paramref name="cell"/> (its centre),
        /// <paramref name="seconds"/> after the warmup began, landing at <paramref name="place"/>.
        /// <paramref name="drawResting"/> draws the stone's own glow and texture after it lands, as the
        /// preview needs; in game the item and MapComponent_Anchors take over once it has skidded in, and
        /// the skid itself is drawn here.
        /// </summary>
        internal static void Throw(Vector2 todo, Vector2 cell, float seconds, float place, Vector2 sun, float strength, bool drawResting)
        {
            Vector2 along = cell - todo;
            float distance = along.magnitude;
            Vector2 dir = distance < 1e-4f ? Vector2.right : along / distance, side = new Vector2(-dir.y, dir.x);
            Vector2 touch = cell - dir * S.Skid;
            float high = S.High(distance), flight = Mathf.Max(0.02f, place - S.Release);
            Begin(todo);

            // In the hand: the stone wrapped in its aura, growing until it leaves.
            if (seconds >= S.Charge && seconds < S.Release)
                Flying(S.Hand(todo, cell, seconds, false), S.StoneHand, 0f, seconds, Smooth((seconds - S.Charge) / (S.Release - S.Charge)), 0f);

            // Release (S2): a blade of light sweeps along the arm into the throw.
            Vector2 released = S.Hand(todo, cell, S.Release, false);
            float rb = seconds - S.Release;
            if (rb >= 0f && rb < S.BladeLife)
            {
                float f = 1f - rb / S.BladeLife;
                for (int i = 0; i <= 8; i++)
                {
                    float v = i / 8f, lengthwise = Mathf.Lerp(-0.35f, 0.6f, v), across = 0.15f * (1f - v) * (1f - v);
                    blade[i] = released + dir * lengthwise + side * across;
                }
                Line(blade, 0.32f * f, Fade(Outline, 0.7f * f), null, Overhead + 0.024f, Taper.Both);
                Line(blade, 0.22f * f, Fade(Teal, f), whiteGlow, Overhead + 0.0245f, Taper.Both);
                Line(blade, 0.07f * f, Fade(White, f), whiteGlow, Overhead + 0.025f, Taper.Both);
            }

            // Touched down short of the cell: it slides in, spinning down, dust behind it; the spin
            // ring sinks into the floor ring and teal dashes spring out; then it rests.
            float age = seconds - place;
            if (age >= 0f)
            {
                float slide = Smooth(Mathf.Min(1f, age / S.SkidTime));
                Vector2 pos = Vector2.Lerp(touch, cell, slide);
                float turn = S.Spin * (place - S.Release) + S.Spin * S.SpinDecay * (1f - Mathf.Exp(-age / S.SpinDecay));
                if (drawResting || age < S.LandLife)
                    Resting(pos, seconds, 1f, age < S.LandLife ? 1f - age / S.LandLife : 0f, 11, drawResting || age < S.SkidTime, turn);
                if (age < 0.15f) Flying(pos, 0f, turn, seconds, 0f, 1f - age / 0.15f, 1f - age / 0.15f);
                if (age < S.LandLife)
                {
                    float k = age / S.LandLife, outward = 1f - (1f - k) * (1f - k), f = 1f - Smooth(k);
                    PaperBombGraphics.RingAt(cell, Mathf.Lerp(0.25f, S.Land, outward), Fade(Teal, 0.8f * f), Floor + 0.04f, false, whiteGlow);
                    for (int i = 0; i < 6; i++)
                    {
                        float ang = (i + Rand(i + 30) * 0.6f) / 6f * Mathf.PI * 2f, r = Mathf.Lerp(0.15f, S.Land * 0.9f, outward);
                        var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        Vector2[] pts = Dash(cell + way * r, cell + way * (r + 0.16f));
                        Line(pts, 0.09f, Fade(Outline, 0.7f * f), null, Overhead + 0.03f, Taper.Both);
                        Line(pts, 0.05f, Fade(Teal, f), whiteGlow, Overhead + 0.031f, Taper.Both);
                    }
                    // Dust kicked up along the skid, behind the stone.
                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 d = Vector2.Lerp(touch, cell, i / 4f * slide);
                        float jitter = (Rand(i + 40) - 0.5f) * 0.2f;
                        Sprite(d + side * jitter + new Vector2(0f, 0.05f * outward), 0.2f + 0.2f * outward, 0.16f + 0.14f * outward,
                            Fade(Dust, 0.45f * f), soft, Overhead + 0.005f);
                    }
                }
            }

            // In flight: shadow, the streak behind it, the stone in its aura inside the spin ring.
            float u = (seconds - S.Release) / flight;
            if (u < 0f || u >= 1f) return;
            InFlight(released, touch, u, high, S.HandH, 0f, seconds, seconds - S.Release, sun, strength, Smooth(u / 0.25f),
                Mathf.Lerp(S.StoneHand, S.StoneRest * 0.9f, u));
        }

        /// <summary>
        /// A stone at <paramref name="cell"/> being taken back by Todo at <paramref name="todo"/>,
        /// <paramref name="seconds"/> after the warmup began, lifted at <paramref name="lift"/>.
        /// Before the lift the stone flares and its floor ring pulls in (<see cref="Flare"/>; the preview
        /// draws the stone itself); after it, it pops off in dust, flies back in its ring and is caught.
        /// </summary>
        internal static void TakeBack(Vector2 todo, Vector2 cell, float seconds, float lift, Vector2 sun, float strength, bool drawResting)
        {
            Begin(todo);
            float flare = FlareAmount(seconds, lift);
            if (drawResting && seconds < lift) Resting(cell, seconds, 1f, flare, 11, true);
            if (seconds < lift) FloorPull(cell, seconds, lift);
            float pop = seconds - lift;
            if (pop >= 0f && pop < 0.15f)
            {
                float k = pop / 0.15f;
                for (int i = 0; i < 3; i++)
                    Sprite(new Vector2(cell.x + (i - 1) * 0.12f, cell.y + 0.03f + 0.06f * k), 0.2f + 0.15f * k, 0.15f + 0.1f * k,
                        Fade(Dust, 0.4f * (1f - k)), soft, Overhead + 0.005f);
            }

            // Caught: the aura is drawn into the hand, 6 teal dashes closing on it, and goes out.
            float catchAt = S.CatchTime(lift), ca = seconds - catchAt;
            Vector2 hand = S.Hand(todo, cell, seconds + (S.Place - lift), true);
            if (ca >= 0f && ca < S.Absorb + 0.14f)
            {
                float k = Mathf.Clamp01(ca / S.Absorb), f = 1f - Smooth(k);
                Flying(hand, S.StoneHand * (1f - 0.3f * k), 0f, seconds, f, 0f);
                for (int i = 0; i < 6; i++)
                {
                    float ang = i / 6f * Mathf.PI * 2f + 0.3f, r = Mathf.Lerp(0.45f, 0.06f, k);
                    var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Vector2[] pts = Dash(hand + way * r, hand + way * (r + 0.12f));
                    Line(pts, 0.08f, Fade(Outline, 0.7f * f), null, Overhead + 0.03f, Taper.Both);
                    Line(pts, 0.04f, Fade(Teal, f), whiteGlow, Overhead + 0.031f, Taper.Both);
                }
                float g = Mathf.Clamp01((ca - S.Absorb) / 0.14f);
                if (ca >= S.Absorb) Sprite(hand, 0.45f, 0.45f, Fade(TealPale, 0.8f * (1f - g) * (1f - g)), glow, Overhead + 0.032f);
            }

            float u = (seconds - lift) / S.CatchFlight;
            if (u < 0f || u >= 1f) return;
            float high = S.High((cell - todo).magnitude);
            InFlight(cell, hand, u, high, 0f, S.HandH, seconds, seconds - lift, sun, strength, Smooth(u / 0.3f),
                Mathf.Lerp(S.StoneHand, S.StoneRest * 0.9f, 1f - u));
        }

        /// <summary>How much a stone being taken back flares, 0 to 1, <paramref name="seconds"/> after the warmup began.</summary>
        internal static float FlareAmount(float seconds, float lift) => Smooth((seconds - (lift - S.FlareLead)) / S.FlareLead);

        /// <summary>The floor ring pulling in under a stone about to be taken back.</summary>
        internal static void FloorPull(Vector2 cell, float seconds, float lift)
        {
            float from = lift - S.FlareLead;
            if (seconds < from || seconds >= lift) return;
            float k = (seconds - from) / S.FlareLead;
            PaperBombGraphics.RingAt(cell, Mathf.Lerp(S.Land, 0.2f, Smooth(k)), Fade(Teal, 0.7f * k), Floor + 0.04f, false, whiteGlow);
        }

        // One flight from a to b, u of the way: heights go fromH -> toH with the arc on top. from/to
        // are screen points; a hand's is already lifted, so only the arc's lift is added.
        private static void InFlight(Vector2 a, Vector2 b, float u, float high, float fromH, float toH, float seconds, float flying,
            Vector2 sun, float strength, float ringIn, float size)
        {
            Vector2 pos = FlightPoint(a, b, u, high, fromH, toH, out float h);
            var ground = new Vector2(pos.x, pos.y - h * SixPathsHeight.Lift);
            Sprite(ground + sun * h, 0.22f, 0.12f, Fade(Outline, strength * 0.7f), soft, ShadowLayer);
            // Stone first, so the End taper thins it toward the tail.
            for (int i = 0; i <= 8; i++) streak[i] = FlightPoint(a, b, Mathf.Max(0f, u - S.Streak * i / 8f), high, fromH, toH, out _);
            Line(streak, 0.24f, Fade(Outline, 0.5f), null, Overhead + 0.0175f, Taper.End);
            Line(streak, 0.16f, Fade(Teal, 0.7f), whiteGlow, Overhead + 0.018f, Taper.End);
            Line(streak, 0.05f, Fade(White, 0.85f), whiteGlow, Overhead + 0.019f, Taper.End);
            Flying(pos, size, S.Spin * flying, seconds, 1f, ringIn, 0.6f + 0.4f * ringIn);
        }

        private static Vector2 FlightPoint(Vector2 a, Vector2 b, float k, float high, float fromH, float toH, out float h)
        {
            float baseH = Mathf.Lerp(fromH, toH, k);
            h = baseH + high * Mathf.Max(0f, Mathf.Sin(Mathf.PI * k));
            Vector2 at = Vector2.Lerp(a, b, k);
            return new Vector2(at.x, at.y + (h - baseH) * SixPathsHeight.Lift);
        }
    }
}
