using Verse;

namespace RimArt
{
    /// <summary>
    /// One mark. It is either fixed to a pawn, in which case it travels with them, or fixed to
    /// a cell, in which case it does not.
    ///
    /// A pawn mark stores a reference rather than a position on purpose: the whole point of
    /// marking a raider is that you mark them where you can reach them and clap when they are
    /// somewhere you cannot.
    /// </summary>
    public class Anchor : IExposable
    {
        public Pawn pawn;
        public IntVec3 cell;
        public int placedTick;

        public Anchor() { }

        public Anchor(Pawn pawn, int placedTick)
        {
            this.pawn = pawn;
            this.cell = pawn.Position;
            this.placedTick = placedTick;
        }

        public Anchor(IntVec3 cell, int placedTick)
        {
            this.cell = cell;
            this.placedTick = placedTick;
        }

        public bool IsOnPawn => pawn != null;

        /// <summary>Where the mark is right now. A pawn mark moves; a tile mark does not.</summary>
        public IntVec3 CurrentCell => pawn != null ? pawn.Position : cell;

        public string Label => pawn != null ? pawn.LabelShort : "AG_AnchorTileLabel".Translate().ToString();

        /// <summary>
        /// A mark survives only while the thing it is on is still reachable in the ordinary
        /// sense - same map, still spawned, still alive. Everything else is a mark that would
        /// teleport someone into a hole in the world.
        /// </summary>
        public bool StillHolds(Map map, int durationTicks)
        {
            if (map == null) return false;
            if (durationTicks > 0 && Find.TickManager.TicksGame - placedTick > durationTicks) return false;

            if (pawn != null)
            {
                return !pawn.Dead && pawn.Spawned && pawn.Map == map;
            }

            return cell.IsValid && cell.InBounds(map) && cell.Standable(map);
        }

        /// <summary>
        /// Whether a target picked in the UI is this mark.
        ///
        /// Position has to count, not just identity. The second pick of a double clap goes
        /// through CompAbilityEffect_WithDest, whose targeting parameters are locations only -
        /// it hands back the cell that was clicked and never the pawn standing in it. Matching
        /// on the thing alone means a marked pawn can never be chosen as the far end.
        /// </summary>
        public bool Marks(LocalTargetInfo target)
        {
            if (target.Pawn != null && target.Pawn == pawn) return true;
            return target.Cell.IsValid && target.Cell == CurrentCell;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref placedTick, "placedTick", 0);
        }
    }
}
