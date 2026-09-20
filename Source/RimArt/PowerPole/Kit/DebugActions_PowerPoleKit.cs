using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real weapon. The drawing previews are in DebugActions_PowerPole.</summary>
    public static class DebugActions_PowerPoleKit
    {
        /// <summary>Puts a Power Pole in the clicked pawn's hands, dropping what it held, with all three abilities ready.</summary>
        [RimArtDebug("Power Pole", "give pole to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                Messages.Message("this pawn cannot hold a weapon", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pawn.equipment.Primary != null)
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position, false);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(PowerPoleDefOf.AG_PowerPole));
        }

        /// <summary>Drops a Power Pole on the clicked cell.</summary>
        [RimArtDebug("Power Pole", "spawn pole on ground")]
        private static void Spawn() =>
            GenSpawn.Spawn(ThingMaker.MakeThing(PowerPoleDefOf.AG_PowerPole), UI.MouseCell(), Find.CurrentMap);
    }
}
