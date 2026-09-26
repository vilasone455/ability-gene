using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    public static class DebugActions_Echo
    {
        [RimArtDebug("Echo", "spawn resonance device")]
        private static void SpawnDevice()
        {
            Thing device = ThingMaker.MakeThing(EchoDefOf.AG_EchoDevice);
            device.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(device, UI.MouseCell(), Find.CurrentMap);
        }

        [RimArtDebug("Echo", "fill charge", RimArtDebugKind.Now)]
        internal static void Fill() => GameComponent_Echoes.Get.charge = GameComponent_Echoes.Get.MaxCharge;

        [RimArtDebug("Echo", "charge to 1", RimArtDebugKind.Now)]
        private static void AlmostEmpty() => GameComponent_Echoes.Get.charge = 1f;

        [RimArtDebug("Echo", "finish device research", RimArtDebugKind.Now)]
        private static void Research()
        {
            foreach (EchoDeviceTier tier in EchoDevice.Props.tiers)
                if (tier.research != null) Find.ResearchManager.FinishProject(tier.research);
            if (!EchoDefOf.AG_EchoDevice.researchPrerequisites.NullOrEmpty())
                foreach (ResearchProjectDef project in EchoDefOf.AG_EchoDevice.researchPrerequisites)
                    Find.ResearchManager.FinishProject(project);
        }

        [RimArtDebug("Echo", "make Host (no trials)", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn) => Find.WindowStack.Add(new FloatMenu(EchoUtility.AllEchoes
            .Select(def => new FloatMenuOption(def.LabelCap, () => EchoUtility.ForceHost(def, pawn))).ToList()));

        [RimArtDebug("Echo", "tune to pawn", RimArtDebugKind.Pawn)]
        private static void Tune(Pawn pawn) => Find.WindowStack.Add(new FloatMenu(EchoUtility.AllEchoes
            .Select(def => new FloatMenuOption(def.LabelCap, () => EchoUtility.Tune(def, pawn))).ToList()));

        /// <summary>
        /// Meets every Trial the candidate can be given: skills, records, weapon kills, and removes an
        /// excluded trait. Stat and colony wealth Trials cannot be set and are reported instead. The
        /// Trials are checked at once, so the awakening letter comes without waiting.
        /// </summary>
        [RimArtDebug("Echo", "meet candidate's trials", RimArtDebugKind.Pawn)]
        internal static void MeetTrials(Pawn pawn)
        {
            EchoRecord record = GameComponent_Echoes.Get.CandidateRecord(pawn);
            if (record == null)
            {
                Messages.Message(pawn.LabelShortCap + " is not a candidate. Use \"tune to pawn\" first.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            var skipped = new List<string>();
            foreach (EchoTrial trial in record.def.trials)
            {
                if (trial.Met(pawn)) continue;
                switch (trial)
                {
                    case Trial_Skill skill:
                        SkillRecord s = pawn.skills.GetSkill(skill.skill);
                        if (s.TotallyDisabled) skipped.Add(trial.Label + " (incapable)");
                        else s.Level = skill.level;
                        break;
                    case Trial_Record rec:
                        pawn.records.AddTo(rec.record, rec.count - rec.Current(pawn));
                        break;
                    case Trial_KillsWith kills:
                        ThingDef weapon = kills.weapons.FirstOrDefault()
                            ?? DefDatabase<ThingDef>.AllDefs.FirstOrDefault(d => kills.Counts(d));
                        if (weapon == null) { skipped.Add(trial.Label); break; }
                        PawnDeeds deeds = GameComponent_Echoes.Get.DeedsFor(pawn, true);
                        deeds.killsByWeapon.TryGetValue(weapon, out int have);
                        deeds.killsByWeapon[weapon] = have + kills.count - (int)kills.Current(pawn);
                        break;
                    case Trial_NotTrait not:
                        foreach (Trait trait in pawn.story.traits.allTraits.Where(t => t.def == not.trait).ToList())
                        {
                            if (trait.sourceGene != null) skipped.Add(trial.Label + " (trait from a gene)");
                            else pawn.story.traits.RemoveTrait(trait);
                        }
                        break;
                    default:
                        skipped.Add(trial.Label);
                        break;
                }
            }
            if (skipped.Count > 0)
                Messages.Message("Could not meet: " + skipped.ToCommaList(), MessageTypeDefOf.RejectInput, false);
            EchoUtility.CheckTrials(record);
        }
    }
}
