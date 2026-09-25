using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: pictures, with no ability and no rule behind them. Nobody is taken or moved and no
    /// pawn is drawn. Both play 2 cells north of the chosen cell, where the sketches put them for the lab's
    /// camera. "unlimited blade works: cast" plays the home-map side (the chant, the fire along its lines,
    /// the white, the ring burning while everyone is away, the return); "unlimited blade works: world"
    /// plays the inside over the map's ground (the white, the fire running out, the world standing, the
    /// white closing in); "world v2" the same on the plate ground with the sky (the v2 sketch, an
    /// experiment). The real world, a pocket map of its own, is under Kit/: RimArts debug window, Trace,
    /// "world map: open" and "open (v2 depth)".
    /// </summary>
    public static class DebugActions_Ubw
    {
        [RimArtDebug("Trace", "unlimited blade works: cast")]
        public static void Cast() => Play(UbwPreview.Cast);

        [RimArtDebug("Trace", "unlimited blade works: world")]
        public static void World() => Play(UbwPreview.World);

        [RimArtDebug("Trace", "unlimited blade works: world v2")]
        public static void WorldV2() => Play(UbwPreview.WorldV2);

        [RimArtDebug("Trace", "clear preview", RimArtDebugKind.Now)]
        public static void Clear() => Find.CurrentMap?.GetComponent<MapComponent_UbwPreview>()?.Stop();

        private static void Play(UbwPreview play) =>
            Find.CurrentMap.GetComponent<MapComponent_UbwPreview>().Play(UI.MouseCell(), play);
    }

    public enum UbwPreview { Cast, World, WorldV2 }

    public sealed class MapComponent_UbwPreview : MapComponent
    {
        public bool active;
        private UbwPreview mode;
        private float seconds, duration;
        private int shaken;
        private IntVec3 cell;

        /// <summary>Each preview's camera shakes, from its sketch's events(), in time order: (when, how hard).</summary>
        private static readonly (float at, float value)[] CastShakes =
        {
            (UbwCastTiming.For(UbwCastTiming.Verse).Taken, UbwCastTiming.TakenShake),
            (UbwCastTiming.For(UbwCastTiming.Verse).Home, UbwCastTiming.HomeShake),
        };
        private static readonly (float at, float value)[] WorldShakes = { (UbwWorldTiming.Start, UbwWorldTiming.StartShake) };

        public MapComponent_UbwPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, UbwPreview play)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            seconds = 0f;
            shaken = 0;
            duration = play == UbwPreview.Cast ? UbwCastTiming.Duration : UbwWorldTiming.Duration;
            active = true;
        }

        public void Stop() => active = false;

        public override void MapComponentUpdate()
        {
            // On another map the preview waits.
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;

            var shakes = mode == UbwPreview.Cast ? CastShakes : WorldShakes;
            while (shaken < shakes.Length && seconds >= shakes[shaken].at) Find.CameraDriver.shaker.DoShake(shakes[shaken++].value);

            Vector3 centre = cell.ToVector3Shifted();
            if (mode == UbwPreview.Cast) UbwCastGraphics.DrawPreview(centre, seconds, map);
            else UbwWorldGraphics.DrawPreview(centre, seconds, map, mode == UbwPreview.WorldV2);
            if (seconds >= duration) Stop();
        }
    }
}
