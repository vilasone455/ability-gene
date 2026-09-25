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
                Name = "Water Gun", Prefix = "Water Gun:", Component = typeof(MapComponent_WaterGunPreview), Clock = "seconds",
                Phases = label => label.Contains("pump") ? PumpPhases() : StreamPhases(),
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
            new Kit
            {
                Name = "Trace", Prefix = "Trace:", Component = typeof(MapComponent_UbwPreview), Clock = "seconds",
                Phases = label => label.Contains("cast") ? UbwCastPhases() : UbwWorldPhases(),
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
