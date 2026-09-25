using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real weapon. The drawing previews are in DebugActions_FlameGauntlet.</summary>
    public static class DebugActions_FlameGauntletKit
    {
        /// <summary>Puts a flame gauntlet in the clicked pawn's hands, dropping what it held.</summary>
        [RimArtDebug("Flame Gauntlet", "give flame gauntlet to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                Messages.Message("this pawn cannot hold a weapon", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pawn.equipment.Primary != null)
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position, false);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(FlameGauntletDefOf.AG_FlameGauntlet));
        }

        [RimArtDebug("Flame Gauntlet", "set heat 0", RimArtDebugKind.Pawn)]
        private static void Heat0(Pawn pawn) => CompFlameGauntlet.HeldBy(pawn)?.SetHeat(0f);

        [RimArtDebug("Flame Gauntlet", "set heat 8", RimArtDebugKind.Pawn)]
        private static void Heat8(Pawn pawn) => CompFlameGauntlet.HeldBy(pawn)?.SetHeat(8f);

        [RimArtDebug("Flame Gauntlet", "set heat 14", RimArtDebugKind.Pawn)]
        private static void Heat14(Pawn pawn) => CompFlameGauntlet.HeldBy(pawn)?.SetHeat(14f);

        [RimArtDebug("Flame Gauntlet", "set heat 20", RimArtDebugKind.Pawn)]
        private static void Heat20(Pawn pawn) => CompFlameGauntlet.HeldBy(pawn)?.SetHeat(20f);

        /// <summary>A 3x3 block of fires on the clicked cell, for Devour.</summary>
        [RimArtDebug("Flame Gauntlet", "start 3x3 fire")]
        private static void Fire3x3()
        {
            Map map = Find.CurrentMap;
            foreach (IntVec3 c in CellRect.CenteredOn(UI.MouseCell(), 1))
                FlameGauntletTestFire.Start(c, map);
        }
    }

    /// <summary>A fire in a cell whatever it stands on, for the debug window and the game tests.</summary>
    public static class FlameGauntletTestFire
    {
        public static Fire Start(IntVec3 cell, Map map, float size = 0.6f)
        {
            if (!cell.InBounds(map) || cell.Impassable(map) || cell.ContainsStaticFire(map)) return null;
            var fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
            fire.fireSize = size;
            GenSpawn.Spawn(fire, cell, map, Rot4.North);
            return fire;
        }
    }
}
