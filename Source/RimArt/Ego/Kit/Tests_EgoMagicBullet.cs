using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests of Magic Bullet (docs/ego-weapons.md, Weapon 1) with the real def, filter <c>ego: magic bullet</c>. The
    /// shooter's mood is set to full, so above the minor break line no shot rolls Corrosion. Shots are started on the verb
    /// itself (one warmup, one shot), not with an attack job, which would keep shooting. Pawns that must have no opinion
    /// of anyone (the seventh's checks) are animals.
    /// </summary>
    public static class Tests_EgoMagicBullet
    {
        private static CompEgoMagicBullet Arm(RimArtTestContext t, Pawn shooter)
        {
            t.Equip(shooter, EgoDefOf.AG_EgoMagicBullet);
            shooter.drafter.FireAtWill = false;
            shooter.needs.mood.CurLevel = 1f;
            return CompEgoMagicBullet.HeldBy(shooter);
        }

        /// <summary>An animal with no faction and no opinions, held still: something on the line that is not a person.</summary>
        private static Pawn Animal(RimArtTestContext t, IntVec3 at, string kind = "Boar")
        {
            Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDef.Named(kind));
            GenSpawn.Spawn(pawn, at, t.map);
            Hold(pawn);
            return t.Note(pawn);
        }

        /// <summary>One shot at <paramref name="target"/>: the verb's warmup, then the line. The shot that went off, or null after 5 s.</summary>
        private static IEnumerable<int> Shoot(RimArtTestContext t, Pawn shooter, CompEgoMagicBullet gun, LocalTargetInfo target, List<EgoMagicBulletCast> into)
        {
            into.Clear();
            int start = t.Now;
            bool started = gun.PrimaryVerb.TryStartCastOn(target);
            t.Log(t.Now + " order: " + started + ", next shot " + gun.NextShot + " | " + Describe(shooter));
            foreach (int wait in WaitFor(() => Last(shooter)?.Fired == true && Last(shooter).fireTick >= start, 300)) yield return wait;
            EgoMagicBulletCast cast = Last(shooter);
            if (cast != null && cast.Fired && cast.fireTick >= start)
            {
                into.Add(cast);
                t.Log("+" + (cast.fireTick - start) + " shot " + cast.shot + (cast.Seventh ? " (the seventh)" : "") + " hit "
                    + (cast.victims.Count == 0 ? "nobody" : string.Join(", ", cast.victims.Select(v => v.LabelShort)))
                    + "; walls punched " + cast.hits.Count(h => h.Wall) + "; count now " + gun.count + (gun.CoolingDown ? ", cooling " + gun.CooldownTicksLeft + " ticks" : ""));
            }
            yield return 2;
        }

        private static EgoMagicBulletCast Last(Pawn shooter) =>
            GameComponent_EgoMagicBullet.Instance.Casts.LastOrDefault(c => c.shooter == shooter);

        private static string Health(Pawn pawn) => pawn.LabelShort + " " + (pawn.Dead ? "dead" : pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent() + (pawn.Downed ? " downed" : ""));

        [RimArtTest("Ego", "magic bullet: the line goes through a wall and hits everyone on it", 1200)]
        public static IEnumerable<int> Line(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_EgoMagicBullet.Instance.Clear();
            IntVec3 c = t.center;
            Pawn shooter = t.Colonist(c + new IntVec3(-8, 0, 0));
            CompEgoMagicBullet gun = Arm(t, shooter);
            Pawn ally = t.Colonist(c + new IntVec3(-5, 0, 0));
            for (int z = -1; z <= 1; z++) t.Wall(c + new IntVec3(-2, 0, z));
            Pawn enemy = t.Target(c + new IntVec3(2, 0, 0));
            Pawn behind = t.Target(c + new IntVec3(8, 0, 0), faction: enemy.Faction);
            Pawn beside = t.Target(c + new IntVec3(0, 0, 2), faction: enemy.Faction);
            IntVec3 farCell = shooter.Position + new IntVec3(42, 0, 0);
            Pawn far = farCell.InBounds(t.map) && farCell.Standable(t.map) ? Animal(t, farCell) : null;
            t.Log("shooter " + shooter.Position + ", ally " + ally.Position + ", wall x " + (c.x - 2) + ", target " + enemy.Position
                + ", behind " + behind.Position + ", beside " + beside.Position + ", far " + (far == null ? "none (cell blocked)" : far.Position.ToString())
                + "; verb range " + gun.PrimaryVerb.EffectiveRange + ", warmup " + gun.PrimaryVerb.verbProps.warmupTime + " s");
            t.Check(gun.PrimaryVerb.CanHitTarget(enemy), "the target behind the wall can be ordered");

            var shot = new List<EgoMagicBulletCast>();
            foreach (int wait in Shoot(t, shooter, gun, enemy, shot)) yield return wait;
            if (!t.Check(shot.Count == 1, "the shot went off")) yield break;
            t.Log(Health(ally) + " | " + Health(enemy) + " | " + Health(behind) + " | " + Health(beside) + (far != null ? " | " + Health(far) : ""));
            t.Check(t.Hurt(ally), "the ally on the line was hit");
            t.Check(t.Hurt(enemy), "the target behind the wall was hit");
            t.Check(t.Hurt(behind), "the pawn behind the target, inside the range, was hit");
            t.Check(t.Untouched(beside), "the pawn 2 cells off the line was not");
            if (far != null) t.Check(t.Untouched(far), "the animal 42 cells away, past the range of 40, was not");
            t.Check(shot[0].victims.SequenceEqual(new[] { ally, enemy, behind }), "the hits went nearest first: ally, target, behind");
            t.Check(shot[0].hits.Any(h => h.Wall && Mathf.Abs(h.At.x - (c.x - 1.5f)) < 0.1f), "the wall cell on the line is punched in the picture");
            t.Check(c.x - 2 >= 0 && new IntVec3(c.x - 2, 0, c.z).GetEdifice(t.map)?.def == ThingDefOf.Wall, "the wall still stands");
            t.Check(gun.count == 1 && !gun.CoolingDown, "the count is 1");
            t.Check(!shooter.InMentalState, "a shooter at full mood did not corrode");
            t.Check(!HeldWeaponHide.Shown(gun.parent), "while the picture is up Core does not draw the rifle");
            foreach (int wait in WaitFor(() => !GameComponent_EgoMagicBullet.Instance.Drawing(shooter), 300)) yield return wait;
            t.Check(HeldWeaponHide.Shown(gun.parent), "once the picture has gone Core draws it again");
        }

        [RimArtTest("Ego", "magic bullet: a shot at a building hits it and every pawn on the line", 1200)]
        public static IEnumerable<int> Building(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_EgoMagicBullet.Instance.Clear();
            IntVec3 c = t.center;
            Pawn shooter = t.Colonist(c + new IntVec3(-6, 0, 0));
            CompEgoMagicBullet gun = Arm(t, shooter);
            Thing wall = t.Wall(c, owned: false);
            Pawn before = t.Target(c + new IntVec3(-3, 0, 0), stunTicks: 600);
            Pawn behind = t.Target(c + new IntVec3(4, 0, 0), stunTicks: 600, faction: before.Faction);
            int hp = wall.HitPoints;
            t.Log("shooter " + shooter.Position + ", target wall " + wall.Position + " (" + hp + " hp), before " + before.Position + ", behind " + behind.Position);

            var shot = new List<EgoMagicBulletCast>();
            foreach (int wait in Shoot(t, shooter, gun, wall, shot)) yield return wait;
            if (!t.Check(shot.Count == 1, "the shot at the wall went off")) yield break;
            t.Log("wall " + (wall.Destroyed ? "destroyed" : wall.HitPoints + " / " + hp + " hp") + " | " + Health(before) + " | " + Health(behind));
            t.Check(shot[0].struck == wall && (wall.Destroyed || wall.HitPoints < hp), "the wall it was aimed at took the damage");
            t.Check(t.Hurt(before) && t.Hurt(behind), "the pawns in front of the wall and behind it were hit");
            t.Check(shot[0].hits.Count(h => h.Wall) == 1, "the wall has one hole in the picture, not two");
            t.Check(gun.count == 1, "the shot counted");
        }

        [RimArtTest("Ego", "magic bullet: the seventh goes to the beloved, through everyone between, then the gun rests", 2400)]
        public static IEnumerable<int> Seventh(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_EgoMagicBullet.Instance.Clear();
            IntVec3 c = t.center;
            Pawn shooter = t.Colonist(c);
            CompEgoMagicBullet gun = Arm(t, shooter);
            Pawn lover = t.Colonist(c + new IntVec3(-7, 0, 0));
            shooter.relations.AddDirectRelation(PawnRelationDefOf.Lover, lover);
            Pawn between = Animal(t, c + new IntVec3(-4, 0, 0));
            Pawn aimed = Animal(t, c + new IntVec3(6, 0, 0));
            t.Log("opinion of the lover " + shooter.relations.OpinionOf(lover) + "; beloved " + EgoMagicBullet.Beloved(shooter)?.LabelShort);
            if (!t.Check(EgoMagicBullet.Beloved(shooter) == lover, "the lover is the beloved (the only other person)")) yield break;

            gun.count = 6;
            t.Check(gun.NextIsSeventh, "after six shots the next is the seventh");
            var shot = new List<EgoMagicBulletCast>();
            foreach (int wait in Shoot(t, shooter, gun, aimed, shot)) yield return wait;
            if (!t.Check(shot.Count == 1, "the seventh went off")) yield break;
            t.Log(Health(lover) + " | " + Health(between) + " | " + Health(aimed));
            t.Check(shot[0].Seventh, "it was the seventh");
            t.Check(t.Hurt(lover), "the lover was hit, though the shot was ordered at the boar on the other side");
            t.Check(t.Hurt(between), "the boar between was hit");
            t.Check(t.Untouched(aimed), "the boar it was aimed at was not");
            t.Check(gun.count == 0 && gun.CoolingDown, "the count is back to 0 and the gun rests");
            int left = gun.CooldownTicksLeft;
            t.Check(left > 1150 && left <= 1200, "for the def's 20 s (" + left + " ticks left)");
            t.Check(!gun.PrimaryVerb.Available(), "the verb is unavailable while it rests");
            // A boar that was shot may turn manhunter; it has done its part.
            if (between.Spawned) between.Destroy();

            // An order while it rests does nothing.
            t.Note(aimed);
            bool started = gun.PrimaryVerb.TryStartCastOn(aimed);
            yield return 150;
            t.Log(t.Now + " order while resting: " + started + " | " + Health(aimed) + " | count " + gun.count);
            t.Check(!started && t.Untouched(aimed) && gun.count == 0, "no shot while it rests");

            // Once it has rested: shot one, aimed again.
            gun.cooldownUntilTick = t.Now;
            foreach (int wait in Shoot(t, shooter, gun, aimed, shot)) yield return wait;
            t.Check(shot.Count == 1 && !shot[0].Seventh && shot[0].victims.Contains(aimed), "after the rest shot one goes where it is aimed");
            t.Check(gun.count == 1, "the count is 1");
        }

        [RimArtTest("Ego", "magic bullet: the seventh's fallbacks (a bonded animal, then the shooter); the count stays on the gun", 2400)]
        public static IEnumerable<int> Fallbacks(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_EgoMagicBullet.Instance.Clear();
            IntVec3 c = t.center;
            Pawn shooter = t.Colonist(c);
            CompEgoMagicBullet gun = Arm(t, shooter);
            Pawn dog = Animal(t, c + new IntVec3(-3, 0, 2), "Husky");
            dog.SetFaction(Faction.OfPlayer);
            Pawn aimed = Animal(t, c + new IntVec3(6, 0, 0));
            t.Check(EgoMagicBullet.Beloved(shooter) == shooter, "no other person and no bond: the shooter");
            shooter.relations.AddDirectRelation(PawnRelationDefOf.Bond, dog);
            t.Check(EgoMagicBullet.Beloved(shooter) == dog, "no other person, a bonded dog: the dog");
            shooter.relations.RemoveDirectRelation(PawnRelationDefOf.Bond, dog);
            t.Log("beloved without the bond: " + EgoMagicBullet.Beloved(shooter)?.LabelShort);

            gun.count = 6;
            t.Note(shooter);
            var shot = new List<EgoMagicBulletCast>();
            foreach (int wait in Shoot(t, shooter, gun, aimed, shot)) yield return wait;
            t.Log(Health(shooter) + " | " + Health(dog) + " | " + Health(aimed));
            t.Check(shot.Count == 1 && shot[0].Seventh && shot[0].victims.SequenceEqual(new[] { shooter }), "the seventh with nobody to love hit the shooter");
            t.Check(t.Hurt(shooter) && t.Untouched(dog) && t.Untouched(aimed), "the shooter was hurt; the dog and the boar were not");

            // The count is the gun's: dropped and picked up by another colonist, it is unchanged.
            gun.cooldownUntilTick = t.Now;
            gun.count = 4;
            Pawn other = t.Colonist(c + new IntVec3(0, 0, -3));
            bool dropped = shooter.equipment.TryDropEquipment(gun.parent, out ThingWithComps lying, shooter.Position);
            yield return 2;
            lying.DeSpawn();
            other.equipment.AddEquipment(lying);
            CompEgoMagicBullet now = CompEgoMagicBullet.HeldBy(other);
            t.Log("dropped " + dropped + "; " + other.LabelShort + " holds " + (now?.parent.LabelShort ?? "nothing") + ", count " + now?.count);
            t.Check(now == gun && now.count == 4 && now.NextShot == 5, "another colonist picked it up at count 4, next shot 5");
        }

        [RimArtTest("Ego", "magic bullet: corroded it shoots the nearest pawn and counts; Overclock hits only hostiles and does not count", 3000)]
        public static IEnumerable<int> Corroded(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_EgoMagicBullet.Instance.Clear();
            IntVec3 c = t.center;
            Pawn shooter = t.Colonist(c);
            CompEgoMagicBullet gun = Arm(t, shooter);
            Pawn ally = t.Colonist(c + new IntVec3(0, 0, 3));
            Pawn enemy = t.Target(c + new IntVec3(-8, 0, 0));

            t.Check(EgoCorrosion.Corrode(shooter, gun), "Corrode started the state");
            int start = t.Now;
            t.Check(!HeldWeaponHide.Shown(gun.parent), "corroded, Core does not draw the rifle (the corroded look does)");
            foreach (int wait in WaitFor(() => Last(shooter)?.Fired == true, 400)) yield return wait;
            EgoMagicBulletCast first = Last(shooter);
            t.Log("+" + (t.Now - start) + " first corroded firing: " + (first?.Fired == true ? "shot " + first.shot + " hit " + string.Join(", ", first.victims.Select(v => v.LabelShort)) : "none")
                + " | " + Health(ally) + " | " + Health(enemy) + " | count " + gun.count);
            if (!t.Check(first != null && first.Fired, "the corroded gun fired")) yield break;
            int at = first.fireTick - start;
            t.Check(at >= 200 && at <= 250, "one 3 s interval (mental states tick at the pawn's interval rate), then the 0.45 s aim (" + at + " ticks)");
            t.Check(first.victims.Contains(ally) && t.Hurt(ally), "it shot the nearest pawn, the ally 3 cells away");
            t.Check(t.Untouched(enemy), "not the enemy 8 cells away, off that line");
            t.Check(gun.count == 1, "the corroded shot counted");
            shooter.MentalState?.RecoverFromState();
            yield return 2;
            t.Check(!shooter.InMentalState, "the state was ended for the Overclock half");
            shooter.health.RemoveAllHediffs();
            shooter.needs.mood.CurLevel = 1f;

            // Overclock, with the next shot the seventh: an ally on the line to the hostile, lines that pass through it.
            GameComponent_EgoMagicBullet.Instance.Clear();
            gun.count = 6;
            Pawn onLine = t.Colonist(c + new IntVec3(-4, 0, 0));
            t.Note(ally);
            var command = new Command_EgoOverclock(gun, shooter);
            if (!t.Check(!command.Disabled, "Overclock is on with a hostile in range")) yield break;
            command.action();
            int fired = 0;
            var seen = new HashSet<EgoMagicBulletCast>();
            start = t.Now;
            foreach (int wait in WaitFor(() => shooter.CurJobDef != EgoDefOf.AG_EgoOverclock, 600))
            {
                foreach (EgoMagicBulletCast cast in GameComponent_EgoMagicBullet.Instance.Casts)
                    if (cast.shooter == shooter && cast.Fired && seen.Add(cast))
                    {
                        fired++;
                        t.Log("+" + (cast.fireTick - start) + " overclock line, shot " + cast.shot + ": hit " + string.Join(", ", cast.victims.Select(v => v.LabelShort))
                            + " | " + Health(enemy));
                    }
                yield return wait;
            }
            t.Log("overclock ended after " + (t.Now - start) + " ticks, " + fired + " lines | " + Health(onLine) + " | " + Health(enemy) + " | count " + gun.count);
            t.Check(fired >= 1, "Overclock fired");
            t.Check(t.Untouched(onLine) && t.Untouched(ally), "the colonist on the line and the one beside were not hit");
            t.Check(t.Hurt(enemy), "the hostile was");
            t.Check(seen.All(s => !s.Seventh && s.victims.All(v => v.HostileTo(shooter))), "no line was the seventh, and every pawn hit was hostile");
            t.Check(gun.count == 6 && !gun.CoolingDown, "Overclock did not move the count: the next shot is still the seventh");
            t.Check(shooter.needs.mood.thoughts.memories.GetFirstMemoryOfDef(EgoDefOf.AG_EgoOverclocked) != null, "the Overclock cost was paid");
        }

        /// <summary>
        /// The corroded look (veins, eyes, wisps, the lit rifle) on three wielders Core draws at a low, a middle and a high
        /// height: it is placed from each pawn's own DrawPos.y, so it shows over every one of them. Close shots of each before
        /// the first corroded firing (about 200 ticks in), when they would shoot each other.
        /// </summary>
        [RimArtTest("Ego", "magic bullet: height, the corroded look over pawns drawn low, middle and high (screenshots)", 900)]
        public static IEnumerable<int> Height(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_EgoMagicBullet.Instance.Clear();
            IntVec3 c = t.center;
            Pawn[] wielders = HeightShots.Spread(t, c + new IntVec3(-3, 0, 0), c, c + new IntVec3(3, 0, 0));
            if (!t.Check(wielders.All(w => w != null), "a colonist for each height")) yield break;
            foreach (Pawn wielder in wielders)
                t.Check(EgoCorrosion.Corrode(wielder, Arm(t, wielder)), wielder.LabelShort + " corroded");
            int start = t.Now;
            foreach (int at in new[] { 30, 120 })
            {
                if (start + at > t.Now) yield return start + at - t.Now;
                foreach (Pawn wielder in wielders) yield return HeightShots.Shoot(t, "magic bullet height " + wielder.LabelShort + " " + at, wielder.Position, wielder);
            }
            t.Check(GameComponent_EgoMagicBullet.Instance.Casts.Count == 0, "no corroded firing yet: the shots show the held look");
        }
    }
}
