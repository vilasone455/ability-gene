using UnityEngine;
using Verse;
using V = RimArt.VectorShove;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody moves or is hurt and no pawn is drawn. Each entry plays the Vector shove
    /// picture round the chosen cell, which is the cell the lab's sketch centres on (the middle of the
    /// throw), on the sketch's clock: the mace lands at <see cref="VectorShove.Lead"/>, the touch follows,
    /// so the recorder can compare the port with accelerator-vector-shove.js.
    /// </summary>
    public static class DebugActions_VectorShovePreview
    {
        private const float East = 0f, NorthWest = 140f;

        [RimArtDebug("Accelerator", "vector shove: raider into a wall")]
        public static void Wall() => Play(VectorShoveScene.Wall, V.React, East);

        [RimArtDebug("Accelerator", "vector shove: raider into a wall, window closed")]
        public static void WallClosed() => Play(VectorShoveScene.Wall, V.ClosedReact, East);

        [RimArtDebug("Accelerator", "vector shove: raider into a wall, north-west")]
        public static void WallNorthWest() => Play(VectorShoveScene.Wall, V.React, NorthWest);

        [RimArtDebug("Accelerator", "vector shove: raider through his line")]
        public static void Line() => Play(VectorShoveScene.Line, V.React, East);

        [RimArtDebug("Accelerator", "vector shove: chunk at a shooter")]
        public static void Chunk() => Play(VectorShoveScene.Chunk, V.React, East);

        [RimArtDebug("Accelerator", "vector shove: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_VectorShovePreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(VectorShoveScene scene, float react, float degrees) =>
            Find.CurrentMap.GetComponent<MapComponent_VectorShovePreview>().Play(UI.MouseCell(), scene, react, degrees);
    }

    /// <summary>The sketch's scenarios: a raider thrown into a wall, through two of his friends, a stone chunk thrown at a shooter.</summary>
    public enum VectorShoveScene { Wall, Line, Chunk }

    public sealed class MapComponent_VectorShovePreview : MapComponent
    {
        public bool active;
        private VectorShoveScene scene;
        private float seconds, react, degrees;
        private IntVec3 cell;
        private int shaken;
        private readonly VectorShoveLiner[] liners = new VectorShoveLiner[2];

        public MapComponent_VectorShovePreview(Map map) : base(map) { }

        /// <summary>Cells the preview's throw travels: to the shooter, to the wall, or its full length.</summary>
        public static float Stop(VectorShoveScene scene) =>
            scene == VectorShoveScene.Chunk ? Mathf.Min(V.ChunkCells, V.ShooterAt - V.ChunkShort)
            : scene == VectorShoveScene.Wall ? V.Stop(V.Cells, V.WallAt - 0.5f) : V.Cells;

        public static bool HitsWall(VectorShoveScene scene) => scene == VectorShoveScene.Wall && V.ReachesWall(V.Cells, V.WallAt - 0.5f);

        /// <summary>The touch on the sketch's clock: after the mace hit, or 0.2 s in for the chunk, which nobody hit.</summary>
        public static float TouchAt(VectorShoveScene scene, float react) => V.Lead + (scene == VectorShoveScene.Chunk ? V.ChunkReact : react);

        public static float Duration(VectorShoveScene scene, float react) => TouchAt(scene, react) + V.End(Stop(scene));

        public void Play(IntVec3 at, VectorShoveScene play, float react, float degrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            scene = play;
            this.react = react;
            this.degrees = degrees;
            seconds = 0f;
            shaken = 0;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            bool chunk = scene == VectorShoveScene.Chunk, wall = HitsWall(scene), returned = !chunk && V.Returned(react);
            float stop = Stop(scene), touch = TouchAt(scene, react), s = seconds - touch;
            Shake(chunk, wall, returned, touch, stop);

            // The sketch's layout: the chosen cell is the middle of the throw, Accelerator Half cells behind
            // it, the target on the next cell toward it.
            Vector3 centre = cell.ToVector3Shifted();
            var o = new Vector2(centre.x, centre.z);
            Vector2 toward = VfxDraw.Turn(degrees);
            Vector2 caster = o - toward * (chunk ? V.ChunkHalf : V.Half), start = caster + toward;

            // The mace hit on Accelerator and the window round the raider, until the throw starts.
            if (!chunk)
            {
                VectorShoveGraphics.HitFlash(caster, toward, seconds - V.Lead, map);
                if (s < V.Touch) VectorShoveGraphics.WindowRing(start, seconds - V.Lead, map);
            }
            // The preview's script: the chunk lies beside Accelerator before the picture starts (the game's own item).
            if (chunk && s < -V.LineLead)
            {
                VfxDraw.Sprite(start, 0.7f, 0.4f, VfxDraw.Fade(VfxDraw.Ink, 0.35f), VfxDraw.soft, VfxDraw.Floor + 0.05f);
                PaperBombGraphics.Rock(start, V.ChunkSize, 20f, 1f, 2, VfxDraw.Overhead + 0.01f);
            }

            int count = 0;
            if (scene == VectorShoveScene.Line)
                for (int i = 0; i < V.Liners.GetLength(0); i++)
                    liners[count++] = new VectorShoveLiner { Along = V.Liners[i, 0], Across = V.Liners[i, 1], Side = V.Liners[i, 2] };
            VectorShoveGraphics.Draw(new VectorShoveShot
            {
                Caster = caster, Start = start, Degrees = degrees, Stop = stop,
                Lies = start + toward * (wall ? stop - V.LieBack : stop),
                HitsWall = wall, WallFace = V.WallAt - 0.5f, Bonus = returned,
                Thing = chunk, Hit = chunk, DrawRock = true, HitAt = start + toward * V.ShooterAt,
                Liners = liners, LinerCount = count,
                Sleeve = AcceleratorGraphics.Shirt, Skin = AcceleratorGraphics.Skin,
            }, s, map);
            if (seconds >= Duration(scene, react)) active = false;
        }

        // The sketch's events(): the mace hit, the throw, the arrival.
        private void Shake(bool chunk, bool wall, bool returned, float touch, float stop)
        {
            if (shaken == 0 && seconds >= V.Lead)
            {
                shaken = 1;
                if (!chunk) Find.CameraDriver.shaker.DoShake(V.MaceShake);
            }
            if (shaken == 1 && seconds >= touch + V.Touch)
            {
                shaken = 2;
                Find.CameraDriver.shaker.DoShake(V.ThrowShake);
            }
            if (shaken == 2 && seconds >= touch + V.Arrive(stop))
            {
                shaken = 3;
                Find.CameraDriver.shaker.DoShake(V.ArriveShake(wall, returned));
            }
        }
    }
}
