using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimArt
{
    /// <summary>
    /// Game tests for Unlimited Blade Works through the Shirou Echo (run with -quicktest -rimarttest=ubw):
    /// Origin: Blade as the Trial, the cast's charge, the chant, the take, the close and the return.
    /// </summary>
    public static class Tests_Ubw
    {
        private static EchoDef Shirou => DefDatabase<EchoDef>.GetNamed("AG_Echo_Shirou");
        private static AbilityDef Ubw => UbwDefOf.AG_Trace_UnlimitedBladeWorks;
        private static TraitDef OriginBlade => DefDatabase<TraitDef>.GetNamed("AG_OriginBlade");

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            GameComponent_UnlimitedBladeWorks.Instance.ResetForTests();
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            return echoes;
        }

        /// <summary>A drafted colonist made Shirou's Host and manifested, with a full pool.</summary>
        private static Pawn Host(RimArtTestContext t, GameComponent_Echoes echoes, out EchoRecord record)
        {
            Pawn host = t.Colonist(t.center);
            record = EchoUtility.ForceHost(Shirou, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            host.drafter.Drafted = true;
            return host;
        }

        private static UbwCast CastOf(Pawn pawn) => GameComponent_UnlimitedBladeWorks.Instance?.For(pawn);

        private static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 5)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        private static void LogPawns(RimArtTestContext t, params Pawn[] pawns)
        {
            foreach (Pawn p in pawns)
                t.Log(RimArtTestContext.Describe(p) + " map=" + (p.MapHeld == null ? "none" : p.MapHeld == t.map ? "home" : "world " + p.MapHeld.uniqueID));
        }

        [RimArtTest("Ubw", "trial 1 Origin: Blade grants nothing and is Shirou's Trial; manifested Shirou has the ability")]
        private static IEnumerable<int> Trial(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn pawn = t.Colonist(t.center);
            pawn.drafter.Drafted = false;
            RimArtTestContext.Hold(pawn);
            t.Check(!Shirou.trials[0].Met(pawn), "the Trial is not met without the trait");
            pawn.story.traits.GainTrait(new Trait(OriginBlade));
            TraitAbilityUtility.SyncAll(pawn);
            yield return 2;
            t.Check(pawn.abilities.GetAbility(Ubw) == null, "Origin: Blade alone does not grant Unlimited Blade Works");

            EchoUtility.Tune(Shirou, pawn);
            EchoRecord record = echoes.RecordFor(Shirou);
            t.Log(EchoGizmos.TrialsText(record));
            t.Check(EchoUtility.TrialsMet(record), "the Origin: Blade Trial is met");
            t.Check(record.offered, "the awakening letter was offered");
            t.Check(EchoUtility.Awaken(record), "awakened as Shirou");
            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "manifested");
            yield return 2;
            t.Check(pawn.abilities.GetAbility(Ubw) != null, "manifested Shirou has Unlimited Blade Works");
            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(pawn.abilities.GetAbility(Ubw) == null, "reverted, the ability is gone");
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Ubw", "cast 1 pays 40, takes the pawns within 6 cells, Close brings them back", 3000)]
        private static IEnumerable<int> CastTakeReturn(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, out EchoRecord record);
            Pawn near = t.Enemy(t.center + new IntVec3(4, 0, 0), armed: false);
            Pawn far = t.Enemy(t.center + new IntVec3(-10, 0, 0), armed: false);
            IntVec3 hostFrom = host.Position, nearFrom = near.Position;
            yield return 2;

            float before = echoes.charge;
            host.abilities.GetAbility(Ubw).QueueCastingJob(host, LocalTargetInfo.Invalid);
            UbwCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null && cast.Chanting, 180)) yield return w;
            if (!t.Check(cast != null && cast.Chanting, "the chant started (" + RimArtTestContext.Describe(host) + ")")) yield break;
            float spent = before - echoes.charge;
            t.Check(spent >= 40f && spent < 41f, "the pool paid 40 (" + spent.ToString("0.##") + " with upkeep)");
            t.Check(Math.Abs(cast.paid - 40f) < 0.01f, "the cast kept 40 to give back");

            cast.AskRelease(t.Now);
            t.Check(cast.releaseAfter == 1, "Release asked in verse 1");
            foreach (int w in WaitFor(() => cast.Standing || cast.fizzled || cast.broken, 900)) yield return w;
            LogPawns(t, host, near, far);
            if (!t.Check(cast.Standing, "the world stands")) yield break;
            Map world = cast.world;
            t.Check(host.Map == world, "the caster is in the world");
            t.Check(near.Map == world, "the enemy 4 cells away was taken");
            t.Check(far.Map == t.map, "the enemy 10 cells away stayed");
            t.Check(near.GetLord()?.LordJob is LordJob_AssaultColony, "the taken enemy fights in the world");
            t.Check(host.abilities.GetAbility(Ubw).GizmoDisabled(out string why), "the ability is disabled while the world stands (" + why + ")");

            yield return 60;
            cast.closeOrdered = true;
            foreach (int w in WaitFor(() => cast.returned, 1200)) yield return w;
            LogPawns(t, host, near, far);
            if (!t.Check(cast.returned, "everyone returned")) yield break;
            t.Check(host.Map == t.map && host.Position.DistanceTo(hostFrom) <= 2f, "the caster is back where it stood (" + hostFrom + ")");
            t.Check(near.Dead || (near.MapHeld == t.map && near.Position.DistanceTo(nearFrom) <= 2f), "the enemy is back where it stood (" + nearFrom + ")");
            t.Check(host.Drafted, "the caster is still drafted");
            foreach (int w in WaitFor(() => !Find.Maps.Contains(world), 300)) yield return w;
            t.Check(!Find.Maps.Contains(world), "the world was removed");
            t.Check(host.abilities.GetAbility(Ubw).CooldownTicksRemaining > 0, "the cooldown is spent");
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Ubw", "chant 1 Stop chanting and a move order each give the charge and the cooldown back")]
        private static IEnumerable<int> ChantRefund(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, out EchoRecord record);
            Ability ability = host.abilities.GetAbility(Ubw);
            yield return 2;

            // Stop chanting (the button's action).
            ability.QueueCastingJob(host, LocalTargetInfo.Invalid);
            UbwCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null && cast.Chanting, 180)) yield return w;
            if (!t.Check(cast != null && cast.Chanting, "the chant started")) yield break;
            float paidDown = echoes.charge;
            cast.Break(null);
            yield return 2;
            t.Check(echoes.charge >= paidDown + 39.9f, "40 charge came back (" + paidDown.ToString("0.##") + " -> " + echoes.charge.ToString("0.##") + ")");
            t.Check(ability.CooldownTicksRemaining == 0, "the cooldown came back");
            t.Check(host.CurJobDef != UbwDefOf.AG_UbwChant, "the chant job ended");
            t.Check(CastOf(host) == null, "no cast holds the caster");
            t.Check(!ability.GizmoDisabled(out string reason), "the ability can be cast again (" + reason + ")");

            // A move order.
            ability.QueueCastingJob(host, LocalTargetInfo.Invalid);
            cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null && cast.Chanting, 180)) yield return w;
            if (!t.Check(cast != null && cast.Chanting, "the second chant started")) yield break;
            paidDown = echoes.charge;
            host.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, host.Position + new IntVec3(0, 0, 3)), JobTag.DraftedOrder);
            foreach (int w in WaitFor(() => cast.broken, 30, 1)) yield return w;
            t.Check(cast.broken, "the move order broke the chant (" + RimArtTestContext.Describe(host) + ")");
            t.Check(echoes.charge >= paidDown + 39.9f, "40 charge came back (" + paidDown.ToString("0.##") + " -> " + echoes.charge.ToString("0.##") + ")");
            t.Check(ability.CooldownTicksRemaining == 0, "the cooldown came back");
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Ubw", "chant 2 reverting during the chant breaks it; the next manifest has the ability ready")]
        private static IEnumerable<int> RevertDuringChant(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, out EchoRecord record);
            yield return 2;
            host.abilities.GetAbility(Ubw).QueueCastingJob(host, LocalTargetInfo.Invalid);
            UbwCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null && cast.Chanting, 180)) yield return w;
            if (!t.Check(cast != null && cast.Chanting, "the chant started")) yield break;
            float paidDown = echoes.charge;

            EchoUtility.Revert(record, collapse: false);
            foreach (int w in WaitFor(() => cast.broken, 30, 1)) yield return w;
            t.Check(cast.broken, "the chant broke");
            t.Check(echoes.charge >= paidDown + 39.9f, "40 charge came back (" + paidDown.ToString("0.##") + " -> " + echoes.charge.ToString("0.##") + ")");
            t.Check(host.CurJobDef != UbwDefOf.AG_UbwChant, "the chant job ended");

            t.Check(EchoUtility.Manifest(record), "manifested again");
            yield return 2;
            Ability ability = host.abilities.GetAbility(Ubw);
            t.Check(ability != null && ability.CooldownTicksRemaining == 0, "the ability is back and ready");
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Ubw", "world 1 reverting while the world stands closes it and brings everyone back", 3000)]
        private static IEnumerable<int> RevertInWorld(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, out EchoRecord record);
            Pawn ally = t.Colonist(t.center + new IntVec3(0, 0, 3));
            yield return 2;
            host.abilities.GetAbility(Ubw).QueueCastingJob(host, LocalTargetInfo.Invalid);
            UbwCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null && cast.Chanting, 180)) yield return w;
            if (!t.Check(cast != null && cast.Chanting, "the chant started")) yield break;
            cast.AskRelease(t.Now);
            foreach (int w in WaitFor(() => cast.Standing || cast.fizzled || cast.broken, 900)) yield return w;
            if (!t.Check(cast.Standing && ally.Map == cast.world, "the world stands with the ally in it")) yield break;
            Map world = cast.world;

            yield return 30;
            EchoUtility.Revert(record, collapse: false);
            foreach (int w in WaitFor(() => cast.returned, 1200)) yield return w;
            LogPawns(t, host, ally);
            t.Check(cast.returned, "the world closed and everyone returned");
            t.Check(cast.WorldSecondsLeft(t.Now) > 5f, "it closed early (" + cast.WorldSecondsLeft(t.Now).ToString("0.#") + " s were left)");
            t.Check(host.Map == t.map && ally.Map == t.map, "both are home");
            foreach (int w in WaitFor(() => !Find.Maps.Contains(world), 300)) yield return w;
            t.Check(!Find.Maps.Contains(world), "the world was removed");
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Ubw", "world 2 an empty pool reverts Shirou and closes the world", 3000)]
        private static IEnumerable<int> PoolEmptyInWorld(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, out EchoRecord record);
            yield return 2;
            host.abilities.GetAbility(Ubw).QueueCastingJob(host, LocalTargetInfo.Invalid);
            UbwCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null && cast.Chanting, 180)) yield return w;
            if (!t.Check(cast != null && cast.Chanting, "the chant started")) yield break;
            cast.AskRelease(t.Now);
            foreach (int w in WaitFor(() => cast.Standing || cast.fizzled || cast.broken, 900)) yield return w;
            if (!t.Check(cast.Standing, "the world stands")) yield break;
            Map world = cast.world;

            echoes.charge = 0.05f;
            foreach (int w in WaitFor(() => cast.returned, 1500)) yield return w;
            LogPawns(t, host);
            t.Check(!record.manifested, "Shirou reverted");
            t.Check(cast.returned, "the world closed and the Host returned");
            t.Check(host.MapHeld == t.map, "the Host is home");
            foreach (int w in WaitFor(() => !Find.Maps.Contains(world), 300)) yield return w;
            t.Check(!Find.Maps.Contains(world), "the world was removed");
            EchoDevice.workingForTests = null;
        }
    }
}
