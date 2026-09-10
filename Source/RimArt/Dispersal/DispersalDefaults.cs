namespace RimArt
{
    /// <summary>
    /// Plain constants, no Unity resources - same rule and same reason as
    /// <see cref="ArcDefaults"/>.
    ///
    /// The balance figures are not here. Charges, recharge, the damage floor and the blood
    /// price all live on <see cref="DispersalGeneExtension"/> so they can be moved without a
    /// rebuild, because every one of them is going to move. What is left in this file is the
    /// shape of the thing: how many birds, how long they are in the air, how far apart they
    /// spread on the way. None of that is balance, and all of it is what makes a scatter read
    /// as a body coming apart rather than a pawn blinking sideways.
    /// </summary>
    public static class DispersalDefaults
    {
        /// <summary>
        /// How many crows one body comes apart into.
        ///
        /// Nine is the smallest number that still reads as "a flock" rather than "some birds"
        /// at the speed these cross the screen, and small enough that the cell the carrier left
        /// is not a solid black square for half a second.
        /// </summary>
        public const int FlockSize = 9;

        /// <summary>
        /// Ticks the flock is in the air.
        ///
        /// The carrier is already standing at the far end before the first bird is drawn - the
        /// damage is being cancelled now, and there is no honest way to make that wait. So this
        /// is decoration catching up with a thing that has happened, and it is short because a
        /// player who watches the birds arrive after the pawn has moved learns the wrong rule.
        ///
        /// Thirty-eight ticks rather than the twenty-six it started at. At 0.43 seconds the
        /// flight ended before two wing beats had finished, and no amount of animation
        /// registers in that window; 0.63 seconds gives it about two and a half.
        /// </summary>
        public const int FlightTicks = 38;

        /// <summary>
        /// Ticks each frame of the wing beat is held. Eight frames at two ticks is a sixteen
        /// tick beat - about 3.7 beats a second, which is roughly a real crow.
        /// </summary>
        public const int TicksPerFrame = 2;

        /// <summary>
        /// Number of wing-beat frames on disk, as Crow0..Crow7. Eight, the same count Odyssey
        /// uses for its own birds, because four could not carry the swing: the first pass moved
        /// the silhouette by nineteen percent across the whole cycle, which at twenty pixels on
        /// screen is four pixels and reads as a static sprite being dragged along a line.
        /// </summary>
        public const int FrameCount = 8;

        /// <summary>One full wing beat, in ticks.</summary>
        public const int BeatTicks = FrameCount * TicksPerFrame;

        /// <summary>
        /// How far off the straight line between the two cells a bird may be pushed, in cells.
        ///
        /// The flock does not fly in formation. Each bird gets its own offset and its own
        /// arrival, and this is the width of that spread at its widest, halfway along.
        /// </summary>
        public const float SpreadCells = 1.9f;

        /// <summary>
        /// Drawn size of one crow, in cells. A crow is smaller than a person - but not as much
        /// smaller as the first pass assumed, because a bird drawn at 0.62 cells is about twenty
        /// screen pixels and there is no wing beat legible at twenty pixels.
        /// </summary>
        public const float CrowSize = 0.78f;

        /// <summary>
        /// Maximum forward pulse over a wing beat, in cells. Flock also caps this against
        /// travel per tick so the pulse stays subtle on short or slow flights.
        /// </summary>
        public const float SurgeCells = 0.08f;

        /// <summary>
        /// Vertical wing-beat bob in cells. Kept small relative to the bird's body so the
        /// wing animation carries the stroke while the flight arc supplies the height.
        /// </summary>
        public const float BobCells = 0.07f;

        /// <summary>
        /// Widest a bird's heading wanders off its own route, in degrees.
        ///
        /// Driven by distance along the route rather than by the beat, so it reads as a bird
        /// correcting its course rather than as one shivering.
        /// </summary>
        public const float WobbleDegrees = 11f;

        /// <summary>How high a scatter's flock rises at its midpoint, in cells. Barely - the
        /// birds are getting out of the way of a blow, not going anywhere.</summary>
        public const float ScatterLift = 0.35f;

        // -------- murder --------
        //
        // A murder is the same organ doing the same thing on purpose, over a longer distance,
        // and every number below is larger than its scatter counterpart for that reason. More
        // birds because there is time to see them, a wider spread because the route is long
        // enough to open out along, and real height because this flock is crossing walls rather
        // than stepping around a swing.

        /// <summary>How many crows a deliberate flight comes apart into.</summary>
        public const int MurderFlockSize = 15;

        /// <summary>Widest a murder opens away from its straight line, in cells.</summary>
        public const float MurderSpreadCells = 3.4f;

        /// <summary>How high a murder rises at its midpoint, in cells.</summary>
        public const float MurderLift = 1.7f;

        /// <summary>
        /// How many discrete alphas a bird fades through, for the same reason
        /// <see cref="ArcDefaults.StreakAlphaSteps"/> exists: the fade is baked into the
        /// material, so each step is one material the game caches for the session.
        /// </summary>
        public const int AlphaSteps = 8;

        /// <summary>Feathers thrown at the cell the carrier came apart in.</summary>
        public const int FeathersOnDeparture = 5;

        /// <summary>Feathers thrown where they put themselves back together.</summary>
        public const int FeathersOnArrival = 3;
    }
}
