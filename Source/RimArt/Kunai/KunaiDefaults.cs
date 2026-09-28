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

        /// <summary>
        /// A kunai standing in the ground after a miss (<see cref="KunaiItem"/>): the point it went in is
        /// the texture's centre, the handle and ring lean back toward the thrower (down the texture).
        /// </summary>
        public const string PlantedTexture = "RimArt/Kunai/Planted";

        /// <summary>Cells across the planted kunai's texture; its kunai is drawn the same length as the item's.</summary>
        public const float PlantedDrawSize = 1f;

        /// <summary>Furthest a planted kunai stands from its cell's centre, so it never looks as if it is in the next cell.</summary>
        public const float PlantedMaxOffset = 0.35f;

        /// <summary>Most kunai stuck in one pawn at once. A hit beyond this drops the kunai instead.</summary>
        public const int MaxEmbeddedPerPawn = 3;

        /// <summary>Bleeding of the wound a kunai is stuck in, while it is stuck.</summary>
        public const float EmbeddedBleedFactor = 0.5f;

        /// <summary>
        /// Severity of the cut added when a kunai is pulled out. Vanilla cuts bleed 6% blood per day
        /// per point of severity, so this adds 36% a day on top of the wound's restored bleeding.
        /// Reduced so it never destroys the body part.
        /// </summary>
        public const float PullCutSeverity = 6f;

        /// <summary>Pull time per kunai against a standing, awake, hostile pawn: 0.5 s, then a melee roll.</summary>
        public const int PullTicksFighting = 30;

        /// <summary>Pull time from anyone else (downed, asleep, friendly, prisoner): 2 s for all stuck kunai, no roll.</summary>
        public const int PullTicksCalm = 120;
    }
}
