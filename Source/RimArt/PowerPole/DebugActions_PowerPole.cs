using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only. There is no weapon, no ability and no def behind any of this: nobody is hit or
    /// pushed and no pawn is drawn. Each entry plays one effect's drawing round the chosen cell, which
    /// is the middle of the scene as it is in the lab's sketch.
    /// </summary>
    public static class DebugActions_PowerPole
    {
        private static readonly Vector2 NorthWest = new Vector2(Mathf.Cos(140f * Mathf.Deg2Rad), Mathf.Sin(140f * Mathf.Deg2Rad));

        [RimArtDebug("Power Pole", "extend thrust")]
        public static void Thrust() => Preview().Play(UI.MouseCell(), PowerPolePreview.Thrust, Vector2.right, false);

        [RimArtDebug("Power Pole", "extend thrust north-west")]
        public static void ThrustNorthWest() => Preview().Play(UI.MouseCell(), PowerPolePreview.Thrust, NorthWest, false);

        [RimArtDebug("Power Pole", "extend thrust into wall")]
        public static void ThrustWall() => Preview().Play(UI.MouseCell(), PowerPolePreview.Thrust, Vector2.right, true);

        [RimArtDebug("Power Pole", "sweep")]
        public static void Sweep() => Preview().Play(UI.MouseCell(), PowerPolePreview.Sweep, Vector2.right, false);

        [RimArtDebug("Power Pole", "sweep north-west")]
        public static void SweepNorthWest() => Preview().Play(UI.MouseCell(), PowerPolePreview.Sweep, NorthWest, false);

        [RimArtDebug("Power Pole", "sweep past wall")]
        public static void SweepWall() => Preview().Play(UI.MouseCell(), PowerPolePreview.Sweep, Vector2.right, true);

        // Vault Strike draws north and south aims differently, so each has an entry.
        [RimArtDebug("Power Pole", "vault strike east")]
        public static void StrikeEast() => Preview().Play(UI.MouseCell(), PowerPolePreview.Strike, Vector2.right, false);

        [RimArtDebug("Power Pole", "vault strike north")]
        public static void StrikeNorth() => Preview().Play(UI.MouseCell(), PowerPolePreview.Strike, Vector2.up, false);

        [RimArtDebug("Power Pole", "vault strike south")]
        public static void StrikeSouth() => Preview().Play(UI.MouseCell(), PowerPolePreview.Strike, Vector2.down, false);

        [RimArtDebug("Power Pole", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_PowerPolePreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_PowerPolePreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_PowerPolePreview>();
    }

    public enum PowerPolePreview { Thrust, Sweep, Strike }

    public sealed class MapComponent_PowerPolePreview : MapComponent
    {
        private const int MostShakes = 8;

        public bool active;
        private PowerPolePreview mode;
        private float seconds, duration;
        private IntVec3 cell;
        private Vector2 toward;
        /// <summary>Thrust: a wall stops the carry. Sweep: a wall cell stands in the arc.</summary>
        private bool flag;
        // The camera shakes of the effect being played, in time order, and how many have been done.
        private readonly float[] shakeAt = new float[MostShakes], shakeSize = new float[MostShakes];
        private int shakes, shaken;

        public MapComponent_PowerPolePreview(Map map) : base(map) { }

        public void Play(IntVec3 target, PowerPolePreview play, Vector2 facing, bool option)
        {
            if (!target.InBounds(map) || target.Fogged(map)) return;
            cell = target;
            mode = play;
            toward = facing;
            flag = option;
            seconds = 0f;
            shakes = shaken = 0;
            switch (play)
            {
                case PowerPolePreview.Sweep:
                    duration = PowerPoleSweepTiming.Duration;
                    // Sorted, because shakes are played in time order.
                    float[] hits = (float[])PowerPoleSweepTiming.Script(ThunderGodTiming.Degrees(facing), option).HitTime.Clone();
                    System.Array.Sort(hits);
                    for (int e = 0; e < hits.Length; e++) Shake(hits[e], PowerPoleSweepTiming.HitShake);
                    break;
                case PowerPolePreview.Strike:
                    PowerPoleStrikeShot strike = PowerPoleStrikeTiming.Script;
                    duration = strike.Duration;
                    Shake(strike.LaunchAt, PowerPoleStrikeTiming.LaunchShake);
                    Shake(strike.StrikeHitAt, PowerPoleStrikeTiming.StrikeShake);
                    Shake(strike.LandAt, PowerPoleStrikeTiming.LandShake);
                    break;
                default:
                    duration = PowerPoleThrustTiming.Duration;
                    Shake(PowerPoleThrustTiming.HitAt, PowerPoleThrustTiming.HitShake);
                    if (option) Shake(PowerPoleThrustTiming.PushedAt, PowerPoleThrustTiming.WallShake);
                    break;
            }
            active = true;
        }

        private void Shake(float at, float size)
        {
            shakeAt[shakes] = at;
            shakeSize[shakes++] = size;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes && seconds >= shakeAt[shaken]) Find.CameraDriver.shaker.DoShake(shakeSize[shaken++]);

            Vector3 centre = cell.ToVector3Shifted();
            switch (mode)
            {
                case PowerPolePreview.Sweep:
                    PowerPoleSweepGraphics.DrawPreview(centre, toward, flag, seconds, map);
                    break;
                case PowerPolePreview.Strike:
                    PowerPoleStrikeGraphics.DrawPreview(centre, toward, seconds, map);
                    break;
                default:
                    PowerPoleThrustGraphics.DrawPreview(centre, toward, flag, seconds, map);
                    break;
            }
            if (seconds >= duration) active = false;
        }
    }
}
