namespace AbilityGenes
{
    /// <summary>
    /// The words the larynx can shape. One AbilityDef per word, all of them running the same
    /// comp with a different value here - the same arrangement the anchor organ uses to get
    /// three gizmos out of one class.
    /// </summary>
    public enum ImperativeWord
    {
        /// <summary>Stand still and do nothing.</summary>
        Stop,

        /// <summary>Let go of whatever is being held.</summary>
        Drop,

        /// <summary>Go prone.</summary>
        Kneel,

        /// <summary>Walk to the speaker.</summary>
        Come,

        /// <summary>Run away from the speaker.</summary>
        Run
    }
}
