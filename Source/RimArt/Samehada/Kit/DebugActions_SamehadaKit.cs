using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real weapon. The drawing previews are in DebugActions_Samehada.</summary>
    public static class DebugActions_SamehadaKit
    {
        /// <summary>Puts Samehada with no charge in the clicked pawn's hands, dropping what it held.</summary>
        [RimArtDebug("Samehada", "give samehada to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                Messages.Message("this pawn cannot hold a weapon", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pawn.equipment.Primary != null)
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position, false);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(SamehadaDefOf.AG_Samehada));
        }

        /// <summary>Drops Samehada on the clicked cell.</summary>
        [RimArtDebug("Samehada", "spawn samehada on ground")]
        private static void Spawn() =>
            GenSpawn.Spawn(ThingMaker.MakeThing(SamehadaDefOf.AG_Samehada), UI.MouseCell(), Find.CurrentMap);

        /// <summary>Fills the held blade to its most charges.</summary>
        [RimArtDebug("Samehada", "fill charges", RimArtDebugKind.Pawn)]
        private static void Fill(Pawn pawn)
        {
            CompSamehada blade = CompSamehada.HeldBy(pawn);
            blade?.SetCharges(blade.Props.maxCharges);
        }

        /// <summary>Adds one charge to the held blade.</summary>
        [RimArtDebug("Samehada", "charge +1", RimArtDebugKind.Pawn)]
        private static void AddOne(Pawn pawn)
        {
            CompSamehada blade = CompSamehada.HeldBy(pawn);
            blade?.SetCharges(blade.Charges + 1);
        }

        /// <summary>Empties the held blade.</summary>
        [RimArtDebug("Samehada", "empty charges", RimArtDebugKind.Pawn)]
        private static void Empty(Pawn pawn) => CompSamehada.HeldBy(pawn)?.SetCharges(0);
    }
}
