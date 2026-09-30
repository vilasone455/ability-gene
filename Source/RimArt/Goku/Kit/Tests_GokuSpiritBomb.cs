using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests for Spirit Bomb (run with -quicktest -rimarttest=goku): a lender's ki and its Rest
    /// cost, the throw hitting hostiles only, the Rest floor, the 60-power cap, Cancel and a stun.
    /// </summary>
    public static class Tests_GokuSpiritBomb
    {
        /// <summary>Sets the Rest bar, so what a pawn gives a Spirit Bomb is known.</summary>
        private static void SetRest(Pawn pawn, float level)
        {
            if (pawn.needs?.rest != null) pawn.needs.rest.CurLevel = level;
        }

        [RimArtTest("Goku", "spirit 1 a lender adds its ki and pays Rest, the throw hurts hostiles under the dome only, pays 30", 3000)]
        private static IEnumerable<int> SpiritBomb(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Tests_Goku.Setup(t);
            yield return 5;
            Pawn host = Tests_Goku.Host(t, t.center, out EchoRecord record);
            Pawn lender = Tests_Goku.Ally(t, t.center + new IntVec3(-2, 0, 0));
            IntVec3 target = t.center + new IntVec3(8, 0, 0);
            Pawn e1 = t.Enemy(target, armed: false);
            Pawn e2 = t.Enemy(target + new IntVec3(0, 0, 2), armed: false);
            // The ally stands under the dome but not next to an enemy, and the enemies are held still: a punch would spoil the "untouched" check.
            Pawn ally = Tests_Goku.Ally(t, target + new IntVec3(-2, 0, -2));
            Thing wall = t.Wall(target + new IntVec3(1, 0, 1), ThingDefOf.WoodLog);
            Pawn far = t.Enemy(target + new IntVec3(0, 0, 6), armed: false);
            e1.stances.stunner.StunFor(900, null, false);
            e2.stances.stunner.StunFor(900, null, false);
            far.stances.stunner.StunFor(900, null, false);
            yield return 2;
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuSpiritBomb);
            if (!t.Check(ability != null && ability.CanCast, "Goku has Spirit Bomb")) yield break;
            SetRest(host, 1f);
            SetRest(lender, 1f);
            float before = echoes.charge;
            ability.QueueCastingJob(target, LocalTargetInfo.Invalid);
            SpiritBombCast cast = null;
            foreach (int w in WaitFor(() => (cast = Tests_Goku.CastOf(host) as SpiritBombCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started (" + RimArtTestContext.Describe(host) + ")")) { EndHost(record); yield break; }
            float spent = before - echoes.charge;
            t.Check(spent >= 30f && spent < 31f, "the pool paid 30 (" + spent.ToString("0.##") + ")");
            t.Check(!cast.CanThrow(t.Now), "Throw is not allowed before 3 s");

            Job lend = JobMaker.MakeJob(GokuDefOf.AG_GokuLend, host);
            lend.playerForced = true;
            lender.jobs.TryTakeOrderedJob(lend, JobTag.Misc);
            yield return 5;
            t.Check(SpiritBombCast.IsLending(lender, host), "the lender took the lend job (" + RimArtTestContext.Describe(lender) + ")");
            float power0 = cast.PowerNow(t.Now), lenderRest0 = GokuLifeEnergy.Rest(lender);
            float want = (SpiritBombCast.RateOf(host, true) + SpiritBombCast.RateOf(lender, false)) * 2f;
            for (int i = 0; i < 4; i++)
            {
                yield return 30;
                t.Log("power " + cast.PowerNow(t.Now).ToString("0.00") + ", rate " + cast.rateNow.ToString("0.00") + "/s; Goku Rest " + GokuLifeEnergy.Rest(host).ToString("0.000")
                      + ", lender Rest " + GokuLifeEnergy.Rest(lender).ToString("0.000"));
            }
            float gained = cast.PowerNow(t.Now) - power0, lenderPaid = lenderRest0 - GokuLifeEnergy.Rest(lender);
            t.Check(cast.LenderCount == 1, "one lender counted");
            t.Check(Mathf.Abs(gained - want) < 0.15f * want, "Goku and the lender gave " + gained.ToString("0.00") + " in 2 s (their ki says " + want.ToString("0.00") + ")");
            t.Check(Mathf.Abs(lenderPaid - 0.04f) < 0.006f, "the lender paid 2 % Rest per second (" + lenderPaid.ToString("0.000") + " in 2 s)");
            lender.jobs.EndCurrentJob(JobCondition.InterruptForced);
            yield return 5;
            t.Check(cast.LenderCount == 0, "Stop lending closed the stint");
            float held = cast.PowerNow(t.Now), alone = SpiritBombCast.RateOf(host, true);
            yield return 60;
            float grew = cast.PowerNow(t.Now) - held;
            t.Check(Mathf.Abs(grew - alone) < 0.15f * alone + 0.02f, "power grows by Goku's own rate after that (" + grew.ToString("0.00") + " in 1 s, rate " + alone.ToString("0.00") + ")");

            t.Check(cast.CanThrow(t.Now), "Throw is allowed after 3 s");
            float radius = cast.RadiusNow(t.Now);
            t.Log("throwing at power " + cast.PowerNow(t.Now).ToString("0.0") + ", radius " + radius.ToString("0.00") + ", 5 hits of " + cast.HitDamageNow(t.Now).ToString("0.0"));
            cast.throwOrdered = true;
            foreach (int w in WaitFor(() => cast.Thrown, 30, 1)) yield return w;
            if (!t.Check(cast.Thrown, "thrown")) { EndHost(record); yield break; }
            t.Check(Mathf.Abs(cast.RadiusNow(t.Now) - (2f + 0.15f * cast.power)) < 0.01f && cast.RadiusNow(t.Now) > 2.5f,
                "radius 2 + 0.15 x power (" + cast.RadiusNow(t.Now).ToString("0.00") + " at power " + cast.power.ToString("0.00") + ")");
            foreach (int w in WaitFor(() => Tests_Goku.CastOf(host) == null, 120, 5)) yield return w;
            t.Check(Tests_Goku.CastOf(host) == null, "the throw let Goku go");
            yield return 300;
            t.Log(RimArtTestContext.Describe(e1) + " | " + RimArtTestContext.Describe(e2) + " | " + RimArtTestContext.Describe(far));
            t.Check(t.Hurt(e1) && t.Hurt(e2), "the two hostiles under the dome were hit");
            foreach (Pawn e in new[] { e1, e2 })
                t.Log(e.LabelShort + ": " + cast.HitsOn(e) + " hits, " + e.health.hediffSet.hediffs.Count(h => h is Hediff_Injury) + " injuries, "
                      + (e.Dead ? "dead" : "health " + e.health.summaryHealth.SummaryHealthPercent.ToStringPercent()));
            t.Check((cast.HitsOn(e1) == 5 || e1.Dead) && (cast.HitsOn(e2) == 5 || e2.Dead), "each took 5 hits, or died on the way (" + cast.HitsOn(e1) + ", " + cast.HitsOn(e2) + ")");
            t.Check(cast.HitsOn(far) == 0, "the hostile outside the radius took none");
            t.Check(t.Untouched(ally), "the ally under the dome was not (" + RimArtTestContext.Describe(ally) + ")");
            t.Check(!wall.Destroyed && wall.HitPoints == wall.MaxHitPoints, "the wall under the dome was not");
            t.Check(t.Untouched(far), "the hostile 6 cells from the centre was not");
            t.Check(ability.CooldownTicksRemaining > 0, "the cooldown is spent");
            EndHost(record);
        }

        [RimArtTest("Goku", "spirit 3 a lender below 28 % Rest cannot lend, one that tires to it stops, and the ball stops at 60 power", 2400)]
        private static IEnumerable<int> SpiritBombKi(RimArtTestContext t)
        {
            Tests_Goku.Setup(t);
            yield return 5;
            Pawn host = Tests_Goku.Host(t, t.center, out EchoRecord record);
            Pawn exhausted = Tests_Goku.Ally(t, t.center + new IntVec3(-2, 0, 0));
            Pawn tiring = Tests_Goku.Ally(t, t.center + new IntVec3(-2, 0, 2));
            Pawn rested = Tests_Goku.Ally(t, t.center + new IntVec3(-2, 0, -2));
            yield return 2;
            SetRest(host, 1f);
            SetRest(exhausted, 0.2f);
            SetRest(tiring, 0.3f);
            SetRest(rested, 1f);
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuSpiritBomb);
            ability.QueueCastingJob(t.center + new IntVec3(8, 0, 0), LocalTargetInfo.Invalid);
            SpiritBombCast cast = null;
            foreach (int w in WaitFor(() => (cast = Tests_Goku.CastOf(host) as SpiritBombCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started")) { EndHost(record); yield break; }

            foreach (Pawn p in new[] { exhausted, tiring })
            {
                Job lend = JobMaker.MakeJob(GokuDefOf.AG_GokuLend, host);
                lend.playerForced = true;
                p.jobs.TryTakeOrderedJob(lend, JobTag.Misc);
            }
            yield return 5;
            t.Log(RimArtTestContext.Describe(exhausted) + " Rest " + GokuLifeEnergy.Rest(exhausted).ToString("0.000"));
            t.Check(!SpiritBombCast.IsLending(exhausted, host) && SpiritBombCast.RateOf(exhausted, false) == 0f, "the pawn at 20 % Rest cannot lend");
            t.Check(SpiritBombCast.IsLending(tiring, host), "the pawn at 30 % Rest lends");
            t.Log("the tired lender gives " + SpiritBombCast.RateOf(tiring, false).ToString("0.00") + "/s, Goku " + SpiritBombCast.RateOf(host, true).ToString("0.00") + "/s");
            foreach (int w in WaitFor(() => !SpiritBombCast.IsLending(tiring, host), 120, 5)) yield return w;
            t.Log(RimArtTestContext.Describe(tiring) + " Rest " + GokuLifeEnergy.Rest(tiring).ToString("0.000"));
            t.Check(!SpiritBombCast.IsLending(tiring, host), "it stopped by itself");
            t.Check(GokuLifeEnergy.Rest(tiring) < 0.28f && GokuLifeEnergy.Rest(tiring) > 0.26f, "at 28 % Rest (" + GokuLifeEnergy.Rest(tiring).ToStringPercent() + ")");

            // Near the cap: a rested lender tops it up, then everyone stops paying.
            cast.power = 59.5f;
            Job last = JobMaker.MakeJob(GokuDefOf.AG_GokuLend, host);
            last.playerForced = true;
            rested.jobs.TryTakeOrderedJob(last, JobTag.Misc);
            foreach (int w in WaitFor(() => cast.Full, 60, 1)) yield return w;
            yield return 5;
            t.Log("power " + cast.power.ToString("0.00") + ", " + RimArtTestContext.Describe(rested));
            t.Check(cast.Full && cast.power <= 60f, "the ball stopped at 60 (" + cast.power.ToString("0.00") + ")");
            t.Check(!SpiritBombCast.IsLending(rested, host), "the lender stopped when it was full");
            float hostRest = GokuLifeEnergy.Rest(host), lenderRest = GokuLifeEnergy.Rest(rested);
            yield return 60;
            t.Check(Mathf.Abs(GokuLifeEnergy.Rest(host) - hostRest) < 0.005f, "Goku pays no Rest while it is full (" + hostRest.ToString("0.000") + " -> " + GokuLifeEnergy.Rest(host).ToString("0.000") + ")");
            t.Check(cast.power <= 60f, "and it stays at 60");
            t.Check(Mathf.Abs(cast.RadiusNow(t.Now) - 11f) < 0.01f && Mathf.Abs(cast.HitDamageNow(t.Now) - 76f) < 0.1f,
                "a full ball: radius 11, 5 hits of 76 (" + cast.RadiusNow(t.Now).ToString("0.00") + ", " + cast.HitDamageNow(t.Now).ToString("0.0") + ")");
            cast.Cancel(false);
            EndHost(record);
        }

        [RimArtTest("Goku", "spirit 2 Cancel before the throw gives 30 back; a stun ends it spent")]
        private static IEnumerable<int> SpiritBombCancel(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Tests_Goku.Setup(t);
            yield return 5;
            Pawn host = Tests_Goku.Host(t, t.center, out EchoRecord record);
            yield return 2;
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuSpiritBomb);
            ability.QueueCastingJob(t.center + new IntVec3(8, 0, 0), LocalTargetInfo.Invalid);
            SpiritBombCast cast = null;
            foreach (int w in WaitFor(() => (cast = Tests_Goku.CastOf(host) as SpiritBombCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started")) { EndHost(record); yield break; }
            float paidDown = echoes.charge;
            cast.Cancel(false);
            yield return 2;
            t.Check(echoes.charge >= paidDown + 29.9f, "30 charge came back (" + paidDown.ToString("0.##") + " -> " + echoes.charge.ToString("0.##") + ")");
            t.Check(ability.CooldownTicksRemaining == 0, "the cooldown came back");

            ability.QueueCastingJob(t.center + new IntVec3(8, 0, 0), LocalTargetInfo.Invalid);
            cast = null;
            foreach (int w in WaitFor(() => (cast = Tests_Goku.CastOf(host) as SpiritBombCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the second channel started")) { EndHost(record); yield break; }
            paidDown = echoes.charge;
            host.stances.stunner.StunFor(60, null, false);
            foreach (int w in WaitFor(() => cast.broken, 30, 1)) yield return w;
            t.Check(cast.broken && cast.spent, "the stun ended it spent");
            t.Check(echoes.charge < paidDown + 1f, "the charge did not come back");
            t.Check(ability.CooldownTicksRemaining > 0, "the cooldown stays spent");
            EndHost(record);
        }
    }
}
