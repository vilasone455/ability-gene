using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The carrier, in the air, as birds.
    ///
    /// Core's PawnFlyer already does the whole mechanism: it takes the pawn off the map into its
    /// own container, carries them along an arc over a computed number of ticks, and puts them
    /// back down at the far end with their job queue intact. Nothing about that needed
    /// reimplementing. What needed replacing is exactly one line - the base class draws the pawn
    /// it is carrying, and this one must not, because the whole claim of the gene is that there
    /// is no pawn to draw.
    ///
    /// So <see cref="DynamicDrawPhaseAt"/> deliberately does not call through to FlyingPawn, and
    /// <see cref="DrawAt"/> drops the shadow with it. A flock has no single shadow, and one
    /// person-shaped shade sliding along under fifteen birds gives the trick away.
    /// </summary>
    public class PawnFlyer_Murder : PawnFlyer
    {
        /// <summary>
        /// Rolled on the first frame rather than at construction. A flyer is created by
        /// PawnFlyer.MakeFlyer, which this class does not get to run code inside of, and it may
        /// be reconstructed by the save loader with no constructor call at all.
        /// </summary>
        private Flock flock;

        private Flock Birds
        {
            get
            {
                if (flock == null)
                {
                    flock = new Flock(DispersalDefaults.MurderFlockSize,
                        DispersalDefaults.MurderSpreadCells, DispersalDefaults.MurderLift);
                }
                return flock;
            }
        }

        /// <summary>
        /// How far along the flight is. The base class keeps these two protected for exactly
        /// this - a subclass that wants to draw the journey rather than the traveller.
        ///
        /// Deliberately raw, where the base class runs the same number through its
        /// progressCurve first. That curve exists to make a jump leave the ground like a jump -
        /// fifteen percent of the distance in the first tenth of the time - and birds do not do
        /// that. Both agree at the ends, which is all that has to be true: the flock arrives in
        /// the same tick the carrier is put back down.
        /// </summary>
        private float Progress =>
            ticksFlightTime <= 0 ? 1f : Mathf.Clamp01(ticksFlying / (float)ticksFlightTime);

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            if (phase != DrawPhase.Draw || FlyingPawn == null) return;

            // DrawPos refreshes the flyer's map position. Do not call the base draw method:
            // PawnFlyer forwards every draw phase to the human pawn inside its container.
            _ = DrawPos;
            Birds.Draw(startVec, DestinationPos, Progress, ticksFlying);
        }

        protected override void TickInterval(int delta)
        {
            if (Spawned && FlyingPawn != null && ticksFlying < ticksFlightTime
                && this.IsHashIntervalTick(6, delta))
            {
                DispersalFX.Travel(startVec, DestinationPos, Progress,
                    DispersalDefaults.MurderLift, Map);
            }

            base.TickInterval(delta);
        }

        /// <summary>
        /// Nothing. The base implementation draws a shadow and any carried thing; a flock has no
        /// shadow of its own, and whatever the carrier was holding went into the container with
        /// them and is not visible until they are a person again.
        /// </summary>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }

        /// <summary>
        /// Landing. The base class puts the pawn back on the map and restores their job queue;
        /// this adds arrival feathers. Deliberate flight leaves the carrier ready to act.
        /// </summary>
        protected override void RespawnPawn()
        {
            Pawn pawn = FlyingPawn;
            Map map = MapHeld;

            base.RespawnPawn();

            if (pawn == null) return;

            if (pawn.Spawned) DispersalFX.Arrive(pawn.DrawPos, pawn.Map);
            else if (map != null) DispersalFX.Arrive(DestinationPos, map);
        }
    }
}
