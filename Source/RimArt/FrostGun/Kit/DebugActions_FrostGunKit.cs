using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real weapon. The drawing previews are in DebugActions_FrostGun.</summary>
    public static class DebugActions_FrostGunKit
    {
        /// <summary>Puts a full frost rifle in the clicked pawn's hands, dropping what it held.</summary>
        [RimArtDebug("Frost Gun", "give frost gun to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                Messages.Message("this pawn cannot hold a weapon", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pawn.equipment.Primary != null)
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position, false);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(FrostGunDefOf.AG_FrostGun));
        }

        [RimArtDebug("Frost Gun", "fill coolant", RimArtDebugKind.Pawn)]
        private static void Fill(Pawn pawn) => CompFrostGun.HeldBy(pawn)?.SetCoolant(float.MaxValue);

        [RimArtDebug("Frost Gun", "empty coolant", RimArtDebugKind.Pawn)]
        private static void Empty(Pawn pawn) => CompFrostGun.HeldBy(pawn)?.SetCoolant(0f);

        [RimArtDebug("Frost Gun", "chill pawn 3 stacks", RimArtDebugKind.Pawn)]
        private static void Chill(Pawn pawn)
        {
            for (int i = 0; i < 3; i++) FlashFreeze.AddChill(pawn, 3, 10f);
        }
    }
}
