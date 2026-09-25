using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt or soaped and no pawn is drawn. Each entry plays one effect's
    /// drawing round the chosen cell, which is the chosen cell as it is in the lab's sketch. The
    /// real weapon is in BubblePipe/Kit and its test entries in DebugActions_Kits ("grant kit...").
    /// </summary>
    public static class DebugActions_BubblePipe
    {
        private static readonly Vector2 NorthWest = new Vector2(Mathf.Cos(140f * Mathf.Deg2Rad), Mathf.Sin(140f * Mathf.Deg2Rad));

        [RimArtDebug("Bubble Pipe", "drifting burst")]
        public static void DriftingBurst() => Preview().Play(UI.MouseCell(), BubblePipePreview.DriftingBurst, Vector2.right);

        [RimArtDebug("Bubble Pipe", "drifting burst north-west")]
        public static void DriftingBurstNorthWest() => Preview().Play(UI.MouseCell(), BubblePipePreview.DriftingBurst, NorthWest);

        [RimArtDebug("Bubble Pipe", "drifting burst empty")]
        public static void DriftingBurstEmpty() => Preview().Play(UI.MouseCell(), BubblePipePreview.DriftingBurstEmpty, Vector2.right);

        [RimArtDebug("Bubble Pipe", "eye pop")]
        public static void EyePop() => Preview().Play(UI.MouseCell(), BubblePipePreview.EyePop, Vector2.right);

        [RimArtDebug("Bubble Pipe", "eye pop north-west")]
        public static void EyePopNorthWest() => Preview().Play(UI.MouseCell(), BubblePipePreview.EyePop, NorthWest);

        [RimArtDebug("Bubble Pipe", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_BubblePipePreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_BubblePipePreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_BubblePipePreview>();
    }

    public enum BubblePipePreview { DriftingBurst, DriftingBurstEmpty, EyePop }

    public sealed class MapComponent_BubblePipePreview : MapComponent
    {
        public bool active;
        private BubblePipePreview mode;
        private float seconds, duration;
        private IntVec3 cell;
        private Vector2 toward;

        public MapComponent_BubblePipePreview(Map map) : base(map) { }

        public void Play(IntVec3 at, BubblePipePreview play, Vector2 facing)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            toward = facing;
            seconds = 0f;
            duration = play == BubblePipePreview.EyePop ? BubblePipeEyePopTiming.ScriptDuration
                : BubblePipeDriftingBurstTiming.ScriptDuration(BubblePipeDriftingBurstTiming.ScriptLife);
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 centre = cell.ToVector3Shifted();
            if (mode == BubblePipePreview.EyePop) BubblePipeEyePopGraphics.DrawPreview(centre, toward, seconds, map);
            else BubblePipeDriftingBurstGraphics.DrawPreview(centre, toward, mode == BubblePipePreview.DriftingBurst, seconds, map);
            if (seconds >= duration) active = false;
        }
    }
}
