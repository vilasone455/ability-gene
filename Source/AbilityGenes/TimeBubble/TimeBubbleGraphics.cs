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
    }
}
