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
                if (!GrantedByOtherSource(pawn, abilities[i], trait, null))
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

        /// <summary>
        /// Whether anything other than the named source still grants this ability, so revoking
        /// one source does not take an ability the pawn has earned twice over.
        /// </summary>
        public static bool GrantedByOtherSource(Pawn pawn, AbilityDef ability, Trait exclude,
            ThingComp excludeComp)
        {
            // A manifested Echo. EchoUtility.Revert clears manifested before it takes, so the Echo
            // never counts as another source of its own abilities.
            if (EchoUtility.ManifestedWith(pawn, ability) != null) return true;

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

            if (pawn.health?.hediffSet != null)
            {
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                for (int i = 0; i < hediffs.Count; i++)
                {
                    if (hediffs[i].def.abilities != null && hediffs[i].def.abilities.Contains(ability))
                        return true;
                }
            }

            if (pawn.equipment != null)
            {
                List<ThingWithComps> equips = pawn.equipment.AllEquipmentListForReading;
                for (int i = 0; i < equips.Count; i++)
                {
                    CompEquippableAbilityReloadable comp = equips[i].TryGetComp<CompEquippableAbilityReloadable>();
                    if (comp?.Props?.abilityDef == ability)
                        return true;
                }
            }

            if (pawn.apparel != null)
            {
                List<Apparel> worn = pawn.apparel.WornApparel;
                for (int i = 0; i < worn.Count; i++)
                {
                    CompEquippableAbilityReloadable comp = worn[i].TryGetComp<CompEquippableAbilityReloadable>();
                    if (comp?.Props?.abilityDef == ability)
                        return true;

                    CompApparelAbility granter = worn[i].TryGetComp<CompApparelAbility>();
                    if (granter != null && granter != excludeComp
                        && granter.Props?.abilities != null && granter.Props.abilities.Contains(ability))
                        return true;
                }
            }

            return false;
        }
    }
}
