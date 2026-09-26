using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Makes, opens and closes the world's pocket map. <see cref="Make"/> is the ability's (UbwCast, which
    /// moves everyone in and out itself); Open and Close are the debug window's, where nobody is moved: the
    /// map on its own, for looking at, as the castle's and the Involute volume's debug entries do. Removal
    /// runs as a long event, never from inside a map's own update.
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

        /// <summary>Makes the world beside <paramref name="source"/> with no sword over these spots (cells from the middle), and nothing else: no camera, no message. <paramref name="depth"/>: the world v2, plates with height and a sky. Null if the game would not make it.</summary>
        public static Map Make(Map source, List<IntVec3> keep, bool depth)
        {
            request = (keep, depth);
            Map world;
            try
            {
                world = PocketMapUtility.GeneratePocketMap(new IntVec3(Size, 1, Size), UbwDefOf.AG_UnlimitedBladeWorks, null, source);
            }
            finally
            {
                request = null;
            }
            if (world == null) return null;
            world.GetComponent<MapComponent_UnlimitedBladeWorks>().source = source;
            return world;
        }

        /// <summary>The debug window's world: made with no one in it, and the camera taken to the middle.</summary>
        public static void Open(Map source, List<IntVec3> keep, bool depth = false)
        {
            Map world = Make(source, keep, depth);
            if (world == null) return;
            MapComponent_UnlimitedBladeWorks component = world.GetComponent<MapComponent_UnlimitedBladeWorks>();
            CameraJumper.TryJump(new GlobalTargetInfo(component.CentreCell, world));
            Messages.Message("Unlimited Blade Works: the world stands. \"world map: close\" runs the fire back in.", MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>Takes the camera back to the map the world was opened from if it is on the world, then removes the world and everything still in it.</summary>
        public static void Close(Map world)
        {
            if (world == null || !Find.Maps.Contains(world)) return;
            Map home = world.GetComponent<MapComponent_UnlimitedBladeWorks>()?.source;
            if (home == null || !Find.Maps.Contains(home)) home = Find.AnyPlayerHomeMap;
            if (home != null && home != world && Find.CurrentMap == world) CameraJumper.TryJump(new GlobalTargetInfo(home.Center, home));
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
