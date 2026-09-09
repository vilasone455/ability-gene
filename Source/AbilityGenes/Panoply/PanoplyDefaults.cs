namespace AbilityGenes
{
    /// <summary>
    /// Plain constants, no Unity resources - same rule and same reason as
    /// <see cref="LarynxDefaults"/>.
    ///
    /// Almost everything here is a shape rather than a balance figure. The one number that
    /// decides what the gene costs is <see cref="DebtPerBlade"/>, because the cost of this gene
    /// is not a cooldown and not a resource: it is how much of the carrier is currently standing
    /// in the ground somewhere else.
    /// </summary>
    public static class PanoplyDefaults
    {
        /// <summary>
        /// Severity added to AG_PanoplyDebt for every blade currently on the map.
        ///
        /// The debt is not accrued and it is not shed. It is recomputed from the blade count
        /// every time that count changes, which means the hediff is a *readout* of the field
        /// rather than a store of its own - loose a blade and the debt drops in the same tick.
        /// That is the whole cost model, and it needs no tick, no comp and no scribing.
        ///
        /// At 0.02 a full rain of fourteen lands the carrier in the second stage and a second
        /// rain on top of it in the third. One field is a weight; two is a decision to stop
        /// being much use for anything else.
        /// </summary>
        public const float DebtPerBlade = 0.02f;

        /// <summary>How long a planted blade stands before the body takes it back.</summary>
        public const int BladeLifetimeTicks = 5000;

        /// <summary>
        /// Ticks between one blade landing and the next, so rain arrives as rain.
        ///
        /// Added to each blade's own ticksToImpact, holding it at its summon gate longer.
        /// </summary>
        public const int RainStaggerTicks = 4;

        /// <summary>Ticks between one blade launching and the next, so loose reads as a volley.</summary>
        public const int LooseStaggerTicks = 4;

        /// <summary>
        /// How far a planted blade leans off upright, either way.
        ///
        /// Small on purpose. A blade at a steep angle reads as one lying on the floor, which is
        /// exactly what this gene must not look like - the whole point of a planted blade is
        /// that it is standing in the ground and cannot be picked up.
        /// </summary>
        public const float PlantedLeanRange = 13f;

        /// <summary>
        /// Where the ground line crosses the planted sprite, measured from the top.
        ///
        /// Must match GROUND_FRACTION in make_textures.py. It is what lets a leaning blade
        /// pivot about the hole it is standing in rather than about the middle of its picture.
        /// </summary>
        public const float PlantedGroundFraction = 0.78f;

        /// <summary>
        /// A planted blade is drawn larger than one in the air: it is a side view of something
        /// standing up, so it occupies more of the screen than the same object seen flat.
        /// </summary>
        public const float PlantedDrawSize = 1.3f;

        public const float BladeDrawSize = 1.05f;

        /// <summary>Ticks a blade takes to travel to a hand, before scaling by distance.</summary>
        public const int GraspTicksPerCell = 2;
        public const int GraspMinTicks = 12;
        public const int GraspMaxTicks = 45;

        /// <summary>How high the arc of a grasped blade rises at the midpoint, in cells.</summary>
        public const float GraspArcHeight = 1.1f;

        /// <summary>Ticks a blade spends lifting and turning before it launches.</summary>
        public const int LooseWindupTicks = 12;
    }
}
