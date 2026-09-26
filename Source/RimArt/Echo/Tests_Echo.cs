using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for the Echo system (run with -quicktest -rimarttest=echo).</summary>
    public static class Tests_Echo
    {
        private static EchoDef Accelerator => DefDatabase<EchoDef>.GetNamed("AG_Echo_Accelerator");
        private static EchoDef Vergil => DefDatabase<EchoDef>.GetNamed("AG_Echo_Vergil");
        private static AbilityDef Shove => DefDatabase<AbilityDef>.GetNamed("AG_VectorShove");

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = null;
            return echoes;
        }

        private static Pawn Colonist(RimArtTestContext t, int dx = 0)
        {
            Pawn pawn = t.Colonist(t.center + new IntVec3(dx, 0, 0));
            pawn.drafter.Drafted = false;
            RimArtTestContext.Hold(pawn);
            return pawn;
        }

        [RimArtTest("Echo", "pool 1 a working device refills and a manifested Host drains")]
        private static IEnumerable<int> PoolRefillDrain(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            EchoDevice.workingForTests = true;
            echoes.charge = 10f;
            yield return GameComponent_Echoes.PoolInterval * 5;
            float refill = echoes.charge - 10f;
            t.Log("charge after 5 intervals with the device working: " + echoes.charge.ToString("0.###"));
            t.Check(refill > 0f, "the pool refilled");

            EchoDevice.workingForTests = false;
            Pawn host = Colonist(t);
            EchoRecord record = EchoUtility.ForceHost(Accelerator, host);
            echoes.charge = 50f;
            t.Check(EchoUtility.Manifest(record), "the Host manifested");
            yield return GameComponent_Echoes.PoolInterval * 5;
            float expected = 50f - Accelerator.upkeepPerHour * GameComponent_Echoes.PoolInterval * 5 / 2500f;
            t.Log("charge after 5 intervals manifested: " + echoes.charge.ToString("0.###") + ", expected about " + expected.ToString("0.###"));
            t.Check(System.Math.Abs(echoes.charge - expected) < 0.2f, "upkeep drained the pool at the Echo's rate");
            EchoDevice.workingForTests = null;
            EchoUtility.Revert(record, collapse: false);
        }

        [RimArtTest("Echo", "pool 2 an empty pool reverts every Host and collapses them")]
        private static IEnumerable<int> PoolEmpty(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            EchoDevice.workingForTests = false;
            Pawn a = Colonist(t, -2), b = Colonist(t, 2);
            EchoRecord ra = EchoUtility.ForceHost(Accelerator, a);
            EchoRecord rb = EchoUtility.ForceHost(Vergil, b);
            echoes.charge = 0.05f;
            t.Check(EchoUtility.Manifest(ra) && EchoUtility.Manifest(rb), "both Hosts manifested");
            bool emptied = false;
            System.Action onEmpty = () => emptied = true;
            EchoUtility.PoolEmptied += onEmpty;
            yield return GameComponent_Echoes.PoolInterval * 2 + 1;
            EchoUtility.PoolEmptied -= onEmpty;
            t.Check(!ra.manifested && !rb.manifested, "both reverted");
            t.Check(a.health.hediffSet.HasHediff(EchoDefOf.AG_EchoCollapse) && b.health.hediffSet.HasHediff(EchoDefOf.AG_EchoCollapse), "both collapsed");
            t.Check(a.Downed && b.Downed, "both are down");
            t.Check(a.abilities.GetAbility(Shove) == null, "the Echo's abilities are gone");
            t.Check(emptied, "PoolEmptied fired");
            t.Check(!EchoUtility.CanManifest(ra, out string reason), "a collapsed Host cannot manifest (" + reason + ")");
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Echo", "manifest 1 hero form grants abilities, blocks work, sets the body and hair; revert restores")]
        private static IEnumerable<int> ManifestRevert(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            Pawn host = Colonist(t);
            BodyTypeDef body = host.story.bodyType;
            UnityEngine.Color hair = host.story.HairColor;
            bool haulingBefore = host.WorkTypeIsDisabled(WorkTypeDefOf.Hauling);
            t.Log("hauling disabled before manifest: " + haulingBefore);
            EchoRecord record = EchoUtility.ForceHost(Accelerator, host);
            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "manifested");
            yield return 2;
            t.Check(host.abilities.GetAbility(Shove) != null, "has Vector Shove");
            t.Check(host.WorkTypeIsDisabled(WorkTypeDefOf.Hauling), "hauling is disabled");
            t.Check(!host.WorkTagIsDisabled(WorkTags.Violent), "violence is not disabled");
            t.Check(host.story.bodyType == Accelerator.bodyType, "body is " + Accelerator.bodyType.defName + " (was " + body.defName + ")");
            t.Check(host.story.HairColor == Accelerator.hairColor, "hair has the Echo's colour");
            t.Check(host.health.hediffSet.HasHediff(Accelerator.manifestHediff), "has the hero form hediff");

            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(host.abilities.GetAbility(Shove) == null, "Vector Shove is gone");
            t.Check(host.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) == haulingBefore, "hauling is as before");
            t.Check(host.story.bodyType == body, "body restored");
            t.Check(host.story.HairColor == hair, "hair restored");
            t.Check(!host.health.hediffSet.HasHediff(Accelerator.manifestHediff), "hero form hediff removed");
        }

        [RimArtTest("Echo", "manifest 2 removing the hero form hediff from outside reverts the Echo")]
        private static IEnumerable<int> HediffRemoved(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            Pawn host = Colonist(t);
            EchoRecord record = EchoUtility.ForceHost(Accelerator, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            yield return 2;
            host.health.RemoveHediff(host.health.hediffSet.GetFirstHediffOfDef(Accelerator.manifestHediff));
            yield return 2;
            t.Check(!record.manifested, "the record reverted");
            t.Check(host.abilities.GetAbility(Shove) == null, "the abilities went with it");
        }

        [RimArtTest("Echo", "cast 1 a cast cost disables the ability when short and is paid when it fires")]
        private static IEnumerable<int> CastCost(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            EchoDevice.workingForTests = false;
            Pawn host = Colonist(t);
            EchoRecord record = EchoUtility.ForceHost(Accelerator, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            host.drafter.Drafted = true;
            yield return 2;
            Ability shove = host.abilities.GetAbility(Shove);
            float cost = Accelerator.CastCost(Shove);
            echoes.charge = cost - 1f;
            t.Check(shove.GizmoDisabled(out string reason), "disabled with " + echoes.charge + " charge (" + reason + ")");
            bool result = true;
            t.Check(!EchoCastPayment.Pay(shove, ref result) && !result, "the cast is refused when the pool is short");
            echoes.charge = 50f;
            t.Check(!shove.GizmoDisabled(out _), "enabled with 50 charge");
            result = true;
            t.Check(EchoCastPayment.Pay(shove, ref result), "the cast is allowed");
            t.Check(System.Math.Abs(echoes.charge - (50f - cost)) < 0.01f, "the pool paid " + cost + " (now " + echoes.charge + ")");
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Echo", "awaken 1 met trials send the letter; awakening gives the trait and replaces a conflicting one")]
        private static IEnumerable<int> Awaken(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            Pawn pawn = Colonist(t);
            TraitDef kind = DefDatabase<TraitDef>.GetNamed("Kind"), abrasive = DefDatabase<TraitDef>.GetNamed("Abrasive");
            foreach (Trait trait in pawn.story.traits.allTraits.ToList())
                if (trait.sourceGene == null && (trait.def.ConflictsWith(kind) || trait.def == abrasive)) pawn.story.traits.RemoveTrait(trait);
            if (!pawn.story.traits.HasTrait(kind)) pawn.story.traits.GainTrait(new Trait(kind));
            pawn.skills.GetSkill(SkillDefOf.Intellectual).Level = 0;
            EchoUtility.Tune(Accelerator, pawn);
            EchoRecord record = echoes.RecordFor(Accelerator);
            t.Check(record.state == EchoState.Tracking && record.candidate == pawn, "tracking the candidate");
            t.Log(EchoGizmos.TrialsText(record));
            t.Check(!EchoUtility.CanAwaken(record, out _), "cannot awaken yet");

            if (!t.Check(!pawn.skills.GetSkill(SkillDefOf.Intellectual).TotallyDisabled, "the pawn can do intellectual work")) yield break;
            pawn.skills.GetSkill(SkillDefOf.Intellectual).Level = 12;
            pawn.records.AddTo(RecordDefOf.DamageTaken, 300f);
            yield return 251;
            t.Log(EchoGizmos.TrialsText(record));
            t.Check(record.offered, "the awakening letter was offered");
            t.Check(Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_EchoAwakening e && e.pawn == pawn), "the letter is in the stack");
            t.Check(EchoUtility.Awaken(record), "awakened");
            t.Check(record.state == EchoState.Awakened && record.host == pawn, "the pawn is the Host");
            t.Check(pawn.story.traits.HasTrait(abrasive), "gained Abrasive");
            t.Check(!pawn.story.traits.HasTrait(kind), "Kind was replaced");
            t.Check(pawn.MarketValue >= Accelerator.wealth, "market value includes the Echo (" + pawn.MarketValue.ToString("0") + ")");
        }

        [RimArtTest("Echo", "cap 1 awakening is refused when the hero cap is reached")]
        private static IEnumerable<int> Cap(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            int cap = echoes.HeroCap;
            List<EchoDef> defs = EchoUtility.AllEchoes.ToList();
            t.Log("hero cap " + cap + ", echoes " + defs.Count);
            if (!t.Check(defs.Count > cap, "more Echoes than the cap")) yield break;
            for (int i = 0; i < cap; i++) EchoUtility.ForceHost(defs[i], Colonist(t, i * 2 - 4));
            t.Check(echoes.HostCount == cap, "Hosts at the cap");
            // A random colonist can be incapable of a skill a trial names; try a few.
            Pawn extra = null;
            for (int tries = 0; tries < 10 && extra == null; tries++)
            {
                Pawn pawn = Colonist(t, 6);
                if (defs[cap].trials.OfType<Trial_Skill>().All(s => !pawn.skills.GetSkill(s.skill).TotallyDisabled)
                    && defs[cap].trials.OfType<Trial_NotTrait>().All(n => n.Met(pawn))) extra = pawn;
                else pawn.Destroy();
            }
            if (!t.Check(extra != null, "a colonist who can meet " + defs[cap].label + "'s trials")) yield break;
            EchoUtility.Tune(defs[cap], extra);
            EchoRecord record = echoes.RecordFor(defs[cap]);
            foreach (EchoTrial trial in defs[cap].trials)
            {
                if (trial is Trial_Skill skill) extra.skills.GetSkill(skill.skill).Level = skill.level;
                else if (trial is Trial_Record rec) extra.records.AddTo(rec.record, rec.count);
            }
            yield return 2;
            bool met = EchoUtility.TrialsMet(record);
            t.Log("trials met: " + met + "\n" + EchoGizmos.TrialsText(record));
            if (!t.Check(met, "the trials are met")) yield break;
            t.Check(!EchoUtility.CanAwaken(record, out string reason), "refused: " + reason);
            t.Check(!EchoUtility.Awaken(record), "Awaken returns false");
        }

        [RimArtTest("Echo", "deeds 1 a longsword kill counts for Vergil's trial")]
        private static IEnumerable<int> LongswordKill(RimArtTestContext t)
        {
            Setup(t);
            Pawn killer = Colonist(t);
            ThingDef longsword = DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
            killer.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(longsword, GenStuff.DefaultStuffFor(longsword)));
            Pawn enemy = t.Enemy(t.center + new IntVec3(2, 0, 0), armed: false);
            var trial = Vergil.trials.OfType<Trial_KillsWith>().First();
            float before = trial.Current(killer);
            enemy.Kill(new DamageInfo(DamageDefOf.Cut, 999f, instigator: killer, weapon: longsword));
            yield return 2;
            t.Check(trial.Current(killer) == before + 1f, "the kill counted (" + before + " -> " + trial.Current(killer) + ")");
            Pawn other = t.Enemy(t.center + new IntVec3(-2, 0, 0), armed: false);
            other.Kill(new DamageInfo(DamageDefOf.Blunt, 999f, instigator: killer, weapon: DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Club")));
            yield return 2;
            t.Check(trial.Current(killer) == before + 1f, "a club kill does not count");
        }

        /// <summary>
        /// Pictures of the UI, for reading, not a check: the device tab with one Host manifested, one
        /// candidate being tracked and the rest untuned, then the Host's charge gizmo and toggle.
        /// </summary>
        [RimArtTest("Echo", "ui 1 device tab and Host gizmos")]
        private static IEnumerable<int> Ui(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            EchoDevice.workingForTests = true;
            Thing device = ThingMaker.MakeThing(EchoDefOf.AG_EchoDevice);
            device.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(device, t.center + new IntVec3(0, 0, 3), t.map);
            Pawn host = Colonist(t, -3), candidate = Colonist(t, 3);
            EchoRecord record = EchoUtility.ForceHost(Accelerator, host);
            echoes.charge = 64f;
            EchoUtility.Manifest(record);
            host.drafter.Drafted = true;
            EchoUtility.Tune(Vergil, candidate);
            candidate.skills.GetSkill(SkillDefOf.Melee).Level = 16;
            yield return 61;
            Find.Selector.ClearSelection();
            Find.Selector.Select(device);
            yield return 2;
            InspectPaneUtility.OpenTab(typeof(ITab_EchoDevice));
            yield return 2;
            yield return t.ShotAs("echo-device-tab");
            Find.Selector.ClearSelection();
            Find.Selector.Select(host);
            yield return 2;
            yield return t.ShotAs("echo-host-gizmos");
            Find.Selector.ClearSelection();
            Find.Selector.Select(candidate);
            yield return 2;
            yield return t.ShotAs("echo-candidate-gizmo");
            Find.Selector.ClearSelection();
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
            t.Check(true, "shots taken");
        }

        [RimArtTest("Echo", "dev 1 the god-mode Meet trials command completes Vergil's trials and sends the letter")]
        private static IEnumerable<int> DevMeetTrials(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            Pawn pawn = null;
            for (int tries = 0; tries < 10 && pawn == null; tries++)
            {
                Pawn p = Colonist(t);
                if (!p.skills.GetSkill(SkillDefOf.Melee).TotallyDisabled
                    && p.story.traits.allTraits.All(tr => tr.sourceGene == null)) pawn = p;
                else p.Destroy();
            }
            if (!t.Check(pawn != null, "a colonist capable of melee")) yield break;
            TraitDef wimp = DefDatabase<TraitDef>.GetNamed("Wimp");
            if (!pawn.story.traits.HasTrait(wimp) && !pawn.story.traits.allTraits.Any(tr => tr.def.ConflictsWith(wimp)))
                pawn.story.traits.GainTrait(new Trait(wimp));
            EchoUtility.Tune(Vergil, pawn);
            EchoRecord record = echoes.RecordFor(Vergil);

            bool godMode = DebugSettings.godMode;
            DebugSettings.godMode = true;
            Command meet = pawn.GetGizmos().OfType<Command_Action>().FirstOrDefault(c => c.defaultLabel == "DEV: Meet trials");
            DebugSettings.godMode = false;
            bool hiddenOutsideGodMode = !pawn.GetGizmos().OfType<Command_Action>().Any(c => c.defaultLabel == "DEV: Meet trials");
            DebugSettings.godMode = godMode;
            t.Check(meet != null, "the command is shown in god mode");
            t.Check(hiddenOutsideGodMode, "the command is hidden outside god mode");
            if (meet == null) yield break;

            ((Command_Action)meet).action();
            yield return 2;
            t.Log(EchoGizmos.TrialsText(record));
            t.Check(EchoUtility.TrialsMet(record), "every trial is met");
            t.Check(!pawn.story.traits.HasTrait(wimp), "Wimp was removed");
            t.Check(record.offered, "the awakening letter was offered");
        }
    }
}
