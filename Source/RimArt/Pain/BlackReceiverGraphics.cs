using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.BlackReceiverTiming;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Draws Black Receiver: the port of Tools/VfxLab/web/sketches/pain-black-receiver.js; its numbers are that file's
    /// (BlackReceiverTiming). A rod is a 3D segment, knob end to tip, drawn as a strip: a near-black edge, a dark face,
    /// a grey stripe on the side that faces the sun, a pointed tip, a small round knob, and a shadow on the floor along
    /// the sun. Heights are drawn north (Lift 0.6). In the hand and in flight the rod lies level, so it turns with the
    /// aim. In a standing pawn it points back at the thrower, tip out of the pawn's back (that part under the pawn). In
    /// a pawn on the floor the rods stand almost upright (BlackReceiverTiming.OnBack spreads them). Chakra going down a
    /// rod is a soft pale spot that runs to the body; a breaking rod shrinks from the knob down and sheds dark flakes;
    /// the floor keeps a hole and short cracks where a rod went in.
    ///
    /// Every routine takes live ground points (a pawn's feet, or the ground point of a lying pawn as
    /// PainGraphics.Lying draws it: body disc 0.08 north of it), ages in seconds and the map, and keeps no state
    /// between frames. None of them draws a pawn; Pain's arm is PainGraphics.Arm.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BlackReceiverGraphics
    {
        private static readonly Color RodFace = new Color(0.1f, 0.1f, 0.12f), RodLit = new Color(0.46f, 0.46f, 0.51f);
        private static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor(), PawnLayer = AltitudeLayer.Pawn.AltitudeFor();
        /// <summary>The rod in the hand and its palm glow, over Pain's arm (PawnLayer + 0.0104, + 0.0112).</summary>
        private static readonly float HandLayer = PawnLayer + 0.0104f, PalmLayer = PawnLayer + 0.0112f;
        /// <summary>A rod in flight and its trail.</summary>
        private static readonly float FlightLayer = Overhead + 0.02f, TrailLayer = Overhead + 0.019f;
        /// <summary>The part of a rod behind a standing pawn's body, under the pawn.</summary>
        private static readonly float BackLayer = PawnLayer - 0.004f;

        /// <summary>Rod <paramref name="index"/> stuck in a pawn: over the pawn, 0.0008 apart per rod.</summary>
        public static float StuckLayer(int index) => PawnLayer + 0.012f + index * 0.0008f;

        // ---- the sun ------------------------------------------------------------------------------------------

        private static Vector2 sun;
        private static float strength;
        private static int litFrame = -1;
        private static Map litMap;

        /// <summary>The map's shadow vector in cells per cell up, as the lab's scene sun (SixPathsSlamGraphics.SunScale).</summary>
        public static Vector2 Sun(Map map)
        {
            Light(map);
            return sun;
        }

        /// <summary>Shadow alpha: 0.32 by day, as the lab, times the map's shadow strength.</summary>
        public static float ShadowStrength(Map map)
        {
            Light(map);
            return strength;
        }

        private static void Light(Map map)
        {
            if (Time.frameCount == litFrame && map == litMap) return;
            litFrame = Time.frameCount;
            litMap = map;
            sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
            strength = 0.32f * GenCelestial.CurShadowStrength(map);
        }

        // ---- the rod ------------------------------------------------------------------------------------------

        private static Vector2 Scr(Vector3 q) => new Vector2(q.x, q.z + q.y * PainGraphics.Lift);
        private static Vector2 Shd(Vector3 q) => new Vector2(q.x + sun.x * Mathf.Max(0f, q.y), q.z + sun.y * Mathf.Max(0f, q.y));

        /// <summary>
        /// A straight strip from A to B (drawn points), <paramref name="w"/> wide, shifted <paramref name="off"/> cells to
        /// its left; <paramref name="tipped"/> narrows the last 12 % to a point. 10 points.
        /// </summary>
        private static void RodStrip(Vector2 A, Vector2 B, float w, Color colour, float layer, float off, bool tipped)
        {
            float dx = B.x - A.x, dz = B.y - A.y, L = Mathf.Sqrt(dx * dx + dz * dz);
            if (L < 0.005f || w <= 0f || colour.a <= 0.001f) return;
            float nx = -dz / L, nz = dx / L;
            Sides(10, out Vector2[] left, out Vector2[] right);
            for (int i = 0; i <= 9; i++)
            {
                float u = i / 9f, k = tipped && u > 0.88f ? Mathf.Max(0f, 1f - (u - 0.88f) / 0.12f) : 1f, hw = w / 2f * k;
                float cx = A.x + dx * u + nx * off * k, cz = A.y + dz * u + nz * off * k;
                left[i] = new Vector2(cx + nx * hw, cz + nz * hw);
                right[i] = new Vector2(cx - nx * hw, cz - nz * hw);
            }
            Strip(left, right, colour, solid, layer);
        }

        /// <summary>
        /// One rod, knob end to tip, or the part of it from share <paramref name="from"/> to <paramref name="to"/> of its
        /// length: shadow, edge, face, lit stripe, and the knob when the part starts at the knob. Call Light first.
        /// </summary>
        private static void Rod(Vector3 knob, Vector3 tip, float w, float layer, float alpha = 1f, float from = 0f, float to = 1f,
            bool tipped = true, bool shadow = true)
        {
            if (alpha <= 0f || to - from <= 0.001f) return;
            Vector3 A3 = BlackReceiverTiming.Mix(knob, tip, from), B3 = BlackReceiverTiming.Mix(knob, tip, to);
            Vector2 A = Scr(A3), B = Scr(B3);
            if (shadow) RodStrip(Shd(A3), Shd(B3), w * 1.1f, Fade(Ink, strength * 0.8f * alpha), ShadowLayer, 0f, tipped);
            float dx = B.x - A.x, dz = B.y - A.y, L = Mathf.Sqrt(dx * dx + dz * dz);
            if (L <= 0f) L = 1f;
            // The side of the rod that faces the light gets the grey stripe.
            float lit = (-dz / L) * -sun.x + (dx / L) * -sun.y >= 0f ? 1f : -1f;
            RodStrip(A, B, w + 0.02f, Fade(PainGraphics.Core, alpha), layer, 0f, tipped);
            RodStrip(A, B, w, Fade(RodFace, alpha), layer + 0.0002f, 0f, tipped);
            RodStrip(A, B, w * 0.32f, Fade(RodLit, 0.9f * alpha), layer + 0.0004f, lit * w * 0.22f, tipped);
            if (from > 0.001f) return;
            DrawMesh(disc, A, layer + 0.0006f, w * 0.85f, w * 0.85f, 0f, Fade(PainGraphics.Core, alpha), solid);
            DrawMesh(disc, new Vector2(A.x - w * 0.15f, A.y + w * 0.2f), layer + 0.0008f, w * 0.35f, w * 0.35f, 0f, Fade(RodLit, 0.8f * alpha), solid);
        }

        /// <summary>Chakra going down a rod: a soft pale spot at share <paramref name="u"/> of the way from the knob to the body.</summary>
        private static void Flow(Vector3 knob, Vector3 body, float u, float alpha, float layer)
        {
            if (alpha <= 0f || u < 0f || u > 1f) return;
            Vector2 q = Scr(BlackReceiverTiming.Mix(knob, body, u));
            Sprite(q, 0.22f, 0.22f, Fade(PainGraphics.PaleBlue, 0.75f * alpha), glow, layer);
            Sprite(q, 0.09f, 0.09f, Fade(PainGraphics.PaleBlue, alpha), glow, layer + 0.0002f);
        }

        /// <summary>A breaking rod sheds 7 dark flakes from where the break has got to, knob toward the body; each lives 0.45 s.</summary>
        private static void Flakes(Vector3 knob, Vector3 body, float age, int seed)
        {
            for (int i = 0; i < 7; i++)
            {
                float u0 = Rand(seed * 17 + i), a = age - u0 * Crumble;
                if (a < 0f || a > FlakeLife) continue;
                Vector3 q = BlackReceiverTiming.Mix(knob, body, u0);
                Vector2 d = Dir(Rand(seed * 17 + i + 50) * 360f);
                Vector2 pt = Scr(new Vector3(q.x + d.x * a * 0.5f, q.y + a * 0.55f, q.z + d.y * a * 0.5f));
                ChainSickleGraphics.Rect(pt, 0.055f, 0.032f, Rand(seed * 17 + i + 90) * 180f + a * 500f,
                    Fade(PainGraphics.Core, 0.85f * (1f - a / FlakeLife)), Overhead + 0.03f + i * 0.0001f);
            }
        }

        // ---- 1. the rod growing in Pain's hand ------------------------------------------------------------------

        /// <summary>
        /// Pain's arm out at <paramref name="aimDegrees"/> (0 east, 90 north) from his ground point <paramref name="pain"/>,
        /// the hand <paramref name="handAlong"/> cells in front of him (<see cref="BlackReceiverTiming.FromBody"/> at rest,
        /// <see cref="BlackReceiverTiming.HandReach"/> fully out), fingers closed by <paramref name="grip"/> (0.55 round a rod).
        /// </summary>
        public static void Arm(Vector2 pain, float aimDegrees, float handAlong, float grip)
        {
            Vector2 dir = Dir(aimDegrees);
            Begin(pain);
            PainGraphics.Arm(PainGraphics.Place(pain, dir, ShoulderAlong, Across, PainGraphics.ShoulderH),
                PainGraphics.Place(pain, dir, handAlong, Across, PainGraphics.HandH), dir, grip);
        }

        /// <summary>Pain's arm with the hand at the drawn point <paramref name="hand"/> (the stab), the fingers pointing along <paramref name="dir"/>.</summary>
        public static void ArmTo(Vector2 pain, Vector2 dir, Vector2 hand, float grip)
        {
            Begin(pain);
            PainGraphics.Arm(PainGraphics.Place(pain, dir, ShoulderAlong, Across, PainGraphics.ShoulderH), hand, dir, grip);
        }

        /// <summary>
        /// The rod growing out of the palm, level at the hand's height: knob in the hand <paramref name="handAlong"/> cells in
        /// front of Pain, tip <paramref name="grow"/> (0..1) of a rod's length further on; a pale glow at the palm that
        /// dims as it grows.
        /// </summary>
        public static void InHand(Vector2 pain, float aimDegrees, float handAlong, float grow, Map map, float rodLength = RodLength)
        {
            if (!Shown(pain, map)) return;
            Light(map);
            Begin(pain);
            Vector2 dir = Dir(aimDegrees);
            Vector3 knob = Up(Ground(pain, dir, handAlong, Across), PainGraphics.HandH);
            Vector3 tip = Up(Ground(pain, dir, handAlong + rodLength * grow, Across), PainGraphics.HandH);
            Rod(knob, tip, RodWidth, HandLayer);
            Sprite(Scr(knob), 0.3f * grow, 0.3f * grow, Fade(PainGraphics.PaleBlue, 0.5f * (1f - grow * 0.5f)), glow, PalmLayer);
        }

        /// <summary>The Rinnegan glint at Pain's eyes, <paramref name="age"/> seconds after the warm-up began; gone after 0.35 s.</summary>
        public static void Eye(Vector2 pain, float age)
        {
            AmenoyodomiGraphics.EyeStar(new Vector2(pain.x, pain.y + 0.6f), 0.28f, Bump(Mathf.Clamp01(age / EyeLife)));
        }

        /// <summary>
        /// One throw in the hand, for the game: the arm comes up at the target over 0.6 x <paramref name="warm"/>, the
        /// fingers round the rod while it grows over <paramref name="warm"/>, the glint at the eyes; after the release the
        /// hand flicks 0.12 cells forward over 0.16 s, stays up 0.25 s and comes down over 0.3 s.
        /// <paramref name="sinceStart"/>: seconds since the warm-up began. <paramref name="sinceRelease"/>: seconds since
        /// the rod left the hand, negative before. Nothing is drawn 0.55 s after the release.
        /// </summary>
        public static void Throw(Vector2 pain, float aimDegrees, float sinceStart, float sinceRelease, Map map, float warm = Warm,
            float rodLength = RodLength)
        {
            if (sinceStart < 0f || !Shown(pain, map)) return;
            bool holding = sinceRelease < 0f;
            float armUp = Smooth(sinceStart / (warm * 0.6f)), armDown = holding ? 0f : Smooth((sinceRelease - ArmHold) / ArmDown);
            float flick = holding ? 0f : FlickReach * Bump(sinceRelease / (Flick * 2f));
            float handAlong = FromBody + PainGraphics.Reach * armUp * (1f - armDown) + flick;
            if (armUp > 0f && armDown < 1f) Arm(pain, aimDegrees, handAlong, holding ? 0.55f : 0f);
            Eye(pain, sinceStart);
            if (holding) InHand(pain, aimDegrees, handAlong, Smooth(sinceStart / warm), map, rodLength);
        }

        // ---- 2. in flight -------------------------------------------------------------------------------------------

        /// <summary>
        /// Rod <paramref name="index"/> (the place it will take in the pawn, 0..2) in flight, share <paramref name="u"/> of
        /// the way from Pain's hand to the target (both live ground points; see BlackReceiverTiming.Flight), with a faint
        /// dark trail 0.7 cells behind the knob.
        /// </summary>
        public static void Flying(Vector2 pain, Vector2 target, float u, int index, Map map, float rodLength = RodLength)
        {
            if (!Shown(pain, map) && !Shown(target, map)) return;
            Light(map);
            Flight(pain, target, u, index, rodLength, out Vector3 knob, out Vector3 tip, out Vector2 trailEnd);
            Begin(Scr(tip));
            Rod(knob, tip, RodWidth, FlightLayer);
            Streak(Scr(knob), trailEnd, 0.07f, Fade(PainGraphics.Core, 0.3f), solid, TrailLayer, 4);
        }

        // ---- 3. the hit -----------------------------------------------------------------------------------------

        /// <summary>
        /// Where a rod goes in: a dark ripple over the body and a pale flash at the drawn point <paramref name="at"/>, for
        /// 0.25 s from <paramref name="age"/> 0. <see cref="Stuck"/> draws this itself; call it only for a rod that is not
        /// drawn stuck.
        /// </summary>
        public static void Hit(Vector2 at, float age, float layer = float.NaN)
        {
            if (age < 0f || age > HitLife) return;
            if (float.IsNaN(layer)) layer = StuckLayer(0) + 0.004f;
            float u = age / HitLife, e = EaseOut(u);
            Sprite(at, 0.15f + 0.6f * e, 0.12f + 0.45f * e, Fade(PainGraphics.Core, 0.35f * (1f - u)), soft, layer);
            Sprite(at, 0.4f, 0.4f, Fade(PainGraphics.PaleBlue, 0.65f * (1f - u)), glow, layer + 0.0003f);
        }

        // ---- 4 and 5. stuck in a pawn -------------------------------------------------------------------------

        /// <summary>
        /// Rod <paramref name="index"/> (0..2) in a standing pawn at <paramref name="ground"/> (its feet), come from the unit
        /// direction <paramref name="toward"/> (pawn to thrower). <paramref name="age"/>: seconds since it landed (the hit
        /// ripple for 0.25 s, then the chakra spot about once every 0.9 s). <paramref name="breakAge"/>: seconds since it
        /// began to break, negative while whole; drawn until <see cref="BlackReceiverTiming.BreakLinger"/> (0.8 s).
        /// </summary>
        public static void InStanding(Vector2 ground, Vector2 toward, int index, float age, float breakAge, Map map) =>
            Stuck(InBody(index, ground, toward), index, age, breakAge, age, 0f, map);

        /// <summary>
        /// Rod <paramref name="index"/> (0..2) in a pawn lying at <paramref name="ground"/>, head toward the unit
        /// <paramref name="head"/>: upright, spread by the body's axis. <paramref name="leanTo"/>: the unit vector to Pain
        /// for a stabbed pawn, zero for one pinned on its back. <paramref name="flare"/> 0..1 brightens the rods and runs
        /// the spot 4 times a second while something tries to move the pawn (<see cref="Flare"/>). Ages as
        /// <see cref="InStanding"/>.
        /// </summary>
        public static void InLying(Vector2 ground, Vector2 head, int index, float age, float breakAge, Map map, float flare = 0f,
            Vector2 leanTo = default) =>
            Stuck(OnBack(index, ground, head, leanTo), index, age, breakAge, age, flare, map);

        /// <summary>The flare of a pinned pawn's rods, <paramref name="sincePush"/> seconds after something tried to move it: 1 falling to 0 over 0.5 s.</summary>
        public static float Flare(float sincePush) => sincePush >= 0f ? Mathf.Clamp01(1f - sincePush / 0.5f) : 0f;

        /// <summary>
        /// Any stuck rod, for poses in between (the fall and the get-up: BlackReceiverTiming.Blend of InBody and OnBack by
        /// FallShare or RiseShare). In a standing pawn the part behind the body is drawn under the pawn. A breaking rod is
        /// eaten from the knob down over 0.35 s and sheds flakes; while whole it shows the hit ripple and the chakra spot.
        /// <paramref name="spotClock"/>: the running clock the spot is timed on (the preview passes the clip time).
        /// </summary>
        public static void Stuck(ReceiverPose pose, int index, float age, float breakAge, float spotClock, float flare, Map map)
        {
            Vector3 body = pose.Body;
            if (!Shown(Scr(body), map)) return;
            if (breakAge >= BreakLinger) return;
            Light(map);
            Begin(Scr(body));
            float crumble = breakAge >= 0f ? Mathf.Clamp01(breakAge / Crumble) : 0f, layer = StuckLayer(index), w = RodWidth;
            float from = crumble * pose.entry;
            if (crumble < 1f)
            {
                if (pose.lying || pose.entry >= 0.999f) Rod(pose.knob, pose.tip, w, layer, 1f, from, 1f, !pose.lying);
                else
                {
                    Rod(pose.knob, pose.tip, w, layer, 1f, from, pose.entry, false);
                    if (crumble <= 0f) Rod(pose.knob, pose.tip, w, BackLayer, 1f, pose.entry, 1f, true, false);
                }
            }
            if (breakAge >= 0f) Flakes(pose.knob, body, breakAge, index);
            if (crumble > 0f) return;
            Hit(Scr(body), age, layer + 0.004f);
            // Chakra runs down every rod about once a second, and fast while it flares.
            float every = flare > 0f ? SpotFlare : SpotEvery, ph = (spotClock + Rand(index + 7) * every) % every / every;
            float bright = age >= 0f && age < HitBright ? 1f : 0.55f;
            Flow(pose.knob, body, ph * 1.4f, bright * Mathf.Sin(Mathf.Min(1f, ph * 1.4f) * Mathf.PI) + 0.5f * flare, layer + 0.005f);
            if (flare > 0f)
                Sprite(Scr(BlackReceiverTiming.Mix(pose.knob, body, 0.5f)), 0.5f, 0.5f, Fade(PainGraphics.PaleBlue, 0.35f * flare), glow, layer + 0.006f);
        }

        // ---- 6. the stab ----------------------------------------------------------------------------------------------

        /// <summary>
        /// The rod of a stab into a pawn lying at <paramref name="body"/> (head toward <paramref name="head"/>) from Pain at
        /// <paramref name="pain"/>: <paramref name="age"/> seconds after the stab began it grows downward from the hand
        /// over 0.2 s, raised 0.35 cells up its own axis, and is driven in over 0.08 s (in at
        /// <see cref="BlackReceiverTiming.StabIn"/>, 0.28 s; after that draw it with <see cref="InLying"/>, leaning to
        /// Pain). Returns false outside that time. <paramref name="hand"/> is the drawn point the hand holds it at.
        /// </summary>
        public static bool StabRod(Vector2 pain, Vector2 body, Vector2 head, int index, float age, Map map, out Vector2 hand)
        {
            hand = default;
            if (age < 0f || age >= StabIn || !Shown(body, map)) return false;
            Light(map);
            Begin(body);
            ReceiverPose final = OnBack(index, body, head, ToPain(pain, body));
            float grow = Smooth(age / StabGrow), push = Mathf.Clamp01((age - StabGrow) / Thrust);
            ReceiverPose q = Raised(final, StabLift * (1f - push * push));
            Rod(q.knob, q.tip, RodWidth, HandLayer, 1f, 0f, grow);
            hand = Scr(q.knob);
            return true;
        }

        /// <summary>
        /// One stab, for the game: the hand goes from rest to above the pawn's back over the 0.2 s before
        /// <paramref name="age"/> 0, grips the rod while it grows and is driven in (<see cref="StabRod"/>), holds 0.08 s
        /// while the fingers open, and goes back to rest over 0.35 s. The glint at the eyes from age 0.
        /// </summary>
        public static void Stab(Vector2 pain, Vector2 body, Vector2 head, int index, float age, Map map)
        {
            if (age < -StabApproach || age >= StabIn + Hold + StabReturn || !Shown(pain, map)) return;
            Vector2 toward = ToPain(pain, body), away = -toward;
            ReceiverPose final = OnBack(index, body, head, toward);
            Vector2 rest = PainGraphics.Place(pain, away, FromBody + PainGraphics.Reach * RestReach, Across, PainGraphics.HandH);
            Vector2 hand;
            float grip = 0.3f;
            if (age < 0f) hand = Vector2.Lerp(rest, Scr(Raised(final, StabLift).knob), Smooth((age + StabApproach) / StabApproach));
            else if (StabRod(pain, body, head, index, age, map, out hand)) grip = 0.7f;
            else if (age < StabIn + Hold)
            {
                hand = Scr(final.knob);
                grip = Mathf.Lerp(0.7f, 0.3f, (age - StabIn) / Hold);
            }
            else hand = Vector2.Lerp(Scr(final.knob), rest, Smooth((age - StabIn - Hold) / StabReturn));
            ArmTo(pain, away, hand, grip);
            Eye(pain, age);
        }

        /// <summary>The unit vector from <paramref name="body"/> to <paramref name="pain"/>.</summary>
        public static Vector2 ToPain(Vector2 pain, Vector2 body)
        {
            Vector2 d = pain - body;
            float L = d.magnitude;
            return L > 1e-4f ? d / L : Vector2.left;
        }

        /// <summary>The drawn point of a pose's knob end (where the hand holds a stabbed rod).</summary>
        public static Vector2 KnobDrawn(ReceiverPose pose) => Scr(pose.knob);

        // ---- 7. the floor, the dust, the break -----------------------------------------------------------------

        /// <summary>
        /// Where a rod went into the floor, at <paramref name="at"/>: a small dark hole with a little dirt thrown up round
        /// it and six short cracks. Full for 27 s from <paramref name="age"/> 0, then fading out by
        /// <see cref="BlackReceiverTiming.HoleLife"/> (30 s). <paramref name="seed"/> picks the cracks.
        /// </summary>
        public static void Hole(Vector2 at, float age, int seed, Map map)
        {
            if (age < 0f || age >= HoleLife || !Shown(at, map)) return;
            float alpha = 1f - Mathf.Clamp01((age - (HoleLife - HoleFade)) / HoleFade);
            Begin(at);
            Sprite(at, 0.2f, 0.14f, Fade(PainGraphics.DustC, 0.3f * alpha), soft, Floor + 0.019f);
            Crack(at, 0.25f, seed, alpha);
            DrawMesh(disc, at, Floor + 0.022f, 0.045f, 0.032f, 0f, Fade(PainGraphics.Core, 0.85f * alpha), solid);
        }

        /// <summary>The three holes under a pawn pinned at <paramref name="ground"/>, head toward <paramref name="head"/> (seeds 0, 3, 6).</summary>
        public static void Holes(Vector2 ground, Vector2 head, float age, Map map)
        {
            for (int k = 0; k < MostRods; k++) Hole(HoleAt(k, ground, head), age, k * 3, map);
        }

        // lib/chain-sickle.js crack() with an alpha: six short dark radial lines and a dark patch.
        private static readonly Vector2[] crackPts = new Vector2[3];

        private static void Crack(Vector2 pos, float amount, int seed, float alpha)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + Rand(i + seed + 400) * 0.6f, len = (0.18f + Rand(i + seed + 410) * 0.2f) * amount;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 p0 = pos + d * 0.1f, p1 = pos + d * (0.1f + len);
                crackPts[0] = p0;
                crackPts[1] = (p0 + p1) / 2f + new Vector2(Mathf.Sin(a) * 0.03f, -Mathf.Cos(a) * 0.03f);
                crackPts[2] = p1;
                ChainSickleGraphics.Tube(crackPts, 3, 0.035f, 0.035f * 0.2f, Fade(ChainSickleGraphics.Body, 0.6f * amount * alpha),
                    Floor + 0.02f + i * 0.0001f);
            }
            Sprite(pos, 0.5f * amount, 0.32f * amount, Fade(ChainSickleGraphics.Body, 0.35f * amount * alpha), soft, Floor + 0.015f);
        }

        /// <summary>Dust thrown up where the pinned pawn lands on its back, <paramref name="sinceLanded"/> seconds after, for 0.6 s.</summary>
        public static void PinDust(Vector2 ground, float sinceLanded, Map map)
        {
            if (sinceLanded < 0f || sinceLanded >= LandDust || !Shown(ground, map)) return;
            ChainSickleGraphics.Kick(ground, sinceLanded, 1f - Mathf.Clamp01(sinceLanded / LandDust), 21);
        }

        /// <summary>Dust at a pinned pawn that something tried to move (a push), <paramref name="sincePush"/> seconds after, for 0.5 s.</summary>
        public static void PushDust(Vector2 ground, float sincePush, Map map)
        {
            if (sincePush < 0f || sincePush >= 0.5f || !Shown(ground, map)) return;
            ChainSickleGraphics.Kick(ground, sincePush, 1f - sincePush / 0.5f, 33);
        }

        /// <summary>The pale flash over a lying pawn when all its rods break at once, <paramref name="age"/> seconds after, for 0.3 s.</summary>
        public static void AllBreak(Vector2 ground, float age, Map map)
        {
            if (age < 0f || age >= AllBreakLife || !Shown(ground, map)) return;
            Sprite(new Vector2(ground.x, ground.y + 0.3f), 1.2f, 1f, Fade(PainGraphics.PaleBlue, 0.35f * (1f - age / AllBreakLife)), glow,
                Overhead + 0.04f);
        }
    }
}
