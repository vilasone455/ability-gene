using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>Game tests for the Last Prism (run with -quicktest -rimarttest=last: the filter is the start of "Last Prism: label"). The holder stands at the arena centre; east is +x.</summary>
    public static class Tests_LastPrism
    {
        private static GameComponent_LastPrism Prisms => GameComponent_LastPrism.Instance;

        /// <summary>A drafted colonist at the centre holding a prism with <paramref name="level"/> s of beam; no beams in the game.</summary>
        private static Pawn Holder(RimArtTestContext t, float level, out CompLastPrism prism)
        {
            t.Clear();
            Prisms.ResetForTests();
            Pawn holder = t.Colonist(t.center);
            NoWimp(holder);
            t.Equip(holder, LastPrismDefOf.AG_LastPrism);
            prism = CompLastPrism.HeldBy(holder);
            if (prism != null) prism.Level = level;
            return holder;
        }

        private static int Burns(Pawn pawn) => pawn.health.hediffSet.hediffs.FindAll(h => h.def == DamageDefOf.Burn.hediff).Count;

        private static string State(LastPrismCast cast, int now) =>
            cast == null ? "no beam" : !cast.Fired ? "warmup" : !cast.Firing ? (cast.dried ? "stopped (dry)" : "stopped") : cast.Joined(now) ? "joined" : "fan";

        /// <summary>One line: the holder, the charge, the beam's phase, aim and target, then each other pawn's state, health and burns.</summary>
        private static void Trace(RimArtTestContext t, Pawn holder, CompLastPrism prism, params Pawn[] others)
        {
            LastPrismCast cast = Prisms.Latest(holder);
            string line = t.Now + " | " + Describe(holder) + " charge " + (prism?.Level ?? -1f).ToString("0.00") + " | " + State(cast, t.Now);
            if (cast != null) line += " aim " + (cast.aim * Mathf.Rad2Deg).ToString("0") + " target " + (cast.target.HasThing ? cast.target.Thing.LabelShort : cast.target.Cell.ToString());
            foreach (Pawn other in others)
                if (other != null)
                    line += " | " + Describe(other) + " " + other.health.summaryHealth.SummaryHealthPercent.ToString("0%") + " burns " + Burns(other);
            t.Log(line);
        }

        /// <summary>Queues Fire at <paramref name="target"/> and waits for the beam to start (0.3 s warmup).</summary>
        private static IEnumerable<int> Fire(RimArtTestContext t, Pawn holder, LocalTargetInfo target)
        {
            Ability ability = holder.abilities.GetAbility(LastPrismDefOf.AG_LastPrism_Fire);
            if (!t.Check(ability != null, "the holder has Fire")) yield break;
            foreach (int step in WaitFor(() => Free(holder), 120)) yield return step;
            if (!t.Check(ability.CanCast, "Fire can be cast (" + ability.CanCast.Reason + ")")) yield break;
            ability.QueueCastingJob(target, LocalTargetInfo.Invalid);
            t.Check(holder.CurJobDef == LastPrismDefOf.AG_CastLastPrism, "the cast job started (job " + holder.CurJobDef?.defName + ")");
            foreach (int step in WaitFor(() => Prisms.FiringBy(holder) != null, 60)) yield return step;
            t.Check(Prisms.FiringBy(holder) != null, "the beam fired");
        }

        [RimArtTest("Last Prism", "fire 1 fan, join, downs two and stops")]
        private static IEnumerable<int> FanJoinDown(RimArtTestContext t)
        {
            Pawn holder = Holder(t, 12f, out CompLastPrism prism);
            Pawn near = t.Target(t.center + new IntVec3(7, 0, 0)), far = t.Target(t.center + new IntVec3(13, 0, 1));
            yield return 2;
            foreach (int step in Fire(t, holder, near)) yield return step;
            int fired = t.Now;
            LastPrismCast cast = Prisms.FiringBy(holder);
            if (!t.Check(cast != null, "a beam to follow")) yield break;
            bool hurtInFan = false, joinedOnTime = false;
            float levelAt2 = -1f;
            int stopped = -1;
            for (int i = 0; i < 900 / 15; i++)
            {
                int since = t.Now - fired;
                if (since % 30 == 0) Trace(t, holder, prism, near, far);
                if (!cast.Joined(t.Now) && t.Hurt(near)) hurtInFan = true;
                if (since >= 120 && levelAt2 < 0f) levelAt2 = prism.Level;
                if (since >= 186 && since < 200 && cast.Joined(t.Now)) joinedOnTime = true;
                if (since == 75) yield return t.ShotAs("prism-fan", holder.Position + new IntVec3(5, 0, 0), 9f);
                if (!cast.Firing)
                {
                    stopped = t.Now;
                    break;
                }
                yield return 15;
            }
            Trace(t, holder, prism, near, far);
            t.Check(hurtInFan, "the fan hurt the near enemy before the join");
            t.Check(joinedOnTime, "the beams joined 3 s after the fire");
            t.Check(Mathf.Abs(levelAt2 - 10f) < 0.15f, "2 s of beam spent 2 s of charge (" + levelAt2.ToString("0.00") + " left)");
            t.Check(near.Downed || near.Dead, "the near enemy is down");
            t.Check(far.Downed || far.Dead, "the far enemy is down");
            t.Check(stopped >= 0 && !cast.dried, "the beam stopped with no enemy left, not dry (at " + (stopped - fired) + " ticks)");
            t.Check(prism.Level > 0.5f, "charge is left (" + prism.Level.ToString("0.00") + ")");
            yield return 10;
            t.Check(holder.CurJobDef != LastPrismDefOf.AG_CastLastPrism, "the cast job ended");
            t.Check(t.Untouched(holder), "the holder is not hurt");
        }

        [RimArtTest("Last Prism", "fire 2 runs dry")]
        private static IEnumerable<int> RunsDry(RimArtTestContext t)
        {
            Pawn holder = Holder(t, 2f, out CompLastPrism prism);
            Pawn enemy = t.Mech(t.center + new IntVec3(10, 0, 0)) ?? t.Target(t.center + new IntVec3(10, 0, 0));
            yield return 2;
            foreach (int step in Fire(t, holder, enemy)) yield return step;
            int fired = t.Now;
            LastPrismCast cast = Prisms.FiringBy(holder);
            if (!t.Check(cast != null, "a beam to follow")) yield break;
            foreach (int step in WaitFor(() => !cast.Firing, 200, 5))
            {
                if ((t.Now - fired) % 30 == 0) Trace(t, holder, prism, enemy);
                yield return step;
            }
            Trace(t, holder, prism, enemy);
            int lasted = t.Now - fired;
            t.Check(!cast.Firing && cast.dried, "the beam stopped dry");
            t.Check(Mathf.Abs(lasted - 120) <= 6, "it lasted 2 s (" + lasted + " ticks)");
            t.Check(prism.Level <= 0.001f, "the prism is empty (" + prism.Level.ToString("0.000") + ")");
            yield return 10;
            Ability ability = holder.abilities.GetAbility(LastPrismDefOf.AG_LastPrism_Fire);
            t.Check(ability != null && !ability.CanCast, "Fire cannot be cast empty (" + ability?.CanCast.Reason + ")");
        }

        [RimArtTest("Last Prism", "fire 3 retarget a cell, hold, stop, rejoin")]
        private static IEnumerable<int> RetargetHoldStop(RimArtTestContext t)
        {
            Pawn holder = Holder(t, 12f, out CompLastPrism prism);
            Pawn enemy = t.Target(t.center + new IntVec3(9, 0, 0));
            yield return 2;
            foreach (int step in Fire(t, holder, enemy)) yield return step;
            LastPrismCast cast = Prisms.FiringBy(holder);
            if (!t.Check(cast != null, "a beam to follow")) yield break;
            yield return 30;
            IntVec3 north = t.center + new IntVec3(0, 0, 9);
            float before = cast.aim;
            cast.Retarget(north);
            t.Log(t.Now + " retarget to " + north + " from aim " + (before * Mathf.Rad2Deg).ToString("0"));
            yield return 60;
            float turned = Mathf.Abs((float)LastPrismTiming.Wrap(cast.aim - before)) * Mathf.Rad2Deg;
            Trace(t, holder, prism, enemy);
            t.Check(turned > 40f && turned < 50f, "the aim turned about 45 degrees in 1 s (" + turned.ToString("0.0") + ")");
            yield return 120;
            Trace(t, holder, prism, enemy);
            t.Check(Mathf.Abs((float)LastPrismTiming.Wrap(cast.aim - Mathf.PI / 2f)) < 0.05f, "the aim reached north");
            float enemyHealth = enemy.health.summaryHealth.SummaryHealthPercent;
            enemy.Destroy();
            yield return 120;
            Trace(t, holder, prism);
            t.Check(cast.Firing, "held on a cell, it keeps firing with no enemy left");
            t.Check(cast.Joined(t.Now), "and stays joined");
            t.Log("enemy health before it was removed " + enemyHealth.ToString("0%"));
            yield return t.ShotAs("prism-held-north", t.center + new IntVec3(0, 0, 5), 9f);
            float left = prism.Level;
            Prisms.Stop(holder);
            yield return 5;
            Trace(t, holder, prism);
            t.Check(!cast.Firing && !cast.dried, "Stop ended the beam");
            t.Check(holder.CurJobDef != LastPrismDefOf.AG_CastLastPrism, "the cast job ended");
            t.Check(Mathf.Abs(prism.Level - left) < 0.1f, "the charge left stays (" + prism.Level.ToString("0.00") + ")");

            // Fired again at once: the beam starts joined.
            foreach (int step in Fire(t, holder, t.center + new IntVec3(-8, 0, 0))) yield return step;
            LastPrismCast again = Prisms.FiringBy(holder);
            t.Check(again != null && again.Joined(t.Now), "fired again within 1 s, it starts joined");
            Prisms.Stop(holder);
            yield return 5;
        }

        [RimArtTest("Last Prism", "fire 4 walls, allies, the downed, retarget on the downed")]
        private static IEnumerable<int> WallsAlliesDowned(RimArtTestContext t)
        {
            Pawn holder = Holder(t, 12f, out CompLastPrism prism);
            Pawn ally = t.Colonist(t.center + new IntVec3(3, 0, 0));
            NoWimp(ally);
            Pawn downed = t.Target(t.center + new IntVec3(5, 0, 0));
            t.Down(downed);
            for (int z = -3; z <= 3; z++) t.Wall(t.center + new IntVec3(8, 0, z));
            Pawn behind = t.Target(t.center + new IntVec3(11, 0, 0));
            Pawn beside = t.Target(t.center + new IntVec3(0, 0, -6));
            yield return 2;
            // A spot in front of the wall: Fire needs line of sight to its target.
            foreach (int step in Fire(t, holder, t.center + new IntVec3(7, 0, 0))) yield return step;
            LastPrismCast cast = Prisms.FiringBy(holder);
            if (!t.Check(cast != null, "a beam to follow")) yield break;
            for (int i = 0; i < 16; i++)
            {
                Trace(t, holder, prism, ally, downed, behind, beside);
                if (i == 12) yield return t.ShotAs("prism-wall", t.center + new IntVec3(5, 0, 0), 9f);
                yield return 30;
            }
            // Retarget on the downed enemy: held as its cell, so the beam neither swings to another enemy nor stops.
            cast.Retarget(downed);
            yield return 30;
            Trace(t, holder, prism, ally, downed, behind, beside);
            t.Check(cast.Firing && !cast.target.HasThing && cast.target.Cell == downed.Position, "Retarget on a downed pawn holds the beam on its cell (target " + cast.target + ")");
            Prisms.Stop(holder);
            yield return 5;
            Trace(t, holder, prism, ally, downed, behind, beside);
            t.Check(t.Hurt(ally), "the ally in the lane was burnt");
            t.Check(!t.Struck(downed), "the downed enemy in the lane was passed over");
            t.Check(t.Untouched(behind), "the enemy behind the wall was not touched");
            t.Check(t.Untouched(beside), "the enemy 6 cells south, outside the fan, was not touched");
            t.Check(t.Untouched(holder), "the holder is not hurt");
        }

        [RimArtTest("Last Prism", "charge 1 sun, roof, hands, bag, firing")]
        private static IEnumerable<int> Charging(RimArtTestContext t)
        {
            Pawn holder = Holder(t, 0f, out CompLastPrism held);
            Thing lying = GenSpawn.Spawn(ThingMaker.MakeThing(LastPrismDefOf.AG_LastPrism), t.center + new IntVec3(3, 0, 0), t.map);
            Thing roofed = GenSpawn.Spawn(ThingMaker.MakeThing(LastPrismDefOf.AG_LastPrism), t.center + new IntVec3(-3, 0, 0), t.map);
            t.map.roofGrid.SetRoof(roofed.Position, RoofDefOf.RoofConstructed);
            Pawn carrier = t.Colonist(t.center + new IntVec3(0, 0, 3));
            var bagged = (ThingWithComps)ThingMaker.MakeThing(LastPrismDefOf.AG_LastPrism);
            carrier.inventory.innerContainer.TryAdd(bagged);
            CompLastPrism onGround = lying.TryGetComp<CompLastPrism>(), underRoof = roofed.TryGetComp<CompLastPrism>(), inBag = bagged.GetComp<CompLastPrism>();
            onGround.Level = underRoof.Level = inBag.Level = 0f;
            yield return 2;

            // Full sun, then half: one step of 20 s adds glow x 20 / 20 s of beam.
            foreach (float glow in new[] { 1f, 0.5f, 0f })
            {
                held.Level = onGround.Level = underRoof.Level = inBag.Level = 0f;
                t.map.skyManager.ForceSetCurSkyGlow(glow);
                Prisms.ChargeAll(20f);
                t.Log(t.Now + " sky glow " + glow.ToString("0.0") + ": held " + held.Level.ToString("0.000") + ", on the ground " + onGround.Level.ToString("0.000")
                      + ", under a roof " + underRoof.Level.ToString("0.000") + ", in a bag " + inBag.Level.ToString("0.000"));
                t.Check(Mathf.Abs(held.Level - glow) < 0.001f, "held in the open at glow " + glow + " gains " + glow.ToString("0.0") + " s");
                t.Check(Mathf.Abs(onGround.Level - glow) < 0.001f, "on open ground at glow " + glow + " gains " + glow.ToString("0.0") + " s");
                t.Check(underRoof.Level == 0f, "under a roof gains nothing");
                t.Check(inBag.Level == 0f, "in a bag gains nothing");
            }
            held.Level = 11.99f;
            t.map.skyManager.ForceSetCurSkyGlow(1f);
            Prisms.ChargeAll(20f);
            t.Check(Mathf.Abs(held.Level - 12f) < 0.0001f, "it stops at 12 s (" + held.Level.ToString("0.000") + ")");

            // Firing: no charge.
            Pawn enemy = t.Target(t.center + new IntVec3(10, 0, 0));
            held.Level = 6f;
            foreach (int step in Fire(t, holder, enemy)) yield return step;
            float before = held.Level;
            t.map.skyManager.ForceSetCurSkyGlow(1f);
            Prisms.ChargeAll(20f);
            t.Log(t.Now + " firing: " + before.ToString("0.000") + " before the step, " + held.Level.ToString("0.000") + " after");
            t.Check(held.Level <= before, "a firing prism gains nothing");
            Prisms.Stop(holder);
            yield return 5;
        }
    }
}
