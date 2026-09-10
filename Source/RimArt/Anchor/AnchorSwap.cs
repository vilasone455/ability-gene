using Verse;

namespace RimArt
{
    /// <summary>
    /// Exchanging two positions. There is no projectile, no travel and no intervening state -
    /// the two ends are read, then written to each other, which is why the ability is a swap
    /// rather than a move: something always comes back the other way.
    ///
    /// Pawn.Position on a spawned pawn re-registers them with the map's grids and regions;
    /// Notify_Teleported drops the job and resets the pather so nobody keeps walking a route
    /// that started on the other side of the map.
    /// </summary>
    public static class AnchorSwap
    {
        /// <summary>Swaps two pawns. Both must be spawned on the same map.</summary>
        public static void Swap(Pawn a, Pawn b)
        {
            if (a == null || b == null || !a.Spawned || !b.Spawned || a.Map != b.Map) return;

            Map map = a.Map;
            IntVec3 aCell = a.Position;
            IntVec3 bCell = b.Position;
            if (aCell == bCell) return;

            AnchorFX.Entry(aCell, map);
            AnchorFX.Entry(bCell, map);

            a.Position = bCell;
            a.Notify_Teleported(true, true);
            b.Position = aCell;
            b.Notify_Teleported(true, true);

            AnchorFX.Exit(aCell, map);
            AnchorFX.Exit(bCell, map);
        }

        /// <summary>
        /// Moves one pawn onto a cell. Nothing comes back, because a marked tile has nothing
        /// standing on it to send.
        /// </summary>
        public static void MoveTo(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || !pawn.Spawned) return;

            Map map = pawn.Map;
            if (!cell.IsValid || !cell.InBounds(map) || !cell.Standable(map)) return;

            IntVec3 from = pawn.Position;
            if (from == cell) return;

            AnchorFX.Entry(from, map);

            pawn.Position = cell;
            pawn.Notify_Teleported(true, true);

            AnchorFX.Exit(cell, map);
        }

        /// <summary>
        /// One end of a clap against one anchor. A pawn mark is a swap; a tile mark is a move.
        /// </summary>
        public static void Resolve(Pawn caster, Anchor anchor)
        {
            if (anchor.IsOnPawn) Swap(caster, anchor.pawn);
            else MoveTo(caster, anchor.cell);
        }

        /// <summary>Both anchors against each other. Tile ends move whoever is on the other end.</summary>
        public static void Resolve(Anchor a, Anchor b)
        {
            if (a.IsOnPawn && b.IsOnPawn) Swap(a.pawn, b.pawn);
            else if (a.IsOnPawn) MoveTo(a.pawn, b.cell);
            else if (b.IsOnPawn) MoveTo(b.pawn, a.cell);
            // Two tile marks have nothing to exchange; the caller refuses this before it gets here.
        }
    }
}
