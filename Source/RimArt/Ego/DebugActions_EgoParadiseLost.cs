using System.Diagnostics;
using UnityEngine;
using Verse;
using T = RimArt.EgoParadiseLostTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: there is no weapon, no def and no rule behind any of this; nobody is hit, slowed or hurt
    /// and no pawn is drawn. Each entry plays one of the Paradise Lost sketch's scenes with the chosen cell as
    /// the wielder's, on the sketch's clock, so the recorder can compare the port with ego-paradise-lost-v2.js.
    /// The corroded scene has one entry per facing because the staff and the wings are drawn per facing.
    /// </summary>
    public static class DebugActions_EgoParadiseLost
    {
        [RimArtDebug("E.G.O.", "paradise lost: room hit")]
        public static void RoomHit() => Play(EgoParadiseLostScene.RoomHit, Rot4.South);

        [RimArtDebug("E.G.O.", "paradise lost: room hit, outdoors")]
        public static void RoomHitOutdoors() => Play(EgoParadiseLostScene.RoomHitOutdoors, Rot4.South);

        [RimArtDebug("E.G.O.", "paradise lost: corroded")]
        public static void Corroded() => Play(EgoParadiseLostScene.Corroded, Rot4.South);

        [RimArtDebug("E.G.O.", "paradise lost: corroded, facing east")]
        public static void CorrodedEast() => Play(EgoParadiseLostScene.Corroded, Rot4.East);

        [RimArtDebug("E.G.O.", "paradise lost: corroded, facing north")]
        public static void CorrodedNorth() => Play(EgoParadiseLostScene.Corroded, Rot4.North);

        [RimArtDebug("E.G.O.", "paradise lost: corroded, facing west")]
        public static void CorrodedWest() => Play(EgoParadiseLostScene.Corroded, Rot4.West);

        [RimArtDebug("E.G.O.", "paradise lost: overclock (hostiles only)")]
        public static void Overclock() => Play(EgoParadiseLostScene.Overclock, Rot4.South);

        [RimArtDebug("E.G.O.", "paradise lost: timing, rebuild every frame (on/off)", RimArtDebugKind.Now)]
        public static void Rebuild() => MapComponent_EgoParadiseLostPreview.ToggleRebuild();

        [RimArtDebug("E.G.O.", "paradise lost: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_EgoParadiseLostPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(EgoParadiseLostScene scene, Rot4 facing) =>
            Find.CurrentMap.GetComponent<MapComponent_EgoParadiseLostPreview>().Play(UI.MouseCell(), scene, facing);
    }

    /// <summary>
    /// Plays a Paradise Lost scene on the sketch's clock with the sketch's defaults. The script (who walks where,
    /// who is hit when) is <see cref="EgoParadiseLostScript"/>; this passes each hit pawn's position now to the
    /// thorns, the wielder's facing and flash to the staff, the corroded look and the rings, and shakes the camera
    /// as the sketch's events do. The sketch's sounds are not played: no SoundDefs exist yet.
    ///
    /// When a corroded or overclock run ends it logs how long the corroded look (star, wings, halo) took to draw:
    /// main-thread time for the geometry, the strip rebuilds and the DrawMesh calls, not the GPU's work. The debug
    /// window's timing switch rebuilds everything every frame, as before the bake, for a comparison.
    /// </summary>
    public sealed class MapComponent_EgoParadiseLostPreview : MapComponent
    {
        public bool active;
        private float seconds;
        private IntVec3 cell;
        private Rot4 facing;
        private EgoParadiseLostScript script;
        private int shaken;
        private readonly Stopwatch watch = new Stopwatch();
        private double fullMs, fullMax, partMs, partMax;
        private int fullFrames, partFrames;

        public MapComponent_EgoParadiseLostPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, EgoParadiseLostScene scene, Rot4 face)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            facing = face;
            script = new EgoParadiseLostScript(scene);
            seconds = 0f;
            shaken = 0;
            fullMs = fullMax = partMs = partMax = 0;
            fullFrames = partFrames = 0;
            active = true;
        }

        public static void ToggleRebuild()
        {
            EgoParadiseLostCorrodedGraphics.Rebuild = !EgoParadiseLostCorrodedGraphics.Rebuild;
            Log.Message("Paradise Lost preview: the corroded look is now " + (EgoParadiseLostCorrodedGraphics.Rebuild ? "rebuilt every frame" : "baked") + ".");
        }

        public override void MapComponentUpdate()
        {
            if (!active || script == null || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            float s = seconds;
            while (shaken < script.ShakeAt.Count && s >= script.ShakeAt[shaken])
                Find.CameraDriver.shaker.DoShake(script.ShakeBy[shaken++]);

            Vector3 centre = cell.ToVector3Shifted();
            var wielder = new Vector2(centre.x, centre.z);
            Rot4 face = script.Facing(s, facing);
            float look = script.Look(s);

            watch.Restart();
            EgoParadiseLostCorrodedGraphics.Draw(wielder, face, look, s, map);
            watch.Stop();
            Measure(look, watch.Elapsed.TotalMilliseconds);
            foreach (float t in script.RingTimes)
                EgoParadiseLostRingGraphics.Draw(new EgoParadiseLostRingShot { Wielder = wielder, Radius = T.RingRadius }, s - t, map);
            for (int i = 0; i < script.People.Count; i++)
            {
                EgoParadiseLostPerson q = script.People[i];
                Vector2 at = wielder + EgoParadiseLostScript.PosAt(q, s);
                for (int j = 0; j < q.Hits.Count; j++)
                    EgoParadiseLostThornGraphics.Draw(at, s - q.Hits[j], i * 31 + j * 7, !script.Room, map);
            }
            EgoParadiseLostStaffGraphics.Draw(wielder, face, script.Flash(s), map);

            if (seconds >= script.End)
            {
                active = false;
                LogTime();
            }
        }

        // Fully open (look 1) and opening or folding are kept apart: only the second rebuilds strips every frame.
        // The one-off bakes land in the opening frames (each wing bakes as it opens fully) and in the first fully
        // open frame (the star and the halo), so they show in the two maxima, not in the averages.
        private void Measure(float look, double ms)
        {
            if (look <= 0f) return;
            if (look < 1f)
            {
                partMs += ms;
                partMax = System.Math.Max(partMax, ms);
                partFrames++;
            }
            else
            {
                fullMs += ms;
                fullMax = System.Math.Max(fullMax, ms);
                fullFrames++;
            }
        }

        private void LogTime()
        {
            if (fullFrames == 0) return;
            Log.Message($"Paradise Lost preview, facing {facing.ToStringHuman()}, corroded look "
                + (EgoParadiseLostCorrodedGraphics.Rebuild ? "rebuilt every frame" : "baked")
                + $": {fullMs / fullFrames:0.000} ms a frame fully open (max {fullMax:0.000}, {fullFrames} frames),"
                + $" {(partFrames > 0 ? partMs / partFrames : 0):0.000} ms opening and folding (max {partMax:0.000}, {partFrames} frames). Main thread only.");
        }
    }
}
