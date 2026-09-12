using Verse;

namespace RimArt
{
    /// <summary>
    /// The frost bomb's throw.
    ///
    /// Everything about putting a thing in a hand, waiting for the arm to come round and honouring
    /// the forced-miss model lives in <see cref="Verb_ThrowByHand"/>, which this was until the
    /// mimic beacon needed the same three behaviours. What is left here is the two things that are
    /// actually the frost bomb's: what the hand holds, and what the cursor draws.
    ///
    /// The bomb declares a forced miss radius of 0.4 and so never scatters: it is pinpoint by
    /// choice, and the value is not zero only because the config check tests "&gt; 0f" while the
    /// scattering code tests "&gt; 0.5f", leaving a gap that satisfies one without waking the other.
    /// The full model is kept in the base class anyway - it is a dozen lines, and it means going
    /// back to a grenade that misses is an XML edit rather than a rewrite.
    /// </summary>
    public class Verb_ThrowFrostBomb : Verb_ThrowByHand
    {
        protected override string HandTexture => "RimArt/Frost/Bomb";

        /// <summary>
        /// The cloud the player is about to make, drawn under the cursor while aiming.
        ///
        /// Worth overriding because the freeze is wider than the blast: the base class reports the
        /// projectile's explosionRadius, and the cold reaches <see cref="FrostDefaults.BurstRadius"/>
        /// past it. Drawing the smaller ring would put the edge of the picture inside the edge of
        /// the effect, which on an item that catches allies is the one error worth spending a
        /// method on. A radius discovered after the throw is a trap; a radius drawn before it is
        /// a decision.
        ///
        /// needLOSToCenter stays true, which gets the blast-shaped preview rather than a plain
        /// disc - and that is also the truth, since the burst tests line of sight from its centre
        /// exactly as the explosion does.
        /// </summary>
        public override float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
        {
            needLOSToCenter = true;
            return FrostDefaults.BurstRadius;
        }
    }
}
