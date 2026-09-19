using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only. There is no ability, no hediff and no def behind any of this: nobody jumps,
    /// nobody is hit and no pawn or kunai is drawn. Each entry plays one effect's drawing round the
    /// chosen cell, which is the middle of the scene as it is in the lab's sketch. The drawing is the
    /// same code for every direction, so most entries run east and one runs on a diagonal.
    /// </summary>
    public static class DebugActions_ThunderGod
    {
        private static readonly Vector2 NorthWest = new Vector2(Mathf.Cos(140f * Mathf.Deg2Rad), Mathf.Sin(140f * Mathf.Deg2Rad));

        [RimArtDebug("Flying Thunder God", "jump to kunai in enemy")]
        public static void JumpEnemy() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Jump, Vector2.right, true);

        [RimArtDebug("Flying Thunder God", "jump to kunai in enemy north-west")]
        public static void JumpEnemyNorthWest() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Jump, NorthWest, true);

        [RimArtDebug("Flying Thunder God", "jump to kunai on ground")]
        public static void JumpGround() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Jump, Vector2.right, false);

        [RimArtDebug("Flying Thunder God", "chain 3 targets stays")]
        public static void ChainStays() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Chain, Vector2.right, false, 3);

        [RimArtDebug("Flying Thunder God", "chain 5 targets jumps back")]
        public static void ChainReturns() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Chain, Vector2.right, true, 5);

        [RimArtDebug("Flying Thunder God", "guiding thunder kunai in enemy")]
        public static void GuidingEnemy() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Guiding, Vector2.right, true);

        [RimArtDebug("Flying Thunder God", "guiding thunder kunai on ground")]
        public static void GuidingGround() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Guiding, Vector2.right, false);

        [RimArtDebug("Flying Thunder God", "rasengan teleport")]
        public static void RasenganTeleport() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Rasengan, Vector2.right, true);

        [RimArtDebug("Flying Thunder God", "rasengan teleport into wall")]
        public static void RasenganWall() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Rasengan, Vector2.right, true, 1);

        [RimArtDebug("Flying Thunder God", "rasengan touch north-west")]
        public static void RasenganTouch() => Preview().Play(UI.MouseCell(), ThunderGodPreview.Rasengan, NorthWest, false);

        [RimArtDebug("Flying Thunder God", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_ThunderGodPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_ThunderGodPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_ThunderGodPreview>();
    }

    public enum ThunderGodPreview { Jump, Chain, Guiding, Rasengan }

    public sealed class MapComponent_ThunderGodPreview : MapComponent
    {
        public bool active;
        private ThunderGodPreview mode;
        private float seconds, duration;
        private IntVec3 cell;
        private Vector2 toward;
        /// <summary>Jump and Guiding: the kunai is in an enemy. Chain: jumps back. Rasengan: teleports.</summary>
        private bool flag;
        /// <summary>Chain: marked pawns. Rasengan: 1 when a wall stops the throw.</summary>
        private int number;
        // The camera shakes of the effect being played, in time order, and how many have been done.
        private readonly float[] shakeAt = new float[ThunderGodChainTiming.MostTargets], shakeSize = new float[ThunderGodChainTiming.MostTargets];
        private int shakes, shaken;

        public MapComponent_ThunderGodPreview(Map map) : base(map) { }

        public void Play(IntVec3 target, ThunderGodPreview play, Vector2 facing, bool option, int count = 0)
        {
            if (!target.InBounds(map) || target.Fogged(map)) return;
            cell = target;
            mode = play;
            toward = facing;
            flag = option;
            number = count;
            seconds = 0f;
            shakes = shaken = 0;
            switch (play)
            {
                case ThunderGodPreview.Jump:
                    duration = ThunderGodJumpTiming.Duration;
                    if (option) Shake(ThunderGodJumpTiming.HitAt, ThunderGodJumpTiming.Shake);
                    break;
                case ThunderGodPreview.Chain:
                    duration = ThunderGodChainTiming.Duration(count, option);
                    for (int k = 0; k < count; k++) Shake(ThunderGodChainTiming.HitAt(k), ThunderGodChainTiming.Shake);
                    break;
                case ThunderGodPreview.Guiding:
                    // A defence: no camera shake.
                    duration = GuidingThunderTiming.Duration;
                    break;
                default:
                    duration = RasenganTiming.Duration(option, count == 1);
                    Shake(RasenganTiming.HitAt(option), RasenganTiming.HitShake);
                    Shake(RasenganTiming.ReleaseAt(option), RasenganTiming.ReleaseShake);
                    if (count == 1) Shake(RasenganTiming.LandAt(option, true), RasenganTiming.WallShake);
                    break;
            }
            active = true;
        }

        private void Shake(float at, float size)
        {
            shakeAt[shakes] = at;
            shakeSize[shakes++] = size;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes && seconds >= shakeAt[shaken]) Find.CameraDriver.shaker.DoShake(shakeSize[shaken++]);

            Vector3 centre = cell.ToVector3Shifted();
            switch (mode)
            {
                case ThunderGodPreview.Jump:
                    ThunderGodJumpGraphics.Draw(centre, toward, flag, seconds, map);
                    break;
                case ThunderGodPreview.Chain:
                    ThunderGodChainGraphics.Draw(centre, toward, number, flag, seconds, map);
                    break;
                case ThunderGodPreview.Guiding:
                    GuidingThunderGraphics.Draw(centre, toward, flag, seconds, map);
                    break;
                default:
                    RasenganGraphics.Draw(centre, toward, flag, number == 1, seconds, map);
                    break;
            }
            if (seconds >= duration) active = false;
        }
    }
}
