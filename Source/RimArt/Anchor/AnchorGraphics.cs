using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The mark's visual. Nothing here may be touched during def parsing - see the note on
    /// TimeBubbleGraphics. The material is resolved lazily so that even if something reaches
    /// this class early, the texture is only fetched from the draw path, which is main thread.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AnchorGraphics
    {
        private static Material markMat;

        /// <summary>
        /// The flash vanilla draws at both ends of a skip. Core, not Royalty - the psycast
        /// references it but the fleck and its texture live in the base game.
        /// </summary>
        public static Material MarkMat
        {
            get
            {
                if (markMat == null)
                {
                    markMat = MaterialPool.MatFrom("Things/Mote/PsycastSkipFlash", ShaderDatabase.MoteGlow);
                }
                return markMat;
            }
        }

        public static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        /// <summary>
        /// Off-white with a blue bias, the same family as the stasis dome, because both are the
        /// same organ family of effect and neither should read as a vanilla grey force field.
        /// </summary>
        public static readonly Color MarkColor = new Color(0.75f, 0.90f, 1f, 0.55f);

        /// <summary>The cell outline under the glow. Marks sit on exact cells and the player
        /// has to be able to tell which one, especially for a tile mark on open ground.</summary>
        public static readonly Color EdgeColor = new Color(0.75f, 0.90f, 1f, 0.45f);

        public const float MarkScale = 1.1f;
    }
}
