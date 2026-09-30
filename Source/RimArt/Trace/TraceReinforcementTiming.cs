using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Reinforcement's picture timing and sizes (Tools/VfxLab/web/sketches/trace-reinforcement.js). The cast is
    /// the ability's warmup; the circuit runs out from the chest over its first 55 %, the weapon's edge lights
    /// from the grip from 45 % to the end, and the lines fade over <see cref="LinesFade"/> s after it. While the
    /// buff lasts the edge keeps a pulsing glow and a moving pawn leaves footprints; when it ends the lines run
    /// back into the chest and the glow fades.
    ///
    /// The run at the raider and the two hits are the preview's script (the sketch's stand-ins): in game the
    /// pawn moves as it is ordered and each melee hit it lands while reinforced gets the slash.
    /// </summary>
    public static class TraceReinforcementTiming
    {
        /// <summary>The sketch's defaults: the cast, the buff's end in the preview (20 s in game), the copy's size in the lab.</summary>
        public const float Cast = 0.5f, EndAt = 3.6f, Size = 1.1f, Distance = 7f;
        public const float Bright = 0.9f, Width = 0.022f;
        /// <summary>RimWorld's base walking speed in cells/s, the sketch's boost and melee reach: the preview's run.</summary>
        public const float Speed = 4.6f, Boost = 1.3f, Reach = 0.9f;
        /// <summary>The preview's script: the cast starts at CastAt; hits come HitGap apart and swing for Swing.</summary>
        public const float CastAt = 0.2f, HitGap = 0.9f, Swing = 0.14f;
        /// <summary>A footprint every Step s, lasting Print s.</summary>
        public const float Step = 0.16f, Print = 0.45f;
        /// <summary>The edge's glow while the buff lasts (share of full) and its pulse (radians a second).</summary>
        public const float Idle = 0.7f, Pulse = 7.5f;
        public const float LinesFade = 0.4f, EndLines = 0.4f, GlowFade = 0.3f, Ring = 0.45f, Slash = 0.3f, Spark = 0.15f, Glint = 0.25f;

        /// <summary>When the lines have run out, when the edge starts to light and for how long, when the cast is done: from the cast's start.</summary>
        public static float Lines(float cast) => 0.55f * cast;
        public static float Climb(float cast) => 0.45f * cast;
        public static float ClimbTime(float cast) => 0.55f * cast;

        /// <summary>The edge's glow, 0..1, <paramref name="sinceLit"/> s after the cast was done: full, settling to the pulsing idle.</summary>
        public static float GlowLevel(float sinceLit, float clock) =>
            Mathf.Lerp(1f, Idle * (0.75f + 0.25f * Mathf.Sin(clock * Pulse)), Smooth(sinceLit / GlowFade));

        // ---- the preview's script ------------------------------------------------------------------------------

        /// <summary>The run starts, reaches the raider, the two hits land, the buff ends, the preview ends.</summary>
        public static float Run => CastAt + Cast + 0.1f;
        public static float Path => Distance - Reach;
        public static float Arrive => Run + Path / (Speed * Boost);
        public static float Hit(int i) => Arrive + 0.15f + i * HitGap;
        public static float End => Mathf.Max(EndAt, Hit(1) + 0.4f);
        public static float Duration => End + 0.8f;
        public static float Lit => CastAt + Cast;

        /// <summary>How far along the run the pawn is at <paramref name="s"/>.</summary>
        public static float Walked(float s) => Mathf.Min(Path, Mathf.Max(0f, s - Run) * Speed * Boost);

        /// <summary>The age of the swing under way at <paramref name="s"/>, or -1.</summary>
        public static float SwingAge(float s)
        {
            for (int i = 0; i < 2; i++)
            {
                float age = s - Hit(i);
                if (age >= 0f && age < Swing) return age;
            }
            return -1f;
        }
    }
}
