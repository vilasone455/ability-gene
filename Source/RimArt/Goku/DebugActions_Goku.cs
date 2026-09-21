using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only. There is no gene, no ability and no def behind any of this: nobody is stunned,
    /// moved or hurt and no pawn is drawn. Each entry plays one effect's drawing round the chosen
    /// cell, which is the cell the lab's sketch centres on, aimed east unless it says otherwise. The
    /// pawns the effects are drawn on stand where the sketches' scripts put them.
    /// </summary>
    public static class DebugActions_Goku
    {
        private static readonly Vector2 NorthWest = new Vector2(Mathf.Cos(140f * Mathf.Deg2Rad), Mathf.Sin(140f * Mathf.Deg2Rad));

        [RimArtDebug("Goku", "solar flare")]
        public static void SolarFlare() => Play(GokuPreview.SolarFlare, Vector2.right);

        [RimArtDebug("Goku", "solar flare no wall")]
        public static void SolarFlareNoWall() => Play(GokuPreview.SolarFlareNoWall, Vector2.right);

        [RimArtDebug("Goku", "instant transmission kidnap")]
        public static void TransmissionKidnap() => Play(GokuPreview.TransmissionKidnap, Vector2.right);

        [RimArtDebug("Goku", "instant transmission kidnap north-west")]
        public static void TransmissionKidnapNorthWest() => Play(GokuPreview.TransmissionKidnap, NorthWest);

        [RimArtDebug("Goku", "instant transmission alone")]
        public static void TransmissionAlone() => Play(GokuPreview.TransmissionAlone, Vector2.right);

        [RimArtDebug("Goku", "instant transmission rescue")]
        public static void TransmissionRescue() => Play(GokuPreview.TransmissionRescue, Vector2.right);

        [RimArtDebug("Goku", "kamehameha")]
        public static void Kamehameha() => Play(GokuPreview.Kamehameha, Vector2.right);

        [RimArtDebug("Goku", "kamehameha north-west")]
        public static void KamehamehaNorthWest() => Play(GokuPreview.Kamehameha, NorthWest);

        [RimArtDebug("Goku", "kamehameha wall")]
        public static void KamehamehaWall() => Play(GokuPreview.KamehamehaWall, Vector2.right);

        [RimArtDebug("Goku", "kamehameha warp")]
        public static void KamehamehaWarp() => Play(GokuPreview.KamehamehaWarp, Vector2.right);

        [RimArtDebug("Goku", "kamehameha warp north-west")]
        public static void KamehamehaWarpNorthWest() => Play(GokuPreview.KamehamehaWarp, NorthWest);

        [RimArtDebug("Goku", "spirit bomb")]
        public static void SpiritBomb() => Play(GokuPreview.SpiritBomb, Vector2.right);

        [RimArtDebug("Goku", "spirit bomb north-west")]
        public static void SpiritBombNorthWest() => Play(GokuPreview.SpiritBomb, NorthWest);

        [RimArtDebug("Goku", "spirit bomb alone")]
        public static void SpiritBombAlone() => Play(GokuPreview.SpiritBombAlone, Vector2.right);

        [RimArtDebug("Goku", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_GokuPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(GokuPreview play, Vector2 toward) =>
            Find.CurrentMap.GetComponent<MapComponent_GokuPreview>().Play(UI.MouseCell(), play, toward);
    }

    public enum GokuPreview
    {
        SolarFlare, SolarFlareNoWall, TransmissionKidnap, TransmissionAlone, TransmissionRescue, Kamehameha, KamehamehaWall, KamehamehaWarp, SpiritBomb, SpiritBombAlone,
    }

    public sealed class MapComponent_GokuPreview : MapComponent
    {
        public bool active;
        private GokuPreview mode;
        private float seconds, duration;
        // The camera shakes of the effect being played, in time order, and how many have been done.
        private List<GokuShake> shakes = new List<GokuShake>();
        private int shaken;
        private IntVec3 cell;
        private Vector2 toward;

        public MapComponent_GokuPreview(Map map) : base(map) { }

        public static float Duration(GokuPreview play)
        {
            switch (play)
            {
                case GokuPreview.SolarFlare:
                case GokuPreview.SolarFlareNoWall: return GokuSolarFlareTiming.Plan().End;
                case GokuPreview.TransmissionKidnap:
                case GokuPreview.TransmissionAlone:
                case GokuPreview.TransmissionRescue: return GokuInstantTransmissionTiming.Plan().End;
                case GokuPreview.Kamehameha:
                case GokuPreview.KamehamehaWall: return GokuKamehamehaTiming.Plan(false).End;
                case GokuPreview.KamehamehaWarp: return GokuKamehamehaTiming.Plan(true).End;
                case GokuPreview.SpiritBomb: return GokuSpiritBombTiming.Plan(GokuSpiritBombTiming.ScriptLenders).End;
                default: return GokuSpiritBombTiming.Plan(0).End;
            }
        }

        /// <summary>The camera shakes of each preview, from its sketch's events(). Solar Flare and Instant Transmission hit nothing and have none.</summary>
        public static List<GokuShake> Shakes(GokuPreview play)
        {
            switch (play)
            {
                case GokuPreview.Kamehameha:
                case GokuPreview.KamehamehaWall: return GokuKamehamehaTiming.Shakes(GokuKamehamehaTiming.Plan(false));
                case GokuPreview.KamehamehaWarp: return GokuKamehamehaTiming.Shakes(GokuKamehamehaTiming.Plan(true));
                case GokuPreview.SpiritBomb: return GokuSpiritBombTiming.Shakes(GokuSpiritBombTiming.Plan(GokuSpiritBombTiming.ScriptLenders));
                case GokuPreview.SpiritBombAlone: return GokuSpiritBombTiming.Shakes(GokuSpiritBombTiming.Plan(0));
                default: return new List<GokuShake>();
            }
        }

        public void Play(IntVec3 at, GokuPreview play, Vector2 facing)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            toward = facing;
            seconds = 0f;
            duration = Duration(play);
            shakes = GokuTiming.Sorted(Shakes(play));
            shaken = 0;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes.Count && seconds >= shakes[shaken].At) Find.CameraDriver.shaker.DoShake(shakes[shaken++].Size);
            Vector3 centre = cell.ToVector3Shifted();
            switch (mode)
            {
                case GokuPreview.SolarFlare: GokuSolarFlareGraphics.DrawPreview(centre, true, seconds, map); break;
                case GokuPreview.SolarFlareNoWall: GokuSolarFlareGraphics.DrawPreview(centre, false, seconds, map); break;
                case GokuPreview.TransmissionKidnap: GokuInstantTransmissionGraphics.DrawPreview(centre, toward, TransmissionScene.Kidnap, seconds, map); break;
                case GokuPreview.TransmissionAlone: GokuInstantTransmissionGraphics.DrawPreview(centre, toward, TransmissionScene.Alone, seconds, map); break;
                case GokuPreview.TransmissionRescue: GokuInstantTransmissionGraphics.DrawPreview(centre, toward, TransmissionScene.Rescue, seconds, map); break;
                case GokuPreview.Kamehameha: GokuKamehamehaGraphics.DrawPreview(centre, toward, false, false, seconds, map); break;
                case GokuPreview.KamehamehaWall: GokuKamehamehaGraphics.DrawPreview(centre, toward, false, true, seconds, map); break;
                case GokuPreview.KamehamehaWarp: GokuKamehamehaGraphics.DrawPreview(centre, toward, true, false, seconds, map); break;
                case GokuPreview.SpiritBomb: GokuSpiritBombGraphics.DrawPreview(centre, toward, GokuSpiritBombTiming.ScriptLenders, seconds, map); break;
                default: GokuSpiritBombGraphics.DrawPreview(centre, toward, 0, seconds, map); break;
            }
            if (seconds >= duration) active = false;
        }
    }
}
