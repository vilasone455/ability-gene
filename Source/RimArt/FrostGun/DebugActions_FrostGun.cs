using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt or frozen and no pawn is drawn. The chosen cell is the target's:
    /// click a pawn to see the ice round it. The caster stands back along the aim (6 cells for Flash
    /// Freeze, 7 for the shot); the held gun is Core's and is not drawn. The weapon, the ability and
    /// their test shortcuts are in FrostGun/Kit.
    /// </summary>
    public static class DebugActions_FrostGun
    {
        [RimArtDebug("Frost Gun", "flash freeze shatter")]
        public static void Shatter() => Preview().Play(UI.MouseCell(), FrostGunPreview.Shatter, 0f);

        [RimArtDebug("Frost Gun", "flash freeze shatter south")]
        public static void ShatterSouth() => Preview().Play(UI.MouseCell(), FrostGunPreview.Shatter, 270f);

        [RimArtDebug("Frost Gun", "flash freeze thaw")]
        public static void Thaw() => Preview().Play(UI.MouseCell(), FrostGunPreview.Thaw, 0f);

        [RimArtDebug("Frost Gun", "flash freeze thaw south")]
        public static void ThawSouth() => Preview().Play(UI.MouseCell(), FrostGunPreview.Thaw, 270f);

        [RimArtDebug("Frost Gun", "shot")]
        public static void Shot() => Preview().Play(UI.MouseCell(), FrostGunPreview.Shot, 0f);

        [RimArtDebug("Frost Gun", "shot south")]
        public static void ShotSouth() => Preview().Play(UI.MouseCell(), FrostGunPreview.Shot, 270f);

        [RimArtDebug("Frost Gun", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_FrostGunPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_FrostGunPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_FrostGunPreview>();
    }

    public enum FrostGunPreview { Shatter, Thaw, Shot }

    public sealed class MapComponent_FrostGunPreview : MapComponent
    {
        public bool active;
        private FrostGunPreview mode;
        private float seconds, duration, aim;
        private IntVec3 cell;
        /// <summary>The script's camera shakes: when and how hard; each is played once.</summary>
        private readonly List<Vector2> shakes = new List<Vector2>();
        private int shaken;

        public MapComponent_FrostGunPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, FrostGunPreview play, float aimDegrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            aim = aimDegrees;
            seconds = 0f;
            shaken = 0;
            shakes.Clear();
            if (play == FrostGunPreview.Shot)
            {
                duration = FrostGunShotTiming.ScriptEnd;
                for (int k = 0; k < FrostGunShotTiming.ScriptShots; k++)
                    shakes.Add(new Vector2(FrostGunShotTiming.Hit(k, FrostGunShotTiming.ScriptDistance), FrostGunShotTiming.HitShake));
            }
            else
            {
                bool shatter = play == FrostGunPreview.Shatter;
                duration = FrostGunFreezeGraphics.PreviewEnd(shatter);
                float hit = FrostGunFreezeTiming.Hit(FrostGunFreezeTiming.ScriptWarmup, FrostGunFreezeTiming.ScriptDistance - FrostGunGraphics.MuzzleAlong);
                shakes.Add(new Vector2(hit, FrostGunFreezeTiming.HitShake));
                if (shatter) shakes.Add(new Vector2(hit + FrostGunFreezeTiming.Grow + FrostGunFreezeTiming.ScriptShatterAfter, FrostGunFreezeTiming.ShatterShake));
            }
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes.Count && seconds >= shakes[shaken].x)
            {
                Find.CameraDriver.shaker.DoShake(shakes[shaken].y);
                shaken++;
            }
            Vector3 centre = cell.ToVector3Shifted();
            if (mode == FrostGunPreview.Shot) FrostGunShotGraphics.DrawPreview(centre, aim, seconds, map);
            else FrostGunFreezeGraphics.DrawPreview(centre, aim, mode == FrostGunPreview.Shatter, seconds, map);
            if (seconds >= duration) active = false;
        }
    }
}
