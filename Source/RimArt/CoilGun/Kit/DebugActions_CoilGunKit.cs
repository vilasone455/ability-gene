using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real weapon. The drawing previews are in DebugActions_CoilGun.</summary>
    public static class DebugActions_CoilGunKit
    {
        /// <summary>Puts a fully charged coil gun in the clicked pawn's hands, dropping what it held.</summary>
        [RimArtDebug("Coil Gun", "give coil gun to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                Messages.Message("this pawn cannot hold a weapon", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pawn.equipment.Primary != null)
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position, false);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(CoilGunDefOf.AG_CoilGun));
        }

        /// <summary>Drops a coil gun on the clicked cell.</summary>
        [RimArtDebug("Coil Gun", "spawn coil gun on ground")]
        private static void Spawn() =>
            GenSpawn.Spawn(ThingMaker.MakeThing(CoilGunDefOf.AG_CoilGun), UI.MouseCell(), Find.CurrentMap);

        [RimArtDebug("Coil Gun", "fill battery", RimArtDebugKind.Pawn)]
        private static void Fill(Pawn pawn)
        {
            CompCoilGun gun = CompCoilGun.HeldBy(pawn);
            gun?.SetCharge(gun.Props.capacity);
        }

        [RimArtDebug("Coil Gun", "empty battery", RimArtDebugKind.Pawn)]
        private static void Empty(Pawn pawn) => CompCoilGun.HeldBy(pawn)?.SetCharge(0f);

        [RimArtDebug("Coil Gun", "soak pawn 60 s", RimArtDebugKind.Pawn)]
        private static void Soak(Pawn pawn) => WaterGunSoak.Apply(pawn, 60f);
    }
}
