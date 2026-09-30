using UnityEngine;
using Verse;
using R = RimArt.GojoRed;

namespace RimArt
{
    /// <summary>
    /// Previews only: there is no ability, no def and no pawn rule behind any of this; nobody is thrown,
    /// pushed or hurt and no pawn is drawn. Each entry plays the Reversal: Red picture round the chosen
    /// cell, which is the cell the lab's sketch centres on: the middle of the whole event, from Gojo to the
    /// wall (raider into a wall), to the end of the throw (group in the open), or 1.5 cells past the target
    /// cell (empty cell). It runs on the sketch's clock, so the recorder can compare the port with
    /// gojo-red-v2.js.
    /// </summary>
    public static class DebugActions_GojoRedPreview
    {
        [RimArtDebug("Gojo", "red: raider into a wall")]
        public static void Wall() => Play(GojoRedScene.Wall, 0f);

        [RimArtDebug("Gojo", "red: raider into a wall, aim 120")]
        public static void Wall120() => Play(GojoRedScene.Wall, 120f);

        [RimArtDebug("Gojo", "red: raider into a wall, aim 250")]
        public static void Wall250() => Play(GojoRedScene.Wall, 250f);

        [RimArtDebug("Gojo", "red: group in the open")]
        public static void Open() => Play(GojoRedScene.Open, 0f);

        [RimArtDebug("Gojo", "red: empty cell")]
        public static void Empty() => Play(GojoRedScene.Empty, 0f);

        [RimArtDebug("Gojo", "red: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_GojoRedPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(GojoRedScene scene, float aim) =>
            Find.CurrentMap.GetComponent<MapComponent_GojoRedPreview>().Play(UI.MouseCell(), scene, aim);
    }

    /// <summary>The sketch's scenarios: a raider thrown into a wall, a group in the open, Red sent at an empty cell.</summary>
    public enum GojoRedScene { Wall, Open, Empty }

    /// <summary>
    /// Plays Reversal: Red on the sketch's clock with the sketch's defaults. The preview's script lives in
    /// <see cref="GojoRed.ScriptBody"/> and here: where the thrown raider is each frame, and the other raiders
    /// pushed from where the sketch stands them. Only the ones inside the burst radius are passed on; the
    /// sketch only tints the others red.
    /// </summary>
    public sealed class MapComponent_GojoRedPreview : MapComponent
    {
        public bool active;
        private float seconds, aim;
        private GojoRedScene scene;
        private IntVec3 cell;
        private int shaken;
        private readonly GojoRedPushed[] pushed = new GojoRedPushed[2];

        public MapComponent_GojoRedPreview(Map map) : base(map) { }

        /// <summary>The wall face, cells past the burst point.</summary>
        public static float WallFace => R.ScriptWallBehind - 0.5f;

        public static bool HitsWall(GojoRedScene scene) => scene == GojoRedScene.Wall && R.ReachesWall(R.ThrowCells, WallFace);

        /// <summary>Cells the thrown raider travels: to the wall, the full throw, or nothing at the empty cell.</summary>
        public static float Stop(GojoRedScene scene) =>
            scene == GojoRedScene.Empty ? 0f : scene == GojoRedScene.Wall ? R.WallStop(R.ThrowCells, WallFace) : R.ThrowCells;

        public static float Arrive => R.Arrive(R.Charge, R.ScriptDist, R.Speed);

        /// <summary>Seconds from the burst until the thrown raider stops; 0 at the empty cell.</summary>
        public static float Fly(GojoRedScene scene) => scene == GojoRedScene.Empty ? 0f : R.FlyTime(R.ThrowCells, Stop(scene));

        public static float Duration(GojoRedScene scene) => R.End(Arrive, Fly(scene));

        /// <summary>The sketch's span: the chosen cell is its middle and Gojo stands half of it back.</summary>
        public static float Span(GojoRedScene scene) =>
            R.ScriptDist + (scene == GojoRedScene.Empty ? 1.5f : scene == GojoRedScene.Wall ? R.ScriptWallBehind : R.ThrowCells);

        public void Play(IntVec3 at, GojoRedScene play, float degrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            scene = play;
            aim = degrees;
            seconds = 0f;
            shaken = 0;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            bool thrown = scene != GojoRedScene.Empty, wall = HitsWall(scene);
            float s = seconds, arrive = Arrive, age = s - arrive, stop = Stop(scene), fly = Fly(scene);
            Shake(arrive, fly, wall);

            Vector3 centre = cell.ToVector3Shifted();
            Vector2 toward = VfxDraw.Turn(aim), gojo = new Vector2(centre.x, centre.z) - toward * (Span(scene) / 2f);
            Vector2 burst = GokuTiming.Place(gojo, toward, R.ScriptDist);

            // The preview's script: the thrown raider's path, and the others pushed straight away from the burst.
            R.ScriptBody(age, R.ThrowCells, stop, wall, out float along, out float height, out bool lying);
            float[,] others = scene == GojoRedScene.Wall ? R.WallOthers : scene == GojoRedScene.Open ? R.OpenOthers : R.EmptyOthers;
            int count = 0;
            for (int i = 0; i < others.GetLength(0); i++)
            {
                Vector2 from = GokuTiming.Place(gojo, toward, R.ScriptDist + others[i, 0], others[i, 1]), off = from - burst;
                if (off.magnitude > R.BurstRadius) continue;
                pushed[count++] = new GojoRedPushed { From = from, Now = from + off.normalized * (R.PushCells * R.Pushed(age)) };
            }

            GojoRedGraphics.Draw(new GojoRedShot
            {
                Gojo = gojo, Aim = aim, Dist = R.ScriptDist, Charge = R.Charge, Speed = R.Speed,
                BurstRadius = R.BurstRadius, WashRadius = R.WashRadius, PushCells = R.PushCells,
                Thrown = thrown, Body = burst + toward * along, BodyHeight = height, Lying = thrown && lying,
                StopAge = fly, StopCells = stop, HitsWall = wall, WallFace = WallFace, SlamHeight = R.Lift(fly, R.ThrowCells),
                TouchAge = R.ThrowTime(R.ThrowCells) * 0.5f, TouchCells = R.ThrowCells * 0.75f,
                Pushed = pushed, PushedCount = count,
                Sleeve = GojoGraphics.Uniform, Skin = GojoGraphics.Skin,
            }, s, map);
            if (seconds >= Duration(scene)) active = false;
        }

        // The sketch's events(): the fire, the burst, the slam on a wall.
        private void Shake(float arrive, float fly, bool wall)
        {
            if (shaken == 0 && seconds >= R.Fire(R.Charge))
            {
                shaken = 1;
                Find.CameraDriver.shaker.DoShake(R.FireShake);
            }
            if (shaken == 1 && seconds >= arrive)
            {
                shaken = 2;
                Find.CameraDriver.shaker.DoShake(R.BurstShake);
            }
            if (shaken == 2 && wall && seconds >= arrive + fly)
            {
                shaken = 3;
                Find.CameraDriver.shaker.DoShake(R.SlamShake);
            }
        }
    }
}
