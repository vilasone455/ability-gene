using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only. There is no gene, no ability and no def behind any of this: nobody is held, no
    /// item moves and no pawn is drawn. Each entry plays one effect's drawing round the chosen cell,
    /// which is the cell the lab's sketch centres on, aimed east unless it says otherwise. The light
    /// levels are the sketches' scripted ones (daylight, or night with one campfire), not the map's.
    /// </summary>
    public static class DebugActions_ShadowPlexus
    {
        private static readonly Vector2 NorthWest = new Vector2(Mathf.Cos(140f * Mathf.Deg2Rad), Mathf.Sin(140f * Mathf.Deg2Rad));

        [RimArtDebug("Shadow Plexus", "imitation")]
        public static void Imitation() => Play(ShadowPlexusPreview.Imitation, Vector2.right);

        [RimArtDebug("Shadow Plexus", "imitation north-west")]
        public static void ImitationNorthWest() => Play(ShadowPlexusPreview.Imitation, NorthWest);

        [RimArtDebug("Shadow Plexus", "imitation cut")]
        public static void ImitationCut() => Play(ShadowPlexusPreview.ImitationCut, Vector2.right);

        [RimArtDebug("Shadow Plexus", "imitation dark")]
        public static void ImitationDark() => Play(ShadowPlexusPreview.ImitationDark, Vector2.right);

        [RimArtDebug("Shadow Plexus", "seam rusher")]
        public static void SeamRusher() => Play(ShadowPlexusPreview.SeamRusher, Vector2.right);

        [RimArtDebug("Shadow Plexus", "seam rescue")]
        public static void SeamRescue() => Play(ShadowPlexusPreview.SeamRescue, Vector2.right);

        [RimArtDebug("Shadow Plexus", "grasp")]
        public static void Grasp() => Play(ShadowPlexusPreview.Grasp, Vector2.right);

        [RimArtDebug("Shadow Plexus", "grasp north-west")]
        public static void GraspNorthWest() => Play(ShadowPlexusPreview.Grasp, NorthWest);

        [RimArtDebug("Shadow Plexus", "grasp blocked")]
        public static void GraspBlocked() => Play(ShadowPlexusPreview.GraspBlocked, Vector2.right);

        [RimArtDebug("Shadow Plexus", "grasp rescue")]
        public static void GraspRescue() => Play(ShadowPlexusPreview.GraspRescue, Vector2.right);

        [RimArtDebug("Shadow Plexus", "double")]
        public static void Double() => Play(ShadowPlexusPreview.Double, Vector2.right);

        [RimArtDebug("Shadow Plexus", "double fire out")]
        public static void DoubleFireOut() => Play(ShadowPlexusPreview.DoubleFireOut, Vector2.right);

        [RimArtDebug("Shadow Plexus", "neck bind")]
        public static void NeckBind() => Play(ShadowPlexusPreview.NeckBind, Vector2.right);

        [RimArtDebug("Shadow Plexus", "neck bind cut")]
        public static void NeckBindCut() => Play(ShadowPlexusPreview.NeckBindCut, Vector2.right);

        [RimArtDebug("Shadow Plexus", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_ShadowPlexusPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(ShadowPlexusPreview play, Vector2 toward) =>
            Find.CurrentMap.GetComponent<MapComponent_ShadowPlexusPreview>().Play(UI.MouseCell(), play, toward);
    }

    public enum ShadowPlexusPreview
    {
        Imitation, ImitationCut, ImitationDark, SeamRusher, SeamRescue, Grasp, GraspBlocked, GraspRescue, Double, DoubleFireOut, NeckBind, NeckBindCut,
    }

    public sealed class MapComponent_ShadowPlexusPreview : MapComponent
    {
        public bool active;
        private ShadowPlexusPreview mode;
        private float seconds, duration, shakeAt;
        private bool shaken;
        private IntVec3 cell;
        private Vector2 toward;

        public MapComponent_ShadowPlexusPreview(Map map) : base(map) { }

        public static float Duration(ShadowPlexusPreview play)
        {
            switch (play)
            {
                case ShadowPlexusPreview.Imitation: return ShadowImitationTiming.Plan(ImitationEnd.Released).Duration;
                case ShadowPlexusPreview.ImitationCut: return ShadowImitationTiming.Plan(ImitationEnd.Cut).Duration;
                case ShadowPlexusPreview.ImitationDark: return ShadowImitationTiming.Plan(ImitationEnd.Dark).Duration;
                case ShadowPlexusPreview.SeamRusher: return ShadowSeamTiming.Plan(SeamScene.Rusher).Duration;
                case ShadowPlexusPreview.SeamRescue: return ShadowSeamTiming.Plan(SeamScene.Rescue).Duration;
                case ShadowPlexusPreview.Grasp: return ShadowGraspTiming.Duration(GraspScene.Grenade);
                case ShadowPlexusPreview.GraspBlocked: return ShadowGraspTiming.Duration(GraspScene.Blocked);
                case ShadowPlexusPreview.GraspRescue: return ShadowGraspTiming.Duration(GraspScene.Rescue);
                case ShadowPlexusPreview.Double: return ShadowDoubleTiming.Plan(DoubleEnd.TimeRunsOut).Duration;
                case ShadowPlexusPreview.DoubleFireOut: return ShadowDoubleTiming.Plan(DoubleEnd.FireGoesOut).Duration;
                case ShadowPlexusPreview.NeckBind: return ShadowNeckBindTiming.Duration(false);
                default: return ShadowNeckBindTiming.Duration(true);
            }
        }

        public void Play(IntVec3 at, ShadowPlexusPreview play, Vector2 facing)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            toward = facing;
            seconds = 0f;
            duration = Duration(play);
            // The seam's rusher is yanked back at 4 cells: the only camera shake in the kit.
            shaken = play != ShadowPlexusPreview.SeamRusher;
            shakeAt = shaken ? 0f : ShadowSeamTiming.Plan(SeamScene.Rusher).Taut;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            if (!shaken && seconds >= shakeAt)
            {
                shaken = true;
                Find.CameraDriver.shaker.DoShake(ShadowSeamTiming.TautShake);
            }
            Vector3 centre = cell.ToVector3Shifted();
            switch (mode)
            {
                case ShadowPlexusPreview.Imitation: ShadowImitationGraphics.DrawPreview(centre, toward, ImitationEnd.Released, seconds, map); break;
                case ShadowPlexusPreview.ImitationCut: ShadowImitationGraphics.DrawPreview(centre, toward, ImitationEnd.Cut, seconds, map); break;
                case ShadowPlexusPreview.ImitationDark: ShadowImitationGraphics.DrawPreview(centre, toward, ImitationEnd.Dark, seconds, map); break;
                case ShadowPlexusPreview.SeamRusher: ShadowSeamGraphics.DrawPreview(centre, toward, SeamScene.Rusher, seconds, map); break;
                case ShadowPlexusPreview.SeamRescue: ShadowSeamGraphics.DrawPreview(centre, toward, SeamScene.Rescue, seconds, map); break;
                case ShadowPlexusPreview.Grasp: ShadowGraspGraphics.DrawPreview(centre, toward, GraspScene.Grenade, seconds, map); break;
                case ShadowPlexusPreview.GraspBlocked: ShadowGraspGraphics.DrawPreview(centre, toward, GraspScene.Blocked, seconds, map); break;
                case ShadowPlexusPreview.GraspRescue: ShadowGraspGraphics.DrawPreview(centre, toward, GraspScene.Rescue, seconds, map); break;
                case ShadowPlexusPreview.Double: ShadowDoubleGraphics.DrawPreview(centre, toward, DoubleEnd.TimeRunsOut, seconds, map); break;
                case ShadowPlexusPreview.DoubleFireOut: ShadowDoubleGraphics.DrawPreview(centre, toward, DoubleEnd.FireGoesOut, seconds, map); break;
                case ShadowPlexusPreview.NeckBind: ShadowNeckBindGraphics.DrawPreview(centre, toward, false, seconds, map); break;
                default: ShadowNeckBindGraphics.DrawPreview(centre, toward, true, seconds, map); break;
            }
            if (seconds >= duration) active = false;
        }
    }
}
