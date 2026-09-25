using Verse;

namespace RimArt
{
    /// <summary>
    /// Exchanging two positions. There is no projectile, no travel and no intervening state -
    /// the two ends are read, then written to each other, which is why the ability is a swap
    /// rather than a move: something always comes back the other way. With a stone, the stone is
    /// what comes back, so it lies where the pawn stood and a second clap returns them.
    ///
    /// Only the ends move. The tiles and whatever is on them - fire, filth, items - stay.
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
        /// A pawn and a stone change places. The stone's def draws in real time, so setting its
        /// Position is enough to move the picture as well as the grids.
        /// </summary>
        public static bool Swap(Pawn pawn, Thing stone, Pawn carrier = null)
        {
            if (pawn == null || stone == null || !pawn.Spawned || !stone.Spawned || pawn.Map != stone.Map) return false;

            Map map = pawn.Map;
            IntVec3 pawnCell = pawn.Position;
            IntVec3 stoneCell = stone.Position;
            if (pawnCell == stoneCell || !stoneCell.Standable(map)) return false;

            pawn.Position = stoneCell;
            pawn.Notify_Teleported(pawn != carrier, true);
            stone.Position = pawnCell;
            return true;
        }

        /// <summary>The carrier against one end: a pawn or a stone.</summary>
        public static bool Resolve(Pawn caster, Anchor end)
        {
            return end.IsOnPawn ? Swap(caster, end.pawn, caster) : Swap(caster, end.stone, caster);
        }

        /// <summary>Two ends against each other, the carrier not one of them. Two stones have nothing to exchange.</summary>
        public static bool Resolve(Anchor a, Anchor b)
        {
            if (a.IsOnPawn && b.IsOnPawn) return Swap(a.pawn, b.pawn);
            if (a.IsOnPawn) return Swap(a.pawn, b.stone);
            if (b.IsOnPawn) return Swap(b.pawn, a.stone);
            return false;
        }
    }
}
