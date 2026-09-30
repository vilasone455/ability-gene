using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests for the Goku Echo (run with -quicktest -rimarttest=goku): Solar Flare, Instant
    /// Transmission, Kamehameha and its Warp, Spirit Bomb with a lender, and the downed-and-recovered
    /// Trial.
    /// </summary>
    public static class Tests_Goku
    {
        private static EchoDef Goku => GokuDefOf.AG_Echo_Goku;

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            GameComponent_Goku.Instance.ResetForTests();
            return t.ClearEchoes();
        }

        /// <summary>A drafted colonist made Goku's Host and manifested, with a full pool.</summary>
        private static Pawn Host(RimArtTestContext t, IntVec3 at, out EchoRecord record)
        {
            Pawn host = t.Host(Goku, at, out record);
            host.drafter.Drafted = true;
            return host;
        }

        private static Pawn Ally(RimArtTestContext t, IntVec3 at)
        {
            Pawn pawn = t.Colonist(at);
            pawn.drafter.Drafted = false;
            RimArtTestContext.Hold(pawn);
            return pawn;
        }

        private static GokuCast CastOf(Pawn pawn) => GameComponent_Goku.Instance?.For(pawn);

        private static bool Blind(Pawn pawn) => pawn.health.hediffSet.HasHediff(GokuDefOf.AG_GokuFlashBlind);

        // ---- Solar Flare ------------------------------------------------------------------------------------------

        [RimArtTest("Goku", "flare 1 stuns and blinds enemies in sight, not allies, not behind a wall, not outside the radius")]
        private static IEnumerable<int> Flare(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            Pawn near = t.Enemy(t.center + new IntVec3(3, 0, 0), armed: false);
            Pawn ally = Ally(t, t.center + new IntVec3(0, 0, 2));
            for (int z = -1; z <= 1; z++) t.Wall(t.center + new IntVec3(-2, 0, z), ThingDefOf.WoodLog);
            Pawn covered = t.Enemy(t.center + new IntVec3(-4, 0, 0), armed: false);
            Pawn far = t.Enemy(t.center + new IntVec3(9, 0, 0), armed: false);
            yield return 2;
            Ability flare = host.abilities.GetAbility(GokuDefOf.AG_GokuSolarFlare);
            if (!t.Check(flare != null && flare.CanCast, "Goku has Solar Flare and can cast it")) yield break;
            float before = echoes.charge;
            flare.QueueCastingJob(host, LocalTargetInfo.Invalid);
            foreach (int w in WaitFor(() => Stunned(near), 120, 1)) yield return w;
            t.Log(RimArtTestContext.Describe(host));
            t.Log(RimArtTestContext.Describe(near) + " blind=" + Blind(near));
            t.Log(RimArtTestContext.Describe(covered) + " blind=" + Blind(covered));
            t.Log(RimArtTestContext.Describe(far) + " blind=" + Blind(far));
            t.Check(Stunned(near) && Blind(near), "the enemy 3 cells away in sight is stunned and flash-blinded");
            t.Check(!Stunned(ally) && !Blind(ally), "the ally 2 cells away is not");
            t.Check(!Stunned(covered) && !Blind(covered), "the enemy behind the wall is not");
            t.Check(!Stunned(far) && !Blind(far), "the enemy 9 cells away is not");
            float spent = before - echoes.charge;
            t.Check(spent >= 3f && spent < 4f, "the pool paid 3 (" + spent.ToString("0.##") + " with upkeep)");
            t.Check(t.Untouched(near), "no damage");
            yield return 215;
            t.Check(!Stunned(near), "the stun ended after 3.5 s (" + RimArtTestContext.Describe(near) + ")");
            t.Check(Blind(near), "still blind at 4 s");
            EndHost(record);
        }

        // ---- Instant Transmission ---------------------------------------------------------------------------------

        [RimArtTest("Goku", "transmission 1 alone, with a hostile passenger (stunned), and with a downed ally")]
        private static IEnumerable<int> Transmission(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            // Undrafted and held: a drafted pawn punches an adjacent enemy, and a busy stance refuses the next cast.
            host.drafter.Drafted = false;
            RimArtTestContext.Hold(host);
            yield return 2;
            Ability it = host.abilities.GetAbility(GokuDefOf.AG_GokuInstantTransmission);
            if (!t.Check(it != null && it.CanCast, "Goku has Instant Transmission")) yield break;

            // Alone.
            IntVec3 dest = t.center + new IntVec3(8, 0, 5);
            float before = echoes.charge;
            t.Check(it.CanApplyOn((LocalTargetInfo)host), "Goku himself is a valid first target");
            it.QueueCastingJob(host, dest);
            foreach (int w in WaitFor(() => host.Position == dest, 90, 1)) yield return w;
            t.Log(RimArtTestContext.Describe(host));
            t.Check(host.Position == dest, "Goku arrived at " + dest);
            float spent = before - echoes.charge;
            t.Check(spent >= 2f && spent < 3f, "the pool paid 2 (" + spent.ToString("0.##") + ")");
            yield return 5;
            RimArtTestContext.Hold(host);

            // A hostile passenger, stunned beforehand so it cannot start a fight before the jump.
            yield return 60;
            it.ResetCooldown();
            Pawn enemy = t.Enemy(host.Position + new IntVec3(1, 0, 0), armed: false);
            enemy.stances.stunner.StunFor(45, null, false);
            IntVec3 dest2 = t.center + new IntVec3(-8, 0, -5);
            yield return 2;
            t.Check(it.CanApplyOn((LocalTargetInfo)enemy), "an adjacent enemy is a valid first target");
            it.QueueCastingJob(enemy, dest2);
            foreach (int w in WaitFor(() => host.Position == dest2, 90, 1)) yield return w;
            t.Log(RimArtTestContext.Describe(host));
            t.Log(RimArtTestContext.Describe(enemy));
            t.Check(host.Position == dest2, "Goku arrived at " + dest2);
            t.Check(enemy.Spawned && enemy.Position.AdjacentTo8WayOrInside(dest2) && enemy.Position != dest2, "the enemy landed beside him");
            t.Check(Stunned(enemy) && enemy.stances.stunner.StunTicksLeft > 50, "the enemy arrived stunned for 1.5 s (" + enemy.stances.stunner.StunTicksLeft + " ticks left)");
            if (enemy.Spawned) enemy.Destroy();
            yield return 5;
            RimArtTestContext.Hold(host);

            // A downed ally, carried lying.
            yield return 60;
            it.ResetCooldown();
            Pawn ally = Ally(t, host.Position + new IntVec3(0, 0, 1));
            HealthUtility.DamageUntilDowned(ally, false);
            IntVec3 dest3 = t.center + new IntVec3(0, 0, 8);
            yield return 2;
            if (!t.Check(ally.Downed, "the ally is downed")) { EndHost(record); yield break; }
            t.Check(it.CanApplyOn((LocalTargetInfo)ally), "a downed ally is a valid first target");
            it.QueueCastingJob(ally, dest3);
            foreach (int w in WaitFor(() => host.Position == dest3, 90, 1)) yield return w;
            t.Log(RimArtTestContext.Describe(host));
            t.Log(RimArtTestContext.Describe(ally));
            t.Check(host.Position == dest3, "Goku arrived at " + dest3);
            t.Check(ally.Spawned && ally.Position.AdjacentTo8WayOrInside(dest3) && ally.Position != dest3, "the downed ally landed beside him");
            t.Check(ally.Downed && !Stunned(ally), "the ally is still downed and not stunned");
            EndHost(record);
        }

        // ---- Kamehameha -------------------------------------------------------------------------------------------

        [RimArtTest("Goku", "kamehameha 1 hits the lane, stops at a wall, blasts at the end, pays 15", 2400)]
        private static IEnumerable<int> Kamehameha(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-11, 0, 0);
            Pawn host = Host(t, from, out EchoRecord record);
            Pawn e5 = t.Enemy(from + new IntVec3(5, 0, 0), armed: false);
            Pawn e10 = t.Enemy(from + new IntVec3(10, 0, 0), armed: false);
            Pawn beside = t.Enemy(from + new IntVec3(6, 0, 3), armed: false);
            var walls = new List<Thing>();
            for (int z = -1; z <= 1; z++) walls.Add(t.Wall(from + new IntVec3(18, 0, z), ThingDefOf.WoodLog));
            Pawn behind = t.Enemy(from + new IntVec3(22, 0, 0), armed: false);
            IntVec3 e5From = e5.Position;
            yield return 2;
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuKamehameha);
            if (!t.Check(ability != null && ability.CanCast, "Goku has Kamehameha")) yield break;
            float before = echoes.charge;
            ability.QueueCastingJob(from + new IntVec3(21, 0, 0), LocalTargetInfo.Invalid);
            KamehamehaCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as KamehamehaCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started (" + RimArtTestContext.Describe(host) + ")")) { EndHost(record); yield break; }
            float spent = before - echoes.charge;
            t.Check(spent >= 15f && spent < 16f, "the pool paid 15 (" + spent.ToString("0.##") + ")");
            t.Check(ability.GizmoDisabled(out string why), "the ability is disabled while channelling (" + why + ")");
            t.Check(host.CurJobDef == GokuDefOf.AG_GokuChannel, "the channel job runs");

            cast.fireOrdered = true;
            foreach (int w in WaitFor(() => cast.Firing, 300, 5)) yield return w;
            t.Log("fired at channelled " + cast.Channelled(t.Now).ToString("0.00") + " s; " + RimArtTestContext.Describe(host));
            if (!t.Check(cast.Firing, "the beam fired at full charge")) { EndHost(record); yield break; }
            t.Check(cast.Channelled(t.Now) >= 2.4f, "not before 2.5 s of channelling");
            for (int i = 0; i < 8; i++)
            {
                yield return 15;
                t.Log(RimArtTestContext.Describe(e5) + " | " + RimArtTestContext.Describe(e10));
            }
            yield return 60;
            t.Check(t.Hurt(e5) && t.Hurt(e10), "the enemies at 5 and 10 cells down the lane were hit");
            t.Check(e5.Dead || (e5.Spawned && e5.Position.x >= e5From.x + 1), "the enemy at 5 was pushed along the lane (" + e5From + " -> " + RimArtTestContext.Describe(e5) + ")");
            t.Check(t.Untouched(beside), "the enemy 3 cells across the lane was not hit");
            bool wallHit = walls.Any(wl => wl.Destroyed || wl.HitPoints < wl.MaxHitPoints);
            t.Check(wallHit, "the wall at 18 cells was hit (" + string.Join(", ", walls.Select(wl => wl.Destroyed ? "destroyed" : wl.HitPoints + "/" + wl.MaxHitPoints)) + ")");
            t.Check(t.Untouched(behind), "the enemy behind the wall was not hit (" + RimArtTestContext.Describe(behind) + ")");
            t.Check(host.Position == from, "Goku stood still (" + RimArtTestContext.Describe(host) + ")");
            foreach (int w in WaitFor(() => CastOf(host) == null, 200, 5)) yield return w;
            t.Check(CastOf(host) == null, "the cast let Goku go");
            t.Check(ability.CooldownTicksRemaining > 0, "the cooldown is spent");
            EndHost(record);
        }

        [RimArtTest("Goku", "kamehameha 2 Cancel gives the charge and cooldown back; a stun during the channel spends them")]
        private static IEnumerable<int> KamehamehaCancel(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            yield return 2;
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuKamehameha);

            // Cancel.
            ability.QueueCastingJob(t.center + new IntVec3(10, 0, 0), LocalTargetInfo.Invalid);
            KamehamehaCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as KamehamehaCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started")) { EndHost(record); yield break; }
            float paidDown = echoes.charge;
            cast.Cancel(false);
            yield return 2;
            t.Check(echoes.charge >= paidDown + 14.9f, "15 charge came back (" + paidDown.ToString("0.##") + " -> " + echoes.charge.ToString("0.##") + ")");
            t.Check(ability.CooldownTicksRemaining == 0, "the cooldown came back");
            t.Check(host.CurJobDef != GokuDefOf.AG_GokuChannel, "the channel job ended");
            t.Check(CastOf(host) == null, "no cast holds Goku");

            // A stun.
            ability.QueueCastingJob(t.center + new IntVec3(10, 0, 0), LocalTargetInfo.Invalid);
            cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as KamehamehaCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the second channel started")) { EndHost(record); yield break; }
            paidDown = echoes.charge;
            host.stances.stunner.StunFor(60, null, false);
            foreach (int w in WaitFor(() => cast.broken, 30, 1)) yield return w;
            t.Check(cast.broken && cast.spent, "the stun broke the channel and spent it (" + RimArtTestContext.Describe(host) + ")");
            t.Check(echoes.charge < paidDown + 1f, "the charge did not come back (" + echoes.charge.ToString("0.##") + ")");
            t.Check(ability.CooldownTicksRemaining > 0, "the cooldown stays spent");
            EndHost(record);
        }

        [RimArtTest("Goku", "warp 1 at full charge Warp jumps to the cell, spends Instant Transmission, and fires from there", 2400)]
        private static IEnumerable<int> Warp(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            IntVec3 landing = t.center + new IntVec3(-8, 0, -8), aim = t.center + new IntVec3(4, 0, -8);
            Pawn enemy = t.Enemy(t.center + new IntVec3(-2, 0, -8), armed: false);
            yield return 2;
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuKamehameha);
            Ability it = host.abilities.GetAbility(GokuDefOf.AG_GokuInstantTransmission);
            ability.QueueCastingJob(t.center + new IntVec3(10, 0, 0), LocalTargetInfo.Invalid);
            KamehamehaCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as KamehamehaCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started")) { EndHost(record); yield break; }
            t.Check(cast.WarpDisabled(t.Now) != null, "Warp is disabled before full charge (" + cast.WarpDisabled(t.Now) + ")");
            foreach (int w in WaitFor(() => cast.FullCharge(t.Now), 240, 5)) yield return w;
            t.Check(cast.WarpDisabled(t.Now) == null, "Warp is enabled at full charge with Instant Transmission ready");
            float before = echoes.charge;
            t.Check(cast.Warp(landing, aim), "Warp accepted");
            foreach (int w in WaitFor(() => cast.Firing, 60, 1)) yield return w;
            t.Log(RimArtTestContext.Describe(host));
            t.Check(host.Position == landing, "Goku appeared on the landing cell");
            t.Check(cast.Firing, "the beam fired from there");
            t.Check(it.CooldownTicksRemaining > 0, "Instant Transmission's cooldown is spent");
            float spent = before - echoes.charge;
            t.Check(spent >= 2f && spent < 3f, "the pool paid Instant Transmission's 2 (" + spent.ToString("0.##") + ")");
            yield return 150;
            t.Check(t.Hurt(enemy), "the enemy in the new lane was hit (" + RimArtTestContext.Describe(enemy) + ")");
            t.Check(host.Position == landing, "Goku stayed on the landing cell (" + RimArtTestContext.Describe(host) + ")");
            EndHost(record);
        }

        // ---- Spirit Bomb ------------------------------------------------------------------------------------------

        [RimArtTest("Goku", "spirit 1 a lender adds power, the throw hurts hostiles under the dome only, pays 30", 3000)]
        private static IEnumerable<int> SpiritBomb(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            Pawn lender = Ally(t, t.center + new IntVec3(-2, 0, 0));
            IntVec3 target = t.center + new IntVec3(8, 0, 0);
            Pawn e1 = t.Enemy(target, armed: false);
            Pawn e2 = t.Enemy(target + new IntVec3(0, 0, 2), armed: false);
            // The ally stands under the dome but not next to an enemy, and the enemies are held still: a punch would spoil the "untouched" check.
            Pawn ally = Ally(t, target + new IntVec3(-2, 0, -2));
            Thing wall = t.Wall(target + new IntVec3(1, 0, 1), ThingDefOf.WoodLog);
            Pawn far = t.Enemy(target + new IntVec3(0, 0, 6), armed: false);
            e1.stances.stunner.StunFor(900, null, false);
            e2.stances.stunner.StunFor(900, null, false);
            far.stances.stunner.StunFor(900, null, false);
            yield return 2;
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuSpiritBomb);
            if (!t.Check(ability != null && ability.CanCast, "Goku has Spirit Bomb")) yield break;
            float before = echoes.charge;
            ability.QueueCastingJob(target, LocalTargetInfo.Invalid);
            SpiritBombCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as SpiritBombCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started (" + RimArtTestContext.Describe(host) + ")")) { EndHost(record); yield break; }
            float spent = before - echoes.charge;
            t.Check(spent >= 30f && spent < 31f, "the pool paid 30 (" + spent.ToString("0.##") + ")");
            t.Check(!cast.CanThrow(t.Now), "Throw is not allowed before 3 s");

            Job lend = JobMaker.MakeJob(GokuDefOf.AG_GokuLend, host);
            lend.playerForced = true;
            lender.jobs.TryTakeOrderedJob(lend, JobTag.Misc);
            yield return 5;
            t.Check(SpiritBombCast.IsLending(lender, host), "the lender took the lend job (" + RimArtTestContext.Describe(lender) + ")");
            yield return 120;
            float power = cast.PowerNow(t.Now);
            t.Log("power " + power.ToString("0.00") + " after 2 s of lending, channelled " + cast.Channelled(t.Now).ToString("0.00") + " s, lenders " + cast.LenderCount);
            t.Check(cast.LenderCount == 1, "one lender counted");
            t.Check(power >= cast.Channelled(t.Now) + 1.8f, "the lender added about 2 power");
            lender.jobs.EndCurrentJob(JobCondition.InterruptForced);
            yield return 5;
            t.Check(cast.LenderCount == 0, "Stop lending closed the stint");
            float held = cast.PowerNow(t.Now);
            yield return 60;
            t.Check(cast.PowerNow(t.Now) - held < 1.3f, "power grows by 1 per second from Goku alone after that (" + (cast.PowerNow(t.Now) - held).ToString("0.00") + ")");

            t.Check(cast.CanThrow(t.Now), "Throw is allowed after 3 s");
            float radius = cast.RadiusNow(t.Now);
            t.Log("throwing at power " + cast.PowerNow(t.Now).ToString("0.0") + ", radius " + radius.ToString("0.00") + ", damage " + cast.DamageNow(t.Now).ToString("0"));
            cast.throwOrdered = true;
            foreach (int w in WaitFor(() => cast.Thrown, 30, 1)) yield return w;
            if (!t.Check(cast.Thrown, "thrown")) { EndHost(record); yield break; }
            t.Check(cast.RadiusNow(t.Now) >= 3f && cast.RadiusNow(t.Now) < 4f, "radius 2 + 0.25 x power (" + cast.RadiusNow(t.Now).ToString("0.00") + ")");
            foreach (int w in WaitFor(() => CastOf(host) == null, 120, 5)) yield return w;
            t.Check(CastOf(host) == null, "the throw let Goku go");
            yield return 300;
            t.Log(RimArtTestContext.Describe(e1) + " | " + RimArtTestContext.Describe(e2) + " | " + RimArtTestContext.Describe(far));
            t.Check(t.Hurt(e1) && t.Hurt(e2), "the two hostiles under the dome were hit");
            t.Check(t.Untouched(ally), "the ally under the dome was not (" + RimArtTestContext.Describe(ally) + ")");
            t.Check(!wall.Destroyed && wall.HitPoints == wall.MaxHitPoints, "the wall under the dome was not");
            t.Check(t.Untouched(far), "the hostile 6 cells from the centre was not");
            t.Check(ability.CooldownTicksRemaining > 0, "the cooldown is spent");
            EndHost(record);
        }

        [RimArtTest("Goku", "spirit 2 Cancel before the throw gives 30 back; a stun ends it spent")]
        private static IEnumerable<int> SpiritBombCancel(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            yield return 2;
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuSpiritBomb);
            ability.QueueCastingJob(t.center + new IntVec3(8, 0, 0), LocalTargetInfo.Invalid);
            SpiritBombCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as SpiritBombCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started")) { EndHost(record); yield break; }
            float paidDown = echoes.charge;
            cast.Cancel(false);
            yield return 2;
            t.Check(echoes.charge >= paidDown + 29.9f, "30 charge came back (" + paidDown.ToString("0.##") + " -> " + echoes.charge.ToString("0.##") + ")");
            t.Check(ability.CooldownTicksRemaining == 0, "the cooldown came back");

            ability.QueueCastingJob(t.center + new IntVec3(8, 0, 0), LocalTargetInfo.Invalid);
            cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as SpiritBombCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the second channel started")) { EndHost(record); yield break; }
            paidDown = echoes.charge;
            host.stances.stunner.StunFor(60, null, false);
            foreach (int w in WaitFor(() => cast.broken, 30, 1)) yield return w;
            t.Check(cast.broken && cast.spent, "the stun ended it spent");
            t.Check(echoes.charge < paidDown + 1f, "the charge did not come back");
            t.Check(ability.CooldownTicksRemaining > 0, "the cooldown stays spent");
            EndHost(record);
        }

        // ---- the Trial --------------------------------------------------------------------------------------------

        [RimArtTest("Goku", "deeds 1 getting back up after being downed counts toward the Trial; DEV Meet trials meets both")]
        private static IEnumerable<int> Deeds(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn pawn = Ally(t, t.center);
            Trial_DownedRecovered trial = Goku.trials.OfType<Trial_DownedRecovered>().First();
            t.Check(trial.Current(pawn) == 0f, "no recoveries yet");
            Hediff sleep = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn);
            sleep.Severity = 1f;
            pawn.health.AddHediff(sleep);
            yield return 2;
            if (!t.Check(pawn.Downed, "anesthetic downed the pawn (" + RimArtTestContext.Describe(pawn) + ")")) yield break;
            pawn.health.RemoveHediff(sleep);
            yield return 5;
            t.Check(!pawn.Downed, "the pawn got back up (" + RimArtTestContext.Describe(pawn) + ")");
            t.Check(trial.Current(pawn) == 1f, "one recovery counted (" + trial.Current(pawn) + ")");

            EchoUtility.Tune(Goku, pawn);
            EchoRecord record = echoes.RecordFor(Goku);
            t.Check(!EchoUtility.TrialsMet(record), "the trials are not met yet");
            DebugActions_Echo.MeetTrials(pawn);
            t.Log(EchoGizmos.TrialsText(record));
            t.Check(EchoUtility.TrialsMet(record), "DEV Meet trials met Melee 15 and the recoveries");
            t.Check(trial.Current(pawn) == 3f, "the recoveries were set to 3");
            t.Check(EchoUtility.Awaken(record), "awakened as Goku");
            t.Check(pawn.story.traits.HasTrait(DefDatabase<TraitDef>.GetNamed("Gourmand")), "the Host gained Gourmand");
            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "manifested");
            yield return 2;
            t.Check(pawn.abilities.GetAbility(GokuDefOf.AG_GokuKamehameha) != null, "manifested Goku has Kamehameha");
            EndHost(record);
        }

        // ---- Pawn height ------------------------------------------------------------------------------------------

        /// <summary>
        /// Close shots for the pawn height fit: Solar Flare with an enemy 3 cells east (the light at the
        /// head, the stun stars), then Instant Transmission with a stunned hostile passenger to a cell 6
        /// east (the brow glint, the touch glow, the slices at home and on arrival, the passenger's stars).
        /// </summary>
        [RimArtTest("Goku", "height 1 flare and transmission on real pawns (screenshots)", 2400)]
        private static IEnumerable<int> HeightFlareTransmission(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = HeightShots.Plain(Host(t, t.center, out EchoRecord record), strip: false);
            Pawn near = HeightShots.Target(t, t.center + new IntVec3(3, 0, 0));
            IntVec3 camera = t.center + new IntVec3(2, 0, 0);
            yield return 2;
            foreach (int step in HeightShots.Cast(t, host, GokuDefOf.AG_GokuSolarFlare, host, camera, "goku flare", near, 12, 45)) yield return step;
            yield return 30;
            near.Destroy();

            host.drafter.Drafted = false;
            RimArtTestContext.Hold(host);
            Pawn enemy = HeightShots.Target(t, host.Position + new IntVec3(1, 0, 0));
            enemy.stances.stunner.StunFor(60, null, false);
            IntVec3 dest = t.center + new IntVec3(6, 0, 0);
            camera = t.center + new IntVec3(3, 0, 0);
            yield return 2;
            foreach (int step in HeightShots.Cast(t, host, GokuDefOf.AG_GokuInstantTransmission, enemy, dest, camera, "goku transmission", enemy, 20, 33, 44, 80))
                yield return step;
            yield return 30;
            EndHost(record);
        }

        /// <summary>
        /// Close shots for the pawn height fit: Kamehameha 5 cells from an enemy (the aura and the ki ball
        /// held at full charge, the beam and the hit burst), then Spirit Bomb with a lender beside Goku and
        /// an ally under the dome (the lender's glow and ribbon, the ally's shell).
        /// </summary>
        [RimArtTest("Goku", "height 2 kamehameha and spirit bomb on real pawns (screenshots)", 4000)]
        private static IEnumerable<int> HeightBeamBomb(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-3, 0, 0);
            Pawn host = HeightShots.Plain(Host(t, from, out EchoRecord record), strip: false);
            Pawn e5 = HeightShots.Target(t, from + new IntVec3(5, 0, 0));
            IntVec3 camera = from + new IntVec3(3, 0, 0);
            yield return 2;
            Ability kame = host.abilities.GetAbility(GokuDefOf.AG_GokuKamehameha);
            kame.QueueCastingJob(from + new IntVec3(12, 0, 0), LocalTargetInfo.Invalid);
            KamehamehaCast beam = null;
            foreach (int w in WaitFor(() => (beam = CastOf(host) as KamehamehaCast) != null && beam.Channelling, 180, 1)) yield return w;
            if (!t.Check(beam != null && beam.Channelling, "the channel started")) { EndHost(record); yield break; }
            yield return 60;
            yield return HeightShots.Shoot(t, "goku kame charging", camera, host, e5);
            yield return 120;
            yield return HeightShots.Shoot(t, "goku kame full", camera, host, e5);
            beam.fireOrdered = true;
            foreach (int w in WaitFor(() => beam.Firing, 300, 1)) yield return w;
            yield return 4;
            yield return HeightShots.Shoot(t, "goku kame fire", camera, host, e5);
            yield return 5;
            yield return HeightShots.Shoot(t, "goku kame hit", camera, host, e5);
            foreach (int w in WaitFor(() => CastOf(host) == null, 300, 5)) yield return w;
            if (e5.Spawned) e5.Destroy();
            yield return 10;

            Pawn lender = HeightShots.Plain(Ally(t, from + new IntVec3(-2, 0, 0)));
            IntVec3 target = from + new IntVec3(8, 0, 0);
            Pawn spared = HeightShots.Plain(Ally(t, target + new IntVec3(-1, 0, -1)), Rot4.South);
            Ability bomb = host.abilities.GetAbility(GokuDefOf.AG_GokuSpiritBomb);
            bomb.QueueCastingJob(target, LocalTargetInfo.Invalid);
            SpiritBombCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as SpiritBombCast) != null && cast.Channelling, 180, 1)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the Spirit Bomb channel started")) { EndHost(record); yield break; }
            Job lend = JobMaker.MakeJob(GokuDefOf.AG_GokuLend, host);
            lend.playerForced = true;
            lender.jobs.TryTakeOrderedJob(lend, JobTag.Misc);
            yield return 60;
            yield return HeightShots.Shoot(t, "goku bomb lending", from, host, lender);
            foreach (int w in WaitFor(() => cast.CanThrow(t.Now), 300, 1)) yield return w;
            cast.throwOrdered = true;
            // The shells show while the dome is up, about 3.5 to 4 s after the throw for this little power.
            int thrown = t.Now;
            foreach (int at in new[] { 150, 190, 226 })
            {
                yield return thrown + at - t.Now;
                yield return HeightShots.Shoot(t, "goku bomb shell " + at, target, spared);
            }
            yield return 100;
            EndHost(record);
        }
    }
}
