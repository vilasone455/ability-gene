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
    /// Game tests for the Minato Echo (run with -quicktest -rimarttest=minato): the Echo and its kill Trial, the
    /// jump to a ground kunai and into a marked pawn, sealing touch, the chain, Guiding Thunder with a bullet and
    /// a grenade, and the Rasengan by touch, from range, and into a wall. Screenshots at the moments of each picture.
    /// </summary>
    public static class Tests_Minato
    {
        private static EchoDef Minato => MinatoDefOf.AG_Echo_Minato;

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            startHealth.Clear();
            GameComponent_Minato.Instance.ResetForTests();
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            return echoes;
        }

        /// <summary>A colonist made Minato's Host and manifested, with a full pool and a kunai belt, drafted and standing still.</summary>
        private static Pawn Host(RimArtTestContext t, GameComponent_Echoes echoes, IntVec3 at, out EchoRecord record)
        {
            Pawn host = t.Colonist(at);
            record = EchoUtility.ForceHost(Minato, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            if (KunaiBelt.WornBy(host) == null) host.apparel.Wear((Apparel)ThingMaker.MakeThing(KunaiDefOf.AG_KunaiBelt), dropReplacedApparel: true);
            host.equipment?.DestroyAllEquipment();
            // Drafted with fire at will off: undrafted he flees the raiders between casts, and drafted with it on he
            // punches the one he lands next to.
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

        /// <summary>A hostile that stands still: unarmed, no apparel (armour would turn a fist to nothing), stunned.</summary>
        private static Pawn Target(RimArtTestContext t, IntVec3 at, int stunTicks = 900)
        {
            Pawn pawn = t.Enemy(at, armed: false);
            pawn.apparel?.DestroyAll();
            pawn.stances.stunner.StunFor(stunTicks, null, false);
            return Noted(pawn);
        }

        private static float Start(Pawn pawn) => startHealth.TryGetValue(pawn, out float h) ? h : 1f;
        private static bool Hurt(Pawn pawn) => pawn.Dead || pawn.Downed || pawn.health.summaryHealth.SummaryHealthPercent < Start(pawn) - 0.001f;
        private static bool Untouched(Pawn pawn) => !pawn.Dead && !pawn.Downed && pawn.health.summaryHealth.SummaryHealthPercent >= Start(pawn) - 0.001f;
        private static bool Stunned(Pawn pawn) => pawn.stances?.stunner?.Stunned == true;

        private static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 1)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        private static T Cast<T>(Pawn host) where T : MinatoCast => GameComponent_Minato.Instance?.Latest<T>(host);

        private static void Finish(EchoRecord record)
        {
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        private static Ability Ready(RimArtTestContext t, Pawn host, AbilityDef def)
        {
            Ability ability = host.abilities.GetAbility(def);
            t.Check(ability != null && ability.CanCast, "Minato can cast " + def.label + " (" + ability?.CanCast.Reason + ")");
            return ability != null && ability.CanCast ? ability : null;
        }

        // ---- the Echo ----------------------------------------------------------------------------------------------

        [RimArtTest("Minato", "echo 1 Manifest gives the four abilities and Kind; his thrown kunai are sealed; a thrown kunai names the kunai as its weapon and a kill with it counts for the Trial")]
        private static IEnumerable<int> Echo(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            yield return 5;
            foreach (AbilityDef def in new[] { MinatoDefOf.AG_ThunderGodJump, MinatoDefOf.AG_ThunderGodChain, MinatoDefOf.AG_GuidingThunder, MinatoDefOf.AG_Rasengan })
                t.Check(host.abilities.GetAbility(def) != null, "Minato has " + def.label);
            t.Check(host.abilities.GetAbility(KunaiDefOf.AG_ThrowKunai) != null, "throw kunai comes from the belt");
            t.Check(host.story.traits.HasTrait(TraitDefOf.Kind), "Minato is Kind");
            t.Check(KunaiSeal.ThrowsSealed(host), "his kunai are sealed in hero form");

            var shot = (Projectile)GenSpawn.Spawn(ThingMaker.MakeThing(KunaiDefOf.AG_KunaiProjectileMinato), host.Position, t.map);
            IntVec3 aim = t.center + new IntVec3(6, 0, 0);
            shot.Launch(host, host.DrawPos, aim, aim, ProjectileHitFlags.None);
            t.Check(shot.EquipmentDef == KunaiDefOf.AG_Kunai, "a thrown kunai's weapon is the kunai (" + shot.EquipmentDef?.defName + ")");

            Trial_KillsWith trial = Minato.trials.OfType<Trial_KillsWith>().FirstOrDefault();
            if (!t.Check(trial != null, "Minato has a kill-with Trial")) { Finish(record); yield break; }
            float before = trial.Current(host);
            Pawn victim = Target(t, t.center + new IntVec3(0, 0, 4));
            victim.Kill(new DamageInfo(DamageDefOf.Stab, 200f, 1f, -1f, host, null, KunaiDefOf.AG_Kunai));
            yield return 2;
            t.Check(trial.Current(host) == before + 1, "a kill with a kunai counts: " + before + " -> " + trial.Current(host));
            Finish(record);
            t.Check(!KunaiSeal.ThrowsSealed(host), "after the revert his kunai are plain again");
        }

        // ---- Flying Thunder God ------------------------------------------------------------------------------------

        [RimArtTest("Minato", "jump 1 to a sealed kunai on the ground: he lands on it 13 ticks after the click and it goes into his belt; a plain kunai is refused (screenshots)")]
        private static IEnumerable<int> JumpGround(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-6, 0, -1), there = t.center + new IntVec3(6, 0, 1);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            CompApparelReloadable belt = KunaiBelt.WornBy(host);
            belt.UsedOnce();
            belt.UsedOnce();
            KunaiItem kunai = DebugActions_Minato.LaySealed(there, t.map);
            KunaiItem plain = GenSpawn.Spawn(ThingMaker.MakeThing(KunaiDefOf.AG_Kunai), t.center + new IntVec3(0, 0, 5), t.map) as KunaiItem;
            yield return 2;
            Ability jump = Ready(t, host, MinatoDefOf.AG_ThunderGodJump);
            if (jump == null) { Finish(record); yield break; }
            t.Check(!jump.verb.ValidateTarget(plain, false), "a plain kunai is not a mark");
            t.Check(jump.verb.ValidateTarget(kunai, false), "his sealed kunai is");
            int charges = belt.RemainingCharges;
            float pool = echoes.charge;
            jump.QueueCastingJob(kunai, LocalTargetInfo.Invalid);
            ThunderGodJumpCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<ThunderGodJumpCast>(host)) != null && cast.Fired, 10)) yield return w;
            if (!t.Check(cast != null && cast.Fired, "the jump fired")) { Finish(record); yield break; }
            int fired = cast.fireTick;
            if (t.Now < fired + 8) yield return fired + 8 - t.Now;
            t.Check(host.Position == from, "before the arrival he has not moved (fire + " + (t.Now - fired) + ")");
            yield return t.ShotAs("jump-ground-script", t.center, 9f);
            foreach (int w in WaitFor(() => host.Position == there, 20)) yield return w;
            t.Log("arrived at fire + " + (t.Now - fired) + ": " + RimArtTestContext.Describe(host));
            t.Check(host.Position == there, "he stands on the kunai's cell " + there);
            t.Check(kunai.Destroyed || !kunai.Spawned, "the kunai is off the ground");
            t.Check(belt.RemainingCharges == charges + 1, "it went into the belt: " + charges + " -> " + belt.RemainingCharges);
            float spent = pool - echoes.charge;
            t.Check(spent >= 2f && spent < 3f, "the pool paid 2 (" + spent.ToString("0.##") + ")");
            yield return 4;
            yield return t.ShotAs("jump-ground-arrive", t.center, 9f);
            Finish(record);
        }

        [RimArtTest("Minato", "jump 2 into a pawn with his kunai: he lands in the cell behind it and cuts once, the kunai stays in; an ally with his kunai is landed behind, not cut (screenshots)")]
        private static IEnumerable<int> JumpEnemy(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-5, 0, 0), at = t.center + new IntVec3(4, 0, 0);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            Pawn enemy = Target(t, at);
            DebugActions_Minato.StickSealed(enemy);
            Noted(enemy);
            yield return 60;
            Ability jump = Ready(t, host, MinatoDefOf.AG_ThunderGodJump);
            if (jump == null) { Finish(record); yield break; }
            t.Check(ThunderGodMarks.Marked(enemy), "the enemy is marked by the kunai");
            jump.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            ThunderGodJumpCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<ThunderGodJumpCast>(host)) != null && cast.Fired, 10)) yield return w;
            if (!t.Check(cast != null && cast.Fired, "the jump fired")) { Finish(record); yield break; }
            int fired = cast.fireTick;
            if (t.Now < fired + 16) yield return fired + 16 - t.Now;
            t.Log("fire + " + (t.Now - fired) + ": " + RimArtTestContext.Describe(host));
            IntVec3 behind = at + new IntVec3(1, 0, 0);
            t.Check(host.Position == behind, "he landed straight behind the enemy at " + behind + " (" + host.Position + ")");
            t.Check(Untouched(enemy), "no cut before the strike tick");
            yield return t.ShotAs("jump-enemy-arrive", at, 7f);
            if (t.Now < fired + 21) yield return fired + 21 - t.Now;
            t.Check(Hurt(enemy), "the enemy is cut");
            t.Check(ThunderGodMarks.HasSealedKunai(enemy), "the kunai is still in");
            t.Check(ThunderGodMarks.Seal(enemy) != null, "the cut left sealing touch");
            yield return 20;
            yield return t.ShotAs("jump-enemy-after", at, 7f);
            t.Check(host.CurJobDef != MinatoDefOf.AG_CastMinato, "the job let him go (" + RimArtTestContext.Describe(host) + ")");

            // An ally with his kunai: landed behind, not cut.
            Pawn ally = t.Colonist(t.center + new IntVec3(-2, 0, 5));
            ally.drafter.Drafted = true;
            ally.drafter.FireAtWill = false;
            Noted(ally);
            DebugActions_Minato.StickSealed(ally);
            Noted(ally);
            jump.ResetCooldown();
            // Standing next to the enemy, his wait job punches it again every time its melee cooldown ends (vanilla
            // auto-attack), and an order waits for the cooldown: the enemy goes first.
            enemy.Destroy();
            foreach (int w in WaitFor(() => !host.stances.FullBodyBusy, 240)) yield return w;
            jump.QueueCastingJob(ally, LocalTargetInfo.Invalid);
            t.Log("after the order: " + RimArtTestContext.Describe(host));
            yield return 30;
            t.Check(host.Position.AdjacentTo8Way(ally.Position), "he landed next to the ally (" + host.Position + ")");
            t.Check(Untouched(ally), "the ally is not cut");
            Finish(record);
        }

        // ---- sealing touch -----------------------------------------------------------------------------------------

        [RimArtTest("Minato", "seal 1 his landed melee hits seal the pawn; a 4th seal replaces the oldest; a plain colonist's hit seals nothing; a sealed pawn is a mark (screenshot)")]
        private static IEnumerable<int> Seal(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            var foes = new List<Pawn>();
            for (int i = 0; i < 4; i++) foes.Add(Target(t, t.center + new IntVec3(-3 + 2 * i, 0, 3)));
            Pawn plainHitter = t.Colonist(t.center + new IntVec3(0, 0, -4));
            Pawn other = Target(t, t.center + new IntVec3(1, 0, -4));
            yield return 2;
            var all = new List<Hediff_MinatoSeal>();
            SealingTouch.All(all);
            t.Log("seals before: " + string.Join(", ", all.Select(h => RimArtTestContext.Describe(h.pawn) + " age " + h.ageTicks)));
            for (int i = 0; i < 3; i++)
            {
                t.Check(ThunderGodStrike.Hit(host, foes[i], 0.2f), "Minato hits foe " + i);
                SealingTouch.All(all);
                t.Log("  " + RimArtTestContext.Describe(foes[i]) + " sealed " + (ThunderGodMarks.Seal(foes[i]) != null) + "; seals now "
                    + string.Join(", ", all.Select(h => h.pawn.LabelShort + " age " + h.ageTicks)));
                yield return 5;
            }
            SealingTouch.All(all);
            t.Log("seals after 3: " + string.Join(", ", all.Select(h => RimArtTestContext.Describe(h.pawn) + " age " + h.ageTicks)));
            t.Check(foes.Take(3).All(f => ThunderGodMarks.Seal(f) != null), "the three he hit are sealed");
            t.Check(ThunderGodMarks.Marked(foes[0]), "a sealed pawn is a mark");
            ThunderGodStrike.Hit(host, foes[3], 0.2f);
            yield return 2;
            t.Check(ThunderGodMarks.Seal(foes[3]) != null, "the 4th is sealed");
            t.Check(ThunderGodMarks.Seal(foes[0]) == null, "the oldest seal is gone");
            SealingTouch.All(all);
            t.Check(all.Count == 3, "3 seals at a time (" + all.Count + ")");
            ThunderGodStrike.Hit(plainHitter, other, 0.2f);
            yield return 2;
            t.Check(ThunderGodMarks.Seal(other) == null, "a plain colonist's hit seals nothing");
            yield return 30;
            yield return t.ShotAs("seal-glow", t.center + new IntVec3(0, 0, 3), 7f);
            Finish(record);
        }

        // ---- the chain ---------------------------------------------------------------------------------------------

        [RimArtTest("Minato", "chain 1 refused with one mark; with three it jumps to each, nearest first, cuts each once and stays at the last (screenshots)")]
        private static IEnumerable<int> Chain(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center + new IntVec3(-6, 0, 0), out EchoRecord record);
            Pawn a = Target(t, t.center + new IntVec3(-2, 0, 2));
            Pawn b = Target(t, t.center + new IntVec3(2, 0, -2));
            Pawn c = Target(t, t.center + new IntVec3(6, 0, 3));
            Pawn unmarked = Target(t, t.center + new IntVec3(0, 0, 5));
            DebugActions_Minato.StickSealed(a);
            Noted(a);
            yield return 2;
            Ability chain = host.abilities.GetAbility(MinatoDefOf.AG_ThunderGodChain);
            bool disabled = chain.GizmoDisabled(out string reason);
            t.Check(disabled, "with one mark the chain is disabled (" + reason + ")");
            SealingTouch.Seal(b);
            DebugActions_Minato.StickSealed(c);
            Noted(c);
            yield return 60;
            chain = Ready(t, host, MinatoDefOf.AG_ThunderGodChain);
            if (chain == null) { Finish(record); yield break; }
            float pool = echoes.charge;
            chain.QueueCastingJob(host, LocalTargetInfo.Invalid);
            ThunderGodChainCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<ThunderGodChainCast>(host)) != null && cast.Fired, 10)) yield return w;
            if (!t.Check(cast != null && cast.Fired, "the chain fired")) { Finish(record); yield break; }
            t.Check(cast.Targets.SequenceEqual(new[] { a, b, c }), "the route is nearest first: " + string.Join(", ", cast.Targets.Select(p => p.Position.ToString())));
            int fired = cast.fireTick;
            if (t.Now < fired + 32) yield return fired + 32 - t.Now;
            yield return t.ShotAs("chain-mid", t.center, 11f);
            if (t.Now < fired + 85) yield return fired + 85 - t.Now;
            for (int k = 0; k < cast.Targets.Count; k++)
                t.Log("target " + k + ": reached " + cast.Reached(k) + ", hurt " + Hurt(cast.Targets[k]) + ", "
                    + RimArtTestContext.Describe(cast.Targets[k]) + ", injuries " + cast.Targets[k].health.hediffSet.hediffs.Count(h => h is Hediff_Injury));
            t.Log(RimArtTestContext.Describe(unmarked));
            t.Check(Hurt(a) && Hurt(b) && Hurt(c), "all three are cut");
            t.Check(Untouched(unmarked), "the unmarked enemy is not");
            t.Check(host.Position.AdjacentTo8Way(c.Position), "he stays next to the last (" + host.Position + ", last at " + c.Position + ")");
            float spent = pool - echoes.charge;
            t.Check(spent >= 10f && spent < 11f, "the pool paid 10 (" + spent.ToString("0.##") + ")");
            yield return t.ShotAs("chain-end", t.center, 11f);
            Finish(record);
        }

        // ---- Guiding Thunder ---------------------------------------------------------------------------------------

        [RimArtTest("Minato", "guiding 1 a bullet at him comes out in the marked pawn, a grenade at his feet goes off at the kunai on the ground; he is held; it ends on time (screenshots)", 1800)]
        private static IEnumerable<int> Guiding(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            Pawn anchor = Target(t, t.center + new IntVec3(8, 0, 3), 1500);
            DebugActions_Minato.StickSealed(anchor);
            Noted(anchor);
            Pawn shooter = Target(t, t.center + new IntVec3(-7, 0, -2), 1500);
            yield return 60;
            Ability guiding = Ready(t, host, MinatoDefOf.AG_GuidingThunder);
            if (guiding == null) { Finish(record); yield break; }
            guiding.QueueCastingJob(anchor, LocalTargetInfo.Invalid);
            GuidingThunderCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<GuidingThunderCast>(host)) != null && cast.Fired, 10)) yield return w;
            if (!t.Check(cast != null && cast.Standing, "the barrier stands")) { Finish(record); yield break; }
            yield return 20;
            t.Check(host.CurJobDef == MinatoDefOf.AG_CastMinato, "the job holds him (" + RimArtTestContext.Describe(host) + ")");

            ThingDef bulletDef = DefDatabase<ThingDef>.GetNamed("Bullet_Revolver");
            var bullet = (Projectile)GenSpawn.Spawn(ThingMaker.MakeThing(bulletDef), shooter.Position, t.map);
            bullet.Launch(shooter, shooter.DrawPos, host, host, ProjectileHitFlags.IntendedTarget);
            foreach (int w in WaitFor(() => bullet.Destroyed, 60)) yield return w;
            t.Check(bullet.Destroyed, "the bullet is gone");
            t.Check(Untouched(host), "Minato is not hit");
            t.Check(Hurt(anchor), "the pawn with his kunai is (" + RimArtTestContext.Describe(anchor) + ")");
            t.Check(cast.taken == 1, "one shot taken (" + cast.taken + ")");
            yield return 2;
            yield return t.ShotAs("guiding-bullet", t.center, 11f);

            // Now the exit is a kunai on the ground: a second barrier after the first ends.
            if (t.Now < cast.endTick + 2) yield return cast.endTick + 2 - t.Now;
            t.Check(!cast.Standing, "the barrier ended on time");
            yield return 5;
            t.Check(host.CurJobDef != MinatoDefOf.AG_CastMinato, "the job let him go");
            IntVec3 ground = t.center + new IntVec3(-3, 0, 7);
            KunaiItem kunai = DebugActions_Minato.LaySealed(ground, t.map);
            guiding.ResetCooldown();
            yield return 2;
            guiding.QueueCastingJob(kunai, LocalTargetInfo.Invalid);
            cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<GuidingThunderCast>(host)) != null && cast.Fired && cast.Standing, 10)) yield return w;
            if (!t.Check(cast != null && cast.Standing, "the second barrier stands")) { Finish(record); yield break; }
            ThingDef grenadeDef = DefDatabase<ThingDef>.GetNamed("Proj_GrenadeFrag");
            var grenade = (Projectile)GenSpawn.Spawn(ThingMaker.MakeThing(grenadeDef), shooter.Position, t.map);
            grenade.Launch(shooter, shooter.DrawPos, host.Position, host.Position, ProjectileHitFlags.IntendedTarget);
            foreach (int w in WaitFor(() => cast.taken > 0, 90)) yield return w;
            t.Check(cast.taken == 1, "the grenade was taken (" + cast.taken + ")");
            t.Log("grenade: " + (grenade.Destroyed ? "destroyed" : "at " + grenade.Position));
            t.Check(grenade.Destroyed || grenade.Position.InHorDistOf(ground, 1.5f), "it came out at the ground kunai");
            yield return 10;
            yield return t.ShotAs("guiding-grenade", t.center + new IntVec3(-1, 0, 3), 11f);
            foreach (int w in WaitFor(() => grenade.Destroyed, 240)) yield return w;
            t.Check(Untouched(host), "Minato is not hurt by the grenade");
            Finish(record);
        }

        // ---- Rasengan ----------------------------------------------------------------------------------------------

        [RimArtTest("Minato", "rasengan 1 with no mark he walks up first; hit, thrown 3 cells away from him and stunned; into a wall it goes 1 cell and takes the slam (screenshots)")]
        private static IEnumerable<int> RasenganTouch(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            IntVec3 at = t.center + new IntVec3(2, 0, 0);
            Pawn enemy = Target(t, at);
            yield return 2;
            Ability rasengan = Ready(t, host, MinatoDefOf.AG_Rasengan);
            if (rasengan == null) { Finish(record); yield break; }
            rasengan.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            // No mark: he walks up to it first.
            foreach (int w in WaitFor(() => host.Position.AdjacentTo8Way(enemy.Position), 60)) yield return w;
            t.Check(host.Position == t.center + new IntVec3(1, 0, 0), "he walked up next to it (" + host.Position + ")");
            yield return 20;
            yield return t.ShotAs("rasengan-form", host.Position, 5f);
            RasenganCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<RasenganCast>(host)) != null && cast.Fired, 40)) yield return w;
            if (!t.Check(cast != null && cast.Fired && !cast.teleports, "the touch Rasengan fired, no jump")) { Finish(record); yield break; }
            int fired = cast.fireTick;
            if (t.Now < fired + 15) yield return fired + 15 - t.Now;
            yield return t.ShotAs("rasengan-grind", at, 5f);
            foreach (int w in WaitFor(() => cast.landedThrow, 40)) yield return w;
            t.Log("after the throw: " + RimArtTestContext.Describe(enemy) + " thrown " + cast.thrown.ToString("0.##") + " wall " + cast.wall);
            t.Check(Hurt(enemy), "the enemy is hit");
            t.Check(enemy.Dead || enemy.Position == at + new IntVec3(3, 0, 0), "thrown 3 cells east, away from him (" + enemy.Position + ")");
            IntVec3 stood = host.Position;
            t.Check(enemy.Dead || Stunned(enemy), "stunned");
            yield return 6;
            yield return t.ShotAs("rasengan-thrown", t.center + new IntVec3(2, 0, 0), 7f);

            // Into a wall: the next one stands 2 cells west of him with a wall 2 cells behind it. He walks up to it
            // and it is thrown 1 cell, into the wall.
            Pawn walled = Target(t, stood + new IntVec3(-2, 0, 0));
            GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), stood + new IntVec3(-4, 0, 0), t.map);
            foreach (int w in WaitFor(() => !host.stances.FullBodyBusy, 240)) yield return w;
            rasengan.ResetCooldown();
            rasengan.QueueCastingJob(walled, LocalTargetInfo.Invalid);
            RasenganCast first = cast;
            cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<RasenganCast>(host)) != null && cast != first && cast.Fired && cast.landedThrow, 120)) yield return w;
            t.Log("second cast: missed " + cast?.missed + " aborted " + cast?.aborted + ", Minato " + RimArtTestContext.Describe(host));
            t.Log("walled: " + RimArtTestContext.Describe(walled) + " thrown " + cast?.thrown.ToString("0.##") + " wall " + cast?.wall);
            t.Check(cast != null && cast.wall && walled.Position == stood + new IntVec3(-3, 0, 0), "it stopped 1 cell out, at the wall, and took the slam");
            Finish(record);
        }

        [RimArtTest("Minato", "rasengan 2 from range at a sealed pawn: he forms it where he stands, lands behind it, and it is thrown back toward where he came from (screenshots)")]
        private static IEnumerable<int> RasenganRange(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 from = t.center + new IntVec3(-6, 0, 0), at = t.center + new IntVec3(3, 0, 0);
            Pawn host = Host(t, echoes, from, out EchoRecord record);
            Pawn enemy = Target(t, at);
            SealingTouch.Seal(enemy);
            yield return 2;
            Ability rasengan = Ready(t, host, MinatoDefOf.AG_Rasengan);
            if (rasengan == null) { Finish(record); yield break; }
            rasengan.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            yield return 25;
            t.Check(host.Position == from, "he forms the ball where he stands (" + host.Position + ")");
            yield return t.ShotAs("rasengan-range-form", t.center, 9f);
            RasenganCast cast = null;
            foreach (int w in WaitFor(() => (cast = Cast<RasenganCast>(host)) != null && cast.Fired, 40)) yield return w;
            if (!t.Check(cast != null && cast.Fired && cast.teleports, "the Rasengan fired from range")) { Finish(record); yield break; }
            int fired = cast.fireTick;
            if (t.Now < fired + 12) yield return fired + 12 - t.Now;
            t.Check(host.Position == at + new IntVec3(1, 0, 0), "he landed behind it (" + host.Position + ")");
            yield return t.ShotAs("rasengan-range-grind", at, 7f);
            foreach (int w in WaitFor(() => cast.landedThrow, 40)) yield return w;
            t.Log(RimArtTestContext.Describe(enemy));
            t.Check(Hurt(enemy), "the enemy is hit");
            t.Check(enemy.Dead || enemy.Position == at - new IntVec3(3, 0, 0), "thrown 3 cells back west, toward where he came from (" + enemy.Position + ")");
            yield return 6;
            yield return t.ShotAs("rasengan-range-thrown", t.center, 9f);
            Finish(record);
        }

        [RimArtTest("Minato", "rasengan 3 the walk up to an unmarked pawn can be called off: a move order ends it with no cooldown and no charge; once the warmup starts the job holds")]
        private static IEnumerable<int> RasenganCallOff(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center + new IntVec3(-8, 0, 0), out EchoRecord record);
            Pawn far = Target(t, t.center + new IntVec3(8, 0, 0));
            yield return 2;
            Ability rasengan = Ready(t, host, MinatoDefOf.AG_Rasengan);
            if (rasengan == null) { Finish(record); yield break; }
            float pool = echoes.charge;
            rasengan.QueueCastingJob(far, LocalTargetInfo.Invalid);
            yield return 20;
            t.Log("walking: " + RimArtTestContext.Describe(host));
            t.Check(host.CurJobDef == MinatoDefOf.AG_CastMinato && host.pather.Moving, "he walks toward it");
            t.Check(host.jobs.IsCurrentJobPlayerInterruptible(), "the walk can be called off");
            host.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, host.Position + new IntVec3(0, 0, 3)), JobTag.DraftedOrder);
            yield return 5;
            t.Log("after the move order: " + RimArtTestContext.Describe(host));
            t.Check(host.CurJobDef != MinatoDefOf.AG_CastMinato, "the move order ended it");
            t.Check(!rasengan.OnCooldown, "no cooldown (" + rasengan.CooldownTicksRemaining + " ticks)");
            t.Check(pool - echoes.charge < 1f, "no charge paid (" + (pool - echoes.charge).ToString("0.##") + ")");

            // An enemy one cell past the walk: once he stands next to it the warmup begins and the job holds.
            foreach (int w in WaitFor(() => !host.pather.Moving, 120)) yield return w;
            Pawn near = Target(t, host.Position + new IntVec3(2, 0, 0));
            yield return 2;
            rasengan.QueueCastingJob(near, LocalTargetInfo.Invalid);
            foreach (int w in WaitFor(() => host.stances.curStance is Stance_Warmup, 90)) yield return w;
            t.Log("warmup: " + RimArtTestContext.Describe(host));
            t.Check(host.stances.curStance is Stance_Warmup, "the warmup began");
            t.Check(!host.jobs.IsCurrentJobPlayerInterruptible(), "from the warmup the job holds");
            Finish(record);
        }
    }
}
