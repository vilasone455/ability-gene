using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The wielder in the air during Vault Strike. Core's PawnFlyer does the flight: a straight line
    /// on the ground at constant speed and a parabola for height, which is the arc the picture
    /// uses, so the time in the air and the peak are this flyer's def numbers.
    ///
    /// Two additions. The flight's progress is public, so the pole can be kept in the wielder's
    /// hands. And for north and south flights the pawn is drawn shifted east by its height, as the
    /// pole is (PowerPoleStrikeTiming.Bow), or the pawn and the pole would part.
    /// </summary>
    public class PawnFlyer_PowerPoleVault : PawnFlyer
    {
        public float Progress => ticksFlightTime <= 0 ? 1f : Mathf.Clamp01(ticksFlying / (float)ticksFlightTime);
        public float FlightSeconds => ticksFlightTime / 60f;
        /// <summary>The arc's peak in cells of height. The def's heightFactor is how far north that is drawn.</summary>
        public float PeakCells => def.pawnFlyer.heightFactor / SixPathsHeight.Lift;

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            if (FlyingPawn == null) return;
            // DrawPos refreshes the flyer's place on the map. The base draw is not called: it would
            // draw the pawn a second time, unshifted.
            Vector3 at = DrawPos;
            Vector3 flight = DestinationPos - startVec;
            float northSouth = flight.MagnitudeHorizontal() < 0.001f ? 0f : Mathf.Abs(flight.z) / flight.MagnitudeHorizontal();
            float height = PeakCells * 4f * Progress * (1f - Progress);
            at.x += PowerPoleStrikeTiming.Bow * northSouth * height;
            FlyingPawn.DynamicDrawPhaseAt(phase, at);
            if (phase == DrawPhase.Draw) DrawAt(drawLoc, flip);
        }
    }
}
