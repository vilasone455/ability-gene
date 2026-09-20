using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only. There is no weapon, no ability and no def behind any of this: nobody is hurt, no
    /// wall breaks and no pawn is drawn. Each entry plays one effect's drawing round the chosen cell,
    /// which is the target cell as it is in the lab's sketch.
    /// </summary>
    public static class DebugActions_PaperBomb
    {
        private static readonly Vector2 NorthWest = new Vector2(Mathf.Cos(140f * Mathf.Deg2Rad), Mathf.Sin(140f * Mathf.Deg2Rad));

        [RimArtDebug("Paper Bomb", "tag throw")]
        public static void TagThrow() => Preview().Play(UI.MouseCell(), PaperBombPreview.TagThrow, Vector2.right, TagThrowTarget.Floor);

        [RimArtDebug("Paper Bomb", "tag throw north-west")]
        public static void TagThrowNorthWest() => Preview().Play(UI.MouseCell(), PaperBombPreview.TagThrow, NorthWest, TagThrowTarget.Floor);

        [RimArtDebug("Paper Bomb", "tag throw carried")]
        public static void TagThrowCarried() => Preview().Play(UI.MouseCell(), PaperBombPreview.TagThrow, Vector2.right, TagThrowTarget.Pawn);

        [RimArtDebug("Paper Bomb", "tag throw at wall")]
        public static void TagThrowWall() => Preview().Play(UI.MouseCell(), PaperBombPreview.TagThrow, Vector2.right, TagThrowTarget.Wall);

        [RimArtDebug("Paper Bomb", "tag line")]
        public static void TagLine() => Preview().Play(UI.MouseCell(), PaperBombPreview.TagLine, Vector2.right, TagThrowTarget.Floor);

        [RimArtDebug("Paper Bomb", "tag line north-west")]
        public static void TagLineNorthWest() => Preview().Play(UI.MouseCell(), PaperBombPreview.TagLine, NorthWest, TagThrowTarget.Floor);

        [RimArtDebug("Paper Bomb", "paper shroud")]
        public static void Shroud() => Preview().Play(UI.MouseCell(), PaperBombPreview.Shroud, Vector2.right, TagThrowTarget.Pawn);

        [RimArtDebug("Paper Bomb", "paper shroud north-west")]
        public static void ShroudNorthWest() => Preview().Play(UI.MouseCell(), PaperBombPreview.Shroud, NorthWest, TagThrowTarget.Pawn);

        [RimArtDebug("Paper Bomb", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_PaperBombPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_PaperBombPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_PaperBombPreview>();
    }

    public enum PaperBombPreview { TagThrow, TagLine, Shroud }

    public sealed class MapComponent_PaperBombPreview : MapComponent
    {
        public bool active;
        private PaperBombPreview mode;
        private const int MostShakes = 8;
        private float seconds, duration;
        // The camera shakes of the effect being played, in time order, and how many have been done.
        private readonly float[] shakeAt = new float[MostShakes], shakeSize = new float[MostShakes];
        private int shakes, shaken;
        private IntVec3 cell;
        private Vector2 toward;
        private TagThrowTarget target;

        public MapComponent_PaperBombPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, PaperBombPreview play, Vector2 facing, TagThrowTarget sticksIn)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            toward = facing;
            target = sticksIn;
            seconds = 0f;
            shakes = shaken = 0;
            switch (play)
            {
                case PaperBombPreview.TagLine:
                    duration = PaperBombTagLineTiming.ScriptDuration;
                    for (int i = 0; i < PaperBombTagLineTiming.ScriptTags; i++)
                        Shake(PaperBombTagLineTiming.ScriptFuseAt + PaperBombTagLineTiming.BurstAt(i, PaperBombTagLineTiming.Start, PaperBombTagLineTiming.ScriptPerTag), PaperBombTagLineTiming.BurstShake);
                    break;
                case PaperBombPreview.Shroud:
                    duration = PaperBombShroudTiming.Duration(PaperBombShroudTiming.ScriptHeld);
                    Shake(PaperBombShroudTiming.BurstAt(PaperBombShroudTiming.ScriptHeld), PaperBombShroudTiming.BurstShake);
                    break;
                default:
                    duration = PaperBombTagThrowTiming.Duration(PaperBombTagThrowTiming.ScriptFuse);
                    Shake(PaperBombTagThrowTiming.BurstAt(PaperBombTagThrowTiming.ScriptFuse), PaperBombTagThrowTiming.BurstShake);
                    break;
            }
            active = true;
        }

        private void Shake(float at, float size)
        {
            shakeAt[shakes] = at;
            shakeSize[shakes++] = size;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes && seconds >= shakeAt[shaken]) Find.CameraDriver.shaker.DoShake(shakeSize[shaken++]);
            Vector3 centre = cell.ToVector3Shifted();
            switch (mode)
            {
                case PaperBombPreview.TagLine:
                    PaperBombTagLineGraphics.DrawPreview(centre, toward, seconds, map);
                    break;
                case PaperBombPreview.Shroud:
                    PaperBombShroudGraphics.DrawPreview(centre, toward, seconds, map);
                    break;
                default:
                    PaperBombTagThrowGraphics.DrawPreview(centre, toward, target, seconds, map);
                    break;
            }
            if (seconds >= duration) active = false;
        }
    }
}
