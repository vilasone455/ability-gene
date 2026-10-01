using System.Collections.Generic;
using UnityEngine;
using Verse;
using T = RimArt.EgoMimicryTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: there is no weapon, no ability, no def and no rule behind any of this; nobody is hurt,
    /// healed or corroded and no pawn is drawn. Each entry plays a Mimicry showcase with the wielder on the
    /// chosen cell (the sketch's origin), on the sketch's clock, so the recorder can compare the port with
    /// Tools/VfxLab/web/sketches/ego-mimicry.js. The aims check the facings: the swing and the arm sit on the
    /// hand side, mirrored aiming west, and aiming north they draw under the pawn.
    /// </summary>
    public static class DebugActions_EgoMimicry
    {
        [RimArtDebug("E.G.O.", "mimicry: swings")]
        public static void Swings() => Play(EgoMimicryMode.Swings, 0f);

        [RimArtDebug("E.G.O.", "mimicry: swings, aim 90")]
        public static void Swings90() => Play(EgoMimicryMode.Swings, 90f);

        [RimArtDebug("E.G.O.", "mimicry: swings, aim 180")]
        public static void Swings180() => Play(EgoMimicryMode.Swings, 180f);

        [RimArtDebug("E.G.O.", "mimicry: swings, aim 270")]
        public static void Swings270() => Play(EgoMimicryMode.Swings, 270f);

        [RimArtDebug("E.G.O.", "mimicry: grown swing")]
        public static void Grown() => Play(EgoMimicryMode.Grown, 0f);

        [RimArtDebug("E.G.O.", "mimicry: grown swing, aim 90")]
        public static void Grown90() => Play(EgoMimicryMode.Grown, 90f);

        [RimArtDebug("E.G.O.", "mimicry: grown swing, aim 180")]
        public static void Grown180() => Play(EgoMimicryMode.Grown, 180f);

        [RimArtDebug("E.G.O.", "mimicry: grown swing, aim 270")]
        public static void Grown270() => Play(EgoMimicryMode.Grown, 270f);

        [RimArtDebug("E.G.O.", "mimicry: corroded")]
        public static void Corroded() => Play(EgoMimicryMode.Corroded, 0f);

        [RimArtDebug("E.G.O.", "mimicry: corroded, aim 200")]
        public static void Corroded200() => Play(EgoMimicryMode.Corroded, 200f);

        [RimArtDebug("E.G.O.", "mimicry: overclock")]
        public static void Overclock() => Play(EgoMimicryMode.Overclock, 0f);

        [RimArtDebug("E.G.O.", "mimicry: overclock, aim 120")]
        public static void Overclock120() => Play(EgoMimicryMode.Overclock, 120f);

        [RimArtDebug("E.G.O.", "mimicry: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_EgoMimicryPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(EgoMimicryMode mode, float aim) =>
            Find.CurrentMap.GetComponent<MapComponent_EgoMimicryPreview>().Play(UI.MouseCell(), mode, aim);
    }

    /// <summary>
    /// Plays a Mimicry showcase on the sketch's clock with the sketch's defaults. The script (who stands where,
    /// who is swung at, the stages and the shot) is <see cref="EgoMimicryScript"/>; this draws it each frame: the
    /// cuts, blood and pools on the scenario's pawns where they stand and rock, the lunges' dust, the grown hits'
    /// marks, the torn flesh, and the wielder (sword, crescent, heal, arm) where the script has it.
    /// </summary>
    public sealed class MapComponent_EgoMimicryPreview : MapComponent
    {
        public bool active;
        private float seconds;
        private IntVec3 cell;
        private EgoMimicryScript script;
        private List<Vector2> shakes;
        private int shaken;

        public MapComponent_EgoMimicryPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, EgoMimicryMode mode, float aim)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            script = new EgoMimicryScript(mode, aim);
            shakes = script.Shakes();
            shaken = 0;
            seconds = 0f;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            float s = seconds;
            if (s >= script.End)
            {
                active = false;
                return;
            }
            // The sketch's events(): a shake per hit, the slam, each lunge and the shot.
            while (shaken < shakes.Count && s >= shakes[shaken].x) Find.CameraDriver.shaker.DoShake(shakes[shaken++].y);

            Vector3 centre = cell.ToVector3Shifted();
            var o = new Vector2(centre.x, centre.z);
            for (int i = 0; i < script.People.Length; i++)
            {
                EgoMimicryPerson q = script.People[i];
                Vector2 at = o + q.Off;
                if (q.DownAt >= 0f && s >= q.DownAt) EgoMimicryStrikeGraphics.Pool(at, s - q.DownAt, map);
                Vector2 pos = at + script.Rock(q, s), chest = new Vector2(pos.x, pos.y + PawnBody.Chest);
                for (int j = 0; j < q.HitTimes.Count; j++)
                    if (!q.HitGrown[j])
                        EgoMimicryStrikeGraphics.Cut(chest, at, q.HitDirs[j], T.Side(q.HitDirs[j]), s - q.HitTimes[j], i * 7 + j, map);
            }
            foreach (EgoMimicryAction a in script.Actions)
            {
                if (a.Lunge)
                {
                    EgoMimicryStrikeGraphics.LungeDust(o + a.From, s - a.T, map);
                    EgoMimicryStrikeGraphics.LungeDust(o + a.To, s - a.T - T.LungeTime, map);
                }
                if (a.Grown)
                {
                    T.SlamFootprint(o + a.From, a.Aim, T.GrownScale, out Vector2 p0, out Vector2 p1);
                    EgoMimicryStrikeGraphics.Slam(p0, p1, T.Dir(a.Aim), s - a.Start - T.SlamAt, EgoMimicryStrikeGraphics.SlamSeed, map);
                }
            }
            foreach (EgoMimicryShot sh in script.Shots)
                EgoMimicryStrikeGraphics.Torn(o + sh.At, o + sh.From, T.HandSide(sh.Aim), s - sh.HitT, map);

            int act = script.ActionAt(s);
            EgoMimicryGraphics.DrawWielder(new EgoMimicryWielder
            {
                Pos = o + script.WielderAt(s),
                Aim = act >= 0 ? script.Actions[act].Aim : script.Aim,
                Move = act < 0 ? EgoMimicryMove.Rest : script.Actions[act].Grown ? EgoMimicryMove.Grown : EgoMimicryMove.Swing,
                MoveAge = act >= 0 ? s - script.Actions[act].Start : -1f,
                Stage = script.Armed ? script.StageAt(s) : 0f,
                Open = script.Open(s),
                FeedAge = script.FeedAge(s),
                Skin = EgoMimicryGraphics.Skin,
            }, s, map);
        }
    }
}
