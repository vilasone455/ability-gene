using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A showcase only. There is no ability, no hediff and no def behind any of this: the orbs
    /// exist to be looked at while the silhouettes and the timing are settled.
    /// </summary>
    public static class DebugActions_SixPaths
    {
        [RimArtDebug("Six Paths", "orb showcase")]
        public static void Showcase() => Preview().Play(UI.MouseCell(), PreviewMode.Ring, 1f, false);

        [RimArtDebug("Six Paths", "slow motion")]
        public static void SlowMotion() => Preview().Play(UI.MouseCell(), PreviewMode.Ring, 0.25f, false);

        [RimArtDebug("Six Paths", "frozen mid-change")]
        public static void Frozen() => Preview().Play(UI.MouseCell(), PreviewMode.Ring, 0f, true);

        [RimArtDebug("Six Paths", "shape sheet")]
        public static void Sheet() => Preview().Play(UI.MouseCell(), PreviewMode.Sheet, 0f, false);

        [RimArtDebug("Six Paths", "slam")]
        public static void Slam() => Preview().Play(UI.MouseCell(), PreviewMode.Slam, 1f, false);

        [RimArtDebug("Six Paths", "slam slow motion")]
        public static void SlamSlow() => Preview().Play(UI.MouseCell(), PreviewMode.Slam, 0.2f, false);

        [RimArtDebug("Six Paths", "slam frozen mid-fall")]
        public static void SlamFrozen() => Preview().Play(UI.MouseCell(), PreviewMode.Slam, 0f, true);

        [RimArtDebug("Six Paths", "bloom")]
        public static void Bloom() => Preview().Play(UI.MouseCell(), PreviewMode.Bloom, 1f, false);

        [RimArtDebug("Six Paths", "twin maw")]
        public static void TwinMaw() => Preview().Play(UI.MouseCell(), PreviewMode.TwinMaw, 1f, false);

        [RimArtDebug("Six Paths", "rods east")]
        public static void RodsEast() => Preview().Play(UI.MouseCell(), PreviewMode.Rods, 1f, false, Vector2.right);

        [RimArtDebug("Six Paths", "rods west")]
        public static void RodsWest() => Preview().Play(UI.MouseCell(), PreviewMode.Rods, 1f, false, Vector2.left);

        [RimArtDebug("Six Paths", "rods north")]
        public static void RodsNorth() => Preview().Play(UI.MouseCell(), PreviewMode.Rods, 1f, false, Vector2.up);

        [RimArtDebug("Six Paths", "rods south")]
        public static void RodsSouth() => Preview().Play(UI.MouseCell(), PreviewMode.Rods, 1f, false, Vector2.down);

        [RimArtDebug("Six Paths", "serpent east")]
        public static void SerpentEast() => Preview().Play(UI.MouseCell(), PreviewMode.Serpent, 1f, false, Vector2.right);

        [RimArtDebug("Six Paths", "serpent south-west")]
        public static void SerpentSouthWest() => Preview().Play(UI.MouseCell(), PreviewMode.Serpent, 1f, false,
            new Vector2(Mathf.Cos(215f * Mathf.Deg2Rad), Mathf.Sin(215f * Mathf.Deg2Rad)));

        [RimArtDebug("Six Paths", "repulse step east")]
        public static void RepulseEast() => Preview().Play(UI.MouseCell(), PreviewMode.Repulse, 1f, false, Vector2.right);

        [RimArtDebug("Six Paths", "repulse step west")]
        public static void RepulseWest() => Preview().Play(UI.MouseCell(), PreviewMode.Repulse, 1f, false, Vector2.left);

        [RimArtDebug("Six Paths", "repulse step north")]
        public static void RepulseNorth() => Preview().Play(UI.MouseCell(), PreviewMode.Repulse, 1f, false, Vector2.up);

        [RimArtDebug("Six Paths", "repulse step south")]
        public static void RepulseSouth() => Preview().Play(UI.MouseCell(), PreviewMode.Repulse, 1f, false, Vector2.down);

        [RimArtDebug("Six Paths", "umbrella canopy")]
        public static void UmbrellaCanopy() => Preview().Play(UI.MouseCell(), PreviewMode.UmbrellaCanopy, 1f, false, Vector2.right);

        [RimArtDebug("Six Paths", "umbrella guard east")]
        public static void UmbrellaGuardEast() => Preview().Play(UI.MouseCell(), PreviewMode.UmbrellaGuard, 1f, false, Vector2.right);

        [RimArtDebug("Six Paths", "umbrella guard west")]
        public static void UmbrellaGuardWest() => Preview().Play(UI.MouseCell(), PreviewMode.UmbrellaGuard, 1f, false, Vector2.left);

        [RimArtDebug("Six Paths", "umbrella guard north")]
        public static void UmbrellaGuardNorth() => Preview().Play(UI.MouseCell(), PreviewMode.UmbrellaGuard, 1f, false, Vector2.up);

        [RimArtDebug("Six Paths", "umbrella guard south")]
        public static void UmbrellaGuardSouth() => Preview().Play(UI.MouseCell(), PreviewMode.UmbrellaGuard, 1f, false, Vector2.down);

        [RimArtDebug("Six Paths", "clear showcase", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_SixPathsPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_SixPathsPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_SixPathsPreview>();
    }

    public enum PreviewMode { Ring, Sheet, Slam, Bloom, TwinMaw, Rods, Serpent, Repulse, UmbrellaCanopy, UmbrellaGuard }

    public sealed class MapComponent_SixPathsPreview : MapComponent
    {
        /// <summary>Cells west of the chosen cell that the bloom's sage stands. No pawn is drawn for either.</summary>
        private const float BloomSageDistance = 4.5f;
        /// <summary>Cells west of the trap tile that the maw's sage stands.</summary>
        private const float MawSageDistance = 5f;
        /// <summary>Cells back along the cast direction from the middle of the wall that the rods' sage stands.</summary>
        private const float RodsSageDistance = 5f;
        /// <summary>Cells back along the cast direction from the target that the serpent's sage stands: 4.5 to the orb's spot and 0.9 behind it.</summary>
        private const float SerpentSageDistance = 4.5f + SixPathsSerpentTiming.Behind;

        public bool active;
        private PreviewMode mode;
        private bool frozen, shaken;
        private float speed, seconds;
        private IntVec3 cell;
        /// <summary>The cardinal the umbrella's sage faces or the rods or the serpent are cast along or the repulse step launches, east and north positive.</summary>
        private Vector2 toward;

        public MapComponent_SixPathsPreview(Map map) : base(map) { }

        public void Play(IntVec3 target, PreviewMode play, float rate, bool freeze, Vector2 facing = default)
        {
            if (!target.InBounds(map) || target.Fogged(map)) return;
            cell = target;
            toward = facing;
            mode = play;
            speed = rate;
            frozen = freeze;
            shaken = false;
            // Frozen starts at the frame worth inspecting. For the ring that is halfway between
            // two forms, with the rim at its brightest; for the slam it is halfway down, where the
            // block is clear of the ground and its shadows and its faces can all be judged.
            seconds = !freeze ? 0f
                : play == PreviewMode.Slam ? SixPathsSlamTiming.FallAt + SixPathsSlamTiming.Fall * 0.5f
                : SixPathsTiming.HoldSeconds + SixPathsTiming.MorphSeconds * 0.5f;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the showcase runs at the same rate whether the game is paused or at 3x,
            // which is the only way to judge a 0.26 s change.
            if (!frozen) seconds += Time.unscaledDeltaTime * speed;

            switch (mode)
            {
                case PreviewMode.Sheet:
                    SixPathsGraphics.DrawSheet(cell.ToVector3Shifted(), map);
                    break;
                case PreviewMode.Slam:
                    if (!frozen && seconds >= SixPathsSlamTiming.LandAt && !shaken)
                    {
                        shaken = true;
                        Find.CameraDriver.shaker.DoShake(SixPathsSlamTiming.Shake);
                    }
                    SixPathsSlamGraphics.Draw(cell.ToVector3Shifted(), seconds, map);
                    // The slam is one event rather than a loop, so it puts itself away.
                    if (!frozen && seconds > SixPathsSlamTiming.Duration) active = false;
                    break;
                case PreviewMode.Bloom:
                    SixPathsBloomGraphics.Draw(cell.ToVector3Shifted(),
                        cell.ToVector3Shifted() - new Vector3(BloomSageDistance, 0f, 0f), seconds, map);
                    if (seconds >= SixPathsBloomTiming.Duration) active = false;
                    break;
                case PreviewMode.TwinMaw:
                    if (seconds >= SixPathsTwinMawTiming.ShutAt && !shaken)
                    {
                        shaken = true;
                        Find.CameraDriver.shaker.DoShake(SixPathsTwinMawTiming.Shake);
                    }
                    SixPathsTwinMawGraphics.Draw(cell.ToVector3Shifted(),
                        cell.ToVector3Shifted() - new Vector3(MawSageDistance, 0f, 0f), seconds, map);
                    if (seconds >= SixPathsTwinMawTiming.Duration) active = false;
                    break;
                case PreviewMode.Rods:
                    if (seconds >= SixPathsRodsTiming.RiseAt && !shaken)
                    {
                        shaken = true;
                        Find.CameraDriver.shaker.DoShake(SixPathsRodsTiming.Shake);
                    }
                    SixPathsRodsGraphics.Draw(cell.ToVector3Shifted(),
                        cell.ToVector3Shifted() - new Vector3(toward.x, 0f, toward.y) * RodsSageDistance, toward, seconds, map);
                    if (seconds >= SixPathsRodsTiming.Duration) active = false;
                    break;
                case PreviewMode.Serpent:
                    SixPathsSerpentGraphics.Draw(cell.ToVector3Shifted(),
                        cell.ToVector3Shifted() - new Vector3(toward.x, 0f, toward.y) * SerpentSageDistance, seconds, map);
                    if (seconds >= SixPathsSerpentTiming.Duration) active = false;
                    break;
                case PreviewMode.Repulse:
                    if (seconds >= SixPathsRepulseTiming.LaunchAt && !shaken)
                    {
                        shaken = true;
                        Find.CameraDriver.shaker.DoShake(SixPathsRepulseTiming.Shake);
                    }
                    // No pawn is launched here: the timing class's script says where the sage, and so its ring, is.
                    SixPathsRepulseGraphics.Draw(cell.ToVector3Shifted(),
                        cell.ToVector3Shifted() + new Vector3(toward.x, 0f, toward.y) * SixPathsRepulseTiming.Travel(seconds),
                        toward, seconds, map);
                    if (seconds >= SixPathsRepulseTiming.Duration) active = false;
                    break;
                case PreviewMode.UmbrellaCanopy:
                case PreviewMode.UmbrellaGuard:
                    // The sage starts behind the chosen cell and walks through it while the umbrella is open.
                    float ahead = SixPathsUmbrellaTiming.Walked(seconds) - SixPathsUmbrellaTiming.StartBack;
                    SixPathsUmbrellaGraphics.Draw(cell.ToVector3Shifted() + new Vector3(toward.x, 0f, toward.y) * ahead, toward,
                        mode == PreviewMode.UmbrellaCanopy ? UmbrellaMode.Canopy : UmbrellaMode.Guard, seconds, map);
                    if (seconds >= SixPathsUmbrellaTiming.Duration) active = false;
                    break;
                default:
                    SixPathsGraphics.DrawRing(cell.ToVector3Shifted(), seconds, 1f, map);
                    break;
            }
        }
    }
}
