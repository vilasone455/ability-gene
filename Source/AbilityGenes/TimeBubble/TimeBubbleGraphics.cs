using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// The dome is the same one vanilla draws for projectile-interceptor shields
    /// (mech cluster shields, firefoam poppers): a flat plane10 quad wearing the
    /// "Other/ForceField" texture under the MoteGlow shader, scaled up to the field
    /// radius and tinted through a MaterialPropertyBlock.
    ///
    /// Materials must not be built before content finishes loading, hence
    /// StaticConstructorOnStartup.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TimeBubbleGraphics
    {
        public static readonly Material FieldMat =
            MaterialPool.MatFrom("Other/ForceField", ShaderDatabase.MoteGlow);

        /// <summary>
        /// The visible ring occupies less than the full quad, so the quad has to be
        /// scaled past the true radius for the ring to land on it. Vanilla calls this
        /// TextureActualRingSizeFactor and uses the same number.
        /// </summary>
        public const float TextureRingSizeFactor = 1.1601562f;

        public static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        /// <summary>
        /// Every vanilla force field is desaturated - bullet shield and the mech shields
        /// are (0.4, 0.4, 0.4), the broadshield projector (0.6, 0.6, 0.8) - and they idle
        /// around alpha 0.2. This sits just off white with a blue bias so it reads as ice
        /// rather than as another bullet shield, and stays only slightly more solid than
        /// vanilla because the player has to see exactly who got caught.
        /// </summary>
        public static readonly Color DefaultDomeColor = new Color(0.75f, 0.90f, 1f, 0.40f);
    }
}
