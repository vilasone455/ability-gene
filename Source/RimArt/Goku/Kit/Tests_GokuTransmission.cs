using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests for Instant Transmission (run with -quicktest -rimarttest=goku): the jumps alone and
    /// with a passenger, the lock channel onto a healthy pawn, a hurt one, a mechanoid and an empty
    /// cell, and what a stun, a move order and a passenger walking off do to the channel.
    /// </summary>
    public static class Tests_GokuTransmission
    {
        private static Ability It(Pawn host) => host.abilities.GetAbility(GokuDefOf.AG_GokuInstantTransmission);

        /// <summary>The lock's channel in ticks for a jump from where the host stands, logged with the distance and what it locks onto.</summary>
        private static int Expect(RimArtTestContext t, Pawn host, IntVec3 dest, Pawn passenger, string what)
        {
            float s = GokuTransmissionLock.Seconds(host, host.Position, dest, passenger, out Pawn locked, out float energy);
            t.Log(what + ": " + (dest - host.Position).LengthHorizontal.ToString("0.0") + " cells, lock "
                  + (locked == null ? "none" : locked.LabelShort + " " + energy.ToString("0.00")) + ", channel " + s.ToString("0.00") + " s");
            return Mathf.RoundToInt(s * 60f);
        }

        [RimArtTest("Goku", "transmission 1 alone, with a hostile passenger (stunned), and with a downed ally")]
        private static IEnumerable<int> Transmission(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Tests_Goku.Setup(t);
            yield return 5;
            Pawn host = Tests_Goku.Host(t, t.center, out EchoRecord record);
            // Undrafted and held: a drafted pawn punches an adjacent enemy, and a busy stance refuses the next cast.
            host.drafter.Drafted = false;
            Hold(host);
            yield return 2;
            Ability it = It(host);
            if (!t.Check(it != null && it.CanCast, "Goku has Instant Transmission")) yield break;

            // Alone, to an empty cell.
            IntVec3 dest = t.center + new IntVec3(8, 0, 5);
            int want = Expect(t, host, dest, null, "alone");
            float before = echoes.charge;
            t.Check(it.CanApplyOn((LocalTargetInfo)host), "Goku himself is a valid first target");
            it.QueueCastingJob(host, dest);
            yield return 2;
            t.Check(host.CurJobDef == GokuDefOf.AG_GokuTransmit, "the channel job runs (" + Describe(host) + ")");
            foreach (int w in WaitFor(() => host.Position == dest, want + 60, 1)) yield return w;
            t.Log(Describe(host));
            t.Check(host.Position == dest, "Goku arrived at " + dest);
            float spent = before - echoes.charge;
            t.Check(spent >= 2f && spent < 3f, "the pool paid 2 (" + spent.ToString("0.##") + " with upkeep)");
            yield return 5;
            Hold(host);

            // A hostile passenger. A healthy colonist near the landing cell makes the lock short, and the
            // enemy is stunned for the channel so it cannot start a fight before the jump.
            yield return 60;
            it.ResetCooldown();
            Pawn enemy = t.Enemy(host.Position + new IntVec3(1, 0, 0), armed: false);
            IntVec3 dest2 = t.center + new IntVec3(-8, 0, -5);
            Tests_Goku.Ally(t, dest2 + new IntVec3(3, 0, 0));
            want = Expect(t, host, dest2, enemy, "with a hostile passenger, onto a colonist");
            enemy.stances.stunner.StunFor(want + 15, null, false);
            yield return 2;
            t.Check(it.CanApplyOn((LocalTargetInfo)enemy), "an adjacent enemy is a valid first target");
            it.QueueCastingJob(enemy, dest2);
            foreach (int w in WaitFor(() => host.Position == dest2, want + 60, 1)) yield return w;
            t.Log(Describe(host));
            t.Log(Describe(enemy));
            t.Check(host.Position == dest2, "Goku arrived at " + dest2);
            t.Check(enemy.Spawned && enemy.Position.AdjacentTo8WayOrInside(dest2) && enemy.Position != dest2, "the enemy landed beside him");
            t.Check(Stunned(enemy) && enemy.stances.stunner.StunTicksLeft > 50, "the enemy arrived stunned for 1.5 s (" + enemy.stances.stunner.StunTicksLeft + " ticks left)");
            if (enemy.Spawned) enemy.Destroy();
            yield return 5;
            Hold(host);

            // A downed ally, carried lying, to an empty cell.
            yield return 60;
            it.ResetCooldown();
            Pawn ally = Tests_Goku.Ally(t, host.Position + new IntVec3(0, 0, 1));
            HealthUtility.DamageUntilDowned(ally, false);
            IntVec3 dest3 = t.center + new IntVec3(0, 0, 8);
            yield return 2;
            if (!t.Check(ally.Downed, "the ally is downed")) { EndHost(record); yield break; }
            want = Expect(t, host, dest3, ally, "with a downed ally, to an empty cell");
            t.Check(it.CanApplyOn((LocalTargetInfo)ally), "a downed ally is a valid first target");
            it.QueueCastingJob(ally, dest3);
            foreach (int w in WaitFor(() => host.Position == dest3, want + 60, 1)) yield return w;
            t.Log(Describe(host));
            t.Log(Describe(ally));
            t.Check(host.Position == dest3, "Goku arrived at " + dest3);
            t.Check(ally.Spawned && ally.Position.AdjacentTo8WayOrInside(dest3) && ally.Position != dest3, "the downed ally landed beside him");
            t.Check(ally.Downed && !Stunned(ally), "the ally is still downed and not stunned");
            EndHost(record);
        }

        [RimArtTest("Goku", "transmission 2 the lock is short onto a healthy pawn, longer onto a hurt one, longest onto an empty cell; a mech counts as empty and cannot be taken along")]
        private static IEnumerable<int> Lock(RimArtTestContext t)
        {
            Tests_Goku.Setup(t);
            yield return 5;
            IntVec3 home = t.center + new IntVec3(-10, 0, 0);
            Pawn host = Tests_Goku.Host(t, home, out EchoRecord record);
            host.drafter.Drafted = false;
            Hold(host);
            Ability it = It(host);
            var comp = it.CompOfType<CompAbilityEffect_InstantTransmission>();
            CompProperties_InstantTransmission p = comp.Props;

            // A mech next to Goku cannot be taken along.
            Pawn near = t.Mech(home + new IntVec3(1, 0, 0));
            if (near != null)
            {
                near.stances.stunner.StunFor(600, null, false);
                yield return 2;
                t.Check(!it.verb.targetParams.CanTarget(near), "the first click cannot target a mech");
                t.Check(!comp.Valid((LocalTargetInfo)near), "and the ability refuses one");
                near.Destroy();
            }

            // Four landing cells 10 east of Goku: next to a healthy colonist and nothing are 13.5 cells
            // away, next to a hurt colonist and a mech 10.4.
            IntVec3 healthyCell = home + new IntVec3(10, 0, 9), emptyCell = home + new IntVec3(10, 0, -9);
            IntVec3 hurtCell = home + new IntVec3(10, 0, 3), mechCell = home + new IntVec3(10, 0, -3);
            Pawn healthy = Tests_Goku.Ally(t, healthyCell + new IntVec3(1, 0, 1));
            Pawn hurt = Tests_Goku.Ally(t, hurtCell + new IntVec3(1, 0, 1));
            HealthUtility.DamageUntilDowned(hurt, false);
            Pawn mech = t.Mech(mechCell + new IntVec3(1, 0, 1));
            mech?.stances.stunner.StunFor(1200, null, false);
            yield return 2;

            float healthyS = GokuTransmissionLock.Seconds(host, home, healthyCell, null, out Pawn onHealthy, out float healthyE);
            float emptyS = GokuTransmissionLock.Seconds(host, home, emptyCell, null, out Pawn onEmpty, out _);
            float hurtS = GokuTransmissionLock.Seconds(host, home, hurtCell, null, out Pawn onHurt, out float hurtE);
            float mechS = GokuTransmissionLock.Seconds(host, home, mechCell, null, out Pawn onMech, out _);
            t.Log("healthy " + healthy.LabelShort + " life energy " + healthyE.ToString("0.00") + ", lock " + healthyS.ToString("0.00") + " s");
            t.Log("hurt " + Describe(hurt) + " life energy " + hurtE.ToString("0.00") + ", lock " + hurtS.ToString("0.00") + " s");
            t.Log("mech cell lock " + mechS.ToString("0.00") + " s, empty cell lock " + emptyS.ToString("0.00") + " s");
            float d13 = (healthyCell - home).LengthHorizontal, d10 = (hurtCell - home).LengthHorizontal;
            t.Check(onHealthy == healthy && Mathf.Abs(healthyS - (p.baseSeconds + p.secondsPerCell * d13) * (1f + p.emptyFactor * (1f - healthyE))) < 0.01f,
                "Goku locks onto the healthy colonist and the channel follows the formula");
            t.Check(onEmpty == null && Mathf.Abs(emptyS - (p.baseSeconds + p.secondsPerCell * d13) * (1f + p.emptyFactor)) < 0.01f,
                "an empty cell has no lock and takes (0.5 + 0.01 x " + d13.ToString("0.0") + ") x 4 s");
            t.Check(onHurt == hurt && hurtE > 0f && hurtE < healthyE, "the downed colonist's life energy is lower (" + hurtE.ToString("0.00") + " < " + healthyE.ToString("0.00") + ")");
            if (mech != null) t.Check(onMech == null && Mathf.Abs(mechS - (p.baseSeconds + p.secondsPerCell * d10) * (1f + p.emptyFactor)) < 0.01f, "the mech gives no lock: its cell takes as long as an empty one");
            t.Check(healthyS < emptyS, "healthy lock " + healthyS.ToString("0.00") + " s is shorter than empty " + emptyS.ToString("0.00") + " s at the same distance");
            t.Check(hurtS < mechS, "hurt lock " + hurtS.ToString("0.00") + " s is shorter than the mech/empty " + mechS.ToString("0.00") + " s at the same distance");

            // Real jumps, measured from the order: onto the healthy colonist, then onto the empty cell.
            int want = Expect(t, host, healthyCell, null, "jump onto the healthy colonist");
            int start = t.Now;
            it.QueueCastingJob(host, healthyCell);
            foreach (int w in WaitFor(() => host.Position == healthyCell, want + 60, 1)) yield return w;
            int took = t.Now - start;
            t.Check(host.Position == healthyCell && Mathf.Abs(took - want) <= 5, "Goku arrived after " + took + " ticks (the lock says " + want + ")");
            yield return 5;
            Hold(host);
            it.ResetCooldown();
            yield return 2;
            want = Expect(t, host, emptyCell, null, "jump onto the empty cell");
            start = t.Now;
            it.QueueCastingJob(host, emptyCell);
            foreach (int w in WaitFor(() => host.Position == emptyCell, want + 60, 1)) yield return w;
            took = t.Now - start;
            t.Check(host.Position == emptyCell && Mathf.Abs(took - want) <= 5, "Goku arrived after " + took + " ticks (the lock says " + want + ")");
            EndHost(record);
        }

        [RimArtTest("Goku", "transmission 3 a stun in the channel spends the cooldown and not the charge; a move order spends nothing; a passenger that walks off is left behind")]
        private static IEnumerable<int> Interrupts(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Tests_Goku.Setup(t);
            yield return 5;
            Pawn host = Tests_Goku.Host(t, t.center, out EchoRecord record);
            yield return 2;
            Ability it = It(host);
            IntVec3 far = t.center + new IntVec3(10, 0, 8);

            // A stun 0.5 s into the channel.
            int want = Expect(t, host, far, null, "to an empty cell");
            float before = echoes.charge;
            it.QueueCastingJob(host, far);
            yield return 30;
            t.Check(host.CurJobDef == GokuDefOf.AG_GokuTransmit, "the channel runs (" + Describe(host) + ")");
            host.stances.stunner.StunFor(30, null, false);
            foreach (int w in WaitFor(() => host.CurJobDef != GokuDefOf.AG_GokuTransmit, 10, 1)) yield return w;
            t.Log(Describe(host));
            t.Check(host.CurJobDef != GokuDefOf.AG_GokuTransmit, "the stun ended the channel at once");
            yield return want;
            float spent = before - echoes.charge;
            t.Check(host.Position == t.center, "Goku did not jump");
            t.Check(it.CooldownTicksRemaining > 0, "the cooldown is spent");
            t.Check(spent < 2f, "the charge is not (" + spent.ToString("0.##") + ", upkeep only)");

            // A move order 0.5 s into the channel.
            it.ResetCooldown();
            yield return 2;
            before = echoes.charge;
            it.QueueCastingJob(host, far);
            yield return 30;
            IntVec3 walkTo = t.center + new IntVec3(-3, 0, 0);
            host.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, walkTo), JobTag.DraftedOrder);
            yield return 2;
            t.Log(Describe(host));
            t.Check(host.CurJobDef == JobDefOf.Goto, "the move order interrupted the channel");
            yield return want;
            spent = before - echoes.charge;
            t.Check(host.Position != far, "Goku did not jump (" + Describe(host) + ")");
            t.Check(it.CooldownTicksRemaining == 0, "no cooldown is spent");
            t.Check(spent < 2f, "no charge is paid (" + spent.ToString("0.##") + ", upkeep only)");

            // A passenger that walks off during the channel.
            host.drafter.Drafted = false;
            Hold(host);
            yield return 2;
            Pawn walker = Tests_Goku.Ally(t, host.Position + new IntVec3(0, 0, 1));
            IntVec3 dest = t.center + new IntVec3(5, 0, -8);
            yield return 2;
            want = Expect(t, host, dest, walker, "with a passenger who walks off");
            it.QueueCastingJob(walker, dest);
            yield return 20;
            walker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, walker.Position + new IntVec3(-6, 0, 6)), JobCondition.InterruptForced);
            foreach (int w in WaitFor(() => host.Position == dest, want + 60, 1)) yield return w;
            t.Log(Describe(host));
            t.Log(Describe(walker));
            t.Check(host.Position == dest, "Goku jumped: the channel went on when the passenger left");
            t.Check(!walker.Position.AdjacentTo8WayOrInside(dest), "the passenger was left behind");
            EndHost(record);
        }
    }
}
