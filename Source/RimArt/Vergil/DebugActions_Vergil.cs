using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only. There is no weapon, no ability and no def behind any of this: nobody is cut and
    /// no pawn is drawn. Each entry plays one effect's drawing round the chosen cell, which is the cell
    /// the lab's sketch centres on — for Judgement Cut that is the caster, with the target the sketch's
    /// 8 cells away along the aim; for Summoned Swords it is where the carrier starts, with the blades'
    /// targets at the sketch's fixed points round it; for Yamato Dash it is the middle of the 6-cell path.
    /// </summary>
    public static class DebugActions_Vergil
    {
        [RimArtDebug("Vergil", "judgement cut")]
        public static void JudgementCut() => Play(VergilPreview.JudgementCut, JudgementCutTiming.Aim);

        [RimArtDebug("Vergil", "judgement cut north")]
        public static void JudgementCutNorth() => Play(VergilPreview.JudgementCut, 90f);

        [RimArtDebug("Vergil", "judgement cut west")]
        public static void JudgementCutWest() => Play(VergilPreview.JudgementCut, 180f);

        [RimArtDebug("Vergil", "judgement cut south")]
        public static void JudgementCutSouth() => Play(VergilPreview.JudgementCut, 270f);

        [RimArtDebug("Vergil", "summoned swords")]
        public static void SummonedSwords() => Play(VergilPreview.SummonedSwords, 0f);

        [RimArtDebug("Vergil", "summoned swords standing")]
        public static void SummonedSwordsStanding() => Play(VergilPreview.SummonedSwordsStanding, 0f);

        [RimArtDebug("Vergil", "summoned swords spin")]
        public static void SummonedSwordsSpin() => Play(VergilPreview.SummonedSwordsSpin, 0f);

        [RimArtDebug("Vergil", "yamato dash")]
        public static void YamatoDash() => Play(VergilPreview.YamatoDash, YamatoDashTiming.Aim);

        [RimArtDebug("Vergil", "yamato dash north")]
        public static void YamatoDashNorth() => Play(VergilPreview.YamatoDash, 90f);

        [RimArtDebug("Vergil", "yamato dash west")]
        public static void YamatoDashWest() => Play(VergilPreview.YamatoDash, 180f);

        [RimArtDebug("Vergil", "yamato dash south")]
        public static void YamatoDashSouth() => Play(VergilPreview.YamatoDash, 270f);

        [RimArtDebug("Vergil", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_VergilPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(VergilPreview play, float aimDegrees) =>
            Find.CurrentMap.GetComponent<MapComponent_VergilPreview>().Play(UI.MouseCell(), play, aimDegrees);
    }

    public enum VergilPreview
    {
        JudgementCut, SummonedSwords, SummonedSwordsStanding, SummonedSwordsSpin, YamatoDash,
    }

    public sealed class MapComponent_VergilPreview : MapComponent
    {
        public bool active;
        private VergilPreview mode;
        private float seconds, duration, aimDegrees;
        private int shaken;
        private IntVec3 cell;

        public MapComponent_VergilPreview(Map map) : base(map) { }

        public static float Duration(VergilPreview play) =>
            play == VergilPreview.JudgementCut ? JudgementCutTiming.Duration(JudgementCutTiming.Warm, JudgementCutTiming.Burst)
            : play == VergilPreview.YamatoDash ? YamatoDashTiming.Duration
            : SummonedSwordsTiming.Duration;

        private static SwordsScene Scene(VergilPreview play) =>
            play == VergilPreview.SummonedSwordsStanding ? SwordsScene.Stands
            : play == VergilPreview.SummonedSwordsSpin ? SwordsScene.Spins
            : SwordsScene.WalksEast;

        /// <summary>The camera shakes of each preview, from its sketch's events(), in time order: (when, how hard).</summary>
        private static readonly (float at, float value)[] CutShakes =
        {
            (JudgementCutTiming.OpenAt(JudgementCutTiming.Warm), 0.06f),
            (JudgementCutTiming.CloseAt(JudgementCutTiming.Warm, JudgementCutTiming.Burst), 0.08f),
        };
        private static readonly (float at, float value)[] SwordShakes = { (SummonedSwordsTiming.StopAt, 0.03f) };
        private static readonly (float at, float value)[] DashShakes = { (YamatoDashTiming.ArriveAt, 0.02f), (YamatoDashTiming.ClickAt, 0.06f) };

        public void Play(IntVec3 at, VergilPreview play, float aim)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            aimDegrees = aim;
            seconds = 0f;
            duration = Duration(play);
            shaken = 0;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;

            var shakes = mode == VergilPreview.JudgementCut ? CutShakes : mode == VergilPreview.YamatoDash ? DashShakes : SwordShakes;
            while (shaken < shakes.Length && seconds >= shakes[shaken].at) Find.CameraDriver.shaker.DoShake(shakes[shaken++].value);

            Vector3 centre = cell.ToVector3Shifted();
            if (mode == VergilPreview.JudgementCut) JudgementCutGraphics.DrawPreview(centre, aimDegrees, seconds, map);
            else if (mode == VergilPreview.YamatoDash) YamatoDashGraphics.DrawPreview(centre, aimDegrees, seconds, map);
            else SummonedSwordsGraphics.DrawPreview(centre, Scene(mode), seconds, map);
            if (seconds >= duration) active = false;
        }
    }
}
