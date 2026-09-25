using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: nothing is swallowed, nobody is hurt and no pawn is drawn. Each entry plays one
    /// sketch's default script round the chosen cell, which is the sketch's centre: halfway between
    /// caster and target for Suck and Spit, the caster's cell for Digest. The sketches' stand-in chunk,
    /// rifle and filth puddle are drawn as props so the recordings can be set beside the sketches. The
    /// weapon, abilities and their test shortcuts are in Vacuum/Kit.
    /// </summary>
    public static class DebugActions_Vacuum
    {
        [RimArtDebug("Vacuum", "suck")]
        public static void Suck() => Preview().Play(UI.MouseCell(), VacuumPreview.Suck, 0f);

        [RimArtDebug("Vacuum", "suck south")]
        public static void SuckSouth() => Preview().Play(UI.MouseCell(), VacuumPreview.Suck, 270f);

        [RimArtDebug("Vacuum", "suck loose things only")]
        public static void SuckLoose() => Preview().Play(UI.MouseCell(), VacuumPreview.SuckLoose, 0f);

        [RimArtDebug("Vacuum", "spit")]
        public static void Spit() => Preview().Play(UI.MouseCell(), VacuumPreview.Spit, 0f);

        [RimArtDebug("Vacuum", "spit south")]
        public static void SpitSouth() => Preview().Play(UI.MouseCell(), VacuumPreview.Spit, 270f);

        [RimArtDebug("Vacuum", "spit rifle on an empty cell")]
        public static void SpitRifle() => Preview().Play(UI.MouseCell(), VacuumPreview.SpitRifle, 0f);

        [RimArtDebug("Vacuum", "digest")]
        public static void Digest() => Preview().Play(UI.MouseCell(), VacuumPreview.Digest, 0f);

        [RimArtDebug("Vacuum", "digest south")]
        public static void DigestSouth() => Preview().Play(UI.MouseCell(), VacuumPreview.Digest, 270f);

        [RimArtDebug("Vacuum", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_VacuumPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_VacuumPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_VacuumPreview>();
    }

    public enum VacuumPreview { Suck, SuckLoose, Spit, SpitRifle, Digest }

    public sealed class MapComponent_VacuumPreview : MapComponent
    {
        public bool active;
        private VacuumPreview mode;
        private float seconds, duration, aim;
        private IntVec3 cell;
        /// <summary>The script's camera shakes: when and how hard; each is played once.</summary>
        private readonly List<Vector2> shakes = new List<Vector2>();
        private int shaken;

        public MapComponent_VacuumPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, VacuumPreview play, float aimDegrees)
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
                case VacuumPreview.Suck:
                case VacuumPreview.SuckLoose:
                    duration = VacuumSuckTiming.ScriptEnd(play == VacuumPreview.Suck);
                    break;
                case VacuumPreview.Spit:
                case VacuumPreview.SpitRifle:
                    duration = VacuumSpitTiming.ScriptEnd;
                    shakes.Add(new Vector2(VacuumSpitTiming.Launch, VacuumSpitTiming.LaunchShake));
                    if (play == VacuumPreview.Spit) shakes.Add(new Vector2(VacuumSpitTiming.Impact, VacuumSpitTiming.HitShake));
                    break;
                default:
                    duration = VacuumDigestTiming.ScriptEnd;
                    shakes.Add(new Vector2(VacuumDigestTiming.Done(new VacuumDigestShot()), VacuumDigestTiming.BurpShake));
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
                case VacuumPreview.Suck:
                case VacuumPreview.SuckLoose:
                    VacuumSuckGraphics.DrawPreview(centre, aim, mode == VacuumPreview.Suck, seconds, map);
                    break;
                case VacuumPreview.Spit:
                case VacuumPreview.SpitRifle:
                    VacuumSpitGraphics.DrawPreview(centre, aim, mode == VacuumPreview.Spit, seconds, map);
                    break;
                default:
                    VacuumDigestGraphics.DrawPreview(centre, aim, seconds, map);
                    break;
            }
            if (seconds >= duration) active = false;
        }
    }
}
