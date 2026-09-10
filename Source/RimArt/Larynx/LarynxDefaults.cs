namespace RimArt
{
    /// <summary>
    /// Plain constants, no Unity resources - same rule and same reason as
    /// <see cref="TimeBubbleDefaults"/>.
    ///
    /// Every number here is a shape rather than a balance figure. The actual cost of a word is
    /// mostly decided by what the target is doing when it is said, which is not in this file.
    /// </summary>
    public static class LarynxDefaults
    {
        /// <summary>Below this much Hearing, the word is not heard and nothing happens.</summary>
        public const float MinHearingToObey = 0.15f;

        /// <summary>Below this much Talking, there is no voice left and the gizmos grey out.</summary>
        public const float MinTalkingToSpeak = 0.05f;

        /// <summary>Telling someone to do what they were already doing.</summary>
        public const float AlreadyObeyingFactor = 0.15f;

        /// <summary>Nobody in a tantrum is listening. This is the dominant term.</summary>
        public const float MentalStateFactor = 2.5f;

        public const float HostileFactor = 2f;

        /// <summary>A downed pawn still resists a little; this is the floor.</summary>
        public const float MinConsciousnessFactor = 0.2f;

        /// <summary>How far RUN sends them.</summary>
        public const int FleeDistance = 24;

        /// <summary>
        /// What each word costs before anything about the target is considered.
        ///
        /// KNEEL and RUN are dearest because they take a body somewhere it did not agree to go.
        /// STOP is cheap because it asks for nothing but a pause. DROP sits between: it is one
        /// small motion, but it is the one nobody performs willingly.
        /// </summary>
        public static float BaseWearFor(ImperativeWord word)
        {
            switch (word)
            {
                case ImperativeWord.Stop:  return 0.030f;
                case ImperativeWord.Drop:  return 0.055f;
                case ImperativeWord.Come:  return 0.070f;
                case ImperativeWord.Kneel: return 0.090f;
                case ImperativeWord.Run:   return 0.090f;
                default:                   return 0.050f;
            }
        }
    }
}
