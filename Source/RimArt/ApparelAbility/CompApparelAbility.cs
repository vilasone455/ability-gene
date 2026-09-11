using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_ApparelAbility : CompProperties
    {
        public List<AbilityDef> abilities;

        public CompProperties_ApparelAbility()
        {
            compClass = typeof(CompApparelAbility);
        }
    }

    /// <summary>
    /// Grants abilities to whoever is wearing the item, and takes them back when it comes off.
    ///
    /// Vanilla has no comp that does this. CompEquippableAbility replaces CompEquippable and so
    /// only works on a weapon, and CompApparelVerbOwner grants a Verb rather than an AbilityDef,
    /// which is the wrong shape for an ability that carries its own effect comps.
    ///
    /// The cooldown is stored here, on the item, rather than being left on the pawn's Ability.
    /// That is the whole reason this class holds state. An ability granted on equip starts with a
    /// clean cooldown, so a five-day field would otherwise recharge instantly by taking the belt
    /// off and putting it back on. Recording the tick the charge completes and reapplying the
    /// remainder on the next wearer makes the cooldown belong to the device, which is also the
    /// more honest reading: it is the belt winding back up, not the person.
    /// </summary>
    public class CompApparelAbility : ThingComp
    {
        private int cooldownCompleteTick = -1;

        public CompProperties_ApparelAbility Props => (CompProperties_ApparelAbility)props;

        public void GrantTo(Pawn pawn)
        {
            if (pawn?.abilities == null || Props.abilities == null) return;

            int remaining = cooldownCompleteTick - Find.TickManager.TicksGame;
            for (int i = 0; i < Props.abilities.Count; i++)
            {
                AbilityDef def = Props.abilities[i];
                pawn.abilities.GainAbility(def);

                if (remaining <= 0) continue;
                Ability ability = pawn.abilities.GetAbility(def, true);
                if (ability != null) ability.StartCooldown(remaining);
            }
        }

        public void RevokeFrom(Pawn pawn)
        {
            if (pawn?.abilities == null || Props.abilities == null) return;

            for (int i = 0; i < Props.abilities.Count; i++)
            {
                AbilityDef def = Props.abilities[i];
                Ability ability = pawn.abilities.GetAbility(def, true);

                // Longest remaining charge wins, so a belt cannot be freshened by swapping it
                // onto a second pawn and back.
                if (ability != null && ability.CooldownTicksRemaining > 0)
                {
                    int complete = Find.TickManager.TicksGame + ability.CooldownTicksRemaining;
                    if (complete > cooldownCompleteTick) cooldownCompleteTick = complete;
                }

                if (!TraitAbilityUtility.GrantedByOtherSource(pawn, def, null, this))
                    pawn.abilities.RemoveAbility(def);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cooldownCompleteTick, "cooldownCompleteTick", -1);
        }
    }
}
