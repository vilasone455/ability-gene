using UnityEngine;
using Verse;
using T = RimArt.BanshoTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is pulled, hurt or stunned. Each entry plays one scenario of the lab's
    /// pain-bansho-tenin.js at its defaults round the chosen cell (the middle of the pull: Pain stands 4 cells back
    /// along the aim, the target 4 cells forward, 8 cells apart), aimed east unless the label names a direction.
    /// The stand-ins are drawn as the sketch draws them (Pain, the pulled pawn, its afterimages, the blocker, the
    /// thrumbo, the sandbags), so the recorder can compare the port with the sketch; "effects only" leaves them out,
    /// as the game does. The camera shakes at the grip and at the stop, block or slam.
    /// </summary>
    public static class DebugActions_BanshoPreview
    {
        [RimArtDebug("Pain", "bansho: sandbags")]
        public static void Sandbags() => Play(BanshoScenario.Sandbags, 0f);

        [RimArtDebug("Pain", "bansho: sandbags north")]
        public static void SandbagsNorth() => Play(BanshoScenario.Sandbags, 90f);

        [RimArtDebug("Pain", "bansho: sandbags south")]
        public static void SandbagsSouth() => Play(BanshoScenario.Sandbags, 270f);

        [RimArtDebug("Pain", "bansho: sandbags north-west")]
        public static void SandbagsNorthWest() => Play(BanshoScenario.Sandbags, 135f);

        [RimArtDebug("Pain", "bansho: blocked")]
        public static void Blocked() => Play(BanshoScenario.Blocked, 0f);

        [RimArtDebug("Pain", "bansho: thrumbo")]
        public static void Thrumbo() => Play(BanshoScenario.Thrumbo, 0f);

        /// <summary>What the game draws over the real pawns: the sandbags scenario with no stand-ins.</summary>
        [RimArtDebug("Pain", "bansho: sandbags, effects only")]
        public static void EffectsOnly() => Play(BanshoScenario.Sandbags, 0f, false);

        [RimArtDebug("Pain", "bansho: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_BanshoPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(BanshoScenario scenario, float aimDegrees, bool standIns = true) =>
            Find.CurrentMap.GetComponent<MapComponent_BanshoPreview>().Play(UI.MouseCell(), scenario, aimDegrees, standIns);
    }

    /// <summary>The sketch's scenarios: a raider behind sandbags; another raider steps into the line; a thrumbo (body size 4), dragged.</summary>
    public enum BanshoScenario { Sandbags, Blocked, Thrumbo }

    /// <summary>
    /// Replays one Banshō Ten'in scenario on its own clock, the sketch's s from 0 (rest) to its end, and switches
    /// itself off. The sketch's target distance is 8 cells.
    /// </summary>
    public sealed class MapComponent_BanshoPreview : MapComponent
    {
        public const float Distance = 8f;

        public bool active;
        /// <summary>The sketch's s.</summary>
        public float seconds;
        // Summed in double so a frame that lands on a phase boundary steps as the sketch does.
        private double clock;
        private IntVec3 cell;
        private BanshoScenario scenario;
        private Vector2 aim;
        private BanshoTimes times;
        private int shaken;
        private bool standIns = true;

        public MapComponent_BanshoPreview(Map map) : base(map) { }

        public static BanshoTimes TimesFor(BanshoScenario scenario) =>
            T.Times(Distance, scenario == BanshoScenario.Thrumbo, scenario == BanshoScenario.Blocked);

        public void Play(IntVec3 at, BanshoScenario play, float aimDegrees, bool withStandIns = true)
        {
            standIns = withStandIns;
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            scenario = play;
            aim = VfxDraw.Turn(aimDegrees);
            times = TimesFor(play);
            clock = 0.0;
            seconds = 0f;
            shaken = 0;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            clock += Time.unscaledDeltaTime;
            seconds = (float)clock;
            float s = seconds;
            if (s >= times.end)
            {
                active = false;
                return;
            }
            Shake(s);
            Vector3 centre = cell.ToVector3Shifted();
            Vector2 pain = new Vector2(centre.x, centre.z) - aim * (Distance / 2f);
            BanshoView view = BanshoGraphics.Scripted(pain, aim, times, s, standIns);
            BanshoGraphics.Draw(view, s, map);
            if (standIns && scenario == BanshoScenario.Sandbags) BanshoGraphics.Sandbags(pain, aim, Distance - 1f, map);
        }

        // The sketch's events: 0.012 at the grip; then 0.01 at the end of a drag, 0.03 at a block, 0.06 at the slam.
        private void Shake(float s)
        {
            if (shaken == 0 && s >= times.grip)
            {
                shaken = 1;
                Find.CameraDriver.shaker.DoShake(T.ShakeGrip);
            }
            float at = times.Normal ? times.down : times.arrive;
            if (shaken == 1 && s >= at)
            {
                shaken = 2;
                Find.CameraDriver.shaker.DoShake(times.heavy ? T.ShakeDrag : times.blocked ? T.ShakeBlock : T.ShakeSlam);
            }
        }
    }
}
