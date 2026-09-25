using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt, pulled or pinned and no pawn is drawn. Each entry plays one
    /// sketch scenario round the chosen cell, which is the sketch's centre: halfway between the holder
    /// and the target. The target weights are the sketch's (ChainSickleRule.ScriptTargets) with a
    /// 75 kg-carry holder. The weapon, abilities and their test shortcuts are in ChainSickle/Kit.
    /// </summary>
    public static class DebugActions_ChainSickle
    {
        [RimArtDebug("Chain Sickle", "snag tribal 65 kg")]
        public static void SnagTribal() => Preview().Play(UI.MouseCell(), ChainSicklePreview.Snag, 0, 0f);

        [RimArtDebug("Chain Sickle", "snag raider 95 kg")]
        public static void SnagRaider() => Preview().Play(UI.MouseCell(), ChainSicklePreview.Snag, 1, 0f);

        [RimArtDebug("Chain Sickle", "snag muffalo 140 kg")]
        public static void SnagMuffalo() => Preview().Play(UI.MouseCell(), ChainSicklePreview.Snag, 2, 0f);

        [RimArtDebug("Chain Sickle", "snag thrumbo 240 kg (holder dragged)")]
        public static void SnagThrumbo() => Preview().Play(UI.MouseCell(), ChainSicklePreview.Snag, 3, 0f);

        [RimArtDebug("Chain Sickle", "snag tribal south")]
        public static void SnagSouth() => Preview().Play(UI.MouseCell(), ChainSicklePreview.Snag, 0, 270f);

        [RimArtDebug("Chain Sickle", "stake tribal (tries to run)")]
        public static void StakeTribal() => Preview().Play(UI.MouseCell(), ChainSicklePreview.StakeRuns, 0, 0f);

        [RimArtDebug("Chain Sickle", "stake tribal (stands still)")]
        public static void StakeStill() => Preview().Play(UI.MouseCell(), ChainSicklePreview.StakeStill, 0, 0f);

        [RimArtDebug("Chain Sickle", "stake raider 95 kg")]
        public static void StakeRaider() => Preview().Play(UI.MouseCell(), ChainSicklePreview.StakeRuns, 1, 0f);

        [RimArtDebug("Chain Sickle", "stake muffalo 140 kg")]
        public static void StakeMuffalo() => Preview().Play(UI.MouseCell(), ChainSicklePreview.StakeRuns, 2, 0f);

        [RimArtDebug("Chain Sickle", "stake tribal south")]
        public static void StakeSouth() => Preview().Play(UI.MouseCell(), ChainSicklePreview.StakeRuns, 0, 270f);

        [RimArtDebug("Chain Sickle", "stake thrumbo 240 kg (too heavy, refused)")]
        public static void StakeRefused() => Preview().Play(UI.MouseCell(), ChainSicklePreview.StakeRefused, 3, 0f);

        [RimArtDebug("Chain Sickle", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_ChainSicklePreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_ChainSicklePreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_ChainSicklePreview>();
    }

    public enum ChainSicklePreview { Snag, StakeRuns, StakeStill, StakeRefused }

    public sealed class MapComponent_ChainSicklePreview : MapComponent
    {
        /// <summary>How long the refused Stake shows the snagged pose.</summary>
        private const float RefusedSeconds = 2f;

        public bool active;
        private ChainSicklePreview mode;
        private int target;
        private float seconds, duration, aim;
        private readonly float[] shakeAt = new float[2], shakeSize = new float[2];
        private readonly bool[] shaken = new bool[2];
        private IntVec3 cell;

        public MapComponent_ChainSicklePreview(Map map) : base(map) { }

        public void Play(IntVec3 at, ChainSicklePreview play, int targetIndex, float aimDegrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            target = targetIndex;
            aim = aimDegrees;
            seconds = 0f;
            shaken[0] = shaken[1] = false;
            if (play == ChainSicklePreview.Snag)
            {
                float reel = ChainSickleRule.Script(targetIndex).Reel;
                duration = ChainSickleSnagTiming.End(reel, ChainSickleSnagTiming.Hold);
                shakeAt[0] = ChainSickleSnagTiming.Hit;
                shakeSize[0] = ChainSickleSnagTiming.HitShake;
                shakeAt[1] = ChainSickleSnagTiming.Reel0;
                shakeSize[1] = ChainSickleSnagTiming.ReelShake;
            }
            else if (play == ChainSicklePreview.StakeRefused)
            {
                duration = RefusedSeconds;
                shakeAt[0] = shakeAt[1] = float.MaxValue;
            }
            else
            {
                duration = ChainSickleStakeTiming.ScriptEnd;
                shakeAt[0] = ChainSickleStakeTiming.Staked;
                shakeSize[0] = ChainSickleStakeTiming.StakeShake;
                shakeAt[1] = ChainSickleStakeTiming.Cut(ChainSickleStakeTiming.ScriptSwingAt);
                shakeSize[1] = ChainSickleStakeTiming.CutShake;
            }
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            for (int i = 0; i < 2; i++)
            {
                if (shaken[i] || seconds < shakeAt[i]) continue;
                shaken[i] = true;
                Find.CameraDriver.shaker.DoShake(shakeSize[i]);
            }
            Vector3 centre = cell.ToVector3Shifted();
            if (mode == ChainSicklePreview.Snag) ChainSickleSnagGraphics.DrawPreview(centre, aim, target, seconds, map);
            else ChainSickleStakeGraphics.DrawPreview(centre, aim, target, mode == ChainSicklePreview.StakeRuns,
                mode == ChainSicklePreview.StakeRefused, seconds, map);
            if (seconds >= duration) active = false;
        }
    }
}
