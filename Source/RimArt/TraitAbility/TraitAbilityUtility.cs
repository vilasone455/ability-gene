using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    public static class TraitAbilityUtility
    {
        public static void GrantAbilities(Pawn pawn, Trait trait)
        {
            if (pawn?.abilities == null) return;

            List<AbilityDef> abilities = AbilitiesFor(trait);
            if (abilities == null) return;

            for (int i = 0; i < abilities.Count; i++)
                pawn.abilities.GainAbility(abilities[i]);
        }

        public static void RevokeAbilities(Pawn pawn, Trait trait)
        {
            if (pawn?.abilities == null) return;

            List<AbilityDef> abilities = AbilitiesFor(trait);
            if (abilities == null) return;

            for (int i = 0; i < abilities.Count; i++)
            {
                if (!GrantedByOtherSource(pawn, abilities[i], trait))
                    pawn.abilities.RemoveAbility(abilities[i]);
            }
        }

        public static void SyncAll(Pawn pawn)
        {
            if (pawn?.story?.traits == null || pawn.abilities == null) return;

            List<Trait> traits = pawn.story.traits.allTraits;
            for (int i = 0; i < traits.Count; i++)
                GrantAbilities(pawn, traits[i]);
        }

        private static List<AbilityDef> AbilitiesFor(Trait trait)
        {
            TraitAbilityExtension ext = trait.def.GetModExtension<TraitAbilityExtension>();
            if (ext?.abilities == null) return null;
            if (ext.degree != int.MinValue && trait.Degree != ext.degree) return null;
            return ext.abilities;
        }

        private static bool GrantedByOtherSource(Pawn pawn, AbilityDef ability, Trait exclude)
        {
            if (pawn.genes != null)
            {
                List<Gene> genes = pawn.genes.GenesListForReading;
                for (int i = 0; i < genes.Count; i++)
                {
                    if (genes[i].def.abilities != null && genes[i].def.abilities.Contains(ability))
                        return true;
                }
            }

            if (pawn.story?.traits != null)
            {
                List<Trait> traits = pawn.story.traits.allTraits;
                for (int i = 0; i < traits.Count; i++)
                {
                    if (traits[i] == exclude) continue;
                    List<AbilityDef> other = AbilitiesFor(traits[i]);
                    if (other != null && other.Contains(ability))
                        return true;
                }
            }

            return false;
        }
    }
}
