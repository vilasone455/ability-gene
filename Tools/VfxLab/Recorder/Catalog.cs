using System;
using System.Linq;

namespace RimArt.VfxLab
{
    public sealed record Phase(string Name, float Seconds);

    /// <summary>
    /// What the recorder needs to know about a kit that its code does not already say: which
    /// preview component plays it, which field is its clock, and where its phases fall. Phase
    /// times are read off the kit's own timing classes wherever one exists, so a retimed effect
    /// moves its markers with it.
    ///
    /// Adding a kit: link its drawing, timing and DebugActions files in Recorder.csproj, then add
    /// an entry here. Every [DebugAction("RimArts", ...)] whose label starts with the prefix is
    /// recorded, except "clear" actions.
    /// </summary>
    public sealed class Kit
    {
        public string Name, Prefix, Clock;
        public Type Component;
        public Func<string, Phase[]> Phases = _ => Array.Empty<Phase>();
        /// <summary>For previews that loop forever: stop after this many seconds on their own clock.</summary>
        public Func<string, float?> LoopSeconds = _ => null;

        public static readonly Kit[] All =
        {
            new Kit
            {
                Name = "Six Paths", Prefix = "Six Paths:", Component = typeof(MapComponent_SixPathsPreview), Clock = "seconds",
                Phases = label => label.Contains("slam") ? SlamPhases()
                    : label.Contains("sheet") ? Array.Empty<Phase>() : RingPhases(),
                LoopSeconds = label => label.Contains("slam") || label.Contains("sheet")
                    ? null : SixPathsTiming.CycleSeconds * SixPathsShapes.Cycle.Length,
            },
            new Kit
            {
                Name = "Gravity Well", Prefix = "Gravity Well:", Component = typeof(MapComponent_GravityPreview), Clock = "seconds",
                // The preview's own numbers (DebugActions_Gravity.cs): mass climbs for 6 s, then
                // the implosion fades out by 6.5 s. There is no timing class to read them from.
                Phases = _ => new[] { new Phase("Gather", 0f), new Phase("Implode", 6f) },
            },
            new Kit
            {
                Name = "Shinra Tensei", Prefix = "Shinra Tensei:", Component = typeof(MapComponent_ShinraVfx), Clock = "elapsed",
                Phases = _ => new[]
                {
                    new Phase("Charge", 0f),
                    new Phase("Release", ShinraVfxTiming.ChargeEnd),
                    new Phase("Expand", ShinraVfxTiming.FlashEnd),
                    new Phase("Peak", ShinraVfxTiming.PeakTime),
                    new Phase("Shell ends", ShinraVfxTiming.ShellEnd),
                },
            },
        };

        public static Kit For(string label) => All.FirstOrDefault(k => label.StartsWith(k.Prefix, StringComparison.Ordinal));

        private static Phase[] SlamPhases() => new[]
        {
            new Phase("Gather", 0f),
            new Phase("Fuse", SixPathsSlamTiming.FuseAt),
            new Phase("Hang", SixPathsSlamTiming.HangAt),
            new Phase("Fall", SixPathsSlamTiming.FallAt),
            new Phase("Land", SixPathsSlamTiming.LandAt),
            new Phase("Fade", SixPathsSlamTiming.FadeAt),
        };

        private static Phase[] RingPhases()
        {
            // Named in the order SixPathsShapes.Cycle lists them.
            string[] names = { "Orb", "Rod", "Blade", "Shield" };
            if (names.Length != SixPathsShapes.Cycle.Length)
                throw new InvalidOperationException("SixPathsShapes.Cycle changed length; name its forms in Catalog.cs");
            return Enumerable.Range(0, names.Length).Select(i => new Phase(
                "→ " + names[(i + 1) % names.Length],
                i * SixPathsTiming.CycleSeconds + SixPathsTiming.HoldSeconds)).Prepend(new Phase(names[0], 0f)).ToArray();
        }
    }
}
