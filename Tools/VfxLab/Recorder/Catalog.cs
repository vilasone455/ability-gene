using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Previews that draw the same frame for their first half second on purpose (the sketch's stand-in
        /// walks up, and the port draws no stand-in): record them whole instead of as a still.
        /// </summary>
        public Func<string, bool> StartsStill = _ => false;
        /// <summary>Keep one frame in this many: a cutscene that rebuilds big meshes every frame would make a recording too big to load.</summary>
        public Func<string, int> KeepEvery = _ => 1;

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
                    : label.Contains("current") ? CurrentPhases()
                    : label.Contains("umbrella") ? UmbrellaPhases(label.Contains("canopy"))
                    : label.Contains("sheet") ? Array.Empty<Phase>() : RingPhases(),
                LoopSeconds = label => label.Contains("slam") || label.Contains("bloom") || label.Contains("maw") || label.Contains("rods") || label.Contains("serpent") || label.Contains("repulse") || label.Contains("current") || label.Contains("umbrella") || label.Contains("sheet")
                    ? null : SixPathsTiming.CycleSeconds * SixPathsShapes.Cycle.Length,
            },
            new Kit
            {
                Name = "Flying Thunder God", Prefix = "Flying Thunder God:", Component = typeof(MapComponent_ThunderGodPreview), Clock = "seconds",
                Phases = label => label.Contains("chain") ? ChainPhases(label.Contains("5 targets") ? 5 : 3, label.Contains("jumps back"))
                    : label.Contains("guiding") ? GuidingPhases()
                    : label.Contains("rasengan") ? RasenganPhases(label.Contains("teleport"), label.Contains("wall"))
                    : JumpPhases(label.Contains("in enemy")),
            },
            new Kit
            {
                Name = "Power Pole", Prefix = "Power Pole:", Component = typeof(MapComponent_PowerPolePreview), Clock = "seconds",
                Phases = label => label.Contains("sweep") ? SweepPhases() : label.Contains("vault") ? StrikePhases() : ThrustPhases(),
            },
            new Kit
            {
                Name = "Paper Bomb", Prefix = "Paper Bomb:", Component = typeof(MapComponent_PaperBombPreview), Clock = "seconds",
                Phases = label => label.Contains("tag line") ? TagLinePhases() : label.Contains("shroud") ? ShroudPhases() : TagThrowPhases(),
            },
            new Kit
            {
                Name = "Bank Shot", Prefix = "Bank Shot:", Component = typeof(MapComponent_BankShotPreview), Clock = "seconds",
                Phases = label => BankShotPhases(label.Contains("corridor") ? BankShotPath.Scene.Corridor : label.Contains("room") ? BankShotPath.Scene.Room : BankShotPath.Scene.Corner),
            },
            new Kit
            {
                Name = "Bubble Pipe", Prefix = "Bubble Pipe:", Component = typeof(MapComponent_BubblePipePreview), Clock = "seconds",
                Phases = label => label.Contains("eye pop") ? EyePopPhases() : DriftingBurstPhases(),
            },
            new Kit
            {
                Name = "Water Gun", Prefix = "Water Gun:", Component = typeof(MapComponent_WaterGunPreview), Clock = "seconds",
                Phases = label => label.Contains("pump") ? PumpPhases() : StreamPhases(),
            },
            new Kit
            {
                Name = "Chain Sickle", Prefix = "Chain Sickle:", Component = typeof(MapComponent_ChainSicklePreview), Clock = "seconds",
                Phases = label => label.Contains("refused") ? new[] { new Phase("Snagged (Stake refused: too heavy)", 0f) }
                    : label.Contains("stake") ? StakePhases()
                    : SnagPhases(label.Contains("raider") ? 1 : label.Contains("muffalo") ? 2 : label.Contains("thrumbo") ? 3 : 0),
            },
            new Kit
            {
                Name = "Flame Gauntlet", Prefix = "Flame Gauntlet:", Component = typeof(MapComponent_FlameGauntletPreview), Clock = "seconds",
                Phases = label => label.Contains("release")
                    ? ReleasePhases(label.Contains("too cold") ? 3f : label.Contains("8 heat") ? 8f : 20f)
                    : DevourPhases(label.Contains("burning pawn") ? 1 : label.Contains("starting hot") ? 2 : 0, label.Contains("south") ? 270f : 0f),
            },
            new Kit
            {
                Name = "Vacuum", Prefix = "Vacuum:", Component = typeof(MapComponent_VacuumPreview), Clock = "seconds",
                Phases = label => label.Contains("digest") ? DigestPhases()
                    : label.Contains("spit") ? SpitPhases()
                    : SuckPhases(label.Contains("loose") ? 2 : 3),
            },
            new Kit
            {
                Name = "Samehada", Prefix = "Samehada:", Component = typeof(MapComponent_SamehadaPreview), Clock = "seconds",
                Phases = label => label.Contains("fusion") ? FusionPhases() : label.Contains("shark") ? SharkSkinPhases() : FeedPhases(),
            },
            new Kit
            {
                Name = "Susanoo", Prefix = "Susanoo:", Component = typeof(MapComponent_SusanooPreview), Clock = "seconds",
                Phases = label => label.Contains("raise") ? SusanooRaisePhases()
                    : label.Contains("block") ? SusanooBlockPhases()
                    : label.Contains("seal") ? SusanooSealPhases(true)
                    : label.Contains("hit") ? SusanooSealPhases(false)
                    : SusanooEndPhases(),
                StartsStill = label => label.Contains("raise"),
            },
            new Kit
            {
                Name = "Nezuko's Box", Prefix = "Nezuko's Box:", Component = typeof(MapComponent_NezukoBoxPreview), Clock = "seconds",
                Phases = label => label.Contains("go in") ? BoxGoInPhases()
                    : label.Contains("strike") ? BoxStrikePhases()
                    : label.Contains("come out") ? BoxCalmPhases(label.Contains("time up") ? NezukoExit.TimeUp : NezukoExit.Downed)
                    : new[] { new Phase("Loop", 0f) },
                StartsStill = label => label.Contains("go in"),
            },
            new Kit
            {
                Name = "Coil Gun", Prefix = "Coil Gun:", Component = typeof(MapComponent_CoilGunPreview), Clock = "seconds",
                Phases = label => label.Contains("chain arc") ? ChainArcPhases() : label.Contains("recharge") ? new[] { new Phase("Charging", 0f) } : CoilShotPhases(),
            },
            new Kit
            {
                Name = "Frost Gun", Prefix = "Frost Gun:", Component = typeof(MapComponent_FrostGunPreview), Clock = "seconds",
                Phases = label => label.Contains("shot") ? FrostShotPhases() : FlashFreezePhases(label.Contains("shatter")),
            },
            new Kit
            {
                Name = "Shadow Plexus", Prefix = "Shadow Plexus:", Component = typeof(MapComponent_ShadowPlexusPreview), Clock = "seconds",
                Phases = label => label.Contains("imitation") ? ImitationPhases(label.Contains("cut") ? ImitationEnd.Cut : label.Contains("dark") ? ImitationEnd.Dark : ImitationEnd.Released)
                    : label.Contains("seam") ? SeamPhases(label.Contains("rescue") ? SeamScene.Rescue : SeamScene.Rusher)
                    : label.Contains("grasp") ? GraspPhases(label.Contains("blocked") ? GraspScene.Blocked : label.Contains("rescue") ? GraspScene.Rescue : GraspScene.Grenade)
                    : label.Contains("double") ? DoublePhases(label.Contains("fire") ? DoubleEnd.FireGoesOut : DoubleEnd.TimeRunsOut)
                    : NeckBindPhases(label.Contains("cut")),
            },
            new Kit
            {
                Name = "Vergil", Prefix = "Vergil:", Component = typeof(MapComponent_VergilPreview), Clock = "seconds",
                Phases = label => label.Contains("summoned swords") ? SwordsPhases(label.Contains("spin"))
                    : label.Contains("yamato dash") ? DashPhases()
                    : label.Contains("judgement cut end") ? CutEndPhases()
                    : JudgementCutPhases(),
            },
            new Kit
            {
                Name = "Goku", Prefix = "Goku:", Component = typeof(MapComponent_GokuPreview), Clock = "seconds",
                Phases = label => label.Contains("solar flare") ? SolarFlarePhases()
                    : label.Contains("instant transmission") ? TransmissionPhases()
                    : label.Contains("kamehameha") ? KamehamehaPhases(label.Contains("warp"))
                    : SpiritBombPhases(label.Contains("alone") ? 0 : GokuSpiritBombTiming.ScriptLenders),
            },
            new Kit
            {
                // Before the "Gojo:" entry below: Kit.For takes the first prefix that matches.
                Name = "Gojo", Prefix = "Gojo: red", Component = typeof(MapComponent_GojoRedPreview), Clock = "seconds",
                Phases = GojoRedPhases,
            },
            new Kit
            {
                // Before "Gojo:", which would match it first. The sketch's markers (gojo-blue-v2.js): Point, Open, Hold, Rush, Burst.
                Name = "Gojo", Prefix = "Gojo: blue", Component = typeof(MapComponent_GojoBluePreview), Clock = "seconds",
                Phases = _ => new[]
                {
                    new Phase("Point", 0f), new Phase("Open", GojoBlue.OpenAt), new Phase("Hold", GojoBlue.FullAt),
                    new Phase("Rush", GojoBlue.ImplodeAt(GojoBlue.Hold)), new Phase("Burst", GojoBlue.BurstAt(GojoBlue.Hold)),
                },
            },
            new Kit
            {
                // Before "Gojo:" too. The sketch's markers (gojo-purple.js).
                Name = "Gojo", Prefix = "Gojo: purple", Component = typeof(MapComponent_HollowPurplePreview), Clock = "seconds",
                Phases = _ => HollowPurplePhases(),
            },
            new Kit
            {
                Name = "Gojo", Prefix = "Gojo:", Component = typeof(MapComponent_GojoPreview), Clock = "seconds",
                Phases = label => label.Contains("inside") ? VoidInsidePhases() : VoidOpenPhases(),
            },
            new Kit
            {
                Name = "Infinity Castle", Prefix = "Infinity Castle:", Component = typeof(MapComponent_InfinityCastlePreview), Clock = "seconds",
                Phases = label => label.Contains("open (take)") ? CastleTakePhases() : label.Contains("open (return)") ? CastleReturnPhases() : CastlePhases(),
            },
            new Kit
            {
                Name = "Obito", Prefix = "Kamui dimension:", Component = typeof(MapComponent_KamuiPreview), Clock = "seconds",
                Phases = _ => new[] { new Phase("Map", 0f) },
            },
            // Shirou's hand pictures come before the UBW entry, which takes every other "Trace:" label.
            new Kit
            {
                Name = "Trace", Prefix = "Trace: trace on", Component = typeof(MapComponent_TracePreview), Clock = "seconds",
                Phases = label => TraceOnPhases(label.Contains("swap") ? TraceOnScenario.SwapCopy : label.Contains("real") ? TraceOnScenario.RealWeapon
                    : label.Contains("downed") ? TraceOnScenario.Downed : TraceOnScenario.EmptyHand),
            },
            new Kit
            {
                Name = "Trace", Prefix = "Trace: reinforcement", Component = typeof(MapComponent_TracePreview), Clock = "seconds",
                Phases = _ => ReinforcementPhases(),
            },
            new Kit
            {
                Name = "Trace", Prefix = "Trace:", Component = typeof(MapComponent_UbwPreview), Clock = "seconds",
                Phases = label => label.Contains("commands") ? UbwCommandPhases() : label.Contains("cast") ? UbwCastPhases() : label.Contains("reveal") ? UbwRevealPhases() : UbwWorldPhases(),
                // The reveal shot rebuilds its gears, clouds, fire and rising swords every frame: 12 frames a second.
                KeepEvery = label => label.Contains("reveal") ? 5 : 1,
            },
            new Kit
            {
                Name = "Todo", Prefix = "Todo:", Component = typeof(MapComponent_TodoPreview), Clock = "seconds",
                Phases = label => label.Contains("boogie woogie") ? BoogiePhases(label.Contains("double"))
                    : label.Contains("take back") ? TakeBackPhases()
                    : label.Contains("stone throw") ? StoneThrowPhases()
                    : BlackFlashPhases(label.Contains("ordinary")),
            },
            new Kit
            {
                Name = "Accelerator", Prefix = "Accelerator: plasma", Component = typeof(MapComponent_PlasmaPreview), Clock = "seconds",
                Phases = label => PlasmaPhases(label.Contains("wall"), label.Contains("broken")),
            },
            new Kit
            {
                Name = "Accelerator", Prefix = "Accelerator: vector shove", Component = typeof(MapComponent_VectorShovePreview), Clock = "seconds",
                Phases = VectorShovePhases,
            },
            new Kit
            {
                // The sketch's markers (accelerator-vector-flick.js): Stand, then per kick Warm-up, Kick, Hit.
                Name = "Accelerator", Prefix = "Accelerator: vector flick", Component = typeof(MapComponent_FlickApplyPreview), Clock = "seconds",
                Phases = label => MapComponent_FlickApplyPreview.FlickPhases(label.Contains("three")).Select(p => new Phase(p.name, p.seconds)).ToArray(),
            },
            new Kit
            {
                // The sketch's markers (accelerator-vector-apply.js): Volley, Paused, Apply, Last round stops.
                Name = "Accelerator", Prefix = "Accelerator: vector apply", Component = typeof(MapComponent_FlickApplyPreview), Clock = "seconds",
                Phases = label => MapComponent_FlickApplyPreview.ApplyPhases(label.Contains("2 groups") ? 2 : 4).Select(p => new Phase(p.name, p.seconds)).ToArray(),
            },
            new Kit
            {
                Name = "Sasuke", Prefix = "Sasuke: raiko kusari", Component = typeof(MapComponent_RaikoKusariPreview), Clock = "seconds",
                Phases = label => RaikoKusariPhases(label.Contains("ring") ? RaikoScenario.Ring
                    : label.Contains("drifting") ? RaikoScenario.DriftingNet
                    : label.Contains("let go") ? RaikoScenario.LetGo
                    : label.Contains("fuma") ? RaikoScenario.FumaCorner : RaikoScenario.Fence),
            },
            new Kit
            {
                Name = "Sasuke", Prefix = "Sasuke: amaterasu", Component = typeof(MapComponent_AmaterasuPreview), Clock = "seconds",
                Phases = AmaterasuPhases,
            },
            new Kit
            {
                Name = "Sasuke", Prefix = "Sasuke: amenotejikara", Component = typeof(MapComponent_AmenotejikaraPreview), Clock = "seconds",
                Phases = _ => new[]
                {
                    new Phase("Before", 0f), new Phase("Swap + flash", MapComponent_AmenotejikaraPreview.Lead),
                    new Phase("Pattern fades", MapComponent_AmenotejikaraPreview.Lead + AmenotejikaraTiming.Ripple),
                },
            },
            new Kit
            {
                Name = "Sato", Prefix = "Sato: reset", Component = typeof(MapComponent_SatoResetPreview), Clock = "seconds",
                Phases = label => label.Contains("pieces") ? SatoPiecesPhases() : SatoResetPhases(),
                // The pieces lie still for their first second, then crumble.
                StartsStill = label => label.Contains("pieces"),
            },
            new Kit
            {
                Name = "Sato", Prefix = "Sato: headshot", Component = typeof(MapComponent_SatoResetPreview), Clock = "seconds",
                Phases = _ => SatoHeadshotPhases(),
            },
            new Kit
            {
                Name = "Sato", Prefix = "Sato: black ghost", Component = typeof(MapComponent_BlackGhostPreview), Clock = "seconds",
                // The sketch's markers (ajin-black-ghost-v2.js), from the preview's plan and TearGraphics, per scene and direction.
                Phases = label => MapComponent_BlackGhostPreview.PhasesFor(label).Select(p => new Phase(p.name, p.seconds)).ToArray(),
            },
            new Kit
            {
                Name = "Pain", Prefix = "Pain: bansho", Component = typeof(MapComponent_BanshoPreview), Clock = "seconds",
                Phases = label => BanshoPhases(label.Contains("thrumbo") ? BanshoScenario.Thrumbo
                    : label.Contains("blocked") ? BanshoScenario.Blocked : BanshoScenario.Sandbags),
            },
            new Kit
            {
                Name = "Pain", Prefix = "Pain: black receiver", Component = typeof(MapComponent_BlackReceiverPreview), Clock = "seconds",
                Phases = BlackReceiverPhases,
            },
            new Kit
            {
                Name = "Anchor", Prefix = "Clap teleport:", Component = typeof(MapComponent_ClapPreview), Clock = "seconds",
                Phases = label => ClapPhases(label.Contains("double")),
            },
            new Kit
            {
                Name = "Anchor", Prefix = "Mark flick:", Component = typeof(MapComponent_MarkFlickPreview), Clock = "seconds",
                Phases = label => label.Contains("lift")
                    ? new[] { new Phase("Reach out", 0f), new Phase("Mark lifted: card leaves", MarkFlick.Place), new Phase("Caught", MarkFlick.Place + MarkFlick.CatchFlight), new Phase("Clip ends", MarkFlick.CatchLength) }
                    : new[] { new Phase("Curl", 0f), new Phase("Card leaves the hand", MarkFlick.Release), new Phase("Mark placed", MarkFlick.Place), new Phase("Clip ends", MarkFlick.FlickLength) },
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
                // The sketch's markers (pain-shinra-tensei.js), from the timing class, per preview.
                Phases = label => ShinraDome.PreviewPhases(label.Contains("tap") ? 0f : label.Contains("1.5") ? 1.5f : 3f)
                    .Select(p => new Phase(p.name, p.seconds)).ToArray(),
                // The first 0.2 s draw nothing (the sketch's Pain raises his hands; the port draws no pawn).
                StartsStill = label => !label.Contains("frozen"),
            },
        };

        public static Kit For(string label) => All.FirstOrDefault(k => label.StartsWith(k.Prefix, StringComparison.Ordinal));

        // The sketch's markers: Gaze, Ignite, then per scene Spreads, Let go, Hits / Steps in or Cuts / Lands, Release.
        private static Phase[] AmaterasuPhases(string label)
        {
            var phases = new List<Phase> { new Phase("Gaze", 0f), new Phase("Ignite", AmaterasuTiming.Ignite) };
            if (label.Contains("spreads")) phases.Add(new Phase("Spreads", AmaterasuTiming.Spreads));
            bool kunai = label.Contains("kunai"), fuma = label.Contains("Fūma");
            if (kunai || fuma)
            {
                AmaterasuHeldScene s = AmaterasuHeldScene.Build(UnityEngine.Vector2.zero, fuma);
                phases.Add(new Phase("Let go", AmaterasuTiming.LetGo));
                if (kunai)
                {
                    phases.Add(new Phase("Hits", Math.Min(s.kunai[0].stopAt, s.kunai[1].stopAt)));
                    phases.Add(new Phase("Steps in", s.step));
                }
                else
                {
                    phases.Add(new Phase("Cuts", s.cuts[0]));
                    phases.Add(new Phase("Lands", s.landedAt));
                }
            }
            phases.Add(new Phase("Release", AmaterasuTiming.Release));
            return phases.Where(p => p.Seconds < AmaterasuTiming.Duration).OrderBy(p => p.Seconds).ToArray();
        }

        // The sketch's phases(): Rest, Warm-up, Pull, Lift (not a dragged one), Catch / Hit / Stop, Result.
        private static Phase[] BanshoPhases(BanshoScenario scenario)
        {
            BanshoTimes t = MapComponent_BanshoPreview.TimesFor(scenario);
            var phases = new List<Phase> { new Phase("Rest", 0f), new Phase("Warm-up", t.cast), new Phase("Pull", t.grip) };
            if (!t.heavy) phases.Add(new Phase("Lift", t.lift));
            phases.Add(new Phase(t.heavy ? "Stop" : t.blocked ? "Hit" : "Catch", t.arrive));
            phases.Add(new Phase("Result", t.down));
            return phases.ToArray();
        }

        // The sketch's markers per scenario, off BlackReceiverTiming and the three throws' script (BlackReceiverThrows).
        private static Phase[] BlackReceiverPhases(string label)
        {
            if (label.Contains("pushes"))
                return new[]
                {
                    new Phase("Pinned", 0f), new Phase("Raiders walk up", 0.2f), new Phase("Push", BlackReceiverTiming.PushAt),
                    new Phase("Result", BlackReceiverTiming.PushAt + BlackReceiverTiming.PushFly),
                };
            if (label.Contains("stabbed"))
                return new[]
                {
                    new Phase("Face-down", 0f), new Phase("Stab 1", BlackReceiverTiming.StabStart(0)), new Phase("Stab 2", BlackReceiverTiming.StabStart(1)),
                    new Phase("Stab 3", BlackReceiverTiming.StabStart(2)), new Phase("Pinned", BlackReceiverTiming.StabStart(2) + BlackReceiverTiming.StabIn),
                };
            if (label.Contains("goes down"))
                return new[]
                {
                    new Phase("Pinned", 0f), new Phase("Shots", BlackReceiverTiming.ShotsAt), new Phase("Pain down, rods break", BlackReceiverTiming.DownAt),
                    new Phase("Free", BlackReceiverTiming.DownAt + BlackReceiverTiming.FreeDelay + BlackReceiverTiming.GetUp),
                };
            var t = new BlackReceiverThrows();
            return new[]
            {
                new Phase("Rest", 0f), new Phase("Throw 1", t.starts[0]), new Phase("Hit 1", t.hits[0]), new Phase("Hit 2", t.hits[1]),
                new Phase("Hit 3: pinned", t.hits[2]), new Phase("Rod 1 breaks", t.breaks[0]), new Phase("All broken", t.breaks[2]),
            };
        }

        // The sketch's marks for the scenario, off the preview's script (RaikoKusariScene, timed by RaikoKusariTiming).
        private static Phase[] RaikoKusariPhases(RaikoScenario scenario) =>
            RaikoKusariScene.Build(scenario, default).Phases().Select(m => new Phase(m.Key, m.Value)).ToArray();

        private static Phase[] BankShotPhases(BankShotPath.Scene scene)
        {
            BankShotShot t = BankShotTiming.Script(scene, default);
            var phases = new List<Phase> { new Phase("Aim", 0f), new Phase("Charge", BankShotTiming.Lead), new Phase("Fire", t.FireAt) };
            for (int i = 0; i < t.Path.Bounces.Count; i++) phases.Add(new Phase("Bounce " + t.Path.Bounces[i].N, t.BounceAt(i)));
            phases.Add(new Phase(t.Path.End == BankShotEnd.Hit ? "Hit" : t.Path.End == BankShotEnd.Embed ? "Embed" : "Spent", t.EndAt));
            return phases.ToArray();
        }

        private static Phase[] ImitationPhases(ImitationEnd end)
        {
            ImitationPlan t = ShadowImitationTiming.Plan(end);
            var phases = new List<Phase> { new Phase("Line runs out", 0f), new Phase("Held", ShadowImitationTiming.ScriptCast) };
            if (t.Steps > 0) phases.Add(new Phase("Carrier walks", t.WalkStart));
            phases.Add(new Phase(end == ImitationEnd.Released ? "Released" : end == ImitationEnd.Cut ? "Line cut" : "Line goes dark", t.Release));
            return phases.ToArray();
        }

        private static Phase[] JudgementCutPhases() => new[]
        {
            new Phase("Sheathed", 0f),
            new Phase("Hand on the hilt", JudgementCutTiming.CastAt),
            new Phase("The draw / the sphere", JudgementCutTiming.OpenAt(JudgementCutTiming.Warm)),
            new Phase("Closes", JudgementCutTiming.CloseAt(JudgementCutTiming.Warm, JudgementCutTiming.Burst)),
        };

        private static Phase[] CutEndPhases() => new[]
        {
            new Phase("Raiders close in", 0f),
            new Phase("Hand on the hilt", JudgementCutEndTiming.CastAt),
            new Phase("Gone / the cuts", JudgementCutEndTiming.VanishAt),
            new Phase("Kneel and sheathe", JudgementCutEndTiming.BackAt),
            new Phase("Click: the cuts land", JudgementCutEndTiming.ClickAt),
        };

        private static Phase[] DashPhases() => new[]
        {
            new Phase("Ready", 0f),
            new Phase("Hand to hilt", YamatoDashTiming.CastAt),
            new Phase("Dash: the marks are set", YamatoDashTiming.LaunchAt),
            new Phase("Sheathe", YamatoDashTiming.ArriveAt),
            new Phase("Click: every mark lands", YamatoDashTiming.ClickAt),
        };

        private static Phase[] VoidOpenPhases() => new[]
        {
            new Phase("Stands", 0f),
            new Phase("Hand sign", UnlimitedVoidOpenTiming.CastAt),
            new Phase("Barrier closes", UnlimitedVoidOpenTiming.OpenAt),
            new Phase("Shrinks", UnlimitedVoidOpenTiming.FullAt),
            new Phase("Ball hangs", UnlimitedVoidOpenTiming.HangAt),
            new Phase("Ball breaks", UnlimitedVoidOpenTiming.BurstAt),
        };

        // The sketch's markers (gojo-purple.js phases()), the same for every scenario and aim.
        private static Phase[] HollowPurplePhases()
        {
            HollowPurpleTimes t = HollowPurple.Default;
            return new[]
            {
                new Phase("Blue", 0f), new Phase("Red", t.Fire), new Phase("Merge", t.Contact), new Phase("Ignite", t.Ignite),
                new Phase("Travel", t.Move), new Phase("Fade", t.Stop), new Phase("After", t.Gone),
            };
        }

        private static Phase[] VoidInsidePhases() => new[]
        {
            new Phase("White (map switch)", 0f),
            new Phase("Speed lines", UnlimitedVoidInsideTiming.SpeedLinesAt),
            new Phase("White light", UnlimitedVoidInsideTiming.LightAt),
            new Phase("Black hole", UnlimitedVoidInsideTiming.OpensAt),
            new Phase("Domain ends", UnlimitedVoidInsideTiming.Hold),
            new Phase("White (back)", UnlimitedVoidInsideTiming.WhiteBackAt),
        };

        private static Phase[] CastleTakePhases() => new[]
        {
            new Phase("Warm-up", 0f),
            new Phase("Strum", InfinityCastleOpenTiming.StrumAt),
            new Phase("Carrier follows", InfinityCastleOpenTiming.CasterDoor),
            new Phase("Result", InfinityCastleOpenTiming.TakeDuration - InfinityCastleOpenTiming.Hold),
        };

        private static Phase[] CastleReturnPhases() => new[]
        {
            new Phase("Castle ends", 0f),
            new Phase("Doors open", InfinityCastleOpenTiming.First),
            new Phase("Back", InfinityCastleOpenTiming.ReturnDuration - InfinityCastleOpenTiming.Hold),
        };

        private static Phase[] CastlePhases() => new[]
        {
            new Phase("Empty castle", 0f),
            new Phase("Carrier lands", InfinityCastleInsideTiming.CasterLands),
            new Phase("Enemies land", InfinityCastleInsideTiming.FirstEnemy),
            new Phase("Hold", InfinityCastleInsideTiming.Landed),
            new Phase("Release", InfinityCastleInsideTiming.Release),
            new Phase("Castle removed", InfinityCastleInsideTiming.FadeAt),
        };

        // The sketch's markers: Arm line, Wire, Steel, Done; Stow before them with a real weapon, Swap and New copy or
        // Down after them.
        private static Phase[] TraceOnPhases(TraceOnScenario scenario)
        {
            TraceTimes t = TraceOnTiming.First(scenario);
            var phases = new List<Phase> { new Phase("Arm line", t.At), new Phase("Wire", t.Wire), new Phase("Steel", t.Fill), new Phase("Done", t.Lit) };
            if (scenario == TraceOnScenario.RealWeapon) phases.Insert(0, new Phase("Stow", TraceOnTiming.CastAt));
            if (scenario == TraceOnScenario.SwapCopy)
            {
                phases.Add(new Phase("Swap", TraceOnTiming.SwapAt));
                phases.Add(new Phase("New copy", TraceOnTiming.Second.At));
            }
            if (scenario == TraceOnScenario.Downed) phases.Add(new Phase("Down", TraceOnTiming.SwapAt));
            return phases.ToArray();
        }

        private static Phase[] ReinforcementPhases() => new[]
        {
            new Phase("Cast", TraceReinforcementTiming.CastAt), new Phase("Run", TraceReinforcementTiming.Run),
            new Phase("Hits", TraceReinforcementTiming.Hit(0)), new Phase("Ends", TraceReinforcementTiming.End),
        };

        private static Phase[] UbwCastPhases()
        {
            UbwCastTiming.Plan t = UbwCastTiming.For(UbwCastTiming.Verse);
            return new[]
            {
                new Phase("Verse 1", 0f),
                new Phase("Release", t.Open),
                new Phase("Ring closes", t.Lit),
                new Phase("Taken", t.Taken),
                new Phase("Away", t.Clear),
                new Phase("World ends", t.Ends),
                new Phase("Back", t.Home),
            };
        }

        private static Phase[] UbwRevealPhases() => new[]
        {
            new Phase("White", 0f), new Phase("Sky and gears", UbwRevealTiming.White), new Phase("Tilt down, fire", 1.25f), new Phase("Crane up", 2.5f),
            new Phase("Blend", UbwRevealTiming.BlendFrom), new Phase("Hand-over", UbwRevealTiming.HandOver), new Phase("World", UbwRevealTiming.BarsOff + UbwRevealTiming.BarsFor),
        };

        /// <summary>The commands' previews share the sketch's order times: the order at 0.4 s, the first sword at 0.5 s.</summary>
        private static Phase[] UbwCommandPhases() => new[]
        {
            new Phase("Order", 0.4f),
            new Phase("Swords leave", 0.5f),
        };

        private static Phase[] UbwWorldPhases() => new[]
        {
            new Phase("White", 0f),
            new Phase("Fire runs out", UbwWorldTiming.Start),
            new Phase("World stands", UbwWorldTiming.Swept),
            new Phase("Close", UbwWorldTiming.CloseAt),
            new Phase("White", UbwWorldTiming.Shut),
        };

        private static Phase[] SwordsPhases(bool spins) => new[]
        {
            new Phase("Stands", 0f),
            new Phase("The blades rise", SummonedSwordsTiming.CastAt),
            spins ? new Phase("Spins and cuts", SummonedSwordsTiming.FormedAt) : new Phase("Fires on its own", SummonedSwordsTiming.FireAt),
            new Phase("Every blade breaks", SummonedSwordsTiming.StopAt),
        };

        private static Phase[] SeamPhases(SeamScene scene)
        {
            SeamPlan t = ShadowSeamTiming.Plan(scene);
            return new[]
            {
                new Phase("Line runs out", 0f),
                new Phase("Forks", ShadowSeamTiming.ScriptCast * ShadowSeamTiming.Fork),
                new Phase("Sewn", t.Sewn),
                new Phase("Taut at 4 cells", t.Taut),
                new Phase("Seam undone", t.Undo),
            };
        }

        private static Phase[] GraspPhases(GraspScene scene)
        {
            GraspShot t = ShadowGraspTiming.Script(scene, 0f, out _);
            return new[]
            {
                new Phase("Tendril runs out", 0f),
                new Phase("Hand opens and closes", t.Cast),
                new Phase("Slide", t.SlideStart),
                new Phase(scene == GraspScene.Blocked ? "Stopped by a pawn" : "Let go", t.Arrive),
            };
        }

        private static Phase[] DoublePhases(DoubleEnd end)
        {
            DoublePlan t = ShadowDoubleTiming.Plan(end);
            bool fire = end == DoubleEnd.FireGoesOut;
            return new[]
            {
                new Phase("Shadow slides out", 0f),
                new Phase("Stands up", ShadowDoubleTiming.ScriptCast),
                new Phase("Copies the steps", t.WalkStart),
                new Phase("Casts from the double", t.CastStart),
                new Phase(fire ? "Raider goes dark: line dies" : "Imitation ends", t.Release),
                new Phase(fire ? "Double goes dark" : "Double sinks", t.Gone),
            };
        }

        private static Phase[] NeckBindPhases(bool cut) => new[]
        {
            new Phase("Hands crawl along the line", 0f),
            new Phase("Climb the body", ShadowNeckBindTiming.Crawl),
            new Phase("Closed on the neck", ShadowNeckBindTiming.Crawl + ShadowNeckBindTiming.ScriptClimb),
            new Phase(cut ? "Line cut: hands fall off" : "Unconscious", ShadowNeckBindTiming.Release(cut)),
        };

        private static Phase[] SolarFlarePhases()
        {
            SolarFlarePlan t = GokuSolarFlareTiming.Plan();
            return new[] { new Phase("Enemies close in", 0f), new Phase("Hands to the face", t.Cast), new Phase("Flash / stunned", t.Flash), new Phase("Stun ends, still blind", t.Wake) };
        }

        private static Phase[] TransmissionPhases()
        {
            TransmissionPlan t = GokuInstantTransmissionTiming.Plan();
            return new[] { new Phase("Stand", 0f), new Phase("Fingers to the forehead", t.Cast), new Phase("Vanish", t.Go), new Phase("Arrive", t.Arrive), new Phase("Result", t.Landed) };
        }

        private static Phase[] KamehamehaPhases(bool warp)
        {
            KamehamehaPlan t = GokuKamehamehaTiming.Plan(warp);
            var phases = new List<Phase> { new Phase("Stand", 0f), new Phase("Ka-me-ha-me (channel)", t.Cast) };
            if (warp) phases.Add(new Phase("Warp", t.Go));
            phases.Add(new Phase("HA", t.Fire));
            phases.Add(new Phase("Beam holds", t.Out));
            phases.Add(new Phase("Beam lets go", t.Release));
            phases.Add(new Phase("End blast", t.Blast));
            return phases.ToArray();
        }

        private static Phase[] SpiritBombPhases(int lenders)
        {
            SpiritBombPlan t = GokuSpiritBombTiming.Plan(lenders);
            var phases = new List<Phase> { new Phase("Stand", 0f), new Phase("Channel", t.Cast) };
            if (lenders > 0) phases.Add(new Phase("Lenders join", t.Joins(0)));
            phases.Add(new Phase("Throw", t.Release));
            phases.Add(new Phase("Grind", t.Hit));
            phases.Add(new Phase("Detonation", t.Dome));
            phases.Add(new Phase("Burst", t.Burst));
            phases.Add(new Phase("Aftermath", t.Gone));
            return phases.ToArray();
        }

        private static Phase[] FrostShotPhases()
        {
            var phases = new List<Phase>();
            for (int k = 0; k < FrostGunShotTiming.ScriptShots; k++)
            {
                phases.Add(new Phase("Shot " + (k + 1), FrostGunShotTiming.Fire(k)));
                phases.Add(new Phase("Hit (" + (k + 1) + " chilled)", FrostGunShotTiming.Hit(k, FrostGunShotTiming.ScriptDistance)));
            }
            return phases.ToArray();
        }

        private static Phase[] FlashFreezePhases(bool shatter)
        {
            float hit = FrostGunFreezeTiming.Hit(FrostGunFreezeTiming.ScriptWarmup, FrostGunFreezeTiming.ScriptDistance - FrostGunGraphics.MuzzleAlong);
            var phases = new List<Phase>
            {
                new Phase("Charge", 0f),
                new Phase("Beam", FrostGunFreezeTiming.ScriptWarmup),
                new Phase("Freeze", hit),
                new Phase("Frozen", hit + FrostGunFreezeTiming.Grow),
            };
            if (shatter) phases.Add(new Phase("Shatter", hit + FrostGunFreezeTiming.Grow + FrostGunFreezeTiming.ScriptShatterAfter));
            else
            {
                phases.Add(new Phase("Thaw", hit + FrostGunFreezeTiming.ScriptFreeze));
                phases.Add(new Phase("Puddle", hit + FrostGunFreezeTiming.ScriptFreeze + FrostGunFreezeTiming.Thaw));
            }
            return phases.ToArray();
        }

        private static Phase[] StreamPhases()
        {
            float d = WaterGunStreamTiming.ScriptDistance, hold = WaterGunStreamTiming.ScriptHold;
            return new[]
            {
                new Phase("Rest", 0f),
                new Phase("Raise", WaterGunStreamTiming.Raise0),
                new Phase("Fire", WaterGunStreamTiming.Fire),
                new Phase("Hit", WaterGunStreamTiming.Hit(d)),
                new Phase("Lower", WaterGunStreamTiming.Lower0(d, hold)),
            };
        }

        private static Phase[] SuckPhases(int things) => new[]
        {
            new Phase("Wand", 0f),
            new Phase("Rise", VacuumSuckTiming.Rise0),
            new Phase("Pull", VacuumSuckTiming.Pull0),
            new Phase("Swallow", VacuumSuckTiming.Swallow),
            new Phase("Result", VacuumSuckTiming.Result(things)),
            new Phase("Sink", VacuumSuckTiming.Sink0(things, VacuumSuckTiming.ScriptHold)),
        };

        private static Phase[] SpitPhases() => new[]
        {
            new Phase("Wand", 0f),
            new Phase("Rise", VacuumSpitTiming.Rise0),
            new Phase("Heave", VacuumSpitTiming.Heave),
            new Phase("Launch", VacuumSpitTiming.Launch),
            new Phase("Impact", VacuumSpitTiming.Impact),
            new Phase("Sink", VacuumSpitTiming.Sink0(VacuumSpitTiming.ScriptHold)),
        };

        private static Phase[] DigestPhases()
        {
            var shot = new VacuumDigestShot();
            return new[]
            {
                new Phase("Wand", 0f),
                new Phase("Rise", VacuumDigestTiming.Rise0),
                new Phase("Chew", VacuumDigestTiming.Chew0),
                new Phase("Burp", VacuumDigestTiming.Done(shot)),
                new Phase("Sink", VacuumDigestTiming.Sink0(shot)),
            };
        }

        private static Phase[] ChainArcPhases()
        {
            float fire = CoilGunArcTiming.ScriptFire;
            var phases = new List<Phase> { new Phase("Rest", 0f), new Phase("Charge", CoilGunArcTiming.Lead), new Phase("Fire", fire) };
            for (int i = 0; i < CoilGunArcTiming.ScriptTargets.Length; i++)
                phases.Add(new Phase("Hit " + (i + 1) + (i == CoilGunArcTiming.ScriptSoaked ? " (Soaked)" : ""), CoilGunArcTiming.Hit(fire, i)));
            phases.Add(new Phase("Faded", CoilGunArcTiming.LastHit(fire, CoilGunArcTiming.ScriptTargets.Length) + CoilGunArcTiming.Lit + CoilGunArcTiming.Fade + CoilGunArcTiming.Strike));
            return phases.ToArray();
        }

        private static Phase[] CoilShotPhases() => new[]
        {
            new Phase("Rest", 0f),
            new Phase("Round 1", CoilGunShotTiming.Fired(0)),
            new Phase("Round 2", CoilGunShotTiming.Fired(1)),
            new Phase("Hit 1", CoilGunShotTiming.Impact(0)),
            new Phase("Hit 2", CoilGunShotTiming.Impact(1)),
        };

        private static Phase[] FeedPhases()
        {
            var phases = new List<Phase> { new Phase("Rest", 0f) };
            for (int i = 0; i < SamehadaFeedTiming.ScriptHits; i++)
            {
                phases.Add(new Phase("Hit " + (i + 1), SamehadaFeedTiming.HitStart(i)));
                phases.Add(new Phase("Bite", SamehadaFeedTiming.Bite(i)));
            }
            phases.Add(new Phase("Result", SamehadaFeedTiming.Result));
            return phases.ToArray();
        }

        private static Phase[] SusanooRaisePhases()
        {
            const float w = SusanooTiming.WarmUp, lead = SusanooTiming.Lead;
            return new[]
            {
                new Phase("Itachi alone", 0f), new Phase("Sharingan", lead), new Phase("Ribs rise", lead + .10f * w),
                new Phase("Skull + skeletal arm", lead + .35f * w), new Phase("Armour, cape, face", lead + .55f * w),
                new Phase("Eyes light", lead + .93f * w), new Phase("Complete (idle)", lead + w),
            };
        }

        private static Phase[] SusanooBlockPhases() => new[]
        {
            new Phase("Susanoo up", 0f), new Phase("Shooter aims, mirror turns", SusanooTiming.Aim), new Phase("Rounds", SusanooTiming.Shots[0]),
            new Phase("Sword raider runs in", SusanooTiming.Run), new Phase("Mirror crosses", SusanooTiming.Cross0), new Phase("Blow blocked", SusanooTiming.Blow),
        };

        private static Phase[] SusanooSealPhases(bool weak)
        {
            SusanooTiming.Seal T = SusanooTiming.SealTimes(weak);
            var start = new List<Phase>
            {
                new Phase("Susanoo up", 0f), new Phase("Wind-up", SusanooTiming.SwingAt), new Phase("Swing", T.windEnd), new Phase("Blade shoots out", T.strikeEnd),
            };
            if (!weak)
                start.AddRange(new[] { new Phase("Pierce (30 damage)", T.pierce), new Phase("Blade pulls back", T.pullFrom), new Phase("Arm back", T.retracted) });
            else
                start.AddRange(new[] { new Phase("Pierce", T.pierce), new Phase("Pulled into the gourd", T.pullFrom), new Phase("Sealed", T.sealedAt) });
            return start.ToArray();
        }

        private static Phase[] SusanooEndPhases()
        {
            SusanooTiming.End T = SusanooTiming.EndTimes();
            const float b = SusanooTiming.BreakAt, D = SusanooTiming.BreakUp;
            return new[]
            {
                new Phase("Last moment", 0f), new Phase("Dims", SusanooTiming.EndIdle), new Phase("Head breaks up", b), new Phase("Arms, mirror, blade", b + .15f * D),
                new Phase("Body", b + .3f * D), new Phase("Bones sink", b + .55f * D), new Phase("Coughs blood", T.cough0), new Phase("Gone", b + D),
            };
        }

        private static Phase[] BoxGoInPhases() => new[]
        {
            new Phase("Approach", NezukoBoxGoInTiming.Approach0),
            new Phase("Open", NezukoBoxGoInTiming.Open0),
            new Phase("Enter", NezukoBoxGoInTiming.Enter0),
            new Phase("Shut", NezukoBoxGoInTiming.Shut0),
            new Phase("Asleep", NezukoBoxGoInTiming.Latch),
        };

        private static Phase[] BoxStrikePhases() => new[]
        {
            new Phase("Rumble", NezukoBoxComeOutTiming.Rumble0),
            new Phase("Burst", NezukoBoxComeOutTiming.BurstAt),
            new Phase("Leap", NezukoBoxComeOutTiming.Leap0),
            new Phase("Land", NezukoBoxComeOutTiming.Land(NezukoBoxComeOutTiming.Flight)),
            new Phase("Strike", NezukoBoxComeOutTiming.StrikeAt(NezukoBoxComeOutTiming.Flight)),
            new Phase("Shut", NezukoBoxComeOutTiming.Shut0(NezukoExit.Strike, NezukoBoxComeOutTiming.Flight)),
        };

        private static Phase[] BoxCalmPhases(NezukoExit kind) => new[]
        {
            new Phase("Open", NezukoBoxComeOutTiming.Open0),
            new Phase("Out", NezukoBoxComeOutTiming.Out0),
            new Phase("Shut", NezukoBoxComeOutTiming.Shut0(kind, NezukoBoxComeOutTiming.Flight)),
        };

        private static Phase[] SharkSkinPhases() => new[]
        {
            new Phase("Rest", 0f),
            new Phase("Tear", SamehadaSharkSkinTiming.Tear0),
            new Phase("Flare", SamehadaSharkSkinTiming.Flare0),
            new Phase("Sweep", SamehadaSharkSkinTiming.Sweep0),
            new Phase("Result", SamehadaSharkSkinTiming.Result),
        };

        private static Phase[] FusionPhases() => new[]
        {
            new Phase("Rest", 0f),
            new Phase("Merge", SamehadaFusionTiming.Merge0),
            new Phase("Fused", SamehadaFusionTiming.Fused0),
            new Phase("Revert", SamehadaFusionTiming.Revert0),
            new Phase("Result", SamehadaFusionTiming.Done),
        };

        private static Phase[] SnagPhases(int target)
        {
            float reel = ChainSickleRule.Script(target).Reel;
            return new[]
            {
                new Phase("Rest", 0f),
                new Phase("Spin", ChainSickleSnagTiming.Spin0),
                new Phase("Throw", ChainSickleSnagTiming.Throw0),
                new Phase("Wrap", ChainSickleSnagTiming.Hit),
                new Phase("Reel", ChainSickleSnagTiming.Reel0),
                new Phase("Result", ChainSickleSnagTiming.ReelEnd(reel)),
            };
        }

        private static Phase[] DevourPhases(int scenario, float aim)
        {
            FlameDevourShot shot = FlameGauntletDevourGraphics.Script(default, aim, scenario, scenario == 2 ? 14f : 0f);
            var phases = new List<Phase> { new Phase("Rest", 0f), new Phase("Wind-up", FlameGauntletTiming.Lead), new Phase("Pull", FlameDevourTiming.Pull) };
            if (shot.RefusedAt >= 0f) phases.Add(new Phase("Too hot", shot.RefusedAt));
            phases.Add(new Phase("Result", shot.Result));
            return phases.ToArray();
        }

        private static Phase[] ReleasePhases(float heat)
        {
            FlameReleaseShot shot = FlameGauntletReleaseGraphics.Script(default, 0f, heat, true);
            if (shot.Lit == 0)
                return new[] { new Phase("Rest", 0f), new Phase("Wind-up", FlameGauntletTiming.Lead), new Phase("Too cold", FlameReleaseTiming.Go), new Phase("Result", FlameReleaseTiming.Result(shot)) };
            return new[]
            {
                new Phase("Rest", 0f),
                new Phase("Wind-up", FlameGauntletTiming.Lead),
                new Phase("Release", FlameReleaseTiming.Go),
                new Phase("Wave", FlameReleaseTiming.Go + FlameReleaseTiming.JetLand),
                new Phase("Stops", FlameReleaseTiming.Stop(shot)),
                new Phase("Result", FlameReleaseTiming.Result(shot)),
            };
        }

        private static Phase[] StakePhases()
        {
            float swing = ChainSickleStakeTiming.ScriptSwingAt;
            return new[]
            {
                new Phase("Snagged", 0f),
                new Phase("Yank", ChainSickleStakeTiming.Yank0),
                new Phase("Staked", ChainSickleStakeTiming.Staked),
                new Phase("Step in", ChainSickleStakeTiming.Step0(swing)),
                new Phase("Cut", swing),
                new Phase("Result", ChainSickleStakeTiming.Cut(swing)),
            };
        }

        private static Phase[] PumpPhases() => new[]
        {
            new Phase("Rest", 0f),
            new Phase("Raise", WaterGunPumpTiming.Raise0),
            new Phase("Pump", WaterGunPumpTiming.Pump0),
            new Phase("Blast", WaterGunPumpTiming.Blast),
            new Phase("Spray ends", WaterGunPumpTiming.SprayEnd),
            new Phase("Up", WaterGunPumpTiming.Up(WaterGunPumpTiming.ScriptDown)),
            new Phase("Lower", WaterGunPumpTiming.Lower0(WaterGunPumpTiming.ScriptGunHold)),
        };

        private static Phase[] TagThrowPhases() => new[]
        {
            new Phase("Tear off", 0f),
            new Phase("Flight", PaperBombTagThrowTiming.Release),
            new Phase("Fuse", PaperBombTagThrowTiming.LandAt),
            new Phase("Burst", PaperBombTagThrowTiming.BurstAt(PaperBombTagThrowTiming.ScriptFuse)),
        };

        private static Phase[] TagLinePhases() => new[]
        {
            new Phase("Flick", 0f),
            new Phase("Strip runs out", PaperBombTagLineTiming.Flick),
            new Phase("Armed", PaperBombTagLineTiming.Lay),
            new Phase("Hand seal", PaperBombTagLineTiming.ScriptFuseAt - PaperBombGraphics.Seal),
            new Phase("Fuse and bursts", PaperBombTagLineTiming.ScriptFuseAt),
            new Phase("Aftermath", PaperBombTagLineTiming.ScriptDuration - PaperBombTagLineTiming.Aftermath),
        };

        private static Phase[] ShroudPhases() => new[]
        {
            new Phase("Wind-up", 0f),
            new Phase("Tags fly", PaperBombShroudTiming.Wind),
            new Phase("Held", PaperBombShroudTiming.FirstLand),
            new Phase("Hand seal", PaperBombShroudTiming.SealAt(PaperBombShroudTiming.ScriptHeld)),
            new Phase("Burst", PaperBombShroudTiming.BurstAt(PaperBombShroudTiming.ScriptHeld)),
        };

        private static Phase[] DriftingBurstPhases() => new[]
        {
            new Phase("Rest", 0f),
            new Phase("Raise", BubblePipeGraphics.Lead),
            new Phase("Blow", BubblePipeDriftingBurstTiming.BlowAt),
            new Phase("Drift", BubblePipeDriftingBurstTiming.AllOutAt(BubblePipeDriftingBurstTiming.ScriptCount)),
            new Phase("Expire", BubblePipeDriftingBurstTiming.ExpireAt(BubblePipeDriftingBurstTiming.ScriptLife)),
            new Phase("Lower", BubblePipeDriftingBurstTiming.ScriptLowerAt(BubblePipeDriftingBurstTiming.ScriptLife)),
        };

        private static Phase[] EyePopPhases() => new[]
        {
            new Phase("Rest", 0f),
            new Phase("Raise", BubblePipeGraphics.Lead),
            new Phase("Blow", BubblePipeEyePopTiming.BlowAt),
            new Phase("Fly", BubblePipeEyePopTiming.LaunchAt),
            new Phase("Hit", BubblePipeEyePopTiming.ScriptHitAt),
            new Phase("Clear", BubblePipeEyePopTiming.ScriptHitAt + BubblePipeEyePopTiming.ScriptDebuff),
            new Phase("Lower", BubblePipeEyePopTiming.ScriptLowerAt),
        };

        private static Phase[] ThrustPhases() => new[]
        {
            new Phase("Wind-up", 0f),
            new Phase("Extend", PowerPoleThrustTiming.ThrustAt),
            new Phase("Hit and carry", PowerPoleThrustTiming.HitAt),
            new Phase("Hold", PowerPoleThrustTiming.PushedAt),
            new Phase("Retract", PowerPoleThrustTiming.RetractAt),
            new Phase("Result", PowerPoleThrustTiming.HomeAt),
        };

        private static Phase[] SweepPhases() => new[]
        {
            new Phase("Wind-up", 0f),
            new Phase("Swing", PowerPoleSweepTiming.SwingAt),
            new Phase("Retract", PowerPoleSweepTiming.RetractAt),
            new Phase("Result", PowerPoleSweepTiming.HomeAt),
        };

        private static Phase[] StrikePhases() => new[]
        {
            new Phase("Plant", 0f),
            new Phase("Pole pushes up", PowerPoleStrikeTiming.Script.LaunchAt),
            new Phase("Whip overhead", PowerPoleStrikeTiming.Script.PeakAt),
            new Phase("Strike", PowerPoleStrikeTiming.Script.StrikeStartAt),
            new Phase("Land and retract", PowerPoleStrikeTiming.Script.LandAt),
            new Phase("Result", PowerPoleStrikeTiming.Script.HomeAt),
        };

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

        private static Phase[] CurrentPhases() => new[]
        {
            new Phase("One orb prepares", 0f),
            new Phase("Pull inward", SixPathsCurrentTiming.PullAt),
            new Phase("Hold in front", SixPathsCurrentTiming.HoldAt),
            new Phase("Fire outward", SixPathsCurrentTiming.ReleaseAt),
            new Phase("Settle", SixPathsCurrentTiming.StopAt),
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

        private static Phase[] BoogiePhases(bool twice)
        {
            float warmup = twice ? BoogieWoogie.DoubleWarmup : BoogieWoogie.ClapWarmup;
            var phases = new List<Phase> { new Phase("Wind-up", 0f) };
            if (twice) phases.Add(new Phase("First clap", BoogieWoogie.FirstContactAt(warmup)));
            phases.Add(new Phase("Contact: swap, ink frames", warmup));
            phases.Add(new Phase("Colour back", warmup + BoogieWoogie.Ink));
            phases.Add(new Phase("Burst gone", warmup + BoogieWoogie.Life));
            phases.Add(new Phase("Flecks gone", warmup + BoogieWoogie.FleckFrom + BoogieWoogie.Fleck));
            return phases.ToArray();
        }

        private static Phase[] StoneThrowPhases() => new[]
        {
            new Phase("Charge", StoneThrow.Charge), new Phase("Release blade", StoneThrow.Release),
            new Phase("Touches down, skids", StoneThrow.Place), new Phase("Resting", StoneThrow.Place + StoneThrow.SkidTime),
        };

        private static Phase[] TakeBackPhases() => new[]
        {
            new Phase("Reach out", 0f), new Phase("Stone flares", StoneThrow.Place - StoneThrow.FlareLead),
            new Phase("Stone leaves the cell", StoneThrow.Place), new Phase("Caught", StoneThrow.CatchTime(StoneThrow.Place)),
        };

        // The sketch's phases(): Stand, Channel, Release, Burst; the broken channel ends at the break.
        private static Phase[] PlasmaPhases(bool wall, bool broken)
        {
            if (broken)
                return new[] { new Phase("Stand", 0f), new Phase("Channel", Plasma.Lead), new Phase("Broken", Plasma.Lead + MapComponent_PlasmaPreview.BreakAt) };
            return new[]
            {
                new Phase("Stand", 0f), new Phase("Channel", Plasma.Lead), new Phase("Release", Plasma.Lead + Plasma.Channel),
                new Phase("Burst", MapComponent_PlasmaPreview.HitAt(wall)),
            };
        }

        // accelerator-vector-shove.js's phases(): Stand, Mace hits (not for the chunk), Touch, Throw, Slam / Lands / Hit.
        private static Phase[] VectorShovePhases(string label)
        {
            VectorShoveScene scene = label.Contains("chunk") ? VectorShoveScene.Chunk : label.Contains("line") ? VectorShoveScene.Line : VectorShoveScene.Wall;
            float touch = MapComponent_VectorShovePreview.TouchAt(scene, label.Contains("window closed") ? VectorShove.ClosedReact : VectorShove.React);
            float fly = touch + VectorShove.Touch, arrive = touch + VectorShove.Arrive(MapComponent_VectorShovePreview.Stop(scene));
            if (scene == VectorShoveScene.Chunk)
                return new[] { new Phase("Stand", 0f), new Phase("Touch", touch), new Phase("Throw", fly), new Phase("Hit", arrive) };
            return new[]
            {
                new Phase("Stand", 0f), new Phase("Mace hits", VectorShove.Lead), new Phase("Touch", touch), new Phase("Throw", fly),
                new Phase(scene == VectorShoveScene.Wall ? "Slam" : "Lands", arrive),
            };
        }

        // gojo-red-v2.js's phases(): Point, Charge, Fire, Burst, then Slam (a wall) or Lands (in the open); none more for the empty cell.
        private static Phase[] GojoRedPhases(string label)
        {
            GojoRedScene scene = label.Contains("empty") ? GojoRedScene.Empty : label.Contains("open") ? GojoRedScene.Open : GojoRedScene.Wall;
            float arrive = MapComponent_GojoRedPreview.Arrive;
            var phases = new List<Phase>
            {
                new Phase("Point", 0f), new Phase("Charge", GojoRed.Start), new Phase("Fire", GojoRed.Fire(GojoRed.Charge)), new Phase("Burst", arrive),
            };
            if (scene != GojoRedScene.Empty)
                phases.Add(new Phase(MapComponent_GojoRedPreview.HitsWall(scene) ? "Slam" : "Lands", arrive + MapComponent_GojoRedPreview.Fly(scene)));
            return phases.ToArray();
        }

        private static Phase[] BlackFlashPhases(bool plain)
        {
            float hit = BlackFlash.Warmup, burst = hit + BlackFlash.SparkTime;
            if (plain) return new[] { new Phase("Wind-up", 0f), new Phase("Hit", hit) };
            return new[]
            {
                new Phase("Wind-up", 0f), new Phase("Hit: dark + spark", hit), new Phase("Burst", burst),
                new Phase("Bolts thin out", burst + BlackFlash.Life * 0.45f), new Phase("In the zone", burst + BlackFlash.Life),
                new Phase("Stun ends", hit + BlackFlash.StunTime),
            };
        }

        // ajin-reset.js's markers: Delay, Rebuild, Rise, off the preview's script and AjinResetTiming.
        private static Phase[] SatoResetPhases() => new[]
        {
            new Phase("Lying", 0f),
            new Phase("Delay", MapComponent_SatoResetPreview.ResetHold),
            new Phase("Rebuild", MapComponent_SatoResetPreview.ResetHold + MapComponent_SatoResetPreview.ResetDelay - AjinResetTiming.Rebuild),
            new Phase("Rise", MapComponent_SatoResetPreview.ResetHold + MapComponent_SatoResetPreview.ResetDelay),
        };

        private static Phase[] SatoPiecesPhases()
        {
            var phases = new List<Phase> { new Phase("Lying", 0f) };
            string[] names = { "leg", "arm", "hand", "finger", "ear" };
            for (int i = 0; i < names.Length; i++)
                phases.Add(new Phase("Crumble: " + names[i], MapComponent_SatoResetPreview.PiecesCrumble + i * MapComponent_SatoResetPreview.PiecesGap));
            return phases.ToArray();
        }

        // ajin-headshot-reset.js's markers: Draw, Shot, Fall, Play dead, Rebuild, Rise.
        private static Phase[] SatoHeadshotPhases() => new[]
        {
            new Phase("Wounded", 0f),
            new Phase("Draw", MapComponent_SatoResetPreview.HeadshotHold),
            new Phase("Shot", MapComponent_SatoResetPreview.HeadshotShot),
            new Phase("Fall", MapComponent_SatoResetPreview.HeadshotShot + 0.1f),
            new Phase("Play dead", MapComponent_SatoResetPreview.HeadshotLie),
            new Phase("Rebuild", MapComponent_SatoResetPreview.HeadshotStand - HeadshotTiming.Rebuild),
            new Phase("Rise", MapComponent_SatoResetPreview.HeadshotStand),
        };

        private static Phase[] ClapPhases(bool twice)
        {
            float contact = twice ? ClapTeleport.SecondContact : ClapTeleport.FirstContact;
            var phases = new List<Phase> { new Phase("Wind-up", 0f) };
            if (twice) phases.Add(new Phase("First clap", ClapTeleport.FirstContact));
            phases.Add(new Phase("Cards rise", contact - ClapTeleport.Rise));
            phases.Add(new Phase("Contact: swap", contact));
            phases.Add(new Phase("Cards fall", contact + ClapTeleport.Cover));
            return phases.OrderBy(p => p.Seconds).ToArray();
        }

        private static Phase[] JumpPhases(bool inEnemy)
        {
            var phases = new List<Phase>
            {
                new Phase("Stand", 0f),
                new Phase("Script written", ThunderGodJumpTiming.CastAt),
                new Phase("Gone / line", ThunderGodJumpTiming.GoAt),
                new Phase("Arrive", ThunderGodJumpTiming.ArriveAt),
            };
            if (inEnemy) phases.Add(new Phase("Strike", ThunderGodJumpTiming.StrikeAt));
            phases.Add(new Phase("Script burns away", ThunderGodJumpTiming.SettleAt));
            return phases.ToArray();
        }

        private static Phase[] ChainPhases(int targets, bool returns)
        {
            var phases = new List<Phase> { new Phase("Stand", 0f), new Phase("Script written", ThunderGodChainTiming.CastAt) };
            for (int k = 0; k < ThunderGodChainTiming.Hops(targets, returns); k++)
                phases.Add(new Phase(k < targets ? "Jump " + (k + 1) : "Jump back", ThunderGodChainTiming.GoAt(k)));
            phases.Add(new Phase("Script burns away", ThunderGodChainTiming.SettleAt(targets, returns)));
            return phases.ToArray();
        }

        private static Phase[] GuidingPhases() => new[]
        {
            new Phase("Stand", 0f),
            new Phase("Ring written", GuidingThunderTiming.CastAt),
            new Phase("Barrier up", GuidingThunderTiming.UpAt),
            new Phase("Ring burns away", GuidingThunderTiming.OverAt),
            new Phase("Barrier gone", GuidingThunderTiming.GoneAt),
        };

        private static Phase[] RasenganPhases(bool teleports, bool wall)
        {
            var phases = new List<Phase> { new Phase("Stand", 0f), new Phase("Ball forms", RasenganTiming.CastAt) };
            if (teleports) phases.Add(new Phase("Teleport", RasenganTiming.FormedAt));
            phases.Add(new Phase("Thrust", RasenganTiming.ThrustAt(teleports)));
            phases.Add(new Phase("Grind", RasenganTiming.HitAt(teleports)));
            phases.Add(new Phase("Release / thrown", RasenganTiming.ReleaseAt(teleports)));
            phases.Add(new Phase(wall ? "Hits the wall" : "Lands", RasenganTiming.LandAt(teleports, wall)));
            return phases.ToArray();
        }

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
