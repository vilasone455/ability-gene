using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using T = RimArt.GojoBlue;

namespace RimArt
{
    /// <summary>
    /// Draws a Gravity Well cast whose def has <c>look</c> GojoBlue (Gojo's Blue, AG_GojoBlue) with the Lapse: Blue
    /// picture (<see cref="GojoBlueGraphics"/>) instead of <see cref="GravityGraphics"/>. The picture's clock is the
    /// cast's: 0 at the cast when the opening is <see cref="GojoBlue.FullAt"/> (0.45 s, Blue's XML), so the ball is
    /// full when the pull starts. The picture's hold is the pull's length less the <see cref="GojoBlue.Rush"/>, so
    /// its implosion flash lands on the tick the implosion damage is dealt. A Blue that closed without imploding
    /// (used up by Hollow Purple, or Gojo lost) stops being drawn at once. The drag marks are the pawns the well
    /// pulled, from where it first took each to where it is; they stay through the implosion's tail.
    /// </summary>
    public static class GojoBlueLook
    {
        /// <summary>Per cast: the last drag marks, kept through the tail, and the camera shakes already made. Not saved.</summary>
        private sealed class State
        {
            public readonly List<BlueDrag> drags = new List<BlueDrag>();
            public int shaken;
        }

        private static readonly ConditionalWeakTable<GravityCast, State> states = new ConditionalWeakTable<GravityCast, State>();

        /// <summary>How long a Blue cast is kept after it implodes: the picture's dust and crater.</summary>
        public static int TailTicks(CompProperties_AbilityGravityWell props) =>
            props.look == GravityLook.GojoBlue ? Mathf.CeilToInt(T.Tail * 60f) + 1 : 0;

        /// <summary>The picture's hold (s): the pull's length less the rush-in.</summary>
        public static float Hold(GravityCast cast) => Mathf.Max(0f, cast.DurationTicks / 60f - T.Rush);

        /// <summary>The picture's clock for this cast now, or below 0 when nothing is to be drawn.</summary>
        public static float Seconds(GravityCast cast)
        {
            if (cast.Active) return T.FullAt - cast.Props.openingSeconds + UbwClock.Since(cast.startTick);
            if (cast.clock.imploded && cast.endTick >= 0) return T.BurstAt(Hold(cast)) + UbwClock.Since(cast.endTick);
            return -1f;
        }

        public static void Draw(MapComponent_Gravity component, GravityCast cast)
        {
            Map map = component.map;
            float s = Seconds(cast), hold = Hold(cast);
            if (s < 0f || s >= T.Duration(hold) || cast.Cell.Fogged(map)) return;
            State state = states.GetValue(cast, _ => new State());
            if (cast.Active)
            {
                state.drags.Clear();
                IReadOnlyList<GravityMotion> motions = component.Motions;
                for (int i = 0; i < motions.Count; i++)
                {
                    GravityMotion motion = motions[i];
                    if (!(motion.thing is Pawn pawn) || !pawn.Spawned || component.Owner(pawn, motion.position) != cast) continue;
                    state.drags.Add(new BlueDrag(GojoKit.Ground(motion.start), GojoKit.Ground(motion.position)));
                }
            }
            Shake(state, s, hold);
            Pawn gojo = cast.caster;
            Vector2 at = gojo != null && gojo.Spawned && gojo.Map == map ? GojoKit.Ground(gojo.DrawPos) : GojoKit.Ground(cast.anchor);
            var props = cast.Props;
            PawnFit.Begin();
            try
            {
                GojoBlueGraphics.Draw(new BlueCast
                {
                    Caster = at, Centre = GojoKit.Ground(cast.Centre), Seconds = s, Hold = hold,
                    PullRadius = cast.Radius, CoreRadius = props.coreRadius,
                    BurstRadius = cast.clock.imploded ? cast.burstRadius : props.BurstRadius(cast.eaten), Dark = T.Dark,
                    Dragged = state.drags, Sleeve = GojoKit.Sleeve(gojo), Skin = GojoKit.Skin(gojo),
                }, map);
            }
            finally
            {
                PawnFit.End();
            }
        }

        /// <summary>The sketch's shakes: as it opens, two rumbles in the hold, the implosion. Only for the map on screen.</summary>
        private static readonly float[] ShakeSize = { T.OpenShake, T.RumbleShake, T.SecondRumbleShake, T.BurstShake };

        private static void Shake(State state, float s, float hold)
        {
            while (state.shaken < ShakeSize.Length)
            {
                int n = state.shaken;
                float at = n == 0 ? T.OpenAt : n == 1 ? T.FirstRumbleAt : n == 2 ? T.SecondRumbleAt : T.BurstAt(hold);
                if (s < at) return;
                // A shake a load or a late first frame skipped past is not made up.
                if (s < at + 0.25f) Find.CameraDriver.shaker.DoShake(ShakeSize[n]);
                state.shaken++;
            }
        }
    }
}
