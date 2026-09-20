using Verse;

namespace RimArt
{
    /// <summary>
    /// Exchanging two positions. There is no projectile, no travel and no intervening state -
    /// the two ends are read, then written to each other, which is why the ability is a swap
    /// rather than a move: something always comes back the other way.
    ///
    /// Pawn.Position on a spawned pawn re-registers them with the map's grids and regions;
    /// Notify_Teleported resets the pather so nobody keeps walking a route that started on the
    /// other side of the map, and drops the job - except the carrier's. The carrier is in the
    /// middle of the clap's cast job, which still has the clip's last half second to hold, and
    /// Melee Animation cancels a clip whose pawn has left the job it was started in.
    ///
    /// Nothing here draws. The comps hand the two ends to MapComponent_ClapTeleports.
    /// </summary>
    public static class AnchorSwap
    {
        /// <summary>Swaps two pawns. Both must be spawned on the same map.</summary>
        public static bool Swap(Pawn a, Pawn b, Pawn carrier = null)
        {
            if (a == null || b == null || !a.Spawned || !b.Spawned || a.Map != b.Map) return false;

            IntVec3 aCell = a.Position;
            IntVec3 bCell = b.Position;
            if (aCell == bCell) return false;

            a.Position = bCell;
            a.Notify_Teleported(a != carrier, true);
            b.Position = aCell;
            b.Notify_Teleported(b != carrier, true);
            return true;
        }

        /// <summary>
        /// Moves one pawn onto a cell. Nothing comes back, because a marked tile has nothing
        /// standing on it to send.
        /// </summary>
        public static bool MoveTo(Pawn pawn, IntVec3 cell, Pawn carrier = null)
        {
            if (pawn == null || !pawn.Spawned) return false;

            Map map = pawn.Map;
            if (!cell.IsValid || !cell.InBounds(map) || !cell.Standable(map)) return false;
            if (pawn.Position == cell) return false;

            pawn.Position = cell;
            pawn.Notify_Teleported(pawn != carrier, true);
            return true;
        }

        /// <summary>
        /// One end of a clap against one anchor. A pawn mark is a swap; a tile mark is a move.
        /// </summary>
        public static bool Resolve(Pawn caster, Anchor anchor)
        {
            return anchor.IsOnPawn ? Swap(caster, anchor.pawn, caster) : MoveTo(caster, anchor.cell, caster);
        }

        /// <summary>Both anchors against each other. Tile ends move whoever is on the other end.</summary>
        public static bool Resolve(Anchor a, Anchor b)
        {
            if (a.IsOnPawn && b.IsOnPawn) return Swap(a.pawn, b.pawn);
            if (a.IsOnPawn) return MoveTo(a.pawn, b.cell);
            if (b.IsOnPawn) return MoveTo(b.pawn, a.cell);
            // Two tile marks have nothing to exchange; the caller refuses this before it gets here.
            return false;
        }
    }
}
