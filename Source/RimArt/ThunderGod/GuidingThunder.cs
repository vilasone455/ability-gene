using UnityEngine;

namespace RimArt
{
    /// <summary>One shot of the preview's script that reaches the barrier while it is up.</summary>
    public struct GuidedShot
    {
        /// <summary>Where on the barrier it went in, at chest height.</summary>
        public Vector2 entry;
        /// <summary>The direction from the caster to that point, in degrees.</summary>
        public float degrees;
        /// <summary>When it reached the barrier, and when it came out at the kunai.</summary>
        public float reached, leftAt;
    }

    /// <summary>
    /// Guiding Thunder: when the barrier is written, holds and burns away, and the preview's script
    /// of shots taken at it. The defaults of the lab's kunai-guiding-thunder.js, with the hold
    /// shortened to 3.2 s as it is there. Two shooters fire in turn; with the kunai in one of them,
    /// that one goes down after 4 redirected hits and stops shooting. The shooters and their
    /// tracers are not drawn, and neither are shots that arrive after the barrier has gone.
    /// </summary>
    public static class GuidingThunderTiming
    {
        public const float Lead = 0.4f, FirstShot = 0.2f, ShotSpeed = 35f, Every = 0.3f;
        public const float Write = 0.25f, Hold = 3.2f, Fade = 0.5f, Tail = 1.1f;
        public const float Radius = 1.3f, Spin = 18f, Glint = 0.5f, Distance = 9f;
        public const float RingPitch = 0.2f, RingFrame = 0.16f, GlintTime = 0.16f, LinkTime = 0.07f, LinkWidth = 0.05f,
            ExitDelay = 0.03f, ExitGlint = 0.7f;
        public const int DownAfter = 4, MostShots = 16;

        public static float CastAt => Lead;
        public static float UpAt => CastAt + Write;
        public static float OverAt => UpAt + Hold;
        public static float GoneAt => OverAt + Fade;
        public static float Duration => GoneAt + Tail;
        public static int RingGlyphs => Mathf.RoundToInt(2f * Mathf.PI * Radius / RingPitch);

        private static Vector2 At(Vector2 centre, Vector2 toward, float along, float across) =>
            centre + toward * along + new Vector2(-toward.y, toward.x) * across;

        // The chosen cell is halfway between the caster and the shooters, so both are in view.
        public static Vector2 Caster(Vector2 centre, Vector2 toward) => At(centre, toward, -Distance / 2f, 0f);
        /// <summary>Shooter 0 is unmarked. Shooter 1 is the one with the kunai in it.</summary>
        public static Vector2 Shooter(Vector2 centre, Vector2 toward, int which) =>
            which == 0 ? At(centre, toward, Distance / 2f, 1.7f) : At(centre, toward, Distance / 2f - 1f, -1.9f);
        public static Vector2 GroundKunai(Vector2 centre, Vector2 toward) => At(centre, toward, Distance / 2f - 2.4f, -0.5f);

        /// <summary>
        /// Every shot taken at the barrier, replayed from zero so any time can be drawn. Fills
        /// <paramref name="shots"/> and returns how many.
        /// </summary>
        public static int Shots(Vector2 centre, Vector2 toward, bool inEnemy, GuidedShot[] shots)
        {
            Vector2 target = Caster(centre, toward) + new Vector2(0f, ThunderGodTiming.Chest);
            int count = 0, taken = 0;
            float down = -1f;
            for (int i = 0; count < shots.Length; i++)
            {
                float fire = UpAt + FirstShot + i * Every;
                if (fire > GoneAt + 0.25f) break;
                int shooter = i % 2;
                if (shooter == 1 && down >= 0f && fire >= down) continue;
                Vector2 from = Shooter(centre, toward, shooter) + new Vector2(0f, ThunderGodTiming.Chest), path = target - from;
                float length = path.magnitude, reached = fire + (length - Radius) / ShotSpeed;
                if (reached < UpAt || reached >= OverAt) continue;
                Vector2 along = path / length;
                shots[count] = new GuidedShot
                {
                    entry = target - along * Radius, degrees = ThunderGodTiming.Degrees(-along), reached = reached, leftAt = reached + ExitDelay,
                };
                if (inEnemy && ++taken == DownAfter) down = shots[count].leftAt;
                count++;
            }
            return count;
        }
    }
}
