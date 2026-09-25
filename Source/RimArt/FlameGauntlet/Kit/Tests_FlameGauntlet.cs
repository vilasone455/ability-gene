using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>Game tests for the Flame Gauntlet kit (run with -quicktest -rimarttest=flame).</summary>
    public static class Tests_FlameGauntlet
    {
        private static Pawn Holder(RimArtTestContext t, float heat, out CompFlameGauntlet gauntlet)
        {
            Pawn holder = t.Colonist(t.center);
            t.Equip(holder, FlameGauntletDefOf.AG_FlameGauntlet);
            gauntlet = CompFlameGauntlet.HeldBy(holder);
            gauntlet?.SetHeat(heat);
            return holder;
        }

        /// <summary>A fire on a chemfuel puddle, so it lasts through the test on bare soil.</summary>
        private static void Fire(RimArtTestContext t, IntVec3 cell)
        {
            FilthMaker.TryMakeFilth(cell, t.map, ThingDefOf.Filth_Fuel);
            FlameGauntletTestFire.Start(cell, t.map);
        }

        private static int FiresIn(Map map, IEnumerable<IntVec3> cells) => cells.Count(c => c.InBounds(map) && c.ContainsStaticFire(map));

        private static int Burns(Pawn pawn) => pawn.health.hediffSet.hediffs.Count(h => h.def == DamageDefOf.Burn.hediff);

        private static void Trace(RimArtTestContext t, Pawn holder, CompFlameGauntlet gauntlet, Pawn other = null) =>
            t.Log(t.Now + " | " + RimArtTestContext.Describe(holder) + " heat " + (gauntlet?.Heat ?? -1f).ToString("0.0")
                + (other != null ? " | " + RimArtTestContext.Describe(other) + (other.IsBurning() ? " BURNING" : "") : ""));

        /// <summary>Casts and traces until the cast job ends (at most <paramref name="most"/> ticks); returns the ticks it took, or -1.</summary>
        private static IEnumerable<int> Cast(RimArtTestContext t, Pawn holder, CompFlameGauntlet gauntlet, AbilityDef def, LocalTargetInfo target,
            Pawn other, string shot, int[] took, int most = 240)
        {
            Ability ability = holder.abilities.GetAbility(def);
            if (!t.Check(ability != null, "the holder has " + def.label)) yield break;
            if (!t.Check(ability.CanCast, def.label + " can be cast (" + ability.CanCast.Reason + ")")) yield break;
            if (!t.Check(ability.CanApplyOn(target), def.label + " can be applied on " + target)) yield break;
            ability.QueueCastingJob(target, LocalTargetInfo.Invalid);
            int cast = t.Now;
            t.Check(holder.CurJobDef == FlameGauntletDefOf.AG_CastFlameGauntlet, "the cast job started (job " + holder.CurJobDef?.defName + ")");
            took[0] = -1;
            for (int i = 0; i * 3 < most; i++)
            {
                Trace(t, holder, gauntlet, other);
                if (holder.CurJobDef != FlameGauntletDefOf.AG_CastFlameGauntlet)
                {
                    took[0] = t.Now - cast;
                    break;
                }
                if (i == 12) yield return t.ShotAs(shot);
                yield return 3;
            }
            t.Check(took[0] >= 0, "the cast job ended (after " + took[0] + " ticks)");
        }

        [RimArtTest("Flame Gauntlet", "devour 1 eats a 3x3 fire")]
        private static IEnumerable<int> DevourBlock(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 0f, out CompFlameGauntlet g);
            IntVec3 centre = t.center + new IntVec3(4, 0, 0);
            List<IntVec3> block = CellRect.CenteredOn(centre, 1).Cells.ToList();
            foreach (IntVec3 c in block) Fire(t, c);
            yield return 2;
            t.Log("fires before: " + FiresIn(t.map, block));
            var took = new int[1];
            foreach (int step in Cast(t, holder, g, FlameGauntletDefOf.AG_FlameGauntlet_Devour, centre, null, "devour-block", took)) yield return step;
            // 9 fires: the last arrives 0.5 + 8 x 0.08 + 0.45 s after the warmup began, the result 0.15 s later.
            int expected = UnityEngine.Mathf.RoundToInt((FlameDevourTiming.Pull + 8 * FlameDevourTiming.Gap + FlameDevourTiming.Travel + 0.15f - FlameGauntletTiming.Lead) * 60f);
            yield return 10;
            t.Check(FiresIn(t.map, block) == 0, "every fire in the block is out (" + FiresIn(t.map, block) + " left)");
            t.Check(UnityEngine.Mathf.Abs(g.Heat - 9f) < 0.2f, "heat is 9 (" + g.Heat.ToString("0.00") + ")");
            t.Check(took[0] >= expected - 3, "the job held until the result (" + took[0] + " ticks, result at " + expected + ")");
            t.Check(!holder.health.hediffSet.HasHediff(FlameGauntletDefOf.AG_FlameOverheating), "not overheating at 9");

            IntVec3 from = holder.Position;
            holder.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, from + new IntVec3(-3, 0, 0)), JobTag.DraftedOrder);
            yield return 120;
            t.Check(holder.Position != from, "the holder walks when ordered after the cast");
        }

        [RimArtTest("Flame Gauntlet", "devour 2 puts out a burning colonist")]
        private static IEnumerable<int> DevourColonist(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 0f, out CompFlameGauntlet g);
            Pawn ally = t.Colonist(t.center + new IntVec3(4, 0, 0));
            ally.TryAttachFire(0.5f, null);
            yield return 2;
            if (!t.Check(ally.IsBurning(), "the ally is on fire before Devour")) yield break;
            var took = new int[1];
            foreach (int step in Cast(t, holder, g, FlameGauntletDefOf.AG_FlameGauntlet_Devour, ally.Position, ally, "devour-ally", took)) yield return step;
            yield return 10;
            t.Check(!ally.IsBurning(), "the ally is no longer burning");
            t.Check(UnityEngine.Mathf.Abs(g.Heat - 2f) < 0.2f, "heat is 2 (" + g.Heat.ToString("0.00") + ")");
        }

        [RimArtTest("Flame Gauntlet", "devour 3 too hot leaves the rest burning")]
        private static IEnumerable<int> DevourTooHot(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 14f, out CompFlameGauntlet g);
            IntVec3 centre = t.center + new IntVec3(4, 0, 0);
            List<IntVec3> block = CellRect.CenteredOn(centre, 1).Cells.ToList();
            foreach (IntVec3 c in block) Fire(t, c);
            yield return 2;
            var took = new int[1];
            foreach (int step in Cast(t, holder, g, FlameGauntletDefOf.AG_FlameGauntlet_Devour, centre, null, "devour-hot", took)) yield return step;
            yield return 70;
            t.Check(g.Heat >= 19.8f, "the meter is full (" + g.Heat.ToString("0.00") + ")");
            t.Check(FiresIn(t.map, block) == 3, "3 fires are left burning (" + FiresIn(t.map, block) + ")");
            t.Check(holder.health.hediffSet.HasHediff(FlameGauntletDefOf.AG_FlameOverheating), "the holder is overheating");
            Ability devour = holder.abilities.GetAbility(FlameGauntletDefOf.AG_FlameGauntlet_Devour);
            t.Check(!devour.CanCast || devour.CooldownTicksRemaining > 0, "Devour cannot be cast again while full");
        }

        [RimArtTest("Flame Gauntlet", "release 1 full cone sets 16 cells and an enemy alight")]
        private static IEnumerable<int> ReleaseFull(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 20f, out CompFlameGauntlet g);
            Pawn enemy = t.Enemy(t.center + new IntVec3(4, 0, 0), armed: false);
            IntVec3 aim = t.center + new IntVec3(6, 0, 0);
            yield return 2;
            List<IntVec3> cone = FlameGauntletCone.Cells(holder, aim, 6, 3).Select(c => c.cell).ToList();
            t.Log("cone cells: " + cone.Count);
            var took = new int[1];
            foreach (int step in Cast(t, holder, g, FlameGauntletDefOf.AG_FlameGauntlet_Release, aim, enemy, "release-full", took)) yield return step;
            yield return 5;
            int fires = FiresIn(t.map, cone);
            t.Check(cone.Count == 16, "the cone has 16 cells (" + cone.Count + ")");
            t.Check(fires == cone.Count, "every cone cell is burning (" + fires + " of " + cone.Count + ")");
            t.Check(UnityEngine.Mathf.Abs(g.Heat - 4f) < 0.2f, "heat is 4 (" + g.Heat.ToString("0.00") + ")");
            t.Check(enemy.IsBurning() || enemy.Dead || enemy.Downed, "the enemy in the cone caught fire");
            t.Check(!holder.IsBurning() && Burns(holder) == 0, "the holder is not burning and has no burns");
            yield return t.ShotAs("release-after");
        }

        [RimArtTest("Flame Gauntlet", "release 2 short cone with 8 heat")]
        private static IEnumerable<int> ReleaseShort(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 8f, out CompFlameGauntlet g);
            IntVec3 aim = t.center + new IntVec3(0, 0, 6);
            yield return 2;
            List<IntVec3> cone = FlameGauntletCone.Cells(holder, aim, 6, 3).Select(c => c.cell).ToList();
            var took = new int[1];
            foreach (int step in Cast(t, holder, g, FlameGauntletDefOf.AG_FlameGauntlet_Release, aim, null, "release-short", took)) yield return step;
            yield return 5;
            t.Check(FiresIn(t.map, cone.Take(8)) == 8, "the first 8 cells burn (" + FiresIn(t.map, cone.Take(8)) + ")");
            t.Check(FiresIn(t.map, cone.Skip(8)) == 0, "the rest of the cone does not (" + FiresIn(t.map, cone.Skip(8)) + ")");
            t.Check(g.Heat < 0.2f, "heat is 0 (" + g.Heat.ToString("0.00") + ")");
        }

        [RimArtTest("Flame Gauntlet", "release 3 too cold under 5 heat")]
        private static IEnumerable<int> ReleaseCold(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 3f, out CompFlameGauntlet g);
            yield return 2;
            Ability release = holder.abilities.GetAbility(FlameGauntletDefOf.AG_FlameGauntlet_Release);
            t.Check(release != null && !release.CanCast, "Release cannot be cast at 3 heat");
            t.Check(release != null && release.GizmoDisabled(out string reason) && reason.StartsWith("Too cold"), "the gizmo says too cold");
        }

        [RimArtTest("Flame Gauntlet", "overheat burns the arm and ends after release", 2400)]
        private static IEnumerable<int> Overheat(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 17f, out CompFlameGauntlet g);
            yield return 70;
            t.Check(holder.health.hediffSet.HasHediff(FlameGauntletDefOf.AG_FlameOverheating), "overheating at 17 heat");
            float move = holder.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
            t.Log("moving capacity " + move.ToString("0.00"));
            t.Check(move < 0.95f, "moving is lowered (" + move.ToString("0.00") + ")");
            yield return 5 * 60 + 70;
            int burns = Burns(holder);
            t.Check(burns >= 1, "the arm has a burn after 5 s (" + burns + ")");
            BodyPartRecord arm = FlameGauntletHeat.GauntletArm(holder);
            t.Check(holder.health.hediffSet.hediffs.Any(h => h.def == DamageDefOf.Burn.hediff && h.Part == arm), "the burn is on the gauntlet arm (" + arm?.Label + ")");
            var took = new int[1];
            foreach (int step in Cast(t, holder, g, FlameGauntletDefOf.AG_FlameGauntlet_Release, t.center + new IntVec3(6, 0, 0), null, "overheat-release", took)) yield return step;
            yield return 70;
            t.Check(!holder.health.hediffSet.HasHediff(FlameGauntletDefOf.AG_FlameOverheating), "overheating ends after Release (" + g.Heat.ToString("0.0") + " heat)");
        }

        [RimArtTest("Flame Gauntlet", "immune to fire, blocked hits add heat")]
        private static IEnumerable<int> Immune(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 0f, out CompFlameGauntlet g);
            yield return 2;
            holder.TryAttachFire(0.5f, null);
            t.Check(!holder.IsBurning(), "no fire attaches to the holder");
            holder.TakeDamage(new DamageInfo(DamageDefOf.Flame, 10f));
            t.Check(Burns(holder) == 0, "a 10 flame hit leaves no burn");
            t.Check(UnityEngine.Mathf.Abs(g.Heat - 1f) < 0.05f, "the blocked hit added 1 heat (" + g.Heat.ToString("0.00") + ")");
            Fire(t, holder.Position);
            yield return 300;
            t.Check(Burns(holder) == 0 && !holder.IsBurning(), "standing in fire for 5 s leaves no burn (heat now " + g.Heat.ToString("0.0") + ")");
        }

        [RimArtTest("Flame Gauntlet", "chemfuel reload stops at 14, no automatic reload")]
        private static IEnumerable<int> Reload(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 0f, out CompFlameGauntlet g);
            yield return 2;
            t.Check(!g.NeedsReload(false), "the holder never goes to reload on its own");
            t.Check(g.NeedsReload(true), "a forced reload is allowed at 0 heat");
            Thing fuel = ThingMaker.MakeThing(ThingDefOf.Chemfuel);
            fuel.stackCount = 20;
            GenSpawn.Spawn(fuel, t.center + new IntVec3(1, 0, 0), t.map);
            t.Check(g.MaxAmmoNeeded(true) == 7, "7 chemfuel fill it to 14 (" + g.MaxAmmoNeeded(true) + ")");
            g.ReloadFrom(fuel);
            t.Check(UnityEngine.Mathf.Abs(g.Heat - 14f) < 0.05f, "heat is 14 after the reload (" + g.Heat.ToString("0.00") + ")");
            t.Check(fuel.stackCount == 13, "13 chemfuel are left (" + fuel.stackCount + ")");
            t.Check(!g.NeedsReload(true), "no more reload at 14");
        }

        [RimArtTest("Flame Gauntlet", "heat drains 1 per 30 s", 2400)]
        private static IEnumerable<int> Drain(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, 10f, out CompFlameGauntlet g);
            for (int i = 0; i < 6; i++)
            {
                Trace(t, holder, g);
                yield return 300;
            }
            Trace(t, holder, g);
            t.Check(UnityEngine.Mathf.Abs(g.Heat - 9f) < 0.05f, "heat is 9 after 30 s (" + g.Heat.ToString("0.00") + ")");
            t.Check(CompFlameGauntlet.HeldBy(holder) == g, "the holder still holds the same gauntlet");
        }
    }
}
