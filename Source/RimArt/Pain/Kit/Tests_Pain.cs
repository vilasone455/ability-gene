using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Game tests for the Pain Echo (run with -quicktest -rimarttest=pain): the Echo and the Echo-only eyes, Shinra
    /// Tensei's charge and gap, Banshō Ten'in's pull, block and drag, and Black Receiver's rods, pin, breaks and stab.
    /// Screenshots at the moments of each picture.
    /// </summary>
    public static class Tests_Pain
    {
        private static EchoDef Pain => PainDefOf.AG_Echo_Pain;

        internal static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            startHealth.Clear();
            GameComponent_Pain.Instance.ResetForTests();
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            return echoes;
        }

        /// <summary>A colonist made Pain's Host and manifested, with a full pool, unarmed, drafted with fire at will off.</summary>
        internal static Pawn Host(RimArtTestContext t, GameComponent_Echoes echoes, IntVec3 at, out EchoRecord record)
        {
            Pawn host = t.Colonist(at);
            record = EchoUtility.ForceHost(Pain, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            host.equipment?.DestroyAllEquipment();
            host.drafter.Drafted = true;
            host.drafter.FireAtWill = false;
            Trait wimp = host.story?.traits?.GetTrait(TraitDefOf.Wimp);
            if (wimp != null) host.story.traits.RemoveTrait(wimp);
            return Noted(host);
        }

        private static readonly Dictionary<Pawn, float> startHealth = new Dictionary<Pawn, float>();

        private static Pawn Noted(Pawn pawn)
        {
            startHealth[pawn] = pawn.health.summaryHealth.SummaryHealthPercent;
            return pawn;
        }

        /// <summary>A hostile that stands still: unarmed, no apparel (armour would turn a hit to nothing), stunned.</summary>
        internal static Pawn Target(RimArtTestContext t, IntVec3 at, int stunTicks = 900)
        {
            Pawn pawn = t.Enemy(at, armed: false);
            pawn.apparel?.DestroyAll();
            Trait wimp = pawn.story?.traits?.GetTrait(TraitDefOf.Wimp);
            if (wimp != null) pawn.story.traits.RemoveTrait(wimp);
            pawn.stances.stunner.StunFor(stunTicks, null, false);
            return Noted(pawn);
        }

        private static float Start(Pawn pawn) => startHealth.TryGetValue(pawn, out float h) ? h : 1f;
        internal static bool Hurt(Pawn pawn) => pawn.Dead || pawn.Downed || pawn.health.summaryHealth.SummaryHealthPercent < Start(pawn) - 0.001f;
        internal static bool Stunned(Pawn pawn) => pawn.stances?.stunner?.Stunned == true;

        internal static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 1)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        internal static T Cast<T>(Pawn host) where T : PainCast => GameComponent_Pain.Instance?.Latest<T>(host);

        internal static void Finish(EchoRecord record)
        {
            if (record != null && record.manifested) EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        internal static Ability Ready(RimArtTestContext t, Pawn host, AbilityDef def)
        {
            Ability ability = host.abilities.GetAbility(def);
            t.Check(ability != null && ability.CanCast, "Pain can cast " + def.label + " (" + ability?.CanCast.Reason + ")");
            return ability != null && ability.CanCast ? ability : null;
        }

        private static string At(Pawn pawn) => pawn.Position.ToString();

        // ---- the Echo ----------------------------------------------------------------------------------------------

        [RimArtTest("Pain", "echo 1 Manifest gives the four abilities with costs 5 / 3 / 0 / 30; the eyes grant nothing; revert takes them back")]
        private static IEnumerable<int> Echo(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            AbilityDef[] four = { PainDefOf.AG_ShinraTensei, PainDefOf.AG_PainBanshoTenin, PainDefOf.AG_PainBlackReceiver, PainDefOf.AG_PainChibakuTensei };
            t.Check(four.All(def => Pain.abilities.Contains(def)), "the Echo lists the four abilities");
            t.Check(Pain.CastCost(PainDefOf.AG_ShinraTensei) == 5f && Pain.CastCost(PainDefOf.AG_PainBanshoTenin) == 3f
                && Pain.CastCost(PainDefOf.AG_PainBlackReceiver) == 0f && Pain.CastCost(PainDefOf.AG_PainChibakuTensei) == 30f,
                "cast costs 5 / 3 / 0 / 30");
            t.Check(Pain.upkeepPerHour == 15f, "upkeep 15 per hour");
            HediffDef repulsion = DefDatabase<HediffDef>.GetNamed("AG_RepulsionEye"), attraction = DefDatabase<HediffDef>.GetNamed("AG_AttractionEye");
            t.Check(repulsion.abilities.NullOrEmpty() && attraction.abilities.NullOrEmpty(), "the repulsion and attraction eyes grant no ability");
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            yield return 5;
            foreach (AbilityDef def in four) t.Check(host.abilities.GetAbility(def) != null, "Pain has " + def.label);
            t.Check(GameComponent_Shinra.HasEye(host), "Shinra Tensei counts him as able while manifested");
            Finish(record);
            yield return 5;
            t.Check(four.All(def => host.abilities.GetAbility(def) == null), "revert takes the four abilities back");
            t.Check(!GameComponent_Shinra.HasEye(host), "and Shinra Tensei with them");
        }

        // ---- Shinra Tensei -----------------------------------------------------------------------------------------

        [RimArtTest("Pain", "shinra 1 release takes the Echo's 5 charge and starts the 5 s gap that holds Banshō; with no charge it cancels")]
        private static IEnumerable<int> Shinra(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            ShinraPawnState s = GameComponent_Shinra.Instance.For(host);
            s.active = true;
            s.map = t.map;
            s.centre = host.Position.ToVector3Shifted();
            s.charge = new ShinraCharge();
            s.Release();
            t.Check(s.charge.releasing && Mathf.Abs(echoes.charge - 95f) < 0.01f, "release took 5 charge (" + echoes.charge.ToString("0.#") + ")");
            s.Cancel();
            PainKit.StartDevaGap(host);
            Ability bansho = host.abilities.GetAbility(PainDefOf.AG_PainBanshoTenin);
            t.Check(bansho.GizmoDisabled(out string why), "Banshō waits in the gap: " + why);
            t.Check(Mathf.Abs(PainKit.DevaGapLeft(host) - 5f) < 0.1f, "gap 5 s (" + PainKit.DevaGapLeft(host).ToString("0.00") + ")");

            GameComponent_Shinra.Instance.For(host).cooldownUntil = 0;
            echoes.charge = 2f;
            s.active = true;
            s.charge = new ShinraCharge();
            s.Release();
            t.Check(!s.active && !s.charge.releasing && Mathf.Abs(echoes.charge - 2f) < 0.01f, "with 2 charge the release cancels and takes nothing");
            Finish(record);
        }

        [RimArtTest("Pain", "shinra 2 the clip's body and hands are read each frame for the sleeves: hands on both sides, out past 0.3 at the burst (screenshots)")]
        private static IEnumerable<int> Sleeves(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            yield return 5;
            if (!t.Check(ShinraCastAnimation.Clip.TryStart(host, out CastClips.Handle clip), "the Shinra clip starts ("
                    + (ShinraCastAnimation.Clip.Missing ?? "Melee Animation present") + ")"))
            { Finish(record); yield break; }
            GameComponent_Shinra.Instance.Begin(host, clip);
            ShinraPawnState s = GameComponent_Shinra.Instance.For(host);
            yield return 40;
            t.Check(s.active && s.charge.Held, "holding at " + s.charge.time.ToString("0.00") + " s");
            bool body = clip.TryPart("BodyA", out Vector3 b), a = clip.TryPart("HandA", out Vector3 ha), c = clip.TryPart("HandB", out Vector3 hb);
            t.Check(body && a && c, "BodyA, HandA, HandB read: body " + b.ToString("F3") + ", hands " + ha.ToString("F3") + " " + hb.ToString("F3"));
            t.Check((ha.x - b.x) * (hb.x - b.x) < 0f, "one hand each side of the body");
            t.Check(ha.y > b.y + 0.04f && hb.y > b.y + 0.04f, "the hands are drawn over the pawn's layers, so the sleeves fit between");
            yield return t.ShotAs("shinra sleeves hold", host.Position, 4f);
            s.Release();
            for (int i = 0; i < 120 && s.active && s.charge.time < ShinraCharge.Burst + 0.1f; i++) yield return 1;
            clip.TryPart("BodyA", out b);
            clip.TryPart("HandA", out ha);
            clip.TryPart("HandB", out hb);
            t.Check(Mathf.Abs(ha.x - b.x) > 0.3f && Mathf.Abs(hb.x - b.x) > 0.3f, "at " + s.charge.time.ToString("0.00")
                + " s the hands are out " + Mathf.Abs(ha.x - b.x).ToString("0.00") + " / " + Mathf.Abs(hb.x - b.x).ToString("0.00"));
            yield return t.ShotAs("shinra sleeves burst", host.Position, 4f);
            s.Cancel();
            Finish(record);
        }

        // ---- Banshō Ten'in -----------------------------------------------------------------------------------------

        [RimArtTest("Pain", "bansho 1 pulls a raider 8 cells to the cell in front of Pain: 15 blunt, stunned 2 s, face-down; takes 3 charge; Shinra waits 5 s (screenshots)")]
        private static IEnumerable<int> Bansho(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-4, 0, 0);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            // Stunned only through the warmup: after the grip the pull holds it, then the slam's own 2 s stun.
            Pawn raider = Target(t, from + new IntVec3(8, 0, 0), 40);
            yield return 5;
            Ability pull = Ready(t, host, PainDefOf.AG_PainBanshoTenin);
            if (pull == null) { Finish(record); yield break; }
            pull.QueueCastingJob(raider, LocalTargetInfo.Invalid);
            foreach (int step in WaitFor(() => Cast<BanshoCast>(host) != null && Cast<BanshoCast>(host).Seconds(t.Now) >= 0.5f, 60)) yield return step;
            yield return t.ShotAs("bansho warm-up");
            foreach (int step in WaitFor(() => Cast<BanshoCast>(host)?.Fired == true, 60)) yield return step;
            BanshoCast cast = Cast<BanshoCast>(host);
            if (!t.Check(cast != null && cast.Fired, "the pull fired")) { Finish(record); yield break; }
            t.Check(Mathf.Abs(echoes.charge - 97f) < 0.01f, "took 3 charge (" + echoes.charge.ToString("0.#") + ")");
            t.Check(PainKit.DevaGapLeft(host) > 4.5f, "the Shinra gap started at the grip");
            t.Check(PainKit.Unmovable(raider), "the carried raider cannot be moved by anything else");
            foreach (int step in WaitFor(() => cast.Seconds(t.Now) >= cast.Lift + cast.Fly * 0.5f, 60)) yield return step;
            t.Log("mid-flight: " + RimArtTestContext.Describe(raider) + " drawn at " + raider.DrawPos.ToString("F2"));
            yield return t.ShotAs("bansho mid-flight");
            foreach (int step in WaitFor(() => cast.slammed, 120)) yield return step;
            IntVec3 front = from + new IntVec3(1, 0, 0);
            t.Check(raider.Position == front, "landed on the cell in front of Pain: " + At(raider) + " (want " + front + ")");
            t.Check(Hurt(raider), "the slam hurt it");
            t.Check(Stunned(raider), "stunned after the slam");
            t.Check(!cast.blocked && !cast.heavy, "not blocked, not dragged");
            yield return 20;
            yield return t.ShotAs("bansho face-down");
            t.Check(PainLooks.TryGet(raider, out PainLook look) && look.lying && look.facing == Rot4.North, "drawn face-down while stunned");
            t.Check(pull.OnCooldown && pull.CooldownTicksRemaining > 700, "15 s cooldown (" + pull.CooldownTicksRemaining + " ticks)");
            t.Check(host.Position == from, "Pain did not move");
            foreach (int step in WaitFor(() => !Stunned(raider), 180)) yield return step;
            t.Check(!Stunned(raider), "the stun ends");
            Finish(record);
        }

        [RimArtTest("Pain", "bansho 2 a raider standing in the line stops it: both take 8 blunt and a 1 s stun, the target drops short of it (screenshot)")]
        private static IEnumerable<int> Blocked(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-4, 0, 0);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            Pawn raider = Target(t, from + new IntVec3(8, 0, 0));
            // Standing and unstunned, so it counts as a standing pawn in the line.
            Pawn wall = t.Enemy(from + new IntVec3(4, 0, 0), armed: false);
            wall.apparel?.DestroyAll();
            Noted(wall);
            yield return 5;
            Ability pull = Ready(t, host, PainDefOf.AG_PainBanshoTenin);
            if (pull == null) { Finish(record); yield break; }
            pull.QueueCastingJob(raider, LocalTargetInfo.Invalid);
            foreach (int step in WaitFor(() => Cast<BanshoCast>(host)?.landed == true, 120)) yield return step;
            BanshoCast cast = Cast<BanshoCast>(host);
            if (!t.Check(cast != null && cast.landed, "the pull came to rest")) { Finish(record); yield break; }
            t.Check(cast.blocked && cast.blocker == wall, "blocked by the raider in the line (" + cast.blocker + ")");
            t.Check(raider.Position.x > wall.Position.x, "the target dropped short of it: " + At(raider) + " vs " + At(wall));
            t.Check(Hurt(raider) && Hurt(wall), "both hurt");
            t.Check(Stunned(raider) && Stunned(wall), "both stunned");
            yield return 6;
            yield return t.ShotAs("bansho blocked");
            Finish(record);
        }

        [RimArtTest("Pain", "bansho 3 a thrumbo (body size 4) is dragged half the distance on the floor, no slam")]
        private static IEnumerable<int> Heavy(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-4, 0, 0);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            Pawn beast = PawnGenerator.GeneratePawn(PawnKindDef.Named("Thrumbo"), Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false));
            GenSpawn.Spawn(beast, from + new IntVec3(9, 0, 0), t.map);
            RimArtTestContext.Hold(beast);
            beast.stances.stunner.StunFor(900, null, false);
            Noted(beast);
            yield return 5;
            Ability pull = Ready(t, host, PainDefOf.AG_PainBanshoTenin);
            if (pull == null) { Finish(record); yield break; }
            pull.QueueCastingJob(beast, LocalTargetInfo.Invalid);
            foreach (int step in WaitFor(() => Cast<BanshoCast>(host)?.landed == true, 240)) yield return step;
            BanshoCast cast = Cast<BanshoCast>(host);
            if (!t.Check(cast != null && cast.landed, "the drag came to rest")) { Finish(record); yield break; }
            t.Check(cast.heavy, "dragged, not lifted");
            int gap = beast.Position.x - host.Position.x;
            t.Check(gap >= 4 && gap <= 6, "moved half the way: " + gap + " cells from Pain (from 9)");
            yield return 10;
            t.Check(!Hurt(beast), "no slam");
            Finish(record);
        }

        // ---- Black Receiver ----------------------------------------------------------------------------------------

        [RimArtTest("Pain", "receiver 1 three rods in a raider 6 cells away: 1, 2, 3, pinned; no abilities; not moved by a push or a pull; charges 3 -> 0 (screenshots)", 1500)]
        private static IEnumerable<int> Rods(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-3, 0, 0);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            Pawn raider = Target(t, from + new IntVec3(6, 0, 0), 60);
            raider.abilities?.GainAbility(PainDefOf.AG_PainBanshoTenin);
            Pawn other = Target(t, from + new IntVec3(2, 0, 3));
            yield return 5;
            Ability rod = Ready(t, host, PainDefOf.AG_PainBlackReceiver);
            if (rod == null) { Finish(record); yield break; }
            t.Check(rod.RemainingCharges == 3, "3 charges");
            for (int n = 1; n <= 3; n++)
            {
                int before = PainRods.Count(raider);
                rod.QueueCastingJob(raider, LocalTargetInfo.Invalid);
                if (n == 1)
                {
                    foreach (int step in WaitFor(() => Cast<BlackReceiverCast>(host)?.Fired == true, 60)) yield return step;
                    yield return 3;
                    yield return t.ShotAs("receiver in flight");
                }
                foreach (int step in WaitFor(() => PainRods.Count(raider) > before, 90)) yield return step;
                t.Check(PainRods.Count(raider) == n, "rod " + n + " in (" + PainRods.Count(raider) + ")");
                if (n == 1)
                {
                    Ability its = raider.abilities?.GetAbility(PainDefOf.AG_PainBanshoTenin);
                    t.Check(its != null && !its.CanCast.Accepted, "a rodded pawn cannot use abilities (" + its?.CanCast.Reason + ")");
                    t.Check(raider.health.capacities.GetLevel(PawnCapacityDefOf.Moving) <= 0.76f, "moving x0.75");
                    yield return t.ShotAs("receiver one rod");
                }
                foreach (int step in WaitFor(() => GameComponent_Pain.Instance.Holding(host, t.Now) == null && host.CurJobDef != PainDefOf.AG_CastPain, 60)) yield return step;
            }
            t.Check(PainRods.Pinned(raider) && Stunned(raider), "three rods: pinned and held");
            t.Check(rod.RemainingCharges == 0, "no charges left (" + rod.RemainingCharges + ")");
            t.Check(Hurt(raider), "the rods hurt it");
            t.Check(BanshoProblem(host, raider) != null, "Banshō refuses the pinned pawn: " + BanshoProblem(host, raider));

            IntVec3 pinnedAt = raider.Position, otherAt = other.Position;
            var push = new ShinraPawnState { pawn = host, map = t.map, centre = host.Position.ToVector3Shifted(), charge = new ShinraCharge { ticks = ShinraCharge.FullTicks } };
            ShinraCombat.Push(push);
            t.Check(raider.Position == pinnedAt, "a Shinra push does not move the pinned raider (" + At(raider) + ")");
            t.Check(other.Position != otherAt, "it does move the other one (" + otherAt + " -> " + At(other) + ")");
            yield return 20;
            yield return t.ShotAs("receiver pinned");
            t.Check(PainLooks.TryGet(raider, out PainLook look) && look.lying && look.facing == Rot4.South, "drawn lying on its back");
            Finish(record);
        }

        private static string BanshoProblem(Pawn host, Pawn target) => CompAbilityEffect_BanshoTenin.Problem(host, target);

        [RimArtTest("Pain", "receiver 2 each rod breaks 8 s after it landed and the pin ends at 2; leaving hero form breaks every rod")]
        private static IEnumerable<int> Breaks(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-3, 0, 0);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            Pawn raider = Target(t, from + new IntVec3(5, 0, 0));
            yield return 5;
            Hediff_PainRods rods = Hediff_PainRods.For(raider);
            int now = t.Now;
            for (int i = 0; i < 3; i++) rods.Add(host, Vector2.left, false, now - i);
            t.Check(rods.rods.Count == 3 && PainRods.Pinned(raider), "three rods, pinned");
            rods.Add(host, Vector2.left, false, now);
            t.Check(rods.rods.Count == 3, "a 4th replaces the oldest (" + rods.rods.Count + ")");
            // Age the oldest to 1 tick short of 8 s.
            PainRod oldest = rods.rods.OrderBy(r => r.landTick).First();
            oldest.landTick = t.Now - PainKit.ReceiverProps.RodTicks + 2;
            yield return 5;
            t.Check(rods.rods.Count == 2 && !PainRods.Pinned(raider), "the oldest broke at 8 s; the pin ended (" + rods.rods.Count + " left)");
            Finish(record);
            yield return 3;
            t.Check(PainRods.Count(raider) == 0 && PainRods.Of(raider) == null, "leaving hero form broke every rod and removed the hediff");
        }

        [RimArtTest("Pain", "receiver 3 at 1.5 cells or less Pain stabs: the rod lands on the fire tick; a pawn in the line of a throw takes the rod (screenshot)")]
        private static IEnumerable<int> Stab(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-3, 0, 0);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            yield return 5;
            Ability rod = Ready(t, host, PainDefOf.AG_PainBlackReceiver);
            if (rod == null) { Finish(record); yield break; }
            // Spawned and targeted on the same tick: a drafted pawn in a Wait job punches an adjacent hostile at
            // once, and the cast job cannot start while that melee cooldown lasts.
            Pawn near = Target(t, from + new IntVec3(1, 0, 0));
            rod.QueueCastingJob(near, LocalTargetInfo.Invalid);
            t.Log("after the order: " + RimArtTestContext.Describe(host));
            foreach (int step in WaitFor(() => Cast<BlackReceiverCast>(host)?.Fired == true, 60)) yield return step;
            BlackReceiverCast cast = Cast<BlackReceiverCast>(host);
            t.Check(cast != null && cast.stab && cast.hit == near && cast.landTick == cast.fireTick, "stabbed on the fire tick");
            t.Check(PainRods.Count(near) == 1, "one rod in");
            yield return 20;
            yield return t.ShotAs("receiver stab");

            // A second raider standing in the line takes the throw meant for the one behind it.
            near.Destroy();
            // Back in his Wait job he punched the stabbed raider next to him; the next cast waits out that melee cooldown.
            foreach (int step in WaitFor(() => !(host.stances.curStance is Stance_Cooldown), 240)) yield return step;
            Pawn far = Target(t, from + new IntVec3(7, 0, 0));
            Pawn inLine = t.Enemy(from + new IntVec3(4, 0, 0), armed: false);
            inLine.apparel?.DestroyAll();
            rod.QueueCastingJob(far, LocalTargetInfo.Invalid);
            foreach (int step in WaitFor(() => PainRods.Count(inLine) + PainRods.Count(far) > 0, 90)) yield return step;
            t.Check(PainRods.Count(inLine) == 1 && PainRods.Count(far) == 0, "the pawn in the line took it (in line " + PainRods.Count(inLine) + ", behind " + PainRods.Count(far) + ")");
            Finish(record);
        }
    }
}
