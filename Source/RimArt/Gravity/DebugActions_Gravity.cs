using UnityEngine;
using RimWorld;
using LudeonTK;
using Verse;

namespace RimArt
{
    public static class DebugActions_Gravity
    {
        [DebugAction("RimArts", "Gravity Well: VFX preview", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Preview() => Find.CurrentMap.GetComponent<MapComponent_GravityPreview>().Preview(UI.MouseCell(), false);
        [DebugAction("RimArts", "Gravity Well: frozen full mass", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Frozen() => Find.CurrentMap.GetComponent<MapComponent_GravityPreview>().Preview(UI.MouseCell(), true);
        [DebugAction("RimArts", "Gravity Well: clear preview", allowedGameStates = AllowedGameStates.PlayingOnMap)]
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
