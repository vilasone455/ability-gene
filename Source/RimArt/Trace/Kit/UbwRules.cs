using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Unlimited Blade Works' balance numbers, on the AG_UnlimitedBladeWorks MapGeneratorDef so they change
    /// without a rebuild. Proposed and not agreed (the sketches' headers): nothing reads them yet but the
    /// map's own timeline, which has no verse. The shapes and timings of the pictures are code.
    /// </summary>
    public class UbwRules : DefModExtension
    {
        /// <summary>Released after verse 1, 2 or 3 the cast takes everyone standing within this many cells.</summary>
        public List<float> radiusByVerse = new List<float> { 6f, 9f, 12f };

        /// <summary>A verse of the chant takes this long.</summary>
        public float verseSeconds = 2f;

        /// <summary>The world lasts this long by verse, before the swords the commands spend are taken off it.</summary>
        public List<float> worldSecondsByVerse = new List<float> { 20f, 25f, 30f };

        /// <summary>Every sword a command uses takes this long off the world.</summary>
        public float swordCostSeconds = 0.5f;

        /// <summary>The cast's cooldown, in days.</summary>
        public float cooldownDays = 2f;

        private static readonly UbwRules fallback = new UbwRules();

        /// <summary>The numbers from the def, or the defaults above if the def carries none.</summary>
        public static UbwRules Of => UbwDefOf.AG_UnlimitedBladeWorks?.GetModExtension<UbwRules>() ?? fallback;
    }
}
