using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimArt
{
    /// <summary>Game tests for the Chain Sickle kit (run with -quicktest -rimarttest=chain).</summary>
    public static class Tests_ChainSickle
    {
        private static JobDef CastJob => DefDatabase<JobDef>.GetNamed("AG_CastChainSickle");

        private static Pawn Holder(RimArtTestContext t, bool drafted = true)
        {
            Pawn holder = t.Colonist(t.center);
            t.Equip(holder, ChainSickleDefOf.AG_ChainSickle);
            holder.drafter.Drafted = drafted;
            return holder;
        }

        private static void Trace(RimArtTestContext t, Pawn holder, Pawn target)
        {
            CompChainSickle sickle = CompChainSickle.HeldBy(holder);
            string link = sickle == null ? "no sickle" : sickle.snagged == null ? "no snag"
                : (sickle.staked ? "staked, pin ends " + sickle.pinEndsTick : "snagged, ends " + sickle.snagEndsTick);
            t.Log(t.Now + " | " + RimArtTestContext.Describe(holder) + " | " + RimArtTestContext.Describe(target) + " | " + link);
        }

        private static IntVec3 Cell(Pawn pawn) => pawn.Spawned ? pawn.Position : pawn.PositionHeld;

        private static float Distance(Pawn a, Pawn b) => Cell(a).DistanceTo(Cell(b));

        /// <summary>
        /// Casts Snag on the target and traces both pawns for 4 s, then checks: the pull (or the drag
        /// when the target is too heavy), that the cast job held the holder until the reel was over and
        /// then ended, and that the holder walks when ordered afterwards.
        /// </summary>
        private static IEnumerable<int> Snag(RimArtTestContext t, Pawn holder, Pawn target, string shot, bool walkAfter = true)
        {
            yield return 2;
            CompChainSickle sickle = CompChainSickle.HeldBy(holder);
            Ability snag = holder.abilities.GetAbility(ChainSickleDefOf.AG_ChainSickle_Snag);
            if (!t.Check(sickle != null && snag != null, "the holder holds a chain sickle and has Snag")) yield break;

            ChainSickleWeight w = ChainSickleCombat.Weigh(holder, target, sickle.Props);
            IntVec3 holderFrom = holder.Position, targetFrom = target.Position;
            float before = Distance(holder, target);
            t.Log("target " + target.KindLabel + ", mass " + ChainSickleCombat.Mass(target).ToString("0") + " kg; weight ratio " + w.Ratio.ToString("0.00")
                + ", pull " + w.Pull.ToString("0.0") + " cells, reel " + w.Reel.ToString("0.00") + " s, dragged " + w.Dragged + "; distance " + before.ToString("0.0"));
            if (!t.Check(snag.CanCast && snag.CanApplyOn((LocalTargetInfo)target), "Snag can be cast on the target")) yield break;

            snag.QueueCastingJob(target, LocalTargetInfo.Invalid);
            int cast = t.Now;
            t.Check(holder.CurJobDef == CastJob, "the cast job started (job " + holder.CurJobDef?.defName + ")");

            // The picture's reel ends at ReelEnd seconds from its start (Spin0 before the cast).
            int reelEndTick = cast + UnityEngine.Mathf.RoundToInt((ChainSickleSnagTiming.ReelEnd(w.Reel) - ChainSickleSnagTiming.Spin0) * 60f);
            bool sawFlyer = false;
            int jobEndTick = -1;
            // Measured on the first tick both are back on the map after a flyer: the target walks on after.
            float landed = -1f;
            IntVec3 holderLanded = holderFrom;
            for (int i = 0; i < 80; i++)
            {
                Trace(t, holder, target);
                bool flying = target.ParentHolder is PawnFlyer || holder.ParentHolder is PawnFlyer;
                if (flying) sawFlyer = true;
                else if (sawFlyer && landed < 0f)
                {
                    landed = Distance(holder, target);
                    holderLanded = Cell(holder);
                }
                if (holder.CurJobDef != CastJob && jobEndTick < 0) jobEndTick = t.Now;
                if (i == 25) yield return t.ShotAs(shot + "-reel");
                yield return 3;
            }
            float after = landed >= 0f ? landed : Distance(holder, target);
            t.Log("distance before " + before.ToString("0.0") + ", at landing " + after.ToString("0.0") + "; holder " + holderFrom + " -> " + Cell(holder)
                + ", target " + targetFrom + " -> " + Cell(target) + "; job ended at " + jobEndTick + ", reel ends at " + reelEndTick);
            // A raider that charged into melee during the warmup is already next to the holder: nothing to pull.
            t.Check(sawFlyer || Distance(holder, target) <= 2f, "a pawn was put in the reel flyer, or the target was already next to the holder");
            t.Check(holder.Spawned && (target.Dead || target.Spawned), "both pawns are back on the map");
            if (w.Dragged) t.Check(holderLanded != holderFrom, "the holder was dragged toward the too-heavy target");
            else t.Check(after <= before - 1.5f, "the target was pulled closer");
            t.Check(jobEndTick >= 0, "the cast job ended");
            // A dragged holder rides the flyer instead, which ends its job at the start of the drag.
            if (!w.Dragged) t.Check(jobEndTick < 0 || jobEndTick >= reelEndTick - 3, "the cast job held the holder until the reel was over (ended " + (jobEndTick - reelEndTick) + " ticks from the reel's end)");
            yield return t.ShotAs(shot + "-after");
            if (!walkAfter) yield break;

            IntVec3 from = holder.Position, to = holder.Position + new IntVec3(-3, 0, 0);
            holder.drafter.Drafted = true;
            holder.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, to), JobTag.DraftedOrder);
            for (int i = 0; i < 6; i++)
            {
                Trace(t, holder, target);
                yield return 20;
            }
            t.Check(holder.Position != from, "the holder walks when ordered after the cast (from " + from + ", now " + holder.Position + ")");
        }

        [RimArtTest("Chain Sickle", "snag 1 drafted holder pulls a standing enemy")]
        private static IEnumerable<int> SnagDrafted(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t);
            Pawn enemy = t.Enemy(t.center + new IntVec3(6, 0, 0));
            foreach (int step in Snag(t, holder, enemy, "drafted")) yield return step;
        }

        [RimArtTest("Chain Sickle", "snag 2 undrafted holder")]
        private static IEnumerable<int> SnagUndrafted(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, drafted: false);
            Pawn enemy = t.Enemy(t.center + new IntVec3(6, 0, 0));
            foreach (int step in Snag(t, holder, enemy, "undrafted")) yield return step;
        }

        [RimArtTest("Chain Sickle", "snag 3 raider in an assaulting group")]
        private static IEnumerable<int> SnagRaider(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t);
            var raiders = new List<Pawn>();
            for (int i = -1; i <= 1; i++) raiders.Add(t.Enemy(t.center + new IntVec3(7, 0, 2 * i)));
            Faction faction = raiders[0].Faction;
            for (int i = 1; i < raiders.Count; i++) raiders[i].SetFaction(faction);
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), t.map, raiders);
            yield return 30;
            Pawn target = raiders.Where(p => !p.Downed && p.Spawned).OrderBy(p => p.Position.DistanceTo(holder.Position)).FirstOrDefault();
            if (!t.Check(target != null && target.Position.DistanceTo(holder.Position) < 7.5f, "a raider is in reach")) yield break;
            foreach (int step in Snag(t, holder, target, "raider")) yield return step;
        }

        [RimArtTest("Chain Sickle", "snag 4 too heavy drags the holder")]
        private static IEnumerable<int> SnagHeavy(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Thrumbo") ?? DefDatabase<PawnKindDef>.GetNamed("Muffalo");
            Pawn beast = PawnGenerator.GeneratePawn(kind, null);
            GenSpawn.Spawn(beast, t.center + new IntVec3(6, 0, 0), t.map);
            RimArtTestContext.Hold(beast);
            foreach (int step in Snag(t, holder, beast, "heavy")) yield return step;
        }

        [RimArtTest("Chain Sickle", "stake 1 pins the snagged enemy")]
        private static IEnumerable<int> Stake(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t);
            Pawn enemy = t.Enemy(t.center + new IntVec3(6, 0, 0));
            foreach (int step in Snag(t, holder, enemy, "stake-snag", walkAfter: false)) yield return step;

            CompChainSickle sickle = CompChainSickle.HeldBy(holder);
            if (!t.Check(sickle != null && sickle.Snags(enemy), "the enemy is still snagged before Stake")) yield break;
            Ability stake = holder.abilities.GetAbility(ChainSickleDefOf.AG_ChainSickle_Stake);
            t.Log("Stake can cast: " + stake.CanCast.Reason + " " + (bool)stake.CanCast);
            if (!t.Check(stake.CanCast && stake.CanApplyOn((LocalTargetInfo)enemy), "Stake can be cast on the snagged enemy")) yield break;
            bool armed = enemy.equipment?.Primary != null;
            stake.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            int cast = t.Now, jobEndTick = -1;
            bool pinned = false;
            for (int i = 0; i < 40; i++)
            {
                Trace(t, holder, enemy);
                if (holder.CurJobDef != CastJob && jobEndTick < 0) jobEndTick = t.Now;
                if (sickle.staked && enemy.stances.stunner.Stunned) pinned = true;
                if (i == 15) yield return t.ShotAs("stake-pin");
                yield return 3;
            }
            t.Check(pinned, "the enemy was pinned (staked and stunned)");
            t.Check(!armed || enemy.equipment.Primary == null, "the enemy dropped its weapon");
            t.Check(jobEndTick >= 0, "the Stake cast job ended (after " + (jobEndTick - cast) + " ticks)");
            t.Check(enemy.health.hediffSet.HasHediff(ChainSickleDefOf.AG_ChainStaked) || !sickle.staked, "the staked hediff is on while pinned");
        }
    }
}
