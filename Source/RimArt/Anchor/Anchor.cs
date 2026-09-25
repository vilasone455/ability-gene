using Verse;

namespace RimArt
{
    /// <summary>
    /// One end of a clap. Two kinds:
    ///
    /// A stone: the only kind the gene stores. Mark throws it onto a tile; it is a real item lying
    /// there, and it can be clapped with at any distance and without line of sight. It never sticks
    /// to a pawn.
    ///
    /// A pawn: never stored. A living pawn in sight and in range is reachable without a stone, so
    /// the clap builds one of these for the cast and drops it afterwards (<see cref="ClapTargets"/>).
    ///
    /// <see cref="cell"/> is only read for a save from before the stones, which the gene drops on
    /// load.
    /// </summary>
    public class Anchor : IExposable
    {
        public Pawn pawn;
        public Thing stone;
        public IntVec3 cell;
        public int placedTick;

        /// <summary>
        /// Which card the stone is drawn with: ClapTeleport.Spade, Heart or Club. Kept on the mark, not
        /// read off its place in the list, so a stone does not change suit when an older one is gone.
        /// -1 is a mark from a save older than the cards; the gene deals it a suit on load.
        /// </summary>
        public int suit = -1;

        public Anchor() { }

        /// <summary>A pawn end, built for one cast.</summary>
        public Anchor(Pawn pawn, int placedTick)
        {
            this.pawn = pawn;
            this.cell = pawn.Position;
            this.placedTick = placedTick;
        }

        public Anchor(Thing stone, int placedTick)
        {
            this.stone = stone;
            this.cell = stone.Position;
            this.placedTick = placedTick;
        }

        public bool IsOnPawn => pawn != null;

        public bool IsStone => stone != null;

        /// <summary>Where the end is right now. A pawn walks; a stone can be picked up and dropped.</summary>
        public IntVec3 CurrentCell => pawn != null ? pawn.Position : stone != null ? stone.PositionHeld : cell;

        public string Label => pawn != null ? pawn.LabelShort : "AG_AnchorTileLabel".Translate().ToString();

        /// <summary>
        /// A stone survives while it is not destroyed, is on this map (lying there or carried),
        /// and has not faded. A stone that is carried still holds but cannot be clapped with until
        /// it is on the ground again (<see cref="Usable"/>).
        /// </summary>
        public bool StillHolds(Map map, int durationTicks)
        {
            if (map == null) return false;

            if (pawn != null)
            {
                return !pawn.Dead && pawn.Spawned && pawn.Map == map;
            }

            if (stone == null || stone.Destroyed || stone.MapHeld != map) return false;
            return durationTicks <= 0 || Find.TickManager.TicksGame - placedTick <= durationTicks;
        }

        /// <summary>A stone lying on the ground on a tile someone can stand on.</summary>
        public bool Usable => stone != null && stone.Spawned && stone.Position.Standable(stone.Map);

        /// <summary>
        /// Whether a target picked in the UI is this stone. Position, because the second pick of a
        /// double clap goes through CompAbilityEffect_WithDest, whose targeting parameters are
        /// locations only - it hands back the cell that was clicked and never the thing in it.
        /// </summary>
        public bool Marks(LocalTargetInfo target)
        {
            if (stone == null) return false;
            if (target.Thing != null && target.Thing == stone) return true;
            return stone.Spawned && target.Cell.IsValid && target.Cell == stone.Position;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref stone, "stone");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref placedTick, "placedTick", 0);
            Scribe_Values.Look(ref suit, "suit", -1);
        }
    }
}
