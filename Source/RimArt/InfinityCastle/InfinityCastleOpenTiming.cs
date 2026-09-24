using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// When each part of Infinity Castle's home-map side happens: seconds in, numbers out, no drawing and
    /// no map. The port of Tools/VfxLab/web/sketches/infinity-castle-open.js; the constants are that
    /// sketch's defaults. Nobody is taken: the doors open where the sketch's hostiles stand, and that
    /// table is the preview's script, not a rule.
    /// </summary>
    internal static class InfinityCastleOpenTiming
    {
        /// <summary>Hostiles within this many cells of the target cell are taken, up to Cap (rules and placeholders; XML fields once there is an ability).</summary>
        public const float Radius = 5.9f;
        public const int Cap = 8;
        /// <summary>The answer ring reaches the radius in 0.3 s; a pawn sinks in 0.4 s and rises in 0.55 s.</summary>
        public const float RingTime = 0.3f, Sink = 0.4f, Rise = 0.55f;
        /// <summary>The sketch's sliders: 1 s of warm-up, 1.2 s of the result on screen, 10 hostiles in range.</summary>
        public const float Warmup = 1f, Hold = 1.2f;
        public const int InRange = 10;
        /// <summary>The camera shake 0.05 s after the strum.</summary>
        public const float StrumShake = 0.02f, ShakeDelay = 0.05f;

        /// <summary>The preview's script: hostiles in cells from the target cell, nearest first (the first 8 are taken).</summary>
        public static readonly Vector2[] Hostiles =
        {
            new Vector2(1.1f, 0.5f), new Vector2(-1.4f, 1.5f), new Vector2(1.9f, -1.6f), new Vector2(0.2f, -2.9f), new Vector2(-3.2f, -0.9f),
            new Vector2(3.3f, 2.1f), new Vector2(-1.6f, 4.0f), new Vector2(4.4f, -1.9f), new Vector2(-4.3f, 2.9f), new Vector2(3.0f, 4.6f),
        };
        /// <summary>The carrier stands 9 cells west and 2.5 south of the target cell; the raider killed inside comes up beside it.</summary>
        public static readonly Vector2 Caster = new Vector2(-9f, -2.5f), CorpseAt = new Vector2(0.9f, -0.7f);
        public const int KilledInside = 4;

        public static int Taken => Mathf.Min(InRange, Cap);
        /// <summary>The biwa's body, where the strum's rings start, for a carrier standing at <paramref name="caster"/>.</summary>
        public static Vector2 BiwaAt(Vector2 caster) => new Vector2(caster.x + 0.1f, caster.y + 0.35f);

        // ---- take: the strum, a door under each hostile as the answer ring passes it, the carrier last ----
        public static float StrumAt => Warmup;
        public static float DoorOf(int i) => StrumAt + 0.05f + Hostiles[i].magnitude / Radius * RingTime;
        public static float CasterDoor => DoorOf(Taken - 1) + CastleEffectGraphics.DoorThrough + Sink + 0.25f;
        public static float TakeDuration => CasterDoor + CastleEffectGraphics.DoorEnd(Sink + 0.05f) + Hold;

        // ---- return: a door at each taken-from cell, 0.07 s apart; the carrier's; the corpse's ---------
        public const float First = 0.3f;
        public static float BackDoorOf(int i) => First + i * 0.07f;
        public static float CasterBackDoor => First + 0.12f;
        public static float CorpseDoor => First + 0.35f;
        public static float ReturnDuration => BackDoorOf(Taken - 1) + CastleEffectGraphics.DoorEnd(Rise * 0.75f) + Hold;
    }
}
