using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Black Flash picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/todo-black-flash.js). Seconds in, numbers out, no drawing.
    ///
    /// The clock starts with the warmup; the fist lands at its end (the AbilityDef's 0.2 s,
    /// <see cref="Warmup"/>), which the caller reads from the ability. The burst follows
    /// <see cref="Spark"/> later. None of this is balance: damage, the window, the stun and the zone
    /// are on the AbilityDef and the zone hediff.
    /// </summary>
    public static class BlackFlash
    {
        public const float Warmup = 0.2f;
        /// <summary>Chest height as drawn; the preview's target stands this far from Todo; the stun as drawn.</summary>
        public const float Chest = 0.38f, Reach = 1f, StunTime = 1f;
        /// <summary>The second, smaller burst; the negative flash; its grey; the disc variant's radius.</summary>
        public const float EchoAt = 0.12f, EchoScale = 0.6f, NegHold = 0.04f, NegBack = 0.06f, Grey = 0.2f, DiscRadius = 2.2f;
        // A bolt's first strike runs out at LeaderSpeed cells/s; after that it is redrawn whole every
        // StrikeMin-StrikeMax s on its own clock, turning up to Sway radians, the last shape left as an
        // afterimage for GhostLife s. At the end it stretches by Stretch and thins to red lines.
        public const float LeaderSpeed = 28f, StrikeMin = 0.07f, StrikeMax = 0.11f, GhostLife = 0.035f, Sway = 0.3f, Stretch = 0.25f;
        public const int Clumps = 7, ShardCount = 12, Specks = 22, SparkCount = 10;
        public const float ClumpLife = 0.55f, ShardLife = 0.16f, SpeckFrom = 0.12f, SpeckLife = 0.6f;
        public const float ForwardShare = 0.6f, ForwardFan = 150f, BackFan = 230f, BackShare = 0.42f, Jag = 0.12f;
        public const float StreakBack = 1.2f, StreakOn = 2.2f, StreakLife = 0.3f, SparkLife = 0.42f;
        public const float ZoneStart = 0.35f, ZoneEvery = 0.45f, ZoneFlick = 0.08f;
        // The sketch's sliders at their defaults.
        public const float SparkTime = 0.07f, Life = 0.45f, ReachBolts = 1.8f, Width = 0.12f, Boil = 15f;
        public const int Count = 9;
        public const float Shake = 0.07f, PlainShake = 0.012f;

        /// <summary>The picture runs this long after the fist lands.</summary>
        public const float After = 2.4f;

        public static float Pull(float warmup) => warmup * 0.6f;
        public static float Crackle(float warmup) => warmup * 0.35f;
        public static float Duration(float warmup) => warmup + After;

        /// <summary>Strength of the negative, 1 to 0, <paramref name="age"/> seconds after the hit.</summary>
        public static float Negative(float age)
        {
            if (age < 0f || age >= NegHold + NegBack) return 0f;
            return age < NegHold ? 1f : 1f - VfxMath.Smooth((age - NegHold) / NegBack);
        }

        /// <summary>
        /// The fist, <paramref name="seconds"/> after the warmup began: pulled back from rest, driven in,
        /// speeding up, to the contact, then home. <paramref name="todo"/> is Todo's feet; aim is unit.
        /// </summary>
        public static Vector2 Fist(Vector2 todo, Vector2 aim, Vector2 contact, float seconds, float warmup)
        {
            var shoulder = new Vector2(todo.x + aim.x * 0.08f, todo.y + 0.4f + aim.y * 0.05f);
            Vector2 rest = shoulder + aim * 0.18f, back = shoulder - aim * 0.1f;
            float pull = Pull(warmup), age = seconds - warmup;
            if (seconds < pull) return Vector2.Lerp(rest, back, VfxMath.Smooth(seconds / pull));
            if (age < 0f) { float u = (seconds - pull) / (warmup - pull); return Vector2.Lerp(back, contact, u * u); }
            return Vector2.Lerp(contact, rest, VfxMath.Smooth((age - 0.08f) / 0.25f));
        }

        /// <summary>Where the fist meets the target standing at <paramref name="foe"/>, hit along <paramref name="aim"/>.</summary>
        public static Vector2 Contact(Vector2 foe, Vector2 aim) => new Vector2(foe.x - aim.x * 0.2f, foe.y - aim.y * 0.2f + Chest);
    }
}
