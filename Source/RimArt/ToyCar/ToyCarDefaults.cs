namespace RimArt
{
    /// <summary>
    /// Every number the toy car is balanced on, in one place.
    ///
    /// The car delivers a charge somewhere a grenade cannot be thrown. Its two governing numbers
    /// are the leash and the speed: the leash decides what "somewhere" can mean, and the speed
    /// decides how long the operator stands helpless to reach it.
    /// </summary>
    public static class ToyCarDefaults
    {
        /// <summary>
        /// How far from its operator the car can be sent, in cells.
        ///
        /// Short of a gun's reach on purpose. The car exists to get past a corner or a door, not
        /// to be a second weapon range, and a leash that matched a rifle would make it one.
        /// </summary>
        public const float LeashRadius = 24f;

        /// <summary>
        /// Ticks spent crossing one cardinal cell: five cells per second at normal game speed.
        /// Diagonal steps take proportionally longer so changing direction does not boost speed.
        /// </summary>
        public const int TicksPerCell = 12;

        /// <summary>Half a second to reach cruising speed; quicker braking for tight corridors.</summary>
        public const int AccelerationTicks = 30;
        public const int BrakingTicks = 20;
        public const float TurnDegreesPerTick = 6f;

        /// <summary>
        /// Length of the stun put on the operator, and how often it is renewed.
        ///
        /// Rolling rather than one long stun, and that is a safety property rather than a
        /// preference. The lock is only ever as durable as the component renewing it, so if the
        /// link dies in a way nothing here anticipated - an exception, a torn-down map, another
        /// mod - the operator comes back on their own within a third of a second. A single stun
        /// long enough to cover a whole drive would leave a colonist frozen for the rest of the
        /// save if it were ever applied and not cleared.
        /// </summary>
        public const int StunTicks = 20;

        /// <summary>How often the stun above is renewed. Must stay comfortably under it.</summary>
        public const int StunRefreshInterval = 10;

        /// <summary>Cells of blast. Matches the frost bomb, so the two read as the same size.</summary>
        public const float BlastRadius = 2.9f;

        /// <summary>
        /// Blast damage. Below a frag grenade's 56: this is a way of putting an explosion
        /// somewhere, not a bigger explosion.
        /// </summary>
        public const int BlastDamage = 45;
    }
}
