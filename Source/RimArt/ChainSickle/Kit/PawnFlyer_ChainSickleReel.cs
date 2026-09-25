using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A pawn Snag reels in, or a holder dragged toward a too-heavy target. Core's flyer slides it
    /// along the ground on the def's progress curve (the sketch's easeOut); the time is not the def's
    /// but the weight rule's reel, set per flight with <see cref="durationTicks"/>. The pawn stays in
    /// the flyer, off the map, for the reel, and is put down once at the end.
    /// </summary>
    public class PawnFlyer_ChainSickleReel : PawnFlyer
    {
        /// <summary>The flight's length in ticks. Set it after MakeFlyer and before the flyer is spawned.</summary>
        public int durationTicks;

        public void SetDuration(int ticks)
        {
            durationTicks = ticks;
            if (ticks > 0) ticksFlightTime = ticks;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            // Set again after spawning, in case Core works the flight time out there.
            if (!respawningAfterLoad && durationTicks > 0) ticksFlightTime = durationTicks;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref durationTicks, "durationTicks");
        }
    }
}
