using UnityEngine;

namespace RimArt
{
    /// <summary>One piece thrown by a ground burst: a dust puff and the dark shard that flies with it.</summary>
    public struct BurstPiece
    {
        /// <summary>The puff, relative to the burst: its drawn point (height included), its size and its alpha.</summary>
        public Vector2 at;
        public float width, depth, puffAlpha;
        /// <summary>The shard: a line from <see cref="tail"/> through <see cref="at"/> to <see cref="nose"/>.</summary>
        public Vector2 tail, nose;
        public float shardWidth, shardAlpha;
    }

    /// <summary>
    /// Pure clock for a small hit on the ground: a flash, a ring that runs outward, and eighteen
    /// dust puffs with shards. It is the VFX lab's impact() helper
    /// (Tools/VfxLab/web/sketches/lib/six-paths-impact.js), number for number. Age in, geometry out.
    /// </summary>
    public static class SixPathsBurstTiming
    {
        public const float Seconds = 0.8f, FlashSeconds = 0.13f;
        public const int Pieces = 18;

        public static bool Showing(float age) => age >= 0f && age <= Seconds;

        public static float Flash(float age) => Mathf.Max(0f, 1f - age / FlashSeconds);

        public static float RingRadius(float age, float size) => size * (0.3f + age * 3f);

        public static float RingAlpha(float age, float strength) => (1f - age / Seconds) * strength * 0.6f;

        /// <summary>False once piece <paramref name="index"/> has lived out its life.</summary>
        public static bool Piece(int index, float age, float strength, float size, float dust, out BurstPiece piece)
        {
            piece = default;
            float life = 0.35f + VfxMath.Rand(index) * 0.4f, u = age / life;
            if (u > 1f) return false;
            float angle = index * 2.399f;
            float reach = size * (0.3f + age * (1.2f + VfxMath.Rand(index + 20) * 2f));
            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // Sin of pi is a hair below zero in single precision; an alpha must not be.
            float arch = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
            piece.at = along * reach + new Vector2(0f, arch * size * 0.65f * SixPathsHeight.Lift);
            piece.width = (0.35f + u) * size;
            piece.depth = (0.3f + u * 0.7f) * size;
            piece.puffAlpha = arch * dust * strength;
            piece.tail = piece.at - along * 0.25f;
            piece.nose = piece.at + along * 0.12f;
            piece.shardWidth = 0.12f * size * (1f - u);
            piece.shardAlpha = strength * (1f - u);
            return true;
        }
    }
}
