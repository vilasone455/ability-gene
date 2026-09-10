using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The dome is the same visual vanilla draws for projectile-interceptor shields
    /// (mech cluster shields, and the Royalty Bullet Shield psycast via
    /// BulletShieldPsychic): the "Other/ForceField" texture on a plane10 quad under the
    /// MoteGlow shader, scaled to the field radius and tinted through a
    /// MaterialPropertyBlock.
    ///
    /// Nothing here may be touched during def parsing - see <see cref="TimeBubbleDefaults"/>.
    /// The material is resolved lazily rather than in a field initializer so that even if
    /// something does reach this class early, the texture is still only fetched from the
    /// draw path, which is always the main thread.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TimeBubbleGraphics
    {
        private static Material fieldMat;

        public static Material FieldMat
        {
            get
            {
                if (fieldMat == null)
                {
                    fieldMat = MaterialPool.MatFrom("Other/ForceField", ShaderDatabase.MoteGlow);
                }
                return fieldMat;
            }
        }

        /// <summary>
        /// The visible ring occupies less than the full quad, so the quad has to be
        /// scaled past the true radius for the ring to land on it. Vanilla calls this
        /// TextureActualRingSizeFactor and uses the same number.
        /// </summary>
        public const float TextureRingSizeFactor = 1.1601562f;

        public static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
    }
}
