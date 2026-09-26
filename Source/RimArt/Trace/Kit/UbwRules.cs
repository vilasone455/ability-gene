using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Unlimited Blade Works' balance numbers, on the AG_Trace_UnlimitedBladeWorks AbilityDef so they change
    /// without a rebuild. Proposed 2026-09-24, taken as placeholders 2026-09-25. The cooldown is the
    /// AbilityDef's own cooldownTicksRange. The shapes and timings of the pictures are code, except the
    /// radius and the verse's length, which the pictures take from here when a cast begins.
    /// </summary>
    public class UbwRules : DefModExtension
    {
        /// <summary>Released after verse 1, 2 or 3 the cast takes everyone standing within this many cells.</summary>
        public List<float> radiusByVerse = new List<float> { 6f, 9f, 12f };

        /// <summary>A verse of the chant takes this long. At least 1.9 s: the chant's lines run out in 1.8 s.</summary>
        public float verseSeconds = 2f;

        /// <summary>The world lasts this long by verse, counted from the moment everyone is taken.</summary>
        public List<float> worldSecondsByVerse = new List<float> { 20f, 25f, 30f };

        /// <summary>The world the ability opens: true for v2 (plates with height, the north sky with gears), false for the flat v1. The debug window opens either.</summary>
        public bool worldV2 = true;

        /// <summary>Every sword a command uses takes this long off the world. For the commands, which are not built yet.</summary>
        public float swordCostSeconds = 0.5f;

        private static readonly UbwRules fallback = new UbwRules();

        /// <summary>The numbers from the ability def, or the defaults above if the def carries none.</summary>
        public static UbwRules Of => UbwDefOf.AG_Trace_UnlimitedBladeWorks?.GetModExtension<UbwRules>() ?? fallback;

        public float RadiusFor(int verse) => At(radiusByVerse, verse, 6f);
        public float WorldSecondsFor(int verse) => At(worldSecondsByVerse, verse, 20f);

        private static float At(List<float> list, int verse, float otherwise)
        {
            if (list == null || list.Count == 0) return otherwise;
            return list[System.Math.Max(0, System.Math.Min(list.Count - 1, verse - 1))];
        }
    }
}
