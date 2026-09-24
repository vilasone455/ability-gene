using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The real castle, a pocket map of its own, with no ability or rule behind it: nobody is taken in.
    /// Spawn a pawn in it with the game's own debug tools to see it at scale. "castle map: open" makes the
    /// lab's castle (seed 1, 38 rooms), "castle map: open (random)" any other; the message names the
    /// seed and room count, which the Castle sketch reproduces exactly. "castle map: release" plays
    /// Release on the castle on screen and closes it after the fade; "castle map: close now" closes it
    /// at once.
    /// </summary>
    public static class DebugActions_InfinityCastleMap
    {
        [RimArtDebug("Infinity Castle", "castle map: open", RimArtDebugKind.Now)]
        public static void Open() => OpenCastle(InfinityCastleInsideTiming.Seed, InfinityCastleInsideTiming.Rooms);

        [RimArtDebug("Infinity Castle", "castle map: open (random)", RimArtDebugKind.Now)]
        public static void OpenRandom()
        {
            var step = InfinityCastleDefOf.AG_InfinityCastleRooms?.genStep as GenStep_InfinityCastle;
            OpenCastle(step != null ? step.seeds.RandomInRange : Rand.RangeInclusive(1, 60), step != null ? step.rooms.RandomInRange : CastleLayout.DefaultRooms);
        }

        [RimArtDebug("Infinity Castle", "castle map: release", RimArtDebugKind.Now)]
        public static void Release()
        {
            MapComponent_InfinityCastle castle = Find.CurrentMap?.GetComponent<MapComponent_InfinityCastle>();
            if (castle == null || !castle.IsCastle) { Messages.Message("The map on screen is not a castle.", MessageTypeDefOf.RejectInput, false); return; }
            castle.Release();
        }

        [RimArtDebug("Infinity Castle", "castle map: close now", RimArtDebugKind.Now)]
        public static void CloseNow()
        {
            Map map = Find.CurrentMap;
            MapComponent_InfinityCastle castle = map?.GetComponent<MapComponent_InfinityCastle>();
            if (castle == null || !castle.IsCastle) { Messages.Message("The map on screen is not a castle.", MessageTypeDefOf.RejectInput, false); return; }
            InfinityCastleMap.CloseLater(map);
        }

        [RimArtDebug("Infinity Castle", "castle: shift east", RimArtDebugKind.Cell)]
        public static void ShiftEast() => Shift(1, 0);

        [RimArtDebug("Infinity Castle", "castle: shift north", RimArtDebugKind.Cell)]
        public static void ShiftNorth() => Shift(0, 1);

        [RimArtDebug("Infinity Castle", "castle: shift west", RimArtDebugKind.Cell)]
        public static void ShiftWest() => Shift(-1, 0);

        [RimArtDebug("Infinity Castle", "castle: shift south", RimArtDebugKind.Cell)]
        public static void ShiftSouth() => Shift(0, -1);

        [RimArtDebug("Infinity Castle", "castle: crush", RimArtDebugKind.Cell)]
        public static void Crush()
        {
            Map map = Find.CurrentMap;
            MapComponent_InfinityCastle castle = map?.GetComponent<MapComponent_InfinityCastle>();
            if (castle == null || !castle.IsCastle) { Messages.Message("The map on screen is not a castle.", MessageTypeDefOf.RejectInput, false); return; }
            IntVec3 cell = UI.MouseCell();
            if (cell.InBounds(map) && !castle.TryCrush(cell, out string why)) Messages.Message(why, MessageTypeDefOf.RejectInput, false);
        }

        [RimArtDebug("Infinity Castle", "castle: seal doorway", RimArtDebugKind.Cell)]
        public static void Seal() => SealOrOpen(true);

        [RimArtDebug("Infinity Castle", "castle: open doorway", RimArtDebugKind.Cell)]
        public static void OpenDoorway() => SealOrOpen(false);

        /// <summary>Seal or open the doorway under the mouse (either of its two door cells).</summary>
        private static void SealOrOpen(bool seal)
        {
            Map map = Find.CurrentMap;
            MapComponent_InfinityCastle castle = map?.GetComponent<MapComponent_InfinityCastle>();
            if (castle == null || !castle.IsCastle) { Messages.Message("The map on screen is not a castle.", MessageTypeDefOf.RejectInput, false); return; }
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map)) return;
            bool done = seal ? castle.TrySeal(cell, out string why) : castle.TryOpen(cell, out why);
            if (!done) Messages.Message(why, MessageTypeDefOf.RejectInput, false);
        }

        [RimArtDebug("Infinity Castle", "castle: void drop", RimArtDebugKind.Pawn)]
        public static void VoidDrop(Pawn pawn)
        {
            MapComponent_InfinityCastle castle = pawn.Map?.GetComponent<MapComponent_InfinityCastle>();
            if (castle == null || !castle.IsCastle) { Messages.Message("That pawn is not in a castle.", MessageTypeDefOf.RejectInput, false); return; }
            castle.VoidDrop(pawn, castle.Castle.RoomAt(pawn.Position.x, pawn.Position.z)?.Id ?? -1);
        }

        /// <summary>Shift the room under the mouse: the Shift command with no biwa yet (the strum's rings still leave the dais).</summary>
        private static void Shift(int dx, int dz)
        {
            Map map = Find.CurrentMap;
            MapComponent_InfinityCastle castle = map?.GetComponent<MapComponent_InfinityCastle>();
            if (castle == null || !castle.IsCastle) { Messages.Message("The map on screen is not a castle.", MessageTypeDefOf.RejectInput, false); return; }
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map)) return;
            if (!castle.TryShift(cell, dx, dz, out string why)) Messages.Message(why, MessageTypeDefOf.RejectInput, false);
        }

        private static void OpenCastle(int seed, int rooms)
        {
            Map source = Find.CurrentMap;
            if (source == null) return;
            if (source.GetComponent<MapComponent_InfinityCastle>()?.IsCastle == true)
            {
                Messages.Message("Already in a castle: release or close it first.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            InfinityCastleMap.OpenLater(source, seed, rooms);
        }
    }
}
