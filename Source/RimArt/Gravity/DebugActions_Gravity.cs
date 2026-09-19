using UnityEngine;
using RimWorld;
using Verse;

namespace RimArt
{
    public static class DebugActions_Gravity
    {
        [RimArtDebug("Gravity Well", "VFX preview")]
        public static void Preview() => Find.CurrentMap.GetComponent<MapComponent_GravityPreview>().Preview(UI.MouseCell(), false);
        [RimArtDebug("Gravity Well", "frozen full mass")]
        public static void Frozen() => Find.CurrentMap.GetComponent<MapComponent_GravityPreview>().Preview(UI.MouseCell(), true);
        [RimArtDebug("Gravity Well", "clear preview", RimArtDebugKind.Now)]
        public static void Clear() => Find.CurrentMap.GetComponent<MapComponent_GravityPreview>().active = false;
    }
    public sealed class MapComponent_GravityPreview : MapComponent
    {
        public bool active;
        private bool frozen;
        private IntVec3 cell;
        private float seconds;
        public MapComponent_GravityPreview(Map map) : base(map) { }
        public void Preview(IntVec3 target, bool freeze)
        {
            if (!target.InBounds(map) || target.Fogged(map)) return;
            cell = target; frozen = freeze; seconds = freeze ? 4f : 0f; active = true;
        }
        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            if (!frozen) seconds += Time.unscaledDeltaTime;
            if (seconds > 6.5f) { active = false; return; }
            GravityGraphics.Draw(cell.ToVector3Shifted(), seconds, frozen ? 200f : seconds / 6f * 200f,
                seconds <= 6f ? 1f : (6.5f - seconds) * 2f, seconds > 6f, map);
        }
    }
}
