using UnityEngine;
using Verse;

namespace RimArt
{
    public static class DebugActions_ChibakuPlates
    {
        public const float Radius = 6f;

        [RimArtDebug("Pain", "Chibaku ground plates")]
        public static void Plates() => MapComponent_ChibakuPlates.Of(Find.CurrentMap)?.Begin(UI.MouseCell(), Radius);
        [RimArtDebug("Pain", "Chibaku ground plates held up")]
        public static void PlatesHeld() => MapComponent_ChibakuPlates.Of(Find.CurrentMap)?.Begin(UI.MouseCell(), Radius, 3f);
        [RimArtDebug("Pain", "Chibaku ground plates clear", RimArtDebugKind.Now)]
        public static void Clear() => MapComponent_ChibakuPlates.Of(Find.CurrentMap)?.Stop();
    }

    /// <summary>
    /// Plays the Chibaku ground plates test on a map: the ground round a cell is captured and cut, the plates
    /// lift 1 cell and land again (<see cref="ChibakuGround.End"/> seconds of real time, also while paused), or
    /// stay at one moment when frozen. A preview only: nothing is saved and the ground is not changed.
    /// </summary>
    public sealed class MapComponent_ChibakuPlates : MapComponent
    {
        private ChibakuGround ground;
        private float seconds;
        private float? frozenAt;

        public MapComponent_ChibakuPlates(Map map) : base(map) { }

        public static MapComponent_ChibakuPlates Of(Map map) => map?.GetComponent<MapComponent_ChibakuPlates>();

        public ChibakuGround Ground => ground;

        public ChibakuGround Begin(IntVec3 cell, float radius, float? frozen = null)
        {
            Stop();
            if (!cell.InBounds(map)) return null;
            ground = ChibakuGround.Capture(map, cell, radius);
            seconds = frozen ?? 0f;
            frozenAt = frozen;
            return ground;
        }

        /// <summary>Holds the plates at <paramref name="at"/> seconds of the timeline.</summary>
        public void Freeze(float at) => frozenAt = seconds = at;

        public void Stop()
        {
            ground?.Dispose();
            ground = null;
        }

        public override void MapComponentUpdate()
        {
            if (ground == null || Find.CurrentMap != map) return;
            if (frozenAt.HasValue) seconds = frozenAt.Value;
            else
            {
                seconds += Time.unscaledDeltaTime;
                if (seconds > ChibakuGround.End)
                {
                    Stop();
                    return;
                }
            }
            ground.Draw(seconds);
        }

        public override void MapRemoved() => Stop();
    }
}
