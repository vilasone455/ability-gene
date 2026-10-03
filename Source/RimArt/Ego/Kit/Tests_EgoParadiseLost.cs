using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests of Paradise Lost (docs/ego-weapons.md, Weapon 4) with the real def, filter <c>ego: paradise lost</c>. The
    /// wielder's mood is full and its Shooting 12, over the requirement, so no shot rolls Corrosion. Shots are started on
    /// the verb itself (one warmup, one shot), not with an attack job, which would keep shooting.
    /// </summary>
    public static class Tests_EgoParadiseLost
    {
        private static CompEgoParadiseLost Arm(RimArtTestContext t, Pawn wielder)
        {
            t.Equip(wielder, EgoDefOf.AG_EgoParadiseLost);
            wielder.drafter.FireAtWill = false;
            wielder.needs.mood.CurLevel = 1f;
            wielder.skills.GetSkill(SkillDefOf.Shooting).Level = 12;
            return CompEgoParadiseLost.HeldBy(wielder);
        }

        private static GameComponent_EgoParadiseLost Game => GameComponent_EgoParadiseLost.Instance;

        private static float Health(Pawn pawn) => pawn.health.summaryHealth.SummaryHealthPercent;

        private static string State(Pawn pawn) => pawn.LabelShort + " " + (pawn.Dead ? "dead" : Health(pawn).ToStringPercent()
            + (EgoParadiseLost.Slowed(pawn) ? ", slowed (speed " + pawn.GetStatValue(StatDefOf.MoveSpeed).ToString("0.00") + ")" : "") + (pawn.Downed ? ", DOWN" : ""));

        private static string States(params Pawn[] pawns) => string.Join(" | ", pawns.Select(State));

        /// <summary>Where a pawn stands for the room hit: its cell, whether it shares a room with <paramref name="aimed"/>, downed, its job.</summary>
        private static string Place(Pawn pawn, Thing aimed)
        {
            if (pawn.Dead || !pawn.Spawned) return pawn.LabelShort + (pawn.Dead ? " dead" : " not spawned");
            var mine = EgoParadiseLost.RoomsOf(pawn, new List<Room>());
            var theirs = EgoParadiseLost.RoomsOf(aimed, new List<Room>());
            return pawn.LabelShort + " at " + pawn.Position + (mine.Any(theirs.Contains) ? " in the aimed room" : " NOT in the aimed room")
                + (pawn.Downed ? ", DOWN" : "") + ", " + Describe(pawn).Substring(pawn.LabelShort.Length).Trim();
        }

        /// <summary>One shot at <paramref name="target"/>: the warmup, then the shot.</summary>
        private static IEnumerable<int> Shoot(RimArtTestContext t, Pawn wielder, CompEgoParadiseLost staff, Thing target)
        {
            Verb verb = staff.PrimaryVerb;
            bool started = verb.TryStartCastOn(target);
            t.Log(t.Now + " shot ordered at " + target.LabelShort + ": " + started + " (warmup " + verb.verbProps.warmupTime + " s)");
            if (!started) yield break;
            foreach (int wait in WaitFor(() => !verb.Bursting && !(wielder.stances.curStance is Stance_Warmup), 180)) yield return wait;
            t.Log(t.Now + " shot over: " + Game.Thorns.Count + " thorns up");
        }

        [RimArtTest("Ego", "paradise lost: a shot strikes the standing hostiles in the room, split by count, slowed, Sanity per hostile", 1800)]
        public static IEnumerable<int> RoomHit(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            t.Room(c + new IntVec3(-4, 0, -3), c + new IntVec3(4, 0, 3), door: c + new IntVec3(3, 0, -4));
            Pawn wielder = t.Colonist(c + new IntVec3(-3, 0, 0));
            CompEgoParadiseLost staff = Arm(t, wielder);
            // Stunned for the whole test: a struck raider's Wait job gives way and it runs, and the one in the doorway
            // walks out of the room before the second shot. None stands next to another pawn, so none punches first.
            const int stun = 1800;
            Pawn aimed = t.Target(c + new IntVec3(3, 0, 0), stun);
            Pawn second = t.Target(c + new IntVec3(2, 0, 2), stun, faction: aimed.Faction);
            Pawn third = t.Target(c + new IntVec3(1, 0, -3), stun, faction: aimed.Faction);
            Pawn downed = t.Target(c + new IntVec3(-1, 0, -3), stun, faction: aimed.Faction);
            Pawn ally = t.Colonist(c + new IntVec3(0, 0, 3));
            Pawn outside = t.Target(c + new IntVec3(7, 0, 0), stun, faction: aimed.Faction);
            Pawn doorway = t.Target(c + new IntVec3(3, 0, -4), stun, faction: aimed.Faction);
            t.Down(downed);
            yield return 2;
            Room room = aimed.GetRoom();
            t.Log("room: " + room?.CellCount + " cells, touches the map edge " + room?.TouchesMapEdge + "; the wielder's the same " + (wielder.GetRoom() == room)
                + ", the one outside the same " + (outside.GetRoom() == room) + ", the one in the doorway the same " + (doorway.GetRoom() == room));
            if (!t.Check(room != null && !EgoParadiseLost.Outdoors(room) && outside.GetRoom() != room, "the walls make a room of their own")) yield break;
            t.Log(States(aimed, second, third, doorway, downed, ally, outside));

            ThingDef round = staff.PrimaryVerb.verbProps.defaultProjectile;
            t.Log("damage by count: 1 " + EgoParadiseLost.Damage(staff, round, 1) + ", 3 " + EgoParadiseLost.Damage(staff, round, 3) + ", 6 " + EgoParadiseLost.Damage(staff, round, 6));
            t.Check(EgoParadiseLost.Damage(staff, round, 1) == 16f && EgoParadiseLost.Damage(staff, round, 3) == 12f && EgoParadiseLost.Damage(staff, round, 6) == 9f,
                "the def's damage: 16 to one, 12 each to 2-5, 9 each to 6 or more");

            foreach (int wait in Shoot(t, wielder, staff, aimed)) yield return wait;
            t.Log(t.Now + " " + States(aimed, second, third, doorway, downed, ally, outside) + " | Sanity +" + EgoParadiseLost.Sanity(wielder));
            t.Check(Game.Thorns.Count == 4 && Game.Thorns.All(h => h.thing == aimed || h.thing == second || h.thing == third || h.thing == doorway),
                "thorns rose round the aimed pawn, the two other standing hostiles and the one in the doorway (" + Game.Thorns.Count + ")");
            t.Check(t.Hurt(aimed) && t.Hurt(second) && t.Hurt(third), "all three in the room were struck");
            t.Check(t.Hurt(doorway), "and the one in the doorway: a door cell is a room of its own, and it counts as in the room it opens on");
            t.Check(!t.Struck(downed), "the downed hostile in the room was not");
            t.Check(t.Untouched(ally), "the colonist in the room was not");
            t.Check(t.Untouched(outside), "the hostile behind the wall, in the next room, was not");
            // A pawn the hit killed is not slowed (Slow skips the dead).
            t.Check(new[] { aimed, second, third, doorway }.All(p => p.Dead || EgoParadiseLost.Slowed(p)) && !EgoParadiseLost.Slowed(ally), "each pawn struck is slowed");
            t.Check(EgoParadiseLost.Sanity(wielder) == 4, "Sanity +1 for each of the 4 hostiles (+" + EgoParadiseLost.Sanity(wielder) + ")");
            t.Check(wielder.MentalStateDef == null, "a wielder over the requirement at full mood does not corrode");

            yield return 70;
            t.Log(t.Now + " " + States(aimed, second, third));
            t.Check(new[] { aimed, second }.All(p => p.Dead || !EgoParadiseLost.Slowed(p)), "the slow is gone after 1 s");

            // Sanity grows with each shot and stops at the cap; each hit renews its 2 h. The second shot strikes the pawn it
            // is aimed at, downed or not, and the others still standing. 12 to the head can kill (the brain has 10): then
            // the second shot is aimed at the next one alive.
            Pawn[] hostiles = { aimed, second, third, doorway };
            Pawn again = new[] { aimed, second, third }.FirstOrDefault(p => !p.Dead);
            if (!t.Check(again != null, "a pawn the first shot struck lived to be aimed at again")) yield break;
            if (again != aimed) t.Log(aimed.LabelShort + " died of the first shot; the second is aimed at " + again.LabelShort);
            int expected = Mathf.Min(10, 4 + 1 + hostiles.Count(p => p != again && !p.Dead && !p.Downed));
            t.Log(t.Now + " before the second shot: " + string.Join(" | ", hostiles.Select(p => Place(p, again))));
            int ordered = t.Now;
            foreach (int wait in Shoot(t, wielder, staff, again)) yield return wait;
            var memory = (Thought_Memory)wielder.needs.mood.thoughts.memories.GetFirstMemoryOfDef(EgoDefOf.AG_EgoParadiseLostSanity);
            t.Log(t.Now + " second shot: " + States(aimed, second, third, doorway) + " | Sanity +" + EgoParadiseLost.Sanity(wielder) + ", "
                + (memory == null ? "no memory" : memory.MoodOffset() + " mood, age " + memory.age + " of " + memory.DurationTicks + " ticks"));
            t.Log("the second shot's thorns: " + string.Join(", ", Game.Thorns.Where(h => h.tick >= ordered && !h.ring).Select(h => h.thing.LabelShort))
                + " | after it: " + string.Join(" | ", hostiles.Select(p => Place(p, again))));
            t.Check(EgoParadiseLost.Sanity(wielder) == expected, "the second shot added one for each hostile it struck (+" + EgoParadiseLost.Sanity(wielder) + ", expected +" + expected + ")");
            t.Check(memory != null && memory.age <= 150 && memory.DurationTicks == 5000 && Mathf.Approximately(memory.MoodOffset(), EgoParadiseLost.Sanity(wielder)),
                "one memory, renewed (its age moves in 150-tick steps, and the shot can land on the wielder's step), lasting the def's 2 h, giving the mood shown");
            EgoParadiseLost.GainSanity(wielder, staff.Props, 20);
            t.Check(EgoParadiseLost.Sanity(wielder) == 10, "never past the cap of 10 (+" + EgoParadiseLost.Sanity(wielder) + ")");
            t.Check(wielder.needs.mood.thoughts.memories.Memories.Count(m => m.def == EgoDefOf.AG_EgoParadiseLostSanity) == 1, "still one memory");
        }

        [RimArtTest("Ego", "paradise lost: outdoors a shot strikes hostiles within 6 cells of the aimed one, not farther, not inside walls", 1200)]
        public static IEnumerable<int> Outdoors(RimArtTestContext t) => OutdoorHit(t, walled: false);

        [RimArtTest("Ego", "paradise lost: a walled, unroofed yard of 300+ cells counts as outdoors, so the 6-cell limit holds there too", 1200)]
        public static IEnumerable<int> WalledYard(RimArtTestContext t) => OutdoorHit(t, walled: true);

        /// <summary>The outdoor hit on open ground or, with <paramref name="walled"/>, in a 21 x 21 unroofed walled yard that does not touch the map edge.</summary>
        private static IEnumerable<int> OutdoorHit(RimArtTestContext t, bool walled)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            if (walled) t.Room(c + new IntVec3(-10, 0, -10), c + new IntVec3(10, 0, 10));
            t.Room(c + new IntVec3(-3, 0, 3), c + new IntVec3(-3, 0, 3));
            Pawn wielder = t.Colonist(c + new IntVec3(-8, 0, 0));
            CompEgoParadiseLost staff = Arm(t, wielder);
            Pawn aimed = t.Target(c);
            Pawn near = t.Target(c + new IntVec3(4, 0, 2), faction: aimed.Faction);
            Pawn far = t.Target(c + new IntVec3(7, 0, 0), faction: aimed.Faction);
            Pawn boxed = t.Target(c + new IntVec3(-3, 0, 3), faction: aimed.Faction);
            Pawn downed = t.Target(c + new IntVec3(2, 0, -2), faction: aimed.Faction);
            t.Down(downed);
            yield return 2;
            Room yard = aimed.GetRoom();
            t.Log("aimed outdoors " + EgoParadiseLost.Outdoors(yard) + " (" + yard?.CellCount + " cells, " + yard?.OpenRoofCount + " unroofed, touches the map edge "
                + yard?.TouchesMapEdge + "); boxed in its own room " + (boxed.GetRoom() != yard)
                + ", " + boxed.Position.DistanceTo(aimed.Position).ToString("0.0") + " cells from the aimed one; near " + near.Position.DistanceTo(aimed.Position).ToString("0.0")
                + ", far " + far.Position.DistanceTo(aimed.Position).ToString("0.0"));
            if (!t.Check(EgoParadiseLost.Outdoors(yard) && yard.TouchesMapEdge == !walled && boxed.GetRoom() != yard,
                walled ? "the walled yard counts as outdoors without touching the map edge, and the boxed one is not" : "the aimed pawn is outdoors and the boxed one is not")) yield break;

            foreach (int wait in Shoot(t, wielder, staff, aimed)) yield return wait;
            t.Log(t.Now + " " + States(aimed, near, far, boxed, downed) + " | Sanity +" + EgoParadiseLost.Sanity(wielder));
            t.Check(Game.Thorns.Count == 2, "two struck (" + Game.Thorns.Count + ")");
            t.Check(t.Hurt(aimed) && t.Hurt(near), "the aimed pawn and the one 4.5 cells from it");
            t.Check(t.Untouched(far), "not the one 7 cells off");
            t.Check(t.Untouched(boxed), "not the one 4.2 cells off behind walls");
            t.Check(!t.Struck(downed), "not the downed one");
            t.Check(EgoParadiseLost.Sanity(wielder) == 2, "Sanity +2");
        }

        [RimArtTest("Ego", "paradise lost: corroded, the ring grows 6, 9, 12 and strikes every faction through walls, downed too", 2400)]
        public static IEnumerable<int> Corroded(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoParadiseLost staff = Arm(t, wielder);
            Pawn ally = t.Colonist(c + new IntVec3(4, 0, 0));
            for (int x = -1; x <= 1; x++) t.Wall(c + new IntVec3(x, 0, -3));
            Pawn walled = t.Target(c + new IntVec3(0, 0, -5));
            Pawn mid = t.Target(c + new IntVec3(0, 0, 8), faction: walled.Faction);
            Pawn downed = t.Target(c + new IntVec3(-11, 0, 0), faction: walled.Faction);
            Pawn corner = t.Target(c + new IntVec3(10, 0, 10), faction: walled.Faction);
            t.Down(downed);
            yield return 2;
            t.Log("distances: ally 4, walled 5 (a wall between), mid 8, downed 11, corner " + corner.Position.DistanceTo(c).ToString("0.0"));

            t.Check(EgoCorrosion.Corrode(wielder, staff), "Corrode started the state");
            int start = t.Now, seen = 0;
            var radii = new List<float>();
            var hurtAt = new Dictionary<Pawn, int>();
            Pawn[] all = { ally, walled, mid, downed, corner };
            foreach (int wait in WaitFor(() => radii.Count >= 3 || !(wielder.MentalState is MentalState_EgoCorroded), 1500))
            {
                if (staff.rings != seen)
                {
                    seen = staff.rings;
                    radii.Add(Game.Rings.Count > 0 ? Game.Rings[Game.Rings.Count - 1].radius : -1f);
                    foreach (Pawn p in all)
                        if (!hurtAt.ContainsKey(p) && (p == downed ? t.Struck(p) : t.Hurt(p))) hurtAt[p] = seen;
                    t.Log("+" + (t.Now - start) + " ring " + seen + ", radius " + radii[radii.Count - 1] + ": " + States(all) + " | wielder at " + wielder.Position
                        + ", Sanity +" + EgoParadiseLost.Sanity(wielder));
                }
                yield return wait;
            }
            int Ring(Pawn p) => hurtAt.TryGetValue(p, out int k) ? k : 0;
            t.Check(radii.SequenceEqual(new[] { 6f, 9f, 12f }), "the rings grew 6, 9, 12 (" + string.Join(", ", radii) + ")");
            t.Check(Ring(ally) == 1, "the colonist 4 cells off was struck by ring 1 (" + Ring(ally) + ")");
            t.Check(Ring(walled) == 1, "the enemy 5 cells off behind a wall by ring 1 (" + Ring(walled) + ")");
            t.Check(Ring(mid) == 2, "the enemy 8 cells off by ring 2 (" + Ring(mid) + ")");
            t.Check(Ring(downed) == 3, "the downed enemy 11 cells off by ring 3 (" + Ring(downed) + ")");
            t.Check(Ring(corner) == 0 && t.Untouched(corner), "the enemy 14 cells off by none");
            t.Check(EgoParadiseLost.Sanity(wielder) == 0, "rings give no Sanity");
            t.Check(wielder.Position == c, "the wielder held its cell");
            t.Check(Game.LookOf(wielder) != null, "the look is up while corroded");

            wielder.MentalState?.RecoverFromState();
            yield return 2;
            t.Log(t.Now + " recovered: rings counted " + staff.rings + ", look " + (Game.LookOf(wielder) == null ? "ended" : "running") + ", "
                + Game.Looks.Count + " look(s) fading");
            t.Check(staff.rings == 0, "the ring count starts again at the next corrosion");
            t.Check(Game.LookOf(wielder) == null && Game.Looks.Any(l => l.wielder == wielder), "the look ended and is folding");
            foreach (int wait in WaitFor(() => !Game.Looks.Any(l => l.wielder == wielder), 120)) yield return wait;
            t.Check(!Game.Looks.Any(l => l.wielder == wielder), "and is gone after the 1 s exit");
        }

        [RimArtTest("Ego", "paradise lost: Overclock fires rings of 6 cells at standing hostiles only", 1800)]
        public static IEnumerable<int> Overclock(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoParadiseLost staff = Arm(t, wielder);
            Pawn a = t.Target(c + new IntVec3(3, 0, 0));
            Pawn b = t.Target(c + new IntVec3(-2, 0, 2), faction: a.Faction);
            Pawn ally = t.Colonist(c + new IntVec3(0, 0, -3));
            Pawn downed = t.Target(c + new IntVec3(2, 0, -2), faction: a.Faction);
            Pawn far = t.Target(c + new IntVec3(0, 0, 9), faction: a.Faction);
            NoWimp(a);
            NoWimp(b);
            t.Down(downed);
            yield return 2;

            var command = new Command_EgoOverclock(staff, wielder);
            if (!t.Check(!command.Disabled, "Overclock is on with standing hostiles within 6 cells")) yield break;
            command.action();
            int start = t.Now, seen = 0;
            var radii = new List<float>();
            bool looked = false;
            foreach (int wait in WaitFor(() => wielder.CurJobDef != EgoDefOf.AG_EgoOverclock, 600))
            {
                if (Game.LookOf(wielder) != null) looked = true;
                int fired = (wielder.jobs.curDriver as JobDriver_EgoOverclock)?.Fired ?? seen;
                if (fired != seen)
                {
                    seen = fired;
                    radii.Add(Game.Rings.Count > 0 ? Game.Rings[Game.Rings.Count - 1].radius : -1f);
                    t.Log("+" + (t.Now - start) + " ring " + seen + ", radius " + radii[radii.Count - 1] + ": " + States(a, b, ally, downed, far));
                }
                yield return wait;
            }
            bool stood = (!a.Dead && !a.Downed) || (!b.Dead && !b.Downed);
            t.Log("overclock over after " + (t.Now - start) + " ticks, " + radii.Count + " rings: " + States(a, b, ally, downed, far));
            t.Check(radii.Count >= 1 && radii.All(r => r == 6f), "every ring is 6 cells; Overclock rings do not grow (" + string.Join(", ", radii) + ")");
            t.Check(!stood || radii.Count == 3, "3 rings while a hostile stood in range (" + radii.Count + ")");
            t.Check(t.Hurt(a) && t.Hurt(b), "both standing hostiles were struck");
            t.Check(t.Untouched(ally), "the colonist 3 cells off was not");
            t.Check(!t.Struck(downed), "the downed hostile was not");
            t.Check(t.Untouched(far), "the hostile 9 cells off was not");
            t.Check(staff.rings == 0, "Overclock does not count toward the corroded ring's growth");
            t.Check(looked, "the look was up during it");
            t.Check(wielder.Position == c, "Overclock held still");
            t.Check(wielder.needs.mood.thoughts.memories.GetFirstMemoryOfDef(EgoDefOf.AG_EgoOverclocked) != null, "the cost was paid");
            t.Check(EgoParadiseLost.Sanity(wielder) == 0, "rings give no Sanity");
        }

        [RimArtTest("Ego", "paradise lost: the def's corrosion numbers, Pale ignores armour, and Core never draws the staff", 600)]
        public static IEnumerable<int> DefAndStaff(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoParadiseLost staff = Arm(t, wielder);
            CompProperties_EgoParadiseLost p = staff.Props;
            SkillRecord shooting = wielder.skills.GetSkill(SkillDefOf.Shooting);
            float over = EgoCorrosion.Chance(wielder, p);
            shooting.Level = 5;
            float under = EgoCorrosion.Chance(wielder, p);
            shooting.Level = 12;
            t.Log("full mood: chance " + over + " at Shooting 12, " + under + " at Shooting 5; overclock range " + p.overclockRange + ", ring " + p.ringRadius);
            t.Check(over == 0f && under == 0.5f, "full mood: no roll over Shooting 10, 50 % under it (one band worse)");
            t.Check(p.overclockRange == p.ringRadius, "Overclock's button looks for hostiles within the ring's radius");

            // Pale against power armour: the full amount, where a bullet loses at least half.
            Pawn armoured = t.Target(c + new IntVec3(5, 0, 0), bare: false);
            ThingDef powerArmor = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_PowerArmor");
            if (powerArmor != null)
            {
                armoured.apparel.Wear((Apparel)ThingMaker.MakeThing(powerArmor));
                BodyPartRecord torso = armoured.RaceProps.body.corePart;
                DamageDef pale = EgoDefOf.AG_EgoPale, bullet = DamageDefOf.Bullet;
                float paleDamage = ArmorUtility.GetPostArmorDamage(armoured, 12f, 0f, torso, ref pale, out _, out _);
                float bulletDamage = ArmorUtility.GetPostArmorDamage(armoured, 12f, 0f, torso, ref bullet, out _, out _);
                t.Log("12 to the torso under power armour: Pale " + paleDamage + ", Bullet " + bulletDamage);
                t.Check(paleDamage == 12f && bulletDamage < 12f, "armour takes nothing off Pale");
            }
            else t.Log("no Apparel_PowerArmor: the armour check is skipped");

            t.Check(Game.Holds(wielder), "the holder registered on equip");
            t.Check(!HeldWeaponHide.Shown(staff.parent), "Core does not draw the held staff");
            Game.Clear();
            foreach (int wait in WaitFor(() => Game.Holds(wielder), HeldWeaponHolders.RescanEvery + 2)) yield return wait;
            t.Check(Game.Holds(wielder), "a holder the component forgot (as after a load) is found again by the rescan");
            ThingWithComps held = wielder.equipment.Primary;
            wielder.equipment.TryDropEquipment(held, out ThingWithComps dropped, wielder.Position);
            yield return 1;
            t.Log(t.Now + " dropped: " + (dropped?.LabelShort ?? "nothing") + " at " + dropped?.Position);
            t.Check(!Game.Holds(wielder), "dropped, the pawn no longer holds it");
        }

        /// <summary>
        /// The corroded look (star, wings, halo) and the staff on three wielders Core draws at a low, a middle and a high
        /// height: the wings, the halo and the staff are placed from each pawn's own DrawPos.y, so the ones over a pawn show
        /// over it and the ones behind stay behind on every one of them. Close shots of each while the wings open (30 ticks,
        /// rebuilt every frame) and once open (150, baked), before the first ring (about 360 ticks in) strikes the others.
        /// </summary>
        [RimArtTest("Ego", "paradise lost: height, the corroded look and the staff on pawns drawn low, middle and high (screenshots)", 900)]
        public static IEnumerable<int> Height(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn[] wielders = HeightShots.Spread(t, c + new IntVec3(-5, 0, 0), c, c + new IntVec3(5, 0, 0));
            if (!t.Check(wielders.All(w => w != null), "a colonist for each height")) yield break;
            foreach (Pawn wielder in wielders)
                t.Check(EgoCorrosion.Corrode(wielder, Arm(t, wielder)), wielder.LabelShort + " corroded");
            int start = t.Now;
            foreach (int at in new[] { 30, 150 })
            {
                if (start + at > t.Now) yield return start + at - t.Now;
                foreach (Pawn wielder in wielders) yield return HeightShots.Shoot(t, "paradise lost height " + wielder.LabelShort + " " + at, wielder.Position, wielder);
            }
            t.Check(Game.Rings.Count == 0 && wielders.All(w => t.Untouched(w)), "no ring yet: nobody was struck");
            t.Check(wielders.All(w => Game.LookOf(w) != null), "each wielder's look is up");
        }
    }
}
