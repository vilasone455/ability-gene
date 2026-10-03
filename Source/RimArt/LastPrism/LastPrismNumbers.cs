namespace RimArt
{
    /// <summary>
    /// The Last Prism's balance numbers as the previews need them, under the XML fields' names: AG_LastPrism_Fire's
    /// comp and its verb's range, and the store of AG_LastPrism's charge comp. The picture side does not name the
    /// rules' types (LastPrism/Kit), so the lab's recorder builds it without them. In game <see cref="Of"/> is filled
    /// from the defs at startup (LastPrismNumbersFromDefs in Kit); the recorder fills it from the same def XML.
    /// </summary>
    public sealed class LastPrismNumbers
    {
        /// <summary>The beam's range (cells): the ability verb's range.</summary>
        public float range;
        public float joinSeconds, fanDegrees, width, fanReach, turnDegreesPerSecond;
        public float fanDamage, fanEverySeconds, joinedDamage, joinedEverySeconds;
        /// <summary>Seconds of beam the prism holds.</summary>
        public float store;

        /// <summary>Set once the defs are loaded, before any preview runs.</summary>
        public static LastPrismNumbers Of;
    }
}
