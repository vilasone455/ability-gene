using UnityEngine;
using Verse;
using T = RimArt.EgoMagicBulletTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: the picture with no rule behind it (the weapon is AG_EgoMagicBullet, Ego/Kit); nobody is hit
    /// and no pawn or wall is drawn. Each entry plays the Magic Bullet picture with the shooter on the chosen cell, on the
    /// sketch's clock, so the recorder can compare the port with ego-magic-bullet.js. The plain entries use the
    /// sketch's stand-ins (a raider 2.5 cells out, three wall cells at 4, a colonist at 6.5; on the seventh's
    /// line a raider at 2.8 and the beloved at 5.5) to place the hits; "empty line" ones have nobody on the
    /// line, to compare with the sketch's stand-ins switched off.
    /// </summary>
    public static class DebugActions_EgoMagicBullet
    {
        [RimArtDebug("E.G.O.", "magic bullet: shot 1")]
        public static void Shot1() => Play(new EgoMagicBulletScene(1, 0f, false, true));

        [RimArtDebug("E.G.O.", "magic bullet: shot 6")]
        public static void Shot6() => Play(new EgoMagicBulletScene(6, 0f, false, true));

        [RimArtDebug("E.G.O.", "magic bullet: the seventh")]
        public static void Seventh() => Play(new EgoMagicBulletScene(T.Shots, 0f, false, true));

        [RimArtDebug("E.G.O.", "magic bullet: corroded")]
        public static void Corroded() => Play(new EgoMagicBulletScene(1, 0f, true, true));

        [RimArtDebug("E.G.O.", "magic bullet: shot 1, empty line")]
        public static void Empty1() => Play(new EgoMagicBulletScene(1, 0f, false, false));

        [RimArtDebug("E.G.O.", "magic bullet: shot 1, empty line, aim 90")]
        public static void Empty1North() => Play(new EgoMagicBulletScene(1, 90f, false, false));

        [RimArtDebug("E.G.O.", "magic bullet: shot 1, empty line, aim 180")]
        public static void Empty1West() => Play(new EgoMagicBulletScene(1, 180f, false, false));

        [RimArtDebug("E.G.O.", "magic bullet: shot 1, empty line, aim 270")]
        public static void Empty1South() => Play(new EgoMagicBulletScene(1, 270f, false, false));

        [RimArtDebug("E.G.O.", "magic bullet: shot 4, empty line")]
        public static void Empty4() => Play(new EgoMagicBulletScene(4, 0f, false, false));

        [RimArtDebug("E.G.O.", "magic bullet: shot 6, empty line")]
        public static void Empty6() => Play(new EgoMagicBulletScene(6, 0f, false, false));

        [RimArtDebug("E.G.O.", "magic bullet: the seventh, empty line")]
        public static void EmptySeventh() => Play(new EgoMagicBulletScene(T.Shots, 0f, false, false));

        [RimArtDebug("E.G.O.", "magic bullet: corroded, empty line")]
        public static void EmptyCorroded() => Play(new EgoMagicBulletScene(1, 0f, true, false));

        [RimArtDebug("E.G.O.", "magic bullet: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_EgoMagicBulletPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(EgoMagicBulletScene scene) =>
            Find.CurrentMap.GetComponent<MapComponent_EgoMagicBulletPreview>().Play(UI.MouseCell(), scene);
    }

    /// <summary>One preview: the shot number (7 is the seventh), the player's aim in degrees, corroded, and whether the sketch's stand-ins stand on the line.</summary>
    public readonly struct EgoMagicBulletScene
    {
        public readonly int Shot;
        public readonly float Aim;
        public readonly bool Corroded, Pawns;

        public EgoMagicBulletScene(int shot, float aim, bool corroded, bool pawns)
        {
            Shot = shot;
            Aim = aim;
            Corroded = corroded;
            Pawns = pawns;
        }
    }

    /// <summary>
    /// Plays one Magic Bullet shot on the sketch's clock with the sketch's defaults. The preview's script (the
    /// stand-ins' places, the corroded gun turning to the nearest of them, what the line crosses, the shooter
    /// rocking back) is in <see cref="EgoMagicBulletTiming"/> and here.
    /// </summary>
    public sealed class MapComponent_EgoMagicBulletPreview : MapComponent
    {
        public bool active;
        private float seconds;
        private EgoMagicBulletScene scene;
        private IntVec3 cell;
        private bool shaken;
        private float aim, seventhAim;
        private int hitCount;
        private readonly Vector2[] pawns = new Vector2[4], walls = new Vector2[2 * T.WallHalfWidth + 1];
        private readonly EgoMagicBulletHit[] hits = new EgoMagicBulletHit[4 + 2 * T.WallHalfWidth + 1];

        public MapComponent_EgoMagicBulletPreview(Map map) : base(map) { }

        public static float Duration(EgoMagicBulletScene scene) => T.End(T.Lead, scene.Shot >= T.Shots, T.Range);

        public void Play(IntVec3 at, EgoMagicBulletScene play)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            scene = play;
            seconds = 0f;
            shaken = false;
            Vector3 c = at.ToVector3Shifted();
            var o = new Vector2(c.x, c.z);
            bool seventh = play.Shot >= T.Shots;
            int pawnCount = play.Pawns ? pawns.Length : 0, wallCount = play.Pawns ? walls.Length : 0;
            T.ScriptPawns(o, play.Aim, play.Corroded, pawns);
            T.ScriptWalls(o, play.Aim, walls);
            // Corroded, the gun turns itself to the nearest pawn; the seventh still goes to the beloved, turning from the player's aim.
            aim = play.Corroded && !seventh ? T.ScriptNearestAim(o, play.Aim, pawns, pawnCount) : play.Aim;
            seventhAim = T.ScriptSeventhAim(play.Aim);
            Vector2 d = VfxDraw.Turn(seventh ? seventhAim : aim);
            hitCount = T.ScriptHits(o + d * T.MuzzleAlong, d, walls, wallCount, pawns, pawnCount, hits);
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            bool seventh = scene.Shot >= T.Shots;
            float fire = T.Fire(T.Lead, seventh);
            // The sketch's events(): one shake on the shot. Its sound markers name defs that do not exist yet.
            if (!shaken && seconds >= fire)
            {
                shaken = true;
                Find.CameraDriver.shaker.DoShake(seventh ? T.SeventhShake : T.FireShake);
            }
            Vector3 c = cell.ToVector3Shifted();
            var o = new Vector2(c.x, c.z);
            Vector2 line = VfxDraw.Turn(seventh ? seventhAim : aim);
            EgoMagicBulletGraphics.Draw(new EgoMagicBulletShot
            {
                Shooter = o, Stand = o - line * T.Rock(seconds - fire), Aim = aim, SeventhAim = seventhAim, Shot = scene.Shot,
                Lead = T.Lead, Range = T.Range, Corroded = scene.Corroded, Hits = hits, HitCount = hitCount,
            }, seconds, map);
            if (seconds >= Duration(scene)) active = false;
        }
    }
}
