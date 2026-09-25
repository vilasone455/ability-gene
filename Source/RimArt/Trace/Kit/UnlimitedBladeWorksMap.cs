using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Opens and closes the world's pocket map. Nothing is moved in or out: this is the map on its own, for
    /// looking at, as the castle's and the Involute volume's debug entries do. Generation and removal run
    /// as long events, never from inside a map's own update.
    /// </summary>
    public static class UnlimitedBladeWorksMap
    {
        /// <summary>The map is 40 x 40 with the caster in the middle (the sketches' MapHalf).</summary>
        public const int Size = (int)UbwField.MapHalf * 2;
        private static (List<IntVec3> keep, bool depth)? request;

        /// <summary>The landing spots asked for, in cells from the middle, and whether the world is the v2 one with depth, once: the GenStep reads them while it builds.</summary>
        internal static (List<IntVec3> keep, bool depth)? TakeRequest()
        {
            var asked = request;
            request = null;
            return asked;
        }

        /// <summary>Makes the world beside <paramref name="source"/> with no sword over these spots (cells from the middle), and takes the camera to the middle. <paramref name="depth"/>: the world v2, plates with height and a sky.</summary>
        public static void Open(Map source, List<IntVec3> keep, bool depth = false)
        {
            request = (keep, depth);
            Map world = PocketMapUtility.GeneratePocketMap(new IntVec3(Size, 1, Size), UbwDefOf.AG_UnlimitedBladeWorks, null, source);
            request = null;
            if (world == null) return;
            MapComponent_UnlimitedBladeWorks component = world.GetComponent<MapComponent_UnlimitedBladeWorks>();
            component.source = source;
            CameraJumper.TryJump(new GlobalTargetInfo(component.CentreCell, world));
            Messages.Message("Unlimited Blade Works: the world stands. \"world map: close\" runs the fire back in.", MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>Takes the camera back to the map the world was opened from, then removes the world and everything in it.</summary>
        public static void Close(Map world)
        {
            if (world == null || !Find.Maps.Contains(world)) return;
            Map home = world.GetComponent<MapComponent_UnlimitedBladeWorks>()?.source;
            if (home == null || !Find.Maps.Contains(home)) home = Find.AnyPlayerHomeMap;
            if (home != null && home != world) CameraJumper.TryJump(new GlobalTargetInfo(home.Center, home));
            PocketMapUtility.DestroyPocketMap(world);
        }

        /// <summary>Close, as a long event: from a map's own update the map must not be removed in place.</summary>
        public static void CloseLater(Map world) =>
            LongEventHandler.QueueLongEvent(() => Close(world), "AG_UnlimitedBladeWorksClosing", false, null);

        /// <summary>Open, as a long event, behind the game's own "generating map" screen.</summary>
        public static void OpenLater(Map source, List<IntVec3> keep, bool depth = false) =>
            LongEventHandler.QueueLongEvent(() => Open(source, keep, depth), "GeneratingMap", false, null);
    }
}
