namespace RimArt
{
    /// <summary>
    /// Plain constants, no Unity resources - same rule and same reason as
    /// <see cref="PanoplyDefaults"/>.
    ///
    /// Almost every number here is a rhythm rather than a balance figure. The arc's damage is
    /// not decided in this mod at all: the strike is Melee Animation's execution, resolved
    /// against the carrier's own weapon, melee skill and lethality by their outcome system.
    /// What is tuned here is how the chain reads - how long the blink hangs before the blow,
    /// how long the beat between one person and the next is - and one real cost,
    /// <see cref="RecoilPerHop"/>.
    /// </summary>
    public static class ArcDefaults
    {
        /// <summary>
        /// Ticks between arriving beside somebody and the strike starting.
        ///
        /// This exists for one reason: the afterimages. Melee Animation takes over drawing a
        /// pawn the moment its animation starts, so a trail drawn in the same tick as the
        /// strike is a trail nobody sees. A fifth of a second of the carrier standing there
        /// having plainly just arrived is what makes the blink read as a blink.
        ///
        /// Kept equal to <see cref="GhostTicks"/> on purpose: the last ghost goes out in the
        /// same tick the animation takes the pawn over, so the two never fight over who is
        /// drawing the carrier.
        /// </summary>
        public const int StrikeDelayTicks = 12;

        /// <summary>Ticks between one strike finishing and the next arc leaving.</summary>
        public const int HopGapTicks = 20;

        /// <summary>
        /// Slack on the wait job that holds the carrier still between arriving and striking.
        ///
        /// It only has to outlive the pause; Melee Animation replaces the job outright when the
        /// strike begins. The margin is there so that an arc which fails to start an animation at
        /// all still leaves the carrier standing rather than frozen.
        /// </summary>
        public const int HoldMarginTicks = 15;

        /// <summary>
        /// How long the last of a dash's afterimages hangs about, in ticks.
        ///
        /// Each ghost gets a share of this by its place in the line, so the one nearest the cell
        /// the carrier left goes out first and the trail retracts toward them rather than the
        /// whole thing blinking out at once. That retraction is what carries the direction of
        /// travel - a trail that vanishes evenly reads as five people, not one moving.
        /// </summary>
        public const int GhostTicks = 12;

        /// <summary>How many copies of the carrier are drawn along the line of a dash.</summary>
        public const int GhostCount = 6;

        /// <summary>
        /// How long the streak itself lasts, in ticks.
        ///
        /// Longer than the ghosts, because it is this mod's own mesh and nothing else wants to
        /// draw it: the light hangs in the air for a moment after the body has stopped being in
        /// two places.
        /// </summary>
        public const int StreakTicks = 16;

        /// <summary>Width of the bright core of the streak, in cells.</summary>
        public const float StreakWidth = 0.16f;

        /// <summary>Width of the glow around it, in cells.</summary>
        public const float StreakGlowWidth = 0.55f;

        /// <summary>
        /// How many discrete alphas the streak fades through.
        ///
        /// The fade is baked into the material rather than pushed through a property block, so
        /// each step is a material the game caches for the session. Eight is smooth at the speed
        /// this fades and is sixteen materials once, for both layers, forever.
        /// </summary>
        public const int StreakAlphaSteps = 8;

        /// <summary>
        /// Severity of AG_ArcRecoil added for every person the arc actually reaches.
        ///
        /// Charged per hop rather than per cast, so an arc that finds one target and stops
        /// costs a third of a full chain. Three hops land on 0.99, which is the top stage - a
        /// full chain is meant to be the thing that leaves the carrier standing in the middle
        /// of a fight barely able to hold anything.
        /// </summary>
        public const float RecoilPerHop = 0.33f;

        /// <summary>
        /// Ticks the run waits after a strike that could not be animated.
        ///
        /// Only reachable when Melee Animation has no execution for what the carrier is
        /// holding - an unarmed carrier without Fists of Fury, most likely. The arc still
        /// happens and the carrier still swings; it simply does not get the animation, and this
        /// is roughly how long a plain melee swing takes so the rhythm survives.
        /// </summary>
        public const int UnanimatedHopTicks = 40;

        /// <summary>
        /// How long the run will wait for an animation that has not ended, in ticks.
        ///
        /// A safety valve, not a timing figure. Nothing in the mod ends an animation early, but
        /// a save reloaded mid-strike or another mod interrupting one would otherwise leave a
        /// run waiting forever on a carrier who is standing about doing nothing.
        /// </summary>
        public const int AnimationWaitLimitTicks = 900;
    }
}
