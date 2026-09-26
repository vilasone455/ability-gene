using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt and no pawn is drawn. Each entry plays one picture round the
    /// chosen cell, which is its centre: the middle of the chain for Chain Arc (four targets, the second
    /// one Soaked), the middle of the line from shooter to target for the shot. The weapon, the ability
    /// and their test shortcuts are in CoilGun/Kit.
    /// </summary>
    public static class DebugActions_CoilGun
    {
        [RimArtDebug("Coil Gun", "chain arc")]
        public static void Arc() => Preview().Play(UI.MouseCell(), CoilGunPreview.Arc, 0f);

        [RimArtDebug("Coil Gun", "chain arc south")]
        public static void ArcSouth() => Preview().Play(UI.MouseCell(), CoilGunPreview.Arc, 270f);

        [RimArtDebug("Coil Gun", "shot")]
        public static void Shot() => Preview().Play(UI.MouseCell(), CoilGunPreview.Shot, 0f);

        [RimArtDebug("Coil Gun", "shot at mechanoid")]
        public static void ShotMech() => Preview().Play(UI.MouseCell(), CoilGunPreview.ShotMech, 0f);

        [RimArtDebug("Coil Gun", "battery recharge")]
        public static void Recharge() => Preview().Play(UI.MouseCell(), CoilGunPreview.Recharge, 0f);

        [RimArtDebug("Coil Gun", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_CoilGunPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_CoilGunPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_CoilGunPreview>();
    }

    public enum CoilGunPreview { Arc, Shot, ShotMech, Recharge }

    public sealed class MapComponent_CoilGunPreview : MapComponent
    {
        /// <summary>The recharge preview's length: four arcs.</summary>
        public const float RechargeSeconds = 2f;

        public bool active;
        private CoilGunPreview mode;
        private float seconds, duration, aim;
        private int shaken;
        private IntVec3 cell;

        public MapComponent_CoilGunPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, CoilGunPreview play, float aimDegrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            aim = aimDegrees;
            seconds = 0f;
            shaken = 0;
            duration = play == CoilGunPreview.Arc ? CoilGunArcTiming.ScriptEnd
                : play == CoilGunPreview.Recharge ? RechargeSeconds : CoilGunShotTiming.ScriptEnd;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 centre = cell.ToVector3Shifted();
            if (mode == CoilGunPreview.Arc)
            {
                // A small shake as each bolt lands.
                int hits = CoilGunArcTiming.ScriptTargets.Length;
                if (shaken < hits && seconds >= CoilGunArcTiming.Hit(CoilGunArcTiming.ScriptFire, shaken))
                {
                    shaken++;
                    Find.CameraDriver.shaker.DoShake(CoilGunArcTiming.HitShake);
                }
                CoilGunArcGraphics.DrawPreview(centre, aim, seconds, map);
            }
            else if (mode == CoilGunPreview.Recharge)
            {
                // A battery one cell west of a holder standing on the cell; the arc runs from its top to the gun.
                var holder = new Vector2(centre.x, centre.z);
                VfxDraw.Begin(holder);
                CoilGunGraphics.Recharge(new Vector2(holder.x - 1f, holder.y + 0.35f), holder + new Vector2(0.35f, -0.05f), seconds, 5);
            }
            else CoilGunShotGraphics.DrawPreview(centre, aim, mode == CoilGunPreview.ShotMech, seconds, map);
            if (seconds >= duration) active = false;
        }
    }
}
