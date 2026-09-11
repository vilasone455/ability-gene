using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Every number vector manipulation is tuned by, in one place and with no Unity resources
    /// loaded at type initialisation - same rule and same reason as <see cref="PanoplyDefaults"/>.
    ///
    /// The two that decide what the kit is worth are <see cref="BaseRangeCells"/> and
    /// <see cref="StrainCosts"/>. Range is what a redirected round can reach; strain is the only
    /// real limit on how often, because the cooldown is five seconds and deliberately trivial.
    /// </summary>
    public static class VectorEditDefaults
    {
        /// <summary>
        /// How far from the carrier a round can be and still be caught by the scan.
        ///
        /// This stopped being a reaction-time number the moment reflex surge took that job.
        /// What the radius decides instead is how much of a volley is in the air when the
        /// player clicks - at six cells the answer was usually one round, which makes four
        /// groups a panel with nothing to put in it.
        ///
        /// Twelve is the first radius at which rounds from separate shooters overlap reliably.
        /// It is also the loudest tuning knob in the kit: raise it and the editor opens earlier
        /// with more on the table, lower it and the catch is late, tight and mostly single.
        /// </summary>
        public const float ScanRadiusCells = 12f;

        /// <summary>
        /// Travel given to an edited round at ×1 force, measured from where it was caught.
        ///
        /// Range is recalculated, not extended: a round two cells from the end of its trip and
        /// one that has just left the barrel both come out of the editor with the same twenty
        /// cells in front of them. That is the whole reason the ability is worth a strain cost
        /// on rounds nobody wanted turned - a spent volley is a loaded one again.
        /// </summary>
        public const float BaseRangeCells = 20f;

        /// <summary>
        /// The four force settings, ascending. Absolute multipliers against the round's own
        /// baseline speed and primary damage, never against a multiplier it already carries,
        /// so re-editing the same round is idempotent rather than compounding.
        /// </summary>
        public static readonly float[] Forces = { 0.25f, 0.5f, 1f, 2f };

        public const float MinForce = 0.25f;
        public const float MaxForce = 2f;

        /// <summary>
        /// Four groups, because four is how many independent decisions fit on one paused screen
        /// without the panel becoming the ability.
        /// </summary>
        public const int MaxGroups = 4;

        /// <summary>
        /// Strain added by one application, indexed by how many groups it changed.
        ///
        /// Steeply superlinear on purpose. One group is a routine correction; four is the whole
        /// field turned at once, and it should cost most of the bar.
        /// </summary>
        public static readonly float[] StrainCosts = { 0.08f, 0.24f, 0.48f, 0.80f };

        /// <summary>At or above this the carrier is in the overload stage, which puts them down.</summary>
        public const float StrainCollapseThreshold = 0.95f;

        /// <summary>Visible stagger at the moment of overload. The lasting part is the strain stage.</summary>
        public const int OverloadStunTicks = 300;

        /// <summary>Five seconds. Strain, not this, is what stops the second use.</summary>
        public const int CooldownTicks = 300;

        /// <summary>
        /// What the world's tick rate becomes while a reflex surge is open.
        ///
        /// A quarter, and the quarter is load-bearing. A rifle round crosses the catch radius in
        /// about twenty-one ticks; at normal rate that is a sixth of a second, which is less
        /// than a person's reaction time before they have even moved the mouse. At a quarter it
        /// is 1.4 seconds, which is enough to see it and press the gizmo. A half would give 0.7
        /// seconds - still about reaction time, so still a coin flip, which is why there is one
        /// surge setting rather than two.
        ///
        /// Forced rather than scaled by the TickRateMultiplier postfix: scaling would let
        /// superfast multiply straight back through it and run the game faster than normal.
        /// </summary>
        public const float SurgeTickRate = 0.25f;

        /// <summary>How far from a group's centre its rotation handle sits, in cells.</summary>
        public const float RotationHandleCells = 3.2f;

        /// <summary>Screen pixels within which a click counts as grabbing the handle.</summary>
        public const float RotationHandleGrabPixels = 20f;

        /// <summary>Screen pixels a drag must cover before it is a box rather than a click.</summary>
        public const float DragThresholdPixels = 6f;

        /// <summary>
        /// Group identity. The map lines are drawn with the SimpleColor entries and the panel
        /// with the RGB ones, so the two lists have to say the same thing in the same order.
        /// </summary>
        public static readonly Color[] GroupColors =
        {
            new Color(0.35f, 0.85f, 1f),
            new Color(1f, 0.65f, 0.25f),
            new Color(1f, 0.45f, 0.85f),
            new Color(0.45f, 0.95f, 0.5f),
        };

        public static readonly SimpleColor[] GroupLineColors =
        {
            SimpleColor.Cyan,
            SimpleColor.Orange,
            SimpleColor.Magenta,
            SimpleColor.Green,
        };

        /// <summary>Strain cost of an application that changes this many groups.</summary>
        public static float StrainCostFor(int groupsChanged)
        {
            if (groupsChanged <= 0) return 0f;
            if (groupsChanged > StrainCosts.Length) groupsChanged = StrainCosts.Length;
            return StrainCosts[groupsChanged - 1];
        }
    }
}
