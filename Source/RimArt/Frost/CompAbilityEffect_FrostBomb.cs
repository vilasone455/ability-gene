using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityFrostBomb : CompProperties_AbilityEffect
    {
        /// <summary>
        /// The texture drawn in the thrower's hand during the wind-up. Null keeps whatever the
        /// animation itself specifies, which is this same bomb - it is named here so that a
        /// second thrown thing can reuse the one clip.
        /// </summary>
        public string handTexture = "RimArt/Frost/Bomb";

        public CompProperties_AbilityFrostBomb()
        {
            compClass = typeof(CompAbilityEffect_FrostBomb);
        }
    }

    /// <summary>
    /// Throws the bomb.
    ///
    /// The cast does almost nothing itself, and that is the point: it hands the throw to
    /// <see cref="MapComponent_Throws"/>, which plays the animation if Melee Animation is loaded
    /// and launches the projectile when the thrown hand opens. Without that mod the same call
    /// launches immediately, so this ability has no branch in it for the two cases and no way to
    /// work in one of them and not the other.
    ///
    /// Nothing about the freeze lives here. The projectile carries it, which is what makes the
    /// bomb behave like a grenade in every system that handles grenades - it can be shot down, it
    /// bounces off a wall the thrower misjudged, and a pawn killed mid-wind-up never throws it.
    /// </summary>
    public class CompAbilityEffect_FrostBomb : CompAbilityEffect
    {
        public new CompProperties_AbilityFrostBomb Props => (CompProperties_AbilityFrostBomb)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn thrower = parent.pawn;
            if (thrower?.Map == null || !target.IsValid) return;

            MapComponent_Throws.Begin(thrower, target, FrostDefOf.AG_FrostBombProjectile, Props.handTexture);
        }

        /// <summary>
        /// The cloud the player is about to make, drawn under the cursor.
        ///
        /// Worth the few lines because the bomb catches allies. A radius that is only discovered
        /// after the throw is a trap; a radius drawn before it is a decision.
        /// </summary>
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(target.Cell, FrostDefaults.BurstRadius);
        }

        public override bool AICanTargetNow(LocalTargetInfo target) => false;
    }
}
