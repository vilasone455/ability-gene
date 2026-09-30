using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using T = RimArt.GojoBlue;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is pulled or hurt, no floor is torn up and no pawn is drawn. Each entry plays the
    /// Lapse: Blue picture round the chosen cell, which is the sketch's origin: the middle between Gojo and
    /// the pull's far edge. The Blue opens (<see cref="GojoBlue.ScriptDistance"/> - pull radius) / 2 = 2 cells
    /// east of the cell and Gojo stands <see cref="GojoBlue.ScriptDistance"/> cells west of the Blue, aiming
    /// east, so the recording lines up with the sketch in the lab. "blue: group" adds the drag marks of the
    /// sketch's three pawns inside the pull; "blue: empty ground" has none.
    /// </summary>
    public static class DebugActions_GojoBluePreview
    {
        [RimArtDebug("Gojo", "blue: group")]
        public static void Group() => Play(true);

        [RimArtDebug("Gojo", "blue: empty ground")]
        public static void EmptyGround() => Play(false);

        [RimArtDebug("Gojo", "blue: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_GojoBluePreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(bool group) =>
            Find.CurrentMap.GetComponent<MapComponent_GojoBluePreview>().Play(UI.MouseCell(), group);
    }

    /// <summary>
    /// Plays Lapse: Blue on its own clock, which is the sketch's, with the sketch's defaults. The sketch's pawns
    /// are the preview's script (<see cref="GojoBlue.ScriptPawnDegrees"/>): each is moved by the sketch's pull
    /// and handed to the picture as a drag, which is what the ability will pass for real pawns.
    /// </summary>
    public sealed class MapComponent_GojoBluePreview : MapComponent
    {
        public bool active;
        private float seconds;
        private bool group;
        private IntVec3 cell;
        private int shaken;
        private readonly List<BlueDrag> dragged = new List<BlueDrag>();

        /// <summary>The sketch's events(): (when, how hard), in time order.</summary>
        private static readonly (float at, float value)[] Shakes =
        {
            (T.OpenAt, T.OpenShake), (T.FirstRumbleAt, T.RumbleShake), (T.SecondRumbleAt, T.SecondRumbleShake), (T.BurstAt(T.Hold), T.BurstShake),
        };

        public MapComponent_GojoBluePreview(Map map) : base(map) { }

        public void Play(IntVec3 at, bool withPawns)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            group = withPawns;
            seconds = 0f;
            shaken = 0;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < Shakes.Length && seconds >= Shakes[shaken].at) Find.CameraDriver.shaker.DoShake(Shakes[shaken++].value);
            // The chosen cell is the sketch's origin, halfway along Gojo to the pull's far edge.
            Vector3 origin = cell.ToVector3Shifted();
            Vector2 centre = new Vector2(origin.x, origin.z) + Turn(T.ScriptAim) * ((T.ScriptDistance - T.PullRadius) / 2f);
            dragged.Clear();
            if (group)
            {
                float pulled = T.PullSeconds(seconds, T.Hold);
                for (int i = 0; i < T.ScriptPawnDegrees.Length; i++)
                {
                    Vector2 way = Turn(T.ScriptAim + T.ScriptPawnDegrees[i]);
                    float r0 = T.ScriptPawnFrom[i], r = T.Pulled(r0, pulled, T.PullRadius, T.CoreRadius, T.PullSpeed);
                    dragged.Add(new BlueDrag(centre + way * r0, centre + way * r));
                }
            }
            GojoBlueGraphics.Draw(new BlueCast
            {
                Caster = centre - Turn(T.ScriptAim) * T.ScriptDistance, Centre = centre, Seconds = seconds,
                Hold = T.Hold, PullRadius = T.PullRadius, CoreRadius = T.CoreRadius, BurstRadius = T.BurstRadius, Dark = T.Dark,
                Dragged = dragged, Sleeve = GojoGraphics.Uniform, Skin = GojoGraphics.Skin,
            }, map);
            if (seconds >= T.Duration(T.Hold)) active = false;
        }
    }
}
