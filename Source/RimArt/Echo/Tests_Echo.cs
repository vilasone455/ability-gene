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
        private static EchoDef Goku => DefDatabase<EchoDef>.GetNamed("AG_Echo_Goku");
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

        // ---- manifest weapon ----

        private static ThingDef Def(string name) => DefDatabase<ThingDef>.GetNamed(name);
        private static ThingDef Knife => Def("MeleeWeapon_Knife");
        private static ThingDef Longsword => Def("MeleeWeapon_LongSword");
        private static int OnMap(RimArtTestContext t, ThingDef def) => t.map.listerThings.ThingsOfDef(def).Count;
        /// <summary>Knives on the map when the forced-weapon Host was made.</summary>
        private static int knivesBefore;

        /// <summary>
        /// A Host holding a longsword, manifested as Vergil with a knife as his forced weapon: the tests
        /// use a plain vanilla weapon in place of Yamato. The caller puts <paramref name="before"/> back.
        /// </summary>
        private static EchoRecord ForcedWeaponHost(RimArtTestContext t, GameComponent_Echoes echoes, out Pawn host, out ThingDef before)
        {
            // A quicktest map is random and may already have a knife lying somewhere: the checks count from here.
            knivesBefore = OnMap(t, Knife);
            t.Log("knives on the map before: " + knivesBefore);
            before = Vergil.manifestWeapon;
            Vergil.manifestWeapon = Knife;
            host = Colonist(t);
            t.Equip(host, Longsword);
            EchoRecord record = EchoUtility.ForceHost(Vergil, host);
            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "manifested");
            return record;
        }

        [RimArtTest("Echo", "weapon 1 empty hands: the held weapon waits in the inventory, equip is refused, revert hands it back")]
        private static IEnumerable<int> WeaponEmptyHands(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            Pawn host = Colonist(t);
            t.Equip(host, Longsword);
            ThingWithComps sword = host.equipment.Primary;
            EchoRecord record = EchoUtility.ForceHost(Goku, host);
            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "manifested");
            yield return 2;
            t.Check(host.equipment.Primary == null, "hands are empty");
            t.Check(host.inventory.innerContainer.Contains(sword), "the longsword is in the inventory");
            ThingDef clubDef = Def("MeleeWeapon_Club");
            Thing club = GenSpawn.Spawn(ThingMaker.MakeThing(clubDef, GenStuff.DefaultStuffFor(clubDef)), t.center + new IntVec3(1, 0, 0), t.map);
            t.Check(!EquipmentUtility.CanEquip(club, host, out string reason), "equipping a club is refused (" + reason + ")");

            var knife = (ThingWithComps)ThingMaker.MakeThing(Knife, GenStuff.DefaultStuffFor(Knife));
            host.equipment.AddEquipment(knife);
            yield return GameComponent_Echoes.PoolInterval + 1;
            t.Check(host.equipment.Primary == null && host.inventory.innerContainer.Contains(knife),
                "a knife put in the hand by code went to the inventory within one pool interval");

            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(host.equipment.Primary == sword, "the longsword is back in hand");
            t.Check(!host.inventory.innerContainer.Contains(sword), "and out of the inventory");
            t.Check(EquipmentUtility.CanEquip(club, host), "equipping is allowed again");
        }

        [RimArtTest("Echo", "weapon 2 forced weapon: manifest puts it in hand, equip is refused, revert destroys it and hands back the old one")]
        private static IEnumerable<int> WeaponForced(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            EchoRecord record = ForcedWeaponHost(t, echoes, out Pawn host, out ThingDef before);
            ThingWithComps sword = host.inventory.innerContainer.OfType<ThingWithComps>().FirstOrDefault(w => w.def == Longsword);
            yield return 2;
            ThingWithComps hero = host.equipment.Primary;
            t.Check(hero != null && hero.def == Knife && hero == record.heroWeapon, "the hero weapon is in hand (" + hero?.LabelCap + ")");
            t.Check(sword != null, "the longsword is in the inventory");
            t.Check(!EquipmentUtility.CanEquip(sword, host, out string reason), "equipping the longsword is refused (" + reason + ")");
            t.Check(EquipmentUtility.CanEquip(hero, host), "the hero weapon itself passes");
            yield return GameComponent_Echoes.PoolInterval + 1;
            t.Check(host.equipment.Primary == hero, "the pool tick leaves the hero weapon in hand");

            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(hero.Destroyed, "the hero weapon is destroyed on revert");
            t.Check(host.equipment.Primary == sword, "the longsword is back in hand");
            t.Check(OnMap(t, Knife) == knivesBefore, "no knife lies on the map");
            Vergil.manifestWeapon = before;
        }

        [RimArtTest("Echo", "weapon 3 a dropped hero weapon vanishes and returns after the return time, not while downed")]
        private static IEnumerable<int> WeaponReturns(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            EchoRecord record = ForcedWeaponHost(t, echoes, out Pawn host, out ThingDef before);
            ThingWithComps sword = host.inventory.innerContainer.OfType<ThingWithComps>().FirstOrDefault(w => w.def == Longsword);
            yield return 2;
            ThingWithComps hero = host.equipment.Primary;
            bool dropped = host.equipment.TryDropEquipment(hero, out ThingWithComps landed, host.Position, false);
            t.Check(dropped && landed == null && hero.Destroyed, "a drop destroys the hero weapon instead of landing it");
            t.Check(OnMap(t, Knife) == knivesBefore, "no knife lies on the map");
            t.Check(record.weaponGone, "the record waits to give it back");
            t.Log("return time " + Vergil.weaponReturnTicks + " ticks; the test moves the mark to 2 pool intervals");
            record.weaponBackTick = Find.TickManager.TicksGame + GameComponent_Echoes.PoolInterval * 2;
            yield return GameComponent_Echoes.PoolInterval;
            t.Check(host.equipment.Primary == null, "still empty before the mark");
            yield return GameComponent_Echoes.PoolInterval * 2;
            ThingWithComps back = host.equipment.Primary;
            t.Check(back != null && back.def == Knife && back != hero, "a new hero weapon is in hand after the mark");

            host.health.AddHediff(HediffDefOf.Anesthetic);
            yield return 2;
            if (!t.Check(host.Downed, "anesthetic downed the Host")) { Vergil.manifestWeapon = before; yield break; }
            t.Check(back != null && back.Destroyed && host.equipment.Primary == null, "downing destroyed the hero weapon");
            t.Check(OnMap(t, Knife) == knivesBefore, "no knife lies on the map");
            t.Log("longsword after downing: " + (sword == null ? "missing" : sword.Spawned ? "on the ground (vanilla drops a downed pawn's inventory)"
                : host.inventory.innerContainer.Contains(sword) ? "in the inventory" : "elsewhere"));
            record.weaponBackTick = Find.TickManager.TicksGame;
            yield return GameComponent_Echoes.PoolInterval * 2;
            t.Check(host.equipment.Primary == null, "no hero weapon while downed");
            host.health.RemoveHediff(host.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic));
            yield return 2;
            t.Check(!host.Downed, "the Host is up");
            yield return GameComponent_Echoes.PoolInterval + 1;
            t.Check(host.equipment.Primary?.def == Knife, "the hero weapon is back once the Host is up");
            EchoUtility.Revert(record, collapse: false);
            Vergil.manifestWeapon = before;
        }

        [RimArtTest("Echo", "weapon 4 a Host who dies leaves no hero weapon behind")]
        private static IEnumerable<int> WeaponDeath(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            // A second colonist keeps the colony alive, so the death does not end the game.
            Colonist(t, 4);
            EchoRecord record = ForcedWeaponHost(t, echoes, out Pawn host, out ThingDef before);
            yield return 2;
            ThingWithComps hero = host.equipment.Primary;
            host.Kill(null);
            yield return 2;
            t.Check(OnMap(t, Knife) == knivesBefore, "no knife lies on the map after the death");
            yield return 251;
            t.Check(record.state == EchoState.Closed, "the Echo closed");
            t.Check(hero == null || hero.Destroyed, "the hero weapon is destroyed");
            t.Check(OnMap(t, Knife) == knivesBefore, "still no knife on the map");
            Vergil.manifestWeapon = before;
        }

        /// <summary>Every node of the pawn's render tree. Builds the tree first.</summary>
        private static IEnumerable<PawnRenderNode> RenderNodes(Pawn pawn)
        {
            pawn.Drawer.renderer.EnsureGraphicsInitialized();
            var queue = new Queue<PawnRenderNode>();
            PawnRenderNode root = pawn.Drawer.renderer.renderTree.rootNode;
            if (root != null) queue.Enqueue(root);
            while (queue.Count > 0)
            {
                PawnRenderNode node = queue.Dequeue();
                yield return node;
                if (node.children != null)
                    foreach (PawnRenderNode child in node.children) queue.Enqueue(child);
            }
        }

        private static PawnRenderNode CostumeNode(Pawn pawn, HediffDef form) =>
            RenderNodes(pawn).FirstOrDefault(node => node.hediff?.def == form);

        /// <summary>Whether each worn piece's render node would draw for a standing pawn facing south.</summary>
        private static Dictionary<string, bool> ApparelDrawn(Pawn pawn)
        {
            PawnDrawParms parms = PawnDrawParms.DefaultFor(pawn);
            parms.facing = Rot4.South;
            var drawn = new Dictionary<string, bool>();
            foreach (PawnRenderNode node in RenderNodes(pawn))
                if (node.apparel != null) drawn[node.apparel.def.defName] = node.Worker.CanDrawNow(node, parms);
            return drawn;
        }

        private static void CheckDrawn(RimArtTestContext t, Pawn pawn, string when, params (string piece, bool drawn)[] expected)
        {
            Dictionary<string, bool> drawn = ApparelDrawn(pawn);
            foreach ((string piece, bool want) in expected)
                t.Check(drawn.TryGetValue(piece, out bool got) && got == want,
                    when + ": " + piece + (want ? " is drawn" : " is hidden")
                    + (drawn.ContainsKey(piece) ? "" : " (no render node)"));
        }

        private static void Wear(Pawn pawn, string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null));
        }

        /// <summary>Turns the pawn and keeps it turned: undrafted, a wait job facing a cell 3 away.</summary>
        private static void Face(Pawn pawn, Rot4 rot)
        {
            // Pawn_RotationTracker turns a drafted pawn that stands idle to face south.
            pawn.drafter.Drafted = false;
            Verse.AI.Job wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, pawn.Position + rot.FacingCell * 3);
            wait.expiryInterval = 600;
            pawn.jobs.StartJob(wait, Verse.AI.JobCondition.InterruptForced);
            pawn.Rotation = rot;
        }

        [RimArtTest("Echo", "costume 1 Vergil's coat is drawn in hero form, hides worn clothes, armour and headgear but not belts, and goes on revert (screenshots)")]
        private static IEnumerable<int> VergilCoat(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            Pawn host = Colonist(t);
            foreach (string piece in new[] { "Apparel_BasicShirt", "Apparel_Pants", "Apparel_SmokepopBelt" }) Wear(host, piece);
            t.Equip(host, DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"));
            HediffDef form = Vergil.manifestHediff;
            var props = form.RenderNodeProperties?.OfType<PawnRenderNodeProperties_EchoCostume>().FirstOrDefault();
            if (!t.Check(props?.bodyTypeGraphicPaths != null, "the hero form hediff has a costume node with body types")) yield break;
            t.Check(props.hideBodyApparel && props.hideHeadgear, "Vergil's coat hides body apparel and headgear");
            foreach (BodyTypeGraphicData body in props.bodyTypeGraphicPaths)
                foreach (string facing in new[] { "south", "east", "north" })
                    t.Check(ContentFinder<UnityEngine.Texture2D>.Get(body.texturePath + "_" + facing, false) != null,
                        body.bodyType.defName + " " + facing + " texture loads");
            t.Check(CostumeNode(host, form) == null, "no coat before manifest");
            // Vanilla trousers have no worn picture and so no render node: only the shirt and belt are checked.
            CheckDrawn(t, host, "before", ("Apparel_BasicShirt", true), ("Apparel_SmokepopBelt", true));
            Face(host, Rot4.South);
            yield return 20;
            yield return t.ShotAs("coat-before-south");

            EchoRecord record = EchoUtility.ForceHost(Vergil, host);
            echoes.charge = 100f;
            int worn = host.apparel.WornApparelCount;
            t.Check(EchoUtility.Manifest(record), "manifested");
            yield return 2;
            PawnRenderNode coat = CostumeNode(host, form);
            t.Check(coat != null, "the coat is in the render tree");
            t.Check(coat?.parent?.Props.tagDef == PawnRenderNodeTagDefOf.ApparelBody,
                "under the body apparel node (" + coat?.parent?.Props.tagDef?.defName + ")");
            t.Check(coat?.PrimaryGraphic?.path == "RimArt/Echo/Costume/VergilCoat_" + host.story.bodyType.defName,
                "the texture is the " + host.story.bodyType.defName + " coat (" + coat?.PrimaryGraphic?.path + ")");
            t.Check(host.apparel.WornApparelCount == worn, "the Host still wears its " + worn + " pieces");
            CheckDrawn(t, host, "hero form", ("Apparel_BasicShirt", false), ("Apparel_SmokepopBelt", true));
            foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.North, Rot4.West })
            {
                Face(host, rot);
                yield return 20;
                yield return t.ShotAs("coat-" + rot.ToStringHuman().ToLowerInvariant());
            }

            // Marine armour and helmet: still worn and still counted, not drawn; the hair shows.
            EchoUtility.Revert(record, collapse: false);
            foreach (string piece in new[] { "Apparel_PowerArmor", "Apparel_PowerArmorHelmet" }) Wear(host, piece);
            yield return 2;
            CheckDrawn(t, host, "armour before manifest", ("Apparel_PowerArmor", true), ("Apparel_PowerArmorHelmet", true));
            t.Check(EchoUtility.Manifest(record), "manifested again in marine armour");
            yield return 2;
            t.Check(host.apparel.WornApparel.Any(a => a.def.defName == "Apparel_PowerArmor"), "the marine armour is still worn under the coat");
            CheckDrawn(t, host, "armour in hero form", ("Apparel_PowerArmor", false), ("Apparel_PowerArmorHelmet", false),
                ("Apparel_SmokepopBelt", true));
            t.Check(!PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(PawnDrawParms.DefaultFor(host)),
                "headgear counts as not visible, so the helmet no longer hides the hair");
            foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.North })
            {
                Face(host, rot);
                yield return 20;
                yield return t.ShotAs("coat-armour-" + rot.ToStringHuman().ToLowerInvariant());
            }

            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(CostumeNode(host, form) == null, "the coat is gone after revert");
            CheckDrawn(t, host, "after revert", ("Apparel_PowerArmor", true), ("Apparel_PowerArmorHelmet", true),
                ("Apparel_SmokepopBelt", true));
            t.Check(PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(PawnDrawParms.DefaultFor(host)), "headgear visible again");
        }

        private static IEnumerable<PawnRenderNode> CostumeNodes(Pawn pawn, HediffDef form) =>
            RenderNodes(pawn).Where(node => node.hediff?.def == form);

        [RimArtTest("Echo", "costume 2 the Akatsuki cloak is shared: Pain's and Itachi's hero forms draw the cloak on the body and the collar on the head, hide worn clothes and hats, and go on revert (screenshots)")]
        private static IEnumerable<int> AkatsukiCloak(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            EchoDef pain = DefDatabase<EchoDef>.GetNamed("AG_Echo_Pain");
            EchoDef itachi = DefDatabase<EchoDef>.GetNamed("AG_Echo_Itachi");
            foreach (EchoDef echo in new[] { pain, itachi })
            {
                var paths = echo.manifestHediff.RenderNodeProperties?.OfType<PawnRenderNodeProperties_EchoCostume>().Select(p => p.texPath).ToList();
                t.Check(paths != null && paths.Contains("RimArt/Echo/Costume/AkatsukiCloak_Thin") && paths.Contains("RimArt/Echo/Costume/AkatsukiCollar"),
                    echo.defName + "'s hero form has the cloak and the collar from the shared parent (" + (paths == null ? "none" : string.Join(", ", paths)) + ")");
            }
            var props = pain.manifestHediff.RenderNodeProperties.OfType<PawnRenderNodeProperties_EchoCostume>().ToList();
            var cloakProps = props.FirstOrDefault(p => p.parentTagDef == PawnRenderNodeTagDefOf.ApparelBody);
            var collarProps = props.FirstOrDefault(p => p.parentTagDef == PawnRenderNodeTagDefOf.Head);
            if (!t.Check(cloakProps?.bodyTypeGraphicPaths != null && collarProps != null, "one node on the body apparel, one on the head")) yield break;
            t.Check(cloakProps.hideBodyApparel && cloakProps.hideHeadgear, "the cloak hides body apparel and headgear");
            foreach (string facing in new[] { "south", "east", "north" })
            {
                foreach (BodyTypeGraphicData body in cloakProps.bodyTypeGraphicPaths)
                    t.Check(ContentFinder<UnityEngine.Texture2D>.Get(body.texturePath + "_" + facing, false) != null,
                        "cloak " + body.bodyType.defName + " " + facing + " texture loads");
                t.Check(ContentFinder<UnityEngine.Texture2D>.Get(collarProps.texPath + "_" + facing, false) != null,
                    "collar " + facing + " texture loads");
            }

            Pawn a = Colonist(t, -2), b = Colonist(t, 2);
            foreach (Pawn pawn in new[] { a, b })
                foreach (string piece in new[] { "Apparel_BasicShirt", "Apparel_Pants", "Apparel_SmokepopBelt", "Apparel_CowboyHat" })
                    Wear(pawn, piece);
            EchoRecord ra = EchoUtility.ForceHost(pain, a), rb = EchoUtility.ForceHost(itachi, b);
            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(ra) && EchoUtility.Manifest(rb), "Pain and Itachi manifested");
            yield return 2;
            foreach ((Pawn pawn, EchoDef echo) in new[] { (a, pain), (b, itachi) })
            {
                List<PawnRenderNode> nodes = CostumeNodes(pawn, echo.manifestHediff).ToList();
                PawnRenderNode cloak = nodes.FirstOrDefault(n => n.parent?.Props.tagDef == PawnRenderNodeTagDefOf.ApparelBody);
                PawnRenderNode collar = nodes.FirstOrDefault(n => n.parent?.Props.tagDef == PawnRenderNodeTagDefOf.Head);
                t.Check(cloak?.PrimaryGraphic?.path == "RimArt/Echo/Costume/AkatsukiCloak_" + pawn.story.bodyType.defName,
                    echo.label + ": the cloak is the " + pawn.story.bodyType.defName + " one (" + cloak?.PrimaryGraphic?.path + ")");
                t.Check(collar?.PrimaryGraphic?.path == "RimArt/Echo/Costume/AkatsukiCollar",
                    echo.label + ": the collar is on the head (" + collar?.PrimaryGraphic?.path + ")");
                PawnRenderNode hair = RenderNodes(pawn).FirstOrDefault(n => n.Props.debugLabel == "Hair");
                t.Check(collar != null && hair != null && collar.Props.baseLayer > hair.Props.baseLayer,
                    echo.label + ": the collar is drawn over the hair (" + collar?.Props.baseLayer + " over " + hair?.Props.baseLayer + ")");
                PawnDrawParms parms = PawnDrawParms.DefaultFor(pawn);
                parms.facing = Rot4.South;
                t.Check(collar != null && collar.Worker.CanDrawNow(collar, parms), echo.label + ": the collar draws");
                CheckDrawn(t, pawn, echo.label, ("Apparel_BasicShirt", false), ("Apparel_CowboyHat", false), ("Apparel_SmokepopBelt", true));
            }
            foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.North, Rot4.West })
            {
                Face(a, rot);
                Face(b, rot);
                yield return 20;
                yield return t.ShotAs("akatsuki-" + rot.ToStringHuman().ToLowerInvariant());
            }

            EchoUtility.Revert(ra, collapse: false);
            EchoUtility.Revert(rb, collapse: false);
            yield return 2;
            foreach ((Pawn pawn, EchoDef echo) in new[] { (a, pain), (b, itachi) })
            {
                t.Check(!CostumeNodes(pawn, echo.manifestHediff).Any(), echo.label + ": cloak and collar are gone after revert");
                CheckDrawn(t, pawn, echo.label + " after revert", ("Apparel_BasicShirt", true), ("Apparel_CowboyHat", true));
            }
        }

        [RimArtTest("Echo", "costume 4 Pain's piercings are on the head over the beard and face parts and under the hair, narrower on a narrow head (close-up screenshots)")]
        private static IEnumerable<int> PainPiercings(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            EchoDef pain = DefDatabase<EchoDef>.GetNamed("AG_Echo_Pain");
            HediffDef form = pain.manifestHediff;
            var props = form.RenderNodeProperties?.OfType<PawnRenderNodeProperties_EchoCostume>().ToList();
            t.Check(props?.Count == 3, "Pain's hero form has the cloak, the collar and the piercings (" + (props?.Count ?? 0) + " costume nodes)");
            var studProps = props?.FirstOrDefault(p => p.texPath == "RimArt/Echo/Costume/PainPiercings");
            if (!t.Check(studProps?.parentTagDef == PawnRenderNodeTagDefOf.Head, "the piercings are a head node")) yield break;
            foreach (string facing in new[] { "south", "east", "north" })
                t.Check(ContentFinder<UnityEngine.Texture2D>.Get(studProps.texPath + "_" + facing, false) != null,
                    "piercings " + facing + " texture loads");

            // An Echo has one Host at a time, so one Host is checked with an average head, then a narrow one.
            Pawn host = Colonist(t);
            host.story.headType = DefDatabase<HeadTypeDef>.GetNamed("Male_AverageNormal");
            EchoRecord record = EchoUtility.ForceHost(pain, host);
            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "the Host manifested Pain");
            foreach (string head in new[] { "Male_AverageNormal", "Male_NarrowNormal" })
            {
                host.story.headType = DefDatabase<HeadTypeDef>.GetNamed(head);
                host.Drawer.renderer.SetAllGraphicsDirty();
                yield return 2;
                PawnRenderNode studs = CostumeNodes(host, form).FirstOrDefault(n => n.Props == studProps);
                PawnRenderNode hair = RenderNodes(host).FirstOrDefault(n => n.Props.debugLabel == "Hair");
                PawnRenderNode beard = RenderNodes(host).FirstOrDefault(n => n.Props.debugLabel == "Beard");
                t.Check(studs?.parent?.Props.tagDef == PawnRenderNodeTagDefOf.Head, head + ": the piercings hang on the head");
                t.Check(studs != null && hair != null && beard != null
                    && beard.Props.baseLayer < studs.Props.baseLayer && studs.Props.baseLayer < hair.Props.baseLayer,
                    head + ": beard " + beard?.Props.baseLayer + " under piercings " + studs?.Props.baseLayer + " under hair " + hair?.Props.baseLayer);
                bool narrow = host.story.headType.narrow;
                foreach ((Rot4 rot, float want) in new[] { (Rot4.South, narrow ? 0.84f : 1f), (Rot4.East, narrow ? 0.7f : 1f) })
                {
                    PawnDrawParms parms = PawnDrawParms.DefaultFor(host);
                    parms.facing = rot;
                    float got = studs == null ? 0f : studs.Worker.ScaleFor(studs, parms).x;
                    t.Check(System.Math.Abs(got - want) < 0.001f,
                        head + " facing " + rot.ToStringHuman() + ": width x" + got.ToString("0.###") + " (want " + want + ")");
                }
                t.Check(!EchoCostume.CoversFace(host), head + ": Pain's face is not covered (Facial Animation keeps its eyebrows)");
                foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.West })
                {
                    Face(host, rot);
                    yield return 20;
                    yield return t.ShotAs("pain-" + (narrow ? "narrow" : "average") + "-" + rot.ToStringHuman().ToLowerInvariant(), host.Position, 1.6f);
                }
            }
            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(!CostumeNodes(host, form).Any(), "no piercings after revert");
        }

        // Obito has no EchoDef until his kit is ported, so the hero form hediff is added directly.
        [RimArtTest("Echo", "costume 3 Obito's hero form draws the cloak, the collar and the spiral mask, the mask on the head between the hair and the collar and narrower on a narrow head (screenshots)")]
        private static IEnumerable<int> ObitoMask(RimArtTestContext t)
        {
            Setup(t);
            HediffDef form = DefDatabase<HediffDef>.GetNamed("AG_EchoManifest_Obito");
            var props = form.RenderNodeProperties?.OfType<PawnRenderNodeProperties_EchoCostume>().ToList();
            t.Check(props?.Count == 3, "the hero form has the cloak, the collar and the mask (" + (props?.Count ?? 0) + " costume nodes)");
            var maskProps = props?.FirstOrDefault(p => p.texPath == "RimArt/Echo/Costume/ObitoMask");
            if (!t.Check(maskProps?.parentTagDef == PawnRenderNodeTagDefOf.Head, "the mask is a head node")) yield break;
            foreach (string facing in new[] { "south", "east", "west", "north" })
                t.Check(ContentFinder<UnityEngine.Texture2D>.Get(maskProps.texPath + "_" + facing, false) != null,
                    "mask " + facing + " texture loads");

            Pawn a = Colonist(t, -2), b = Colonist(t, 2);
            foreach ((Pawn pawn, string head) in new[] { (a, "Male_AverageNormal"), (b, "Male_NarrowNormal") })
            {
                pawn.story.headType = DefDatabase<HeadTypeDef>.GetNamed(head);
                pawn.story.bodyType = BodyTypeDefOf.Thin;
                pawn.story.HairColor = new UnityEngine.Color(0.08f, 0.08f, 0.1f);
                Wear(pawn, "Apparel_CowboyHat");
                pawn.health.AddHediff(form);
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            yield return 2;
            foreach (Pawn pawn in new[] { a, b })
            {
                string who = pawn.story.headType.defName;
                List<PawnRenderNode> nodes = CostumeNodes(pawn, form).ToList();
                t.Check(nodes.Count == 3, who + ": cloak, collar and mask are in the render tree (" + nodes.Count + ")");
                PawnRenderNode mask = nodes.FirstOrDefault(n => n.Props == maskProps);
                PawnRenderNode collar = nodes.FirstOrDefault(n => n.Props.texPath == "RimArt/Echo/Costume/AkatsukiCollar");
                PawnRenderNode hair = RenderNodes(pawn).FirstOrDefault(n => n.Props.debugLabel == "Hair");
                t.Check(mask?.parent?.Props.tagDef == PawnRenderNodeTagDefOf.Head, who + ": the mask hangs on the head");
                t.Check(mask != null && collar != null && hair != null
                    && hair.Props.baseLayer < mask.Props.baseLayer && mask.Props.baseLayer < collar.Props.baseLayer,
                    who + ": hair " + hair?.Props.baseLayer + " under mask " + mask?.Props.baseLayer + " under collar " + collar?.Props.baseLayer);
                bool narrow = pawn.story.headType.narrow;
                foreach ((Rot4 rot, float want) in new[] { (Rot4.South, narrow ? 0.84f : 1f), (Rot4.East, narrow ? 0.7f : 1f) })
                {
                    PawnDrawParms parms = PawnDrawParms.DefaultFor(pawn);
                    parms.facing = rot;
                    float got = mask == null ? 0f : mask.Worker.ScaleFor(mask, parms).x;
                    t.Check(System.Math.Abs(got - want) < 0.001f,
                        who + " facing " + rot.ToStringHuman() + ": mask width x" + got.ToString("0.###") + " (want " + want + ")");
                }
                CheckDrawn(t, pawn, who, ("Apparel_CowboyHat", false));
                t.Check(EchoCostume.CoversFace(pawn), who + ": the mask counts as covering the face");
                // Facial Animation (when loaded) draws eyebrows at layer 100, over the mask, unless hidden.
                PawnDrawParms south = PawnDrawParms.DefaultFor(pawn);
                south.facing = Rot4.South;
                List<PawnRenderNode> brows = RenderNodes(pawn)
                    .Where(n => n.Props.debugLabel?.StartsWith(Patch_CanDrawNow_FaceCovered.BrowLabel) == true).ToList();
                if (brows.Count == 0) t.Log(who + ": no Facial Animation eyebrow node (the mod is not loaded); eyebrow check skipped");
                foreach (PawnRenderNode brow in brows)
                    t.Check(!brow.Worker.CanDrawNow(brow, south), who + ": " + brow.Props.debugLabel + " (layer " + brow.Props.baseLayer + ") is not drawn over the mask");
            }
            foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.North, Rot4.West })
            {
                Face(a, rot);
                Face(b, rot);
                yield return 20;
                yield return t.ShotAs("obito-" + rot.ToStringHuman().ToLowerInvariant());
            }

            foreach (Pawn pawn in new[] { a, b })
            {
                pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(form));
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            yield return 2;
            foreach (Pawn pawn in new[] { a, b })
            {
                t.Check(!CostumeNodes(pawn, form).Any(), pawn.story.headType.defName + ": nothing of Obito is drawn after the form is removed");
                CheckDrawn(t, pawn, pawn.story.headType.defName + " after", ("Apparel_CowboyHat", true));
                PawnDrawParms south = PawnDrawParms.DefaultFor(pawn);
                south.facing = Rot4.South;
                foreach (PawnRenderNode brow in RenderNodes(pawn).Where(n => n.Props.debugLabel?.StartsWith(Patch_CanDrawNow_FaceCovered.BrowLabel) == true))
                    t.Check(brow.Worker.CanDrawNow(brow, south), pawn.story.headType.defName + ": " + brow.Props.debugLabel + " is drawn again");
            }
        }

        // Minato has no EchoDef until his kit is ported, so the hero form hediff is added directly.
        [RimArtTest("Echo", "costume 5 Minato's hero form draws the haori on the body and the forehead protector on the head over the hair, hides worn clothes and hats but not belts, narrower on a narrow head (screenshots)")]
        private static IEnumerable<int> MinatoHaori(RimArtTestContext t)
        {
            Setup(t);
            HediffDef form = DefDatabase<HediffDef>.GetNamed("AG_EchoManifest_Minato");
            var props = form.RenderNodeProperties?.OfType<PawnRenderNodeProperties_EchoCostume>().ToList();
            t.Check(props?.Count == 2, "the hero form has the haori and the forehead protector (" + (props?.Count ?? 0) + " costume nodes)");
            var haoriProps = props?.FirstOrDefault(p => p.parentTagDef == PawnRenderNodeTagDefOf.ApparelBody);
            var bandProps = props?.FirstOrDefault(p => p.parentTagDef == PawnRenderNodeTagDefOf.Head);
            if (!t.Check(haoriProps?.bodyTypeGraphicPaths != null && bandProps != null,
                "one node on the body apparel with body types, one on the head")) yield break;
            t.Check(haoriProps.hideBodyApparel && haoriProps.hideHeadgear, "the haori hides body apparel and headgear");
            foreach (string facing in new[] { "south", "east", "north" })
            {
                foreach (BodyTypeGraphicData body in haoriProps.bodyTypeGraphicPaths)
                    t.Check(ContentFinder<UnityEngine.Texture2D>.Get(body.texturePath + "_" + facing, false) != null,
                        "haori " + body.bodyType.defName + " " + facing + " texture loads");
                t.Check(ContentFinder<UnityEngine.Texture2D>.Get(bandProps.texPath + "_" + facing, false) != null,
                    "forehead protector " + facing + " texture loads");
            }

            // Only the first wears a pack, so the second shows the writing on the haori's back.
            Pawn a = Colonist(t, -2), b = Colonist(t, 2);
            foreach ((Pawn pawn, string head) in new[] { (a, "Male_AverageNormal"), (b, "Male_NarrowNormal") })
            {
                pawn.story.headType = DefDatabase<HeadTypeDef>.GetNamed(head);
                pawn.story.bodyType = BodyTypeDefOf.Thin;
                pawn.story.HairColor = new UnityEngine.Color(0.98f, 0.84f, 0.36f);
                foreach (string piece in new[] { "Apparel_BasicShirt", "Apparel_Pants", "Apparel_CowboyHat" })
                    Wear(pawn, piece);
                if (pawn == a) Wear(pawn, "Apparel_SmokepopBelt");
                pawn.health.AddHediff(form);
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            yield return 2;
            foreach (Pawn pawn in new[] { a, b })
            {
                string who = pawn.story.headType.defName;
                List<PawnRenderNode> nodes = CostumeNodes(pawn, form).ToList();
                t.Check(nodes.Count == 2, who + ": the haori and the forehead protector are in the render tree (" + nodes.Count + ")");
                PawnRenderNode haori = nodes.FirstOrDefault(n => n.Props == haoriProps);
                PawnRenderNode headband = nodes.FirstOrDefault(n => n.Props == bandProps);
                t.Check(haori?.PrimaryGraphic?.path == "RimArt/Echo/Costume/MinatoHaori_Thin",
                    who + ": the haori is the Thin one (" + haori?.PrimaryGraphic?.path + ")");
                PawnRenderNode hair = RenderNodes(pawn).FirstOrDefault(n => n.Props.debugLabel == "Hair");
                t.Check(headband?.parent?.Props.tagDef == PawnRenderNodeTagDefOf.Head, who + ": the forehead protector hangs on the head");
                t.Check(headband != null && hair != null && headband.Props.baseLayer > hair.Props.baseLayer,
                    who + ": the forehead protector is drawn over the hair (" + headband?.Props.baseLayer + " over " + hair?.Props.baseLayer + ")");
                bool narrow = pawn.story.headType.narrow;
                foreach ((Rot4 rot, float want) in new[] { (Rot4.South, narrow ? 0.84f : 1f), (Rot4.East, narrow ? 0.7f : 1f) })
                {
                    PawnDrawParms parms = PawnDrawParms.DefaultFor(pawn);
                    parms.facing = rot;
                    float got = headband == null ? 0f : headband.Worker.ScaleFor(headband, parms).x;
                    t.Check(System.Math.Abs(got - want) < 0.001f,
                        who + " facing " + rot.ToStringHuman() + ": forehead protector width x" + got.ToString("0.###") + " (want " + want + ")");
                }
                CheckDrawn(t, pawn, who, ("Apparel_BasicShirt", false), ("Apparel_CowboyHat", false));
                if (pawn == a) CheckDrawn(t, pawn, who, ("Apparel_SmokepopBelt", true));
                t.Check(!EchoCostume.CoversFace(pawn), who + ": the face is not covered (Facial Animation keeps its eyebrows)");
            }
            foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.North, Rot4.West })
            {
                Face(a, rot);
                Face(b, rot);
                yield return 20;
                yield return t.ShotAs("minato-" + rot.ToStringHuman().ToLowerInvariant());
            }

            foreach (Pawn pawn in new[] { a, b })
            {
                pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(form));
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            yield return 2;
            foreach (Pawn pawn in new[] { a, b })
            {
                string who = pawn.story.headType.defName;
                t.Check(!CostumeNodes(pawn, form).Any(), who + ": nothing of Minato is drawn after the form is removed");
                CheckDrawn(t, pawn, who + " after", ("Apparel_BasicShirt", true), ("Apparel_CowboyHat", true));
            }
        }
    }
}
