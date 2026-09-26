using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Game tests for the Frost Gun kit (run with -quicktest "-rimarttest=frost gun"). Bolts are
    /// launched straight at their target, so every one hits; one test fires the rifle's own verb. The
    /// targets are unarmed enemies with their clothes taken off, told to stand still.
    /// </summary>
    public static class Tests_FrostGun
    {
        private static Pawn Holder(RimArtTestContext t, IntVec3 at, out CompFrostGun gun)
        {
            Pawn holder = t.Colonist(at);
            // Drafted so it stays put, but it must not shoot on its own: a stray bolt shatters the ice.
            holder.drafter.FireAtWill = false;
            t.Equip(holder, FrostGunDefOf.AG_FrostGun);
            gun = CompFrostGun.HeldBy(holder);
            t.Log("holder " + RimArtTestContext.Describe(holder) + " primary " + holder.equipment.Primary?.def.defName
                + " verbs " + (holder.equipment.PrimaryEq?.AllVerbs?.Count ?? -1) + " coolant " + (gun?.Coolant ?? -1f)
                + " abilities " + (holder.abilities?.abilities == null ? "null" : string.Join(",", holder.abilities.abilities.Select(a => a.def.defName))));
            return holder;
        }

        private static Pawn Target(RimArtTestContext t, IntVec3 at)
        {
            Pawn enemy = t.Enemy(at, armed: false);
            enemy.apparel?.DestroyAll();
            // A wimp goes down from a little pain, and a downed pawn cannot be frozen.
            TraitDef wimpDef = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
            Trait wimp = wimpDef == null ? null : enemy.story?.traits?.GetTrait(wimpDef);
            if (wimp != null) enemy.story.traits.RemoveTrait(wimp);
            return enemy;
        }

        private static float Injuries(Pawn pawn) => pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);

        private static void HealAll(Pawn pawn)
        {
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList()) pawn.health.RemoveHediff(injury);
        }

        private static string State(Pawn pawn, MapComponent_FrostGun comp) =>
            RimArtTestContext.Describe(pawn) + " chilled " + FlashFreeze.ChilledStacks(pawn) + " soaked " + WaterGunSoak.IsSoaked(pawn)
            + " frozen " + comp.IsFrozen(pawn) + " (" + comp.FrozenTicksLeft(pawn) + " left) injuries " + Injuries(pawn).ToString("0.0");

        /// <summary>One bolt from the holder straight at the target: it always hits.</summary>
        private static void Bolt(Pawn holder, Pawn target)
        {
            var bolt = (Projectile)GenSpawn.Spawn(FrostGunDefOf.AG_FrostGun_Bolt, holder.Position, holder.Map);
            bolt.Launch(holder, holder.DrawPos, target, target, ProjectileHitFlags.IntendedTarget, false, holder.equipment.Primary);
        }

        [RimArtTest("Frost Gun", "shot 1 bolts add Chilled up to 3 stacks and slow the pawn")]
        private static IEnumerable<int> ShotStacks(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, t.center + new IntVec3(-3, 0, 0), out CompFrostGun gun);
            Pawn enemy = Target(t, t.center + new IntVec3(1, 0, 0));
            var comp = t.map.GetComponent<MapComponent_FrostGun>();
            yield return 5;
            if (!t.Check(gun != null, "the holder holds a frost gun")) yield break;
            t.Check(holder.abilities.GetAbility(FrostGunDefOf.AG_FrostGun_FlashFreeze) != null, "holding it grants Flash Freeze");
            float speed0 = enemy.GetStatValue(StatDefOf.MoveSpeed);
            float coolant0 = gun.Coolant;
            for (int shot = 1; shot <= 4; shot++)
            {
                HealAll(enemy);
                Bolt(holder, enemy);
                for (int i = 0; i < 20; i++)
                {
                    if (FlashFreeze.ChilledStacks(enemy) >= Mathf.Min(shot, 3) && i > 2) break;
                    yield return 1;
                }
                int stacks = FlashFreeze.ChilledStacks(enemy);
                // The bolt's own wound slows the pawn too; take it off to read what Chilled does.
                HealAll(enemy);
                float speed = enemy.GetStatValue(StatDefOf.MoveSpeed);
                int left = enemy.health.hediffSet.GetFirstHediffOfDef(FrostGunDefOf.AG_FrostGunChilled)?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? -1;
                t.Log(t.Now + " shot " + shot + ": " + State(enemy, comp) + " move " + speed.ToString("0.000") + " / " + speed0.ToString("0.000") + " chill left " + left);
                t.Check(stacks == Mathf.Min(shot, 3), "after " + shot + " hits the pawn has " + Mathf.Min(shot, 3) + " Chilled stacks (" + stacks + ")");
                t.Check(left > 540 && left <= 600, "each hit sets Chilled to last 10 s again (" + left + " ticks)");
                float want = 1f - 0.1f * Mathf.Min(shot, 3);
                t.Check(Mathf.Abs(speed / speed0 - want) < 0.02f, "move speed is " + want.ToString("0.0") + " of normal (" + (speed / speed0).ToString("0.000") + ")");
                if (shot == 3)
                {
                    yield return 20;
                    yield return t.ShotAs("frost-gun-chilled-3");
                }
            }
            t.Check(Mathf.Abs(gun.Coolant - coolant0) < 0.5f, "the shots used no coolant (" + coolant0.ToString("0.0") + " -> " + gun.Coolant.ToString("0.0") + ")");
        }

        [RimArtTest("Frost Gun", "shot 2 the rifle's own verb fires the frost bolt")]
        private static IEnumerable<int> ShotVerb(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, t.center + new IntVec3(-2, 0, 0), out CompFrostGun gun);
            Pawn enemy = Target(t, t.center + new IntVec3(1, 0, 0));
            var comp = t.map.GetComponent<MapComponent_FrostGun>();
            yield return 5;
            Verb verb = holder.equipment?.PrimaryEq?.PrimaryVerb;
            if (!t.Check(verb != null && verb.GetProjectile() == FrostGunDefOf.AG_FrostGun_Bolt, "the rifle's verb fires AG_FrostGun_Bolt (" + verb?.GetProjectile()?.defName + ")")) yield break;
            t.Check(verb.verbProps.range > 22f && verb.verbProps.range < 23f, "range 22 (" + verb.verbProps.range + ")");
            for (int attempt = 0; attempt < 8 && FlashFreeze.ChilledStacks(enemy) == 0; attempt++)
            {
                bool started = verb.TryStartCastOn(enemy);
                for (int i = 0; i < 150 && (holder.stances.curStance is Stance_Busy || i < 5); i++) yield return 1;
                t.Log(t.Now + " attempt " + attempt + " started " + started + ": " + State(enemy, comp));
            }
            t.Check(FlashFreeze.ChilledStacks(enemy) >= 1, "a shot from the verb landed and chilled the pawn");
        }

        [RimArtTest("Frost Gun", "freeze 1 refused on a dry unchilled pawn, allowed on a soaked or 3-chilled one, and without coolant")]
        private static IEnumerable<int> FreezeRefusal(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, t.center + new IntVec3(-3, 0, 0), out CompFrostGun gun);
            Pawn dry = Target(t, t.center + new IntVec3(1, 0, 2));
            Pawn wet = Target(t, t.center + new IntVec3(1, 0, 0));
            Pawn cold = Target(t, t.center + new IntVec3(1, 0, -2));
            yield return 5;
            Ability ability = holder.abilities.GetAbility(FrostGunDefOf.AG_FrostGun_FlashFreeze);
            var effect = ability?.CompOfType<CompAbilityEffect_FlashFreeze>();
            if (!t.Check(effect != null, "the holder has Flash Freeze")) yield break;
            // Ability.CanApplyOn reads a list that Ability.EffectComps fills on first use; until then it
            // accepts anything. In play the gizmo has read it long before a target is picked.
            t.Log("effect comps: " + ability.EffectComps.Count);

            string why = effect.Refusal(dry);
            t.Log("dry: " + why);
            t.Check(why != null && why.Contains("soaked"), "a dry, unchilled pawn has a refusal that names soaking");
            t.Check(!effect.Valid((LocalTargetInfo)dry), "Valid refuses the dry pawn");
            t.Check(!ability.CanApplyOn((LocalTargetInfo)dry), "CanApplyOn refuses the dry pawn");
            WaterGunSoak.Apply(wet, 60f);
            t.Check(effect.Valid((LocalTargetInfo)wet) && ability.CanApplyOn((LocalTargetInfo)wet), "a soaked pawn is allowed (" + effect.Refusal(wet) + ")");
            FlashFreeze.AddChill(cold, 3, 10f);
            FlashFreeze.AddChill(cold, 3, 10f);
            t.Check(!effect.Valid((LocalTargetInfo)cold), "2 Chilled stacks are refused (" + effect.Refusal(cold) + ")");
            FlashFreeze.AddChill(cold, 3, 10f);
            t.Check(effect.Valid((LocalTargetInfo)cold) && ability.CanApplyOn((LocalTargetInfo)cold), "3 Chilled stacks are allowed (" + effect.Refusal(cold) + ")");
            t.Check(!effect.Valid((LocalTargetInfo)holder), "the caster itself (dry) is refused");

            gun.SetCoolant(5f);
            bool disabled = effect.GizmoDisabled(out string reason);
            t.Log("at 5 coolant: CanCast " + (bool)ability.CanCast + ", gizmo disabled " + disabled + ": " + reason);
            t.Check(!ability.CanCast && disabled && reason != null && reason.Contains("coolant"), "at 5 coolant the ability is disabled with a reason");
            gun.SetCoolant(10f);
            t.Check(ability.CanCast, "at 10 coolant it can be cast (" + ability.CanCast.Reason + ")");
        }

        /// <summary>Casts Flash Freeze from a drafted holder at a soaked enemy and waits until the ice forms.</summary>
        private static IEnumerable<int> CastFreeze(RimArtTestContext t, Pawn holder, CompFrostGun gun, Pawn enemy, MapComponent_FrostGun comp)
        {
            Ability ability = holder.abilities.GetAbility(FrostGunDefOf.AG_FrostGun_FlashFreeze);
            if (!t.Check(ability != null && ability.CanCast, "Flash Freeze can be cast (" + ability?.CanCast.Reason + ")")) yield break;
            if (!t.Check(ability.CanApplyOn((LocalTargetInfo)enemy), "on the soaked enemy")) yield break;
            float before = gun.Coolant;
            ability.QueueCastingJob((LocalTargetInfo)enemy, LocalTargetInfo.Invalid);
            int cast = t.Now;
            t.Check(holder.CurJobDef == FrostGunDefOf.AG_CastFrostGun, "the cast job started (" + holder.CurJobDef?.defName + ")");
            for (int i = 0; i < 120 && !comp.IsFrozen(enemy); i++)
            {
                if (i % 6 == 0) t.Log((t.Now - cast) + " | " + RimArtTestContext.Describe(holder) + " | " + State(enemy, comp));
                yield return 1;
            }
            t.Log((t.Now - cast) + " frozen | " + RimArtTestContext.Describe(holder) + " | " + State(enemy, comp));
            t.Check(comp.IsFrozen(enemy) && FlashFreeze.IsFrozen(enemy), "the enemy is frozen " + (t.Now - cast) + " ticks after the order (warmup 48 + beam)");
            t.Check(Mathf.Abs(before - 10f - gun.Coolant) < 0.2f, "the cast spent 10 coolant (" + before.ToString("0.0") + " -> " + gun.Coolant.ToString("0.0") + ")");
            t.Check(!WaterGunSoak.IsSoaked(enemy), "the freeze used up Soaked");
        }

        [RimArtTest("Frost Gun", "freeze 2 a frozen pawn cannot move or act for 5 s, then thaws")]
        private static IEnumerable<int> FreezeHolds(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, t.center + new IntVec3(-4, 0, 0), out CompFrostGun gun);
            Pawn enemy = Target(t, t.center);
            var comp = t.map.GetComponent<MapComponent_FrostGun>();
            WaterGunSoak.Apply(enemy, 60f);
            yield return 5;
            foreach (int step in CastFreeze(t, holder, gun, enemy, comp)) yield return step;
            if (!comp.IsFrozen(enemy)) yield break;
            int frozenAt = t.Now, left = comp.FrozenTicksLeft(enemy);
            t.Check(left >= 298 && left <= 300, "it stays frozen 5 s (" + left + " ticks)");
            IntVec3 stands = enemy.Position, goal = enemy.Position + new IntVec3(0, 0, 4);
            enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, goal), JobCondition.InterruptForced);
            t.Log("ordered to walk to " + goal + ": " + State(enemy, comp));
            bool moved = false, loose = false;
            while (comp.IsFrozen(enemy) && t.Now - frozenAt < 400)
            {
                if (enemy.Position != stands) moved = true;
                if (!enemy.stances.stunner.Stunned) loose = true;
                if (t.Now - frozenAt == 40) yield return t.ShotAs("flash-freeze-ice");
                if ((t.Now - frozenAt) % 30 == 0) t.Log((t.Now - frozenAt) + " | " + State(enemy, comp));
                yield return 1;
            }
            int thawed = t.Now - frozenAt;
            t.Log(thawed + " thawed | " + State(enemy, comp));
            t.Check(!moved, "it did not move while frozen");
            t.Check(!loose, "it was stunned on every tick of the freeze");
            t.Check(thawed >= 298 && thawed <= 302, "the ice thawed after 5 s (" + thawed + " ticks)");
            yield return 3;
            t.Check(!FlashFreeze.IsFrozen(enemy) && !enemy.stances.stunner.Stunned, "after the thaw it is neither frozen nor stunned");
            if (enemy.CurJobDef != JobDefOf.Goto) enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, goal), JobCondition.InterruptForced);
            for (int i = 0; i < 180 && enemy.Position == stands; i++) yield return 1;
            t.Log("after the thaw: " + State(enemy, comp));
            t.Check(enemy.Position != stands, "it walks again after the thaw");
        }

        [RimArtTest("Frost Gun", "freeze 3 a hit while frozen shatters the ice for 15 more")]
        private static IEnumerable<int> FreezeShatter(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, t.center + new IntVec3(-4, 0, 0), out CompFrostGun gun);
            Pawn enemy = Target(t, t.center);
            var comp = t.map.GetComponent<MapComponent_FrostGun>();
            for (int i = 0; i < 3; i++) FlashFreeze.AddChill(enemy, 3, 10f);
            yield return 5;
            foreach (int step in CastFreeze(t, holder, gun, enemy, comp)) yield return step;
            if (!comp.IsFrozen(enemy)) yield break;
            t.Check(FlashFreeze.ChilledStacks(enemy) == 0, "the freeze used up the Chilled stacks");
            yield return 60;
            comp.lastShatter = null;
            comp.lastShatterDealt = -1f;
            float before = Injuries(enemy);
            var hit = new DamageInfo(DamageDefOf.Blunt, 4f, 0f, -1f, holder);
            float dealt = enemy.TakeDamage(hit).totalDamageDealt;
            t.Log(t.Now + " hit for 4 blunt (dealt " + dealt.ToString("0.0") + "): " + State(enemy, comp));
            yield return 2;
            t.Log(t.Now + " after: " + State(enemy, comp) + " shatter " + comp.lastShatter?.Def?.defName + " " + comp.lastShatter?.Amount + " dealt " + comp.lastShatterDealt.ToString("0.0"));
            t.Check(!comp.IsFrozen(enemy) && !FlashFreeze.IsFrozen(enemy), "the hit broke the ice");
            t.Check(!enemy.stances.stunner.Stunned, "the pawn is no longer stunned");
            t.Check(comp.lastShatter.HasValue && Mathf.Approximately(comp.lastShatter.Value.Amount, 15f), "the shatter dealt 15 more (" + comp.lastShatter?.Amount + ")");
            t.Check(comp.lastShatterDealt > 0f && Injuries(enemy) - before >= dealt + comp.lastShatterDealt - 0.5f,
                "the pawn took the hit and the shatter (" + before.ToString("0.0") + " -> " + Injuries(enemy).ToString("0.0") + ")");
            yield return 4;
            yield return t.ShotAs("flash-freeze-shatter");
            yield return 30;
            yield return t.ShotAs("flash-freeze-shards");
        }

        [RimArtTest("Frost Gun", "freeze 4 hold flash freeze holds an undrafted caster until the beam has landed")]
        private static IEnumerable<int> FreezeHold(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, FrostGunDefOf.AG_FrostGun);
            Pawn enemy = Target(t, t.center + new IntVec3(5, 0, 0));
            WaterGunSoak.Apply(enemy, 60f);
            var comp = t.map.GetComponent<MapComponent_FrostGun>();
            foreach (int step in CastHoldTest.Run(t, caster, FrostGunDefOf.AG_FrostGun_FlashFreeze, enemy,
                () => comp.Fired(caster), CastHoldTest.Asking(() => comp.Holds(caster)))) yield return step;
            t.Check(comp.IsFrozen(enemy), "the enemy was frozen");
        }

        /// <summary>A 3 x 3 roofed room walled round <paramref name="centre"/>, at <paramref name="celsius"/>.</summary>
        private static Room Room(RimArtTestContext t, IntVec3 centre, float celsius)
        {
            ThingDef stuff = GenStuff.DefaultStuffFor(ThingDefOf.Wall);
            foreach (IntVec3 c in CellRect.CenteredOn(centre, 2))
            {
                t.map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                if (Mathf.Abs(c.x - centre.x) == 2 || Mathf.Abs(c.z - centre.z) == 2)
                    GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, stuff), c, t.map);
            }
            Room room = centre.GetRoom(t.map);
            if (room != null) room.Temperature = celsius;
            return room;
        }

        [RimArtTest("Frost Gun", "coolant 1 refills slowly when warm and fast below 0 C")]
        private static IEnumerable<int> CoolantRefill(RimArtTestContext t)
        {
            t.Clear();
            IntVec3 warmAt = t.center + new IntVec3(-5, 0, 0), coldAt = t.center + new IntVec3(5, 0, 0);
            Room warm = Room(t, warmAt, 21f), cold = Room(t, coldAt, -15f);
            Pawn warmHolder = Holder(t, warmAt, out CompFrostGun warmGun);
            Pawn coldHolder = Holder(t, coldAt, out CompFrostGun coldGun);
            yield return 2;
            t.Log("warm room " + warm?.ID + " " + warmAt.GetTemperature(t.map).ToString("0.0") + " C, cold room " + cold?.ID + " " + coldAt.GetTemperature(t.map).ToString("0.0") + " C, outdoors " + t.map.mapTemperature.OutdoorTemp.ToString("0.0") + " C");
            t.Check(warmAt.GetTemperature(t.map) >= 0f && coldAt.GetTemperature(t.map) < 0f, "one holder stands above 0 C and one below");
            // Start on a refill tick boundary so both count the same number of refills.
            while (t.Now % 60 != 1) yield return 1;
            warmGun.SetCoolant(0f);
            coldGun.SetCoolant(0f);
            int start = t.Now;
            yield return 300;
            float seconds = (t.Now - start) / 60f;
            t.Log("after " + seconds.ToString("0.0") + " s: warm " + warmGun.Coolant.ToString("0.00") + " at " + warmAt.GetTemperature(t.map).ToString("0.0")
                + " C, cold " + coldGun.Coolant.ToString("0.00") + " at " + coldAt.GetTemperature(t.map).ToString("0.0") + " C");
            t.Check(Mathf.Abs(warmGun.Coolant - 0.5f) < 0.11f, "warm: 0.1 a second, 0.5 in 5 s (" + warmGun.Coolant.ToString("0.00") + ")");
            t.Check(Mathf.Abs(coldGun.Coolant - 10f) < 0.5f, "below 0 C: 2 a second, 10 in 5 s (" + coldGun.Coolant.ToString("0.00") + ")");
            coldGun.SetCoolant(29.5f);
            yield return 120;
            t.Check(Mathf.Abs(coldGun.Coolant - 30f) < 0.01f, "the tank stops at 30 (" + coldGun.Coolant.ToString("0.00") + ")");
        }
    }
}
