using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// One pull's times in sketch seconds, from <see cref="BanshoTiming.Times"/>. The clock reads 0 at rest and
    /// <see cref="cast"/> (BanshoTiming.Lead, 0.2) when the warm-up begins.
    /// </summary>
    public readonly struct BanshoTimes
    {
        /// <summary>Cells from Pain to the target at the cast; the warm-up; flight speed (cells/s); how long the result shows; the stun.</summary>
        public readonly float distance, warm, speed, hold, stun;
        /// <summary>Body size over 2: dragged, not lifted. A standing pawn steps into the line: stops it.</summary>
        public readonly bool heavy, blocked;
        /// <summary>Cells from Pain to the pawn that blocks (blocked only).</summary>
        public readonly float blockAt;
        /// <summary>The tug: seconds, and cells the target slides in it (0 for a dragged one).</summary>
        public readonly float tug, slide;
        /// <summary>Cells covered in the flight or drag, and its seconds.</summary>
        public readonly float path, fly;
        /// <summary>
        /// cast: the warm-up begins. grip: the pull takes hold. lift: the target leaves the floor (a dragged one starts
        /// moving). arrive: it reaches the cell in front of Pain, the blocker, or the end of the drag. down: it is on the
        /// floor (the slam). end: the picture is over.
        /// </summary>
        public readonly float cast, grip, lift, arrive, down, end;

        public bool Normal => !heavy && !blocked;

        public BanshoTimes(float distance, bool heavy, bool blocked, float blockAt, float warm, float speed, float hold, float stun)
        {
            this.distance = distance;
            this.heavy = heavy;
            this.blocked = blocked && !heavy;
            this.blockAt = blockAt;
            this.warm = warm;
            this.speed = speed;
            this.hold = hold;
            this.stun = stun;
            cast = BanshoTiming.Lead;
            grip = cast + warm;
            tug = heavy ? 0f : BanshoTiming.Tug;
            slide = heavy ? 0f : BanshoTiming.TugSlide;
            lift = grip + tug;
            float p = heavy ? (distance - BanshoTiming.LandGap) * BanshoTiming.HeavyShare
                : this.blocked ? distance - slide - blockAt - BanshoTiming.BlockGap
                : distance - slide - BanshoTiming.LandGap;
            path = Mathf.Max(0f, p);
            fly = Mathf.Max(0.0001f, path / (speed * (heavy ? BanshoTiming.HeavySpeed : 1f)));
            arrive = lift + fly;
            down = arrive + (heavy ? 0f : this.blocked ? BanshoTiming.Drop : BanshoTiming.Catch);
            end = down + hold + BanshoTiming.Tail;
        }
    }

    /// <summary>
    /// Times, sizes and the target's motion for the Banshō Ten'in picture: the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/pain-bansho-tenin.js, look "Storm 4: black core, pale glow"). No drawing and no map:
    /// seconds in, cells out. The sketch clock s reads 0 at rest and <see cref="Lead"/> when the warm-up begins; the
    /// game starts it at Lead when the warm-up begins. Cells "along" are from Pain's ground point along the aim.
    /// </summary>
    public static class BanshoTiming
    {
        /// <summary>Rest before the warm-up, and the warm-up (Pain's palm comes up at the target).</summary>
        public const float Lead = 0.2f, Warm = 0.4f;
        /// <summary>The pull takes hold: seconds, cells slid toward Pain, how far the body leans toward Pain (tilt -0.3).</summary>
        public const float Tug = 0.15f, TugSlide = 0.25f, TugLean = 0.3f;
        /// <summary>Flight speed (cells/s) and how high the target is lifted (cells).</summary>
        public const float Speed = 20f, LiftHeight = 0.6f;
        /// <summary>It lands this many cells in front of Pain.</summary>
        public const float LandGap = 1f;
        /// <summary>Body size over 2: dragged half the distance at 35 % of the speed.</summary>
        public const float HeavyShare = 0.5f, HeavySpeed = 0.35f;
        /// <summary>A blocked target stops this many cells short of the blocker; the sketch's blocker stands at half the distance.</summary>
        public const float BlockGap = 0.5f, BlockShare = 0.5f;
        /// <summary>From arrival to the floor: pushed down on the catch in Catch, dropped when blocked in Drop.</summary>
        public const float Catch = 0.1f, Drop = 0.12f;
        /// <summary>Stun shown after the slam, and on both pawns after a block (seconds).</summary>
        public const float Stun = 2f, BlockStun = 1f;
        /// <summary>The result shows Hold seconds after the slam; then the lasting marks fade over Tail.</summary>
        public const float Hold = 2.5f, Tail = 0.4f;
        /// <summary>Crater plates (the sketch's slider default).</summary>
        public const int Slabs = 7;
        /// <summary>Palm core radius, the sphere's radius once it has closed on the chest, the gap from palm to core.</summary>
        public const float OrbR = 0.2f, BindR = 0.44f, OrbGap = 0.2f;
        /// <summary>The blocker is knocked this many cells toward Pain over KnockTime.</summary>
        public const float Knock = 0.35f, KnockTime = 0.2f;
        /// <summary>Camera shakes: at the grip; at the end of a drag, a block, a slam.</summary>
        public const float ShakeGrip = 0.012f, ShakeDrag = 0.01f, ShakeBlock = 0.03f, ShakeSlam = 0.06f;

        /// <summary>
        /// One pull's times. <paramref name="blockAt"/> is the blocker's cells from Pain; below 0 it is the sketch's
        /// half the distance. A heavy target is never blocked.
        /// </summary>
        public static BanshoTimes Times(float distance, bool heavy, bool blocked, float blockAt = -1f, float warm = Warm,
            float speed = Speed, float hold = Hold, float stun = Stun) =>
            new BanshoTimes(distance, heavy, blocked, blockAt < 0f ? distance * BlockShare : blockAt, warm, speed, hold, stun);

        /// <summary>
        /// Cells from Pain along the aim at <paramref name="s"/>. During the tug the target slides TugSlide cells,
        /// speeding up; the flight starts at the speed the slide ended at and keeps speeding up toward the hand. A
        /// dragged one eases in and out. Holds the arrival point after arrive.
        /// </summary>
        public static float AlongAt(float s, in BanshoTimes t)
        {
            if (s < t.grip) return t.distance;
            if (s < t.lift)
            {
                float v = (s - t.grip) / t.tug;
                return t.distance - t.slide * v * v;
            }
            float u = Mathf.Clamp01((s - t.lift) / t.fly);
            if (t.heavy) return t.distance - t.path * VfxMath.Smooth(u);
            float k = t.path > 0f ? Mathf.Min(0.6f, 2f * t.slide / t.tug * t.fly / t.path) : 0.6f;
            return t.distance - t.slide - t.path * (k * u + (1f - k) * Mathf.Pow(u, 1.6f));
        }

        /// <summary>
        /// Cells up at <paramref name="s"/>: rises to <paramref name="lift"/> in 0.07 s after the lift, holds through the
        /// flight, falls to the floor between arrive and down (1 - d²). A dragged one stays on the floor.
        /// </summary>
        public static float HeightAt(float s, in BanshoTimes t, float lift = LiftHeight)
        {
            if (t.heavy || s < t.lift) return 0f;
            float up = lift * VfxMath.Smooth((s - t.lift) / 0.07f);
            if (s < t.arrive) return up;
            float d = Mathf.Clamp01((s - t.arrive) / (t.down - t.arrive));
            return up * (1f - d * d);
        }

        /// <summary>
        /// The body's tilt: 0 upright, 1 lying along the pull line with the head away from Pain, below 0 leaning toward
        /// Pain. 0 before the grip; to -0.3 over the tug; to 0.7 in 0.08 s after the lift (the head snaps back); 0.7 to
        /// 1 between arrive and down. From down it is face-down (<see cref="FaceDown"/>), head toward Pain. A dragged
        /// one stays upright.
        /// </summary>
        public static float Tilt(float s, in BanshoTimes t)
        {
            if (t.heavy || s < t.grip) return 0f;
            if (s < t.lift) return -TugLean * VfxMath.Smooth((s - t.grip) / t.tug);
            if (s < t.arrive) return Mathf.Lerp(-TugLean, 0.7f, VfxMath.Smooth((s - t.lift) / 0.08f));
            if (s < t.down) return Mathf.Lerp(0.7f, 1f, (s - t.arrive) / (t.down - t.arrive));
            return 1f;
        }

        /// <summary>From <see cref="BanshoTimes.down"/> the target lies face-down with its head toward Pain (never a dragged one).</summary>
        public static bool FaceDown(float s, in BanshoTimes t) => !t.heavy && s >= t.down;

        /// <summary>In the air with its afterimages: from the lift until down.</summary>
        public static bool Flying(float s, in BanshoTimes t) => !t.heavy && s >= t.lift && s < t.down;

        /// <summary>
        /// The unit way the head points from the body on screen (x east, y north) for a <paramref name="tilt"/>:
        /// north when upright, turned toward <paramref name="back"/> (the unit way away from Pain) as tilt rises, toward
        /// Pain when tilt is below 0.
        /// </summary>
        public static Vector2 Head(float tilt, Vector2 back)
        {
            var h = new Vector2(back.x * tilt, 1f - Mathf.Abs(tilt) + back.y * tilt);
            float l = h.magnitude;
            return l > 0f ? h / l : new Vector2(0f, 1f);
        }

        /// <summary>
        /// The way the head points at <paramref name="s"/> (unit, x east, y north): <see cref="Head"/> of the tilt, and
        /// toward Pain once face-down. <paramref name="aim"/> is the unit way from Pain to the target.
        /// </summary>
        public static Vector2 HeadDirection(float s, in BanshoTimes t, Vector2 aim) =>
            FaceDown(s, t) ? -aim : Head(Tilt(s, t), aim);

        /// <summary>
        /// <see cref="HeadDirection"/> as degrees clockwise from north, the turn the stand-in's body disc is given
        /// (0 upright, 90 head east).
        /// </summary>
        public static float HeadDegrees(float s, in BanshoTimes t, Vector2 aim)
        {
            Vector2 h = HeadDirection(s, t, aim);
            return Mathf.Atan2(h.x, h.y) * Mathf.Rad2Deg;
        }

        /// <summary>Cells the blocker is pushed toward Pain when it is hit: 0.35, ease-out over 0.2 s from arrive.</summary>
        public static float KnockAt(float s, in BanshoTimes t) =>
            t.blocked ? Knock * EaseOut((s - t.arrive) / KnockTime) : 0f;

        /// <summary>A dragged beast's shake across the line while it is pulled, cells (the sketch's stand-in).</summary>
        public static float HeavyJitter(float s, in BanshoTimes t) =>
            t.heavy && s >= t.grip && s < t.arrive ? Mathf.Sin(s * 60f) * 0.03f : 0f;

        /// <summary>The preview's blocker walks into the line from 1.8 cells to the side, 0.5 s before the grip, over 0.55 s.</summary>
        public static float BlockerSide(float s, in BanshoTimes t) =>
            1.8f * (1f - VfxMath.Smooth((s - (t.grip - 0.5f)) / 0.55f));

        /// <summary>The lasting marks' alpha: 1 until end - Tail, then down to 0 at end.</summary>
        public static float Lasting(float s, in BanshoTimes t) => 1f - VfxMath.Smooth((s - (t.end - Tail)) / Tail);

        /// <summary>1 - (1 - x)³ of x clamped to 0..1 (lib/chain-sickle.js easeOut).</summary>
        public static float EaseOut(float x)
        {
            float u = 1f - Mathf.Clamp01(x);
            return 1f - u * u * u;
        }
    }
}
