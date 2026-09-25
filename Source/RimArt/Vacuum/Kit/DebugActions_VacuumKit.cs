using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real weapon. The drawing previews are in DebugActions_Vacuum.</summary>
    public static class DebugActions_VacuumKit
    {
        /// <summary>Puts an empty vacuum in the clicked pawn's hands, dropping what it held.</summary>
        [RimArtDebug("Vacuum", "give vacuum to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                Messages.Message("this pawn cannot hold a weapon", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pawn.equipment.Primary != null)
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position, false);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(VacuumDefOf.AG_Vacuum));
        }

        /// <summary>Drops an empty vacuum on the clicked cell.</summary>
        [RimArtDebug("Vacuum", "spawn vacuum on ground")]
        private static void Spawn() =>
            GenSpawn.Spawn(ThingMaker.MakeThing(VacuumDefOf.AG_Vacuum), UI.MouseCell(), Find.CurrentMap);

        /// <summary>Adds 25 kg to the held vacuum's stomach.</summary>
        [RimArtDebug("Vacuum", "stomach +25 kg", RimArtDebugKind.Pawn)]
        private static void Feed(Pawn pawn)
        {
            CompVacuum vacuum = CompVacuum.HeldBy(pawn);
            if (vacuum == null) return;
            vacuum.SetStomach(vacuum.StomachKg + 25f);
            vacuum.Sync();
        }

        /// <summary>Empties the held vacuum at once: the mouth's thing and the stomach are destroyed.</summary>
        [RimArtDebug("Vacuum", "empty vacuum", RimArtDebugKind.Pawn)]
        private static void Empty(Pawn pawn) => CompVacuum.HeldBy(pawn)?.Empty();

        /// <summary>Scatters test things round the clicked cell: a steel stack, a granite chunk, a rifle and blood.</summary>
        [RimArtDebug("Vacuum", "scatter things to suck")]
        private static void Scatter()
        {
            Map map = Find.CurrentMap;
            IntVec3 at = UI.MouseCell();
            Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
            steel.stackCount = 75;
            GenPlace.TryPlaceThing(steel, at + IntVec3.East, map, ThingPlaceMode.Near);
            ThingDef chunk = DefDatabase<ThingDef>.GetNamedSilentFail("ChunkGranite");
            if (chunk != null) GenPlace.TryPlaceThing(ThingMaker.MakeThing(chunk), at + IntVec3.North, map, ThingPlaceMode.Near);
            ThingDef rifle = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_BoltActionRifle");
            if (rifle != null) GenPlace.TryPlaceThing(ThingMaker.MakeThing(rifle), at + IntVec3.West, map, ThingPlaceMode.Near);
            FilthMaker.TryMakeFilth(at + IntVec3.South, map, ThingDefOf.Filth_Blood, 3);
        }
    }
}
