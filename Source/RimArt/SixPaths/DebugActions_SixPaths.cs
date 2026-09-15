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
        public static void Showcase() => Preview().Play(UI.MouseCell(), 1f, false, false);

        [DebugAction("RimArts", "Six Paths: slow motion", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SlowMotion() => Preview().Play(UI.MouseCell(), 0.25f, false, false);

        [DebugAction("RimArts", "Six Paths: frozen mid-change", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Frozen() => Preview().Play(UI.MouseCell(), 0f, true, false);

        [DebugAction("RimArts", "Six Paths: shape sheet", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Sheet() => Preview().Play(UI.MouseCell(), 0f, false, true);

        [DebugAction("RimArts", "Six Paths: clear showcase", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_SixPathsPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_SixPathsPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_SixPathsPreview>();
    }

    public sealed class MapComponent_SixPathsPreview : MapComponent
    {
        public bool active;
        private bool frozen, sheet;
        private float speed, seconds;
        private IntVec3 cell;

        public MapComponent_SixPathsPreview(Map map) : base(map) { }

        public void Play(IntVec3 target, float rate, bool freeze, bool shapeSheet)
        {
            if (!target.InBounds(map) || target.Fogged(map)) return;
            cell = target;
            speed = rate;
            frozen = freeze;
            sheet = shapeSheet;
            // Frozen starts part way into a change, which is the frame worth inspecting: the
            // outline is halfway between two forms and the rim is at its brightest.
            seconds = freeze ? SixPathsTiming.HoldSeconds + SixPathsTiming.MorphSeconds * 0.5f : 0f;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the showcase runs at the same rate whether the game is paused or at 3x,
            // which is the only way to judge a 0.26 s change.
            if (!frozen) seconds += Time.unscaledDeltaTime * speed;

            if (sheet) SixPathsGraphics.DrawSheet(cell.ToVector3Shifted(), map);
            else SixPathsGraphics.DrawRing(cell.ToVector3Shifted(), seconds, 1f, map);
        }
    }
}
