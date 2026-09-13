namespace RimArt
{
    /// <summary>
    /// Kunai numbers that live in code rather than in the defs. Range, damage, accuracy and belt
    /// capacity are in AG_Kunai_Things.xml and AG_Kunai_Abilities.xml.
    /// </summary>
    public static class KunaiDefaults
    {
        /// <summary>Chance a kunai breaks when it hits a pawn or building. Misses never break.</summary>
        public const float BreakChanceOnHit = 0.2f;

        /// <summary>Drawn in the pawn's hand during the throw animation.</summary>
        public const string HandTexture = "RimArt/Kunai/Kunai";
    }
}
