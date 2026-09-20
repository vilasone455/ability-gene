using UnityEngine;
using Verse;
using F = RimArt.MarkFlick;
using T = RimArt.ClapTeleport;

namespace RimArt
{
    /// <summary>
    /// Previews only: no mark is placed and no pawn is drawn. Each entry plays the Mark card flick's
    /// drawing round the chosen cell, which is the middle of the scene as it is in the lab's sketch -
    /// the carrier is 3 cells west of it and the target 3 cells east, a 6-cell flick due east. The
    /// real ability draws through MapComponent_MarkFlicks and MapComponent_Anchors; this exists so
    /// the recorder can compare the port with the sketch.
    /// </summary>
    public static class DebugActions_MarkFlick
    {
        [RimArtDebug("Mark flick", "mark a pawn")]
        public static void Pawn() => Preview().Play(UI.MouseCell(), MarkPreview.Pawn);

        [RimArtDebug("Mark flick", "mark a tile")]
        public static void Tile() => Preview().Play(UI.MouseCell(), MarkPreview.Tile);

        [RimArtDebug("Mark flick", "lift a mark from a pawn")]
        public static void Lift() => Preview().Play(UI.MouseCell(), MarkPreview.Lift);

        [RimArtDebug("Mark flick", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_MarkFlickPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_MarkFlickPreview Preview() => Find.CurrentMap.GetComponent<MapComponent_MarkFlickPreview>();
    }

    public enum MarkPreview { Pawn, Tile, Lift }

    public sealed class MapComponent_MarkFlickPreview : MapComponent
    {
        /// <summary>Half the distance from the carrier to the target, and how long the result stays up.</summary>
        public const float HalfSpan = 3f, Tail = 0.6f;

        public bool active;
        private MarkPreview mode;
        private float seconds;
        private IntVec3 cell;

        public MapComponent_MarkFlickPreview(Map map) : base(map) { }

        public static float Duration(MarkPreview mode) => (mode == MarkPreview.Lift ? F.CatchLength : F.FlickLength) + Tail;

        public void Play(IntVec3 target, MarkPreview play)
        {
            if (!target.InBounds(map) || target.Fogged(map)) return;
            cell = target;
            mode = play;
            seconds = 0f;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;

            Vector3 centre = cell.ToVector3Shifted();
            var middle = new Vector2(centre.x, centre.z);
            Vector2 carrier = middle + Vector2.left * HalfSpan, target = middle + Vector2.right * HalfSpan;
            ClapMark kind = mode == MarkPreview.Tile ? ClapMark.Tile : ClapMark.Pawn;
            Vector2 mark = F.MarkPoint(target, kind);

            if (mode == MarkPreview.Lift)
            {
                float age = seconds - F.Place;
                Vector2 hand = F.Hand(carrier, mark, true);
                if (age < 0f) ClapTeleportGraphics.Mark(target, kind, T.Heart, 0, seconds);
                ClapTeleportGraphics.FlyingCard(mark, hand, F.MarkHeight(kind), F.HandHeight, T.MarkScale, 1f, age / F.CatchFlight, age, map);
                ClapTeleportGraphics.FlickSparkle(hand, 0.16f, age - F.CatchFlight);
            }
            else
            {
                float flying = seconds - F.Release, placed = seconds - F.Place;
                ClapTeleportGraphics.FlyingCard(F.Hand(carrier, mark, false), mark, F.HandHeight, F.MarkHeight(kind), 1f, T.MarkScale,
                    flying / (F.Place - F.Release), flying, map);
                if (placed >= 0f)
                {
                    ClapTeleportGraphics.Mark(target, kind, T.Heart, 0, seconds, -1f, placed);
                    if (kind == ClapMark.Tile) ClapTeleportGraphics.TileOutline(target, 0.5f * SixPathsSlamTiming.Smooth(placed / F.Settle));
                    ClapTeleportGraphics.FlickSparkle(mark, 0.22f, placed);
                }
            }
            if (seconds >= Duration(mode)) active = false;
        }
    }
}
