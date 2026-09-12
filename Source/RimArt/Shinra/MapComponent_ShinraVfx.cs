using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>One replaceable, unsaved VFX preview per map. It never touches game objects.</summary>
    public class MapComponent_ShinraVfx : MapComponent
    {
        private bool active;
        private Vector3 centre;
        private float elapsed;
        private float playbackSpeed;

        public MapComponent_ShinraVfx(Map map) : base(map) { }

        public void Preview(IntVec3 cell, float speed, bool freezeAtPeak = false)
        {
            if (!cell.InBounds(map) || cell.Fogged(map)) return;
            centre = cell.ToVector3Shifted();
            elapsed = freezeAtPeak ? ShinraVfxTiming.PeakTime : 0f;
            playbackSpeed = freezeAtPeak ? 0f : speed;
            active = true;
        }

        public void Clear() => active = false;

        public override void MapComponentUpdate()
        {
            // Keep the preview on its own map; switching maps suspends its clock.
            if (!active || Find.CurrentMap != map) return;
            if (centre.ToIntVec3().Fogged(map)) { Clear(); return; }

            // Intentionally runs while paused, so it can be inspected without running a fight.
            elapsed += Time.unscaledDeltaTime * playbackSpeed;
            if (elapsed >= ShinraVfxTiming.Duration) { Clear(); return; }
            ShinraVfxGraphics.Draw(centre, elapsed, map);
        }
    }
}
