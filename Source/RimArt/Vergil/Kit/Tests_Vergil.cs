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
    /// Game tests for the Vergil Echo (run with -quicktest -rimarttest=vergil): Yamato on Manifest and how it
    /// is worn in four facings, Judgement Cut, Yamato Dash, Summoned Swords in both modes, Judgement Cut End,
    /// and the Style meter. The screenshots are close-ups of the pose.
    /// </summary>
    public static class Tests_Vergil
    {
        private static EchoDef Vergil => VergilDefOf.AG_Echo_Vergil;

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            GameComponent_Vergil.Instance.ResetForTests();
            return t.ClearEchoes();
        }

        /// <summary>A colonist made Vergil's Host and manifested, with a full pool, undrafted and standing still.</summary>
        private static Pawn Host(RimArtTestContext t, IntVec3 at, out EchoRecord record)
        {
            Pawn host = t.Host(Vergil, at, out record);
            // Undrafted and held: a drafted pawn attacks an adjacent enemy, and a busy stance refuses the next cast.
            host.drafter.Drafted = false;
            RimArtTestContext.Hold(host);
            NoWimp(host);
            return t.Note(host);
        }

        private static Pawn Ally(RimArtTestContext t, IntVec3 at)
        {
            // Drafted with fire at will off: it neither flees from the raiders nor hits one standing next to it.
            Pawn pawn = t.Colonist(at);
            pawn.drafter.Drafted = true;
            pawn.drafter.FireAtWill = false;
            return t.Note(pawn);
        }

        /// <summary>A hostile that stands still for the test: unarmed and stunned, so it starts no fist fight.</summary>
        private static Pawn Target(RimArtTestContext t, IntVec3 at, int stunTicks = 600)
        {
            return t.Note(t.Target(at, stunTicks, bare: false));
        }

        private static int Injuries(Pawn pawn) => pawn.Dead ? 999 : pawn.health.hediffSet.hediffs.Count(h => h is Hediff_Injury || h is Hediff_MissingPart);

        private static T Cast<T>(Pawn host) where T : VergilCast => GameComponent_Vergil.Instance?.Latest<T>(host);

        // ---- Yamato ------------------------------------------------------------------------------------------------

        [RimArtTest("Vergil", "yamato 1 Manifest puts Yamato in hand with the four abilities; worn at the hip in four facings; gone on revert (screenshots)")]
        private static IEnumerable<int> Yamato(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            yield return 5;
            t.Check(host.equipment.Primary?.def == VergilDefOf.AG_Yamato, "Yamato is in hand (" + host.equipment.Primary?.LabelCap + ")");
            foreach (AbilityDef def in new[] { VergilDefOf.AG_VergilJudgementCut, VergilDefOf.AG_VergilYamatoDash, VergilDefOf.AG_VergilSummonedSwords, VergilDefOf.AG_VergilJudgementCutEnd })
                t.Check(host.abilities.GetAbility(def) != null, "Vergil has " + def.label);
            Ability end = host.abilities.GetAbility(VergilDefOf.AG_VergilJudgementCutEnd);
            string reason = null;
            bool disabled = end != null && end.GizmoDisabled(out reason);
            t.Check(disabled && reason != null && reason.Contains("Style"), "Judgement Cut End is disabled without Style (" + reason + ")");
            foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.North, Rot4.West })
            {
                Face(host, rot);
                yield return 3;
                yield return t.ShotAs("yamato-" + rot.ToStringHuman().ToLower(), host.Position, 3f);
            }
            host.drafter.Drafted = true;
            yield return 3;
            yield return t.ShotAs("yamato-drafted", host.Position, 3f);
            ThingWithComps yamato = host.equipment.Primary;
            EndHost(record);
            yield return 5;
            t.Check(yamato == null || yamato.Destroyed, "Yamato is gone after the revert");
        }

        // ---- Judgement Cut -----------------------------------------------------------------------------------------

        [RimArtTest("Vergil", "cut 1 Judgement Cut hits every pawn in the sphere (the ally too), not outside; Style +4 per hostile; pays 3 (screenshots)")]
        private static IEnumerable<int> JudgementCut(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 target = t.center + new IntVec3(3, 0, 0);
            Pawn host = Host(t, t.center + new IntVec3(-5, 0, 0), out EchoRecord record);
            Pawn e1 = Target(t, target + new IntVec3(1, 0, 0));
            Pawn e2 = Target(t, target + new IntVec3(-1, 0, -1));
            Pawn e3 = Target(t, target + new IntVec3(0, 0, 1));
            Pawn ally = Ally(t, target + new IntVec3(-1, 0, 1));
            Pawn outside = Target(t, target + new IntVec3(3, 0, 0));
            yield return 2;
            Ability cut = host.abilities.GetAbility(VergilDefOf.AG_VergilJudgementCut);
            if (!t.Check(cut != null && cut.CanCast, "Vergil can cast Judgement Cut (" + cut?.CanCast.Reason + ")")) { EndHost(record); yield break; }
            float before = echoes.charge;
            cut.QueueCastingJob(target, LocalTargetInfo.Invalid);
            t.Check(host.CurJobDef == VergilDefOf.AG_CastVergil, "the Vergil cast job started (" + host.CurJobDef?.defName + ")");
            yield return 20;
            t.Log("warmup: hand " + (VergilLooks.TryGet(host, out VergilLook look) ? look.hand.ToString("0.00") : "none"));
            yield return t.ShotAs("cut-warmup-hand", host.Position, 3f);
            JudgementCutCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<JudgementCutCast>(host)) != null && cast.Fired, 60)) yield return w;
            if (!t.Check(cast != null && cast.Fired, "the cut fired")) { EndHost(record); yield break; }
            yield return 12;
            yield return t.ShotAs("cut-sphere", target, 5f);
            yield return 30;
            foreach (Pawn p in new[] { e1, e2, e3, ally, outside }) t.Log(RimArtTestContext.Describe(p) + " injuries=" + Injuries(p));
            t.Check(t.Hurt(e1) && t.Hurt(e2) && t.Hurt(e3), "the three enemies inside are cut");
            t.Check(t.Hurt(ally), "the ally inside is cut too");
            t.Check(t.Untouched(outside), "the enemy 3 cells from the centre is not");
            t.Check(t.Untouched(host), "Vergil is not");
            float style = VergilStyle.Of(host);
            t.Check(Mathf.Abs(style - 12f) < 0.01f, "Style is 12 (3 hostiles x 4): " + style.ToString("0.##"));
            float spent = before - echoes.charge;
            t.Check(spent >= 3f && spent < 4f, "the pool paid 3 (" + spent.ToString("0.##") + " with upkeep)");
            EndHost(record);
        }

        // ---- Yamato Dash -------------------------------------------------------------------------------------------

        [RimArtTest("Vergil", "dash 1 Yamato Dash moves him on arrival, marks the hostiles on the path, cuts them on the click; not the ally or off the path; pays 2 (screenshots)")]
        private static IEnumerable<int> Dash(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-4, 0, 0), dest = t.center + new IntVec3(3, 0, 0);
            Pawn host = Host(t, from, out EchoRecord record);
            Pawn m1 = Target(t, t.center + new IntVec3(-2, 0, 0));
            Pawn m2 = Target(t, t.center + new IntVec3(2, 0, 0));
            Pawn ally = Ally(t, t.center);
            Pawn off = Target(t, t.center + new IntVec3(0, 0, 2));
            // A blocked line is refused.
            t.Wall(t.center + new IntVec3(-4, 0, -2), ThingDefOf.WoodLog);
            yield return 2;
            string blocked = CompAbilityEffect_YamatoDash.Problem(host, t.center + new IntVec3(-4, 0, -4));
            t.Check(blocked != null, "a dash through a wall is refused (" + blocked + ")");
            t.Check(CompAbilityEffect_YamatoDash.Problem(host, dest) == null, "the open line to " + dest + " is allowed");

            Ability dash = host.abilities.GetAbility(VergilDefOf.AG_VergilYamatoDash);
            if (!t.Check(dash != null && dash.CanCast, "Vergil can cast Yamato Dash (" + dash?.CanCast.Reason + ")")) { EndHost(record); yield break; }
            float before = echoes.charge;
            dash.QueueCastingJob(dest, LocalTargetInfo.Invalid);
            // A screenshot pauses the game, but the ticks already due in that frame still run, so it is taken
            // well before the fire.
            yield return 10;
            yield return t.ShotAs("dash-prepare-crouch", host.Position, 3f);
            YamatoDashCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<YamatoDashCast>(host)) != null && cast.Fired, 60)) yield return w;
            if (!t.Check(cast != null && cast.Fired, "the dash fired")) { EndHost(record); yield break; }
            int fired = cast.fireTick, ghostsBefore = VergilGhost.Drawn;
            t.Log("fire tick seen " + (t.Now - fired) + " ticks late");
            if (t.Now < fired + 4) yield return fired + 4 - t.Now;
            if (t.Now <= fired + 6)
            {
                t.Log("mid-dash (fire + " + (t.Now - fired) + "): " + RimArtTestContext.Describe(host) + " drawn at " + host.DrawPos);
                t.Check(host.Position == from, "mid-dash his cell has not changed yet");
            }
            yield return t.ShotAs("dash-mid", t.center, 5f);
            t.Check(VergilGhost.Drawn > ghostsBefore, "his afterimages were drawn along the path (" + (VergilGhost.Drawn - ghostsBefore) + " draws)");
            foreach (int w in WaitFor(() => host.Position == dest, 20)) yield return w;
            t.Log("arrived by fire + " + (t.Now - fired) + " ticks: " + RimArtTestContext.Describe(host));
            t.Check(host.Position == dest, "he arrived at " + dest);
            t.Check(t.Untouched(m1) && t.Untouched(m2), "the marks are not cut before the click");
            yield return 8;
            yield return t.ShotAs("dash-sheathe", host.Position, 3f);
            t.Check(host.CurJobDef == VergilDefOf.AG_CastVergil, "the job holds him for the sheathe (" + RimArtTestContext.Describe(host) + ")");
            t.Check(t.Untouched(m1) && t.Untouched(m2), "still not cut just before the click (fire + " + (t.Now - fired) + ")");
            if (t.Now < fired + 35) yield return fired + 35 - t.Now;
            t.Log("checked at fire + " + (t.Now - fired) + " ticks (the click is due at 33)");
            foreach (Pawn p in new[] { m1, m2, ally, off }) t.Log(RimArtTestContext.Describe(p) + " injuries=" + Injuries(p));
            t.Check(t.Hurt(m1) && t.Hurt(m2), "both enemies on the path are cut on the click");
            t.Check(t.Untouched(ally), "the ally on the path is not");
            t.Check(t.Untouched(off), "the enemy 2 cells off the path is not");
            float style = VergilStyle.Of(host);
            t.Check(Mathf.Abs(style - 6f) < 0.01f, "Style is 6 (2 marks x 3): " + style.ToString("0.##"));
            float spent = before - echoes.charge;
            t.Check(spent >= 2f && spent < 3f, "the pool paid 2 (" + spent.ToString("0.##") + ")");
            yield return 20;
            t.Check(host.CurJobDef != VergilDefOf.AG_CastVergil, "the job let him go after the click (" + RimArtTestContext.Describe(host) + ")");
            EndHost(record);
        }

        // ---- Summoned Swords ---------------------------------------------------------------------------------------

        [RimArtTest("Vergil", "swords 1 blades fly at the nearest hostile in range, stick and slow; spin cuts what is close; all break at the end (screenshots)", 2400)]
        private static IEnumerable<int> Swords(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            CompProperties_SummonedSwords props = VergilKit.Props<CompProperties_SummonedSwords>(VergilDefOf.AG_VergilSummonedSwords);
            float seconds = props.seconds;
            props.seconds = 7f;   // the test does not wait out 20 s
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            Pawn near = Target(t, t.center + new IntVec3(5, 0, 1), 1200);
            Pawn other = Target(t, t.center + new IntVec3(-2, 0, -7), 1200);
            Pawn far = Target(t, t.center + new IntVec3(0, 0, 13), 1200);
            yield return 2;
            Ability swords = host.abilities.GetAbility(VergilDefOf.AG_VergilSummonedSwords);
            if (!t.Check(swords != null && swords.CanCast, "Vergil can cast Summoned Swords (" + swords?.CanCast.Reason + ")")) { props.seconds = seconds; EndHost(record); yield break; }
            float before = echoes.charge;
            swords.QueueCastingJob(host, LocalTargetInfo.Invalid);
            yield return 20;
            yield return t.ShotAs("swords-rising", host.Position, 3f);
            SummonedSwordsCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<SummonedSwordsCast>(host)) != null && cast.Fired, 60)) yield return w;
            if (!t.Check(cast != null && cast.Fired, "the swords formed")) { props.seconds = seconds; EndHost(record); yield break; }
            float spent = before - echoes.charge;
            t.Check(spent >= 10f && spent < 11f, "the pool paid 10 (" + spent.ToString("0.##") + ")");
            yield return 2;
            // Undrafted, he would flee from the raiders; drafted with fire at will off he neither flees nor swings.
            host.drafter.Drafted = true;
            host.drafter.FireAtWill = false;
            yield return 18;
            yield return t.ShotAs("swords-ring", host.Position, 4f);
            yield return 90;
            HediffDef pin = VergilDefOf.AG_VergilSwordPinned;
            t.Log(RimArtTestContext.Describe(near) + " injuries=" + Injuries(near) + " pinned=" + near.health.hediffSet.GetFirstHediffOfDef(pin)?.Severity);
            t.Log(RimArtTestContext.Describe(other) + " injuries=" + Injuries(other) + " pinned=" + other.health.hediffSet.GetFirstHediffOfDef(pin)?.Severity);
            t.Check(t.Hurt(near), "the nearest enemy was hit");
            t.Check(near.health.hediffSet.HasHediff(pin), "a blade is stuck in it (the slow)");
            yield return t.ShotAs("swords-stuck", near.Position, 4f);
            t.Check(t.Untouched(far), "the enemy 13 cells away is out of range");

            // Spin: an enemy right next to him is cut every 0.9 s; nothing more is thrown.
            cast.spin = true;
            t.Log("Vergil before the spin: " + RimArtTestContext.Describe(host));
            other.Position = host.Position + new IntVec3(1, 0, 0);
            other.Notify_Teleported(true, true);
            RimArtTestContext.Hold(other);
            int cuts = Injuries(other);
            yield return 25;
            yield return t.ShotAs("swords-spin", host.Position, 4f);
            yield return 100;
            t.Log(RimArtTestContext.Describe(other) + " injuries=" + Injuries(other) + " (was " + cuts + ")");
            t.Check(Injuries(other) > cuts, "the enemy next to him is cut by the spinning ring");
            float style = VergilStyle.Of(host);
            t.Check(style >= 2f, "Style rose with the hits: " + style.ToString("0.##"));

            foreach (int w in WaitFor(() => !cast.Out(t.Now), 400, 5)) yield return w;
            yield return 5;
            t.Check(!cast.Out(t.Now), "the swords broke after " + props.seconds + " s");
            t.Check(!near.health.hediffSet.HasHediff(pin) && !other.health.hediffSet.HasHediff(pin), "no blade is left stuck in anyone");
            t.Check(t.Untouched(far), "the far enemy was never hit");
            props.seconds = seconds;
            EndHost(record);
        }

        // ---- Judgement Cut End -------------------------------------------------------------------------------------

        [RimArtTest("Vergil", "end 1 Judgement Cut End: needs S; vanish (untargetable, projectiles break); kneel; shared cuts on the marked; buildings (screenshots)", 2400)]
        private static IEnumerable<int> CutEnd(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            Pawn near = t.Note(t.Enemy(t.center + new IntVec3(2, 0, 0), armed: false));
            Pawn mid = t.Note(t.Enemy(t.center + new IntVec3(0, 0, 6), armed: false));
            for (int z = -1; z <= 1; z++) t.Wall(t.center + new IntVec3(-3, 0, z), ThingDefOf.WoodLog);
            Pawn behind = t.Note(t.Enemy(t.center + new IntVec3(-5, 0, 0), armed: false));
            Pawn outside = t.Note(t.Enemy(t.center + new IntVec3(11, 0, 0), armed: false));
            Pawn ally = Ally(t, t.center + new IntVec3(0, 0, -2));
            Thing unowned = t.Wall(t.center + new IntVec3(4, 0, 4), ThingDefOf.WoodLog, owned: false);
            Thing own = t.Wall(t.center + new IntVec3(-4, 0, 4), ThingDefOf.WoodLog);
            int unownedHp = unowned.HitPoints, ownHp = own.HitPoints;
            yield return 2;

            Ability end = host.abilities.GetAbility(VergilDefOf.AG_VergilJudgementCutEnd);
            if (!t.Check(end != null, "Vergil has Judgement Cut End")) { EndHost(record); yield break; }
            t.Check(end.GizmoDisabled(out string reason), "disabled at Style 0 (" + reason + ")");
            VergilStyle.Set(host, 100f);
            t.Check(!end.GizmoDisabled(out reason), "enabled at Style 100 (" + reason + ")");
            float before = echoes.charge;
            end.QueueCastingJob(host, LocalTargetInfo.Invalid);
            yield return 30;
            yield return t.ShotAs("end-warmup-hand", host.Position, 3f);
            JudgementCutEndCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<JudgementCutEndCast>(host)) != null && cast.Fired, 60)) yield return w;
            if (!t.Check(cast != null && cast.Fired, "he vanished")) { EndHost(record); yield break; }
            int fired = t.Now, ghostsBefore = VergilGhost.Drawn;
            yield return 2;
            t.Check(host.health.hediffSet.HasHediff(VergilDefOf.AG_VergilGone) && host.IsPsychologicallyInvisible(), "he is gone: invisible to others");
            t.Check(Stunned(near) && Stunned(mid), "the two enemies in sight are marked and stunned");
            t.Check(!Stunned(behind) && !Stunned(outside), "the one behind the wall and the one outside the radius are not");
            t.Check(!Stunned(ally), "the ally is not");
            t.Check(cast.Marked.Count == 2, "2 marked (" + cast.Marked.Count + ")");
            t.Check(cast.cuts == 20, "Style 100 gives 20 cuts (" + cast.cuts + ")");
            t.Check(VergilStyle.Of(host) < 0.01f, "all Style is spent");
            t.Check(before - echoes.charge < 1f, "no charge is spent (" + (before - echoes.charge).ToString("0.##") + ", upkeep only)");

            // A bullet inside the radius while he is gone breaks.
            ThingDef bulletDef = DefDatabase<ThingDef>.GetNamedSilentFail("Bullet_Revolver");
            if (bulletDef != null)
            {
                var bullet = (Projectile)GenSpawn.Spawn(bulletDef, t.center + new IntVec3(6, 0, 0), t.map);
                bullet.Launch(outside, bullet.DrawPos, near, near, ProjectileHitFlags.All);
                yield return 2;
                t.Check(bullet.Destroyed, "a bullet inside the radius is destroyed while he is gone");
            }
            yield return 25;
            yield return t.ShotAs("end-gone", t.center, 10f);
            t.Check(VergilGhost.Drawn > ghostsBefore, "his afterimages were drawn at the cut ends while he is gone (" + (VergilGhost.Drawn - ghostsBefore) + " draws)");

            // The camera eases toward a new zoom over several frames; aim it close now so the kneel shot is a real close-up.
            Find.CameraDriver.SetRootPosAndSize(host.Position.ToVector3Shifted(), 3f);
            foreach (int w in WaitFor(() => cast.Back, 120)) yield return w;
            t.Log("back after " + (t.Now - fired) + " ticks: " + RimArtTestContext.Describe(host) + " facing " + host.Rotation);
            t.Check(!host.health.hediffSet.HasHediff(VergilDefOf.AG_VergilGone), "he is visible again");
            t.Check(host.Position == t.center, "on the cell he left");
            yield return 20;
            t.Check(host.Rotation == Rot4.South, "kneeling facing the camera (" + host.Rotation + ")");
            t.Check(VergilLooks.TryGet(host, out VergilLook kneel) && kneel.kneelPicture != null && kneel.Squash > 0.999f,
                "kneeling in the coat's kneel picture, nothing squashed (body " + host.story?.bodyType?.defName + ")");
            t.Log("kneel shot at sketch second " + cast.Seconds(t.Now).ToString("0.00") + ", sheathe " + (kneel?.sheathe ?? -1f).ToString("0.00"));
            yield return t.ShotAs("end-kneel", host.Position, 3f);
            foreach (int w in WaitFor(() => cast.Clicked, 80)) yield return w;
            yield return 4;
            yield return t.ShotAs("end-click", t.center, 10f);
            foreach (Pawn p in new[] { near, mid, behind, outside, ally }) t.Log(RimArtTestContext.Describe(p) + " injuries=" + Injuries(p));
            t.Check(t.Hurt(near) && t.Hurt(mid), "both marked enemies are cut");
            t.Check(t.Untouched(behind) && t.Untouched(outside) && t.Untouched(ally), "the enemy behind the wall, the one outside and the ally are not");
            t.Log("unowned wall " + unownedHp + " -> " + (unowned.Destroyed ? "destroyed" : unowned.HitPoints.ToString()) + ", own wall " + ownHp + " -> " + own.HitPoints);
            t.Check(unowned.Destroyed || unownedHp - unowned.HitPoints >= 59, "the unowned wall in sight took 60");
            t.Check(!own.Destroyed && own.HitPoints == ownHp, "his own colony's wall did not");
            yield return 10;
            t.Check(host.CurJobDef == VergilDefOf.AG_CastVergil, "still kneeling just after the click (" + RimArtTestContext.Describe(host) + ")");
            yield return 60;
            t.Check(host.CurJobDef != VergilDefOf.AG_CastVergil, "then he stands and the job lets him go (" + RimArtTestContext.Describe(host) + ")");
            EndHost(record);
        }

        // ---- Style -------------------------------------------------------------------------------------------------

        [RimArtTest("Vergil", "style 1 ranks; a Yamato melee hit +2; an ability's own hit is not counted twice; damage taken -20; drain after 10 s; empty on revert")]
        private static IEnumerable<int> Style(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            Pawn enemy = t.Enemy(t.center + new IntVec3(3, 0, 0), armed: false);
            yield return 2;
            VergilStyleExtension rules = VergilStyle.Rules;
            t.Check(rules.RankOf(0f) == "D" && rules.RankOf(59f) == "A" && rules.RankOf(60f) == "S" && rules.RankOf(95f) == "SSS",
                "ranks: 0 D, 59 A, 60 S, 95 SSS");
            VergilStyle.Set(host, 50f);
            enemy.TakeDamage(new DamageInfo(DamageDefOf.Cut, 3f, 0f, -1f, host, null, VergilDefOf.AG_Yamato));
            t.Check(Mathf.Abs(VergilStyle.Of(host) - 52f) < 0.01f, "a Yamato melee hit: 50 -> 52 (" + VergilStyle.Of(host).ToString("0.##") + ")");
            enemy.TakeDamage(new DamageInfo(DamageDefOf.Cut, 3f, 0f, -1f, host));
            t.Check(Mathf.Abs(VergilStyle.Of(host) - 52f) < 0.01f, "a cut with no weapon (an ability's) adds nothing here (" + VergilStyle.Of(host).ToString("0.##") + ")");
            host.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 2f, 0f, -1f, enemy));
            t.Check(Mathf.Abs(VergilStyle.Of(host) - 32f) < 0.01f, "taking damage: 52 -> 32 (" + VergilStyle.Of(host).ToString("0.##") + ")");
            VergilStyleMeter meter = GameComponent_Vergil.Instance.Meter(host, false);
            meter.lastHitTick = t.Now - 700;
            yield return 60;
            float drained = VergilStyle.Of(host);
            t.Check(drained < 28.5f && drained > 25.5f, "after 10 s without a hit it drains about 5 a second: " + drained.ToString("0.##"));
            EndHost(record);
            yield return VergilStyle.TickInterval + 1;
            t.Check(VergilStyle.Of(host) < 0.01f, "empty after the revert (" + VergilStyle.Of(host).ToString("0.##") + ")");
        }
    }
}
