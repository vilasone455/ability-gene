namespace RimArt
{
    /// <summary>
    /// Every number the frost bomb is balanced on, in one place.
    ///
    /// The bomb's job is to buy a few seconds, and the two numbers that decide whether it does
    /// that are the hard freeze and the thaw. The hard freeze is the part that is worth using the
    /// charge for; the thaw is the part that stops it from being an execution. Both are short on
    /// purpose - a control tool that removes somebody from a fight entirely is a kill with extra
    /// steps, and this mod already has kills.
    /// </summary>
    public static class FrostDefaults
    {
        /// <summary>
        /// The hard freeze: no movement, no attacks, nothing. Three seconds at normal speed.
        ///
        /// A stun rather than a pile of capacity offsets, because the stun handler is the one
        /// mechanic in the game that every AI, every verb and every job already respects. Offsets
        /// alone leave a frozen pawn still turning to face people and still finishing a swing.
        /// </summary>
        public const int FreezeTicks = 180;

        /// <summary>
        /// Severity the rime lands at, which is also the top of the hediff's last stage. The thaw
        /// is the hediff shedding this back to zero.
        /// </summary>
        public const float RimeSeverity = 1f;

        /// <summary>
        /// How much of the freeze a target shrugs off per point of body size over human.
        ///
        /// A thrumbo is body size 4. At this rate it takes a little over a third of the freeze a
        /// human does, which is the doc's "large targets resist" without a table of exceptions.
        /// </summary>
        public const float BodySizeResistance = 1f;

        /// <summary>
        /// Mechs seize rather than freeze. They are not wet and they have no blood to slow, so
        /// what the cold reaches is bearings and actuators - less of an effect, on the same curve.
        /// </summary>
        public const float MechanicalMultiplier = 0.6f;

        /// <summary>Nothing smaller than this is worth the game telling the player about.</summary>
        public const float MinimumFreezeFraction = 0.15f;

        /// <summary>
        /// Cells of freezing cloud. Small: this is a doorway or half a sandbag line, not a
        /// battlefield. Anything bigger stops being a flank-maker and starts being a win button.
        /// </summary>
        public const float BurstRadius = 3.4f;

        /// <summary>
        /// How far the cold drops off from the centre. An edge-of-cloud target is stunned for
        /// this fraction of the full time, so standing at the rim is survivable and the player
        /// has a reason to aim rather than to lob.
        /// </summary>
        public const float EdgeFreezeFraction = 0.45f;
    }
}
