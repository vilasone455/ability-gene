using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt and no pawn is drawn. Each entry plays one sketch's default script
    /// round the chosen cell, which is the sketch's centre: halfway between holder and target for Feed,
    /// half a cell ahead of the holder for Shark Skin, 1.5 cells ahead of the holder's start for Fusion.
    /// The sketches' stand-in pawns and Fusion's water patch are not drawn. Feed and Shark Skin mirror
    /// their swing aiming west, so they have a west entry; Fusion's shark form differs by facing, so it
    /// has all four. The weapon, abilities and their test shortcuts are in Samehada/Kit.
    /// </summary>
    public static class DebugActions_Samehada
    {
        [RimArtDebug("Samehada", "feed")]
        public static void Feed() => Preview().Play(UI.MouseCell(), SamehadaPreview.Feed, 0f);

        [RimArtDebug("Samehada", "feed south")]
        public static void FeedSouth() => Preview().Play(UI.MouseCell(), SamehadaPreview.Feed, 270f);

        [RimArtDebug("Samehada", "feed west")]
        public static void FeedWest() => Preview().Play(UI.MouseCell(), SamehadaPreview.Feed, 180f);

        [RimArtDebug("Samehada", "shark skin")]
        public static void SharkSkin() => Preview().Play(UI.MouseCell(), SamehadaPreview.SharkSkin, 0f);

        [RimArtDebug("Samehada", "shark skin south")]
        public static void SharkSkinSouth() => Preview().Play(UI.MouseCell(), SamehadaPreview.SharkSkin, 270f);

        [RimArtDebug("Samehada", "shark skin west")]
        public static void SharkSkinWest() => Preview().Play(UI.MouseCell(), SamehadaPreview.SharkSkin, 180f);

        [RimArtDebug("Samehada", "fusion east")]
        public static void Fusion() => Preview().Play(UI.MouseCell(), SamehadaPreview.Fusion, 0f);

        [RimArtDebug("Samehada", "fusion north")]
        public static void FusionNorth() => Preview().Play(UI.MouseCell(), SamehadaPreview.Fusion, 90f);

        [RimArtDebug("Samehada", "fusion west")]
        public static void FusionWest() => Preview().Play(UI.MouseCell(), SamehadaPreview.Fusion, 180f);

        [RimArtDebug("Samehada", "fusion south")]
        public static void FusionSouth() => Preview().Play(UI.MouseCell(), SamehadaPreview.Fusion, 270f);

        [RimArtDebug("Samehada", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_SamehadaPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_SamehadaPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_SamehadaPreview>();
    }

    public enum SamehadaPreview { Feed, SharkSkin, Fusion }

    public sealed class MapComponent_SamehadaPreview : MapComponent
    {
        public bool active;
        private SamehadaPreview mode;
        private float seconds, duration, aim;
        private IntVec3 cell;
        /// <summary>The script's camera shakes: when and how hard; each is played once.</summary>
        private readonly List<Vector2> shakes = new List<Vector2>();
        private int shaken;

        public MapComponent_SamehadaPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, SamehadaPreview play, float aimDegrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            aim = aimDegrees;
            seconds = 0f;
            shaken = 0;
            shakes.Clear();
            switch (play)
            {
                case SamehadaPreview.Feed:
                    duration = SamehadaFeedTiming.End;
                    for (int i = 0; i < SamehadaFeedTiming.ScriptHits; i++)
                        shakes.Add(new Vector2(SamehadaFeedTiming.Bite(i), SamehadaFeedTiming.BiteShake));
                    break;
                case SamehadaPreview.SharkSkin:
                    duration = SamehadaSharkSkinTiming.End;
                    shakes.Add(new Vector2(SamehadaSharkSkinTiming.Sweep0 + SamehadaSharkSkinTiming.Sweep * 0.5f, SamehadaSharkSkinTiming.SweepShake));
                    break;
                default:
                    duration = SamehadaFusionTiming.End;
                    shakes.Add(new Vector2(SamehadaFusionTiming.Fused0, SamehadaFusionTiming.FusedShake));
                    break;
            }
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes.Count && seconds >= shakes[shaken].x)
            {
                Find.CameraDriver.shaker.DoShake(shakes[shaken].y);
                shaken++;
            }
            Vector3 centre = cell.ToVector3Shifted();
            switch (mode)
            {
                case SamehadaPreview.Feed:
                    SamehadaFeedGraphics.DrawPreview(centre, aim, seconds, map);
                    break;
                case SamehadaPreview.SharkSkin:
                    SamehadaSharkSkinGraphics.DrawPreview(centre, aim, seconds, map);
                    break;
                default:
                    SamehadaFusionGraphics.DrawPreview(centre, aim, seconds, map);
                    break;
            }
            if (seconds >= duration) active = false;
        }
    }
}
