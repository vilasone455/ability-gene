namespace RimArt
{
    /// <summary>
    /// The words Inumaki can say. One AbilityDef per word, all of them running the same comp with a
    /// different value here - the same arrangement the anchor organ uses to get three gizmos out of
    /// one class.
    /// </summary>
    public enum ImperativeWord
    {
        /// <summary>Stand still and do nothing.</summary>
        Stop,

        /// <summary>Let go of the held weapon.</summary>
        Drop,

        /// <summary>Blunt damage and a short stun. Replaced Kneel on 2026-09-27.</summary>
        Crush,

        /// <summary>Walk to the speaker.</summary>
        Come,

        /// <summary>Run away from the speaker.</summary>
        Run,

        /// <summary>A Bomb explosion centred on each listener.</summary>
        Explode
    }

    /// <summary>How loud a word is said. Reach and throat factor per volume are in <see cref="LarynxExtension"/>.</summary>
    public enum WordVolume
    {
        Whisper,
        Speak,
        Shout
    }
}
