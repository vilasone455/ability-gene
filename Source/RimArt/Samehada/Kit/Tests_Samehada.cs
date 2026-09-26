using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Game tests for the Samehada kit (run with -quicktest -rimarttest=samehada). Melee hits are made
    /// with the blade's own verb as surprise attacks on downed (anaesthetised) targets, so every hit
    /// lands and nobody fights back; the targets wear marine armour so four hits do not kill them.
    /// </summary>
    public static class Tests_Samehada
    {
        private static Pawn Holder(RimArtTestContext t, int charges, out CompSamehada blade)
        {
            // Drafted, so it stays where it is between orders; a drafted pawn does not attack a downed enemy on its own.
            Pawn holder = t.Colonist(t.center);
            t.Equip(holder, SamehadaDefOf.AG_Samehada);
            holder.Rotation = Rot4.East;
            blade = CompSamehada.HeldBy(holder);
            blade?.SetCharges(charges);
            t.Log("holder " + RimArtTestContext.Describe(holder) + " age " + holder.ageTracker.AgeBiologicalYears + " kind " + holder.kindDef.defName
                + " primary " + holder.equipment.Primary?.def.defName + " verbs " + (holder.equipment.PrimaryEq?.AllVerbs?.Count ?? -1)
                + " abilities " + (holder.abilities?.abilities == null ? "null" : string.Join(",", holder.abilities.abilities.Select(a => a.def.defName))));
            return holder;
        }

        /// <summary>A hostile humanlike that is down and cannot fight back, in marine armour.</summary>
        private static Pawn DownedEnemy(RimArtTestContext t, IntVec3 at)
        {
            Pawn enemy = t.Enemy(at, armed: false);
            Armour(enemy);
            enemy.health.AddHediff(HediffDefOf.Anesthetic).Severity = 1f;
            return enemy;
        }

        private static void Armour(Pawn pawn)
        {
            foreach (string name in new[] { "Apparel_PowerArmor", "Apparel_PowerArmorHelmet" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                var apparel = (Apparel)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                pawn.apparel?.Wear(apparel, false);
            }
        }

        private static void HealAll(Pawn pawn)
        {
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList()) pawn.health.RemoveHediff(injury);
        }

        private static float InjurySeverity(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);

        /// <summary>
        /// Gives the pawn two 10-point cuts on its torso. That is about 0.25 pain, over a wimp's 0.2 pain
        /// shock, so the trait is taken off first: a downed holder drops the blade and the test fails.
        /// </summary>
        private static void Wound(Pawn pawn)
        {
            TraitDef wimpDef = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
            Trait wimp = wimpDef == null ? null : pawn.story?.traits?.GetTrait(wimpDef);
            if (wimp != null) pawn.story.traits.RemoveTrait(wimp);
            BodyPartRecord torso = pawn.RaceProps.body.corePart;
            for (int i = 0; i < 2; i++)
            {
                var cut = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, torso);
                cut.Severity = 10f;
                pawn.health.AddHediff(cut, torso);
            }
        }

        private static Verb BladeVerb(Pawn holder) =>
            holder.equipment?.PrimaryEq?.AllVerbs?.FirstOrDefault(v => v.IsMeleeAttack);

        /// <summary>One melee attack with the blade that cannot miss.</summary>
        private static bool Strike(RimArtTestContext t, Pawn holder, Pawn target)
        {
            Verb verb = BladeVerb(holder);
            if (!t.Check(verb != null, "the holder has the blade's melee verb")) return false;
            bool started = holder.meleeVerbs.TryMeleeAttack(target, verb, true);
            t.Log(t.Now + " strike " + target.LabelShort + ": " + started + " | " + RimArtTestContext.Describe(holder));
            return started;
        }

        private static void Trace(RimArtTestContext t, Pawn holder, CompSamehada blade, params Pawn[] others)
        {
            string line = t.Now + " | " + RimArtTestContext.Describe(holder) + " charges " + (blade?.Charges ?? -1)
                + (blade != null && blade.SharkSkinActive ? " sharkskin " + blade.SharkSkinTicksLeft : "")
                + " injuries " + InjurySeverity(holder).ToString("0.0");
            foreach (Pawn o in others) line += " | " + RimArtTestContext.Describe(o) + " drained " + SamehadaFeeding.Stacks(o) + " injuries " + InjurySeverity(o).ToString("0.0");
            t.Log(line);
            foreach (Hediff_Injury h in holder.health.hediffSet.hediffs.OfType<Hediff_Injury>())
                t.Log("      holder injury " + h.def.defName + " on " + h.Part?.def.defName + " " + h.Severity.ToString("0.0")
                    + (h.IsPermanent() ? " scar" : "") + " from " + (h.sourceDef?.defName ?? "?") + " " + h.sourceToolLabel + " " + h.sourceLabel);
        }

        /// <summary>Casts a self-targeted Samehada ability and traces until its job ends (at most 180 ticks).</summary>
        private static IEnumerable<int> Cast(RimArtTestContext t, Pawn holder, CompSamehada blade, AbilityDef def, string shot)
        {
            Ability ability = holder.abilities.GetAbility(def);
            if (!t.Check(ability != null, "the holder has " + def.label)) yield break;
            if (!t.Check(ability.CanCast, def.label + " can be cast (" + ability.CanCast.Reason + ")")) yield break;
            ability.QueueCastingJob(new LocalTargetInfo(holder), LocalTargetInfo.Invalid);
            int cast = t.Now;
            t.Check(holder.CurJobDef == SamehadaDefOf.AG_CastSamehada, "the cast job started (job " + holder.CurJobDef?.defName + ")");
            for (int i = 0; i < 60; i++)
            {
                Trace(t, holder, blade);
                if (holder.CurJobDef != SamehadaDefOf.AG_CastSamehada)
                {
                    t.Log("the cast job ended after " + (t.Now - cast) + " ticks");
                    break;
                }
                if (i == 8 && shot != null) yield return t.ShotAs(shot);
                yield return 3;
            }
        }

        [RimArtTest("Samehada", "feed 1 hits give charges, 3 stacks at most, the holder heals, charges add damage")]
        private static IEnumerable<int> FeedHits(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 0, out CompSamehada blade);
            Pawn enemy = DownedEnemy(t, t.center + IntVec3.East);
            Wound(holder);
            yield return 5;
            if (!t.Check(blade != null, "the holder holds Samehada")) yield break;
            t.Check(holder.abilities.GetAbility(SamehadaDefOf.AG_Samehada_SharkSkin) != null && holder.abilities.GetAbility(SamehadaDefOf.AG_Samehada_Fusion) != null,
                "holding it grants Shark Skin and Fusion");
            float before = InjurySeverity(holder);
            for (int hit = 1; hit <= 4; hit++)
            {
                HealAll(enemy);
                int charges = blade.Charges;
                // The damage bonus, read off the prefix on a hit with the blade's tool.
                var dinfo = new DamageInfo(DamageDefOf.Cut, 14f, 0f, -1f, holder, null, SamehadaDefOf.AG_Samehada, DamageInfo.SourceCategory.ThingOrUnknown, enemy);
                dinfo.SetTool(SamehadaDefOf.AG_Samehada.tools[0]);
                Patch_Samehada_ChargeDamage.Prefix(ref dinfo);
                t.Check(UnityEngine.Mathf.Abs(dinfo.Amount - (14f + 2f * charges)) < 0.01f, "hit " + hit + ": a 14 cut becomes " + dinfo.Amount + " at " + charges + " charges");
                if (!Strike(t, holder, enemy)) yield break;
                yield return 1;
                Trace(t, holder, blade, enemy);
                t.Check(blade.Charges == hit, "after hit " + hit + " the blade has " + hit + " charges (" + blade.Charges + ")");
                t.Check(SamehadaFeeding.Stacks(enemy) == UnityEngine.Mathf.Min(hit, 3), "after hit " + hit + " the enemy has " + UnityEngine.Mathf.Min(hit, 3) + " Drained stacks (" + SamehadaFeeding.Stacks(enemy) + ")");
                if (hit == 2) yield return t.ShotAs("feed-hit");
                yield return 190;
            }
            float after = InjurySeverity(holder);
            t.Check(UnityEngine.Mathf.Abs(before - after - 16f) < 0.5f, "4 hits healed the holder 16 (" + before.ToString("0.0") + " -> " + after.ToString("0.0") + ")");
            Hediff drained = enemy.health.hediffSet.GetFirstHediffOfDef(SamehadaDefOf.AG_SamehadaDrained);
            int left = drained?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? -1;
            t.Check(left > 900 && left <= 1200, "Drained lasts 20 s from the last hit (" + left + " ticks left)");
            float consciousness = enemy.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            t.Log("enemy consciousness " + consciousness.ToString("0.00") + " (anaesthetised as well)");
            t.Check(drained != null && drained.CurStageIndex == 2, "Drained is at its third stage (" + drained?.CurStageIndex + ")");
        }

        // The 60 s clock is not waited out: the test moves it on with AgeChargeClock, then checks
        // either side of the 60 s mark with a real 60-tick wait between.
        [RimArtTest("Samehada", "feed 2 a charge is lost after 60 s without a hit")]
        private static IEnumerable<int> FeedDecay(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 0, out CompSamehada blade);
            Pawn enemy = DownedEnemy(t, t.center + IntVec3.East);
            yield return 5;
            for (int i = 0; i < 3; i++)
            {
                HealAll(enemy);
                if (!Strike(t, holder, enemy)) yield break;
                yield return i < 2 ? 190 : 1;
            }
            t.Check(blade.Charges == 3, "3 hits: 3 charges (" + blade.Charges + ")");
            // Nobody next to the holder: an enemy that wakes up would start a fight.
            enemy.Destroy();
            // The last hit was 1 tick ago; move the clock on to 59.5 s after it.
            blade.AgeChargeClock(3600 - 30 - 1);
            Trace(t, holder, blade);
            t.Check(blade.Charges == 3, "59.5 s after the last hit: still 3 (" + blade.Charges + ")");
            yield return 60;
            Trace(t, holder, blade);
            t.Check(blade.Charges == 2, "60.5 s after the last hit: 2 (" + blade.Charges + ")");
            enemy = DownedEnemy(t, t.center + IntVec3.East);
            yield return 5;
            if (!Strike(t, holder, enemy)) yield break;
            yield return 1;
            t.Check(blade.Charges == 3, "a hit adds one again: 3 (" + blade.Charges + ")");
            enemy.Destroy();
            blade.AgeChargeClock(3600 + 30 - 1);
            t.Check(blade.Charges == 2, "60.5 s after that hit: 2 (" + blade.Charges + ")");
        }

        [RimArtTest("Samehada", "feed 3 a mechanoid gives nothing")]
        private static IEnumerable<int> FeedMech(RimArtTestContext t)
        {
            t.Clear();
            // Undrafted and waiting, so it does not attack the mechanoid on its own.
            Pawn holder = CastHoldTest.Caster(t, SamehadaDefOf.AG_Samehada);
            CompSamehada blade = CompSamehada.HeldBy(holder);
            Wound(holder);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther") ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Lancer");
            if (!t.Check(kind != null, "a mechanoid kind exists")) yield break;
            Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfMechanoids));
            GenSpawn.Spawn(mech, t.center + IntVec3.East, t.map);
            // A stunned mechanoid cannot fight back.
            mech.stances.stunner.StunFor(1200, null, false);
            yield return 5;
            float before = InjurySeverity(holder);
            // Counted from before the holder can do anything, in case it swings at the mechanoid on its own.
            int landed = Patch_Samehada_Feed.LandedHits;
            for (int i = 0; i < 60 && holder.stances.FullBodyBusy; i++) yield return 5;
            t.Log("stance before the strike: " + RimArtTestContext.Describe(holder) + ", pain " + holder.health.hediffSet.PainTotal.ToString("0.00")
                + " of " + holder.GetStatValue(StatDefOf.PainShockThreshold).ToString("0.00") + ", landed so far " + (Patch_Samehada_Feed.LandedHits - landed));
            if (!t.Check(Strike(t, holder, mech), "the strike started")) yield break;
            yield return 1;
            Trace(t, holder, blade, mech);
            t.Check(Patch_Samehada_Feed.LandedHits > landed, "the blade's hits landed on the mechanoid (" + (Patch_Samehada_Feed.LandedHits - landed) + ")");
            t.Check(blade.Charges == 0, "no charge from a mechanoid (" + blade.Charges + ")");
            t.Check(SamehadaFeeding.Stacks(mech) == 0, "the mechanoid is not Drained");
            // Natural healing may take a tenth off while it waits for its stance; Feed would take 4.
            t.Check(before - InjurySeverity(holder) < 1f, "the holder is not healed by Feed (" + before.ToString("0.0") + " -> " + InjurySeverity(holder).ToString("0.0") + ")");
            mech.Destroy();
        }

        [RimArtTest("Samehada", "shark skin 1 spends 2 and sweeps the 3 cells in front, hostiles only")]
        private static IEnumerable<int> SharkSkinSweep(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 1, out CompSamehada blade);
            yield return 5;
            Ability shark = holder.abilities.GetAbility(SamehadaDefOf.AG_Samehada_SharkSkin);
            t.Check(!shark.CanCast && shark.GizmoDisabled(out string reason) && reason.StartsWith("Needs 2"), "Shark Skin is refused at 1 charge");
            blade.SetCharges(3);
            yield return 2;
            foreach (int step in Cast(t, holder, blade, SamehadaDefOf.AG_Samehada_SharkSkin, "shark-tear")) yield return step;
            t.Check(blade.Charges == 1, "Shark Skin spent 2: 1 left (" + blade.Charges + ")");
            t.Check(blade.SharkSkinActive && blade.SharkSkinTicksLeft > 500, "Shark Skin is on (" + blade.SharkSkinTicksLeft + " ticks left)");
            t.Check(shark.CooldownTicksRemaining > 1700 && shark.CooldownTicksRemaining <= 1800, "cooldown 30 s (" + shark.CooldownTicksRemaining + " ticks)");

            Pawn front = DownedEnemy(t, t.center + IntVec3.East);
            Pawn left = DownedEnemy(t, t.center + IntVec3.East + IntVec3.North);
            Pawn right = DownedEnemy(t, t.center + IntVec3.East + IntVec3.South);
            Pawn behind = DownedEnemy(t, t.center + IntVec3.West);
            Pawn ally = t.Colonist(t.center + IntVec3.North);
            ally.drafter.Drafted = false;
            RimArtTestContext.Hold(ally);
            // Generated colonists come with old scars: compare against what it had.
            float allyHurt = InjurySeverity(ally);
            yield return 3;
            if (!Strike(t, holder, front)) yield break;
            yield return 1;
            Trace(t, holder, blade, front, left, right, behind, ally);
            t.Check(SamehadaFeeding.Stacks(front) == 1 && SamehadaFeeding.Stacks(left) == 1 && SamehadaFeeding.Stacks(right) == 1,
                "the attacked cell and both beside it were hit and drained");
            t.Check(SamehadaFeeding.Stacks(behind) == 0, "the enemy behind was not hit");
            t.Check(SamehadaFeeding.Stacks(ally) == 0 && InjurySeverity(ally) == allyHurt, "the ally beside the holder was not hit");
            t.Check(blade.Charges == 4, "three fed hits: 1 + 3 = 4 charges (" + blade.Charges + ")");
            yield return t.ShotAs("shark-sweep");
            yield return 190;

            // An ally inside the arc is not hit either.
            left.Destroy();
            Pawn allyInArc = t.Colonist(t.center + IntVec3.East + IntVec3.North);
            allyInArc.drafter.Drafted = false;
            RimArtTestContext.Hold(allyInArc);
            float inArcHurt = InjurySeverity(allyInArc);
            HealAll(front);
            HealAll(right);
            yield return 3;
            if (!Strike(t, holder, front)) yield break;
            yield return 1;
            Trace(t, holder, blade, front, right, allyInArc);
            t.Check(SamehadaFeeding.Stacks(right) == 2, "the hostile beside was hit again (" + SamehadaFeeding.Stacks(right) + ")");
            t.Check(SamehadaFeeding.Stacks(allyInArc) == 0 && InjurySeverity(allyInArc) == inArcHurt, "the ally in the arc was not hit (injuries " + inArcHurt.ToString("0.0") + " -> " + InjurySeverity(allyInArc).ToString("0.0") + ")");
            t.Check(blade.Charges == 5, "two more fed hits: 5 charges (" + blade.Charges + ")");

            yield return blade.SharkSkinTicksLeft + 5;
            t.Check(!blade.SharkSkinActive, "Shark Skin ends after 10 s");
            HealAll(front);
            HealAll(right);
            int stacksRight = SamehadaFeeding.Stacks(right);
            if (!Strike(t, holder, front)) yield break;
            yield return 1;
            t.Check(SamehadaFeeding.Stacks(right) == stacksRight, "after it ends the cell beside is not hit (" + SamehadaFeeding.Stacks(right) + ")");
        }

        [RimArtTest("Samehada", "fusion 1 needs 5, spends all, faster, regenerates, keeps the blade and feeding", 2400)]
        private static IEnumerable<int> Fusion(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 4, out CompSamehada blade);
            Pawn enemy = DownedEnemy(t, t.center + IntVec3.East);
            yield return 5;
            Ability fusion = holder.abilities.GetAbility(SamehadaDefOf.AG_Samehada_Fusion);
            t.Check(!fusion.CanCast && fusion.GizmoDisabled(out string reason) && reason.StartsWith("Needs 5"), "Fusion is refused at 4 charges");
            blade.SetCharges(5);
            float speed = holder.GetStatValue(StatDefOf.MoveSpeed, true, 0);
            yield return 2;
            foreach (int step in Cast(t, holder, blade, SamehadaDefOf.AG_Samehada_Fusion, "fusion-merge")) yield return step;
            Hediff fused = holder.health.hediffSet.GetFirstHediffOfDef(SamehadaDefOf.AG_SamehadaFused);
            t.Check(fused != null, "the holder is fused");
            t.Check(blade.Charges == 0, "Fusion spent every charge (" + blade.Charges + ")");
            t.Check(holder.equipment.Primary?.def == SamehadaDefOf.AG_Samehada, "the blade is still in hand");
            t.Check(fusion.CooldownTicksRemaining > 2300 && fusion.CooldownTicksRemaining <= 2400, "cooldown 40 s (" + fusion.CooldownTicksRemaining + ")");
            float fast = holder.GetStatValue(StatDefOf.MoveSpeed, true, 0);
            t.Check(UnityEngine.Mathf.Abs(fast / speed - 1.3f) < 0.02f, "move speed x1.3 (" + speed.ToString("0.00") + " -> " + fast.ToString("0.00") + ")");

            Wound(holder);
            float wounded = InjurySeverity(holder);
            yield return 180;
            float regen = wounded - InjurySeverity(holder);
            t.Check(regen >= 5.5f && regen <= 6.5f, "3 s of regeneration healed 6 (" + regen.ToString("0.0") + ")");
            yield return t.ShotAs("fusion-fused");

            if (!Strike(t, holder, enemy)) yield break;
            yield return 1;
            Trace(t, holder, blade, enemy);
            t.Check(blade.Charges == 1 && SamehadaFeeding.Stacks(enemy) == 1, "Feed works while fused: 1 charge, 1 stack");

            int left = fused.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? 0;
            t.Log("fusion ticks left " + left + " (15 s = 900 from the cast)");
            // The speed just before and just after the end: the regeneration has changed the wounds since the
            // cast, so the comparison is across the end only.
            yield return left - 3;
            float lastFused = holder.GetStatValue(StatDefOf.MoveSpeed, true, 0);
            yield return 8;
            t.Check(holder.health.hediffSet.GetFirstHediffOfDef(SamehadaDefOf.AG_SamehadaFused) == null, "the fusion has ended");
            t.Check(blade.Charges == 0, "the blade is at no charge when it ends (" + blade.Charges + ")");
            float after = holder.GetStatValue(StatDefOf.MoveSpeed, true, 0);
            t.Check(UnityEngine.Mathf.Abs(lastFused / after - 1.3f) < 0.03f, "the x1.3 is gone (" + speed.ToString("0.00") + " before, " + fast.ToString("0.00")
                + " fused, " + lastFused.ToString("0.00") + " at the end, " + after.ToString("0.00") + " after)");
        }

        [RimArtTest("Samehada", "hold shark skin")]
        private static IEnumerable<int> HoldSharkSkin(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, SamehadaDefOf.AG_Samehada);
            CompSamehada.HeldBy(caster).SetCharges(2);
            MapComponent_Samehada samehada = t.map.GetComponent<MapComponent_Samehada>();
            foreach (int step in CastHoldTest.Run(t, caster, SamehadaDefOf.AG_Samehada_SharkSkin, caster,
                () => samehada.Fired(caster), CastHoldTest.Asking(() => samehada.Holds(caster)))) yield return step;
        }

        [RimArtTest("Samehada", "hold fusion", 2400)]
        private static IEnumerable<int> HoldFusion(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, SamehadaDefOf.AG_Samehada);
            CompSamehada.HeldBy(caster).SetCharges(5);
            MapComponent_Samehada samehada = t.map.GetComponent<MapComponent_Samehada>();
            foreach (int step in CastHoldTest.Run(t, caster, SamehadaDefOf.AG_Samehada_Fusion, caster,
                () => samehada.Fired(caster), CastHoldTest.Asking(() => samehada.Holds(caster)))) yield return step;
        }

        /// <summary>A drafted colonist holding Samehada at <paramref name="charges"/>, standing still facing <paramref name="facing"/>.</summary>
        private static Pawn Posed(RimArtTestContext t, IntVec3 at, Rot4 facing, int charges)
        {
            Pawn pawn = t.Colonist(at);
            t.Equip(pawn, SamehadaDefOf.AG_Samehada);
            CompSamehada.HeldBy(pawn).SetCharges(charges);
            Job wait = JobMaker.MakeJob(JobDefOf.Wait, 6000);
            wait.overrideFacing = facing;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            pawn.Rotation = facing;
            return pawn;
        }

        private static readonly Rot4[] Facings = { Rot4.North, Rot4.East, Rot4.South, Rot4.West };

        [RimArtTest("Samehada", "pictures 1 held blade in 4 facings at 0 and 5 charges")]
        private static IEnumerable<int> HeldPictures(RimArtTestContext t)
        {
            t.Clear();
            var pawns = new List<Pawn>();
            for (int i = 0; i < 4; i++)
            {
                pawns.Add(Posed(t, t.center + new IntVec3(-6 + i * 4, 0, 2), Facings[i], 0));
                pawns.Add(Posed(t, t.center + new IntVec3(-6 + i * 4, 0, -2), Facings[i], 5));
            }
            yield return 20;
            foreach (Pawn p in pawns) t.Log(RimArtTestContext.Describe(p) + " facing " + p.Rotation.ToStringHuman() + " charges " + CompSamehada.HeldBy(p).Charges);
            t.Check(pawns.All(p => p.Rotation == Facings[pawns.IndexOf(p) / 2]), "each pawn faces its way (north, east, south, west from the left)");
            yield return t.ShotAs("held-top0-bottom5-N-E-S-W");
        }

        [RimArtTest("Samehada", "pictures 2 fused in 4 facings")]
        private static IEnumerable<int> FusedPictures(RimArtTestContext t)
        {
            t.Clear();
            var pawns = new List<Pawn>();
            MapComponent_Samehada samehada = t.map.GetComponent<MapComponent_Samehada>();
            for (int i = 0; i < 4; i++)
            {
                Pawn p = Posed(t, t.center + new IntVec3(-6 + i * 4, 0, 0), Facings[i], 2);
                Hediff fused = p.health.AddHediff(SamehadaDefOf.AG_SamehadaFused);
                fused.TryGetComp<HediffComp_Disappears>()?.SetDuration(900);
                samehada.Fused(p);
                pawns.Add(p);
            }
            yield return 90;
            t.Check(pawns.All(p => p.health.hediffSet.HasHediff(SamehadaDefOf.AG_SamehadaFused)), "all four are fused");
            t.Check(pawns.All(p => p.Rotation == Facings[pawns.IndexOf(p)]), "each pawn faces its way (north, east, south, west from the left)");
            yield return t.ShotAs("fused-N-E-S-W");
        }
    }
}
