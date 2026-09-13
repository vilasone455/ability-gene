namespace RimArt
{
    /// <summary>
    /// Makibishi numbers that live in code rather than in the defs. Range, warmup, pouch capacity
    /// and the slow's size are in AG_Makibishi_Things.xml, AG_Makibishi_Abilities.xml and
    /// AG_Makibishi_Hediffs.xml.
    /// </summary>
    public static class MakibishiDefaults
    {
        /// <summary>Drawn in the pawn's hand during the scatter animation.</summary>
        public const string HandTexture = "RimArt/Makibishi/Handful";

        /// <summary>How long a spiked cell lasts: 30 s. Spiking the cell again restarts it.</summary>
        public const int LifetimeTicks = 1800;

        /// <summary>
        /// Path cost added to a spiked cell for every pawn it can hurt. A plain cell costs 13, so a
        /// pawn takes a detour up to about 30 cells longer rather than cross one spiked cell.
        /// Building_Trap adds 800 for a trap the pawn knows about.
        /// </summary>
        public const ushort PathFindCost = 400;

        /// <summary>Chance, each time a pawn enters a spiked cell, that it steps on a spike.</summary>
        public const float TriggerChance = 0.35f;

        /// <summary>Stab damage of one spike, to a foot or the lowest leg part left.</summary>
        public const float WoundDamage = 4f;

        public const float WoundArmorPenetration = 0.10f;

        /// <summary>Chance for each of the four corner cells of the 3x3 patch. The centre "+" is always spiked.</summary>
        public const float CornerChance = 0.5f;
    }
}
