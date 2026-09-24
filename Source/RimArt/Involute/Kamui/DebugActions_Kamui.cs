using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: Kamui's dimension drawn over the current map, centred on the chosen cell, as the
    /// lab's Kamui dimension sketch draws it with "Show: empty map" (seed 1, 48 cells, the whole map in
    /// view). Nothing is generated or moved and no pawn is drawn. The real dimension is the Fold organ's
    /// pocket map: RimArts debug window, Involute, "go to the volume".
    /// </summary>
    public static class DebugActions_Kamui
    {
        [RimArtDebug("Kamui dimension", "field")]
        public static void Field() => Play(KamuiGraphics.Fight);

        [RimArtDebug("Kamui dimension", "field (still)")]
        public static void FieldStill() => Play(KamuiGraphics.Still);

        [RimArtDebug("Kamui dimension", "clear preview", RimArtDebugKind.Now)]
        public static void Clear() => Find.CurrentMap?.GetComponent<MapComponent_KamuiPreview>()?.Stop();

        private static void Play(string palette) =>
            Find.CurrentMap.GetComponent<MapComponent_KamuiPreview>().Play(UI.MouseCell(), palette);
    }

    public sealed class MapComponent_KamuiPreview : MapComponent
    {
        /// <summary>The field does not move; the preview holds it this long, then clears.</summary>
        public const float Duration = 1f;
        /// <summary>The sketch's default seed, so the recording and the sketch show the same map.</summary>
        public const int PreviewSeed = 1;
        public bool active;
        private float seconds;
        private string palette = KamuiGraphics.Fight;
        private IntVec3 cell;

        public MapComponent_KamuiPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, string palette)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            this.palette = palette;
            seconds = 0f;
            active = true;
        }

        public void Stop() => active = false;

        public override void MapComponentUpdate()
        {
            // On another map the preview waits.
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            if (seconds >= Duration) { active = false; return; }

            var (layout, field) = KamuiGraphics.For(PreviewSeed, KamuiLayout.DefaultSize);
            // The sketch's "the whole map" view: the map's middle on the chosen cell's corner.
            var corner = new Vector2(cell.x - layout.Size / 2f, cell.z - layout.Size / 2f);
            KamuiGraphics.Draw(layout, field, corner, KamuiLayers.Preview, palette);
        }
    }
}
