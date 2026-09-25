using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: no fire is put out or started, no heat is kept and no pawn is drawn. Devour
    /// entries use the chosen cell as the target (the wearer 4 cells back along the aim); Release
    /// entries use it as the sketch's centre (the wearer 3 cells back). The weapon, abilities and
    /// their test shortcuts are in FlameGauntlet/Kit.
    /// </summary>
    public static class DebugActions_FlameGauntlet
    {
        [RimArtDebug("Flame Gauntlet", "devour base fire")]
        public static void DevourBase() => Preview().Devour(UI.MouseCell(), 0, 0f, 0f);

        [RimArtDebug("Flame Gauntlet", "devour burning pawn")]
        public static void DevourPawn() => Preview().Devour(UI.MouseCell(), 1, 0f, 0f);

        [RimArtDebug("Flame Gauntlet", "devour starting hot (14)")]
        public static void DevourHot() => Preview().Devour(UI.MouseCell(), 2, 14f, 0f);

        [RimArtDebug("Flame Gauntlet", "devour south")]
        public static void DevourSouth() => Preview().Devour(UI.MouseCell(), 0, 0f, 270f);

        [RimArtDebug("Flame Gauntlet", "release 20 heat")]
        public static void Release20() => Preview().Release(UI.MouseCell(), 20f, true, 0f);

        [RimArtDebug("Flame Gauntlet", "release 8 heat (short cone)")]
        public static void Release8() => Preview().Release(UI.MouseCell(), 8f, true, 0f);

        [RimArtDebug("Flame Gauntlet", "release too cold (3 heat)")]
        public static void ReleaseCold() => Preview().Release(UI.MouseCell(), 3f, true, 0f);

        [RimArtDebug("Flame Gauntlet", "release south")]
        public static void ReleaseSouth() => Preview().Release(UI.MouseCell(), 20f, true, 270f);

        [RimArtDebug("Flame Gauntlet", "release east no enemy")]
        public static void ReleaseEast() => Preview().Release(UI.MouseCell(), 20f, false, 0f);

        [RimArtDebug("Flame Gauntlet", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_FlameGauntletPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_FlameGauntletPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_FlameGauntletPreview>();
    }

    public sealed class MapComponent_FlameGauntletPreview : MapComponent
    {
        public bool active;
        private bool release, enemy, shaken;
        private int scenario;
        private float seconds, duration, aim, heat, shakeAt;
        private IntVec3 cell;

        public MapComponent_FlameGauntletPreview(Map map) : base(map) { }

        /// <param name="play">0 base fire, 1 burning pawn, 2 starting hot (14).</param>
        public void Devour(IntVec3 at, int play, float startHeat, float aimDegrees)
        {
            if (!Start(at, aimDegrees)) return;
            release = false;
            scenario = play;
            heat = startHeat;
            Vector3 c = at.ToVector3Shifted();
            duration = FlameDevourTiming.End(FlameGauntletDevourGraphics.Script(new Vector2(c.x, c.z), aimDegrees, play, startHeat));
            shakeAt = float.MaxValue;
        }

        public void Release(IntVec3 at, float startHeat, bool withEnemy, float aimDegrees)
        {
            if (!Start(at, aimDegrees)) return;
            release = true;
            enemy = withEnemy;
            heat = startHeat;
            Vector3 c = at.ToVector3Shifted();
            FlameReleaseShot shot = FlameGauntletReleaseGraphics.Script(new Vector2(c.x, c.z), aimDegrees, startHeat, withEnemy);
            duration = FlameReleaseTiming.End(shot);
            shakeAt = shot.Lit > 0 ? FlameReleaseTiming.Go : float.MaxValue;
        }

        private bool Start(IntVec3 at, float aimDegrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return false;
            cell = at;
            aim = aimDegrees;
            seconds = 0f;
            shaken = false;
            active = true;
            return true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            if (!shaken && seconds >= shakeAt)
            {
                shaken = true;
                Find.CameraDriver.shaker.DoShake(FlameReleaseTiming.Shake);
            }
            Vector3 centre = cell.ToVector3Shifted();
            if (release) FlameGauntletReleaseGraphics.DrawPreview(centre, aim, heat, enemy, seconds, map);
            else FlameGauntletDevourGraphics.DrawPreview(centre, aim, scenario, heat, seconds, map);
            if (seconds >= duration) active = false;
        }
    }
}
