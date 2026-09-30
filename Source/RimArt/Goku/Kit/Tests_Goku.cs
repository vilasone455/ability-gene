using System;
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
    /// Game tests for the Goku Echo (run with -quicktest -rimarttest=goku): Solar Flare, Kamehameha
    /// and its Warp, and the downed-and-recovered Trial. Instant Transmission's are in
    /// Tests_GokuTransmission.cs, Spirit Bomb's in Tests_GokuSpiritBomb.cs.
    /// </summary>
    public static class Tests_Goku
    {
        private static EchoDef Goku => GokuDefOf.AG_Echo_Goku;

        internal static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            GameComponent_Goku.Instance.ResetForTests();
            return t.ClearEchoes();
        }

        /// <summary>A drafted colonist made Goku's Host and manifested, with a full pool.</summary>
        internal static Pawn Host(RimArtTestContext t, IntVec3 at, out EchoRecord record)
        {
            Pawn host = t.Host(Goku, at, out record);
            host.drafter.Drafted = true;
            return host;
        }

        internal static Pawn Ally(RimArtTestContext t, IntVec3 at)
        {
            Pawn pawn = t.Colonist(at);
            pawn.drafter.Drafted = false;
            RimArtTestContext.Hold(pawn);
            return pawn;
        }

        internal static GokuCast CastOf(Pawn pawn) => GameComponent_Goku.Instance?.For(pawn);

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
            // The farthest cell the pushed pawn reaches: once it lands it walks off on a job of its own.
            int e5FarX = e5From.x;
            for (int i = 0; i < 8; i++)
            {
                yield return 15;
                if (e5.Spawned) e5FarX = Math.Max(e5FarX, e5.Position.x);
                t.Log(RimArtTestContext.Describe(e5) + " | " + RimArtTestContext.Describe(e10));
            }
            yield return 60;
            t.Check(t.Hurt(e5) && t.Hurt(e10), "the enemies at 5 and 10 cells down the lane were hit");
            t.Check(e5.Dead || e5FarX >= e5From.x + 1, "the enemy at 5 was pushed along the lane (" + e5From + " -> x " + e5FarX + "; now " + RimArtTestContext.Describe(e5) + ")");
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
            // Nobody within 4 cells of the landing cell (the enemy is 6 away): the lock is an empty cell's, about 2.5 s.
            int lockTicks = Mathf.RoundToInt(GokuTransmissionLock.Seconds(host, host.Position, landing, null, out Pawn locked, out _) * 60f);
            t.Log("warp lock onto " + (locked?.LabelShort ?? "nobody") + ": " + lockTicks + " ticks");
            t.Check(cast.Warp(landing, aim), "Warp accepted");
            yield return lockTicks / 2;
            t.Check(host.Position == t.center && cast.Locking(t.Now), "halfway through the lock Goku still holds the ball where he channelled (" + RimArtTestContext.Describe(host) + ")");
            foreach (int w in WaitFor(() => cast.Firing, lockTicks + 60, 1)) yield return w;
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

        [RimArtTest("Goku", "warp 2 Cancel while Goku locks onto the warp cell gives Kamehameha's and Instant Transmission's charge and cooldown back", 2400)]
        private static IEnumerable<int> WarpCancel(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            IntVec3 landing = t.center + new IntVec3(-10, 0, -10), aim = t.center + new IntVec3(0, 0, -10);
            yield return 2;
            Ability ability = host.abilities.GetAbility(GokuDefOf.AG_GokuKamehameha);
            Ability it = host.abilities.GetAbility(GokuDefOf.AG_GokuInstantTransmission);
            float start = echoes.charge;
            ability.QueueCastingJob(t.center + new IntVec3(10, 0, 0), LocalTargetInfo.Invalid);
            KamehamehaCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host) as KamehamehaCast) != null && cast.Channelling, 180, 5)) yield return w;
            if (!t.Check(cast != null && cast.Channelling, "the channel started")) { EndHost(record); yield break; }
            foreach (int w in WaitFor(() => cast.FullCharge(t.Now), 240, 5)) yield return w;
            t.Check(cast.Warp(landing, aim), "Warp accepted");
            yield return 20;
            float paidDown = echoes.charge;
            t.Log("paid " + (start - paidDown).ToString("0.##") + " with upkeep; " + RimArtTestContext.Describe(host));
            t.Check(cast.Locking(t.Now), "Goku is locking onto the cell");
            t.Check(it.CooldownTicksRemaining > 0, "Instant Transmission's cooldown was taken");
            cast.Cancel(false);
            yield return 2;
            t.Check(echoes.charge >= paidDown + 16.9f, "15 + 2 charge came back (" + paidDown.ToString("0.##") + " -> " + echoes.charge.ToString("0.##") + ")");
            t.Check(ability.CooldownTicksRemaining == 0 && it.CooldownTicksRemaining == 0, "both cooldowns came back");
            t.Check(host.Position == t.center && CastOf(host) == null, "Goku stayed and no cast holds him (" + RimArtTestContext.Describe(host) + ")");
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
