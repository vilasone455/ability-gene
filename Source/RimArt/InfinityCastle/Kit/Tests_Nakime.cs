using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests for Nakime through her Echo (run with -quicktest -rimarttest=Nakime): the awakening, the
    /// take, the cap, the lords after the return, the commands through the castle, the ways the castle
    /// ends, the play job and the sunlight. The Host's cell is roofed so the sunlight rule does not wound
    /// her during the castle tests.
    /// </summary>
    public static class Tests_Nakime
    {
        private static EchoDef Nakime => DebugActions_Nakime.Echo;
        private static AbilityDef CastleAbility => InfinityCastleDefOf.AG_Nakime_InfinityCastle;

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            GameComponent_InfinityCastle.Instance.ResetForTests();
            return t.ClearEchoes();
        }

        /// <summary>A drafted colonist 8 cells west of the centre made Nakime's Host and manifested, a full pool, a roof over her.</summary>
        private static Pawn Host(RimArtTestContext t, out EchoRecord record)
        {
            Pawn host = t.Host(Nakime, t.center + new IntVec3(-8, 0, 0), out record);
            foreach (IntVec3 c in GenRadial.RadialCellsAround(host.Position, 2.5f, true)) t.map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            host.drafter.Drafted = true;
            return host;
        }

        private static IntVec3 Target(RimArtTestContext t) => t.center + new IntVec3(4, 0, 0);

        private static InfinityCastleCast CastOf(Pawn pawn) => GameComponent_InfinityCastle.Instance?.For(pawn);

        private static void LogPawns(RimArtTestContext t, InfinityCastleCast cast, params Pawn[] pawns)
        {
            foreach (Pawn p in pawns)
                t.Log(RimArtTestContext.Describe(p) + " map=" + (p.MapHeld == null ? "none" : p.MapHeld == t.map ? "home" : cast != null && p.MapHeld == cast.castle ? "castle" : "other " + p.MapHeld.uniqueID));
        }

        /// <summary>Casts at the target cell and waits until everyone is in the castle.</summary>
        private static IEnumerable<int> CastAndWait(RimArtTestContext t, Pawn host, Action<InfinityCastleCast> got)
        {
            host.abilities.GetAbility(CastleAbility).QueueCastingJob(new LocalTargetInfo(Target(t)), LocalTargetInfo.Invalid);
            InfinityCastleCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null && cast.Standing || cast != null && cast.fizzled, 900, 5)) yield return w;
            got(cast);
        }

        private static IEnumerable<int> ReleaseAndWait(InfinityCastleCast cast)
        {
            cast.releaseOrdered = true;
            foreach (int w in WaitFor(() => cast.returned, 9000, 5)) yield return w;
        }

        private static IEnumerable<int> StrumReady(MapComponent_InfinityCastle castle)
        {
            foreach (int w in WaitFor(() => castle.StrumWait <= 0f, 9000, 5)) yield return w;
        }

        [RimArtTest("Nakime", "echo 1 awakening gives the castle organ; manifest gives Infinity Castle and the biwa; revert takes both, the gene stays")]
        private static IEnumerable<int> Echo(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn pawn = t.Colonist(t.center);
            EchoRecord record = EchoUtility.ForceHost(Nakime, pawn);
            if (!t.Check(record != null, "made Nakime's Host")) yield break;
            if (ModsConfig.BiotechActive)
                t.Check(pawn.genes.Xenogenes.Any(g => g.def == InfinityCastleDefOf.AG_CastleOrgan), "the castle organ is a xenogene");
            t.Check(pawn.story.traits.HasTrait(TraitDef.Named("NightOwl")), "Night Owl was given");
            string costs = string.Join(" / ", EchoUtility.CostLines(Nakime, pawn));
            t.Log("cost lines: " + costs);
            t.Check(costs.Contains("sunlight"), "the gene's cost line says sunlight burns");
            t.Check(pawn.abilities.GetAbility(CastleAbility) == null, "no ability before manifesting");

            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "manifested");
            yield return 2;
            t.Check(pawn.abilities.GetAbility(CastleAbility) != null, "manifested Nakime has Infinity Castle");
            t.Check(pawn.equipment.Primary?.def == DefDatabase<ThingDef>.GetNamed("AG_Biwa"), "the biwa is in her hands (" + pawn.equipment.Primary?.def.defName + ")");

            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(pawn.abilities.GetAbility(CastleAbility) == null, "reverted, the ability is gone");
            t.Check(pawn.equipment.Primary?.def.defName != "AG_Biwa", "reverted, the biwa is gone");
            if (ModsConfig.BiotechActive) t.Check(pawn.genes.HasActiveGene(InfinityCastleDefOf.AG_CastleOrgan), "the castle organ stays");
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "take 1 pays 30 and takes the hostiles within 5.9 cells into rooms of their own; Release brings everyone back", 12000)]
        private static IEnumerable<int> TakeReturn(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            IntVec3 target = Target(t);
            Pawn a = t.Enemy(target + new IntVec3(1, 0, 0), armed: false);
            Pawn b = t.Enemy(target + new IntVec3(0, 0, 3), armed: false);
            Pawn c = t.Enemy(target + new IntVec3(-3, 0, -4), armed: false);
            Pawn far = t.Enemy(target + new IntVec3(7, 0, 0), armed: false);
            Pawn downed = t.Enemy(target + new IntVec3(2, 0, -3), armed: false);
            HealthUtility.DamageUntilDowned(downed, false);
            Pawn ally = t.Colonist(target + new IntVec3(-4, 0, 3));
            ally.drafter.Drafted = false;
            RimArtTestContext.Hold(ally);
            IntVec3 hostFrom = host.Position, aFrom = a.Position, bFrom = b.Position;
            yield return 2;

            float before = echoes.charge;
            host.abilities.GetAbility(CastleAbility).QueueCastingJob(new LocalTargetInfo(target), LocalTargetInfo.Invalid);
            InfinityCastleCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null, 300, 1)) yield return w;
            if (!t.Check(cast != null && cast.Taking, "the strum began a cast (" + RimArtTestContext.Describe(host) + ")")) yield break;
            float spent = before - echoes.charge;
            t.Check(spent >= 30f && spent < 31f, "the pool paid 30 (" + spent.ToString("0.##") + " with upkeep)");
            t.Check(Math.Abs(cast.paid - 30f) < 0.01f, "the cast kept 30");
            t.Check(cast.taken.Count(x => x.kind == CastleGuest.Enemy) == 3, "three enemies chosen (" + cast.taken.Count(x => x.kind == CastleGuest.Enemy) + ")");
            yield return 30;
            yield return t.ShotAs("nakime-take", target, 12f);

            foreach (int w in WaitFor(() => cast.Standing || cast.fizzled, 600, 5)) yield return w;
            LogPawns(t, cast, host, a, b, c, far, downed, ally);
            if (!t.Check(cast.Standing, "the castle stands")) yield break;
            MapComponent_InfinityCastle castle = cast.Component;
            t.Check(a.Map == cast.castle && b.Map == cast.castle && c.Map == cast.castle, "the three enemies are in the castle");
            t.Check(far.Map == t.map, "the enemy 7 cells away stayed");
            t.Check(downed.Map == t.map, "the downed enemy stayed");
            t.Check(ally.Map == t.map, "the colonist in reach stayed");
            t.Check(host.Map == cast.castle && host.Position == castle.DaisCell, "Nakime is on the dais");
            t.Check(host.CurJobDef == InfinityCastleDefOf.AG_CastlePlay, "Nakime plays (" + host.CurJobDef?.defName + ")");
            List<int> rooms = new[] { a, b, c }.Select(p => castle.RoomAt(p.Position)?.Id ?? -1).ToList();
            t.Log("rooms: " + string.Join(", ", rooms));
            t.Check(rooms.All(r => r > 0) && rooms.Distinct().Count() == 3, "each enemy is in a room of its own, not the biwa room");
            t.Check(a.GetLord()?.LordJob is LordJob_AssaultColony, "the enemies fight in the castle");
            t.Check(host.abilities.GetAbility(CastleAbility).GizmoDisabled(out string why), "the ability is disabled while the castle stands (" + why + ")");
            t.Check(host.abilities.GetAbility(CastleAbility).GetGizmos().Count() >= 7, "the six commands follow the ability's button");
            yield return 90;
            yield return t.ShotAs("nakime-inside", castle.DaisCell + new IntVec3(10, 0, -4), 26f);

            Map castleMap = cast.castle;
            foreach (int w in ReleaseAndWait(cast)) yield return w;
            LogPawns(t, cast, host, a, b, c);
            if (!t.Check(cast.returned, "everyone returned")) yield break;
            t.Check(host.Map == t.map && host.Position.DistanceTo(hostFrom) <= 2f, "Nakime is back where she stood (" + hostFrom + ")");
            t.Check(a.Dead || a.MapHeld == t.map && a.Position.DistanceTo(aFrom) <= 2f, "enemy a is back where it stood (" + aFrom + ")");
            t.Check(b.Dead || b.MapHeld == t.map && b.Position.DistanceTo(bFrom) <= 2f, "enemy b is back where it stood (" + bFrom + ")");
            t.Check(host.Drafted, "Nakime is still drafted");
            yield return 20;
            yield return t.ShotAs("nakime-return", target, 12f);
            foreach (int w in WaitFor(() => !Find.Maps.Contains(castleMap), 600, 5)) yield return w;
            t.Check(!Find.Maps.Contains(castleMap), "the castle was removed");
            t.Check(host.abilities.GetAbility(CastleAbility).CooldownTicksRemaining > 0, "the cooldown is spent");
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "take 2 ten hostiles in reach: the nearest eight are taken", 6000)]
        private static IEnumerable<int> Cap(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            IntVec3 target = Target(t);
            var offsets = new[] { (0, 1), (1, 0), (0, -1), (-1, 0), (2, 2), (-2, 2), (2, -2), (-2, -2), (5, 0), (0, -5) };
            List<Pawn> enemies = offsets.Select(o => t.Enemy(target + new IntVec3(o.Item1, 0, o.Item2), armed: false)).ToList();
            yield return 2;
            InfinityCastleCast cast = null;
            foreach (int w in CastAndWait(t, host, x => cast = x)) yield return w;
            if (!t.Check(cast != null && cast.Standing, "the castle stands")) yield break;
            int inside = enemies.Count(p => p.Map == cast.castle);
            t.Check(inside == 8, "eight taken (" + inside + ")");
            t.Check(enemies[8].Map == t.map && enemies[9].Map == t.map, "the two farthest stayed");
            foreach (int w in ReleaseAndWait(cast)) yield return w;
            t.Check(cast.returned && enemies.All(p => p.Dead || p.MapHeld == t.map), "all ten are on the home map");
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "lord 1 a raid partly taken rejoins its lord; a raid wholly taken leaves the map", 12000)]
        private static IEnumerable<int> Lords(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            IntVec3 target = Target(t);
            Pawn a1 = t.Enemy(target + new IntVec3(1, 0, 0), armed: false);
            Pawn a2 = t.Enemy(t.center + new IntVec3(-10, 0, 10), armed: false);
            Pawn b1 = t.Enemy(target + new IntVec3(-2, 0, -1), armed: false);
            Pawn b2 = t.Enemy(target + new IntVec3(-1, 0, -3), armed: false);
            a2.SetFaction(a1.Faction);
            b1.SetFaction(a1.Faction);
            b2.SetFaction(a1.Faction);
            Lord lordA = LordMaker.MakeNewLord(a1.Faction, new LordJob_DefendPoint(a2.Position, 2f), t.map, new[] { a1, a2 });
            Lord lordB = LordMaker.MakeNewLord(a1.Faction, new LordJob_DefendPoint(target, 2f), t.map, new[] { b1, b2 });
            foreach (Pawn p in new[] { a1, a2, b1, b2 }) RimArtTestContext.Hold(p);
            yield return 2;
            InfinityCastleCast cast = null;
            foreach (int w in CastAndWait(t, host, x => cast = x)) yield return w;
            LogPawns(t, cast, a1, a2, b1, b2);
            if (!t.Check(cast != null && cast.Standing, "the castle stands")) yield break;
            if (a1.Map != cast.castle) t.Log("a1 walked out of reach before the strum: the partly-taken check is skipped");
            t.Check(a2.Map == t.map && t.map.lordManager.lords.Contains(lordA), "raid A's lord goes on with a2");
            t.Check(b1.Map == cast.castle && b2.Map == cast.castle, "raid B was taken whole");
            t.Check(!t.map.lordManager.lords.Contains(lordB), "raid B's lord ended");
            foreach (int w in ReleaseAndWait(cast)) yield return w;
            LogPawns(t, cast, a1, a2, b1, b2);
            if (a1.MapHeld == t.map && !a1.Dead) t.Check(a1.GetLord() == lordA, "a1 rejoined raid A's lord (" + a1.GetLord()?.LordJob?.GetType().Name + ")");
            t.Check(b1.Dead || b1.GetLord()?.LordJob is LordJob_ExitMapBest, "b1 leaves the map (" + b1.GetLord()?.LordJob?.GetType().Name + ")");
            t.Check(b2.Dead || b2.GetLord()?.LordJob is LordJob_ExitMapBest, "b2 leaves the map (" + b2.GetLord()?.LordJob?.GetType().Name + ")");
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "command 1 Summon, Drop, Seal and Open, Shift and Crush through the castle; the summoned colonist goes home", 20000)]
        private static IEnumerable<int> Commands(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            IntVec3 target = Target(t);
            Pawn a = t.Enemy(target + new IntVec3(1, 0, 0), armed: false);
            Pawn b = t.Enemy(target + new IntVec3(0, 0, 3), armed: false);
            Pawn colonist = t.Colonist(t.center + new IntVec3(-8, 0, -6));
            IntVec3 colonistFrom = colonist.Position;
            yield return 2;
            InfinityCastleCast cast = null;
            foreach (int w in CastAndWait(t, host, x => cast = x)) yield return w;
            if (!t.Check(cast != null && cast.Standing, "the castle stands")) yield break;
            MapComponent_InfinityCastle castle = cast.Component;
            CastleLayout layout = castle.Castle;
            CastleRoom Middle(Pawn p) => castle.RoomAt(p.Position);
            IntVec3 MiddleOf(CastleRoom r) => new IntVec3(r.X + r.W / 2, 0, r.Z + r.H / 2);
            List<CastleRoom> empty = layout.Rooms.Where(r => r.Kind != CastleKind.Biwa && r != Middle(a) && r != Middle(b)).ToList();
            string why;

            // Summon.
            foreach (int w in StrumReady(castle)) yield return w;
            t.Check(cast.SummonChoices().Contains(colonist), "the colonist is a Summon choice");
            CastleRoom summonRoom = empty[0];
            t.Check(cast.Summon(colonist, MiddleOf(summonRoom), out why), "Summon played (" + why + ")");
            t.Check(colonist.Map == cast.castle && castle.RoomAt(colonist.Position) == summonRoom, "the colonist is in the chosen room");
            t.Check(!cast.Summon(host, MiddleOf(empty[1]), out why), "Nakime cannot summon herself (" + why + ")");

            // Drop.
            foreach (int w in StrumReady(castle)) yield return w;
            CastleRoom dropRoom = empty[1];
            t.Check(castle.TryDrop(a, MiddleOf(dropRoom), out why), "Drop played (" + why + ")");
            t.Check(castle.RoomAt(a.Position) == dropRoom, "enemy a is in the chosen room");

            // Seal, then Open.
            foreach (int w in StrumReady(castle)) yield return w;
            CastleDoorway door = layout.Doorways.FirstOrDefault(d => d.A != layout.Biwa.Id && d.B != layout.Biwa.Id
                && d.Cells.All(x => !new IntVec3(x.x, 0, x.z).GetThingList(cast.castle).Any(th => th is Pawn)));
            if (t.Check(door != null, "found a doorway away from the biwa room"))
            {
                IntVec3 cell = new IntVec3(door.Cells[0].x, 0, door.Cells[0].z);
                t.Check(castle.TrySeal(cell, out why), "Seal played (" + why + ")");
                t.Check(castle.SealedAt(cell, out _) && !cell.Walkable(cast.castle), "the doorway is shut");
                foreach (int w in StrumReady(castle)) yield return w;
                t.Check(castle.TryOpen(cell, out why), "Open played (" + why + ")");
                t.Check(!castle.SealedAt(cell, out _) && cell.Walkable(cast.castle), "the doorway is open again");
            }

            // Shift.
            foreach (int w in StrumReady(castle)) yield return w;
            CastleRoom mover = null;
            int dx = 0, dz = 0, distance = 0;
            foreach (CastleRoom r in castle.Castle.Rooms.Where(r => r.Kind != CastleKind.Biwa))
            {
                foreach (var (x, z) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int d = castle.ShiftDistance(MiddleOf(r), x, z);
                    if (d > 0) { mover = r; dx = x; dz = z; distance = d; break; }
                }
                if (mover != null) break;
            }
            if (t.Check(mover != null, "found a room that can move"))
            {
                int id = mover.Id, x0 = mover.X, z0 = mover.Z;
                t.Check(castle.TryShift(MiddleOf(mover), dx, dz, out why), "Shift played (" + why + ")");
                CastleRoom moved = castle.Castle.Rooms[id];
                t.Check(moved.X == x0 + dx * distance && moved.Z == z0 + dz * distance, "the room moved " + distance + " cells");
            }

            // Crush.
            // Retried until the slide has stopped and the strum has faded.
            CastleRoom big = castle.Castle.Rooms.FirstOrDefault(r => r.Kind != CastleKind.Biwa && r.W >= 7 && r.H >= 7);
            if (t.Check(big != null, "found a room of 7 x 7 or more"))
            {
                bool crushed = false;
                foreach (int w in WaitFor(() => crushed = castle.TryCrush(MiddleOf(big), out why), 900, 10)) yield return w;
                t.Check(crushed, "Crush played (" + why + ")");
                t.Check(castle.CrushWait > 0f, "Crush's own cooldown runs");
            }

            foreach (int w in ReleaseAndWait(cast)) yield return w;
            LogPawns(t, cast, host, a, b, colonist);
            t.Check(colonist.Map == t.map && colonist.Position.DistanceTo(colonistFrom) <= 2f, "the summoned colonist is back where it was (" + colonistFrom + ")");
            t.Check(a.Dead || a.MapHeld == t.map, "enemy a is home");
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "end 1 Nakime downed in the castle ends it; everyone comes home", 12000)]
        private static IEnumerable<int> Downed(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            Pawn a = t.Enemy(Target(t) + new IntVec3(1, 0, 0), armed: false);
            yield return 2;
            InfinityCastleCast cast = null;
            foreach (int w in CastAndWait(t, host, x => cast = x)) yield return w;
            if (!t.Check(cast != null && cast.Standing, "the castle stands")) yield break;
            yield return 60;
            HealthUtility.DamageUntilDowned(host, false);
            foreach (int w in WaitFor(() => cast.returned, 9000, 5)) yield return w;
            LogPawns(t, cast, host, a);
            t.Check(cast.returned, "the castle ended");
            t.Check(cast.SecondsLeft(t.Now) > 20f, "it ended early (" + cast.SecondsLeft(t.Now).ToString("0.#") + " s were left)");
            t.Check(host.MapHeld == t.map && (a.Dead || a.MapHeld == t.map), "both are home");
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "end 2 reverting in the castle ends it", 12000)]
        private static IEnumerable<int> Revert(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            Pawn a = t.Enemy(Target(t) + new IntVec3(1, 0, 0), armed: false);
            yield return 2;
            InfinityCastleCast cast = null;
            foreach (int w in CastAndWait(t, host, x => cast = x)) yield return w;
            if (!t.Check(cast != null && cast.Standing, "the castle stands")) yield break;
            Map castleMap = cast.castle;
            yield return 30;
            EchoUtility.Revert(record, collapse: false);
            foreach (int w in WaitFor(() => cast.returned, 9000, 5)) yield return w;
            LogPawns(t, cast, host, a);
            t.Check(cast.returned && host.Map == t.map, "the castle ended and Nakime is home");
            foreach (int w in WaitFor(() => !Find.Maps.Contains(castleMap), 600, 5)) yield return w;
            t.Check(!Find.Maps.Contains(castleMap), "the castle was removed");
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "end 3 Nakime downed before she goes in: nobody moves, the castle is removed", 6000)]
        private static IEnumerable<int> Fizzle(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            Pawn a = t.Enemy(Target(t) + new IntVec3(1, 0, 0), armed: false);
            IntVec3 aFrom = a.Position;
            yield return 2;
            host.abilities.GetAbility(CastleAbility).QueueCastingJob(new LocalTargetInfo(Target(t)), LocalTargetInfo.Invalid);
            InfinityCastleCast cast = null;
            foreach (int w in WaitFor(() => (cast = CastOf(host)) != null, 300, 1)) yield return w;
            if (!t.Check(cast != null && cast.Taking, "the take began")) yield break;
            Map castleMap = cast.castle;
            HealthUtility.DamageUntilDowned(host, false);
            foreach (int w in WaitFor(() => cast.fizzled, 300, 1)) yield return w;
            t.Check(cast.fizzled, "the cast ended");
            t.Check(a.Map == t.map && a.Position == aFrom, "the enemy stayed where it was");
            t.Check(host.MapHeld == t.map, "Nakime stayed");
            foreach (int w in WaitFor(() => !Find.Maps.Contains(castleMap), 600, 5)) yield return w;
            t.Check(!Find.Maps.Contains(castleMap), "the castle was removed");
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "play 1 a move order does not take Nakime off the dais", 6000)]
        private static IEnumerable<int> Play(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            Pawn a = t.Enemy(Target(t) + new IntVec3(1, 0, 0), armed: false);
            yield return 2;
            InfinityCastleCast cast = null;
            foreach (int w in CastAndWait(t, host, x => cast = x)) yield return w;
            if (!t.Check(cast != null && cast.Standing, "the castle stands")) yield break;
            IntVec3 dais = cast.Component.DaisCell;
            yield return 60;
            host.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, dais + new IntVec3(0, 0, -3)), JobTag.DraftedOrder);
            yield return 90;
            t.Log(RimArtTestContext.Describe(host));
            t.Check(host.Position == dais, "she is still on the dais");
            t.Check(host.CurJobDef == InfinityCastleDefOf.AG_CastlePlay, "she still plays");
            foreach (int w in ReleaseAndWait(cast)) yield return w;
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        [RimArtTest("Nakime", "sun 1 outdoors while the sky is lit the castle organ burns her; under a roof it does not", 3000)]
        private static IEnumerable<int> Sunlight(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            if (!ModsConfig.BiotechActive) { t.Log("Biotech is off: no genes"); yield break; }
            Pawn pawn = t.Colonist(t.center);
            pawn.drafter.Drafted = false;
            RimArtTestContext.Hold(pawn);
            EchoUtility.ForceHost(Nakime, pawn);
            yield return 2;
            int Burns() => pawn.health.hediffSet.hediffs.Count(h => h is Hediff_Injury && h.def == DamageDefOf.Burn.hediff);
            bool lit = pawn.Position.InSunlight(t.map);
            t.Log("sky glow " + t.map.skyManager.CurSkyGlow.ToString("0.00") + ", in sunlight: " + lit);
            int before = Burns();
            yield return 130;
            if (lit) t.Check(Burns() > before, "outdoors by day she burns (" + before + " -> " + Burns() + ")");
            else t.Check(Burns() == before, "outdoors at night she does not burn");

            foreach (IntVec3 c in GenRadial.RadialCellsAround(pawn.Position, 1.5f, true)) t.map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            yield return 2;
            before = Burns();
            yield return 130;
            t.Check(Burns() == before, "under a roof she does not burn (" + before + " -> " + Burns() + ")");
            EchoDevice.workingForTests = null;
        }
    }
}
