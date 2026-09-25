using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt or soaked and no pawn is drawn. Each entry plays one sketch's
    /// drawing round the chosen cell, which is the sketch's centre: halfway between caster and target
    /// for Stream Shot, the middle of the cone for Hydro Pump. The weapon, abilities and their test
    /// shortcuts are in WaterGun/Kit.
    /// </summary>
    public static class DebugActions_WaterGun
    {
        [RimArtDebug("Water Gun", "stream shot")]
        public static void Stream() => Preview().Play(UI.MouseCell(), WaterGunPreview.Stream, 0f);

        [RimArtDebug("Water Gun", "stream shot south")]
        public static void StreamSouth() => Preview().Play(UI.MouseCell(), WaterGunPreview.Stream, 270f);

        [RimArtDebug("Water Gun", "stream shot burning pawn")]
        public static void StreamBurning() => Preview().Play(UI.MouseCell(), WaterGunPreview.StreamBurning, 0f);

        [RimArtDebug("Water Gun", "hydro pump")]
        public static void Pump() => Preview().Play(UI.MouseCell(), WaterGunPreview.Pump, 0f);

        [RimArtDebug("Water Gun", "hydro pump south")]
        public static void PumpSouth() => Preview().Play(UI.MouseCell(), WaterGunPreview.Pump, 270f);

        [RimArtDebug("Water Gun", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_WaterGunPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_WaterGunPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_WaterGunPreview>();
    }

    public enum WaterGunPreview { Stream, StreamBurning, Pump }

    public sealed class MapComponent_WaterGunPreview : MapComponent
    {
        public bool active;
        private WaterGunPreview mode;
        private float seconds, duration, aim, shakeAt, shakeSize;
        private bool shaken;
        private IntVec3 cell;

        public MapComponent_WaterGunPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, WaterGunPreview play, float aimDegrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            aim = aimDegrees;
            seconds = 0f;
            shaken = false;
            if (play == WaterGunPreview.Pump)
            {
                duration = WaterGunPumpTiming.ScriptEnd;
                shakeAt = WaterGunPumpTiming.Blast;
                shakeSize = WaterGunPumpTiming.BlastShake;
            }
            else
            {
                duration = WaterGunStreamTiming.ScriptEnd;
                shakeAt = WaterGunStreamTiming.Hit(WaterGunStreamTiming.ScriptDistance);
                shakeSize = WaterGunStreamTiming.HitShake;
            }
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            if (!shaken && seconds >= shakeAt)
            {
                shaken = true;
                Find.CameraDriver.shaker.DoShake(shakeSize);
            }
            Vector3 centre = cell.ToVector3Shifted();
            if (mode == WaterGunPreview.Pump) WaterGunPumpGraphics.DrawPreview(centre, aim, seconds, map);
            else WaterGunStreamGraphics.DrawPreview(centre, aim, mode == WaterGunPreview.StreamBurning, seconds, map);
            if (seconds >= duration) active = false;
        }
    }
}
