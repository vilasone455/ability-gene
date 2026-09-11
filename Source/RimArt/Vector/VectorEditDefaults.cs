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
        /// How close a round has to come to the carrier to be worth catching.
        ///
        /// Not how far away it can be when it is caught - that is <see cref="LeadTicks"/>. This
        /// is the threat test: a round on a line that never brings it within twelve cells is
        /// somebody else's problem, however near it passes the camera.
        ///
        /// What the figure decides is how much of a volley is in the air at once - at six cells
        /// the answer was usually one round, which makes four groups a panel with nothing to put
        /// in it. Twelve is the first radius at which rounds from separate shooters overlap
        /// reliably. It is still the loudest tuning knob in the kit: raise it and more of the
        /// field is the carrier's business, lower it and the catch is tight and mostly single.
        /// </summary>
        public const float ScanRadiusCells = 12f;

        /// <summary>
        /// How far ahead of itself a round is caught, in game ticks of its own flight.
        ///
        /// This is the number that decides whether the ability can be used at all, and it is
        /// counted in ticks rather than in cells for one reason: a catch measured in cells is a
        /// different amount of time for every round in the game. Twelve cells is ten ticks of a
        /// vanilla rifle round, six of a typical Combat Extended one, three and a half of a fast
        /// one and under one tick of the fastest CE ships - and three and a half ticks is under a
        /// quarter of a second even with reflex surge holding the world at quarter rate, which is
        /// below anyone's reaction time. Twenty-one ticks is twenty-one ticks whatever fired it:
        /// 0.35 seconds at normal rate, 1.4 at quarter.
        ///
        /// Twenty-one is not a new number. It is how long a vanilla rifle round already spent
        /// crossing the old twelve-cell circle, which is the window the kit was tuned against and
        /// the one that plays. Stating it directly is what makes a CE round behave like a vanilla
        /// one - it is simply caught further out, because it is coming faster.
        ///
        /// A round already inside the reach counts as arriving now, so one that has just gone
        /// past can still be taken hold of and thrown back.
        /// </summary>
        public const int LeadTicks = 21;

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
        /// A quarter, and the quarter is load-bearing. A round is catchable for
        /// <see cref="LeadTicks"/> ticks before it arrives, whatever fired it; at normal rate
        /// that is 0.35 seconds, which is gone before a person has moved the mouse. At a quarter
        /// it is 1.4 seconds, which is enough to see it and press the gizmo. A half would give
        /// 0.7 seconds - still about reaction time, so still a coin flip, which is why there is
        /// one surge setting rather than two.
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

        /// <summary>
        /// Whether a round on this line will come inside the carrier's reach soon enough to be
        /// caught - which is the whole catch rule, and the only place it is stated.
        ///
        /// Flies the round forward at its own speed for <see cref="LeadTicks"/> and asks whether
        /// that stretch of its path passes within <see cref="ScanRadiusCells"/> of the carrier.
        /// A round already in reach passes at once, because the segment starts inside.
        /// </summary>
        public static bool ComesIntoReach(Vector3 position, Vector3 heading, float speedPerTick,
            Vector3 carrier)
        {
            float lead = speedPerTick * LeadTicks;
            if (lead < 0f) lead = 0f;

            Vector3 unused;
            return Rounds.SegmentEntersCircle(position, position + heading * lead, carrier,
                ScanRadiusCells, out unused);
        }

        /// <summary>Strain cost of an application that changes this many groups.</summary>
        public static float StrainCostFor(int groupsChanged)
        {
            if (groupsChanged <= 0) return 0f;
            if (groupsChanged > StrainCosts.Length) groupsChanged = StrainCosts.Length;
            return StrainCosts[groupsChanged - 1];
        }
    }
}
