using UnityEngine;

namespace RimArt
{
    /// <summary>A point of the sling: where it is over the ground, in cells east and north of the launch cell, and how high.</summary>
    public struct SlingPoint
    {
        public Vector2 ground;
        public float height;

        /// <summary>Where it is drawn: its height is drawn north.</summary>
        public Vector2 At => ground + new Vector2(0f, height * SixPathsHeight.Lift);
    }

    /// <summary>One puff of dust on the ground, in cells east and north of the launch cell.</summary>
    public struct SlingDust
    {
        public Vector2 at;
        public float width, depth, alpha;
    }

    /// <summary>
    /// Pure clock for Repulse Step, the rope sling: two orbs leave the sage's ring and become padded
    /// posts behind it, five ropes between the posts load against the sage's back, then snap and
    /// send it off level along the launch direction; the posts reform into two orbs that fly after
    /// the sage and sit in their slots where it stopped. Seconds in, geometry out; nothing here
    /// draws or touches the map.
    ///
    /// The posts stand on their true cells for every direction. Launching east or west, the ropes'
    /// span and their height share the screen axis, so the ropes fan back along the launch axis by
    /// height to stay separate.
    ///
    /// The numbers are the ones picked in the VFX lab's Repulse Step sketch
    /// (Tools/VfxLab/web/sketches/six-paths-repulse-launch.js). There is no ability behind it yet.
    /// </summary>
    public static class SixPathsRepulseTiming
    {
        public const float Gather = 0.55f, Form = 0.40f, Load = 0.40f, Dash = 0.45f, Brake = 0.18f, Recall = 0.65f, Settle = 0.40f;
        /// <summary>Cells the sage is sent.</summary>
        public const float Distance = 5f;
        /// <summary>Post half height, the gap between the posts, pad thickness, and how far the ropes stretch, in cells.</summary>
        public const float Size = 0.9f, Spacing = 2.6f, Pad = 0.26f, Compression = 0.38f;
        /// <summary>The posts stand this far along the launch axis, which is behind the sage.</summary>
        public const float PostAlong = -0.85f;
        /// <summary>The height of the rope at the sage's back.</summary>
        public const float ContactHeight = 0.65f;
        public const float TrailStrength = 0.7f, Shake = 0.09f;
        public const int Ropes = 5, RopePoints = 49, PostRows = 25, GatherPoints = 18, RecallPoints = 16,
            Slipstreams = 3, SlipstreamPoints = 24, DustPuffs = 22;

        public static float FormAt => Gather;
        public static float LoadAt => FormAt + Form;
        public static float LaunchAt => LoadAt + Load;
        public static float StopAt => LaunchAt + Dash;
        public static float RecallAt => StopAt + Brake;
        public static float Duration => RecallAt + Recall + Settle;

        /// <summary>The height the orbs gather at and the posts grow from, and the height of a grown post's top.</summary>
        public static float CentreHeight => Mathf.Max(1.05f, Size + 0.12f);
        public static float PostTop => CentreHeight + Size;
        public static float Half => Spacing * 0.5f;

        private static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);

        public static float Gathered(float seconds) => Smooth(seconds / Gather);
        public static float Formed(float seconds) => Smooth((seconds - FormAt) / Form);
        public static float Loaded(float seconds) => Smooth((seconds - LoadAt) / Load);
        public static float Recalled(float seconds) => Smooth((seconds - RecallAt) / Recall);
        /// <summary>Posts and ropes: 0 none, 1 full grown; it falls again as they reform into orbs.</summary>
        public static float Growth(float seconds) => Formed(seconds) * (1f - Recalled(seconds));
        /// <summary>Seconds since the release; negative before.</summary>
        public static float Age(float seconds) => seconds - LaunchAt;

        /// <summary>Cells the loaded ropes are pushed back at their middle.</summary>
        public static float Pressure(float seconds) =>
            Compression * Loaded(seconds) * (1f - Smooth(Age(seconds) / 0.13f));

        /// <summary>Cells the ropes swing forward past rest after the release.</summary>
        public static float Rebound(float seconds)
        {
            float age = Age(seconds);
            return age >= 0f ? Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(age / 0.28f) * Mathf.PI)) * 0.24f * Mathf.Exp(-age * 4f) : 0f;
        }

        /// <summary>
        /// The preview's script for the pawn: cells along the launch axis from the launch cell. It
        /// backs into the ropes while they load, then a short acceleration, a fast middle and gentle
        /// braking. In the game the pawn's own flight says where it is.
        /// </summary>
        public static float Travel(float seconds)
        {
            float braced = PostAlong - Compression + 0.12f;
            if (seconds < LaunchAt) return braced * Loaded(seconds);
            float u = Mathf.Clamp01((seconds - LaunchAt) / Dash);
            float f = u < 0.2f ? 3.125f * u * u : u < 0.8f ? 1.25f * u - 0.125f : 1f - 3.125f * (1f - u) * (1f - u);
            return Mathf.Lerp(braced, Distance, f);
        }

        /// <summary>A ground point <paramref name="along"/> the launch direction and <paramref name="across"/> it, from the launch cell.</summary>
        public static Vector2 Ground(Vector2 toward, float along, float across = 0f) =>
            new Vector2(toward.x * along - toward.y * across, toward.y * along + toward.x * across);

        /// <summary>True launching east or west, where the ropes fan by height and draw over the posts.</summary>
        public static bool SideView(Vector2 toward) => Mathf.Abs(toward.x) > 0.5f;

        /// <summary>A point on post <paramref name="side"/> (-1 or 1) at <paramref name="height"/>.</summary>
        public static SlingPoint Anchor(Vector2 toward, int side, float height) =>
            new SlingPoint { ground = Ground(toward, PostAlong, side * Half), height = height };

        /// <summary>Row <paramref name="row"/> of a post, which grows up and down from <see cref="CentreHeight"/>; and the pad's width there.</summary>
        public static SlingPoint PostRow(Vector2 toward, int side, int row, float seconds, out float bulge)
        {
            float u = row / (float)(PostRows - 1);
            bulge = Pad * (0.72f + 0.28f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)));
            return Anchor(toward, side, Mathf.Lerp(CentreHeight, u * PostTop, Growth(seconds)));
        }

        /// <summary>How firmly a post's foot is in the ground, 0 to 1.</summary>
        public static float Planted(float seconds) => Smooth((Formed(seconds) - 0.35f) / 0.65f) * (1f - Recalled(seconds));

        /// <summary>The height of the two violet bindings on a post.</summary>
        public static float BindingHeight(int binding, float seconds) =>
            Mathf.Lerp(CentreHeight, PostTop * (binding == 1 ? 0.90f : 0.12f), Growth(seconds));

        /// <summary>The height rope <paramref name="rope"/> ends up at: one low, one at the sage's back, three up the post.</summary>
        public static float RopeHeight(int rope, float seconds)
        {
            float full = rope == 0 ? 0.23f : rope == 1 ? ContactHeight : Mathf.Lerp(ContactHeight, PostTop - 0.16f, (rope - 1) / 3f);
            return Mathf.Lerp(CentreHeight, full, Growth(seconds));
        }

        /// <summary>Point <paramref name="j"/> of rope <paramref name="rope"/>. Its ends stay on the posts; its middle tracks the sage's back.</summary>
        public static SlingPoint RopePoint(Vector2 toward, int rope, int j, float seconds)
        {
            float u = j / (float)(RopePoints - 1), height = RopeHeight(rope, seconds), age = Age(seconds);
            // Sin of pi is a hair below zero in single precision, and a power of that is not a number.
            float arch = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)), bow = Mathf.Pow(arch, 1.6f);
            float vibration = age > 0f ? Mathf.Sin(u * Mathf.PI * 3f) * Mathf.Sin(age * 38f - rope * 0.7f) * 0.07f * Mathf.Exp(-age * 7f) : 0f;
            float fan = SideView(toward) ? (height - ContactHeight) * 0.30f * arch : 0f;
            float along = PostAlong - fan - (Pressure(seconds) - Rebound(seconds)) * bow + vibration;
            return new SlingPoint
            {
                ground = Ground(toward, along, (u * 2f - 1f) * Half),
                height = height - 0.035f * arch * (1f - Loaded(seconds)),
            };
        }

        /// <summary>Where the middle rope meets the sage's back, which is where the release flashes.</summary>
        public static SlingPoint Contact(Vector2 toward, float seconds) => new SlingPoint
        {
            ground = Ground(toward, PostAlong - Pressure(seconds) + Rebound(seconds)), height = ContactHeight,
        };

        /// <summary>Point <paramref name="j"/> of slipstream <paramref name="stream"/>, which trails 0.19 s of the flight.</summary>
        public static SlingPoint Slipstream(Vector2 toward, int stream, int j, float seconds)
        {
            float then = Mathf.Max(LaunchAt, seconds - (1f - j / (float)(SlipstreamPoints - 1)) * 0.19f);
            return new SlingPoint { ground = Ground(toward, Travel(then), (stream - 1) * 0.18f), height = 0.45f + stream * 0.22f };
        }

        public static float SlipstreamAlpha(float seconds) => TrailStrength * 0.38f * (1f - Smooth((seconds - StopAt) / 0.22f));

        /// <summary>One puff of dust left on the ground where the sage passed; alpha 0 when it is not showing.</summary>
        public static SlingDust Dust(Vector2 toward, int index, float seconds)
        {
            float emitted = LaunchAt + index / (float)DustPuffs * Dash, u = (seconds - emitted) / 0.48f;
            if (u < 0f || u > 1f) return default;
            return new SlingDust
            {
                at = Ground(toward, Travel(emitted) - u * 0.18f, (SixPathsBloomTiming.Rand(index + 44) - 0.5f) * (0.45f + u * 0.8f)),
                width = 0.22f + u * 0.45f, depth = 0.16f + u * 0.3f,
                alpha = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * 0.26f,
            };
        }

        /// <summary>How far gather trail point <paramref name="j"/> is from the slot to the anchor.</summary>
        public static float GatherTrail(int j, float seconds) =>
            Smooth(Mathf.Max(0f, seconds - (1f - j / (float)(GatherPoints - 1)) * 0.16f) / Gather);

        /// <summary>How far recall trail point <paramref name="j"/> is from the anchor to the slot.</summary>
        public static float RecallTrail(int j, float seconds) =>
            Smooth((seconds - (1f - j / (float)(RecallPoints - 1)) * 0.12f - RecallAt) / Recall);
    }
}
