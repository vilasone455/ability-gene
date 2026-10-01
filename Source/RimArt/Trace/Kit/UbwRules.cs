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

        /// <summary>Every sword a command takes out of the ground takes this long off the world, as it leaves. The commands cost no charge.</summary>
        public float swordCostSeconds = 0.5f;

        /// <summary>Full Open: while charging, one sword pulls out of the ground this often, nearest the target first.</summary>
        public float fullOpenSwordSeconds = 0.1f;

        /// <summary>Full Open: the most swords hovering at once; the rest wait.</summary>
        public int fullOpenSwords = 30;

        /// <summary>Full Open: Release fires the hovering swords one after another within this long.</summary>
        public float fullOpenVolleySeconds = 0.3f;

        /// <summary>Full Open: a fired sword flies this many cells a second.</summary>
        public float fullOpenSpeed = 16f;

        /// <summary>Pin: this many swords, the nearest to the target, fly in and pin it.</summary>
        public int pinSwords = 4;

        /// <summary>Pin: Cut damage of each pinning sword.</summary>
        public float pinDamage = 2f;

        /// <summary>Pin: how long the target stays pinned (AG_UbwPinned caps Moving at 0, so it counts as downed).</summary>
        public float pinSeconds = 12f;

        /// <summary>Pin: a pinning sword flies this many cells a second.</summary>
        public float pinSpeed = 16f;

        /// <summary>Draw: the sword taken is the one nearest the clicked cell within this many cells.</summary>
        public float drawPickRadius = 1.5f;

        /// <summary>Draw: the sword flies to the caster this many cells a second.</summary>
        public float drawSpeed = 22f;

        /// <summary>Intercept: a sword needs this long to leave the ground and turn onto the shot's path.</summary>
        public float interceptRiseSeconds = 0.1f;

        /// <summary>Intercept: a sword flies to the meeting point this many cells a second.</summary>
        public float interceptSpeed = 20f;

        /// <summary>Intercept: a shot is met no nearer than this many cells to where it was fired from.</summary>
        public float interceptClearOfGun = 1.2f;

        /// <summary>Intercept: a shot is met at least this many cells short of where it was aimed.</summary>
        public float interceptShortOfTarget = 1.5f;

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
