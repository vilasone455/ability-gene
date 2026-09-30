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
    /// Game tests for the Satō Echo (run with -quicktest -rimarttest=sato): the Reset (paid, slow, explosions, damage
    /// on the lying body, the downed Reset), Sever and its anchors, Headshot Reset, Grenade Reset, The Game, the Black
    /// Ghost and Tear, and surgery. Long delays are cut short by moving the rise tick after the numbers are checked.
    /// </summary>
    public static class Tests_Sato
    {
        private static EchoDef Sato => SatoDefOf.AG_Echo_Sato;

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            foreach (ThingDef def in new[] { SatoDefOf.AG_AjinAnchor, SatoDefOf.AG_AjinRemains })
                foreach (Thing thing in t.map.listerThings.ThingsOfDef(def).ToList())
                    (thing as AjinAnchor)?.DiscardForTests();
            GameComponent_Sato.Instance.ResetForTests();
            return t.ClearEchoes();
        }

        /// <summary>A colonist made Satō's Host, stripped, with a full pool; manifested unless told not to.</summary>
        private static Pawn Host(RimArtTestContext t, IntVec3 at, out EchoRecord record, bool manifest = true)
        {
            Pawn host = t.Host(Sato, at, out record, manifest);
            host.drafter.Drafted = false;
            RimArtTestContext.Hold(host);
            NoWimp(host);
            return host;
        }

        private static Pawn Target(RimArtTestContext t, IntVec3 at, bool armed = false, int stunTicks = 900)
        {
            return t.Target(at, stunTicks, armed: armed);
        }

        /// <summary>Cuts the Reset short: he rises after a few ticks.</summary>
        private static void Hurry(Pawn pawn, int ticks = 20)
        {
            Hediff_AjinReset reset = AjinReset.Resetting(pawn);
            if (reset != null) reset.riseTick = Find.TickManager.TicksGame + ticks;
        }

        private static int Wounds(Pawn pawn) => pawn.health.hediffSet.hediffs.Count(h => h is Hediff_Injury || h is Hediff_MissingPart);

        private static BodyPartRecord Part(Pawn pawn, BodyPartDef def, bool left = true) =>
            pawn.health.hediffSet.GetNotMissingParts().Where(p => p.def == def)
                .OrderBy(p => p.Label.Contains(left ? "left" : "right") ? 0 : 1).FirstOrDefault();

        /// <summary>Sever through the ability's own Apply: the part is picked as the button's menu would, then the cast lands.</summary>
        private static AjinAnchor Sever(Pawn host, BodyPartDef part, IntVec3 at)
        {
            Ability sever = host.abilities.GetAbility(SatoDefOf.AG_SatoSever);
            CompAbilityEffect_Sever comp = sever.CompOfType<CompAbilityEffect_Sever>();
            comp.chosen = Part(host, part);
            sever.Activate(new LocalTargetInfo(at), new LocalTargetInfo(at));
            return AjinReset.Anchors(host, host.Map).LastOrDefault();
        }

        private static float Charge => GameComponent_Echoes.Get.charge;

        // ---- Reset ---------------------------------------------------------------------------------------------

        [RimArtTest("Sato", "reset 1 a kill in hero form: 20 s Reset paid 20, lies downed, rises healed where he lay")]
        private static IEnumerable<int> ResetPaid(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            t.Check(AjinReset.IsAjin(host), "awakening gave the Ajin trait");
            t.Check(host.story.traits.HasTrait(TraitDefOf.Psychopath), "awakening gave Psychopath");
            host.TakeDamage(new DamageInfo(DamageDefOf.Cut, 8f, 0f, -1f, null, Part(host, BodyPartDefOf.Arm)));
            float before = Charge;
            host.Kill(null);
            yield return 3;
            Hediff_AjinReset reset = AjinReset.Resetting(host);
            t.Check(!host.Dead, "he is not dead");
            t.Check(reset != null && reset.fast, "a paid Reset started");
            t.Check(reset != null && reset.riseTick - reset.startTick == 1200, "delay 20 s (" + (reset?.riseTick - reset?.startTick) + " ticks)");
            t.Check(Mathf.Abs(before - Charge - 20f) < 0.5f, "paid 20 charge (" + (before - Charge).ToString("0.0") + ")");
            t.Check(host.Downed, "he lies downed");
            IntVec3 lay = host.Position;
            Hurry(host);
            yield return 30;
            t.Check(AjinReset.Resetting(host) == null, "the Reset ended");
            t.Check(host.Spawned && host.Position == lay, "he rose where he lay");
            t.Check(!host.Downed, "he stands");
            t.Check(Wounds(host) == 0, "no wounds left (" + Wounds(host) + ")");
            EndHost(record);
        }

        [RimArtTest("Sato", "reset 2 out of hero form: the slow Reset, one day, no charge, in place")]
        private static IEnumerable<int> ResetSlow(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record, manifest: false);
            float before = Charge;
            host.Kill(null);
            yield return 3;
            Hediff_AjinReset reset = AjinReset.Resetting(host);
            t.Check(reset != null && !reset.fast, "a slow Reset started");
            t.Check(reset != null && reset.riseTick - reset.startTick == 60000, "delay one day (" + (reset?.riseTick - reset?.startTick) + " ticks)");
            t.Check(Mathf.Abs(before - Charge) < 0.01f, "no charge taken");
            Hurry(host);
            yield return 30;
            t.Check(AjinReset.Resetting(host) == null && host.Spawned && host.Position == t.center && !host.Downed, "he rose in place");
            EndHost(record);
        }

        [RimArtTest("Sato", "reset 3 an explosion with two anchors: body gone, gear on the spot, rises naked at the arm, the hand crumbles")]
        private static IEnumerable<int> ResetExplosionAnchor(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            t.Equip(host, ThingDef.Named("Gun_Autopistol"));
            host.apparel.Wear((Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_BasicShirt"), ThingDefOf.Cloth));
            AjinAnchor hand = Sever(host, BodyPartDefOf.Hand, t.center + new IntVec3(5, 0, 0));
            yield return 2;
            AjinAnchor arm = Sever(host, BodyPartDefOf.Arm, t.center + new IntVec3(-5, 0, 2));
            yield return 25;
            t.Check(hand != null && arm != null && hand != arm, "two anchors lie on the floor");
            float before = Charge;
            IntVec3 spot = host.Position;
            GenExplosion.DoExplosion(spot, t.map, 1.9f, DamageDefOf.Bomb, null, 20);
            yield return 3;
            Hediff_AjinReset reset = AjinReset.Resetting(host);
            t.Check(reset != null && reset.bodyDestroyed, "the explosion destroyed the body");
            t.Check(!host.Spawned && reset?.holder == arm, "the arm holds him (" + reset?.holder?.PieceLabel + ")");
            t.Check(Mathf.Abs(before - Charge - 25f) < 0.5f, "paid the arm's 25 (" + (before - Charge).ToString("0.0") + ")");
            t.Check(spot.GetThingList(t.map).Any(th => th.def.IsWeapon) || GenRadial.RadialCellsAround(spot, 2f, true).Any(c => c.GetThingList(t.map).Any(th => th.def.IsWeapon)),
                "his pistol lies where he blew up");
            IntVec3 armCell = arm.Position;
            Hurry(host);
            yield return 20 + AjinAnchor.CrumbleTicks + 10;
            t.Check(host.Spawned && host.Position.InHorDistOf(armCell, 1.5f), "he rose at the arm (" + host.PositionHeld + ", arm at " + armCell + ")");
            t.Check(host.apparel.WornApparelCount == 0 && host.equipment.Primary == null, "he rose naked and empty-handed");
            t.Check(arm.Destroyed, "the arm was used up");
            t.Check(hand.Destroyed, "the other anchor crumbled");
            t.Check(Wounds(host) == 0, "arm and hand grew back");
            EndHost(record);
        }

        [RimArtTest("Sato", "reset 4 an explosion with no anchor: remains on the spot, rises there")]
        private static IEnumerable<int> ResetExplosionRemains(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            IntVec3 spot = host.Position;
            GenExplosion.DoExplosion(spot, t.map, 1.9f, DamageDefOf.Bomb, null, 20);
            yield return 3;
            Hediff_AjinReset reset = AjinReset.Resetting(host);
            t.Check(reset?.holder != null && reset.holder.def == SatoDefOf.AG_AjinRemains && reset.holder.Position == spot, "remains hold him on the spot");
            AjinAnchor remains = reset?.holder;
            Hurry(host);
            yield return 30;
            t.Check(host.Spawned && host.Position.InHorDistOf(spot, 1.5f) && !host.Downed, "he rose on the spot");
            t.Check(remains == null || remains.Destroyed, "the remains are gone");
            EndHost(record);
        }

        [RimArtTest("Sato", "reset 5 half the body's health lost while he lies there destroys it: he goes to his leg")]
        private static IEnumerable<int> ResetDamageDestroys(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            AjinAnchor leg = Sever(host, BodyPartDefOf.Leg, t.center + new IntVec3(4, 0, 0));
            yield return 25;
            host.Kill(null);
            yield return 3;
            Hediff_AjinReset reset = AjinReset.Resetting(host);
            t.Check(reset != null && reset.holder == null && host.Spawned, "the body lies on the map");
            float share = reset.destroyAtDamage;
            for (int i = 0; i < 200 && reset.holder == null && !reset.destroyPending; i++)
            {
                BodyPartRecord part = host.health.hediffSet.GetNotMissingParts().Where(p => p.depth == BodyPartDepth.Outside && p.coverageAbs > 0f).RandomElement();
                host.TakeDamage(new DamageInfo(DamageDefOf.Cut, 12f, 1f, -1f, null, part));
            }
            t.Log("damage taken " + reset.damageTaken.ToString("0") + " of " + share.ToString("0"));
            yield return 3;
            t.Check(reset.bodyDestroyed && reset.holder == leg, "the body was destroyed and the leg holds him");
            Hurry(host);
            yield return 30;
            t.Check(host.Spawned && !host.Downed && Wounds(host) == 0, "he rose whole at the leg");
            EndHost(record);
        }

        [RimArtTest("Sato", "reset 6 downed 5 s in hero form Resets him; out of hero form it does not")]
        private static IEnumerable<int> ResetDowned(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            // One record per Echo, so the second Ajin just gets the trait: an Ajin out of hero form.
            Pawn plain = t.Colonist(t.center + new IntVec3(4, 0, 0));
            plain.drafter.Drafted = false;
            plain.story.traits.GainTrait(new Trait(SatoDefOf.AG_Ajin));
            foreach (Pawn pawn in new[] { host, plain })
                foreach (BodyPartRecord leg in pawn.health.hediffSet.GetNotMissingParts().Where(p => p.def == BodyPartDefOf.Leg).ToList())
                    CompAbilityEffect_Sever.Cut(pawn, leg);
            yield return 5;
            t.Check(host.Downed && plain.Downed, "both are downed without legs");
            yield return 280;
            t.Check(AjinReset.Resetting(host) == null, "no Reset before 5 s");
            yield return 30;
            Hediff_AjinReset reset = AjinReset.Resetting(host);
            t.Check(reset != null && reset.cause == AjinCause.Downed, "the manifested one Reset himself");
            t.Check(AjinReset.Resetting(plain) == null, "the other did not");
            Hurry(host);
            yield return 30;
            t.Check(!host.Downed && Wounds(host) == 0, "he rose with both legs");
            EndHost(record);
        }

        // ---- Sever ---------------------------------------------------------------------------------------------

        [RimArtTest("Sato", "sever 1 each cut leaves a stump and an anchor where aimed; a third makes the oldest crumble")]
        private static IEnumerable<int> SeverAnchors(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            IntVec3 a = t.center + new IntVec3(3, 0, 0), b = t.center + new IntVec3(0, 0, 3), c = t.center + new IntVec3(-3, 0, 0);
            AjinAnchor first = Sever(host, BodyPartDefOf.Hand, a);
            t.Check(first != null && first.Position == a && first.piece == AjinPiece.Hand, "a hand anchor at the aimed cell");
            t.Check(host.health.hediffSet.hediffs.Any(h => h is Hediff_MissingPart m && m.Part.def == BodyPartDefOf.Hand), "his hand is missing");
            yield return 2;
            AjinAnchor second = Sever(host, DefDatabase<BodyPartDef>.GetNamed("Ear"), b);
            yield return 2;
            AjinAnchor third = Sever(host, BodyPartDefOf.Leg, c);
            yield return AjinAnchor.CrumbleTicks + 5;
            t.Check(first.Destroyed, "the oldest crumbled");
            t.Check(!second.Destroyed && !third.Destroyed && AjinReset.Anchors(host, t.map).Count == 2, "two anchors left");
            t.Check(third.piece == AjinPiece.Leg && second.piece == AjinPiece.Ear, "pieces: ear and leg");
            EndHost(record);
        }

        // ---- Headshot / Grenade -------------------------------------------------------------------------------

        [RimArtTest("Sato", "headshot 1 greyed below 20 charge; the cast plays dead 6 s and rises in place, anchor kept, hand back")]
        private static IEnumerable<int> Headshot(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            AjinAnchor hand = Sever(host, BodyPartDefOf.Hand, t.center + new IntVec3(4, 0, 0));
            yield return 25;
            Ability shot = host.abilities.GetAbility(SatoDefOf.AG_SatoHeadshotReset);
            echoes.charge = 10f;
            t.Check(shot.GizmoDisabled(out string why), "greyed at 10 charge (" + why + ")");
            echoes.charge = 100f;
            t.Check(!shot.GizmoDisabled(out _), "open at 100 charge");
            shot.QueueCastingJob(host, LocalTargetInfo.Invalid);
            foreach (int w in WaitFor(() => AjinReset.Resetting(host) != null, 120)) yield return w;
            Hediff_AjinReset reset = AjinReset.Resetting(host);
            t.Check(reset != null && reset.cause == AjinCause.Headshot && reset.riseTick - reset.startTick == 360, "a 6 s Reset (" + (reset?.riseTick - reset?.startTick) + ")");
            yield return 380;
            t.Check(AjinReset.Resetting(host) == null && host.Position == t.center && !host.Downed, "he rose in place");
            t.Check(!hand.Destroyed, "the hand anchor stays");
            t.Check(Wounds(host) == 0, "the hand grew back");
            EndHost(record);
        }

        [RimArtTest("Sato", "grenade 1 the blast hurts the enemy and the ally next to him, and he rises at his anchor")]
        private static IEnumerable<int> Grenade(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            AjinAnchor leg = Sever(host, BodyPartDefOf.Leg, t.center + new IntVec3(0, 0, 6));
            yield return 25;
            Pawn enemy = Target(t, t.center + new IntVec3(1, 0, 0));
            Pawn ally = t.Colonist(t.center + new IntVec3(-2, 0, 0));
            ally.drafter.FireAtWill = false;
            ally.apparel?.DestroyAll();
            int enemyWounds = Wounds(enemy), allyWounds = Wounds(ally);
            Ability grenade = host.abilities.GetAbility(SatoDefOf.AG_SatoGrenadeReset);
            grenade.Activate(host, host);
            yield return 5;
            t.Check(Wounds(enemy) > enemyWounds || enemy.Dead, "the enemy was hit");
            t.Check(Wounds(ally) > allyWounds || ally.Dead, "the ally was hit");
            Hediff_AjinReset reset = AjinReset.Resetting(host);
            t.Check(reset != null && reset.holder == leg, "the leg holds him");
            Hurry(host);
            yield return 30;
            t.Check(host.Spawned && host.Position.InHorDistOf(t.center + new IntVec3(0, 0, 6), 1.5f), "he rose at the leg");
            EndHost(record);
        }

        // ---- The Game ------------------------------------------------------------------------------------------

        [RimArtTest("Sato", "game 1 the mark: his shots x1.3 on it, others' not; a kill while marked refunds 15")]
        private static IEnumerable<int> TheGameMark(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            Pawn other = t.Colonist(t.center + new IntVec3(-2, 0, 0));
            Pawn enemy = Target(t, t.center + new IntVec3(6, 0, 0));
            Ability game = host.abilities.GetAbility(SatoDefOf.AG_SatoTheGame);
            game.Activate(enemy, enemy);
            yield return 2;
            Hediff_SatoGameMark mark = TheGame.MarkOn(enemy);
            t.Check(mark != null && mark.marker == host, "the enemy is marked by him");
            ThingDef rifle = ThingDef.Named("Gun_AssaultRifle");
            BodyPartRecord torso = enemy.RaceProps.body.corePart;
            float his = enemy.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 10f, 0f, -1f, host, torso, rifle)).totalDamageDealt;
            float theirs = enemy.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 10f, 0f, -1f, other, torso, rifle)).totalDamageDealt;
            t.Check(theirs > 0f && Mathf.Abs(his / theirs - 1.3f) < 0.01f, "his shot x1.3 of another colonist's (" + his + " vs " + theirs + ")");
            t.Check(!TheGame.IsHisShot(new DamageInfo(DamageDefOf.Blunt, 5f, 0f, -1f, host, torso, null), host), "his punches do not count");
            echoes.charge = 50f;
            enemy.Kill(null);
            yield return 2;
            t.Check(Mathf.Abs(Charge - 65f) < 0.5f, "the kill refunded 15 (" + Charge.ToString("0.0") + ")");
            EndHost(record);
        }

        // ---- Black Ghost ---------------------------------------------------------------------------------------

        [RimArtTest("Sato", "ghost 1 at a hand anchor: lives 20 s, half damage, fights a nearby enemy, one at a time, dissolves on revert")]
        private static IEnumerable<int> Ghost(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            IntVec3 at = t.center + new IntVec3(8, 0, 0);
            AjinAnchor hand = Sever(host, BodyPartDefOf.Hand, t.center + new IntVec3(6, 0, 0));
            yield return 25;
            Pawn enemy = Target(t, at + new IntVec3(3, 0, 0), stunTicks: 2000);
            Ability summon = host.abilities.GetAbility(SatoDefOf.AG_SatoBlackGhost);
            t.Check(summon.Activate(new LocalTargetInfo(hand.Position), LocalTargetInfo.Invalid), "summoned at the hand");
            yield return 2;
            Pawn ghost = BlackGhosts.Of(host);
            CompBlackGhost comp = ghost?.TryGetComp<CompBlackGhost>();
            t.Check(ghost != null && ghost.Position.InHorDistOf(hand.Position, 2.5f), "the ghost formed at the hand");
            t.Check(comp != null && comp.lifeTicks == 1200 && comp.piece == AjinPiece.Hand, "it lives 20 s (" + comp?.lifeTicks + ")");
            t.Check(summon.GizmoDisabled(out _), "one at a time");
            Pawn plain = t.Colonist(t.center + new IntVec3(-4, 0, -4));
            plain.apparel?.DestroyAll();
            float Injured(Pawn p) => p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
            float ghostBefore = Injured(ghost), plainBefore = Injured(plain);
            ghost.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 10f, 1f, -1f, enemy, ghost.RaceProps.body.corePart));
            plain.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 10f, 1f, -1f, enemy, plain.RaceProps.body.corePart));
            float dealt = Injured(ghost) - ghostBefore, plainDealt = Injured(plain) - plainBefore;
            t.Log("incoming damage factor " + ghost.GetStatValue(StatDefOf.IncomingDamageFactor) + ", ghost took " + dealt + ", a colonist " + plainDealt);
            t.Check(Mathf.Abs(dealt - plainDealt * 0.5f) < 0.05f, "it takes half what a colonist takes (" + dealt + " vs " + plainDealt + ")");
            foreach (int w in WaitFor(() => ghost.CurJobDef == JobDefOf.AttackMelee, 200)) yield return w;
            t.Log("ghost: " + RimArtTestContext.Describe(ghost));
            t.Check(ghost.CurJobDef == JobDefOf.AttackMelee && ghost.CurJob.targetA.Thing == enemy, "it went for the enemy");
            yield return t.ShotAs("sato ghost fights");
            EchoUtility.Revert(record, collapse: false);
            yield return CompBlackGhost.DissolveTicks + 10;
            t.Check(ghost.Destroyed, "it dissolved on revert");
            EndHost(null);
        }

        [RimArtTest("Sato", "ghost 2 tear an arm: the enemy loses it and drops its weapon, a limb lies behind the ghost, once per summon")]
        private static IEnumerable<int> Tear(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            Ability summon = host.abilities.GetAbility(SatoDefOf.AG_SatoBlackGhost);
            summon.Activate(new LocalTargetInfo(host), LocalTargetInfo.Invalid);
            yield return CompBlackGhost.BuildTicks + 5;
            Pawn ghost = BlackGhosts.Of(host);
            CompBlackGhost comp = ghost?.TryGetComp<CompBlackGhost>();
            IntVec3 next = GenAdj.CellsAdjacent8Way(ghost).First(c => c.Standable(t.map) && c.GetFirstPawn(t.map) == null);
            Pawn enemy = Target(t, next, armed: true, stunTicks: 2000);
            t.Check(enemy.equipment.Primary != null, "the enemy is armed");
            int arms = BlackGhostTear.Limbs(enemy, true).Count();
            Job job = JobMaker.MakeJob(SatoDefOf.AG_BlackGhostTear, enemy);
            job.count = 1;
            ghost.jobs.StartJob(job, JobCondition.InterruptForced);
            foreach (int w in WaitFor(() => comp.tearUsed, 200)) yield return w;
            yield return 60;
            t.Check(BlackGhostTear.Limbs(enemy, true).Count() == arms - 1, "one arm fewer");
            t.Check(enemy.equipment.Primary == null, "the weapon dropped");
            t.Check(t.map.listerThings.ThingsOfDef(SatoDefOf.AG_TornLimb).Any(), "a torn limb lies on the floor");
            t.Check(comp.tearUsed, "Tear is used up");
            EndHost(record);
        }

        // ---- pictures ------------------------------------------------------------------------------------------

        [RimArtTest("Sato", "pictures 1 anchors, headshot cast, lying, head-first cover, rise; explosion shell at the leg (screenshots)", 3000)]
        private static IEnumerable<int> Pictures(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            host.Rotation = Rot4.South;
            AjinAnchor hand = Sever(host, BodyPartDefOf.Hand, t.center + new IntVec3(2, 0, 0));
            yield return 2;
            AjinAnchor leg = Sever(host, BodyPartDefOf.Leg, t.center + new IntVec3(-2, 0, 1));
            yield return 8;
            yield return t.ShotAs("sato anchors in flight", t.center, 6f);
            yield return 30;
            yield return t.ShotAs("sato anchors", t.center, 6f);
            Ability shot = host.abilities.GetAbility(SatoDefOf.AG_SatoHeadshotReset);
            shot.QueueCastingJob(host, LocalTargetInfo.Invalid);
            foreach (int w in WaitFor(() => host.stances.curStance is Stance_Warmup, 30)) yield return w;
            yield return 24;
            yield return t.ShotAs("sato headshot draw", host.Position, 3.5f);
            foreach (int w in WaitFor(() => AjinReset.Resetting(host) != null, 60)) yield return w;
            yield return 4;
            yield return t.ShotAs("sato headshot shot", host.Position, 3.5f);
            yield return 60;
            yield return t.ShotAs("sato lying seep and pistol", host.Position, 3.5f);
            Hurry(host, 50);
            yield return 30;
            yield return t.ShotAs("sato cover head first", host.Position, 3.5f);
            yield return 30;
            yield return t.ShotAs("sato rise peel", host.Position, 3.5f);
            yield return 40;
            GenExplosion.DoExplosion(host.Position, t.map, 1.5f, DamageDefOf.Bomb, null, 10);
            yield return 5;
            t.Check(AjinReset.Resetting(host)?.holder == leg, "the leg holds him");
            yield return t.ShotAs("sato held at the leg", leg.Position, 3.5f);
            Hurry(host, 60);
            yield return 40;
            yield return t.ShotAs("sato shell at the leg", leg.Position, 3.5f);
            yield return 30;
            yield return t.ShotAs("sato peel at the leg", host.Position, 3.5f);
            yield return 40;
            EndHost(record);
        }

        [RimArtTest("Sato", "pictures 2 the Black Ghost builds, fights, tears an arm and dissolves (screenshots)", 3000)]
        private static IEnumerable<int> GhostPictures(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center + new IntVec3(-3, 0, 0), out EchoRecord record);
            Ability summon = host.abilities.GetAbility(SatoDefOf.AG_SatoBlackGhost);
            summon.Activate(new LocalTargetInfo(host), LocalTargetInfo.Invalid);
            yield return 30;
            Pawn ghost = BlackGhosts.Of(host);
            CompBlackGhost comp = ghost.TryGetComp<CompBlackGhost>();
            yield return t.ShotAs("sato ghost building", ghost.Position, 4f);
            yield return 60;
            yield return t.ShotAs("sato ghost idle", ghost.Position, 3.5f);
            Pawn enemy = Target(t, ghost.Position + new IntVec3(3, 0, 0), stunTicks: 3000);
            foreach (int w in WaitFor(() => comp.swipes > 0, 300)) yield return w;
            yield return 12;
            yield return t.ShotAs("sato ghost swipe", ghost.Position, 3.5f);
            enemy.Destroy();
            Pawn victim = Target(t, ghost.Position + new IntVec3(0, 0, -1), armed: true, stunTicks: 3000);
            Job job = JobMaker.MakeJob(SatoDefOf.AG_BlackGhostTear, victim);
            job.count = 1;
            ghost.jobs.StartJob(job, JobCondition.InterruptForced);
            foreach (int w in WaitFor(() => comp.tearStart >= 0, 120)) yield return w;
            yield return 30;
            yield return t.ShotAs("sato tear lifted", ghost.Position, 3.5f);
            yield return 34;
            yield return t.ShotAs("sato tear rip", ghost.Position, 3.5f);
            yield return 40;
            yield return t.ShotAs("sato tear dropped", ghost.Position, 3.5f);
            yield return 80;
            yield return t.ShotAs("sato torn limb", ghost.Position, 3.5f);
            EchoUtility.Revert(record, collapse: false);
            yield return 25;
            yield return t.ShotAs("sato ghost dissolving", ghost.Position, 3.5f);
            yield return 60;
            t.Check(ghost.Destroyed, "dissolved");
            EndHost(null);
        }

        // ---- surgery -------------------------------------------------------------------------------------------

        [RimArtTest("Sato", "surgery 1 a kidney taken out of him crumbles; out of anyone else it drops")]
        private static IEnumerable<int> Surgery(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record, manifest: false);
            Pawn plain = t.Colonist(t.center + new IntVec3(3, 0, 0));
            BodyPartRecord kidney = host.health.hediffSet.GetNotMissingParts().First(p => p.def.defName == "Kidney");
            BodyPartRecord otherKidney = plain.health.hediffSet.GetNotMissingParts().First(p => p.def.defName == "Kidney");
            Thing his = MedicalRecipesUtility.SpawnNaturalPartIfClean(host, kidney, host.Position, t.map);
            Thing theirs = MedicalRecipesUtility.SpawnNaturalPartIfClean(plain, otherKidney, plain.Position, t.map);
            t.Check(his == null, "no kidney from him");
            t.Check(theirs != null, "a kidney from the other colonist");
            EndHost(record);
        }
    }
}
