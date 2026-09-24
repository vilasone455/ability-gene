using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only. There is no ability, no pocket map and no def behind any of this: nobody is taken,
    /// frozen or spared and no pawn is drawn. "unlimited void: open" plays the home-map side round the
    /// chosen cell, Gojo's: the sphere closing over 9 cells, shrinking into the ball, the ball breaking.
    /// "unlimited void: inside" plays the inside round the chosen cell, where Gojo lands: the white
    /// arrival, the speed-line opening with the camera pushing in on the vanishing point 7.5 cells north
    /// and back, the void and its black hole, the collapse to white. The void is drawn over the map's
    /// ground (the real one will be a pocket map of its own).
    /// </summary>
    public static class DebugActions_Gojo
    {
        [RimArtDebug("Gojo", "unlimited void: open")]
        public static void Open() => Play(GojoPreview.Open);

        [RimArtDebug("Gojo", "unlimited void: inside")]
        public static void Inside() => Play(GojoPreview.Inside);

        [RimArtDebug("Gojo", "clear preview", RimArtDebugKind.Now)]
        public static void Clear() => Find.CurrentMap?.GetComponent<MapComponent_GojoPreview>()?.Stop();

        private static void Play(GojoPreview play) =>
            Find.CurrentMap.GetComponent<MapComponent_GojoPreview>().Play(UI.MouseCell(), play);
    }

    public enum GojoPreview { Open, Inside }

    public sealed class MapComponent_GojoPreview : MapComponent
    {
        public bool active;
        private GojoPreview mode;
        private float seconds, duration, reach;
        private int shaken;
        private IntVec3 cell;
        private readonly CameraMove push = new CameraMove(UnlimitedVoidInsideTiming.CameraEvents);

        /// <summary>The open's camera shakes, from its sketch's events(), in time order: (when, how hard). The inside has none.</summary>
        private static readonly (float at, float value)[] OpenShakes =
        {
            (UnlimitedVoidOpenTiming.OpenAt, UnlimitedVoidOpenTiming.OpenShake),
            (UnlimitedVoidOpenTiming.BurstAt + UnlimitedVoidOpenTiming.BurstShakeDelay, UnlimitedVoidOpenTiming.BurstShake),
        };
        private static readonly (float at, float value)[] NoShakes = { };

        public MapComponent_GojoPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, GojoPreview play)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            Stop();
            cell = at;
            mode = play;
            seconds = 0f;
            shaken = 0;
            duration = play == GojoPreview.Open ? UnlimitedVoidOpenTiming.Duration : UnlimitedVoidInsideTiming.Duration;
            if (play == GojoPreview.Inside)
            {
                Vector3 centre = at.ToVector3Shifted();
                reach = VoidSpaceGraphics.ReachFor(UnlimitedVoidInsideTiming.PointFor(new Vector2(centre.x, centre.z)));
                push.Begin();
            }
            active = true;
        }

        /// <summary>Ends the preview and gives the camera back if the push still holds it.</summary>
        public void Stop()
        {
            active = false;
            push.Release();
        }

        public override void MapComponentUpdate()
        {
            // On another map the preview waits: the camera then belongs to that map and is left alone.
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;

            var shakes = mode == GojoPreview.Open ? OpenShakes : NoShakes;
            while (shaken < shakes.Length && seconds >= shakes[shaken].at) Find.CameraDriver.shaker.DoShake(shakes[shaken++].value);

            Vector3 centre = cell.ToVector3Shifted();
            if (mode == GojoPreview.Open) UnlimitedVoidOpenGraphics.DrawPreview(centre, seconds, map);
            else
            {
                push.Apply(new Vector2(centre.x, centre.z), seconds);
                UnlimitedVoidInsideGraphics.DrawPreview(centre, reach, seconds, map);
            }
            if (seconds >= duration) Stop();
        }
    }
}
