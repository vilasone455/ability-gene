using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real weapon. The drawing previews are in DebugActions_WaterGun.</summary>
    public static class DebugActions_WaterGunKit
    {
        /// <summary>Puts a full water gun in the clicked pawn's hands, dropping what it held.</summary>
        [RimArtDebug("Water Gun", "give water gun to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                Messages.Message("this pawn cannot hold a weapon", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pawn.equipment.Primary != null)
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position, false);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(WaterGunDefOf.AG_WaterGun));
        }

        /// <summary>Drops a water gun on the clicked cell.</summary>
        [RimArtDebug("Water Gun", "spawn water gun on ground")]
        private static void Spawn() =>
            GenSpawn.Spawn(ThingMaker.MakeThing(WaterGunDefOf.AG_WaterGun), UI.MouseCell(), Find.CurrentMap);

        [RimArtDebug("Water Gun", "fill bag", RimArtDebugKind.Pawn)]
        private static void Fill(Pawn pawn) => CompWaterGun.HeldBy(pawn)?.Fill();

        [RimArtDebug("Water Gun", "empty bag", RimArtDebugKind.Pawn)]
        private static void Empty(Pawn pawn)
        {
            CompWaterGun gun = CompWaterGun.HeldBy(pawn);
            gun?.Spend(gun.Units);
        }

        [RimArtDebug("Water Gun", "soak pawn 60 s", RimArtDebugKind.Pawn)]
        private static void Soak(Pawn pawn) => WaterGunSoak.Apply(pawn, 60f);
    }
}
