using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// What is left of the mark's old look. The marks themselves are playing cards now, drawn by
    /// ClapTeleportGraphics; this is only the outline the double clap's targeter puts round every
    /// mark still held, in the same gold as a marked tile's own outline.
    /// </summary>
    public static class AnchorGraphics
    {
        public static readonly Color EdgeColor = new Color(0.88f, 0.69f, 0.25f, 0.6f);
    }
}
