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
