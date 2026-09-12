using Verse;

namespace RimArt
{
    /// <summary>
    /// One operator driving one car.
    ///
    /// The link is what "controlled" means. A car with no link is parked: an object on the map
    /// that pins nobody and costs nothing. That is the whole reason the car is a Thing rather
    /// than a Pawn - a parked colonist-shaped thing would still be orderable, and the cost of
    /// driving is the design.
    ///
    /// Deliberately not IExposable, and the car does not remember its operator either. A link is
    /// a live session, and a save taken mid-drive reloads with every car parked and every
    /// operator free. The alternative - scribing both references and resuming - buys back a few
    /// seconds of driving in exchange for a class of load-order bugs, which is the same trade
    /// <see cref="HediffComp_Resonance"/> refuses for the same reason. The operator's stun is
    /// saved by their own stance tracker and lapses on its own within
    /// <see cref="ToyCarDefaults.StunTicks"/>, so a reloaded operator is never stuck.
    /// </summary>
    public class ToyCarLink
    {
        public readonly Pawn Operator;
        public readonly ToyCar Car;

        public ToyCarLink(Pawn operatorPawn, ToyCar car)
        {
            Operator = operatorPawn;
            Car = car;
        }

        /// <summary>
        /// Runs one tick of the link. Returns false once the link is over.
        ///
        /// Every way a link can end is decided here, in one place and in this order, rather than
        /// by hooking death, downing, apparel removal and map changes separately. A per-tick
        /// check catches all of them uniformly - including the ones nobody thought of, like a
        /// dev-mode teleport or a quest pulling the pawn off the map - and cannot be bypassed.
        /// </summary>
        public bool Tick(Map map)
        {
            // The car went first. It has already dealt with its own destruction, including
            // exploding if it was killed, so there is nothing to detonate and nobody to blame.
            if (Car == null || Car.Destroyed || !Car.Spawned) return false;

            if (Operator == null || Operator.Dead || Operator.Downed)
            {
                // The dead-man's switch. It fires wherever the car happens to be, which may be
                // in the middle of your own line. That is the deal: the operator is a single
                // point of failure and standing them somewhere safe is the player's problem.
                Car.Detonate(Operator);
                return false;
            }

            // Off the map, or on another one. Not a dead-man event - losing the transmitter is
            // not the same as being shot - so the car simply parks where it stands.
            if (!Operator.Spawned || Operator.Map != map)
            {
                Car.Stop();
                return false;
            }

            if (Operator.IsHashIntervalTick(ToyCarDefaults.StunRefreshInterval))
            {
                // showMote false: a stun star every few ticks is noise, and the wrong reading.
                // The operator is not reeling, they are busy. Rotation is left alone so they go
                // on facing whatever they were facing.
                Operator.stances?.stunner?.StunFor(
                    ToyCarDefaults.StunTicks, Car, false, false, false);
            }

            return true;
        }
    }
}
