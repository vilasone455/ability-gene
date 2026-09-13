namespace RimArt
{
    /// <summary>
    /// Every number the retrieval hook belt is balanced on, in one place.
    ///
    /// Kept free of game types so Tests/RetrievalHook can compile it on its own.
    /// </summary>
    public static class RetrievalHookDefaults
    {
        /// <summary>Maximum distance from the wearer to the target, in cells.</summary>
        public const float Range = 15f;

        /// <summary>Most item mass one pull can move. Stacks heavier than this are split.</summary>
        public const float ItemCapacityKg = 20f;

        /// <summary>Stationary work to reel the tether back in: 10 seconds at normal speed.</summary>
        public const int ReloadTicks = 600;

        /// <summary>How fast the net travels out, in cells per tick. 15 cells takes 25 ticks.</summary>
        public const float LaunchCellsPerTick = 0.6f;

        /// <summary>Launch never resolves faster than this, so an adjacent shot is still visible.</summary>
        public const int MinLaunchTicks = 6;

        /// <summary>Ticks spent dragging the target across one cell.</summary>
        public const int DragTicksPerCell = 8;

        /// <summary>
        /// How long the net may wait at the target for the wearer's cast job to hand over to the
        /// pull job. The hand-over normally takes a tick or two.
        /// </summary>
        public const int ConnectGraceTicks = 60;

        /// <summary>Largest severity added to the chosen wound.</summary>
        public const float MaxSeverityIncrease = 1f;

        /// <summary>Step the increase is reduced by until it is safe.</summary>
        public const float SeverityStep = 0.01f;
    }
}
