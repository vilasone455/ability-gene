using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using A = RimArt.AmenoyodomiGraphics;
using T = RimArt.RaikoKusariTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>One line of the net at one moment: everything <see cref="RaikoKusariGraphics.Link"/> reads.</summary>
    public struct RaikoLink
    {
        /// <summary>Link k joins corner k and k + 1; the ring's closing link (last corner to corner 0) is the last k. Sets when the run reaches it and its shapes.</summary>
        public int index;
        /// <summary>How many links the net has, failed ones included: the whole net flashes when the run has passed them all.</summary>
        public int links;
        /// <summary>Its two ends now, on the ground: a kunai's ground point, or the Fūma's blade tip nearest the other end (<see cref="RaikoKusariTiming.Joint"/>). Drawn <see cref="AmenoyodomiGraphics.Hold"/> cells up.</summary>
        public Vector2 a, b;
        /// <summary>Its two ends on the ground at the moment it ended; read only once it has.</summary>
        public Vector2 endA, endB;
        /// <summary>Cells per second each end moves while held (heading × flight speed × hold share), so a spark leaves from where the line was when it left. Zero is close enough for a still net.</summary>
        public Vector2 driftA, driftB;
        /// <summary>Longer than the longest link when the run reached it: the bolt gets a third of the way and dies.</summary>
        public bool failed;
        /// <summary>Seconds after the leap it ended (it snapped, was let go, or the net's time ran out), or float.PositiveInfinity while it lives.</summary>
        public float end;
        /// <summary>It ended by a snap or a let-go, not by the net's time running out: a burst at each end.</summary>
        public bool cut;
        /// <summary>The DrawPos of every pawn the line holds now (null for none): the line bends to run through each chest.</summary>
        public List<Vector2> held;
    }

    /// <summary>
    /// Draws Raikō Kusari, the port of the lab's rinnegan-raiko-kusari.js: a Chidori charged in the hand, leaping to
    /// the first held weapon and running down the chain, then one lightning line per link (a soft halo; 2 bolts with
    /// 2-4 big kinks and fine crackle, their width swelling and pinching, a side branch each, redrawn 12 times a
    /// second; bolt 0 carries a white thread; a flash every 0.4-0.7 s; two white-hot pulses; light or a dark trace on
    /// the floor; sparks dropping off it); the whole net's white flash; a caught pawn's burst, the line bent through
    /// its chest, the bolts crawling over it, its white flashes and the scorch that stays; the lines breaking into
    /// falling sparks; sparks on the corners and the Fūma's rim arcs; the charged weapons' trails and the Fūma's ring
    /// of arcs in flight; a mechanoid's EMP crackle. The weapons themselves are AmenoyodomiGraphics'; the stand-in
    /// pawns, the shield bubble and the shooter's tracers are not drawn here.
    ///
    /// Every line joins two weapons at the same height, so the net is a flat shape at one height and turns freely with
    /// the direction: one drawing for every facing. The Fūma's arcs are level circles. Bolts are strips with a width
    /// per point, rebuilt on every redraw from a hash of the redraw step; the halo and the floor light are soft discs
    /// stretched along the line. Sparks are computed from their birth time. Every routine takes ages and places and
    /// keeps no state between frames.
    ///
    /// Arguments: ground points are floor points (x, z), drawn points are what the sketch's at() gives; a pawn is its
    /// DrawPos (its feet are 0.3 cells south). age is seconds since the thing named; clock is the redraw clock
    /// (<see cref="RaikoKusariTiming"/>). Every routine takes the colour; Dark Chidori is the default.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RaikoKusariGraphics
    {
        private struct Look
        {
            public bool dark;
            public Color bolt, halo, floor;
            public float haloA, floorA;
        }

        private static readonly Color White = Color.white;
        /// <summary>Dark Chidori's black: the bolts, the dark trace on the floor, the scorch.</summary>
        public static readonly Color Ink = new Color(0.02f, 0.018f, 0.03f);
        public static readonly Color EmpBlue = new Color(0.55f, 0.82f, 1f);
        // Dark Chidori is black bolts round a white thread; the other two are light.
        private static readonly Look[] Looks =
        {
            new Look { dark = true, bolt = Ink, halo = new Color(0.72f, 0.68f, 0.88f), haloA = 0.14f, floor = Ink, floorA = 0.24f },
            new Look { dark = false, bolt = new Color(0.82f, 0.93f, 1f), halo = new Color(0.22f, 0.5f, 1f), haloA = 0.32f, floor = new Color(0.22f, 0.5f, 1f), floorA = 0.1f },
            new Look { dark = false, bolt = new Color(0.9f, 0.82f, 1f), halo = new Color(0.52f, 0.3f, 0.96f), haloA = 0.32f, floor = new Color(0.52f, 0.3f, 0.96f), floorA = 0.1f },
        };
        private static readonly Look EmpLook = new Look { dark = false, bolt = new Color(0.82f, 0.94f, 1f), halo = EmpBlue, haloA = 0.3f, floor = EmpBlue, floorA = 0.1f };
        private static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();

        // Scratch: control points of a line, bolt 0 (kept for the pulses), the other bolts, small bolts, arcs,
        // widths per point, lengths along a bolt, the kinks of one bolt.
        private static readonly Vector2[] ctrl = new Vector2[34], first = new Vector2[MostPoints], other = new Vector2[MostPoints],
            small = new Vector2[MostPoints], arc = new Vector2[MostPoints], pair = new Vector2[2];
        private static readonly float[] widths = new float[MostPoints], along = new float[MostPoints], ku = new float[6], kv = new float[6];
        private static readonly float[] chestU = new float[32];

        private static Look LookOf(RaikoColour colour) => Looks[(int)colour];
        private static Vector2 Lift(Vector2 ground) => A.Up(ground, A.Hold);
        private static Vector2 North(Vector2 q, float z) => new Vector2(q.x, q.y + z);
        private static Vector2 Between(Vector2 a, Vector2 b, float u) => new Vector2(a.x + (b.x - a.x) * u, a.y + (b.y - a.y) * u);

        // ---- The charge, the leap ----

        /// <summary>
        /// The Chidori charging in the caster's hand: a white core that beats and grows, 6-10 bolts crackling out of
        /// it and 3 up the arm (black on Dark Chidori). age: seconds since the warmup began; drawn from 0 to
        /// <paramref name="warmup"/> + 0.15 s (it fades out after the leap). hand: <see cref="RaikoKusariTiming.Hand"/>;
        /// caster: the caster's DrawPos, where the arm bolts end.
        /// </summary>
        public static void Charge(Vector2 hand, Vector2 caster, float age, float warmup, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            float u = Mathf.Clamp01(age / warmup), a = Mathf.Min(1f, u * 3f) * (1f - Mathf.Clamp01((age - warmup) / T.HandFade));
            if (a <= 0f) return;
            Look look = LookOf(colour);
            Begin(hand);
            int step = T.Step(clock);
            float core = (0.2f + 0.3f * u) * (0.85f + 0.15f * Mathf.Sin(clock * 40f));
            if (look.dark) Sprite(hand, core * 2.4f, core * 2.4f, Fade(Ink, 0.25f * a), soft, Overhead + 0.05f);
            else Sprite(hand, core * 4f, core * 4f, Fade(look.halo, 0.45f * a), glow, Overhead + 0.05f);
            Sprite(hand, core * 1.6f, core * 1.6f, Fade(White, 0.9f * a), glow, Overhead + 0.058f);
            Sprite(hand, core * 0.7f, core * 0.7f, Fade(White, a), glow, Overhead + 0.059f);
            int count = 6 + Mathf.FloorToInt(4f * u + 0.5f);
            for (int i = 0; i < count; i++)
            {
                int sd = step * 53 + i * 11 + 7;
                float ang = Rand(sd) * Mathf.PI * 2f, len = (0.25f + 0.5f * Rand(sd + 1)) * (0.5f + 0.5f * u);
                var tip = new Vector2(hand.x + Mathf.Cos(ang) * len, hand.y + Mathf.Sin(ang) * len * 0.8f);
                int n = BoltPoints(small, 0, hand, tip, 0.07f, sd + 3, 0.07f);
                BoltLine(small, n, 0.04f, a * (0.6f + 0.4f * Rand(sd + 2)), look, Overhead + 0.052f + i * 0.0005f, Taper.End, i % 3 != 0 ? 0f : 0.7f);
            }
            Vector2 feet = T.Feet(caster);
            for (int i = 0; i < 3; i++)
            {
                int sd = step * 71 + i * 19 + 3;
                var tip = new Vector2(feet.x + (Rand(sd) - 0.5f) * 0.3f, feet.y + 0.35f + Rand(sd + 1) * 0.35f);
                int n = BoltPoints(small, 0, hand, tip, 0.05f, sd + 5, 0.06f);
                BoltLine(small, n, 0.028f, a * 0.8f * u, look, PawnLayer + 0.006f + i * 0.0005f, Taper.End);
            }
        }

        /// <summary>
        /// The leap from the hand to the first weapon (ground point <paramref name="firstWeapon"/>): a white-flashing
        /// bolt with a spark at its head for 0.05 s, then the bolt fading over 0.12 s. age: seconds since the leap.
        /// </summary>
        public static void Leap(Vector2 hand, Vector2 firstWeapon, float age, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            float lu = age / T.Leap;
            if (lu < 0f || age >= T.Leap + T.LeapLinger) return;
            Look look = LookOf(colour);
            Begin(hand);
            Vector2 to = Lift(firstWeapon), head = lu < 1f ? Between(hand, to, lu) : to;
            ctrl[0] = hand;
            ctrl[1] = head;
            Lightning(2, clock, look, lu < 1f ? 1f : 1f - (age - T.Leap) / T.LeapLinger, 11, lu < 1f ? 1f : 0f);
            if (lu < 1f) Spark(head, 0.3f, look, 1f);
        }

        // ---- The lines ----

        /// <summary>
        /// One line of the net, age seconds after the leap: from <see cref="RaikoKusariTiming.LinkStart"/> the run
        /// along it (a flashing bolt and a spark at its head, 0.05 s), then the lit line: halo, bolts, branches, white
        /// thread, flashes, two pulses, the floor light and the sparks dropping off it, bent through the chest of every
        /// pawn it holds. After <see cref="RaikoLink.end"/> it vanishes in 0.12 s and falls as sparks, with a burst at
        /// each end if it was cut; all gone <see cref="RaikoKusariTiming.Afterglow"/> after the end. A failed link fizzles
        /// for 0.3 s instead.
        /// </summary>
        public static void Link(in RaikoLink link, float age, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            float start = T.LinkStart(link.index);
            if (age < start) return;
            Look look = LookOf(colour);
            int seed = T.LinkSeed(link.index);
            Vector2 a = Lift(link.a), b = Lift(link.b);
            Begin(link.a);
            if (link.failed)
            {
                Fizzle(a, b, age - start, clock, look);
                return;
            }
            float u = (age - start) / T.PerLink;
            if (u < 1f)
            {
                Vector2 head = Between(a, b, u);
                ctrl[0] = a;
                ctrl[1] = head;
                Lightning(2, clock, look, 1f, seed, 1f);
                Spark(head, 0.26f, look, 1f);
                return;
            }
            float end = link.end, f = 1f - Mathf.Clamp01((age - end) / T.CutOut);
            LineSparks(link, age, seed, look);
            if (f > 0f)
            {
                float surge = T.Surge(link.index, link.links, age, clock, end);
                int count = Chests(link.held, a, b);
                int n = Lightning(count, clock, look, f, seed, surge);
                Pulses(n, clock, seed, look, f);
                LightPool(link.a, link.b, look, f, surge, 0.7f + 0.3f * Rand(T.Step(clock) * 3 + link.index));
            }
            if (age >= end)
            {
                BreakSparks(link.endA, link.endB, age - end, seed, look);
                if (link.cut)
                {
                    Burst(a, age - end, T.CutLife, 0.35f, look, 4, 60 + link.index);
                    Burst(b, age - end, T.CutLife, 0.35f, look, 4, 80 + link.index);
                }
            }
        }

        // The line's control points: its two ends and the chests it runs through, sorted from a to b; only chests
        // between 2 % and 98 % of the way count. Returns the count.
        private static int Chests(List<Vector2> held, Vector2 a, Vector2 b)
        {
            ctrl[0] = a;
            int count = 1;
            if (held != null)
            {
                Vector2 ab = b - a;
                float l2 = ab.sqrMagnitude;
                if (l2 == 0f) l2 = 1f;
                for (int k = 0; k < held.Count && count < ctrl.Length - 1; k++)
                {
                    Vector2 c = T.Chest(held[k]);
                    float u = Vector2.Dot(c - a, ab) / l2;
                    if (u <= 0.02f || u >= 0.98f) continue;
                    int at = count;
                    while (at > 1 && chestU[at - 1] > u)
                    {
                        ctrl[at] = ctrl[at - 1];
                        chestU[at] = chestU[at - 1];
                        at--;
                    }
                    ctrl[at] = c;
                    chestU[at] = u;
                    count++;
                }
            }
            ctrl[count++] = b;
            return count;
        }

        /// <summary>
        /// The burst when the run reaches a weapon (ground point), age seconds after: 0.15 s and 5 bolts, or 0.24 s and
        /// 7 bolts for a ring's closing weapon. n: 0 for the first weapon, then 1, 2... for each link that formed, in
        /// order. The first weapon is reached at <see cref="RaikoKusariTiming.Leap"/>, link k's second weapon at
        /// <see cref="RaikoKusariTiming.LinkLit"/>(k).
        /// </summary>
        public static void Reached(Vector2 weapon, float age, bool ring, int n, RaikoColour colour = RaikoColour.DarkChidori)
        {
            Begin(weapon);
            Burst(Lift(weapon), age, T.CornerFlash * (ring ? T.RingFlash : 1f), ring ? 0.6f : 0.38f, LookOf(colour), ring ? 7 : 5, 140 + n * 9);
        }

        /// <summary>
        /// The pool under a weapon while a live line holds it (ground point): a flickering coloured light, or on Dark
        /// Chidori white light during a flash only. surge: the most <see cref="RaikoKusariTiming.Surge"/> of its live
        /// lines. corner: its place in the net.
        /// </summary>
        public static void CornerPool(Vector2 weapon, int corner, float surge, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            Look look = LookOf(colour);
            float flick = 0.7f + 0.3f * Rand(T.Step(clock) * 5 + corner);
            if (!look.dark) Sprite(weapon, 1.3f, 1.3f, Fade(look.floor, 0.1f + 0.06f * flick + 0.2f * surge), glow, Floor + 0.016f);
            else if (surge > 0f) Sprite(weapon, 1.2f, 1.2f, Fade(White, 0.14f * surge), glow, Floor + 0.016f);
        }

        /// <summary>
        /// The spark where a live line meets a weapon: on a kunai its ground point, on the Fūma each live line's joint.
        /// It beats and grows with the surge.
        /// </summary>
        public static void CornerSpark(Vector2 joint, int corner, float surge, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            Spark(Lift(joint), 0.2f * (0.75f + 0.25f * Mathf.Sin(clock * 37f + corner * 2f)) * (1f + 0.8f * surge), LookOf(colour), 0.9f);
        }

        /// <summary>
        /// The Fūma as a corner (ground point, turned <paramref name="turn"/> degrees clockwise): arcs jump round the
        /// rim from each blade tip toward the next. Each shows on about 60 % of redraws (all of them during a flash)
        /// and covers 55-90 of the 90 degrees, so they never close into a ring.
        /// </summary>
        public static void FumaArcs(Vector2 fuma, float turn, float surge, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            Look look = LookOf(colour);
            Begin(fuma);
            int step = T.Step(clock);
            for (int k = 0; k < 4; k++)
            {
                int sd = step * 37 + k * 5;
                if (Rand(sd + 9) > 0.6f && surge < 0.3f) continue;
                float a0 = T.BladeAngle(turn, k, T.TipR);
                int n = ArcPoints(fuma, T.TipR * A.FumaSize, a0, a0 + Mathf.PI / 2f * (0.6f + 0.4f * Rand(sd + 1)), 0.1f, sd);
                Widths(n - 1, 0.032f * (1f + 0.8f * surge), sd);
                BoltStroke(arc, n, 0.85f, look, Overhead + 0.031f + k * 0.0003f, k != 0 ? 0.8f * surge : 0.5f + 0.5f * surge);
            }
        }

        // ---- Flying on charged, after a let-go ----

        /// <summary>A charged kunai in flight (ground point, heading degrees): a jagged tail 0.8 cells long and a spark on it. corner: its place in the net.</summary>
        public static void FlyingKunai(Vector2 kunai, float deg, int corner, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            Look look = LookOf(colour);
            Begin(kunai);
            Vector2 tail = Lift(kunai - Turn(deg) * 0.8f), head = Lift(kunai);
            int n = BoltPoints(small, 0, tail, head, 0.07f, T.Step(clock) * 29 + corner * 3, 0.13f);
            BoltLine(small, n, 0.035f, 0.9f, look, Overhead + 0.03f, Taper.Both, 0.6f);
            Spark(head, 0.16f, look, 0.8f);
        }

        /// <summary>
        /// The charged Fūma in flight (ground point, heading degrees, turn degrees clockwise): three broken arcs of
        /// lightning spinning round it with its turn (never a closed ring) and a jagged tail 1.1 cells long.
        /// </summary>
        public static void FlyingFuma(Vector2 fuma, float deg, float turn, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            Look look = LookOf(colour);
            Begin(fuma);
            int step = T.Step(clock);
            for (int k = 0; k < 3; k++)
            {
                int sd = step * 37 + k * 5;
                float a0 = -turn * Mathf.Deg2Rad + k * 2.094f + Rand(sd) * 0.5f, span = 0.9f + 0.5f * Rand(sd + 1);
                int n = ArcPoints(fuma, (0.46f + 0.1f * Rand(sd + 2)) * A.FumaSize, a0, a0 + span, 0.1f, sd + 3);
                Widths(n - 1, 0.04f, sd);
                BoltStroke(arc, n, 0.95f, look, Overhead + 0.031f + k * 0.0003f, k != 0 ? 0f : 0.7f);
            }
            int m = BoltPoints(small, 0, Lift(fuma - Turn(deg) * 1.1f), Lift(fuma), 0.1f, step * 43, 0.15f);
            BoltLine(small, m, 0.04f, 0.85f, look, Overhead + 0.03f, Taper.Both, 0.6f);
        }

        /// <summary>The burst where the charged Fūma lands (ground point), age seconds after: 0.5 s, 6 bolts.</summary>
        public static void FumaLands(Vector2 fuma, float age, RaikoColour colour = RaikoColour.DarkChidori)
        {
            Begin(fuma);
            Burst(North(fuma, 0.1f), age, T.FumaLandLife, 0.7f, LookOf(colour), 6, 500);
        }

        // ---- Pawns ----

        /// <summary>
        /// A caught pawn (its DrawPos, shaken by <see cref="RaikoKusariTiming.Shake"/> if the game shakes it): a burst
        /// where it touched (age seconds after the catch, 0.16 s), then 3 bolts crawling over its body and a white flash
        /// about 3 times a second, fading out over 0.3 s after the net ends (sinceEnd: seconds since the net ended,
        /// negative while it holds). pawn: any number fixed per pawn (its seeds).
        /// </summary>
        public static void Caught(Vector2 pawnPos, float age, float sinceEnd, int pawn, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            if (age < 0f) return;
            Look look = LookOf(colour);
            Vector2 q = T.Feet(pawnPos);
            Begin(q);
            Burst(North(q, 0.4f), age, T.CatchLife, 0.5f, look, 6, 200 + pawn * 11);
            Crackle(q, clock, look, 1f - Mathf.Clamp01(sinceEnd / T.FadeOut), 3, 1f, 300 + pawn * 17);
        }

        /// <summary>
        /// The scorch under a caught pawn: a dark oval at its feet (DrawPos where it was caught) that grows over 1.5 s
        /// held and stays. heldFor: seconds it has been held (it stops growing when the net ends).
        /// </summary>
        public static void Scorch(Vector2 pawnPos, float heldFor, float alpha = 1f)
        {
            if (heldFor < 0f) return;
            Sprite(North(T.Feet(pawnPos), 0.02f), 0.78f, 0.46f, Fade(Ink, 0.34f * Smooth(heldFor / T.ScorchGrow) * alpha), soft, Floor + 0.012f);
        }

        /// <summary>
        /// A pawn struck by a charged kunai or cut by the charged Fūma (its DrawPos), age seconds after: a burst (0.18 s)
        /// and 2 bolts crawling over it for the stun (seconds, fading over its last 0.3 s). hit: which strike on this
        /// pawn, 0 for the first.
        /// </summary>
        public static void Struck(Vector2 pawnPos, float age, float stun, int pawn, int hit, float clock, RaikoColour colour = RaikoColour.DarkChidori)
        {
            if (age < 0f || age >= stun) return;
            Look look = LookOf(colour);
            Vector2 q = T.Feet(pawnPos);
            Begin(q);
            Burst(North(q, 0.4f), age, T.StruckLife, 0.45f, look, 5, 400 + pawn * 13 + hit);
            Crackle(q, clock, look, 0.75f * (1f - Mathf.Clamp01((age - stun + 0.3f) / 0.3f)), 2, 0.9f, 500 + pawn * 19 + hit);
        }

        /// <summary>A mechanoid still stunned after the net (its DrawPos), age seconds after the net ended: blue crackle and a ring opening round it every 0.625 s.</summary>
        public static void Emp(Vector2 pawnPos, float age, float clock)
        {
            if (age < 0f) return;
            Vector2 q = T.Feet(pawnPos);
            Begin(q);
            Crackle(q, clock, EmpLook, 0.7f, 2, 1f, 900);
            float u = age * 1.6f % 1f;
            Circle(North(q, 0.32f), 0.25f + 0.35f * u, 0.6f * (1f - u), Overhead + 0.02f, EmpBlue);
        }

        // ---- Pieces ----

        /// <summary>
        /// A jagged bolt from a to b written into <paramref name="into"/> from <paramref name="at"/>: 2-4 big kinks it
        /// meanders through with fine crackle on top, both zero at the ends and smaller on a bolt under 0.8 cells. One
        /// point every <paramref name="seg"/> cells, 4 segments at least. Returns the points written.
        /// </summary>
        private static int BoltPoints(Vector2[] into, int at, Vector2 a, Vector2 b, float jag, int seed, float seg = 0.15f)
        {
            float dx = b.x - a.x, dz = b.y - a.y, len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len == 0f) len = 1e-6f;
            int n = Mathf.Min(Mathf.Max(4, Mathf.FloorToInt(len / seg + 0.5f)), into.Length - 1 - at);
            float nx = -dz / len, nz = dx / len, j = jag * Mathf.Min(1f, len / 0.8f);
            int kinks = 2 + Mathf.FloorToInt(Rand(seed + 1) * 3f), last = kinks + 1;
            ku[0] = kv[0] = 0f;
            for (int k = 1; k <= kinks; k++)
            {
                ku[k] = (k - 0.5f + (Rand(seed + 10 + k) - 0.5f) * 0.6f) / kinks;
                kv[k] = (Rand(seed + 20 + k) - 0.5f) * 3f * j;
            }
            ku[last] = 1f;
            kv[last] = 0f;
            for (int i = 0, k = 1; i <= n; i++)
            {
                float u = i / (float)n;
                while (k < last && u > ku[k]) k++;
                float meander = kv[k - 1] + (kv[k] - kv[k - 1]) * Mathf.Clamp01((u - ku[k - 1]) / Mathf.Max(1e-6f, ku[k] - ku[k - 1]));
                float off = i > 0 && i < n ? meander + (Rand(seed + i * 7) - 0.5f) * 0.7f * j : 0f;
                into[at + i] = new Vector2(a.x + dx * u + nx * off, a.y + dz * u + nz * off);
            }
            return n + 1;
        }

        /// <summary>Width along a bolt of n segments into <see cref="widths"/>: swells and pinches between 0.5 and 1.6 times w through three random control values, half at the ends.</summary>
        private static void Widths(int n, float w, int seed)
        {
            float c1 = 0.5f + 1.1f * Rand(seed + 41), c2 = 0.5f + 1.1f * Rand(seed + 42), c3 = 0.5f + 1.1f * Rand(seed + 43);
            for (int i = 0; i <= n; i++)
            {
                float f = i / (float)n * 4f;
                int k = Mathf.Min(3, Mathf.FloorToInt(f));
                float e = (1f - Mathf.Cos((f - k) * Mathf.PI)) / 2f;
                float from = k == 0 ? 0.5f : k == 1 ? c1 : k == 2 ? c2 : c3, to = k == 0 ? c1 : k == 1 ? c2 : k == 2 ? c3 : 0.5f;
                widths[i] = w * (from + (to - from) * e);
            }
        }

        /// <summary>A strip through the first <paramref name="count"/> points, <see cref="widths"/> × scale wide at each.</summary>
        private static void Stroke(Vector2[] pts, int count, float scale, Color colour, Material material, float altitude)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            Sides(count, out Vector2[] a, out Vector2[] b);
            int last = count - 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = pts[Mathf.Min(last, i + 1)] - pts[Mathf.Max(0, i - 1)];
                float len = d.magnitude, w = widths[i] * scale / 2f + 0.003f;
                if (len == 0f) len = 1f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, material, altitude);
        }

        /// <summary>A line through the first <paramref name="count"/> points that tapers as GokuGraphics.Line does (the lab's goku.js line).</summary>
        private static void Line(Vector2[] pts, int count, float width, Color colour, Material material, float altitude, Taper taper)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            Sides(count, out Vector2[] a, out Vector2[] b);
            int last = count - 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = pts[Mathf.Min(last, i + 1)] - pts[Mathf.Max(0, i - 1)];
                float len = d.magnitude, u = i / (float)last;
                if (len == 0f) len = 1f;
                float w = width / 2f * (taper == Taper.Both ? Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) : taper == Taper.End ? Mathf.Pow(Mathf.Max(0f, 1f - u), 0.6f) : 1f) + 0.004f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, material ?? solid, altitude);
        }

        /// <summary>
        /// One bolt with a width per point (<see cref="widths"/>). Dark Chidori: black, with a white thread (core 0 to
        /// 1; at 1 it fills most of the bolt, a flash frame). Light looks: a coloured halo strip, a pale core and the
        /// white thread.
        /// </summary>
        private static void BoltStroke(Vector2[] pts, int count, float alpha, Look look, float altitude, float core)
        {
            if (alpha <= 0.01f || count < 2) return;
            if (look.dark)
            {
                Stroke(pts, count, 1f, Fade(look.bolt, Mathf.Min(1f, alpha)), solid, altitude);
                if (core > 0f) Stroke(pts, count, 0.3f + 0.45f * core, Fade(White, Mathf.Min(1f, alpha * core)), whiteGlow, altitude + 0.0002f);
                return;
            }
            Stroke(pts, count, 2f, Fade(look.halo, alpha * 0.45f), whiteGlow, altitude);
            Stroke(pts, count, 0.7f, Fade(look.bolt, Mathf.Min(1f, alpha)), whiteGlow, altitude + 0.0002f);
            if (core > 0f) Stroke(pts, count, 0.3f + 0.25f * core, Fade(White, Mathf.Min(1f, alpha * core)), whiteGlow, altitude + 0.0003f);
        }

        /// <summary>A small bolt of even width that tapers: hand crackle, branches, bursts, body crawl, trails.</summary>
        private static void BoltLine(Vector2[] pts, int count, float width, float alpha, Look look, float altitude, Taper taper, float core = 0f)
        {
            if (alpha <= 0.01f || count < 2) return;
            if (look.dark)
            {
                Line(pts, count, width, Fade(look.bolt, Mathf.Min(1f, alpha)), solid, altitude, taper);
                if (core > 0f) Line(pts, count, width * 0.34f, Fade(White, Mathf.Min(1f, alpha * core)), whiteGlow, altitude + 0.0002f, taper);
                return;
            }
            Line(pts, count, width * 2f, Fade(look.halo, alpha * 0.45f), whiteGlow, altitude, taper);
            Line(pts, count, width * 0.7f, Fade(look.bolt, Mathf.Min(1f, alpha)), whiteGlow, altitude + 0.0002f, taper);
            if (core > 0f) Line(pts, count, width * 0.3f, Fade(White, Mathf.Min(1f, alpha * core)), whiteGlow, altitude + 0.0003f, taper);
        }

        /// <summary>
        /// A lit line through the first <paramref name="count"/> points of <see cref="ctrl"/> (drawn points: its two ends
        /// and any chest it runs through): a soft halo in pieces of 1.1 cells at most, 2 bolts (3 during a flash) with a
        /// side branch each, redrawn 12 times a second; a bolt after the first is left out on 15 % of redraws. Bolt 0
        /// carries the white thread. surge 0 to 1 flares the lot. Leaves bolt 0 in <see cref="first"/> and returns its
        /// point count (0 when nothing was drawn).
        /// </summary>
        private static int Lightning(int count, float clock, Look look, float alpha, int seed, float surge)
        {
            if (alpha <= 0.01f) return 0;
            int step = T.Step(clock);
            for (int m = 0; m + 1 < count; m++)
            {
                Vector2 a = ctrl[m], d = ctrl[m + 1] - a;
                float len = d.magnitude;
                if (len < 0.03f) continue;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                int k = Mathf.Max(1, Mathf.CeilToInt(len / 1.1f));
                for (int i = 0; i < k; i++)
                {
                    float u = (i + 0.5f) / k;
                    Sprite(a + d * u, len / k * 1.9f, T.GlowWidth * (1f + 0.6f * surge) * (0.8f + 0.4f * Rand(step * 7 + i + m * 31 + seed)),
                        Fade(look.halo, Mathf.Min(1f, look.haloA * (1f + 1.5f * surge)) * alpha), glow, Overhead + 0.02f, -ang);
                }
            }
            int firstCount = 0, bolts = T.Bolts + (surge > 0.3f ? 1 : 0);
            for (int j = 0; j < bolts; j++)
            {
                int sd = seed * 7 + step * 131 + j * 17;
                if (j > 0 && surge < 0.3f && Rand(sd + 999) < 0.15f) continue;
                float flick = 0.65f + 0.35f * Rand(sd + 5), jag = T.Jag * (j != 0 ? 1.3f : 1f);
                Vector2[] pts = j == 0 ? first : other;
                int n = 0;
                for (int m = 0; m + 1 < count; m++) n = (m == 0 ? 0 : n - 1) + BoltPoints(pts, m == 0 ? 0 : n - 1, ctrl[m], ctrl[m + 1], jag, sd + m * 101);
                if (j == 0) firstCount = n;
                Widths(n - 1, (j != 0 ? 0.035f : 0.05f) * (1f + 0.8f * surge), sd);
                BoltStroke(pts, n, alpha * Mathf.Max(flick, surge), look, Overhead + 0.03f + j * 0.002f, j != 0 ? 0.8f * surge : 0.65f + 0.35f * surge);
                int bi = 1 + Mathf.FloorToInt(Rand(sd + 11) * (n - 2));
                Vector2 q0 = pts[bi], q1 = pts[Mathf.Min(n - 1, bi + 1)];
                float dir = Mathf.Atan2(q1.y - q0.y, q1.x - q0.x) + (Rand(sd + 12) < 0.5f ? -1f : 1f) * (0.5f + 0.6f * Rand(sd + 13));
                float bl = 0.14f + 0.3f * Rand(sd + 14);
                int c = BoltPoints(small, 0, q0, new Vector2(q0.x + Mathf.Cos(dir) * bl, q0.y + Mathf.Sin(dir) * bl), 0.04f, sd + 21, 0.07f);
                BoltLine(small, c, 0.03f, alpha * flick * 0.8f, look, Overhead + 0.029f + j * 0.002f, Taper.End);
            }
            return firstCount;
        }

        /// <summary>Two white-hot pulses running along bolt 0 (<see cref="first"/>) at 7 cells/s, one each way, dim at the weapons.</summary>
        private static void Pulses(int count, float clock, int seed, Look look, float alpha)
        {
            if (count < 2 || alpha <= 0.01f) return;
            along[0] = 0f;
            for (int i = 1; i < count; i++) along[i] = along[i - 1] + (first[i] - first[i - 1]).magnitude;
            float total = along[count - 1];
            if (total < 0.3f) return;
            for (int k = 0; k < 2; k++)
            {
                float f = (clock * T.PulseSpeed / total + Rand(seed + k * 3)) % 1f;
                if (k != 0) f = 1f - f;
                float d = f * total;
                int i = 1;
                while (i < count - 1 && along[i] < d) i++;
                float t = (d - along[i - 1]) / Mathf.Max(1e-6f, along[i] - along[i - 1]);
                Vector2 q = Between(first[i - 1], first[i], t);
                float a = alpha * Mathf.Min(1f, Mathf.Sin(f * Mathf.PI) * 3f);
                Sprite(q, 0.42f, 0.42f, look.dark ? Fade(White, 0.22f * a) : Fade(look.halo, 0.5f * a), glow, Overhead + 0.046f);
                Sprite(q, 0.2f, 0.2f, Fade(White, 0.95f * a), glow, Overhead + 0.047f);
            }
        }

        /// <summary>The floor under a line (ground ends): a dark trace (Dark Chidori) or a flickering coloured light, and white light during a flash.</summary>
        private static void LightPool(Vector2 from, Vector2 to, Look look, float alpha, float surge, float flick)
        {
            Vector2 d = to - from;
            float len = d.magnitude, ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            int k = Mathf.Max(1, Mathf.CeilToInt(len / 1.2f));
            for (int i = 0; i < k; i++)
            {
                Vector2 c = from + d * ((i + 0.5f) / k);
                float l = len / k * 1.9f;
                if (look.dark) Sprite(c, l, 0.6f, Fade(Ink, look.floorA * alpha), soft, Floor + 0.014f, -ang);
                else Sprite(c, l, 0.95f, Fade(look.floor, (0.1f + 0.08f * flick) * alpha), glow, Floor + 0.014f, -ang);
                if (surge > 0f)
                    Sprite(c, l, 1.2f, Fade(look.dark ? White : look.floor, (look.dark ? 0.16f : 0.3f) * surge * alpha), glow, Floor + 0.015f, -ang);
            }
        }

        /// <summary>
        /// One spark: falls from the net's height under 6 cells/s² with a sideways drift (v cells/s), a short tail
        /// behind it, and a small flash where it lands. from: where it left, on the ground.
        /// </summary>
        private static void FallingSpark(Vector2 from, float vx, float vz, float age, Look look)
        {
            float fall = T.SparkFall;
            if (age < 0f || age >= fall + 0.1f) return;
            if (age < fall)
            {
                var pos = new Vector2(from.x + vx * age, from.y + vz * age + (A.Hold - 0.5f * T.Gravity * age * age) * SixPathsHeight.Lift);
                float sx = vx, sz = vz - T.Gravity * age * SixPathsHeight.Lift, sl = Mathf.Sqrt(sx * sx + sz * sz);
                if (sl == 0f) sl = 1f;
                pair[0] = new Vector2(pos.x - sx / sl * 0.14f, pos.y - sz / sl * 0.14f);
                pair[1] = pos;
                Line(pair, 2, 0.03f, Fade(look.bolt, 0.9f), look.dark ? solid : whiteGlow, Overhead + 0.043f, Taper.None);
                Sprite(pos, 0.08f, 0.08f, White, glow, Overhead + 0.045f);
                return;
            }
            float u = (age - fall) / 0.1f;
            Sprite(new Vector2(from.x + vx * fall, from.y + vz * fall), 0.18f * (1f - u), 0.12f * (1f - u), Fade(White, 0.8f * (1f - u)), glow, Floor + 0.02f);
        }

        // Sparks spitting off a live line, 3 a second, each from where the line was when it left: the ends at the
        // line's reference time (now, or its end) moved back by their drift.
        private static void LineSparks(in RaikoLink link, float age, int seed, Look look)
        {
            float fall = T.SparkFall, lit = T.LinkLit(link.index), until = Mathf.Min(age, link.end);
            bool ended = age >= link.end;
            Vector2 refA = ended ? link.endA : link.a, refB = ended ? link.endB : link.b;
            int firstK = Mathf.Max(0, Mathf.FloorToInt((age - fall - 0.1f - lit) * T.SparkRate)), lastK = Mathf.FloorToInt((until - lit) * T.SparkRate);
            for (int k = firstK; k <= lastK; k++)
            {
                int sd = seed * 13 + k * 17;
                float born = lit + (k + Rand(sd)) / T.SparkRate;
                if (born > age || born >= link.end) continue;
                float back = until - born, u = Rand(sd + 1);
                Vector2 from = Between(refA - link.driftA * back, refB - link.driftB * back, u);
                FallingSpark(from, (Rand(sd + 2) - 0.5f) * 1.2f, (Rand(sd + 3) - 0.5f) * 1.2f, age - born, look);
            }
        }

        // When a line ends it breaks and falls as sparks, 3 per cell of its length (ground ends at the end).
        private static void BreakSparks(Vector2 from, Vector2 to, float age, int seed, Look look)
        {
            if (age < 0f || age > T.BreakLife) return;
            int n = Mathf.CeilToInt((to - from).magnitude * T.BreakPerCell);
            for (int k = 0; k < n; k++)
            {
                int sd = seed * 29 + 500 + k * 13;
                float u = (k + Rand(sd + 1)) / n;
                FallingSpark(Between(from, to, u), (Rand(sd + 2) - 0.5f) * 1.6f, (Rand(sd + 3) - 0.5f) * 1.6f, age - Rand(sd) * 0.05f, look);
            }
        }

        /// <summary>Points into <see cref="arc"/> round the circle of radius R about c (ground) from a0 to a1 radians, jittered in and out, lifted to the net's height. Returns the count.</summary>
        private static int ArcPoints(Vector2 c, float R, float a0, float a1, float jag, int seed)
        {
            int n = Mathf.Min(arc.Length - 1, Mathf.Max(6, Mathf.FloorToInt(Mathf.Abs(a1 - a0) * R / 0.1f + 0.5f)));
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, a = a0 + (a1 - a0) * u;
                float r = R + (i > 0 && i < n ? (Rand(seed + i * 7) - 0.5f) * 2f * jag * Mathf.Sin(u * Mathf.PI) : 0f);
                arc[i] = Lift(new Vector2(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r));
            }
            return n + 1;
        }

        /// <summary>A flash (drawn point): dark on Dark Chidori, the halo colour otherwise, round a white core.</summary>
        private static void Spark(Vector2 pos, float size, Look look, float alpha)
        {
            if (alpha <= 0f) return;
            if (look.dark) Sprite(pos, size * 1.6f, size * 1.6f, Fade(Ink, 0.3f * alpha), soft, Overhead + 0.044f);
            else Sprite(pos, size * 2.4f, size * 2.4f, Fade(look.halo, 0.4f * alpha), glow, Overhead + 0.044f);
            Sprite(pos, size, size, Fade(White, alpha), glow, Overhead + 0.046f);
        }

        /// <summary>A flash with <paramref name="count"/> short bolts thrown out of it over <paramref name="life"/> seconds: a weapon reached, a pawn caught or struck, a line cut.</summary>
        private static void Burst(Vector2 pos, float age, float life, float size, Look look, int count, int seed)
        {
            if (age < 0f || age >= life) return;
            float u = age / life, f = 1f - u;
            Spark(pos, size * (1f - 0.5f * u), look, f);
            for (int i = 0; i < count; i++)
            {
                int sd = seed + i * 13;
                float ang = (i + Rand(sd)) / count * Mathf.PI * 2f, len = size * (0.6f + 0.8f * Rand(sd + 1)) * (0.5f + 0.5f * Smooth(u * 3f));
                int n = BoltPoints(small, 0, pos, new Vector2(pos.x + Mathf.Cos(ang) * len, pos.y + Mathf.Sin(ang) * len), 0.05f, sd + 3, 0.07f);
                BoltLine(small, n, 0.03f, f, look, Overhead + 0.05f + i * 0.0003f, Taper.End);
            }
        }

        // A link too long when the run reached it: the bolt gets a third of the way and dies (drawn ends).
        private static void Fizzle(Vector2 a, Vector2 b, float age, float clock, Look look)
        {
            if (age < 0f || age > T.FizzleLife) return;
            float u = age / T.FizzleLife;
            Vector2 head = Between(a, b, 0.35f * Smooth(u * 2f));
            int n = BoltPoints(small, 0, a, head, 0.08f, 77 + T.Step(clock) * 13);
            BoltLine(small, n, 0.035f, 1f - u, look, Overhead + 0.03f, Taper.End, 0.5f);
            Burst(head, age, T.FizzleLife, 0.25f, look, 4, 31);
        }

        /// <summary>
        /// Bolts crawling over a body (q: its feet; the body spans 0 to 0.75 up the screen), redrawn 12 times a second,
        /// and a white flash about 3 times a second: the muscles lock.
        /// </summary>
        private static void Crackle(Vector2 q, float clock, Look look, float alpha, int count, float size, int seed)
        {
            if (alpha <= 0.01f) return;
            int step = T.Step(clock);
            for (int i = 0; i < count; i++)
            {
                int sd = seed + step * 97 + i * 13;
                var a = new Vector2(q.x + (Rand(sd) - 0.5f) * 0.34f * size, q.y + (0.02f + Rand(sd + 1) * 0.62f) * size);
                var b = new Vector2(q.x + (Rand(sd + 2) - 0.5f) * 0.42f * size, q.y + (0.08f + Rand(sd + 3) * 0.7f) * size);
                int n = BoltPoints(small, 0, a, b, 0.06f * size, sd + 5, 0.07f);
                BoltLine(small, n, 0.032f * size, alpha * (0.6f + 0.4f * Rand(sd + 4)), look, Overhead + 0.035f + i * 0.0004f, Taper.Both, i != 0 ? 0f : 0.6f);
            }
            float ph = (clock * 3.3f + seed * 0.137f) % 1f;
            if (ph < 0.2f) Sprite(North(q, 0.36f), 0.5f, 0.92f, Fade(White, 0.4f * alpha * (1f - ph / 0.2f)), glow, Overhead + 0.034f);
        }
    }
}
