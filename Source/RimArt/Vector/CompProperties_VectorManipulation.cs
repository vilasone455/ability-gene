using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Vector manipulation's balance numbers on its AbilityDef, so a rebalance is an XML edit. The ability
    /// has no effect of its own to apply (Command_VectorEdit and VectorEditSession do the work), so the comp
    /// class is the plain one and does nothing.
    /// </summary>
    public class CompProperties_VectorManipulation : CompProperties_AbilityEffect
    {
        /// <summary>Strain added by one Apply, indexed by how many groups it changed less one.</summary>
        public List<float> strainCosts = new List<float> { 0.08f, 0.24f, 0.48f, 0.80f };

        public CompProperties_VectorManipulation()
        {
            compClass = typeof(CompAbilityEffect);
        }
    }
}
