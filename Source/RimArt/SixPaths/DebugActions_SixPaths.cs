using LudeonTK;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A showcase only. There is no ability, no hediff and no def behind any of this: the orbs
    /// exist to be looked at while the silhouettes and the timing are settled.
    /// </summary>
    public static class DebugActions_SixPaths
    {
        [DebugAction("RimArts", "Six Paths: orb showcase", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Showcase() => Preview().Play(UI.MouseCell(), PreviewMode.Ring, 1f, false);

        [DebugAction("RimArts", "Six Paths: slow motion", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SlowMotion() => Preview().Play(UI.MouseCell(), PreviewMode.Ring, 0.25f, false);

        [DebugAction("RimArts", "Six Paths: frozen mid-change", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Frozen() => Preview().Play(UI.MouseCell(), PreviewMode.Ring, 0f, true);

        [DebugAction("RimArts", "Six Paths: shape sheet", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Sheet() => Preview().Play(UI.MouseCell(), PreviewMode.Sheet, 0f, false);

        [DebugAction("RimArts", "Six Paths: slam", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Slam() => Preview().Play(UI.MouseCell(), PreviewMode.Slam, 1f, false);

        [DebugAction("RimArts", "Six Paths: slam slow motion", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SlamSlow() => Preview().Play(UI.MouseCell(), PreviewMode.Slam, 0.2f, false);

        [DebugAction("RimArts", "Six Paths: slam frozen mid-fall", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SlamFrozen() => Preview().Play(UI.MouseCell(), PreviewMode.Slam, 0f, true);

        [DebugAction("RimArts", "Six Paths: clear showcase", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_SixPathsPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_SixPathsPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_SixPathsPreview>();
    }

    public enum PreviewMode { Ring, Sheet, Slam }

    public sealed class MapComponent_SixPathsPreview : MapComponent
    {
        public bool active;
        private PreviewMode mode;
        private bool frozen, shaken;
        private float speed, seconds;
        private IntVec3 cell;

        public MapComponent_SixPathsPreview(Map map) : base(map) { }

        public void Play(IntVec3 target, PreviewMode play, float rate, bool freeze)
        {
            if (!target.InBounds(map) || target.Fogged(map)) return;
            cell = target;
            mode = play;
            speed = rate;
            frozen = freeze;
            shaken = false;
            // Frozen starts at the frame worth inspecting. For the ring that is halfway between
            // two forms, with the rim at its brightest; for the slam it is halfway down, where the
            // block is clear of the ground and its shadow and its three faces can all be judged.
            seconds = !freeze ? 0f
                : play == PreviewMode.Slam ? SixPathsSlamTiming.FallAt + SixPathsSlamTiming.Fall * 0.5f
                : SixPathsTiming.HoldSeconds + SixPathsTiming.MorphSeconds * 0.5f;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the showcase runs at the same rate whether the game is paused or at 3x,
            // which is the only way to judge a 0.26 s change.
            if (!frozen) seconds += Time.unscaledDeltaTime * speed;

            switch (mode)
            {
                case PreviewMode.Sheet:
                    SixPathsGraphics.DrawSheet(cell.ToVector3Shifted(), map);
                    break;
                case PreviewMode.Slam:
                    if (!frozen && seconds >= SixPathsSlamTiming.LandAt && !shaken)
                    {
                        shaken = true;
                        Find.CameraDriver.shaker.DoShake(0.14f);
                    }
                    SixPathsSlamGraphics.Draw(cell.ToVector3Shifted(), seconds, map);
                    // The slam is one event rather than a loop, so it puts itself away.
                    if (!frozen && seconds > SixPathsSlamTiming.Duration) active = false;
                    break;
                default:
                    SixPathsGraphics.DrawRing(cell.ToVector3Shifted(), seconds, 1f, map);
                    break;
            }
        }
    }
}
