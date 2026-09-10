using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One flock in the air over a scatter, from the cell a body came apart in to the cell it
    /// landed in.
    ///
    /// This is decoration and nothing else. The carrier is already standing at the far end
    /// before the first bird is drawn, and no part of the game reads this class - it holds no
    /// damage, blocks nothing and is not scribed, so a save taken mid-flight simply loads with
    /// the birds already gone. The arc tendon makes the same call about its afterimages and for
    /// the same reason: half a second of picture is wrong on the other side of a reload anyway.
    ///
    /// Murder is the opposite case and does not use this class: there the flight *is* the
    /// ability, the carrier is inside it, and it is a <see cref="PawnFlyer_Murder"/> that the
    /// game ticks and saves like any other thing.
    /// </summary>
    public class ScatterFlight
    {
        private readonly Vector3 from;
        private readonly Vector3 to;
        private readonly Flock flock;

        private int ticks;

        public ScatterFlight(Vector3 from, Vector3 to)
        {
            this.from = from;
            this.to = to;
            flock = new Flock(DispersalDefaults.FlockSize,
                DispersalDefaults.SpreadCells, DispersalDefaults.ScatterLift);
        }

        /// <returns>False when the flight is over and should be dropped.</returns>
        public bool Tick(Map map)
        {
            ticks++;
            if (ticks % 6 == 0)
            {
                DispersalFX.Travel(from, to, ticks / (float)DispersalDefaults.FlightTicks,
                    DispersalDefaults.ScatterLift, map);
            }
            return ticks <= DispersalDefaults.FlightTicks;
        }

        public void Draw()
        {
            flock.Draw(from, to, ticks / (float)DispersalDefaults.FlightTicks, ticks);
        }
    }
}
