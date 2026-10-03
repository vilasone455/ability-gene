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
    /// Game tests of Solemn Lament (docs/ego-weapons.md, Weapon 2) with the real def, filter <c>ego: solemn lament</c>. The
    /// shooter's mood is set to full and its Shooting to 12, over the def's 4, so no shot rolls Corrosion. Bursts are started on the verb
    /// itself (one warmup, one burst), not with an attack job, which would keep shooting.
    /// </summary>
    public static class Tests_EgoSolemnLament
    {
        private static CompEgoSolemnLament Arm(RimArtTestContext t, Pawn shooter)
        {
            t.Equip(shooter, EgoDefOf.AG_EgoSolemnLament);
            shooter.drafter.FireAtWill = false;
            shooter.needs.mood.CurLevel = 1f;
            shooter.skills.GetSkill(SkillDefOf.Shooting).Level = 12;
            return CompEgoSolemnLament.HeldBy(shooter);
        }

        private static GameComponent_EgoSolemnLament Game => GameComponent_EgoSolemnLament.Instance;

        private static int Stacks(Pawn pawn) => EgoButterfly.Stacks(pawn);

        /// <summary>
        /// A hostile at full consciousness: its hediffs removed, no Wimp, and clothed against the cold map. Butterfly's numbers
        /// are for an unhurt pawn: 9 stacks leave 32.5 %, just over Core's 30 %, and a raider's old wound (pain, a scarred
        /// lung) or shivering (-5 %) takes enough to keep it down at 9.
        /// </summary>
        private static Pawn Unhurt(RimArtTestContext t, IntVec3 at, Faction faction = null)
        {
            Pawn pawn = t.Target(at, bare: false, faction: faction);
            NoWimp(pawn);
            pawn.health.RemoveAllHediffs();
            return t.Note(pawn);
        }

        /// <summary>
        /// Stacks, consciousness and health. A wound shows as health below the note taken at spawn (<see cref="RimArtTestContext.Struck"/>):
        /// a generated raider often already carries an old injury. Butterfly lowers consciousness, not health.
        /// </summary>
        private static string State(Pawn pawn) => pawn.LabelShort + " " + (pawn.Dead ? "dead" : Stacks(pawn) + " stacks, consciousness "
            + pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness).ToStringPercent() + ", health "
            + pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent() + (pawn.Downed ? ", DOWN" : ""));

        /// <summary>One burst at <paramref name="target"/>: the warmup, then shots until the verb stops bursting. Logs every shot.</summary>
        private static IEnumerable<int> Burst(RimArtTestContext t, Pawn shooter, CompEgoSolemnLament gun, LocalTargetInfo target, List<string> shots)
        {
            Verb verb = gun.PrimaryVerb;
            bool started = verb.TryStartCastOn(target);
            int start = t.Now, seen = 0;
            t.Log(start + " burst ordered: " + started + ", ammo " + gun.Ammo + ", next " + (gun.nextWhite ? "white" : "black"));
            if (!started) yield break;
            foreach (int wait in WaitFor(() => !verb.Bursting && !(shooter.stances.curStance is Stance_Warmup), 240))
            {
                EgoSolemnLamentBurstCast burst = Game.Bursts.FirstOrDefault(b => b.verb == verb);
                while (burst != null && burst.ShotCount > seen)
                {
                    seen++;
                    string line = "+" + (t.Now - start) + " shot " + seen + ": " + (target.Thing is Pawn p ? State(p) : target.Thing?.LabelShort ?? "cell") + ", ammo " + gun.Ammo;
                    shots.Add(line);
                    t.Log("  " + line);
                }
                yield return wait;
            }
        }

        [RimArtTest("Ego", "solemn lament: the guns take turns, white stacks and black hurts; the cap downs and they stop at it", 2400)]
        public static IEnumerable<int> Pair(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            Pawn shooter = t.Colonist(t.center);
            CompEgoSolemnLament gun = Arm(t, shooter);
            Pawn target = t.Target(t.center + new IntVec3(5, 0, 0));
            NoWimp(target);
            t.Log("verb: range " + gun.PrimaryVerb.EffectiveRange + ", burst " + gun.PrimaryVerb.verbProps.burstShotCount + ", "
                + gun.PrimaryVerb.verbProps.ticksBetweenBurstShots + " ticks apart; cap " + EgoButterflyExtension.Of.cap + "; " + State(target));

            // The first shot alone: white, 2 stacks, no wound.
            var shots = new List<string>();
            gun.PrimaryVerb.TryStartCastOn(target);
            foreach (int wait in WaitFor(() => Game.Bursts.Any(b => b.ShotCount >= 1), 120)) yield return wait;
            t.Log(t.Now + " after shot 1: " + State(target));
            t.Check(Stacks(target) == 2 && !t.Struck(target), "shot 1 is white: 2 stacks, no wound");
            foreach (int wait in WaitFor(() => Game.Bursts.Any(b => b.ShotCount >= 2), 60)) yield return wait;
            t.Log(t.Now + " after shot 2: " + State(target));
            t.Check(Stacks(target) == 3 && t.Struck(target), "shot 2 is black: 1 more stack and a wound");
            // From here the black shots' damage is absorbed and only the stacks are under test. A black shot can kill a
            // pawn near the cap: a brain wound or pain takes the consciousness Butterfly leaves (9 stacks leave 32.5 %).
            Shield(target);
            foreach (int wait in WaitFor(() => !gun.PrimaryVerb.Bursting, 120)) yield return wait;
            t.Log(t.Now + " burst 1 over: " + State(target) + ", ammo " + gun.Ammo);
            t.Check(Stacks(target) == 6 && gun.Ammo == 16, "a burst of 4 put on 2 + 1 + 2 + 1 = 6 stacks and spent 4 rounds");
            t.Check(gun.nextWhite, "the next burst starts white again (4 shots, even)");

            // Burst 2 reaches the cap and downs it; burst 3 at the downed pawn adds nothing past the cap.
            yield return 70;
            foreach (int wait in Burst(t, shooter, gun, target, shots)) yield return wait;
            t.Log(t.Now + " burst 2 over: " + State(target));
            t.Check(Stacks(target) == 10, "burst 2 stopped at the cap, 10 (" + Stacks(target) + ")");
            t.Check(target.Downed, "at the cap the pawn is down");
            yield return 70;
            foreach (int wait in Burst(t, shooter, gun, target, shots)) yield return wait;
            t.Log(t.Now + " burst 3 over: " + State(target));
            t.Check(!target.Dead && Stacks(target) == 10, "the guns never put on more than the cap, so Butterfly cannot kill it");
        }

        [RimArtTest("Ego", "solemn lament: the pool runs dry, the pair reloads, and stacks fade", 1800)]
        public static IEnumerable<int> PoolAndFade(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            Pawn shooter = t.Colonist(t.center);
            CompEgoSolemnLament gun = Arm(t, shooter);
            Pawn target = t.Target(t.center + new IntVec3(4, 0, 0));
            for (int i = 0; i < 19; i++) gun.Spend();
            t.Log("19 rounds spent: ammo " + gun.Ammo);
            var shots = new List<string>();
            foreach (int wait in Burst(t, shooter, gun, target, shots)) yield return wait;
            t.Check(shots.Count == 1, "the burst stopped after the pool's last round (" + shots.Count + " shot)");
            t.Check(gun.Reloading && gun.Ammo == 0, "empty, the pair reloads");
            t.Check(!gun.PrimaryVerb.Available() && !gun.PrimaryVerb.TryStartCastOn(target), "and cannot fire while it does");
            int left = gun.ReloadTicksLeft;
            t.Check(left > 150 && left <= 180, "for the def's 3 s (" + left + " ticks left)");
            foreach (int wait in WaitFor(() => !gun.Reloading, 240)) yield return wait;
            t.Check(gun.Ammo == 20, "reloaded to 20 (" + gun.Ammo + ")");

            // Fade: 10 stacks down the pawn; one fades every 10 s, and at 9 it stands again.
            Pawn faded = Unhurt(t, t.center + new IntVec3(0, 0, -4));
            t.Log("before the stacks: " + State(faded));
            EgoButterfly.Add(faded, 10, EgoButterflyExtension.Of.cap);
            yield return 2;
            t.Log(t.Now + " " + State(faded));
            t.Check(faded.Downed, "10 stacks down an unhurt pawn");
            int start = t.Now;
            foreach (int wait in WaitFor(() => Stacks(faded) < 10, 700)) yield return wait;
            yield return 2;
            t.Log("+" + (t.Now - start) + " " + State(faded));
            t.Check(t.Now - start >= 580 && t.Now - start <= 640, "one stack faded after 10 s (" + (t.Now - start) + " ticks)");
            t.Check(Stacks(faded) == 9 && !faded.Downed, "at 9 stacks (32.5 % consciousness) it is up again");
        }

        [RimArtTest("Ego", "solemn lament: corroded, it walks to the nearest pawn and the cloud kills a downed one at 20", 3600)]
        public static IEnumerable<int> Funeral(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c + new IntVec3(-4, 0, 0));
            CompEgoSolemnLament gun = Arm(t, wielder);
            Pawn downed = t.Colonist(c + new IntVec3(2, 0, 0));
            NoWimp(downed);
            EgoButterfly.Add(downed, 10, EgoButterflyExtension.Of.cap);
            Pawn far = t.Target(c + new IntVec3(-4, 0, 11));
            yield return 2;
            t.Log("wielder " + wielder.Position + "; " + State(downed) + " at " + downed.Position + "; enemy at " + far.Position);
            t.Check(downed.Downed, "the colonist is down from 10 stacks");

            t.Check(EgoCorrosion.Corrode(wielder, gun), "Corrode started the state");
            int start = t.Now, lastLog = 0;
            bool walked = false;
            foreach (int wait in WaitFor(() => downed.Dead || !wielder.InMentalState, 1800))
            {
                if (wielder.CurJobDef == EgoDefOf.AG_EgoCorrodedWalk) walked = true;
                if (t.Now - lastLog >= 60)
                {
                    lastLog = t.Now;
                    t.Log("+" + (t.Now - start) + " " + Describe(wielder) + " | " + State(downed) + " | " + State(far) + " | wielder " + Stacks(wielder) + " stacks");
                }
                yield return wait;
            }
            t.Log("+" + (t.Now - start) + " " + State(downed) + " | wielder at " + wielder.Position + " | coffin " + (Game.CoffinOf(wielder) != null ? "up" : "none"));
            t.Check(walked, "the corroded wielder walked (AG_EgoCorrodedWalk)");
            t.Check(downed.Dead, "the downed colonist died in the cloud");
            t.Check(downed.Dead && (downed.Corpse?.InnerPawn.health.hediffSet.GetFirstHediffOfDef(EgoDefOf.AG_EgoButterfly) as Hediff_EgoButterfly)?.Stacks >= 20,
                "at 20 stacks");
            t.Check(Stacks(wielder) == 0, "the wielder took no stacks");
            t.Check(t.Untouched(far) && Stacks(far) == 0, "the enemy 11 cells off was not in the cloud");
            t.Check(Game.MarkOf(downed)?.mark.Dead == true, "the picture's mark has the funeral (the cover lifts off)");
            wielder.MentalState?.RecoverFromState();
        }

        [RimArtTest("Ego", "solemn lament: corroded, the walk turns to a pawn that comes nearer", 1800)]
        public static IEnumerable<int> WalkTurns(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoSolemnLament gun = Arm(t, wielder);
            IntVec3 farCell = c + new IntVec3(18, 0, 0);
            if (!farCell.InBounds(t.map) || !farCell.Standable(t.map)) farCell = c + new IntVec3(12, 0, 0);
            Pawn far = t.Target(farCell, stunTicks: 1800);
            t.Check(EgoCorrosion.Corrode(wielder, gun), "Corrode started the state");
            foreach (int wait in WaitFor(() => wielder.CurJobDef == EgoDefOf.AG_EgoCorrodedWalk, 300)) yield return wait;
            t.Log(t.Now + " " + Describe(wielder) + " -> " + (wielder.CurJob?.targetA.Thing?.LabelShort ?? "none") + ", far at " + far.Position);
            if (!t.Check(wielder.CurJob?.targetA.Thing == far, "the wielder walks to the only other pawn")) yield break;

            // A pawn just ahead of the wielder and 3 cells to the side: nearer than the one it walks to for the rest of the walk.
            Pawn near = t.Target(wielder.Position + new IntVec3(2, 0, 3), stunTicks: 1800, faction: far.Faction);
            int start = t.Now;
            bool Turned() => wielder.CurJob?.targetA.Thing == near
                || (wielder.CurJobDef == EgoDefOf.AG_EgoCorrodedHold && wielder.Position.AdjacentTo8WayOrInside(near.Position));
            foreach (int wait in WaitFor(Turned, 180))
            {
                if ((t.Now - start) % 30 == 0)
                    t.Log("+" + (t.Now - start) + " " + Describe(wielder) + " -> " + (wielder.CurJob?.targetA.Thing?.LabelShort ?? "none")
                        + "; far " + wielder.Position.DistanceTo(far.Position).ToString("0.0") + ", near " + wielder.Position.DistanceTo(near.Position).ToString("0.0"));
                yield return wait;
            }
            t.Log("+" + (t.Now - start) + " " + Describe(wielder) + " -> " + (wielder.CurJob?.targetA.Thing?.LabelShort ?? "none"));
            t.Check(Turned(), "within the 1 s expiry (and the pawn's hash tick) the walk turned to the nearer pawn (" + (t.Now - start) + " ticks)");
            wielder.MentalState?.RecoverFromState();
        }

        [RimArtTest("Ego", "solemn lament: a corroded wielder killed outright ends its coffin", 1200)]
        public static IEnumerable<int> CoffinOnDeath(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoSolemnLament gun = Arm(t, wielder);
            t.Target(c + new IntVec3(8, 0, 0), stunTicks: 1200);
            t.Check(EgoCorrosion.Corrode(wielder, gun), "Corrode started the state");
            yield return 90;
            EgoSolemnLamentCoffinCast coffin = Game.CoffinOf(wielder);
            t.Log(t.Now + " " + Describe(wielder) + " | coffin " + (coffin == null ? "none" : coffin.Running ? "running" : "ended"));
            if (!t.Check(coffin != null && coffin.Running, "the coffin rose with the corrosion")) yield break;

            wielder.Kill(null);
            yield return 2;
            t.Log(t.Now + " killed outright: dead " + wielder.Dead + ", the state still set on the corpse " + (wielder.MentalState is MentalState_EgoCorroded)
                + " | coffin " + (coffin.Running ? "running" : "ended") + ", drawing the guns " + Game.Drawing(wielder));
            t.Check(!coffin.Running && Game.CoffinOf(wielder) == null, "the coffin ended on its next tick, though the state's PostEnd never came");
            t.Check(!Game.Drawing(wielder), "it no longer counts as drawing the wielder's guns");
            int ended = t.Now;
            foreach (int wait in WaitFor(() => !Game.Coffins.Contains(coffin), 600)) yield return wait;
            t.Check(!Game.Coffins.Contains(coffin), "it sank and was dropped " + (t.Now - ended) + " ticks later");
        }

        [RimArtTest("Ego", "solemn lament: Overclock stacks standing hostiles only, never past the cap", 1800)]
        public static IEnumerable<int> Overclock(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoSolemnLament gun = Arm(t, wielder);
            Pawn ally = t.Colonist(c + new IntVec3(0, 0, 2));
            Pawn fresh = t.Target(c + new IntVec3(2, 0, 0));
            // At full consciousness, so it stands at 9 and Overclock (standing hostiles only) takes it to the cap.
            Pawn nearCap = Unhurt(t, c + new IntVec3(-2, 0, 0), fresh.Faction);
            Pawn down = t.Target(c + new IntVec3(0, 0, -2), faction: fresh.Faction);
            NoWimp(fresh);
            EgoButterfly.Add(nearCap, 8, EgoButterflyExtension.Of.cap);
            HealthUtility.DamageUntilDowned(down, allowBleedingWounds: false);
            t.Log(State(fresh) + " | " + State(nearCap) + " | " + State(down) + " | " + State(ally));

            var command = new Command_EgoOverclock(gun, wielder);
            if (!t.Check(!command.Disabled, "Overclock is on with standing hostiles within 3 cells")) yield break;
            command.action();
            int start = t.Now;
            foreach (int wait in WaitFor(() => wielder.CurJobDef != EgoDefOf.AG_EgoOverclock, 600))
            {
                if ((t.Now - start) % 60 == 1)
                    t.Log("+" + (t.Now - start) + " " + State(fresh) + " | " + State(nearCap) + " | " + State(down) + " | " + State(ally));
                yield return wait;
            }
            t.Log("overclock over after " + (t.Now - start) + " ticks: " + State(fresh) + " | " + State(nearCap) + " | " + State(down) + " | " + State(ally));
            t.Check(Stacks(fresh) == 5, "the standing hostile took 1 stack a second for 5 s (" + Stacks(fresh) + ")");
            t.Check(Stacks(nearCap) == 10, "the one at 8 stopped at the cap, 10 (" + Stacks(nearCap) + ")");
            t.Check(Stacks(down) == 0, "the downed hostile took none");
            t.Check(Stacks(ally) == 0 && Stacks(wielder) == 0, "the ally and the wielder took none");
            t.Check(wielder.Position == c, "Overclock held still");
            t.Check(wielder.needs.mood.thoughts.memories.GetFirstMemoryOfDef(EgoDefOf.AG_EgoOverclocked) != null, "the cost was paid");
        }
    }
}
