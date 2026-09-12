namespace RimArt
{
    /// <summary>
    /// Every number the mimic beacon has. Two of them are derived rather than chosen, and the
    /// derivations are written out because both are easy to "improve" into being wrong.
    /// </summary>
    internal static class MimicDefaults
    {
        /// <summary>
        /// How long a decoy stands, in ticks. Twenty seconds, matching provoke's window and the
        /// stasis field's, because it buys the same thing those do: one retreat, one reload, one
        /// rescue. Long enough to cross open ground, short enough that it is a moment rather
        /// than a state of the battle.
        /// </summary>
        public const int LifetimeTicks = 1200;

        /// <summary>
        /// The multiplier the decoy applies to its own score inside
        /// AttackTargetFinder.GetShootingTargetScore, and the whole of the taunt.
        ///
        /// That function scores a target at 60, minus the distance to it (clamped at 40), plus 10
        /// if the target is aiming back at the shooter, plus 40 if the shooter has fired on it
        /// within the last 300 ticks, minus up to 10 for cover, and multiplies the result by this.
        ///
        /// Two inequalities have to hold at every distance, and each is written for the case that
        /// is worst for the decoy rather than the average one.
        ///
        /// It must beat a target nobody is shooting at, even when that target is shooting back and
        /// so holds the +10:
        ///
        ///     (60 - d) * F  &gt;  (60 - d) + 10        =&gt;  F &gt; 1 + 10/(60 - d)
        ///
        /// which is hardest at the 40-cell distance clamp and demands F &gt; 1.5.
        ///
        /// And it must lose to a target the shooter is already engaged on, even when that target
        /// is *not* shooting back and so holds only the +40:
        ///
        ///     (60 - d) * F  &lt;  (60 - d) + 40        =&gt;  F &lt; 1 + 40/(60 - d)
        ///
        /// which is hardest at point-blank and demands F &lt; 1.667. That is the end of the item's
        /// promise that a raider who has already reached somebody does not turn around, and it is
        /// the bound that is easy to get wrong: 1.75 satisfies the same inequality at ten cells
        /// and upward and quietly breaks it inside seven, which is exactly where it matters most.
        ///
        /// The window is therefore (1.5, 1.667) and this sits near the middle of it, beating an
        /// idle target by three fifths. Tests/Mimic asserts both bounds at every distance.
        ///
        /// Raising it past 1.667 turns a strong preference into a compulsion, which is provoke's
        /// job and not this one. If it pulls shooters off targets in heavy cover, lower it toward
        /// 1.55 rather than adding a rule.
        /// </summary>
        public const float PriorityFactor = 1.6f;

        /// <summary>
        /// The projection's colour, multiplied into every material the copy is drawn with.
        ///
        /// Held as four floats rather than a UnityEngine.Color because Tests/Mimic compiles this
        /// file on its own against no game assemblies, and one Color field would cost that
        /// harness its reason to be simple.
        ///
        /// Multiplied, not assigned, so the damage flash still reads through it - a decoy being
        /// shot flickers the way anything else being shot does.
        /// </summary>
        public const float TintRed = 0.40f;
        public const float TintGreen = 0.85f;
        public const float TintBlue = 1.00f;
        public const float TintAlpha = 1.00f;

        /// <summary>
        /// Whether the copy is drawn through the game's cloaking shader as well as tinted.
        ///
        /// True gives a translucent, faintly rippling figure - the shader carries a distortion
        /// texture and halves the alpha, so the tint above lands on top of that and the result
        /// reads as projected light. It also costs the copy its shadow, which is correct and is
        /// the game's own behaviour rather than anything added here: RenderPawnAt skips the
        /// shadow for anything drawn with that flag, and a shadow under a hologram is the single
        /// most obvious tell there is.
        ///
        /// False gives a solid blue clone instead - the same figure, fully opaque, with its
        /// shadow. Worth trying if the shimmer reads as a bug rather than as an effect; it is
        /// the one visual decision here that cannot be made by reading code.
        /// </summary>
        public const bool Shimmer = true;

        /// <summary>
        /// The radius searched for somewhere to stand when the beacon lands on a cell that is
        /// occupied or unstandable. Two cells, as the toy car uses: far enough to clear a pawn
        /// or a wall corner, near enough that the decoy still appears where it was thrown.
        /// </summary>
        public const float LandingSearchRadius = 2f;
    }
}
