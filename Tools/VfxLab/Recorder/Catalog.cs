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
    /// an entry here. Every [RimArtDebug] entry of kind Cell whose full label starts with the
    /// prefix is recorded; "clear" entries are kind Now and are not.
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
                    : label.Contains("bloom") ? BloomPhases()
                    : label.Contains("maw") ? TwinMawPhases()
                    : label.Contains("rods") ? RodsPhases()
                    : label.Contains("serpent") ? SerpentPhases()
                    : label.Contains("repulse") ? RepulsePhases()
                    : label.Contains("umbrella") ? UmbrellaPhases(label.Contains("canopy"))
                    : label.Contains("sheet") ? Array.Empty<Phase>() : RingPhases(),
                LoopSeconds = label => label.Contains("slam") || label.Contains("bloom") || label.Contains("maw") || label.Contains("rods") || label.Contains("serpent") || label.Contains("repulse") || label.Contains("umbrella") || label.Contains("sheet")
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
            new Phase("Exit: sink", SixPathsSlamTiming.ExitAt),
        };

        private static Phase[] BloomPhases() => new[]
        {
            new Phase("Sink", 0f),
            new Phase("Unfurl", SixPathsBloomTiming.UnfurlAt),
            new Phase("Poise", SixPathsBloomTiming.PoiseAt),
            new Phase("Fold shut", SixPathsBloomTiming.FoldAt),
            new Phase("Cocoon / heal", SixPathsBloomTiming.ShutAt),
            new Phase("Uncurl", SixPathsBloomTiming.UncurlAt),
            new Phase("Orb returns", SixPathsBloomTiming.ReturnAt),
        };

        private static Phase[] TwinMawPhases() => new[]
        {
            new Phase("Orbs sink", 0f),
            new Phase("Armed", SixPathsTwinMawTiming.ArmedAt),
            new Phase("Jaws rise", SixPathsTwinMawTiming.TriggerAt),
            new Phase("Shut / bite", SixPathsTwinMawTiming.SnapAt),
            new Phase("Hold", SixPathsTwinMawTiming.ShutAt),
            new Phase("Release", SixPathsTwinMawTiming.ReleaseAt),
            new Phase("Orbs return", SixPathsTwinMawTiming.GoneAt),
        };

        private static Phase[] RodsPhases() => new[]
        {
            new Phase("Orbs sink", 0f),
            new Phase("Crack", SixPathsRodsTiming.CrackAt),
            new Phase("Rods up", SixPathsRodsTiming.RiseAt),
            new Phase("Wall stands", SixPathsRodsTiming.StandAt),
            new Phase("Back down", SixPathsRodsTiming.RetractAt),
            new Phase("Orbs return", SixPathsRodsTiming.GoneAt),
        };

        private static Phase[] SerpentPhases() => new[]
        {
            new Phase("Prepare", 0f),
            new Phase("Seek", SixPathsSerpentTiming.SeekAt),
            new Phase("Wrap", SixPathsSerpentTiming.ReachAt),
            new Phase("Restrained", SixPathsSerpentTiming.CatchAt),
            new Phase("Release", SixPathsSerpentTiming.ReleaseAt),
            new Phase("Reformed", SixPathsSerpentTiming.ReformAt),
        };

        private static Phase[] RepulsePhases() => new[]
        {
            new Phase("Two orbs gather", 0f),
            new Phase("Posts and ropes", SixPathsRepulseTiming.FormAt),
            new Phase("Brace / compress", SixPathsRepulseTiming.LoadAt),
            new Phase("Release / level launch", SixPathsRepulseTiming.LaunchAt),
            new Phase("Brake", SixPathsRepulseTiming.StopAt),
            new Phase("Two orbs follow", SixPathsRepulseTiming.RecallAt),
        };

        private static Phase[] UmbrellaPhases(bool canopy) => new[]
        {
            new Phase("Form umbrella", 0f),
            new Phase("Thrust", SixPathsUmbrellaTiming.ThrustAt),
            new Phase(canopy ? "Canopy opens" : "Guard opens", SixPathsUmbrellaTiming.OpenAt),
            new Phase(canopy ? "Moving cover" : "Front guard", SixPathsUmbrellaTiming.UpAt),
            new Phase("Folds", SixPathsUmbrellaTiming.CloseAt),
            new Phase("Held again", SixPathsUmbrellaTiming.ClosedAt),
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
