using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Opens and closes the castle's pocket map. Nothing is moved in or out: this is the map on its own,
    /// for looking at, as the Involute volume's debug entry does. Generation and removal run as long
    /// events, never from inside a map's own update.
    /// </summary>
    public static class InfinityCastleMap
    {
        private static (int seed, int rooms)? request;

        /// <summary>The seed and room count asked for, once: the GenStep reads it while it builds.</summary>
        internal static (int seed, int rooms)? TakeRequest()
        {
            var asked = request;
            request = null;
            return asked;
        }

        /// <summary>Makes the castle for this seed and room count beside <paramref name="source"/> and takes the camera to the dais.</summary>
        public static void Open(Map source, int seed, int rooms)
        {
            request = (seed, rooms);
            Map castle = PocketMapUtility.GeneratePocketMap(new IntVec3(CastleLayout.Size, 1, CastleLayout.Size),
                InfinityCastleDefOf.AG_InfinityCastle, null, source);
            request = null;
            if (castle == null) return;
            MapComponent_InfinityCastle component = castle.GetComponent<MapComponent_InfinityCastle>();
            component.source = source;
            CameraJumper.TryJump(new GlobalTargetInfo(component.DaisCell, castle));
            Messages.Message($"Infinity Castle: seed {component.seed}, {component.rooms} rooms. Set the Castle sketch's seed and rooms to these to compare.",
                MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>Takes the camera back to the map the castle was opened from, then removes the castle and everything in it.</summary>
        public static void Close(Map castle)
        {
            if (castle == null || !Find.Maps.Contains(castle)) return;
            Map home = castle.GetComponent<MapComponent_InfinityCastle>()?.source;
            if (home == null || !Find.Maps.Contains(home)) home = Find.AnyPlayerHomeMap;
            if (home != null && home != castle) CameraJumper.TryJump(new GlobalTargetInfo(home.Center, home));
            PocketMapUtility.DestroyPocketMap(castle);
        }

        /// <summary>Close, as a long event: from a map's own update the map must not be removed in place.</summary>
        public static void CloseLater(Map castle) =>
            LongEventHandler.QueueLongEvent(() => Close(castle), "AG_InfinityCastleClosing", false, null);

        /// <summary>Open, as a long event, behind the game's own "generating map" screen.</summary>
        public static void OpenLater(Map source, int seed, int rooms) =>
            LongEventHandler.QueueLongEvent(() => Open(source, seed, rooms), "GeneratingMap", false, null);
    }
}
