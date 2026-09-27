namespace RimArt
{
    /// <summary>
    /// Plain constants, no Unity resources - same rule and same reason as
    /// <see cref="TimeBubbleDefaults"/>. Logic thresholds and preview shapes only: every balance
    /// number is in the XML (<see cref="LarynxExtension"/> on the throat hediff, and
    /// <see cref="CompProperties_AbilityImperative"/> on each word).
    /// </summary>
    public static class LarynxDefaults
    {
        /// <summary>Below this much Talking, there is no voice left and the gizmos grey out.</summary>
        public const float MinTalkingToSpeak = 0.05f;

        /// <summary>Radius of the ring drawn round each listener in the preview.</summary>
        public const float ListenerRingRadius = 0.7f;

        /// <summary>Alpha of the fill on every cell the word reaches, in the preview.</summary>
        public const float ReachFillAlpha = 0.10f;
    }
}
