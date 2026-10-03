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
    /// white closing in) as the flat v1; "world v4" the same on the plate ground with the sword crest and
    /// the sky behind it, the world the ability opens; "reveal" the shot that opens it (a cutscene camera over
    /// the map, the lab's stand-in pawns, the game not paused), handing over to v4 at 4.2 s; "commands: full open", "pin", "draw",
    /// "arm" and "intercept" one command each on the standing v4 with the sketch's stand-ins (<see cref="UbwCommandsPreview"/>). The real world, a pocket map of its own, is under
    /// Kit/: RimArts debug window, Trace, "world map: open" and "open (v1 flat)".
    /// </summary>
    public static class DebugActions_Ubw
    {
        [RimArtDebug("Trace", "unlimited blade works: cast")]
        public static void Cast() => Play(UbwPreview.Cast);

        [RimArtDebug("Trace", "unlimited blade works: world")]
        public static void World() => Play(UbwPreview.World);

        [RimArtDebug("Trace", "unlimited blade works: world v4")]
        public static void WorldV4() => Play(UbwPreview.WorldV4);

        [RimArtDebug("Trace", "unlimited blade works: reveal")]
        public static void Reveal() => Play(UbwPreview.Reveal);

        [RimArtDebug("Trace", "unlimited blade works: commands: full open")]
        public static void FullOpen() => Command(UbwCommandPreview.FullOpen);

        [RimArtDebug("Trace", "unlimited blade works: commands: pin")]
        public static void Pin() => Command(UbwCommandPreview.Pin);

        [RimArtDebug("Trace", "unlimited blade works: commands: draw")]
        public static void Draw() => Command(UbwCommandPreview.Draw);

        [RimArtDebug("Trace", "unlimited blade works: commands: arm")]
        public static void Arm() => Command(UbwCommandPreview.Arm);

        [RimArtDebug("Trace", "unlimited blade works: commands: intercept")]
        public static void Intercept() => Command(UbwCommandPreview.Intercept);

        [RimArtDebug("Trace", "clear preview", RimArtDebugKind.Now)]
        public static void Clear() => Find.CurrentMap?.GetComponent<MapComponent_UbwPreview>()?.Stop();

        private static void Play(UbwPreview play) =>
            Find.CurrentMap.GetComponent<MapComponent_UbwPreview>().Play(UI.MouseCell(), play);

        private static void Command(UbwCommandPreview command) =>
            Find.CurrentMap.GetComponent<MapComponent_UbwPreview>().Play(UI.MouseCell(), UbwPreview.Commands, command);
    }

    public enum UbwPreview { Cast, World, WorldV4, Reveal, Commands }

    public sealed class MapComponent_UbwPreview : MapComponent
    {
        public bool active;
        private UbwPreview mode;
        private UbwCommandPreview command;
        private float seconds, duration;
        private int shaken;
        private IntVec3 cell;

        /// <summary>Each preview's camera shakes, from its sketch's events(), in time order: (when, how hard).</summary>
        private static readonly (float at, float value)[] CastShakes =
        {
            (UbwCastTiming.For(UbwCastTiming.Verse).Taken, UbwCastTiming.TakenShake),
            (UbwCastTiming.For(UbwCastTiming.Verse).Home, UbwCastTiming.HomeShake),
        };
        private static readonly (float at, float value)[] WorldShakes = { (UbwWorldTiming.Start, UbwWorldTiming.StartShake) }, NoShakes = { };

        public MapComponent_UbwPreview(Map map) : base(map) { }

        internal void Play(IntVec3 at, UbwPreview play, UbwCommandPreview which = UbwCommandPreview.FullOpen)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            command = which;
            seconds = 0f;
            shaken = 0;
            duration = play == UbwPreview.Cast ? UbwCastTiming.Duration : play == UbwPreview.Reveal ? UbwRevealTiming.End
                : play == UbwPreview.Commands ? UbwCommandsPreview.Duration(which, map) : UbwWorldTiming.Duration;
            active = true;
            // The shot hands over to the world v4 at its usual framing: the camera goes there first.
            if (play == UbwPreview.Reveal)
            {
                Vector3 c = at.ToVector3Shifted();
                Find.CameraDriver.SetRootPosAndSize(new Vector3(c.x, 0f, c.z + UbwWorldGraphics.SceneNorth + UbwRevealTiming.GameNorth), UbwRevealTiming.CellsTall / 2f);
            }
        }

        public void Stop() => active = false;

        public override void MapComponentUpdate()
        {
            // On another map the preview waits.
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;

            var shakes = mode == UbwPreview.Cast ? CastShakes : mode == UbwPreview.Reveal || mode == UbwPreview.Commands ? NoShakes : WorldShakes;
            while (shaken < shakes.Length && seconds >= shakes[shaken].at) Find.CameraDriver.shaker.DoShake(shakes[shaken++].value);

            Vector3 centre = cell.ToVector3Shifted();
            if (mode == UbwPreview.Cast) UbwCastGraphics.DrawPreview(centre, seconds, map);
            else if (mode == UbwPreview.Reveal) UbwRevealGraphics.DrawPreview(centre, seconds, map);
            else if (mode == UbwPreview.Commands) UbwCommandsPreview.Draw(command, centre, seconds, map);
            else UbwWorldGraphics.DrawPreview(centre, seconds, map, mode == UbwPreview.WorldV4);
            if (seconds >= duration) Stop();
        }
    }
}
