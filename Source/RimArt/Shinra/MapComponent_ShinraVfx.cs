using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One replaceable, unsaved picture preview per map: the lab sketch's timeline on one cell, without a pawn
    /// (<see cref="ShinraDome"/>'s Lead, charge and burst). It never touches game objects. <see cref="charge"/> is
    /// the seconds held; 0 plays the tap.
    /// </summary>
    public class MapComponent_ShinraVfx : MapComponent
    {
        private bool active;
        private Vector3 centre;
        private float elapsed;
        private float playbackSpeed;
        private float charge;
        private bool released;

        public MapComponent_ShinraVfx(Map map) : base(map) { }

        /// <param name="freezeAt">Seconds after the burst to hold on, or a negative number to play.</param>
        public void Preview(IntVec3 cell, float heldSeconds, float speed, float freezeAt = -1f)
        {
            if (!cell.InBounds(map) || cell.Fogged(map)) return;
            centre = cell.ToVector3Shifted();
            charge = heldSeconds;
            bool frozen = freezeAt >= 0f;
            elapsed = frozen ? ShinraDome.PreviewBurst(charge) + freezeAt : 0f;
            playbackSpeed = frozen ? 0f : speed;
            // A preview that opens after the release has nothing to announce, so the frozen one stays silent.
            released = frozen;
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
            if (elapsed >= ShinraDome.PreviewEnd(charge)) { Clear(); return; }
            float burst = ShinraDome.PreviewBurst(charge), e = elapsed - burst;
            if (!released && e >= 0f)
            {
                released = true;
                ShinraSound.Release(map, centre.ToIntVec3());
            }
            var o = new Vector2(centre.x, centre.z);
            ShinraPalette col = ShinraPalette.Soil;
            if (e >= 0f)
            {
                PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
                ShinraDomeGraphics.Burst(o, e, ShinraDome.PreviewCast(charge), col, sun);
            }
            else if (charge <= 0f)
                ShinraDomeGraphics.TapPress(o, Mathf.Clamp01((elapsed - (burst - 0.12f)) / 0.12f), col);
            else if (elapsed > ShinraDome.Lead)
            {
                // Charge counted from the start of the gesture, frozen when the release is asked.
                float held = Mathf.Min(elapsed - ShinraDome.Lead, charge);
                ShinraDomeGraphics.Charge(o, elapsed, Mathf.Max(Mathf.Clamp01(held / ShinraDome.FullCharge), 0.05f), col);
                ShinraDomeGraphics.SizeRing(o, held, ShinraDome.PreviewSizes);
            }
        }
    }
}
