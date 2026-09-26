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
        /// <summary>
        /// Height of the block's centre. <see cref="SixPathsSlab.Rest"/> is standing on the ground;
        /// lower than that is sunk into it, and the part below the ground is not drawn.
        /// </summary>
        public float height;
        /// <summary>Degrees about the vertical. It is the same number for the whole of the effect.</summary>
        public float yaw;
        public float scale, alpha;
    }

    /// <summary>One puff of dust or one chunk of debris, relative to the target cell.</summary>
    public struct ImpactParticle
    {
        /// <summary>Cells east and north of the target, on the ground.</summary>
        public float x, z;
        /// <summary>Cells above the ground.</summary>
        public float height;
        public float size, rotation, alpha;
    }

    /// <summary>
    /// Pure clock for the slam: six orbs spiral up to one point in the sky, fuse into a standing
    /// block, hang, come down on the cell they left, stand, and sink into the ground. Seconds in,
    /// geometry out; nothing here draws or touches the map, so the whole sequence is checked in
    /// Tests/SixPaths.
    ///
    /// The numbers are the ones picked in the VFX lab's Slam v2 sketch
    /// (Tools/VfxLab/web/sketches/six-paths-slam-v2.js), exit mode "sink".
    ///
    /// The fall is a translation and nothing else. The block does not tumble, tip, spin or settle:
    /// it holds the attitude it formed in until it is gone. A block that rotates on the way down
    /// ends up showing the camera its top face, and a top face seen from above is a flat rectangle.
    ///
    /// 9.5 cells in 0.22 s, from the apex centre at 12.5 to the resting centre at 3, averages
    /// 43 cells a second and ends at 86: about 1.4 cells between frames at 60 fps at the moment
    /// it lands. The two motion ghosts cover that gap.
    /// </summary>
    public static class SixPathsSlamTiming
    {
        public const float Gather = 0.95f, Fuse = 0.18f, Hang = 0.55f, Fall = 0.22f,
            Hold = 0.90f, ExitSeconds = 1.20f;
        /// <summary>Seconds the cracks and the debris on the floor take to fade after the block is gone.</summary>
        public const float MarksFade = 0.60f;

        /// <summary>Height of the block's centre where the orbs meet and it forms.</summary>
        public const float Apex = 12.5f;
        public const float StartRadius = 2.8f, StartHeight = 0.30f, OrbSize = 0.42f;
        /// <summary>Degrees the orbs sweep through on the way in.</summary>
        public const float Sweep = 210f;
        /// <summary>
        /// The one angle the block is ever drawn at. At 0 it is square to the camera and shows two
        /// faces, its front and its top; the outline and the sun shadow carry the rest.
        /// </summary>
        public const float Yaw = 0f;

        /// <summary>Cells the block drives into the ground on landing, and the seconds it takes.</summary>
        public const float Sink = 0.20f, SinkTime = 0.06f;
        public const float Shake = 0.20f;
        /// <summary>Peak opacity of the floor flash and of the shock ring.</summary>
        public const float Flash = 0.32f, Ring = 0.70f;
        public const int Puffs = 40, Debris = 14, Cracks = 9;
        public const float FlashSeconds = 0.22f, RingSeconds = 0.55f, RingReach = 7.5f;
        /// <summary>Puffs in the skirt that hides where the block meets the ground while it sinks.</summary>
        public const int SkirtPuffs = 10;

        public static float FuseAt => Gather;
        public static float HangAt => FuseAt + Fuse;
        public static float FallAt => HangAt + Hang;
        public static float LandAt => FallAt + Fall;
        public static float ExitAt => LandAt + Hold;
        /// <summary>The block's top reaches the ground.</summary>
        public static float GoneAt => ExitAt + ExitSeconds;
        public static float Duration => GoneAt + MarksFade;

        /// <summary>Kept for callers outside this branch's reach (UBW, Chain Sickle); new code calls VfxMath.Smooth.</summary>
        public static float Smooth(float t) => VfxMath.Smooth(t);

        private static float Progress(float seconds, float start, float span) =>
            Mathf.Clamp01((seconds - start) / span);

        /// <summary>
        /// 0 to 1, fixed per (index, key). The same integer hash as the lab's standins.js, so the
        /// cracks, dust and debris land where they did in the sketch. Visual scatter must not
        /// consume gameplay RNG.
        /// </summary>
        public static float Rand(int index, int key)
        {
            const int seed = 4242;
            unchecked
            {
                int h = index * 374761393 + key * 668265263 + seed * 144665;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967295f;
            }
        }

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
                // A slow rise and fall of a third of a cell, so the block is held rather than parked.
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

            // Landed: driven 0.2 cells in by the impact, then after the hold the rest of the way,
            // until the top is level with the ground.
            float sunk = Sink * Smooth(Progress(seconds, LandAt, SinkTime))
                + (SixPathsSlab.Height - Sink) * Smooth(Progress(seconds, ExitAt, ExitSeconds));
            pose.height = SixPathsSlab.Rest - sunk;
            if (SixPathsSlab.Height - sunk <= 0.01f) pose.alpha = 0f;
            return pose;
        }

        /// <summary>Cells of daylight under the block, which the shadows and the size gain are
        /// measured from. Zero once it has landed, sunk or not.</summary>
        public static float Clearance(in SlabPose pose) =>
            Mathf.Max(0f, pose.height - SixPathsSlab.Rest * pose.scale);

        /// <summary>
        /// The size the block is actually drawn at. Things nearer the camera are bigger, and the
        /// block is 9.5 cells nearer at the apex than when it lands, which is what turns the drop
        /// into an approach. The gain is measured from the block's underside rather than its
        /// centre, so it is back to 1 the moment the block touches the floor.
        /// </summary>
        public static float DrawScale(in SlabPose pose) =>
            pose.scale * SixPathsHeight.Scale(Clearance(pose));

        /// <summary>The flash that covers six orbs becoming one block, 0 to 1.</summary>
        public static float FuseFlash(float seconds)
        {
            float t = Progress(seconds, FuseAt, Fuse * 1.6f);
            return t <= 0f || t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI);
        }

        /// <summary>
        /// Width of the seam lines, as a multiple of their full width: 1 at the fuse, 0.35 from
        /// 1.2 s after it. They mark the six orbs the block was made of, and fade into the block.
        /// </summary>
        public static float SeamWeight(float seconds) =>
            0.35f + 0.65f * (1f - Progress(seconds, FuseAt, 1.2f));

        public static float ImpactFlash(float seconds)
        {
            float since = seconds - LandAt;
            if (since < 0f || since >= FlashSeconds) return 0f;
            float left = 1f - since / FlashSeconds;
            return Flash * left * left;
        }

        /// <summary>Radius of the floor flash, which widens as it fades.</summary>
        public static float ImpactFlashRadius(float seconds) =>
            Mathf.Lerp(2.5f, 5f, (seconds - LandAt) / FlashSeconds);

        public static float RingRadius(float seconds) =>
            Mathf.Lerp(SixPathsSlab.Width * 0.6f, RingReach, Smooth(Progress(seconds, LandAt, RingSeconds)));

        public static float RingAlpha(float seconds)
        {
            if (seconds < LandAt) return 0f;
            float t = Progress(seconds, LandAt, RingSeconds);
            return t >= 1f ? 0f : Ring * (1f - t) * (1f - t);
        }

        /// <summary>Opacity of what the impact leaves on the floor: cracks and settled debris.</summary>
        public static float MarksAlpha(float seconds) =>
            seconds < LandAt ? 0f : 1f - Smooth(Progress(seconds, GoneAt, MarksFade));

        /// <summary>How far the cracks have run, 0 to 1 over the first 0.1 s after landing.</summary>
        public static float CrackGrowth(float seconds) => Smooth(Progress(seconds, LandAt, 0.1f));

        /// <summary>
        /// One crack: <paramref name="step"/> of four jagged segments running out from the base.
        /// </summary>
        public static void CrackSegment(int crack, int step, float growth, out Vector2 from, out Vector2 to)
        {
            const int steps = 4;
            float angle = crack / (float)Cracks * Mathf.PI * 2f + Rand(crack, 1) * 0.6f;
            float stride = (1.2f + Rand(crack, 2) * 1.6f) * growth / steps;
            float x = Mathf.Cos(angle) * SixPathsSlab.Width * 0.55f, z = Mathf.Sin(angle) * SixPathsSlab.Depth * 0.55f;
            from = to = new Vector2(x, z);
            for (int k = 0; k <= step; k++)
            {
                float turn = angle + (Rand(crack, 10 + k) - 0.5f) * 0.9f;
                from = to;
                to = new Vector2(from.x + Mathf.Cos(turn) * stride, from.y + Mathf.Sin(turn) * stride);
            }
        }

        public static float CrackWidth(int step) => Mathf.Lerp(0.09f, 0.025f, step / 4f);

        /// <summary>
        /// A puff of dust rolling out from the base and rising a little. Alpha is 0 before the
        /// landing and after the puff's own life of 0.9 to 1.5 s.
        /// </summary>
        public static ImpactParticle Puff(int index, float seconds)
        {
            float since = seconds - LandAt, life = 0.9f + Rand(index, 20) * 0.6f, u = since / life;
            if (since < 0f || u >= 1f) return default;
            float angle = index / (float)Puffs * Mathf.PI * 2f + Rand(index, 21);
            float edge = Mathf.Max(SixPathsSlab.Width, SixPathsSlab.Depth) * 0.55f;
            float reach = edge + (0.6f + Rand(index, 22) * 1.8f) * Smooth(Mathf.Min(1f, u * 1.8f));
            return new ImpactParticle
            {
                x = Mathf.Cos(angle) * reach,
                z = Mathf.Sin(angle) * reach * 0.8f,
                height = u * (0.3f + Rand(index, 23) * 0.5f),
                size = Mathf.Lerp(0.6f, 1.9f, Mathf.Sqrt(u)) * (0.8f + Rand(index, 24) * 0.5f),
                rotation = Rand(index, 25) * 360f + u * 40f,
                alpha = 0.55f * Mathf.Sin(Mathf.Min(1f, u * 4f) * Mathf.PI * 0.5f) * Mathf.Pow(1f - u, 1.4f),
            };
        }

        /// <summary>
        /// A chunk of dirt thrown up at the landing, falling back under 22 cells/s² and lying
        /// where it lands until the marks fade.
        /// </summary>
        public static ImpactParticle Chunk(int index, float seconds)
        {
            float since = seconds - LandAt;
            if (since < 0f) return default;
            const float gravity = 22f;
            float angle = Rand(index, 30) * Mathf.PI * 2f, speed = 1.4f + Rand(index, 31) * 2.6f;
            float rise = 3f + Rand(index, 32) * 4f;
            float flight = 2f * rise / gravity, t = Mathf.Min(since, flight);
            float reach = Mathf.Max(SixPathsSlab.Width, SixPathsSlab.Depth) * 0.5f + speed * t;
            return new ImpactParticle
            {
                x = Mathf.Cos(angle) * reach,
                z = Mathf.Sin(angle) * reach,
                height = Mathf.Max(0f, rise * t - 0.5f * gravity * t * t),
                size = 0.1f + Rand(index, 33) * 0.16f,
                rotation = Rand(index, 34) * 360f + t * 720f * (Rand(index, 35) - 0.5f),
                alpha = MarksAlpha(seconds) * (since > flight ? 0.85f : 1f),
            };
        }

        /// <summary>
        /// The dust skirt round the base while the block sinks: puffs circling slowly at the
        /// block's edge, brightest halfway through the exit.
        /// </summary>
        public static ImpactParticle SkirtPuff(int index, float seconds)
        {
            float since = seconds - ExitAt, u = since / ExitSeconds;
            if (since < 0f || u >= 1f) return default;
            float angle = index / (float)SkirtPuffs * Mathf.PI * 2f + since * 0.8f;
            float radius = Mathf.Max(SixPathsSlab.Width, SixPathsSlab.Depth) * 0.6f;
            return new ImpactParticle
            {
                x = Mathf.Cos(angle) * radius,
                z = Mathf.Sin(angle) * radius * 0.8f,
                rotation = angle * 57f + since * 30f,
                alpha = 0.45f * Mathf.Sin(u * Mathf.PI),
            };
        }
    }

    /// <summary>
    /// How the block is drawn, as numbers: face values, edges and shadows. Picked in the VFX lab's
    /// Slam v2 sketch alongside <see cref="SixPathsSlamTiming"/>.
    /// </summary>
    public static class SixPathsSlamLook
    {
        /// <summary>Face colour, 0 = the orb's near-black body, 1 = its violet tint.</summary>
        public const float Top = 0.08f, Front = 0.05f, Side = 0.08f;
        /// <summary>Five lines across each upright face, splitting its height into six: one per orb.</summary>
        public const bool Seams = true;

        /// <summary>Opacity of the silhouette edges; the inner edges are this times <see cref="Inner"/>.</summary>
        public const float Outline = 0.80f, Inner = 0.41f;
        /// <summary>Cells across. The glow is drawn only along the silhouette.</summary>
        public const float EdgeWidth = 0.02f, GlowWidth = 0.07f;
        public const float GlowAlpha = 0.23f;

        public const float SunShadow = 0.38f, Contact = 0.50f;
        /// <summary>Cells the shadow edges are softened over, on the ground.</summary>
        public const float Softness = 0.14f;

        public static float FaceValue(int face, int front) =>
            face == SixPathsSlab.Top ? Top : face == front ? Front : Side;

        /// <summary>The sun shadow, thinning to 35% of its strength at the ceiling.</summary>
        public static float SunStrength(float clearance) =>
            SunShadow * Mathf.Lerp(1f, 0.35f, clearance / SixPathsHeight.Ceiling);

        public static float SunSoftness(float clearance) => Softness * (1f + clearance * 0.25f);

        /// <summary>The shadow straight below: faint and wide high up, dark and tight as it arrives.</summary>
        public static float ContactStrength(float clearance)
        {
            float near = SixPathsHeight.Nearness(clearance);
            return Contact * near * near;
        }

        public static float ContactSpread(float clearance) => 1f + (1f - SixPathsHeight.Nearness(clearance)) * 0.6f;

        public static float ContactSoftness(float clearance) =>
            Softness * (1f + (1f - SixPathsHeight.Nearness(clearance)) * 4f);
    }
}
