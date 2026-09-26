using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>Game tests for the Vacuum kit (run with -quicktest -rimarttest=vacuum).</summary>
    public static class Tests_Vacuum
    {
        private static Pawn Holder(RimArtTestContext t, out CompVacuum vacuum, bool drafted = true)
        {
            Pawn holder = t.Colonist(t.center);
            t.Equip(holder, VacuumDefOf.AG_Vacuum);
            holder.drafter.Drafted = drafted;
            vacuum = CompVacuum.HeldBy(holder);
            return holder;
        }

        private static Thing Make(string def, int count = 1)
        {
            Thing thing = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(def), GenStuff.DefaultStuffFor(DefDatabase<ThingDef>.GetNamed(def)));
            thing.stackCount = Mathf.Min(count, thing.def.stackLimit);
            return thing;
        }

        private static Thing Place(RimArtTestContext t, string def, IntVec3 cell, int count = 1)
        {
            Thing thing = Make(def, count);
            GenSpawn.Spawn(thing, cell, t.map);
            return thing;
        }

        /// <summary>Puts a thing straight into the mouth (what was there goes down to the stomach).</summary>
        private static Thing Feed(CompVacuum vacuum, string def, int count = 1)
        {
            Thing thing = Make(def, count);
            vacuum.Swallow(thing, CompVacuum.KgOf(thing));
            vacuum.Sync();
            return thing;
        }

        private static string State(CompVacuum vacuum, Pawn holder)
        {
            Hediff_VacuumFull load = VacuumLoad.Of(holder);
            return vacuum.ContentsLine() + ", fullness " + vacuum.Fullness.ToString("0.##") + ", hediff " + (load == null ? "none" : load.Severity.ToString("0.##") + " kg x" + load.MoveFactor.ToString("0.###"));
        }

        /// <summary>
        /// Injuries and missing parts the pawn has now that were not in <paramref name="before"/>. A hit that
        /// takes off a limb leaves no injury: the missing part replaces the injuries on it.
        /// </summary>
        private static int NewWounds(Pawn pawn, HashSet<Hediff> before) =>
            pawn.health.hediffSet.hediffs.Count(h => (h is Hediff_Injury || h is Hediff_MissingPart) && !before.Contains(h));

        private static string Hediffs(Pawn pawn)
        {
            var list = pawn.health.hediffSet.hediffs;
            if (list.Count == 0) return "none";
            return string.Join(", ", list.Select(h => h.def.defName + (h.Part != null ? " on " + h.Part.Label : "") + " " + h.Severity.ToString("0.#")));
        }

        /// <summary>Casts and traces until the cast job ends (at most <paramref name="most"/> ticks).</summary>
        private static IEnumerable<int> Cast(RimArtTestContext t, Pawn holder, CompVacuum vacuum, AbilityDef def, LocalTargetInfo target, Pawn other, string shot, int most = 300)
        {
            Ability ability = holder.abilities.GetAbility(def);
            if (!t.Check(ability != null, "the holder has " + def.label)) yield break;
            if (!t.Check(ability.CanCast, def.label + " can be cast (" + ability.CanCast.Reason + ")")) yield break;
            if (!t.Check(ability.CanApplyOn(target), def.label + " can be applied on " + target)) yield break;
            ability.QueueCastingJob(target, LocalTargetInfo.Invalid);
            int cast = t.Now;
            JobDef job = def.jobDef;
            t.Check(holder.CurJobDef == job, "the cast job started (job " + holder.CurJobDef?.defName + ")");
            int ended = -1;
            for (int i = 0; i * 3 < most; i++)
            {
                if (i % 2 == 0)
                    t.Log((t.Now - cast) + " | " + RimArtTestContext.Describe(holder) + " | " + State(vacuum, holder)
                        + (other != null ? " | " + RimArtTestContext.Describe(other) + " primary=" + (other.equipment?.Primary?.LabelShort ?? "none") : ""));
                if (holder.CurJobDef != job)
                {
                    ended = t.Now - cast;
                    break;
                }
                if (i == 12) yield return t.ShotAs(shot);
                yield return 3;
            }
            t.Check(ended >= 0, "the cast job ended (after " + ended + " ticks)");
        }

        // ------------------------------------------------------------------ the cast job holds

        private static IEnumerable<int> Hold(RimArtTestContext t, AbilityDef def, System.Func<CompVacuum, Pawn, LocalTargetInfo> setUp)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, VacuumDefOf.AG_Vacuum);
            CompVacuum vacuum = CompVacuum.HeldBy(caster);
            LocalTargetInfo target = setUp(vacuum, caster);
            var vacuums = t.map.GetComponent<MapComponent_Vacuum>();
            foreach (int step in CastHoldTest.Run(t, caster, def, target, () => vacuums.Fired(caster), CastHoldTest.Asking(() => vacuums.Holds(caster)), 400)) yield return step;
            t.Log("after: " + State(vacuum, caster));
        }

        [RimArtTest("Vacuum", "hold 1 suck holds an undrafted caster until the canister is down")]
        private static IEnumerable<int> HoldSuck(RimArtTestContext t) => Hold(t, VacuumDefOf.AG_Vacuum_Suck, (v, c) =>
        {
            IntVec3 at = t.center + new IntVec3(5, 0, 0);
            Place(t, "Steel", at, 20);
            return at;
        });

        [RimArtTest("Vacuum", "hold 2 spit holds an undrafted caster until the canister is down")]
        private static IEnumerable<int> HoldSpit(RimArtTestContext t) => Hold(t, VacuumDefOf.AG_Vacuum_Spit, (v, c) =>
        {
            Feed(v, "Steel", 20);
            return t.Enemy(t.center + new IntVec3(6, 0, 0), armed: false);
        });

        [RimArtTest("Vacuum", "hold 3 digest holds an undrafted caster until the canister is down")]
        private static IEnumerable<int> HoldDigest(RimArtTestContext t) => Hold(t, VacuumDefOf.AG_Vacuum_Digest, (v, c) =>
        {
            v.SetStomach(20f);
            v.Sync();
            return c;
        });

        // ------------------------------------------------------------------ rules

        [RimArtTest("Vacuum", "suck 1 takes loose things; the heaviest ends in the mouth")]
        private static IEnumerable<int> SuckLoose(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, out CompVacuum vacuum);
            Thing old = Feed(vacuum, "WoodLog", 10);
            float before = vacuum.Fullness;
            IntVec3 at = t.center + new IntVec3(5, 0, 0);
            var things = new List<Thing>
            {
                Place(t, "Steel", at, 75),
                Place(t, "ChunkGranite", at + IntVec3.North),
                Place(t, "Gun_BoltActionRifle", at + IntVec3.South),
                Place(t, "Silver", at + IntVec3.West, 100),
            };
            FilthMaker.TryMakeFilth(at + IntVec3.East, t.map, ThingDefOf.Filth_Blood, 2);
            Thing filth = (at + IntVec3.East).GetThingList(t.map).FirstOrDefault(x => x is Filth);
            Thing far = Place(t, "Steel", at + new IntVec3(0, 0, 4), 10);
            yield return 2;
            float[] kg = things.Select(CompVacuum.KgOf).ToArray();
            for (int i = 0; i < things.Count; i++) t.Log(things[i].LabelCap + ": " + kg[i].ToString("0.##") + " kg");
            Thing heaviest = things[System.Array.IndexOf(kg, kg.Max())];
            float sum = kg.Sum();
            t.Log("in the mouth before: " + old.LabelCap + " " + before.ToString("0.##") + " kg; the heaviest now: " + heaviest.LabelCap);
            foreach (int step in Cast(t, holder, vacuum, VacuumDefOf.AG_Vacuum_Suck, at, null, "suck-loose")) yield return step;
            yield return 5;
            t.Log("after: " + State(vacuum, holder));
            t.Check(things.All(x => !x.Spawned), "every loose thing in the radius left the ground");
            t.Check(filth != null && filth.Destroyed, "the filth is gone");
            t.Check(far.Spawned, "the steel 4 cells away (outside the radius) stayed");
            t.Check(vacuum.Mouth == heaviest, "the heaviest (" + heaviest.LabelCap + ") is in the mouth (" + (vacuum.Mouth?.LabelCap ?? "empty") + ")");
            t.Check(old.Destroyed, "the old mouth thing went down to the stomach and is destroyed");
            t.Check(things.Where(x => x != heaviest).All(x => x.Destroyed), "the lighter things are destroyed (in the stomach)");
            t.Check(Mathf.Abs(vacuum.StomachKg - (before + sum - kg.Max())) < 0.05f,
                "stomach " + vacuum.StomachKg.ToString("0.##") + " = old " + before.ToString("0.##") + " + lighter " + (sum - kg.Max()).ToString("0.##"));
            t.Check(Mathf.Abs(vacuum.Fullness - (before + sum)) < 0.05f, "fullness " + vacuum.Fullness.ToString("0.##") + " = " + (before + sum).ToString("0.##"));
            Hediff_VacuumFull load = VacuumLoad.Of(holder);
            t.Check(load != null && Mathf.Abs(load.Severity - vacuum.Fullness) < 0.05f, "the holder's load hediff is the fullness");
            Ability spit = holder.abilities.GetAbility(VacuumDefOf.AG_Vacuum_Spit);
            t.Check(spit.CooldownTicksRemaining > 0, "Spit is on the shared cooldown (" + spit.CooldownTicksRemaining + " ticks)");
        }

        [RimArtTest("Vacuum", "suck 2 disarms only the nearest hostile")]
        private static IEnumerable<int> SuckDisarm(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, out CompVacuum vacuum);
            IntVec3 at = t.center + new IntVec3(5, 0, 0);
            Pawn near = t.Enemy(at);
            Pawn other = t.Enemy(at + new IntVec3(1, 0, 1));
            Pawn ally = t.Colonist(at + new IntVec3(0, 0, -1));
            foreach (Pawn p in new[] { near, other, ally })
                if (p.equipment.Primary == null) t.Equip(p, DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));
            ally.drafter.Drafted = false;
            RimArtTestContext.Hold(ally);
            yield return 2;
            Thing nearWeapon = near.equipment.Primary, otherWeapon = other.equipment.Primary, allyWeapon = ally.equipment.Primary;
            t.Log("near " + near.LabelShort + " holds " + nearWeapon?.LabelShort + ", other " + other.LabelShort + " holds " + otherWeapon?.LabelShort + ", ally holds " + allyWeapon?.LabelShort);
            foreach (int step in Cast(t, holder, vacuum, VacuumDefOf.AG_Vacuum_Suck, at, near, "suck-disarm")) yield return step;
            yield return 5;
            t.Log("after: " + State(vacuum, holder));
            t.Check(near.equipment?.Primary == null, "the nearest hostile lost its weapon (" + (near.equipment?.Primary?.LabelShort ?? "none") + ")");
            t.Check(vacuum.Mouth == nearWeapon, "its weapon is in the mouth (" + (vacuum.Mouth?.LabelShort ?? "empty") + ")");
            t.Check(other.equipment?.Primary == otherWeapon, "the second hostile keeps its weapon");
            t.Check(ally.equipment?.Primary == allyWeapon, "the ally keeps its weapon");
            t.Check(near.Spawned && other.Spawned && ally.Spawned, "no pawn was taken");
        }

        [RimArtTest("Vacuum", "suck 3 leaves what would go over capacity")]
        private static IEnumerable<int> SuckCapacity(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, out CompVacuum vacuum);
            vacuum.SetStomach(80f);
            vacuum.Sync();
            IntVec3 at = t.center + new IntVec3(5, 0, 0);
            Thing steel = Place(t, "Steel", at, 75);
            Thing rifle = Place(t, "Gun_BoltActionRifle", at + IntVec3.North);
            yield return 2;
            t.Log("steel " + CompVacuum.KgOf(steel).ToString("0.##") + " kg, rifle " + CompVacuum.KgOf(rifle).ToString("0.##") + " kg, inside 80 of " + vacuum.Props.capacityKg);
            foreach (int step in Cast(t, holder, vacuum, VacuumDefOf.AG_Vacuum_Suck, at, null, "suck-capacity")) yield return step;
            yield return 5;
            t.Log("after: " + State(vacuum, holder));
            t.Check(steel.Spawned && steel.Position == at, "the 37.5 kg steel stays on the ground");
            t.Check(vacuum.Mouth == rifle, "the rifle, which fits, is in the mouth");
            t.Check(vacuum.Fullness <= vacuum.Props.capacityKg + 0.01f, "fullness " + vacuum.Fullness.ToString("0.##") + " is within capacity");
        }

        [RimArtTest("Vacuum", "suck 4 refused when full")]
        private static IEnumerable<int> SuckFull(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, out CompVacuum vacuum);
            vacuum.SetStomach(vacuum.Props.capacityKg);
            vacuum.Sync();
            Place(t, "Steel", t.center + new IntVec3(5, 0, 0), 10);
            yield return 2;
            Ability suck = holder.abilities.GetAbility(VacuumDefOf.AG_Vacuum_Suck);
            t.Check(!suck.CanCast, "Suck cannot be cast at " + vacuum.Fullness + " kg");
            t.Check(suck.GizmoDisabled(out string reason) && reason.StartsWith("Full"), "the gizmo says full (" + reason + ")");
            Ability spit = holder.abilities.GetAbility(VacuumDefOf.AG_Vacuum_Spit);
            t.Check(spit.GizmoDisabled(out string spitReason) && spitReason.StartsWith("Nothing in the mouth"), "Spit is greyed with an empty mouth (" + spitReason + ")");
        }

        [RimArtTest("Vacuum", "spit 1 lands the mouth thing and hits a pawn for at most 40")]
        private static IEnumerable<int> SpitHit(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, out CompVacuum vacuum);
            vacuum.SetStomach(10f);
            Thing steel = Feed(vacuum, "Steel", 75);
            IntVec3 at = t.center + new IntVec3(6, 0, 0);
            Pawn enemy = t.Enemy(at, armed: false);
            // No apparel: with armour penetration 0, any worn armour can deflect the hit to 0 at random.
            enemy.apparel?.DestroyAll();
            yield return 2;
            var before = new HashSet<Hediff>(enemy.health.hediffSet.hediffs);
            float kg = vacuum.MouthKg;
            var vacuums = t.map.GetComponent<MapComponent_Vacuum>();
            t.Log("mouth " + steel.LabelCap + " " + kg.ToString("0.##") + " kg -> " + (kg * 1.5f).ToString("0.#") + " before the cap");
            t.Log("enemy hediffs before: " + Hediffs(enemy));
            bool stunned = false;
            Ability spit = holder.abilities.GetAbility(VacuumDefOf.AG_Vacuum_Spit);
            if (!t.Check(spit.CanCast, "Spit can be cast")) yield break;
            spit.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            int cast = t.Now;
            for (int i = 0; i < 100; i++)
            {
                if (enemy.stances?.stunner?.Stunned == true) stunned = true;
                if (i % 4 == 0) t.Log((t.Now - cast) + " | " + RimArtTestContext.Describe(holder) + " | " + State(vacuum, holder) + " | " + RimArtTestContext.Describe(enemy));
                if (holder.CurJobDef != VacuumDefOf.AG_CastVacuum && i > 2) break;
                if (i == 20) yield return t.ShotAs("spit-hit");
                yield return 3;
            }
            yield return 5;
            t.Log("after: " + State(vacuum, holder) + "; hit " + vacuums.lastSpitVictim?.LabelShort + " for " + vacuums.lastSpitDamage);
            t.Log("enemy hediffs after: " + Hediffs(enemy));
            t.Check(vacuums.lastSpitVictim == enemy, "the pawn on the cell was hit");
            t.Check(Mathf.Abs(vacuums.lastSpitDamage - 40f) < 0.01f, "the damage is capped at 40 (" + vacuums.lastSpitDamage + ")");
            t.Check(enemy.Dead || NewWounds(enemy, before) > 0, "the pawn is injured (" + (enemy.Dead ? "dead" : NewWounds(enemy, before) + " new injuries or lost parts") + ")");
            t.Check(stunned || enemy.Dead || enemy.Downed, "the pawn was dazed (stunned)");
            t.Check(steel.Spawned && steel.Position.DistanceTo(at) <= 2.5f, "the steel landed near the cell (" + (steel.Spawned ? steel.Position.ToString() : "not spawned") + ", " + steel.stackCount + ")");
            t.Check(vacuum.Mouth == null, "the mouth is empty");
            t.Check(Mathf.Abs(vacuum.StomachKg - 10f) < 0.01f, "the stomach keeps its 10 kg (" + vacuum.StomachKg + ")");
            Ability suck = holder.abilities.GetAbility(VacuumDefOf.AG_Vacuum_Suck);
            t.Check(suck.CooldownTicksRemaining > 0, "Suck is on the shared cooldown (" + suck.CooldownTicksRemaining + " ticks)");
        }

        [RimArtTest("Vacuum", "digest 1 empties the vacuum")]
        private static IEnumerable<int> DigestAll(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, out CompVacuum vacuum);
            vacuum.SetStomach(30f);
            Thing chunk = Feed(vacuum, "ChunkGranite");
            yield return 2;
            float kg = vacuum.Fullness;
            t.Log("inside " + kg.ToString("0.##") + " kg; chew " + (kg * 0.05f).ToString("0.##") + " s");
            foreach (int step in Cast(t, holder, vacuum, VacuumDefOf.AG_Vacuum_Digest, holder, null, "digest", 600)) yield return step;
            t.Log("after: " + State(vacuum, holder));
            t.Check(vacuum.Fullness < 0.001f, "fullness is 0 (" + vacuum.Fullness + ")");
            t.Check(chunk.Destroyed && vacuum.Mouth == null, "the mouth's chunk is destroyed");
            t.Check(VacuumLoad.Of(holder) == null, "the load hediff is gone");
        }

        [RimArtTest("Vacuum", "digest 2 interrupted keeps what is left")]
        private static IEnumerable<int> DigestInterrupted(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, out CompVacuum vacuum);
            vacuum.SetStomach(100f);
            vacuum.Sync();
            yield return 2;
            Ability digest = holder.abilities.GetAbility(VacuumDefOf.AG_Vacuum_Digest);
            digest.QueueCastingJob(holder, LocalTargetInfo.Invalid);
            yield return 120;
            t.Log("at 2 s: " + State(vacuum, holder) + " | " + RimArtTestContext.Describe(holder));
            holder.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, t.center + new IntVec3(-3, 0, 0)), JobTag.DraftedOrder);
            yield return 10;
            float left = vacuum.Fullness;
            t.Log("after the order: " + State(vacuum, holder) + " | " + RimArtTestContext.Describe(holder));
            t.Check(holder.CurJobDef != VacuumDefOf.AG_CastVacuumDigest, "the player's order ended the digest");
            t.Check(left > 40f && left < 90f, "part of it was chewed and the rest stays (" + left.ToString("0.#") + " kg)");
            yield return 120;
            t.Check(Mathf.Abs(vacuum.Fullness - left) < 0.01f, "nothing more is chewed after the order (" + vacuum.Fullness.ToString("0.#") + ")");
        }

        [RimArtTest("Vacuum", "load hediff slows by 0.4 % per kg")]
        private static IEnumerable<int> Slow(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, out CompVacuum vacuum);
            yield return 2;
            float free = holder.GetStatValue(StatDefOf.MoveSpeed);
            t.Check(VacuumLoad.Of(holder) == null, "no load hediff when empty");
            vacuum.SetStomach(50f);
            vacuum.Sync();
            yield return 2;
            float loaded = holder.GetStatValue(StatDefOf.MoveSpeed);
            Hediff_VacuumFull load = VacuumLoad.Of(holder);
            t.Log("move speed " + free.ToString("0.###") + " empty, " + loaded.ToString("0.###") + " at 50 kg; " + State(vacuum, holder) + "; label " + load?.LabelCap);
            t.Check(load != null && Mathf.Abs(load.Severity - 50f) < 0.01f, "the hediff's severity is 50 kg");
            t.Check(Mathf.Abs(loaded / free - 0.8f) < 0.01f, "move speed is x0.8 at 50 kg (" + (loaded / free).ToString("0.###") + ")");
            vacuum.SetStomach(100f);
            vacuum.Sync();
            yield return 2;
            t.Check(Mathf.Abs(holder.GetStatValue(StatDefOf.MoveSpeed) / free - 0.6f) < 0.01f, "move speed is x0.6 at 100 kg (" + (holder.GetStatValue(StatDefOf.MoveSpeed) / free).ToString("0.###") + ")");
        }

        [RimArtTest("Vacuum", "handing it over keeps the contents and moves the hediff")]
        private static IEnumerable<int> HandOver(RimArtTestContext t)
        {
            t.Clear();
            Pawn a = Holder(t, out CompVacuum vacuum);
            Pawn b = t.Colonist(t.center + new IntVec3(0, 0, 3));
            vacuum.SetStomach(20f);
            Thing chunk = Feed(vacuum, "ChunkGranite");
            float kg = vacuum.Fullness;
            yield return 2;
            t.Log("A: " + State(vacuum, a));
            ThingWithComps weapon = a.equipment.Primary;
            t.Check(a.equipment.TryDropEquipment(weapon, out ThingWithComps dropped, a.Position, false), "A drops the vacuum");
            yield return 2;
            t.Check(VacuumLoad.Of(a) == null, "A's load hediff is gone");
            t.Check(vacuum.Mouth == chunk && Mathf.Abs(vacuum.Fullness - kg) < 0.01f, "the dropped vacuum keeps its contents (" + vacuum.ContentsLine() + ")");
            dropped.DeSpawn();
            b.equipment.AddEquipment(dropped);
            yield return 2;
            t.Log("B: " + State(vacuum, b));
            Hediff_VacuumFull load = VacuumLoad.Of(b);
            t.Check(CompVacuum.HeldBy(b) == vacuum, "B holds the same vacuum");
            t.Check(load != null && Mathf.Abs(load.Severity - kg) < 0.01f, "B has the load hediff at " + kg.ToString("0.##") + " kg");
            t.Check(vacuum.Mouth == chunk, "the chunk is still in the mouth");
            t.Check(b.abilities.GetAbility(VacuumDefOf.AG_Vacuum_Spit) != null && a.abilities.GetAbility(VacuumDefOf.AG_Vacuum_Spit) == null, "the abilities moved from A to B");
        }
    }
}
