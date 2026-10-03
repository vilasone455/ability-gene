using Verse;
using Verse.AI.Group;
using static RimArt.CrossMapMove;

namespace RimArt
{
    /// <summary>
    /// One pawn taken to a pocket map: the home cell it came from and the lord it left. Unlimited Blade Works uses
    /// it as it is; Unlimited Void and Infinity Castle extend it with their own state. <see cref="PocketReturn"/>
    /// brings it home.
    /// </summary>
    public class PocketGuest : IExposable
    {
        public Pawn pawn;
        public IntVec3 from;
        public Lord lord;

        /// <summary>
        /// Takes the pawn out of its lord and onto <paramref name="pocket"/> at the free cell nearest
        /// <paramref name="want"/>, remembering where it stood and the lord. Returns the cell it landed on.
        /// </summary>
        public IntVec3 TakeTo(Map pocket, IntVec3 want)
        {
            from = pawn.Position;
            lord = pawn.GetLord();
            lord?.RemovePawn(pawn);
            IntVec3 cell = FreeCellNear(pocket, want);
            Move(pawn, cell, pocket);
            return cell;
        }

        public virtual void ExposeData()
        {
            // A lord that has ended is saved nowhere, and a reference to it would not resolve on load.
            if (Scribe.mode == LoadSaveMode.Saving && lord != null && (lord.Map == null || !lord.Map.lordManager.lords.Contains(lord))) lord = null;
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_Values.Look(ref from, "from");
            Scribe_References.Look(ref lord, "lord");
        }
    }
}
