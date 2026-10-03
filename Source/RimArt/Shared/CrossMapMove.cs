using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimArt
{
    /// <summary>
    /// Moving pawns between a home map and a pocket map, shared by Unlimited Blade Works, Infinity Castle,
    /// Unlimited Void and the Kamui dimension: the move itself, the free cell a pawn lands on, and the lord hostile
    /// pawns fight on under. <see cref="PocketGuest"/> and <see cref="PocketReturn"/> build the take and the return on it.
    /// </summary>
    public static class CrossMapMove
    {
        /// <summary>Takes a pawn off its map and puts it on another, keeping it drafted if it was.</summary>
        public static void Move(Pawn p, IntVec3 cell, Map to)
        {
            bool drafted = p.Drafted;
            Rot4 facing = p.Rotation;
            p.DeSpawnOrDeselect();
            GenSpawn.Spawn(p, cell, to, facing);
            p.Notify_Teleported(true, true);
            if (drafted && p.drafter != null && !p.Downed) p.drafter.Drafted = true;
        }

        /// <summary><paramref name="want"/> moved at least one cell in from the map's edge.</summary>
        public static IntVec3 ClampInside(Map map, IntVec3 want) =>
            new IntVec3(Mathf.Clamp(want.x, 1, map.Size.x - 2), 0, Mathf.Clamp(want.z, 1, map.Size.z - 2));

        /// <summary>
        /// The nearest cell to <paramref name="want"/> within 8 cells a pawn can stand on with no other pawn on it;
        /// else a standable cell within 20; else any standable cell on the map; else <paramref name="want"/> itself.
        /// Always a cell on the map, so a pawn already taken off its old map is never left on none.
        /// </summary>
        public static IntVec3 FreeCellNear(Map map, IntVec3 want)
        {
            want = ClampInside(map, want);
            int cells = GenRadial.NumCellsInRadius(8f);
            for (int i = 0; i < cells; i++)
            {
                IntVec3 c = want + GenRadial.RadialPattern[i];
                if (c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null) return c;
            }
            IntVec3 near = CellFinder.StandableCellNear(want, map, 20f);
            if (near.IsValid) return near;
            return CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map), map, 1000, out IntVec3 any) ? any : want;
        }

        /// <summary>
        /// Hostile pawns fight on: an assault lord for each faction hostile to the player, with no fleeing and no
        /// kidnapping. A pawn hostile only through a mental state or a rebellion (a berserk colonist, a rebelling
        /// slave, a visitor gone mad) gets none: its mental state drives it, and the player's own faction never
        /// gets an assault lord.
        /// </summary>
        public static void Assault(IEnumerable<Pawn> pawns, Map map)
        {
            foreach (IGrouping<Faction, Pawn> group in pawns.GroupBy(p => p.Faction))
            {
                Faction faction = group.Key;
                if (faction == null || faction.IsPlayer || !faction.HostileTo(Faction.OfPlayer)) continue;
                LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, false, false, false, false, false), map, group);
            }
        }
    }
}
