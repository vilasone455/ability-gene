using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Game tests for Pain's Chibaku Tensei cast from his Echo (run with -quicktest -rimarttest=chibaku, with the
    /// ground and ball tests of <see cref="Tests_ChibakuPlates"/>): the cost and cooldown, the launch and hand-over to
    /// the ball, Pain held until it forms, the Shinra Tensei / Banshō Ten'in lock, Pain and a pinned pawn left on their
    /// ground, the early burst when Pain goes down, and the Release button.
    /// </summary>
    public static class Tests_ChibakuTensei
    {
        private static AbilityDef Def => PainDefOf.AG_PainChibakuTensei;

        private static float Damage(Pawn p) => p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);

        /// <summary>The Chibaku arena with Pain's Host manifested at <paramref name="from"/>, and no ball on the map.</summary>
        private static Pawn Arena(RimArtTestContext t, IntVec3 from, out GameComponent_Echoes echoes, out EchoRecord record, out MapComponent_ChibakuPlates component)
        {
            echoes = Tests_Pain.Setup(t);
            component = MapComponent_ChibakuPlates.Of(t.map);
            component.Stop();
            Tests_ChibakuPlates.Arena(t);
            return Tests_Pain.Host(t, echoes, from, out record);
        }

        /// <summary>Ticks until the ball's clock reads <paramref name="seconds"/>.</summary>
        private static int Until(MapComponent_ChibakuPlates component, float seconds) => Mathf.Max(0, Mathf.CeilToInt((seconds - component.LiveSeconds) * 60f));

        [RimArtTest("Chibaku", "tensei 1 Pain casts it 10 cells away: 30 charge, 1 day cooldown; two raiders and a colonist are taken, held and dropped hurt; Pain stands until it forms; Shinra and Banshō wait for the burst (screenshots)", 2400)]
        private static IEnumerable<int> Cast(RimArtTestContext t)
        {
            IntVec3 c = t.center, from = c + new IntVec3(-10, 0, 0), view = c + new IntVec3(-4, 0, 1);
            Pawn host = Arena(t, from, out GameComponent_Echoes echoes, out EchoRecord record, out MapComponent_ChibakuPlates component);
            // Stunned until the pull holds them, so they do not walk off the circle first.
            var taken = new List<Pawn> { Tests_Pain.Target(t, c + new IntVec3(1, 0, 1), 300), Tests_Pain.Target(t, c + new IntVec3(-2, 0, 2), 300) };
            Pawn colonist = t.Colonist(c + new IntVec3(0, 0, -2));
            colonist.drafter.FireAtWill = false;
            taken.Add(colonist);
            yield return 5;

            Ability ability = Tests_Pain.Ready(t, host, Def);
            Ability bansho = host.abilities.GetAbility(PainDefOf.AG_PainBanshoTenin), receiver = host.abilities.GetAbility(PainDefOf.AG_PainBlackReceiver);
            if (ability == null || bansho == null || receiver == null) { Tests_Pain.Finish(record); yield break; }
            ability.QueueCastingJob(new LocalTargetInfo(c), LocalTargetInfo.Invalid);
            foreach (int step in Tests_Pain.WaitFor(() => Tests_Pain.Cast<ChibakuCast>(host)?.Seconds(t.Now) >= .75f, 90)) yield return step;
            yield return t.ShotAs("chibaku-tensei-1-warm-up", view, 12f);
            yield return t.ShotAs("chibaku-tensei-1b-hands-cupped", from, 4f);
            foreach (int step in Tests_Pain.WaitFor(() => Tests_Pain.Cast<ChibakuCast>(host)?.Fired == true, 90)) yield return step;
            ChibakuCast cast = Tests_Pain.Cast<ChibakuCast>(host);
            if (!t.Check(cast != null && cast.Fired, "the core was launched")) { Tests_Pain.Finish(record); yield break; }
            t.Check(Mathf.Abs(echoes.charge - 70f) < .5f, "took 30 charge (" + echoes.charge.ToString("0.#") + ")");
            t.Check(ability.OnCooldown && ability.CooldownTicksRemaining > 59000, "1 day cooldown (" + ability.CooldownTicksRemaining + " ticks)");
            t.Log($"the core flies {cast.run:0.00} cells in {cast.Arrive - cast.LaunchAt:0.00} s; formed at {cast.Formed:0.00} s, Pain free at {cast.Free:0.00} s on the cast's clock");
            foreach (int step in Tests_Pain.WaitFor(() => cast.Seconds(t.Now) >= (cast.LaunchAt + cast.Arrive) / 2f, 120)) yield return step;
            yield return t.ShotAs("chibaku-tensei-2-core-flying", view, 12f);

            foreach (int step in Tests_Pain.WaitFor(() => cast.handed, 120)) yield return step;
            ChibakuBall ball = component.Ball;
            if (!t.Check(component.Live && component.Caster == host && ball != null, "the ball began over the cell when the core arrived")) { Tests_Pain.Finish(record); yield break; }
            t.Check(host.CurJobDef == PainDefOf.AG_CastPain, "Pain holds his hand up (the cast job)");
            yield return Until(component, ChibakuBall.Pull + 1.2f);
            yield return t.ShotAs("chibaku-tensei-3-pull", view, 12f);
            yield return t.ShotAs("chibaku-tensei-3b-seal", from, 4f);

            yield return Until(component, ChibakuBall.Formed - .1f);
            foreach (Pawn p in taken) t.Check(!p.Spawned && p.ParentHolder == component, p.LabelShort + " is in the ball");
            t.Check(host.Spawned && host.Position == from && host.CurJobDef == PainDefOf.AG_CastPain, "Pain stands where he cast, hand still up");
            t.Check(bansho.GizmoDisabled(out string why) && why.Contains("Chibaku"), "Banshō Ten'in waits (" + why + ")");
            t.Check(PainKit.ChibakuLock(host) != null && PainKit.ChibakuLeft(host) > 12f, "Shinra Tensei waits: " + PainKit.ChibakuLock(host));

            yield return Until(component, ChibakuBall.Formed + ChibakuCast.ArmDownAfter + ChibakuCast.ArmDownTime + .2f);
            t.Check(host.CurJobDef != PainDefOf.AG_CastPain, "Pain is free once the ball has formed and his hand is down (" + host.CurJobDef?.defName + ")");
            t.Check(!receiver.GizmoDisabled(out string receiverWhy), "Black Receiver can be used while the ball holds (" + receiverWhy + ")");
            t.Check(bansho.GizmoDisabled(out why) && why.Contains("Chibaku"), "Banshō Ten'in still waits");
            yield return t.ShotAs("chibaku-tensei-4-held", view, 12f);
            yield return t.ShotAs("chibaku-tensei-4a-ball-close", c + new IntVec3(0, 0, 3), 8f);
            yield return Until(component, ball.Crack - .5f);
            yield return t.ShotAs("chibaku-tensei-4b-hairline-cracks", c + new IntVec3(0, 0, 3), 8f);
            yield return Until(component, ball.Crack + .3f);
            yield return t.ShotAs("chibaku-tensei-4c-cracks-open", c + new IntVec3(0, 0, 3), 8f);

            yield return Until(component, ball.Burst + ball.FallTime + .1f);
            foreach (Pawn p in taken)
            {
                if (p.Dead) { t.Log(p.LabelShort + " died of the crush and the fall"); continue; }
                t.Log($"{p.LabelShort}: {Damage(p):0.#} damage, {(p.Downed ? "down" : p.stances.stunner.Stunned ? "stunned" : "up")}, {(p.Position - c).LengthHorizontal:0.0} cells from the middle");
                t.Check(p.Spawned && Damage(p) > 0f && (p.stances.stunner.Stunned || p.Downed), p.LabelShort + " landed hurt and stunned (or down)");
            }
            t.Check(PainKit.ChibakuLeft(host) == 0f && !bansho.GizmoDisabled(out why), "after the burst Banshō Ten'in is free (" + why + ")");
            yield return t.ShotAs("chibaku-tensei-5-landed", view, 12f);
            yield return Until(component, ball.End + .2f);
            t.Check(!component.Live && component.Inner.Count == 0, "the ball ended with nothing left inside");
            Tests_Pain.Finish(record);
        }

        [RimArtTest("Chibaku", "tensei 2 Pain inside the circle and a raider pinned by 3 rods stay on their ground; a raider beside them is taken (screenshot)", 1500)]
        private static IEnumerable<int> Islands(RimArtTestContext t)
        {
            IntVec3 c = t.center, from = c + new IntVec3(-3, 0, 0);
            Pawn host = Arena(t, from, out _, out EchoRecord record, out MapComponent_ChibakuPlates component);
            IntVec3 pinnedAt = c + new IntVec3(2, 0, -1);
            Pawn pinned = Tests_Pain.Target(t, pinnedAt, 30), free = Tests_Pain.Target(t, c + new IntVec3(0, 0, 3), 300);
            Hediff_PainRods rods = Hediff_PainRods.For(pinned);
            for (int i = 0; i < 3; i++) rods.Add(host, Vector2.left, false, t.Now);
            t.Check(PainRods.Pinned(pinned), "three rods pin the raider");
            yield return 5;

            Ability ability = Tests_Pain.Ready(t, host, Def);
            if (ability == null) { Tests_Pain.Finish(record); yield break; }
            ability.QueueCastingJob(new LocalTargetInfo(c), LocalTargetInfo.Invalid);
            foreach (int step in Tests_Pain.WaitFor(() => Tests_Pain.Cast<ChibakuCast>(host)?.handed == true, 150)) yield return step;
            ChibakuGround ground = component.Ground;
            if (!t.Check(component.Live && ground != null, "the ball began")) { Tests_Pain.Finish(record); yield break; }
            t.Check(ground.plates.Any(p => p.anchored && p.cells.Contains(from)), "the plate under Pain stays");
            t.Check(ground.plates.Any(p => p.anchored && p.cells.Contains(pinnedAt)), "the plate under the pinned raider stays");

            yield return Until(component, ChibakuBall.Formed + .2f);
            t.Check(host.Spawned && host.Position == from && !Tests_Pain.Stunned(host), "Pain is still on his ground, not held by the pull");
            t.Check(pinned.Spawned && pinned.Position == pinnedAt, "the pinned raider is still where it was pinned");
            t.Check(!free.Spawned && free.ParentHolder == component, "the free raider is in the ball");
            yield return t.ShotAs("chibaku-tensei-6-islands", c, 10f);
            component.Stop();
            t.Check(free.Spawned, "stopping the ball puts the raider down");
            Tests_Pain.Finish(record);
        }

        [RimArtTest("Chibaku", "tensei 3 Pain downed while the ball holds: it bursts at once and the raider inside takes the crush only for the time held", 1500)]
        private static IEnumerable<int> Broken(RimArtTestContext t)
        {
            IntVec3 c = t.center, from = c + new IntVec3(-10, 0, 0);
            Pawn host = Arena(t, from, out _, out EchoRecord record, out MapComponent_ChibakuPlates component);
            Pawn raider = Tests_Pain.Target(t, c + new IntVec3(1, 0, 0), 300);
            yield return 5;

            Ability ability = Tests_Pain.Ready(t, host, Def);
            if (ability == null) { Tests_Pain.Finish(record); yield break; }
            ability.QueueCastingJob(new LocalTargetInfo(c), LocalTargetInfo.Invalid);
            foreach (int step in Tests_Pain.WaitFor(() => Tests_Pain.Cast<ChibakuCast>(host)?.handed == true, 150)) yield return step;
            ChibakuBall ball = component.Ball;
            if (!t.Check(component.Live && ball != null, "the ball began")) { Tests_Pain.Finish(record); yield break; }
            yield return Until(component, ChibakuBall.Formed + 2f);
            t.Check(!raider.Spawned, "the raider is in the ball");
            float fullBurst = ball.Burst;

            Hediff anesthetic = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, host);
            anesthetic.Severity = 1f;
            host.health.AddHediff(anesthetic);
            yield return 2;
            t.Check(host.Downed, "Pain is down");
            t.Check(ball.Broken && ball.Burst < fullBurst - 5f, $"the ball broke: it bursts at {ball.Burst:0.00} s instead of {fullBurst:0.00} s");
            t.Check(PainKit.ChibakuLeft(host) <= ChibakuBall.CrackTime + .1f, "the lock ends with the burst (" + PainKit.ChibakuLeft(host).ToString("0.00") + " s left)");

            yield return Until(component, ball.Burst + ball.FallTime + .1f);
            float damage = Damage(raider);
            t.Log($"the raider took {damage:0.#} damage, {(raider.Dead ? "dead" : raider.Downed ? "down" : "stunned " + raider.stances.stunner.Stunned)}");
            t.Check(raider.Spawned && damage > 0f && damage < 20f, "it landed with the fall and about 2 s of crush (a full hold is about 34)");
            yield return Until(component, ball.End + .2f);
            t.Check(!component.Live && component.Inner.Count == 0, "the ball ended with nothing left inside");
            Tests_Pain.Finish(record);
        }

        private static Command_Action ReleaseButton(Ability ability) =>
            ability.GetGizmos().OfType<Command_Action>().FirstOrDefault(c => c.defaultLabel == Patch_ChibakuRelease.Label);

        [RimArtTest("Chibaku", "tensei 4 Pain presses Release while the ball holds: no button while it forms, it bursts 0.4 s later, the raider inside takes the crush only for the time held, Banshō is free after the burst (screenshot)", 1500)]
        private static IEnumerable<int> Release(RimArtTestContext t)
        {
            IntVec3 c = t.center, from = c + new IntVec3(-10, 0, 0);
            Pawn host = Arena(t, from, out _, out EchoRecord record, out MapComponent_ChibakuPlates component);
            Pawn raider = Tests_Pain.Target(t, c + new IntVec3(1, 0, 0), 300);
            yield return 5;

            Ability ability = Tests_Pain.Ready(t, host, Def), bansho = host.abilities.GetAbility(PainDefOf.AG_PainBanshoTenin);
            if (ability == null || bansho == null) { Tests_Pain.Finish(record); yield break; }
            t.Check(ReleaseButton(ability) == null, "no Release button before the cast");
            ability.QueueCastingJob(new LocalTargetInfo(c), LocalTargetInfo.Invalid);
            foreach (int step in Tests_Pain.WaitFor(() => Tests_Pain.Cast<ChibakuCast>(host)?.handed == true, 150)) yield return step;
            ChibakuBall ball = component.Ball;
            if (!t.Check(component.Live && ball != null, "the ball began")) { Tests_Pain.Finish(record); yield break; }
            yield return Until(component, ChibakuBall.Pull + 1f);
            t.Check(ReleaseButton(ability) == null, "no Release button while the ball is still forming");

            yield return Until(component, ChibakuBall.Formed + 2f);
            t.Check(!raider.Spawned, "the raider is in the ball");
            float fullBurst = ball.Burst;
            Command_Action release = ReleaseButton(ability);
            if (!t.Check(release != null && !release.Disabled, "the Release button is there once the ball has formed")) { Tests_Pain.Finish(record); yield break; }
            float pressedAt = component.LiveSeconds;
            release.action();
            t.Check(ball.Broken && Mathf.Abs(ball.Burst - (pressedAt + ChibakuBall.CrackTime)) < .05f,
                $"it bursts {ChibakuBall.CrackTime:0.0} s after the press: at {ball.Burst:0.00} s instead of {fullBurst:0.00} s");
            t.Check(ReleaseButton(ability) == null, "the button is gone once the seams have opened");
            t.Check(PainKit.ChibakuLeft(host) <= ChibakuBall.CrackTime + .05f, "the lock ends with the burst (" + PainKit.ChibakuLeft(host).ToString("0.00") + " s left)");

            yield return Until(component, ball.Burst + .1f);
            bool waits = bansho.GizmoDisabled(out string why) && why != null && why.Contains("Chibaku");
            t.Check(!waits, "Banshō Ten'in is free after the burst (" + why + ")");
            yield return Until(component, ball.Burst + ball.FallTime + .1f);
            float damage = Damage(raider);
            t.Log($"the raider took {damage:0.#} damage, {(raider.Dead ? "dead" : raider.Downed ? "down" : "stunned " + raider.stances.stunner.Stunned)}");
            t.Check(raider.Spawned && damage > 0f && damage < 20f, "it landed with the fall and about 2 s of crush (a full hold is about 34)");
            yield return t.ShotAs("chibaku-tensei-7-released", c + new IntVec3(0, 0, 1), 12f);
            yield return Until(component, ball.End + .2f);
            t.Check(!component.Live && component.Inner.Count == 0, "the ball ended with nothing left inside");
            Tests_Pain.Finish(record);
        }
    }
}
