using UnityEngine;

namespace AbilityGenes
{
    /// <summary>
    /// Plain constants, in a class that loads no Unity resources - the same rule and the same
    /// reason as <see cref="LanceDefaults"/> and <see cref="TimeBubbleDefaults"/>. CompProperties
    /// field initializers run on the def-parsing thread, so a default that reached
    /// <see cref="InvoluteGraphics"/> would fire a Material load off the main thread.
    /// </summary>
    public static class InvoluteDefaults
    {
        /// <summary>
        /// The hole. Nearly black with a violet bias rather than pure black, so it reads as an
        /// opening onto somewhere rather than as a rendering fault, and nearly opaque so that it
        /// is still obvious standing on pale sand at noon.
        /// </summary>
        public static readonly Color VoidColor = new Color(0.05f, 0.02f, 0.10f, 0.96f);

        /// <summary>
        /// The edge, and the part that actually catches the eye. Bright, and deliberately not
        /// the skip-blue of the anchor marks and the stasis dome: those move things through
        /// normal space, this opens somewhere else, and the player should be able to tell which
        /// family of effect they are looking at without reading a word.
        /// </summary>
        public static readonly Color RimColor = new Color(0.72f, 0.40f, 1f, 0.95f);

        /// <summary>What is on the far side, over the top of the void.</summary>
        public static readonly Color SwirlColor = new Color(0.85f, 0.66f, 1f, 0.75f);

        public const float VoidScale = 1f;
        public const float RimScale = 1.55f;
        public const float SwirlScale = 0.92f;

        /// <summary>The aperture on the ground, in cells across.</summary>
        public const float ApertureScale = 1.9f;

        /// <summary>The hole on a body is a detail on a pawn, not a thing on the ground.</summary>
        public const float BodyHoleScale = 0.55f;

        /// <summary>Where on the pawn the hole is drawn. See the note in MapComponent_Involute.</summary>
        public static readonly Vector3 BodyHoleOffset = new Vector3(0.28f, 0f, -0.10f);
    }
}
