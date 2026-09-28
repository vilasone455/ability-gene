using System.Collections.Generic;
using UnityEngine;
using static RimArt.VfxMath;
using A = RimArt.AmenoyodomiGraphics;

namespace RimArt
{
    /// <summary>Raikō Kusari's three looks, the sketch's Colour dropdown. Dark Chidori is the default (the user's pick).</summary>
    public enum RaikoColour { DarkChidori, BlueWhite, Violet }

    /// <summary>
    /// Times and sizes of the Raikō Kusari picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/rinnegan-raiko-kusari.js). Seconds in, numbers out, no drawing.
    ///
    /// Two clocks. An age is seconds since something happened; the net's ages count from the leap, which is
    /// the fire tick at the end of the 0.6 s warmup. The redraw clock ("clock") is the net's seconds since
    /// the charge began (the sketch's s): it only picks the redraw step (12 a second), the flash rhythm of
    /// each line and where the pulses are. Any steady clock works in the game; the preview passes the
    /// sketch's so the recording matches it frame for frame.
    ///
    /// The net's rule numbers (8 s, 6-cell links, 3 burn damage a second, the stuns) are balance and live
    /// in the ability's XML; the preview's copies are in <see cref="RaikoKusariScene"/>.
    /// </summary>
    public static class RaikoKusariTiming
    {
        /// <summary>The charge: the sketch's warmup, the AbilityDef's warmupTime. The white core grows over it.</summary>
        public const float Charge = 0.6f;
        /// <summary>The charge in the hand fades out over this long after the leap.</summary>
        public const float HandFade = 0.15f;
        /// <summary>The Chidori leaps from the hand to the first weapon in Leap, and the leap bolt lingers LeapLinger after.</summary>
        public const float Leap = 0.05f, LeapLinger = 0.12f;
        /// <summary>The run down the chain: each link takes this long to light.</summary>
        public const float PerLink = 0.05f;
        /// <summary>A weapon reached flashes this long (a ring's closing weapon RingFlash times as long); the whole net flashes white CloseFlash when the last link lights.</summary>
        public const float CornerFlash = 0.15f, RingFlash = 1.6f, CloseFlash = 0.06f;
        /// <summary>An ended line vanishes over CutOut; a caught pawn's crackle over FadeOut after the net ends.</summary>
        public const float CutOut = 0.12f, FadeOut = 0.3f;
        /// <summary>A line's own flash: every 0.4-0.7 s (fixed per line), SurgeTime long.</summary>
        public const float SurgeTime = 0.07f, SurgeEvery = 0.4f, SurgeSpread = 0.3f;
        /// <summary>Two white-hot pulses run along each line at this speed, cells per second, one each way.</summary>
        public const float PulseSpeed = 7f;
        /// <summary>Sparks: SparkRate a second drop off each line; a line that ends breaks into BreakPerCell per cell of its length. They fall under Gravity cells/s² from <see cref="AmenoyodomiGraphics.Hold"/> up.</summary>
        public const float SparkRate = 3f, BreakPerCell = 3f, Gravity = 6f;
        /// <summary>The break sparks leave over 0.05 s and are gone this long after the line ends.</summary>
        public const float BreakLife = 0.7f;
        /// <summary>The look sliders: bolts per line, redraws per second, jag (cells), halo width (cells).</summary>
        public const int Bolts = 2;
        public const float Boil = 12f, Jag = 0.14f, GlowWidth = 0.35f;
        /// <summary>A body the current runs through shakes up to this far on every redraw: held by the net, or stunned after it (EMP, a charged hit).</summary>
        public const float ShakeHeld = 0.02f, ShakeHit = 0.012f;
        /// <summary>The camera shake at the leap.</summary>
        public const float CameraShake = 0.03f;
        /// <summary>A Fūma blade tip, in texture widths from its middle: where a line joins the Fūma.</summary>
        public const float TipR = 0.55f;
        /// <summary>The scorch under a caught pawn grows to full over this long.</summary>
        public const float ScorchGrow = 1.5f;
        /// <summary>How long each one-off burst lasts: a pawn caught, a pawn struck by a charged weapon, a line cut at its ends, the Fūma landing.</summary>
        public const float CatchLife = 0.16f, StruckLife = 0.18f, CutLife = 0.22f, FumaLandLife = 0.5f, FizzleLife = 0.3f;
        /// <summary>The sketches draw a pawn from its feet, this far south of the pawn's DrawPos (VergilKit.FeetBelowDrawPos).</summary>
        public const float FeetBelowDrawPos = 0.3f;

        /// <summary>Seconds a spark takes to fall from the net's height to the floor, 0.43 s.</summary>
        public static float SparkFall => Mathf.Sqrt(2f * A.Hold / Gravity);

        /// <summary>A line still draws (its break sparks) this long after it ended.</summary>
        public static float Afterglow => BreakLife + 0.05f;

        /// <summary>When the run reaches link k and starts along it, seconds after the leap. The ring's closing link is the last k.</summary>
        public static float LinkStart(int k) => Leap + k * PerLink;

        /// <summary>When link k is lit end to end, seconds after the leap: its second weapon is reached then.</summary>
        public static float LinkLit(int k) => LinkStart(k) + PerLink;

        /// <summary>When the run has passed every link (the failed ones take their time too): the whole net flashes.</summary>
        public static float Formed(int links) => Leap + links * PerLink;

        /// <summary>
        /// How hard link k flashes now, 0 to 1: its own flash every 0.4-0.7 s, and the whole-net flash as the last
        /// link lights, faded out with the line after <paramref name="end"/>. Take the most of every live link at
        /// a weapon for its pool and spark. age: seconds after the leap; clock: the redraw clock.
        /// </summary>
        public static float Surge(int link, int links, float age, float clock, float end = float.PositiveInfinity)
        {
            int seed = LinkSeed(link);
            float period = SurgeEvery + SurgeSpread * Rand(seed + 5), at = (Mathf.Max(0f, clock) + Rand(seed + 6) * period) % period;
            float own = at < SurgeTime ? 1f - at / SurgeTime : 0f, formed = Formed(links);
            float whole = age >= formed && age < formed + CloseFlash ? 1f : 0f;
            return Mathf.Max(own, whole) * (1f - Mathf.Clamp01((age - end) / CutOut));
        }

        /// <summary>The sketch's seed for link k's shapes.</summary>
        public static int LinkSeed(int link) => 20 + link * 7;

        /// <summary>The redraw step of <paramref name="clock"/>: shapes change 12 times a second.</summary>
        public static int Step(float clock) => Mathf.FloorToInt(clock * Boil);

        /// <summary>
        /// Where a body the current runs through is drawn off its place on this redraw, up to
        /// <paramref name="size"/> each way (<see cref="ShakeHeld"/> or <see cref="ShakeHit"/>). pawn: any number fixed per pawn.
        /// </summary>
        public static Vector2 Shake(int pawn, float clock, float size)
        {
            int step = Step(clock);
            return new Vector2((Rand(step * 13 + pawn * 71 + 1) - 0.5f) * 2f * size, (Rand(step * 17 + pawn * 71 + 2) - 0.5f) * 2f * size);
        }

        /// <summary>The caster's hand, from its DrawPos and aim (degrees, 0 east): 0.26 cells along the aim, 0.25 north of the feet.</summary>
        public static Vector2 Hand(Vector2 drawPos, float aim) =>
            new Vector2(drawPos.x, drawPos.y - FeetBelowDrawPos + 0.25f) + VfxDraw.Turn(aim) * 0.26f;

        /// <summary>A pawn's feet, the point the sketch draws it from.</summary>
        public static Vector2 Feet(Vector2 drawPos) => new Vector2(drawPos.x, drawPos.y - FeetBelowDrawPos);

        /// <summary>Where a line runs through a caught pawn: 0.38 north of its feet.</summary>
        public static Vector2 Chest(Vector2 drawPos) => new Vector2(drawPos.x, drawPos.y - FeetBelowDrawPos + 0.38f);

        /// <summary>Blade k's angle at r texture widths out, radians counter-clockwise, the Fūma turned <paramref name="turn"/> degrees clockwise (RimArt/Fuma/Unfolded: 22 + 40 r + 90 k).</summary>
        public static float BladeAngle(float turn, int k, float r) => (22f + 40f * r + 90f * k - turn) * Mathf.Deg2Rad;

        /// <summary>
        /// Where a line meets the Fūma hanging over <paramref name="fuma"/>: the blade tip nearest the weapon at the
        /// line's other end (<paramref name="other"/>, that weapon's ground point). A kunai's end is its ground point.
        /// </summary>
        public static Vector2 Joint(Vector2 fuma, float turn, Vector2 other)
        {
            Vector2 best = fuma;
            float far = float.PositiveInfinity;
            for (int k = 0; k < 4; k++)
            {
                float a = BladeAngle(turn, k, TipR);
                var q = new Vector2(fuma.x + Mathf.Cos(a) * TipR * A.FumaSize, fuma.y + Mathf.Sin(a) * TipR * A.FumaSize);
                float d = (q - other).magnitude;
                if (d < far)
                {
                    far = d;
                    best = q;
                }
            }
            return best;
        }
    }

    /// <summary>The sketch's five scenarios.</summary>
    public enum RaikoScenario { Fence, Ring, DriftingNet, LetGo, FumaCorner }

    /// <summary>
    /// The preview's script: the sketch's scene(), timeline(), flights() and build(), for the five scenarios at the
    /// sketch's defaults (aim 0, the net 5 cells east of the caster, net 4 s, let go 2 s after it forms, links of 6
    /// cells at most). Times are the sketch's clip seconds, from the start of the charge. Nothing here is drawn or
    /// used by the game: the game's net (RaikoNet) keeps its own rule.
    ///
    /// Each weapon was thrown at its cell before the clip and hangs there from 0 s, going on along its heading at
    /// the hold share (1 %: a kunai 0.24 cells/s, the Fūma 0.144; 10 % in the drifting net); a let-go sends it on at
    /// full speed to the first standing pawn on its path (a kunai) or the end of its range. Who is caught, and when,
    /// is replayed from the leap in 1/60 s steps: a pawn is caught when its ground track comes within 0.45 cells of
    /// a live line (the game uses the cells the line crosses).
    /// </summary>
    public sealed class RaikoKusariScene
    {
        // The showcase (the sketch's defaults) and the rule's placeholders it plays.
        public const float Distance = 5f, Lasts = 4f, LetGoAfter = 2f, MaxLink = 6f;
        public const float MechExtra = 3f, HitStun = 2f;
        private const float Touch = 0.45f, HitReach = 0.4f, CutReach = 0.55f;
        private const float RunSpeed = 3f, WalkSpeed = 1.5f, MechSpeed = 2.2f, RunPast = 2.6f;
        // lib/amenoyodomi.js: the hold shares and the weapons' ranges.
        public const float Hang = 0.01f, Drift = 0.1f, KunaiRange = 14.9f, FumaRange = 12f;
        private const double Step = 1.0 / 60.0;

        public enum Kind { Raider, Shield, Mech, Shooter, Ally }

        /// <summary>A held weapon: thrown from the caster at its cell before the clip, held there from 0 s.</summary>
        public sealed class Weapon
        {
            public bool fuma;
            public Vector2 from, dir;
            public float dist, speed, share, deg, range;
            public int seed;
            public float letGo = float.PositiveInfinity, stopT = float.PositiveInfinity, stopU;
            public int hitPawn = -1;
            public float hitT = float.PositiveInfinity;
            public readonly List<KeyValuePair<int, float>> cuts = new List<KeyValuePair<int, float>>();

            /// <summary>Cells along its heading at clip time s.</summary>
            public float U(float s)
            {
                s = Mathf.Max(0f, s);
                float held = dist + share * speed * Mathf.Min(s, letGo);
                if (s < letGo) return held;
                return s < stopT ? held + speed * (s - letGo) : stopU;
            }

            public Vector2 Ground(float u) => from + dir * u;
            public Vector2 At(float s) => Ground(U(s));
            /// <summary>The Fūma's turn, degrees clockwise: 720 a second of flight, in step with the distance travelled.</summary>
            public float Turn(float s) => fuma ? A.FumaSpin * U(s) / speed : 0f;
            /// <summary>Where it was let go from.</summary>
            public Vector2 LetGoAt => Ground(dist + share * speed * letGo);
            /// <summary>Cells per second it creeps while held.</summary>
            public Vector2 Creep => dir * (share * speed);
        }

        public sealed class Pawn
        {
            public Kind kind;
            public Vector2 start, dir;
            public float speed, t0, reach;
            public float caught = float.PositiveInfinity;
            public int link = -1;
            public readonly List<float> hits = new List<float>();
            public readonly List<Vector2> freezes = new List<Vector2>();

            /// <summary>Where it would be at t if nothing held it.</summary>
            public Vector2 RunAt(float t) => start + dir * Mathf.Min(reach, speed * Mathf.Max(0f, t - t0));

            /// <summary>Where it is at s, the time it spent held taken out.</summary>
            public Vector2 At(float s)
            {
                float lost = 0f;
                foreach (Vector2 f in freezes)
                {
                    if (s <= f.x) break;
                    lost += Mathf.Min(s, f.y) - f.x;
                }
                return RunAt(s - lost);
            }
        }

        public sealed class Link
        {
            public int a, b;
            public float start, lit, snap = float.PositiveInfinity;
            public bool fails, closing;
        }

        public RaikoScenario scenario;
        public Vector2 caster, d;
        public readonly List<Weapon> weapons = new List<Weapon>();
        public readonly List<Pawn> pawns = new List<Pawn>();
        public readonly List<Link> links = new List<Link>();
        public bool letsGo;
        /// <summary>Clip seconds: the leap, the first weapon reached, the run passes the last link, the net ends, the let-go (or never), the clip ends.</summary>
        public float leap, reach0, formed, netEnd, letGo = float.PositiveInfinity, end;

        /// <summary>The ground points the net forms round: the caster 5 cells west of <paramref name="o"/>, the net about <paramref name="o"/>.</summary>
        public static RaikoKusariScene Build(RaikoScenario scenario, Vector2 o)
        {
            var S = new RaikoKusariScene { scenario = scenario, d = Vector2.right };
            S.Lay(o);
            S.Timeline();
            S.Flights();
            S.Catches();
            return S;
        }

        private Vector2 Place(Vector2 o, float u, float v) => o + d * u + new Vector2(-d.y, d.x) * v;

        private void Lay(Vector2 o)
        {
            caster = Place(o, -Distance, 0f);
            Vector2 back = -d;
            Weapon W(bool fuma, float u, float v, float share = Hang)
            {
                Vector2 cell = Place(o, u, v), to = cell - caster;
                float dist = to.magnitude;
                if (dist == 0f) dist = 1e-6f;
                var w = new Weapon
                {
                    fuma = fuma, from = caster, dir = to / dist, dist = dist, share = share, seed = weapons.Count,
                    speed = fuma ? A.FumaSpeed : A.KunaiSpeed, range = fuma ? FumaRange : KunaiRange,
                };
                w.deg = Mathf.Atan2(w.dir.y, w.dir.x) * Mathf.Rad2Deg;
                weapons.Add(w);
                return w;
            }
            void P(Kind kind, float u, float v, Vector2 dir, float speed = 0f, float t0 = 0f, float reach = 0f) =>
                pawns.Add(new Pawn { kind = kind, start = Place(o, u, v), dir = dir, speed = speed, t0 = t0, reach = reach });
            void RunIn(Kind kind, float u, float v, float t0, float speed = RunSpeed) => P(kind, u, v, back, speed, t0, u + RunPast);
            void Behind(Weapon w, float far) => pawns.Add(new Pawn { kind = Kind.Raider, start = w.Ground(w.dist) + w.dir * far, dir = d });

            switch (scenario)
            {
                case RaikoScenario.Ring:
                    W(false, 1.9f, 1.9f); W(false, 1.9f, -1.9f); W(false, -1.9f, -1.9f); W(false, -1.9f, 1.9f);
                    P(Kind.Ally, -1.9f, 0.5f, d); P(Kind.Raider, 0.4f, 0.8f, d, WalkSpeed, 1.2f, 3f); P(Kind.Raider, 0.1f, -0.7f, d);
                    break;
                case RaikoScenario.DriftingNet:
                    W(false, 0f, 3.1f, Drift); W(false, 0.3f, 0f, Drift); W(false, 0f, -3.1f, Drift);
                    P(Kind.Raider, 2f, 1f, d); P(Kind.Raider, 2.6f, -1.4f, d); P(Kind.Raider, 3.3f, 0.4f, d);
                    break;
                case RaikoScenario.LetGo:
                {
                    Weapon w0 = W(false, 0.2f, 3.1f);
                    W(false, 0.5f, 0f);
                    Weapon w2 = W(false, 0.2f, -3.1f);
                    RunIn(Kind.Raider, 4f, 1.2f, 0f); Behind(w0, 2.2f); Behind(w2, 2.2f);
                    letsGo = true;
                    break;
                }
                case RaikoScenario.FumaCorner:
                    W(false, 0.2f, 3.1f); W(true, 0.6f, 0f); W(false, 0.2f, -3.1f);
                    RunIn(Kind.Raider, 3.6f, 1.5f, 0.3f); P(Kind.Raider, 2.2f, 0.15f, d); P(Kind.Raider, 3.4f, -0.1f, d); P(Kind.Raider, 4.6f, 0.2f, d);
                    letsGo = true;
                    break;
                default:
                    W(false, 0.2f, 3.2f); W(false, 0.5f, 0f); W(false, 0.2f, -3.2f);
                    RunIn(Kind.Raider, 4f, 1.6f, 0f); RunIn(Kind.Shield, 5f, -1f, 0.2f); RunIn(Kind.Mech, 5.6f, -2.3f, 0.3f, MechSpeed);
                    P(Kind.Shooter, 4.6f, 0.4f, d);
                    break;
            }
        }

        /// <summary>Where a line meets weapon i at s on the ground: a kunai's middle, or the Fūma's blade tip nearest weapon j.</summary>
        public Vector2 Joint(int i, int j, float s)
        {
            Weapon w = weapons[i];
            Vector2 c = w.At(s);
            return w.fuma ? RaikoKusariTiming.Joint(c, w.Turn(s), weapons[j].At(s)) : c;
        }

        private float Length(int a, int b, float s) => (Joint(b, a, s) - Joint(a, b, s)).magnitude;

        // The run lights the links one after another; a link too long when the run reaches it never forms; one that
        // grows past the limit later snaps. The net ends at the let-go, at the showcase's end, or when its last link
        // has snapped.
        private void Timeline()
        {
            int n = weapons.Count;
            leap = RaikoKusariTiming.Charge;
            reach0 = leap + RaikoKusariTiming.Leap;
            for (int i = 0; i + 1 < n; i++) links.Add(new Link { a = i, b = i + 1, start = reach0 + i * RaikoKusariTiming.PerLink });
            float closeAt = reach0 + (n - 1) * RaikoKusariTiming.PerLink;
            if (n >= 3 && Length(n - 1, 0, closeAt) <= MaxLink) links.Add(new Link { a = n - 1, b = 0, start = closeAt, closing = true });
            foreach (Link L in links)
            {
                L.lit = L.start + RaikoKusariTiming.PerLink;
                L.fails = Length(L.a, L.b, L.start) > MaxLink;
            }
            formed = reach0 + links.Count * RaikoKusariTiming.PerLink;
            float planned = letsGo ? formed + LetGoAfter : leap + Lasts;
            float latest = float.NegativeInfinity;
            bool any = false;
            foreach (Link L in links)
            {
                if (L.fails) continue;
                for (double t = L.lit; t < planned; t += Step)
                    if (Length(L.a, L.b, (float)t) > MaxLink)
                    {
                        L.snap = (float)t;
                        break;
                    }
                any = true;
                latest = Mathf.Max(latest, L.snap);
            }
            netEnd = any ? Mathf.Min(planned, latest) : reach0;
            if (letsGo) letGo = planned;
        }

        // After a let-go: a kunai flies on until the first standing pawn on its path or the end of its range; the
        // Fūma flies to the end of its range and cuts every standing pawn on its line.
        private void Flights()
        {
            if (float.IsInfinity(letGo)) return;
            foreach (Weapon w in weapons)
            {
                w.letGo = letGo;
                float uL = w.dist + w.share * w.speed * letGo;
                Vector2 q = w.Ground(uL);
                int best = -1;
                float bestU = 0f;
                for (int k = 0; k < pawns.Count; k++)
                {
                    Pawn P = pawns[k];
                    if (P.speed != 0f) continue;
                    Vector2 dq = P.start - q;
                    float u = dq.x * w.dir.x + dq.y * w.dir.y, off = Mathf.Abs(dq.x * w.dir.y - dq.y * w.dir.x);
                    if (u <= 0f || u >= w.range) continue;
                    if (w.fuma)
                    {
                        if (off < CutReach) w.cuts.Add(new KeyValuePair<int, float>(k, letGo + u / w.speed));
                    }
                    else if (off < HitReach && (best < 0 || u < bestU))
                    {
                        best = k;
                        bestU = u;
                    }
                }
                if (best >= 0)
                {
                    w.hitPawn = best;
                    w.hitT = letGo + bestU / w.speed;
                    w.stopT = w.hitT;
                    w.stopU = uL + bestU;
                }
                else
                {
                    w.stopT = letGo + w.range / w.speed;
                    w.stopU = uL + w.range;
                }
            }
        }

        private static float SegmentDistance(Vector2 q, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude, u = l2 > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / l2) : 0f;
            return (q - a - ab * u).magnitude;
        }

        // The first moment each pawn touches a live line, replayed from the leap, and which line; then the times it
        // stands still (held, stunned by a charged weapon) and how long the clip runs.
        private void Catches()
        {
            for (int k = 0; k < pawns.Count; k++)
            {
                Pawn P = pawns[k];
                for (double t = reach0; t < netEnd && float.IsInfinity(P.caught); t += Step)
                {
                    float ft = (float)t;
                    Vector2 q = P.RunAt(ft);
                    for (int li = 0; li < links.Count; li++)
                    {
                        Link L = links[li];
                        // Within 0.0001 s of lighting counts as lit: the sketch sums these times in double, and a pawn
                        // standing on a line is caught on the step it lights.
                        if (L.fails || ft < L.lit - 1e-4f || ft >= L.snap) continue;
                        if (SegmentDistance(q, Joint(L.a, L.b, ft), Joint(L.b, L.a, ft)) <= Touch)
                        {
                            P.caught = ft;
                            P.link = li;
                            break;
                        }
                    }
                }
                foreach (Weapon w in weapons)
                {
                    if (w.hitPawn == k) P.hits.Add(w.hitT);
                    foreach (KeyValuePair<int, float> c in w.cuts) if (c.Key == k) P.hits.Add(c.Value);
                }
                foreach (float t in P.hits) P.freezes.Add(new Vector2(t, t + HitStun));
                if (!float.IsInfinity(P.caught)) P.freezes.Add(new Vector2(P.caught, netEnd + (P.kind == Kind.Mech ? MechExtra : 0f)));
                P.freezes.Sort((x, y) => x.x.CompareTo(y.x));
            }
            end = netEnd + 1.1f;
            foreach (Pawn P in pawns) foreach (Vector2 f in P.freezes) end = Mathf.Max(end, f.y + 0.5f);
            foreach (Weapon w in weapons) if (!float.IsInfinity(w.stopT)) end = Mathf.Max(end, Mathf.Min(w.stopT, letGo + 0.8f) + 0.5f);
        }

        /// <summary>Link L is lit and has not ended at s.</summary>
        public bool Live(Link L, float s) => !L.fails && s >= L.lit && s < Mathf.Min(L.snap, netEnd);

        /// <summary>When link L ended: its snap, or the end of the net.</summary>
        public float LinkEnd(Link L) => Mathf.Min(L.snap, netEnd);

        /// <summary>The sketch's phase marks: charge, leap, net, first caught, let go or snaps and end, the mech freed.</summary>
        public List<KeyValuePair<string, float>> Phases()
        {
            var marks = new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("Charge", 0f), new KeyValuePair<string, float>("Leaps", leap) };
            if (!links.Exists(L => !L.fails)) marks.Add(new KeyValuePair<string, float>("Links too long: no net", reach0));
            else
            {
                marks.Add(new KeyValuePair<string, float>("Net", formed));
                float first = float.PositiveInfinity;
                foreach (Pawn P in pawns) first = Mathf.Min(first, P.caught);
                if (!float.IsInfinity(first)) marks.Add(new KeyValuePair<string, float>("Caught", first));
                if (letsGo) marks.Add(new KeyValuePair<string, float>("Let go", letGo));
                else
                {
                    float snap = float.PositiveInfinity;
                    foreach (Link L in links) snap = Mathf.Min(snap, L.snap);
                    if (!float.IsInfinity(snap)) marks.Add(new KeyValuePair<string, float>("Snaps", snap));
                    if (float.IsInfinity(snap) || netEnd > snap + 0.05f) marks.Add(new KeyValuePair<string, float>("Ends", netEnd));
                }
                if (pawns.Exists(P => P.kind == Kind.Mech && !float.IsInfinity(P.caught)))
                    marks.Add(new KeyValuePair<string, float>("Mech free", netEnd + MechExtra));
            }
            marks.RemoveAll(m => m.Value >= end);
            marks.Sort((x, y) => x.Value.CompareTo(y.Value));
            return marks;
        }
    }
}
