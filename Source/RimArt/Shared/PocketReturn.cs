using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RimArt.CrossMapMove;

namespace RimArt
{
    /// <summary>
    /// Bringing everyone home from a pocket map, shared by Unlimited Blade Works, Unlimited Void and Infinity Castle.
    /// Made at the return: <see cref="Bring"/> each guest home and <see cref="Rejoin"/> it, then bring anyone else
    /// still there (<see cref="Others"/>) and give it <see cref="NoLord"/>, <see cref="Place"/> the
    /// <see cref="Items"/>, and call <see cref="Finish"/>.
    ///
    /// Lords: a guest goes back into the lord it left if that lord still stands on the home map and takes it. Else a
    /// pawn hostile to the player gets an assault lord (hostilesFight) or leaves the map; a pawn of another faction
    /// that lost its lord leaves the map; anyone else (colonists, prisoners, animals) gets no lord.
    /// </summary>
    public sealed class PocketReturn
    {
        public readonly Map pocket, to;
        public readonly FollowView view;
        /// <summary>Everyone brought home, in order.</summary>
        public readonly List<Pawn> moved = new List<Pawn>();
        private readonly bool hostilesFight;
        private readonly List<Pawn> fight = new List<Pawn>(), leave = new List<Pawn>();

        public PocketReturn(Map pocket, Map to, bool hostilesFight)
        {
            this.pocket = pocket;
            this.to = to;
            this.hostilesFight = hostilesFight;
            view = new FollowView(pocket);
        }

        /// <summary>The map everyone goes back to: home while it exists, else any player home map; null when the colony has none.</summary>
        public static Map HomeOr(Map home) => home != null && Find.Maps.Contains(home) ? home : Find.AnyPlayerHomeMap;

        /// <summary>Alive and still on the pocket map. A pawn carried by another is inside its carrier and comes home with it.</summary>
        public bool Here(Pawn p) => p != null && !p.Destroyed && !p.Dead && p.Spawned && p.Map == pocket;

        /// <summary>Everyone alive still on the pocket map: call after the guests are home to get those the cast did not take.</summary>
        public List<Pawn> Others() => pocket.mapPawns.AllPawnsSpawned.Where(p => !p.Dead).ToList();

        /// <summary>Takes the pawn out of any lord it has on the pocket map and home to the free cell nearest <paramref name="want"/>. Returns the cell.</summary>
        public IntVec3 Bring(Pawn p, IntVec3 want)
        {
            p.GetLord()?.RemovePawn(p);
            IntVec3 cell = FreeCellNear(to, want);
            Move(p, cell, to);
            moved.Add(p);
            return cell;
        }

        /// <summary>
        /// A guest brought home goes back into the lord it left. Not when that lord has ended or its toil refuses new
        /// pawns: <c>Lord.AddPawn</c> would log an error and leave the pawn with no lord at all.
        /// </summary>
        public void Rejoin(Pawn p, Lord left)
        {
            if (left != null && to.lordManager.lords.Contains(left) && left.CanAddPawn(p)) left.AddPawn(p);
            else NoLord(p, left != null);
        }

        /// <summary>A pawn brought home with no lord to go back to. <paramref name="hadLord"/>: it had one when it was taken.</summary>
        public void NoLord(Pawn p, bool hadLord = false)
        {
            bool otherFaction = p.Faction != null && !p.Faction.IsPlayer;
            if (p.HostileTo(Faction.OfPlayer))
            {
                if (hostilesFight) fight.Add(p);
                else if (otherFaction) leave.Add(p);
            }
            else if (hadLord && otherFaction) leave.Add(p);
        }

        /// <summary>The items lying on the pocket map: corpses, dropped weapons, everything else.</summary>
        public List<Thing> Items() =>
            pocket.listerThings.AllThings.Where(t => !t.Destroyed && t.Spawned && t.def.category == ThingCategory.Item).ToList();

        /// <summary>Moves an item home near <paramref name="want"/>, one cell in from the edge at least. False if there was no room.</summary>
        public bool Place(Thing thing, IntVec3 want)
        {
            thing.DeSpawn();
            return GenPlace.TryPlaceThing(thing, ClampInside(to, want), to, ThingPlaceMode.Near);
        }

        /// <summary>
        /// The new lords (an assault lord per hostile faction, a lord that leaves the map at <paramref name="leaveSpeed"/>
        /// per other faction), then the camera to <paramref name="look"/> if the player was watching the pocket map.
        /// </summary>
        public void Finish(GlobalTargetInfo look, LocomotionUrgency leaveSpeed)
        {
            Assault(fight, to);
            foreach (IGrouping<Faction, Pawn> group in leave.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_ExitMapBest(leaveSpeed), to, group);
            view.Follow(look, moved);
        }
    }
}
