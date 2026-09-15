using UnityEngine;

namespace RimArt
{
    /// <summary>Where one orb of the gather is at a given moment, in polar terms about the target.</summary>
    public struct GatherStep
    {
        /// <summary>Radians about the target cell.</summary>
        public float angle;
        /// <summary>Cells out from it, on the ground.</summary>
        public float radius;
        /// <summary>Cells above the ground.</summary>
        public float height;
        public float size, alpha;
    }

    /// <summary>How the block sits at a given moment.</summary>
    public struct SlabPose
    {
        /// <summary>Height of the block's centre. <see cref="SixPathsSlab.Rest"/> is standing on the ground.</summary>
        public float height;
        /// <summary>Degrees about the vertical. It is the same number for the whole of the fall.</summary>
        public float yaw;
        public float scale, alpha;
    }

    /// <summary>
    /// Pure clock for the slam: six orbs spiral up to one point in the sky, fuse into a standing
    /// block, hang, and come down on the cell they left. Seconds in, geometry out; nothing here
    /// draws or touches the map, so the whole sequence is checked in Tests/SixPaths.
    ///
    /// The fall is a translation and nothing else. The block does not tumble, tip, spin or settle:
    /// it holds the attitude it formed in until it is gone, the way a dropped refrigerator arrives
    /// in the pose it left in. Everything about the landing is carried by the floor instead --
    /// the flash, the ring, the dust -- because a block that rotates on the way down ends up
    /// showing the camera its top face, and a top face seen from above is a flat rectangle.
    ///
    /// Six cells in 0.22 s is about 27 cells a second, which at 60 fps is nearly half a cell
    /// between frames: fast enough to read as dropped rather than lowered, slow enough that the
    /// block is drawn half a dozen times on the way down and the motion ghosts have something to
    /// trail.
    /// </summary>
    public static class SixPathsSlamTiming
    {
        public const float Gather = 0.95f, Fuse = 0.18f, Hang = 0.55f, Fall = 0.22f,
            Burst = 0.55f, Settle = 1.20f;

        /// <summary>Height of the block's centre where the orbs meet and it forms.</summary>
        public const float Apex = 9f;
        public const float StartRadius = 2.8f, StartHeight = 0.30f, OrbSize = 0.42f;
        /// <summary>Degrees the orbs sweep through on the way in.</summary>
        public const float Sweep = 210f;
        /// <summary>
        /// The one angle the block is ever drawn at. Square to the camera a box shows two faces,
        /// its front and its top; turned a little it shows three, and three planes at three angles
        /// to the light is what a solid looks like. Twenty-five degrees is enough for the third
        /// face to be a face rather than a sliver, and little enough that the block still reads as
        /// standing square on the cell it hit.
        /// </summary>
        public const float Yaw = 25f;

        public static float FuseAt => Gather;
        public static float HangAt => FuseAt + Fuse;
        public static float FallAt => HangAt + Hang;
        public static float LandAt => FallAt + Fall;
        public static float FadeAt => LandAt + Burst;
        public static float Duration => FadeAt + Settle;

        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Progress(float seconds, float start, float span) =>
            Mathf.Clamp01((seconds - start) / span);

        /// <summary>
        /// One orb on its way in. The six share a clock and differ only by their starting angle,
        /// so they arrive together: the fuse is an event, not six separate arrivals.
        /// </summary>
        public static GatherStep Orb(int index, int count, float seconds)
        {
            float t = Progress(seconds, 0f, Gather);
            // Pulled in rather than flown in: the radius closes late and fast while the climb is
            // even, so the orbs rise in formation and then snap together at the top.
            float inward = t * t * t;
            float climb = Smooth(t);
            return new GatherStep
            {
                angle = index / (float)count * Mathf.PI * 2f + t * Sweep * Mathf.Deg2Rad,
                radius = Mathf.Lerp(StartRadius, 0f, inward),
                height = Mathf.Lerp(StartHeight, Apex, climb),
                size = OrbSize * Mathf.Lerp(1f, 0.72f, inward),
                // They are gone the moment the block exists; the fuse flash covers the swap.
                alpha = seconds >= FuseAt ? 0f : 1f,
            };
        }

        public static SlabPose Slab(float seconds)
        {
            // One yaw, start to finish. Nothing below is allowed to change it.
            var pose = new SlabPose { height = Apex, yaw = Yaw, scale = 1f, alpha = 1f };
            if (seconds < FuseAt) { pose.alpha = 0f; pose.scale = 0f; return pose; }

            if (seconds < HangAt)
            {
                // Forms at the size it will keep, with one overshoot. Growing into it from nothing
                // would read as a bubble rather than as six orbs becoming one solid.
                float t = Progress(seconds, FuseAt, Fuse);
                pose.scale = Mathf.Lerp(0.42f, 1f, Smooth(t)) * (1f + 0.26f * Mathf.Sin(t * Mathf.PI));
                pose.alpha = Smooth(t / 0.5f);
                return pose;
            }

            if (seconds < FallAt)
            {
                // It hangs, and the hang is the only part of the sequence that is not a straight
                // line: a slow rise and fall of a third of a cell, so the block is held rather
                // than parked.
                float t = Progress(seconds, HangAt, Hang);
                pose.height = Apex + 0.30f * Mathf.Sin(t * Mathf.PI * 2f);
                return pose;
            }

            if (seconds < LandAt)
            {
                float left = 1f - Progress(seconds, FallAt, Fall);
                // Distance left falls off as the square of the time left, which is a drop under
                // gravity read backwards from the landing.
                pose.height = SixPathsSlab.Rest + (Apex - SixPathsSlab.Rest) * left * left;
                return pose;
            }

            // Landed, standing, in the attitude it formed in. It does not bounce or settle:
            // everything that says it arrived hard happens on the floor around it.
            pose.height = SixPathsSlab.Rest;
            pose.alpha = 1f - Smooth(Progress(seconds, FadeAt, Settle));
            return pose;
        }

        /// <summary>Cells of daylight under the block, which is what the shadow and the size gain
        /// are both measured from.</summary>
        public static float Clearance(in SlabPose pose) =>
            Mathf.Max(0f, pose.height - SixPathsSlab.Rest * pose.scale);

        /// <summary>
        /// The size the block is actually drawn at. Things nearer the camera are bigger, and the
        /// block is nine cells nearer at the apex than when it lands, which is what turns the drop
        /// into an approach. The gain is measured from the block's underside rather than its
        /// centre: measured from the centre it would still be growing when the block is already
        /// lying down, and a block grown about its centre on the floor is a block drawn through it.
        /// </summary>
        public static float DrawScale(in SlabPose pose) =>
            pose.scale * SixPathsHeight.Scale(Clearance(pose));

        /// <summary>The white flash that covers six orbs becoming one block.</summary>
        public static float FuseFlash(float seconds)
        {
            float t = Progress(seconds, FuseAt, Fuse * 1.6f);
            return t <= 0f || t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * (1f - t * 0.4f);
        }

        /// <summary>The flash at the floor, sharper and shorter than the fuse.</summary>
        public static float ImpactFlash(float seconds)
        {
            float t = Progress(seconds, LandAt, 0.26f);
            return t <= 0f || t >= 1f ? 0f : (1f - t) * (1f - t);
        }

        public const float RingReach = 7.5f;

        public static float RingRadius(float seconds) =>
            Mathf.Lerp(SixPathsSlab.HalfWidth, RingReach,
                Smooth(Progress(seconds, LandAt, Burst)));

        public static float RingAlpha(float seconds)
        {
            float t = Progress(seconds, LandAt, Burst);
            return t <= 0f || t >= 1f ? 0f : (1f - t) * (1f - t) * 0.85f;
        }

        /// <summary>Dust thrown out along the floor, and gone well before the block fades.</summary>
        public static float DustAlpha(float seconds)
        {
            float t = Progress(seconds, LandAt, Burst + 0.35f);
            return t <= 0f || t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * 0.7f;
        }

        public static float DustRadius(int index, float seconds)
        {
            float t = Progress(seconds, LandAt, Burst + 0.35f);
            float speed = 0.7f + (index % 5) * 0.14f;
            return Mathf.Lerp(SixPathsSlab.HalfWidth, RingReach * speed, Smooth(t));
        }
    }
}
