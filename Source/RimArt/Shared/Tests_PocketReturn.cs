using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimArt
{
    /// <summary>
    /// The lords <see cref="PocketReturn"/> gives pawns brought home. The arena map stands in for both maps (a take
    /// and a return within one map), so no pocket map is made; the rules are the same.
    /// </summary>
    public static class Tests_PocketReturn
    {
        /// <summary>A lord whose toil takes no new pawns, as a toil past its last join point does.</summary>
        private sealed class LordToil_Closed : LordToil_DefendPoint
        {
            public LordToil_Closed(IntVec3 at) : base(at) { }
            public override bool CanAddPawn(Pawn p) => false;
        }

        private sealed class LordJob_Closed : LordJob
        {
            private readonly IntVec3 at;
            public LordJob_Closed(IntVec3 at) { this.at = at; }
            public override StateGraph CreateGraph() => new StateGraph { StartingToil = new LordToil_Closed(at) };
        }

        private static Lord LordOf(Pawn a, Pawn b, LordJob job) => LordMaker.MakeNewLord(a.Faction, job, a.Map, new List<Pawn> { a, b });

        private static PocketGuest Take(Pawn p, IntVec3 to)
        {
            var guest = new PocketGuest { pawn = p };
            guest.TakeTo(p.Map, to);
            return guest;
        }

        private static string LordName(Pawn p) => p.GetLord()?.LordJob?.GetType().Name ?? "no lord";

        [RimArtTest("Pocket", "return 1 lords: rejoin, refused, ended, visitor, prisoner")]
        private static IEnumerable<int> Lords(RimArtTestContext t)
        {
            t.Clear();
            yield return 5;
            IntVec3 c = t.center;

            // Rejoins: its lord still stands and takes pawns.
            Pawn a = t.Enemy(c + new IntVec3(-6, 0, 4), armed: false), a2 = t.Enemy(c + new IntVec3(-5, 0, 4), false, a.Faction);
            Lord assault = LordOf(a, a2, new LordJob_AssaultColony(a.Faction, false, false, false, false, false));
            // Refused: its lord still stands but its toil takes no new pawns (the UBW return logged an error here).
            Pawn b = t.Enemy(c + new IntVec3(-6, 0, 0), armed: false), b2 = t.Enemy(c + new IntVec3(-5, 0, 0), false, b.Faction);
            Lord closed = LordOf(b, b2, new LordJob_Closed(b.Position));
            // Ended: its lord was removed while it was away.
            Pawn e = t.Enemy(c + new IntVec3(-6, 0, -4), armed: false), e2 = t.Enemy(c + new IntVec3(-5, 0, -4), false, e.Faction);
            Lord ended = LordOf(e, e2, new LordJob_AssaultColony(e.Faction, false, false, false, false, false));
            yield return 2;

            PocketGuest ga = Take(a, c + new IntVec3(4, 0, 4)), gb = Take(b, c + new IntVec3(4, 0, 0)), ge = Take(e, c + new IntVec3(4, 0, -4));
            t.Check(a.GetLord() == null && b.GetLord() == null && e.GetLord() == null, "the taken pawns left their lords");
            t.Check(ga.lord == assault && gb.lord == closed && ge.lord == ended, "each guest remembers the lord it left");
            ended.lordManager.RemoveLord(ended);
            yield return 2;

            var back = new PocketReturn(t.map, t.map, hostilesFight: true);
            foreach (PocketGuest g in new[] { ga, gb, ge })
            {
                back.Bring(g.pawn, g.from);
                back.Rejoin(g.pawn, g.lord);
            }
            back.Finish(new GlobalTargetInfo(c, t.map), LocomotionUrgency.Walk);
            yield return 2;
            t.Log("rejoin: " + LordName(a) + ", refused: " + LordName(b) + ", ended: " + LordName(e));
            t.Check(a.GetLord() == assault, "rejoin: back in the assault lord it left");
            t.Check(b.GetLord()?.LordJob is LordJob_AssaultColony && b.GetLord() != closed, "refused: a new assault lord, no error");
            t.Check(e.GetLord()?.LordJob is LordJob_AssaultColony, "ended: a new assault lord");
            t.Check(a.Position.DistanceTo(ga.from) <= 2f, "brought back where it stood (" + ga.from + ")");

            Faction friendly = Find.FactionManager.RandomNonHostileFaction(allowNonHumanlike: false);
            if (friendly == null)
            {
                t.Log("no non-hostile faction in this game: visitor and prisoner cases skipped");
                yield break;
            }
            // Visitor: a friendly pawn whose lord ended leaves the map. Prisoner: no lord before, none after.
            Pawn v = PawnGenerator.GeneratePawn(new PawnGenerationRequest(friendly.def.basicMemberKind, friendly));
            Pawn v2 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(friendly.def.basicMemberKind, friendly));
            Pawn prisoner = PawnGenerator.GeneratePawn(new PawnGenerationRequest(friendly.def.basicMemberKind, friendly));
            GenSpawn.Spawn(v, c + new IntVec3(0, 0, 6), t.map);
            GenSpawn.Spawn(v2, c + new IntVec3(1, 0, 6), t.map);
            GenSpawn.Spawn(prisoner, c + new IntVec3(0, 0, -6), t.map);
            prisoner.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
            Lord visit = LordOf(v, v2, new LordJob_DefendPoint(v.Position));
            yield return 2;

            PocketGuest gv = Take(v, c + new IntVec3(3, 0, 6)), gp = Take(prisoner, c + new IntVec3(3, 0, -6));
            visit.lordManager.RemoveLord(visit);
            back = new PocketReturn(t.map, t.map, hostilesFight: true);
            foreach (PocketGuest g in new[] { gv, gp })
            {
                back.Bring(g.pawn, g.from);
                back.Rejoin(g.pawn, g.lord);
            }
            back.Finish(new GlobalTargetInfo(c, t.map), LocomotionUrgency.Walk);
            yield return 2;
            t.Log("visitor: " + LordName(v) + ", prisoner: " + LordName(prisoner));
            t.Check(v.GetLord()?.LordJob is LordJob_ExitMapBest, "visitor whose lord ended: leaves the map");
            t.Check(prisoner.GetLord() == null && prisoner.IsPrisonerOfColony, "prisoner: no lord, still a prisoner");
        }

        [RimArtTest("Pocket", "return 2 hostiles leave when they do not fight (Infinity Castle)")]
        private static IEnumerable<int> HostilesLeave(RimArtTestContext t)
        {
            t.Clear();
            yield return 5;
            IntVec3 c = t.center;
            Pawn e = t.Enemy(c + new IntVec3(-4, 0, 0), armed: false), e2 = t.Enemy(c + new IntVec3(-3, 0, 0), false, e.Faction);
            Lord ended = LordOf(e, e2, new LordJob_AssaultColony(e.Faction, false, false, false, false, false));
            yield return 2;

            PocketGuest g = Take(e, c + new IntVec3(4, 0, 0));
            ended.lordManager.RemoveLord(ended);
            var back = new PocketReturn(t.map, t.map, hostilesFight: false);
            back.Bring(e, g.from);
            back.Rejoin(e, g.lord);
            back.Finish(new GlobalTargetInfo(c, t.map), LocomotionUrgency.Jog);
            yield return 2;
            t.Log("hostile: " + LordName(e));
            t.Check(e.GetLord()?.LordJob is LordJob_ExitMapBest, "a hostile whose lord ended leaves the map");
        }
    }
}
