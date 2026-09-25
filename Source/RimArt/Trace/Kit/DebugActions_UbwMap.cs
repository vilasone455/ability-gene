using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The real world, a pocket map of its own, with no ability or rule behind it: nobody is taken in.
    /// Spawn a pawn in it with the game's own debug tools to see it at scale. "world map: open" makes the
    /// world beside the map on screen and plays the fire running out ("open (v2 depth)": the plate ground
    /// with height and the sky); "world map: close" runs the white in and removes it; "world map: close
    /// now" removes it at once.
    /// </summary>
    public static class DebugActions_UbwMap
    {
        [RimArtDebug("Trace", "world map: open", RimArtDebugKind.Now)]
        public static void Open() => OpenWorld(false);

        [RimArtDebug("Trace", "world map: open (v2 depth)", RimArtDebugKind.Now)]
        public static void OpenDepth() => OpenWorld(true);

        private static void OpenWorld(bool depth)
        {
            Map source = Find.CurrentMap;
            if (source == null) return;
            if (source.GetComponent<MapComponent_UnlimitedBladeWorks>()?.IsWorld == true)
            {
                Messages.Message("Already in the world: close it first.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            UnlimitedBladeWorksMap.OpenLater(source, new List<IntVec3> { IntVec3.Zero }, depth);
        }

        [RimArtDebug("Trace", "world map: close", RimArtDebugKind.Now)]
        public static void Close()
        {
            MapComponent_UnlimitedBladeWorks component = Find.CurrentMap?.GetComponent<MapComponent_UnlimitedBladeWorks>();
            if (component == null || !component.IsWorld) { Messages.Message("The map on screen is not the world.", MessageTypeDefOf.RejectInput, false); return; }
            component.Close();
        }

        [RimArtDebug("Trace", "world map: close now", RimArtDebugKind.Now)]
        public static void CloseNow()
        {
            Map map = Find.CurrentMap;
            MapComponent_UnlimitedBladeWorks component = map?.GetComponent<MapComponent_UnlimitedBladeWorks>();
            if (component == null || !component.IsWorld) { Messages.Message("The map on screen is not the world.", MessageTypeDefOf.RejectInput, false); return; }
            UnlimitedBladeWorksMap.CloseLater(map);
        }
    }
}
