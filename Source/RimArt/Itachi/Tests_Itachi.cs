using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>Game tests for Itachi's kit (run with -quicktest -rimarttest=itachi).</summary>
    public static class Tests_Itachi
    {
        private static EchoDef Itachi => DefDatabase<EchoDef>.GetNamed("AG_Echo_Itachi");
        private static GeneDef Plexus => DefDatabase<GeneDef>.GetNamed("AG_DispersalPlexus");
        private static ThingDef Autopistol => DefDatabase<ThingDef>.GetNamed("Gun_Autopistol");
        private static ThingDef Knife => DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife");

        private static Faction enemyFaction;

        /// <summary>A cleared arena with Itachi at the centre, awakened and (by default) manifested.</summary>
        private static Pawn Setup(RimArtTestContext t, bool manifest = true)
        {
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            enemyFaction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            Pawn itachi = t.Colonist(t.center);
            EchoRecord record = EchoUtility.ForceHost(Itachi, itachi);
            echoes.charge = 100f;
            if (manifest) EchoUtility.Manifest(record);
            return itachi;
        }

        private static void TearDown() => EchoDevice.workingForTests = null;

        private static EchoRecord Record(Pawn itachi) => GameComponent_Echoes.Get.HostRecord(itachi);

        /// <summary>An enemy of one faction for the whole scenario, so allies are allies. No armour.</summary>
        private static Pawn Enemy(RimArtTestContext t, int dx, int dz, bool armed = false)
        {
            Pawn pawn = t.Enemy(t.center + new IntVec3(dx, 0, dz), armed);
            if (enemyFaction != null && pawn.Faction != enemyFaction) pawn.SetFaction(enemyFaction);
            pawn.apparel?.DestroyAll();
            return pawn;
        }

        private static Pawn Mech(RimArtTestContext t, int dx, int dz)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
            Faction mechs = Faction.OfMechanoids;
            if (kind == null || mechs == null) return null;
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, mechs));
            GenSpawn.Spawn(pawn, t.center + new IntVec3(dx, 0, dz), t.map);
            RimArtTestContext.Hold(pawn);
            return pawn;
        }

        private static Gene_Dispersal Gene(Pawn pawn) => pawn.genes?.GetFirstGeneOfType<Gene_Dispersal>();
        private static bool Has(Pawn pawn, AbilityDef def) => pawn.abilities?.GetAbility(def) != null;
        private static Ability AbilityOf(Pawn pawn, AbilityDef def) => pawn.abilities.GetAbility(def);

        private static HediffComp_Susanoo Susanoo(Pawn itachi) =>
            itachi.health.hediffSet.GetFirstHediffOfDef(ItachiDefOf.AG_Susanoo)?.TryGetComp<HediffComp_Susanoo>();

        /// <summary>Casts at once, skipping the warm-up; pays the charge like a real cast.</summary>
        private static void Cast(Pawn itachi, AbilityDef def) =>
            AbilityOf(itachi, def).Activate(new LocalTargetInfo(itachi), LocalTargetInfo.Invalid);

        private static DamageWorker.DamageResult Shoot(Pawn target, Pawn shooter, float amount) =>
            target.TakeDamage(new DamageInfo(DamageDefOf.Bullet, amount, 0f, -1f, shooter));

        private static int Hurts(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.Count(h => h is Hediff_Injury || h is Hediff_MissingPart);

        private static bool InFalseFace(Pawn pawn) => pawn.MentalStateDef == ItachiDefOf.AG_FalseFace;

        private static MentalState_FalseFace State(Pawn pawn) => pawn.MentalState as MentalState_FalseFace;

        private static bool AttacksAlly(Pawn victim, Pawn ally)
        {
            if (Hurts(ally) > 0) return true;
            Job job = victim.CurJob;
            if (job != null && (job.def == JobDefOf.AttackStatic || job.def == JobDefOf.AttackMelee) && job.targetA.Thing == ally) return true;
            return victim.stances?.curStance is Stance_Busy busy && busy.focusTarg.Thing == ally;
        }

        // ---- the gene ----

        [RimArtTest("Itachi", "gene 1 awakening adds the plexus as a xenogene and Sickly; crow abilities only while manifested")]
        private static IEnumerable<int> GeneOnAwakening(RimArtTestContext t)
        {
            Pawn itachi = Setup(t, manifest: false);
            yield return 1;
            t.Check(itachi.genes.HasActiveGene(Plexus), "the plexus is on him after awakening");
            t.Check(itachi.genes.Xenogenes.Any(g => g.def == Plexus), "as a xenogene");
            t.Check(itachi.story.traits.HasTrait(TraitDef.Named("Immunity"), -1), "Sickly (Immunity -1) is forced");
            t.Check(!Has(itachi, ItachiDefOf.AG_DispersalMurder) && !Has(itachi, ItachiDefOf.AG_DispersalCarrion),
                "no crow abilities before manifesting");
            EchoUtility.Manifest(Record(itachi));
            yield return 2;
            t.Check(Has(itachi, ItachiDefOf.AG_DispersalMurder) && Has(itachi, ItachiDefOf.AG_DispersalCarrion),
                "Crow Dispersal and Carrion while manifested");
            t.Check(Has(itachi, ItachiDefOf.AG_ItachiFalseFace) && Has(itachi, ItachiDefOf.AG_ItachiSusanoo),
                "False Face and Susanoo while manifested");
            EchoUtility.Revert(Record(itachi), collapse: false);
            yield return 2;
            t.Check(!Has(itachi, ItachiDefOf.AG_DispersalMurder) && !Has(itachi, ItachiDefOf.AG_DispersalCarrion),
                "the crow abilities are taken back on revert");
            t.Check(itachi.genes.HasActiveGene(Plexus), "the gene stays");
            TearDown();
        }

        [RimArtTest("Itachi", "gene 2 outside hero form a 12-damage shot lands and there is no toggle; manifested it scatters")]
        private static IEnumerable<int> ScatterOnlyManifested(RimArtTestContext t)
        {
            Pawn itachi = Setup(t, manifest: false);
            Pawn enemy = Enemy(t, 4, 0);
            yield return 3;
            Gene_Dispersal gene = Gene(itachi);
            t.Check(gene != null && DispersalRegistry.CarrierFor(itachi) == gene, "the gene reports to the registry");
            t.Check(!gene.GetGizmos().Any(), "no Scatter toggle outside hero form");
            int charges = gene.Charges;
            DamageWorker.DamageResult hit = Shoot(itachi, enemy, 12f);
            t.Check(hit.totalDamageDealt > 0f, "the shot lands (" + hit.totalDamageDealt + ")");
            t.Check(itachi.Position == t.center, "he did not move");
            t.Check(gene.Charges == charges, "no charge spent (" + gene.Charges + ")");

            EchoUtility.Manifest(Record(itachi));
            yield return 3;
            t.Check(gene.GetGizmos().Any(), "the Scatter toggle is back in hero form");
            hit = Shoot(itachi, enemy, 12f);
            yield return 1;
            t.Check(hit.totalDamageDealt == 0f, "the hit was cancelled (" + hit.totalDamageDealt + ")");
            t.Check(itachi.Position != t.center, "he scattered (" + RimArtTestContext.Describe(itachi) + ")");
            t.Check(gene.Charges == charges - 1, "one charge spent (" + gene.Charges + ")");
            TearDown();
        }

        [RimArtTest("Itachi", "gene 3 charges persist across a revert and regrow while not manifested", 6000)]
        private static IEnumerable<int> ChargesPersist(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            yield return 2;
            Gene_Dispersal gene = Gene(itachi);
            gene.Spend();
            t.Check(gene.Charges == gene.MaxCharges - 1, "one spent (" + gene.Charges + ")");
            EchoUtility.Revert(Record(itachi), collapse: false);
            yield return 5;
            t.Check(gene.Charges == gene.MaxCharges - 1, "still spent after revert (" + gene.Charges + ")");
            yield return gene.RechargeTicks + 200;
            t.Check(gene.Charges == gene.MaxCharges, "regrown while not manifested (" + gene.Charges + ")");
            EchoUtility.Manifest(Record(itachi));
            yield return 2;
            t.Check(Gene(itachi) == gene && gene.Charges == gene.MaxCharges, "the same gene, full, in hero form");
            TearDown();
        }

        [RimArtTest("Itachi", "gene 4 a pawn holding the gene outside hero form loses a stale Crow Dispersal once")]
        private static IEnumerable<int> StaleAbilities(RimArtTestContext t)
        {
            Setup(t, manifest: false);
            Pawn other = t.Colonist(t.center + new IntVec3(3, 0, 0));
            other.genes.AddGene(Plexus, xenogene: true);
            other.abilities.GainAbility(ItachiDefOf.AG_DispersalMurder);
            t.Check(Has(other, ItachiDefOf.AG_DispersalMurder), "given by hand");
            yield return 60;
            t.Check(!Has(other, ItachiDefOf.AG_DispersalMurder), "taken back by the gene's reconcile");
            t.Check(other.genes.HasActiveGene(Plexus), "the gene itself stays");
            TearDown();
        }

        // ---- False Face ----

        [RimArtTest("Itachi", "falseface 1 a ranged enemy aiming at Itachi turns on its nearest ally; costs 3 charge")]
        private static IEnumerable<int> RangedVictim(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            Pawn victim = Enemy(t, 6, 0);
            t.Equip(victim, Autopistol);
            Pawn ally = Enemy(t, 6, 3);
            Pawn far = Enemy(t, -8, -6);
            victim.mindState.enemyTarget = itachi;
            yield return 2;
            float charge = GameComponent_Echoes.Get.charge;
            Cast(itachi, ItachiDefOf.AG_ItachiFalseFace);
            yield return 2;
            t.Check(GameComponent_Echoes.Get.charge == charge - 3f, "3 charge paid (" + charge + " -> " + GameComponent_Echoes.Get.charge + ")");
            t.Check(InFalseFace(victim), "the victim is in False Face (" + victim.MentalStateDef?.defName + ")");
            t.Check(State(victim)?.falseItachi == ally, "its false Itachi is the nearest ally");
            t.Check(victim.mindState.enemyTarget == ally, "its enemy target is the ally");
            t.Check(!InFalseFace(ally) && !InFalseFace(far), "the ally and the enemy that was not looking are untouched");
            bool attacked = false;
            for (int i = 0; i < 30 && !attacked; i++)
            {
                yield return 10;
                attacked = AttacksAlly(victim, ally);
            }
            t.Check(attacked, "the victim attacks the ally within 5 s (" + RimArtTestContext.Describe(victim) + ")");
            TearDown();
        }

        [RimArtTest("Itachi", "falseface 2 a melee victim hits its ally and the ally fights back")]
        private static IEnumerable<int> AllyFightsBack(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            Pawn victim = Enemy(t, 3, 0);
            t.Equip(victim, Knife);
            Pawn ally = Enemy(t, 3, 2);
            t.Equip(ally, Knife);
            victim.mindState.enemyTarget = itachi;
            yield return 2;
            Cast(itachi, ItachiDefOf.AG_ItachiFalseFace);
            yield return 2;
            t.Check(InFalseFace(victim) && State(victim).falseItachi == ally, "the victim is on its ally");
            bool hit = false;
            for (int i = 0; i < 40 && !hit; i++)
            {
                yield return 10;
                hit = Hurts(ally) > 0 || ally.mindState.meleeThreat == victim;
            }
            t.Check(hit, "the ally was attacked (" + RimArtTestContext.Describe(victim) + " / " + RimArtTestContext.Describe(ally) + ")");
            t.Check(ally.HostileTo(victim) && victim.HostileTo(ally), "the two are hostile to each other while it lasts");
            bool back = false;
            for (int i = 0; i < 30 && !back; i++)
            {
                yield return 10;
                back = ally.mindState.meleeThreat == victim
                    || (ally.CurJob != null && ally.CurJob.def == JobDefOf.AttackMelee && ally.CurJob.targetA.Thing == victim)
                    || (ally.mindState.enemyTarget == victim);
            }
            t.Check(back, "the ally fights back (" + RimArtTestContext.Describe(ally) + ")");
            TearDown();
        }

        [RimArtTest("Itachi", "falseface 3 breaks on damage, on the ally going down, and ends after 10 s with targets cleared")]
        private static IEnumerable<int> Breaks(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            Pawn v1 = Enemy(t, 5, 0), a1 = Enemy(t, 5, 2);
            Pawn v2 = Enemy(t, -5, 0), a2 = Enemy(t, -5, -2);
            Pawn v3 = Enemy(t, 0, 6), a3 = Enemy(t, 2, 6);
            foreach (Pawn v in new[] { v1, v2, v3 }) v.mindState.enemyTarget = itachi;
            yield return 2;
            int start = t.Now;
            FalseFaceCast.Cast(itachi, 15f, true);
            yield return 2;
            t.Check(InFalseFace(v1) && InFalseFace(v2) && InFalseFace(v3), "all three are caught");
            t.Check(State(v1).falseItachi == a1 && State(v2).falseItachi == a2 && State(v3).falseItachi == a3, "each on its own nearest ally");

            DamageWorker.DamageResult cut = v1.TakeDamage(new DamageInfo(DamageDefOf.Cut, 3f, 0f, -1f, itachi));
            for (int i = 0; i < 6 && InFalseFace(v1); i++) yield return 5;
            t.Check(!InFalseFace(v1), "damage breaks it (dealt " + cut.totalDamageDealt + ", " + RimArtTestContext.Describe(v1) + ")");

            HealthUtility.DamageUntilDowned(a2, false);
            for (int i = 0; i < 9 && InFalseFace(v2); i++) yield return 10;
            t.Check(!InFalseFace(v2), "its false Itachi going down breaks it (a2 downed " + a2.Downed + ", " + RimArtTestContext.Describe(v2) + ")");

            t.Check(InFalseFace(v3), "the third still holds at " + (t.Now - start) + " ticks");
            while (t.Now - start < 660) yield return 10;
            t.Check(!InFalseFace(v3), "it ends on its own after 10 s");
            t.Check(v3.mindState.enemyTarget != a3 && v3.mindState.meleeThreat != a3, "the false target is cleared");
            t.Check(!v3.HostileTo(a3), "no longer hostile to its ally");
            TearDown();
        }

        [RimArtTest("Itachi", "falseface 4 mechanoids and enemies not looking at Itachi are untouched; Itachi going down ends it")]
        private static IEnumerable<int> Immune(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            Pawn decoy = t.Colonist(t.center + new IntVec3(0, 0, -5));
            Pawn bystander = Enemy(t, -4, 0), bystanderAlly = Enemy(t, -4, 2);
            bystander.mindState.enemyTarget = decoy;
            Pawn victim = Enemy(t, 4, 0), ally = Enemy(t, 4, 2);
            victim.mindState.enemyTarget = itachi;
            Pawn mech = Mech(t, 0, 4), mechAlly = Mech(t, 0, 6);
            if (mech != null) mech.mindState.enemyTarget = itachi;
            yield return 2;
            FalseFaceCast.Cast(itachi, 15f, true);
            yield return 2;
            t.Check(InFalseFace(victim), "the one looking at him is caught");
            t.Check(!InFalseFace(bystander), "the one looking at someone else is not");
            if (mech != null) t.Check(!InFalseFace(mech), "the mechanoid is not");
            else t.Log("no mechanoid faction or kind on this world; mech check skipped");
            HealthUtility.DamageUntilDowned(itachi, false);
            yield return 32;
            t.Check(!InFalseFace(victim), "Itachi going down ends it");
            TearDown();
        }

        // ---- Susanoo ----

        [RimArtTest("Itachi", "susanoo 1 costs 20, halves his speed, the Mirror absorbs outside hits and not his own")]
        private static IEnumerable<int> Mirror(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            Pawn enemy = Enemy(t, 4, 0);
            yield return 2;
            float speed = itachi.GetStatValue(StatDefOf.MoveSpeed);
            float charge = GameComponent_Echoes.Get.charge;
            // A generated colonist may carry an old scar or a missing toe: count injuries from here.
            int hurts = Hurts(itachi);
            Cast(itachi, ItachiDefOf.AG_ItachiSusanoo);
            yield return 2;
            HediffComp_Susanoo comp = Susanoo(itachi);
            t.Check(comp != null, "the Susanoo hediff is on him");
            t.Check(GameComponent_Echoes.Get.charge == charge - 20f, "20 charge paid (" + GameComponent_Echoes.Get.charge + ")");
            float slowed = itachi.GetStatValue(StatDefOf.MoveSpeed);
            t.Check(slowed < speed * 0.6f && slowed > speed * 0.4f, "move speed halved (" + speed + " -> " + slowed + ")");
            DamageWorker.DamageResult hit = Shoot(itachi, enemy, 30f);
            t.Check(hit.totalDamageDealt == 0f && Hurts(itachi) == hurts, "a 30-damage shot from outside is absorbed (" + hit.totalDamageDealt + ")");
            hit = itachi.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 10f));
            t.Check(hit.totalDamageDealt == 0f && Hurts(itachi) == hurts, "damage with no instigator is absorbed too (" + hit.totalDamageDealt + ")");
            itachi.TakeDamage(new DamageInfo(DamageDefOf.Cut, 3f, 0f, -1f, itachi));
            t.Check(Hurts(itachi) > hurts, "his own cut lands (" + hurts + " -> " + Hurts(itachi) + ")");
            TearDown();
        }

        [RimArtTest("Itachi", "susanoo 2 Crow Dispersal is greyed out and Scatter stands down while it stands")]
        private static IEnumerable<int> CrowsStandDown(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            Pawn enemy = Enemy(t, 4, 0);
            yield return 2;
            t.Check(!AbilityOf(itachi, ItachiDefOf.AG_DispersalMurder).GizmoDisabled(out _), "Crow Dispersal is usable before");
            Cast(itachi, ItachiDefOf.AG_ItachiSusanoo);
            yield return 2;
            bool disabled = AbilityOf(itachi, ItachiDefOf.AG_DispersalMurder).GizmoDisabled(out string reason);
            t.Check(disabled && reason == "AG_ItachiSusanooHolds".Translate(), "Crow Dispersal is greyed out: " + reason);
            Gene_Dispersal gene = Gene(itachi);
            int charges = gene.Charges;
            DamageWorker.DamageResult hit = Shoot(itachi, enemy, 12f);
            yield return 1;
            t.Check(hit.totalDamageDealt == 0f, "the Mirror took the 12-damage shot");
            t.Check(itachi.Position == t.center, "no scatter");
            t.Check(gene.Charges == charges, "no charge spent (" + gene.Charges + ")");
            TearDown();
        }

        [RimArtTest("Itachi", "susanoo 3 Totsuka: a healthy target is cut, a downed one is sealed once as a kill with its gear left, mechanoids and later weak targets are only cut")]
        private static IEnumerable<int> Totsuka(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            Pawn healthy = Enemy(t, 2, 0);
            Pawn mech = Mech(t, 0, 2);
            yield return 2;
            Cast(itachi, ItachiDefOf.AG_ItachiSusanoo);
            yield return 2;
            HediffComp_Susanoo comp = Susanoo(itachi);
            t.Check(itachi.GetGizmos().OfType<Command_Target>().Any(g => g.defaultLabel.StartsWith("AG_ItachiTotsuka".Translate())),
                "the Totsuka Blade command is on him");

            int hurts = Hurts(healthy);
            comp.Stab(healthy);
            t.Check(Hurts(healthy) == hurts, "nothing lands at the click: the blade is on its way");
            yield return 30;
            t.Check(Hurts(healthy) > hurts && !healthy.Dead, "a healthy target takes the stab 0.45 s later (" + Hurts(healthy) + " injuries)");
            t.Check(!comp.Sealed, "not sealed");
            hurts = Hurts(healthy);
            comp.Stab(healthy);
            yield return 30;
            t.Check(Hurts(healthy) == hurts, "a second stab inside 3 s does nothing");
            yield return 121;

            if (mech != null)
            {
                HealthUtility.DamageUntilDowned(mech, false);
                comp.Stab(mech);
                yield return 30;
                t.Check(!comp.Sealed, "a downed mechanoid is not sealed");
                t.Check(!mech.Dead || (mech.Corpse != null && !mech.Corpse.Destroyed), "the mechanoid is cut, not taken");
                yield return 151;
            }
            else t.Log("no mechanoid faction or kind on this world; mech check skipped");

            // Spawned and downed in the same step as the stab: a held enemy still walks off within
            // a few hundred ticks, and the blade reaches only 4 cells.
            Pawn weak = t.Enemy(t.center + new IntVec3(-2, 0, 0), armed: true);
            if (enemyFaction != null && weak.Faction != enemyFaction) weak.SetFaction(enemyFaction);
            HealthUtility.DamageUntilDowned(weak, false);
            t.Check(weak.Downed || weak.health.summaryHealth.SummaryHealthPercent <= 0.3f,
                "the weak target is downed or at 30 % or less (downed " + weak.Downed + ", health "
                + weak.health.summaryHealth.SummaryHealthPercent.ToString("0.00") + ")");
            t.Check(comp.ValidTarget(weak) && comp.WouldSeal(weak), "in reach and sealable");
            // Weapons and apparel only: a raider's inventory also holds food, drugs or silver.
            int gear = (weak.equipment?.AllEquipmentListForReading.Count ?? 0) + (weak.apparel?.WornApparelCount ?? 0)
                + (weak.inventory?.innerContainer.Count(th => th.def.IsWeapon || th.def.IsApparel) ?? 0);
            t.Check(gear > 0, "it carries " + gear + " pieces of gear");
            IntVec3 cell = weak.Position;
            Faction faction = weak.Faction;
            int goodwill = faction?.GoodwillWith(Faction.OfPlayer) ?? 0;
            float kills = itachi.records.GetValue(RecordDefOf.Kills);
            comp.Stab(weak);
            t.Check(!comp.Sealed && !weak.Dead, "at the click the target still stands");
            yield return 30;
            t.Check(comp.Sealed, "sealed when the blade lands");
            t.Check(weak.Dead, "the target is dead");
            t.Check(!weak.Spawned && (weak.Corpse == null || weak.Corpse.Destroyed), "no corpse");
            t.Check(!GenRadial.RadialCellsAround(cell, 2f, true).Any(c => c.InBounds(t.map) && c.GetThingList(t.map).OfType<Corpse>().Any()),
                "no corpse on the ground either");
            int dropped = GenRadial.RadialCellsAround(cell, 3f, true).Where(c => c.InBounds(t.map))
                .Sum(c => c.GetThingList(t.map).Count(th => th.def.IsWeapon || th.def.IsApparel));
            t.Check(dropped >= gear, "its gear is on the ground (" + dropped + " of " + gear + ")");
            t.Check(itachi.records.GetValue(RecordDefOf.Kills) == kills + 1f, "counted as Itachi's kill (" + kills + " -> " + itachi.records.GetValue(RecordDefOf.Kills) + ")");
            t.Check(faction == null || faction.GoodwillWith(Faction.OfPlayer) == goodwill, "goodwill unchanged by the strip");
            yield return 151;

            Pawn weak2 = Enemy(t, 0, -2);
            HealthUtility.DamageUntilDowned(weak2, false);
            comp.Stab(weak2);
            yield return 30;
            t.Check(!weak2.Dead || (weak2.Corpse != null && !weak2.Corpse.Destroyed), "a second weak target is only cut");
            TearDown();
        }

        [RimArtTest("Itachi", "susanoo 4 ending drains him for 6 h and costs blood; reverting ends it early the same way")]
        private static IEnumerable<int> Drained(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            yield return 2;
            Cast(itachi, ItachiDefOf.AG_ItachiSusanoo);
            yield return 2;
            t.Check(Susanoo(itachi) != null, "up");
            yield return 740;
            t.Check(Susanoo(itachi) == null, "gone after 12 s");
            Hediff drained = itachi.health.hediffSet.GetFirstHediffOfDef(ItachiDefOf.AG_SusanooDrained);
            t.Check(drained != null, "drained");
            int left = drained?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? 0;
            t.Check(left > 14000 && left <= 15000, "for about 6 h (" + left + " ticks left)");
            float consciousness = itachi.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            t.Check(consciousness <= 0.71f, "consciousness down by 30 % (" + consciousness + ")");
            Hediff blood = itachi.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            t.Check(blood != null && blood.Severity > 0.05f && blood.Severity < 0.15f, "blood loss about 0.10 (" + blood?.Severity + ")");

            if (drained != null) itachi.health.RemoveHediff(drained);
            AbilityOf(itachi, ItachiDefOf.AG_ItachiSusanoo).ResetCooldown();
            GameComponent_Echoes.Get.charge = 100f;
            Cast(itachi, ItachiDefOf.AG_ItachiSusanoo);
            yield return 2;
            t.Check(Susanoo(itachi) != null, "up again");
            EchoUtility.Revert(Record(itachi), collapse: false);
            yield return 5;
            t.Check(Susanoo(itachi) == null, "reverting ended it");
            t.Check(itachi.health.hediffSet.HasHediff(ItachiDefOf.AG_SusanooDrained), "and drained him");
            TearDown();
        }

        // ---- defs and UI ----

        [RimArtTest("Itachi", "echo 1 the EchoDef is well formed and the think tree carries False Face")]
        private static IEnumerable<int> Defs(RimArtTestContext t)
        {
            List<string> errors = Itachi.ConfigErrors().ToList();
            t.Check(errors.Count == 0, "no config errors: " + string.Join("; ", errors));
            t.Check(Itachi.CastCost(ItachiDefOf.AG_ItachiFalseFace) == 3f && Itachi.CastCost(ItachiDefOf.AG_ItachiSusanoo) == 20f
                && Itachi.CastCost(ItachiDefOf.AG_DispersalMurder) == 0f, "cast costs 3 / 20 / 0");
            t.Check(Itachi.awakenGenes.Contains(Plexus), "the plexus is his awaken gene");
            t.Check(Plexus.abilities.NullOrEmpty(), "the gene lists no abilities");
            ThinkTreeDef tree = DefDatabase<ThinkTreeDef>.GetNamed("MentalStateCritical");
            bool node = tree.thinkRoot.subNodes.OfType<ThinkNode_ConditionalMentalState>()
                .Any(n => n.state == ItachiDefOf.AG_FalseFace && n.subNodes.OfType<JobGiver_FalseFace>().Any());
            t.Check(node, "MentalStateCritical has the False Face node with JobGiver_FalseFace");
            yield break;
        }

        [RimArtTest("Itachi", "ui 1 gizmos with the Susanoo up")]
        private static IEnumerable<int> Ui(RimArtTestContext t)
        {
            Pawn itachi = Setup(t);
            Enemy(t, 3, 0);
            yield return 2;
            Cast(itachi, ItachiDefOf.AG_ItachiSusanoo);
            yield return 5;
            Find.Selector.ClearSelection();
            Find.Selector.Select(itachi);
            yield return 5;
            yield return t.ShotAs("itachi-susanoo-gizmos");
            TearDown();
        }
    }
}
