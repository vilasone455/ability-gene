using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: pictures, with no ability, no gene and no rule behind them. Nobody is taken,
    /// frozen or moved and no pawn is drawn. "open (take)" and "open (return)" play the home-map side
    /// round the chosen cell; "castle" and "castle (biwa room)" play the castle's own timeline (the
    /// sketch's castle, seed 1 with 38 rooms) over the map's ground, centred on the whole castle or on
    /// its biwa room. The real castle, a pocket map of its own, is under Kit/: RimArts debug window,
    /// Infinity Castle, "castle map: open".
    /// </summary>
    public static class DebugActions_InfinityCastle
    {
        [RimArtDebug("Infinity Castle", "open (take)")]
        public static void OpenTake() => Play(InfinityCastlePreview.OpenTake);

        [RimArtDebug("Infinity Castle", "open (return)")]
        public static void OpenReturn() => Play(InfinityCastlePreview.OpenReturn);

        [RimArtDebug("Infinity Castle", "castle")]
        public static void Castle() => Play(InfinityCastlePreview.Castle);

        [RimArtDebug("Infinity Castle", "castle (biwa room)")]
        public static void CastleBiwaRoom() => Play(InfinityCastlePreview.CastleBiwaRoom);

        [RimArtDebug("Infinity Castle", "clear preview", RimArtDebugKind.Now)]
        public static void Clear() => Find.CurrentMap?.GetComponent<MapComponent_InfinityCastlePreview>()?.Stop();

        private static void Play(InfinityCastlePreview play) =>
            Find.CurrentMap.GetComponent<MapComponent_InfinityCastlePreview>().Play(UI.MouseCell(), play);
    }

    public enum InfinityCastlePreview { OpenTake, OpenReturn, Castle, CastleBiwaRoom }

    public sealed class MapComponent_InfinityCastlePreview : MapComponent
    {
        public bool active;
        private InfinityCastlePreview mode;
        private float seconds, duration;
        private bool shaken;
        private IntVec3 cell;

        public MapComponent_InfinityCastlePreview(Map map) : base(map) { }

        public void Play(IntVec3 at, InfinityCastlePreview play)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            seconds = 0f;
            shaken = false;
            duration = play == InfinityCastlePreview.OpenTake ? InfinityCastleOpenTiming.TakeDuration
                : play == InfinityCastlePreview.OpenReturn ? InfinityCastleOpenTiming.ReturnDuration
                : InfinityCastleInsideTiming.Duration;
            active = true;
        }

        public void Stop() => active = false;

        public override void MapComponentUpdate()
        {
            // On another map the preview waits.
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;

            Vector3 centre = cell.ToVector3Shifted();
            switch (mode)
            {
                case InfinityCastlePreview.OpenTake:
                    if (!shaken && seconds >= InfinityCastleOpenTiming.StrumAt + InfinityCastleOpenTiming.ShakeDelay)
                    {
                        Find.CameraDriver.shaker.DoShake(InfinityCastleOpenTiming.StrumShake);
                        shaken = true;
                    }
                    InfinityCastleOpenGraphics.DrawPreview(centre, true, seconds, map);
                    break;
                case InfinityCastlePreview.OpenReturn:
                    InfinityCastleOpenGraphics.DrawPreview(centre, false, seconds, map);
                    break;
                default:
                    InfinityCastleInsideGraphics.DrawPreview(centre, mode == InfinityCastlePreview.CastleBiwaRoom, seconds, map);
                    break;
            }
            if (seconds >= duration) Stop();
        }
    }
}
