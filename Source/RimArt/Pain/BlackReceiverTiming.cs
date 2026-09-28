using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// One Black Receiver rod placed in 3D: the knob end and the tip as (x east, y cells up, z north), the share of
    /// its length in front of the body (<see cref="entry"/>; 1 for a pawn on the floor), whether the pawn is lying,
    /// and for a lying pawn the point where the rod went into the floor.
    /// </summary>
    public struct ReceiverPose
    {
        public Vector3 knob, tip;
        public float entry;
        public bool lying;
        public Vector2 ground;

        /// <summary>The point where the rod goes into the body, share <see cref="entry"/> of the way from the knob.</summary>
        public Vector3 Body => BlackReceiverTiming.Mix(knob, tip, entry);

        /// <summary>The same pose moved <paramref name="east"/> cells east (the pinned pawn's shudder).</summary>
        public ReceiverPose Shifted(float east)
        {
            ReceiverPose q = this;
            q.knob.x += east;
            q.tip.x += east;
            return q;
        }
    }

    /// <summary>
    /// Times and sizes of the Black Receiver picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/pain-black-receiver.js). Seconds and cells; no drawing. The rule's numbers that
    /// gameplay reads (charges, range, rod life in game) are XML fields; the ones here are the picture's.
    /// </summary>
    public static class BlackReceiverTiming
    {
        // ---- the sketch's sliders at their defaults -----------------------------------------------------------

        /// <summary>The rod grows out of the palm over Warm; it flies at Speed cells/s.</summary>
        public const float Warm = 0.3f, Speed = 30f;
        /// <summary>Rod length and width, cells.</summary>
        public const float RodLength = 1.1f, RodWidth = 0.05f;
        /// <summary>Each rod lasts ShownLife in the lab so the clip stays short; the game rule is GameLife.</summary>
        public const float ShownLife = 5f, GameLife = 8f;
        /// <summary>The preview's target: cells from Pain at the start, walking at Pain at Walk cells/s.</summary>
        public const float Distance = 8f, Walk = 2f;

        // ---- the rule's numbers as the picture shows them ---------------------------------------------------

        /// <summary>Moving lost per rod (0.25); rods that pin (3).</summary>
        public const float PerRod = 0.25f;
        public const int PinAt = 3, MostRods = 3;

        // ---- decided timing and shape ----------------------------------------------------------------------

        /// <summary>Clip start to the first throw; the least time from one release to the next warm-up; the hand's flick lasts 2 x Flick.</summary>
        public const float Lead = 0.2f, Recover = 0.2f, Flick = 0.08f;
        /// <summary>How far the hand flicks forward as the rod leaves, cells.</summary>
        public const float FlickReach = 0.12f;
        /// <summary>The arm stays up ArmHold after the last release, then comes down over ArmDown.</summary>
        public const float ArmHold = 0.25f, ArmDown = 0.3f;
        /// <summary>The Rinnegan glint at the eyes lasts EyeLife from the first warm-up.</summary>
        public const float EyeLife = 0.35f;
        /// <summary>The third rod: the pawn falls on its back in Fall, KnockBack cells further from Pain. Freed, it gets up in GetUp.</summary>
        public const float Fall = 0.18f, KnockBack = 0.35f, GetUp = 0.3f;
        /// <summary>A breaking rod shrinks from the knob down in Crumble; its flakes last FlakeLife each.</summary>
        public const float Crumble = 0.35f, FlakeLife = 0.45f;
        /// <summary>A breaking rod is drawn this long after it starts to break (the last flake is gone).</summary>
        public const float BreakLinger = Crumble + FlakeLife;
        /// <summary>The preview clip runs Tail past the last break.</summary>
        public const float Tail = 0.8f;
        /// <summary>The ripple and flash where a rod goes in last HitLife; the chakra spot is brighter for HitBright.</summary>
        public const float HitLife = 0.25f, HitBright = 0.3f;
        /// <summary>The chakra spot runs down a rod once every SpotEvery seconds, every SpotFlare while something pushes the pawn.</summary>
        public const float SpotEvery = 0.9f, SpotFlare = 0.25f;
        /// <summary>The first two rods jolt the pawn JoltReach cells back over JoltTime.</summary>
        public const float JoltReach = 0.08f, JoltTime = 0.14f;
        /// <summary>A pinned pawn shudders ShudderReach cells east-west at ShudderRate radians per second.</summary>
        public const float ShudderReach = 0.012f, ShudderRate = 70f;
        /// <summary>Dust thrown up where a pinned pawn lands, and when a push hits it.</summary>
        public const float LandDust = 0.6f;
        /// <summary>Holes and cracks in the floor where rods went in: they stay HoleLife, the last HoleFade of it fading.</summary>
        public const float HoleLife = 30f, HoleFade = 3f;

        /// <summary>A rod in a standing pawn: cells out the front (to the knob), cells out the back.</summary>
        public const float Front = 0.62f, Through = 0.25f;
        /// <summary>Cells up where a rod leaves a pawn lying on the floor.</summary>
        public const float BodySurface = 0.1f;
        /// <summary>The hand starts FromBody cells in front of Pain's ground point; it is held Across cells to his right.</summary>
        public const float FromBody = 0.12f, Across = -0.1f, ShoulderAlong = 0.05f;
        /// <summary>A flying rod's tip stops this far short of the target's ground point, and its trail runs this far behind the knob.</summary>
        public const float TipShort = 0.12f, TrailBack = 0.7f;

        /// <summary>The stab: body this far from Pain; one stab every StabGap; the rod grows in StabGrow and is driven StabLift cells down in Thrust; the hand holds Hold.</summary>
        public const float StabBody = 1.05f, StabGap = 0.5f, StabGrow = 0.2f, Thrust = 0.08f, StabLift = 0.35f, Hold = 0.08f;
        /// <summary>From the stab's start to the rod being in.</summary>
        public const float StabIn = StabGrow + Thrust;
        /// <summary>The hand travels to the next stab over StabApproach before it, and back to rest over StabReturn after the last.</summary>
        public const float StabApproach = 0.2f, StabReturn = 0.35f;
        /// <summary>The hand at rest between stabs: this share of the arm's reach.</summary>
        public const float RestReach = 0.4f;

        /// <summary>The push preview: raiders walk from PushWalkFrom to PushWalkTo, are thrown PushOut cells in PushFly; the pinned one lies PinnedAt cells out.</summary>
        public const float PushWalkFrom = 4.2f, PushWalkTo = 1.7f, PushOut = 5f, PushFly = 0.45f, PinnedAt = 2.2f;
        /// <summary>The two raiders come from these angles off the aim (degrees, counter-clockwise).</summary>
        public static readonly float[] PushFrom = { 50f, 115f };
        /// <summary>The "Pain goes down" preview: shots at ShotsAt, ShotGap apart; Pain down at DownAt; the raider free FreeDelay later; pinned DownPinnedAt cells out.</summary>
        public const float ShotsAt = 0.55f, ShotGap = 0.15f, DownAt = 0.95f, FreeDelay = 0.45f, DownPinnedAt = 3f;
        /// <summary>Each rod of a pawn broken at once starts BreakStagger after the one before.</summary>
        public const float BreakStagger = 0.03f;
        /// <summary>The pale flash on the body when every rod breaks at once.</summary>
        public const float AllBreakLife = 0.3f;

        /// <summary>Camera shake: a rod lands in a standing pawn; the pinned pawn lands; the third stab; the push.</summary>
        public const float ShakeHit = 0.012f, ShakePin = 0.03f, ShakeStabPin = 0.025f, ShakePush = 0.04f;

        // Where each rod goes. In a standing pawn: across the body (cells, left of the line to Pain), height, yaw off the
        // line to Pain (degrees), how much higher the knob end is.
        private static readonly float[] InAcross = { 0.09f, -0.08f, 0.02f }, InHeight = { 0.58f, 0.32f, 0.47f },
            InYaw = { 7f, -9f, 3f }, InRise = { 0.1f, 0.14f, 0.08f };
        // In a pawn on the floor: along the body toward the head, across it, height of the knob above the body, which
        // side it leans to.
        private static readonly float[] BackAlong = { 0.26f, -0.14f, 0.06f }, BackAcross = { 0.1f, -0.1f, -0.01f },
            BackHeight = { 0.6f, 0.5f, 0.7f }, BackSide = { 1f, -1f, 1f };

        /// <summary>Height of rod <paramref name="index"/> in a standing pawn, cells.</summary>
        public static float HitHeight(int index) => InHeight[Slot(index)];
        /// <summary>Across offset of rod <paramref name="index"/> in a standing pawn, cells left of the line to Pain.</summary>
        public static float HitAcross(int index) => InAcross[Slot(index)];

        private static int Slot(int index) => index < 0 ? 0 : index % MostRods;

        // ---- small maths ------------------------------------------------------------------------------------

        /// <summary>Share <paramref name="u"/> of the way from <paramref name="a"/> to <paramref name="b"/>, unclamped (the sketch's mix3).</summary>
        public static Vector3 Mix(Vector3 a, Vector3 b, float u) => a + (b - a) * u;
        public static float EaseOut(float x) { float u = 1f - Mathf.Clamp01(x); return 1f - u * u * u; }
        public static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;
        /// <summary>A vector turned <paramref name="degrees"/> counter-clockwise (x east, y north).</summary>
        public static Vector2 Turned(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
        /// <summary>The unit vector of an aim in degrees (0 east, 90 north).</summary>
        public static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        /// <summary>A point <paramref name="along"/> cells down <paramref name="dir"/> and <paramref name="across"/> cells to its left.</summary>
        public static Vector2 Ground(Vector2 from, Vector2 dir, float along, float across) =>
            new Vector2(from.x + along * dir.x - across * dir.y, from.y + along * dir.y + across * dir.x);
        /// <summary>A ground point with a height.</summary>
        public static Vector3 Up(Vector2 ground, float h) => new Vector3(ground.x, h, ground.y);

        // ---- the fall, the get-up and the shudder: for moving the real pawn -----------------------------------

        /// <summary>0..1: how far the pawn has gone over onto its back, <paramref name="sinceHit"/> seconds after the third rod.</summary>
        public static float FallShare(float sinceHit) => Smooth(sinceHit / Fall);
        /// <summary>Cells the third rod has thrown the pawn back from Pain, 0 to <see cref="KnockBack"/> in <see cref="Fall"/>.</summary>
        public static float FallBack(float sinceHit) => sinceHit <= 0f ? 0f : KnockBack * EaseOut(sinceHit / Fall);
        /// <summary>0..1: how far a freed pawn has got up, <paramref name="sinceFree"/> seconds after the pin ended.</summary>
        public static float RiseShare(float sinceFree) => Smooth(sinceFree / GetUp);
        /// <summary>Cells a standing pawn is jolted back from Pain by a rod that landed <paramref name="sinceHit"/> ago.</summary>
        public static float Jolt(float sinceHit) => JoltReach * Bump(sinceHit / JoltTime);
        /// <summary>The pinned pawn's shudder, cells east, on any running clock.</summary>
        public static float Shudder(float clock) => ShudderReach * Mathf.Sin(clock * ShudderRate);
        /// <summary>Walk speed with <paramref name="rods"/> rods in the pawn, as the picture slows it (0 when pinned).</summary>
        public static float Slowed(float walk, int rods) => rods >= PinAt ? 0f : walk * Mathf.Max(0f, 1f - PerRod * rods);

        // ---- where the rods sit -------------------------------------------------------------------------------

        /// <summary>
        /// Rod <paramref name="index"/> in a standing pawn at <paramref name="ground"/> (its feet): in at the chest or hip,
        /// tip out of the back, knob end sticking out toward where it came from. <paramref name="toward"/> is the unit
        /// vector from the pawn to the thrower.
        /// </summary>
        public static ReceiverPose InBody(int index, Vector2 ground, Vector2 toward)
        {
            int k = Slot(index);
            Vector2 u = Turned(toward, InYaw[k]), side = new Vector2(-toward.y, toward.x);
            Vector2 e = ground + side * InAcross[k];
            return new ReceiverPose
            {
                knob = new Vector3(e.x + u.x * Front, InHeight[k] + InRise[k], e.y + u.y * Front),
                tip = new Vector3(e.x - u.x * Through, InHeight[k] - 0.04f, e.y - u.y * Through),
                entry = Front / (Front + Through),
                lying = false,
            };
        }

        /// <summary>
        /// Rod <paramref name="index"/> in a pawn lying at <paramref name="ground"/> with its head toward
        /// <paramref name="head"/> (unit): almost upright out of the body, the rest through it into the floor. A body
        /// lying north-south spreads the tops east and west (0.05 + 0.12 x |north share| cells, alternating sides) so
        /// they do not fall on one screen line. <paramref name="leanTo"/> (zero for none; a stab passes the unit vector
        /// to Pain) tips the tops that way by 0.2 x |east share of the body| only.
        /// </summary>
        public static ReceiverPose OnBack(int index, Vector2 ground, Vector2 head, Vector2 leanTo = default)
        {
            int k = Slot(index);
            var side = new Vector2(-head.y, head.x);
            Vector2 b = ground + head * BackAlong[k] + side * BackAcross[k];
            float spread = BackSide[k] * (0.05f + 0.12f * Mathf.Abs(head.y));
            Vector2 lean = side * spread + head * 0.03f + leanTo * (0.2f * Mathf.Abs(head.x));
            return new ReceiverPose
            {
                knob = new Vector3(b.x + lean.x, BodySurface + BackHeight[k], b.y + lean.y),
                tip = new Vector3(b.x, BodySurface, b.y),
                entry = 1f,
                lying = true,
                ground = b,
            };
        }

        /// <summary>Where rod <paramref name="index"/> of a pawn lying as <see cref="OnBack"/> went into the floor.</summary>
        public static Vector2 HoleAt(int index, Vector2 ground, Vector2 head) => OnBack(index, ground, head).ground;

        /// <summary>A pose part way from <paramref name="a"/> to <paramref name="b"/> (the fall, the get-up).</summary>
        public static ReceiverPose Blend(ReceiverPose a, ReceiverPose b, float u) => new ReceiverPose
        {
            knob = BlackReceiverTiming.Mix(a.knob, b.knob, u),
            tip = BlackReceiverTiming.Mix(a.tip, b.tip, u),
            entry = Mathf.Lerp(a.entry, b.entry, u),
            lying = u > 0.5f ? b.lying : a.lying,
            ground = u > 0.5f ? b.ground : a.ground,
        };

        /// <summary>
        /// Rod <paramref name="index"/> while the third rod throws the pawn onto its back: from the standing pose at
        /// <paramref name="stoodAt"/> (where it stood when hit) to the lying pose at <paramref name="liesAt"/>, head away
        /// from the thrower, over <see cref="Fall"/>. <paramref name="toward"/>: unit vector from the pawn to the thrower.
        /// </summary>
        public static ReceiverPose FallPose(int index, Vector2 stoodAt, Vector2 liesAt, Vector2 toward, float sinceHit) =>
            Blend(InBody(index, stoodAt, toward), OnBack(index, liesAt, -toward), FallShare(sinceHit));

        /// <summary>Rod <paramref name="index"/> while a freed pawn gets up: from lying at <paramref name="liesAt"/> to standing at <paramref name="standsAt"/> over <see cref="GetUp"/>.</summary>
        public static ReceiverPose RisePose(int index, Vector2 liesAt, Vector2 standsAt, Vector2 toward, float sinceFree) =>
            Blend(OnBack(index, liesAt, -toward), InBody(index, standsAt, toward), RiseShare(sinceFree));

        /// <summary>A pose moved back up its own axis by <paramref name="lift"/> cells: a stabbed rod before it is driven in.</summary>
        public static ReceiverPose Raised(ReceiverPose q, float lift)
        {
            Vector3 d = q.tip - q.knob;
            float L = d.magnitude;
            if (L <= 0f) L = 1f;
            Vector3 m = -d / L * lift;
            q.knob += m;
            q.tip += m;
            return q;
        }

        // ---- the hand and the flight ------------------------------------------------------------------------

        /// <summary>Cells from Pain's ground point to the hand with the arm fully out.</summary>
        public static float HandReach => FromBody + PainGraphics.Reach;

        /// <summary>
        /// Rod <paramref name="index"/>'s knob end and tip in flight, a share <paramref name="u"/> of the way from Pain's
        /// hand to the target (both ground points, live). The tip starts at the hand plus a rod's length and ends
        /// <see cref="TipShort"/> short of the target; the rod stays level, dropping from the hand's height to the height
        /// it goes in at, and moves from the hand's side to its place across the body.
        /// </summary>
        public static void Flight(Vector2 pain, Vector2 target, float u, int index, float rodLength, out Vector3 knob, out Vector3 tip,
            out Vector2 trailEnd)
        {
            Vector2 d = target - pain;
            float a = d.magnitude;
            Vector2 dir = a > 1e-4f ? d / a : Vector2.right;
            float tipAlong = Mathf.Lerp(HandReach + rodLength, a - TipShort, u);
            float h = Mathf.Lerp(PainGraphics.HandH, HitHeight(index), u), across = Mathf.Lerp(Across, HitAcross(index), u);
            tip = Up(Ground(pain, dir, tipAlong, across), h);
            knob = Up(Ground(pain, dir, tipAlong - rodLength, across), h + 0.02f);
            trailEnd = Ground(pain, dir, tipAlong - rodLength - TrailBack, across);
            trailEnd.y += h * PainGraphics.Lift;
        }

        /// <summary>Seconds a rod takes from release to hit at <see cref="Speed"/>, with the target <paramref name="gap"/> cells past the rod's tip, closing at <paramref name="closing"/> cells/s.</summary>
        public static float FlightTime(float gap, float closing = 0f) => Mathf.Max(0.02f, gap / (Speed + closing));

        // ---- the other previews' scripts (the sketch's stabPlan, pushPlan and the "Pain goes down" end) ---------

        /// <summary>Stab <paramref name="k"/> (0..2) begins: 0.2, 0.7, 1.2 s. It is in <see cref="StabIn"/> later.</summary>
        public static float StabStart(int k) => Lead + k * StabGap;
        /// <summary>The stab preview ends 2.2 s after the third rod is in: 3.68 s.</summary>
        public static float StabEnd => StabStart(2) + StabIn + 2.2f;
        /// <summary>The push preview: the raiders arrive at 1.25 s, the push at 1.45 s, the end at 3.35 s.</summary>
        public static float PushArrive => (PushWalkFrom - PushWalkTo) / Mathf.Max(0.5f, Walk);
        public static float PushAt => PushArrive + 0.2f;
        public static float PushEnd => PushAt + 1.9f;
        /// <summary>The "Pain goes down" preview ends at 3.2 s.</summary>
        public const float DownEnd = DownAt + FreeDelay + GetUp + 1.5f;
    }

    /// <summary>
    /// The "three throws" preview's script (the sketch's throwPlan): a raider walks in from <see cref="BlackReceiverTiming.Distance"/>
    /// cells at <see cref="BlackReceiverTiming.Walk"/> cells/s, loses 25 % of it per rod, and is pinned by the third (it
    /// falls on its back 0.35 cells further away). Each throw waits for the one before it to land. Seconds from the clip
    /// start and cells from Pain along the aim.
    /// </summary>
    public sealed class BlackReceiverThrows
    {
        public readonly float[] starts = new float[3], releases = new float[3], hits = new float[3], breaks = new float[3];
        public readonly float hand, distance, walk, warm, rodLength, life;
        public float landed, upEnd, end;
        // Walking segments: from time t at along a, at speed v (toward Pain).
        private readonly float[] segT = new float[8], segA = new float[8], segV = new float[8];
        private int segs;

        public BlackReceiverThrows(float distance = BlackReceiverTiming.Distance, float walk = BlackReceiverTiming.Walk,
            float warm = BlackReceiverTiming.Warm, float rodLength = BlackReceiverTiming.RodLength, float life = BlackReceiverTiming.ShownLife)
        {
            this.distance = distance;
            this.walk = walk;
            this.warm = warm;
            this.rodLength = rodLength;
            this.life = life;
            hand = BlackReceiverTiming.HandReach;
            AddSeg(0f, walk, distance);
            float start = BlackReceiverTiming.Lead;
            for (int k = 0; k < 3; k++)
            {
                float release = start + warm, v = segV[segs - 1];
                float gap = WalkAt(release) - BlackReceiverTiming.TipShort - hand - rodLength;
                float hit = release + BlackReceiverTiming.FlightTime(gap, v);
                starts[k] = start;
                releases[k] = release;
                hits[k] = hit;
                AddSeg(hit, BlackReceiverTiming.Slowed(walk, k + 1));
                start = Mathf.Max(release + BlackReceiverTiming.Recover, hit + 0.05f);
            }
            for (int k = 0; k < 3; k++) breaks[k] = hits[k] + life;
            upEnd = breaks[0] + BlackReceiverTiming.GetUp;
            landed = hits[2] + BlackReceiverTiming.Fall;
            // After the get-up it walks again, faster as each remaining rod breaks.
            float[] after = { upEnd, breaks[1], breaks[2] };
            System.Array.Sort(after);
            foreach (float x in after)
                if (x >= upEnd) AddSeg(x, BlackReceiverTiming.Slowed(walk, Count(x)));
            end = breaks[2] + BlackReceiverTiming.Crumble + BlackReceiverTiming.Tail;
        }

        private void AddSeg(float t, float v, float a = float.NaN)
        {
            segA[segs] = float.IsNaN(a) ? WalkAt(t) : a;
            segT[segs] = t;
            segV[segs] = v;
            segs++;
        }

        /// <summary>Rods in the raider at <paramref name="t"/>.</summary>
        public int Count(float t)
        {
            int n = 0;
            for (int k = 0; k < 3; k++)
                if (hits[k] <= t && t < breaks[k]) n++;
            return n;
        }

        /// <summary>Cells from Pain along the aim, walking only (no knock-back or jolt).</summary>
        public float WalkAt(float t)
        {
            int q = 0;
            for (int i = 0; i < segs; i++)
                if (segT[i] <= t) q = i;
            return segA[q] - segV[q] * (t - segT[q]);
        }

        /// <summary>Cells from Pain along the aim, with the jolts of the first two rods and the third's knock-back; never nearer than 1.3.</summary>
        public float AlongAt(float s)
        {
            float jolt = BlackReceiverTiming.Jolt(s - hits[0]) + BlackReceiverTiming.Jolt(s - hits[1]);
            return Mathf.Max(1.3f, WalkAt(s) + (s >= hits[2] ? BlackReceiverTiming.FallBack(s - hits[2]) : 0f) + jolt);
        }

        /// <summary>Where the raider lies while pinned, cells from Pain along the aim.</summary>
        public float LyingAt => WalkAt(hits[2]) + BlackReceiverTiming.KnockBack;
    }
}
