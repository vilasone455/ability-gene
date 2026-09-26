using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Grants a held item's abilities to whoever holds it and takes them back when it is put down,
    /// for the kit weapons (Power Pole, Bank Shot pistol, tag scroll, water gun, bubble pipe). Core's
    /// CompEquippableAbility grants one ability; these grant several.
    ///
    /// Each cooldown is kept here, on the item, as CompApparelAbility keeps a belt's: an ability
    /// granted on equip starts ready, so without this a cooldown would be skipped by dropping the
    /// item and picking it up again.
    ///
    /// The owning comp calls <see cref="ExposeData"/> from its PostExposeData, so the cooldowns save
    /// under the comp's own "readyAtTick" node.
    /// </summary>
    public sealed class ItemAbilityGrant
    {
        private Dictionary<AbilityDef, int> readyAtTick = new Dictionary<AbilityDef, int>();
        private List<AbilityDef> scribeDefs;
        private List<int> scribeTicks;

        public void Give(Pawn pawn, List<AbilityDef> abilities)
        {
            if (pawn?.abilities == null || abilities == null) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < abilities.Count; i++)
            {
                AbilityDef def = abilities[i];
                pawn.abilities.GainAbility(def);
                if (!readyAtTick.TryGetValue(def, out int ready) || ready <= now) continue;
                pawn.abilities.GetAbility(def, true)?.StartCooldown(ready - now);
            }
        }

        /// <param name="source">The owning comp, so another source granting the same ability keeps it on the pawn.</param>
        public void Take(Pawn pawn, List<AbilityDef> abilities, ThingComp source)
        {
            if (pawn?.abilities == null || abilities == null) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < abilities.Count; i++)
            {
                AbilityDef def = abilities[i];
                Ability ability = pawn.abilities.GetAbility(def, true);
                if (ability != null && ability.CooldownTicksRemaining > 0) readyAtTick[def] = now + ability.CooldownTicksRemaining;
                else readyAtTick.Remove(def);

                if (!TraitAbilityUtility.GrantedByOtherSource(pawn, def, null, source))
                    pawn.abilities.RemoveAbility(def);
            }
        }

        /// <summary>Forgets a kept cooldown, so the ability comes back ready: a cast refunded after the ability was taken.</summary>
        public void Forget(AbilityDef def) => readyAtTick.Remove(def);

        public void ExposeData()
        {
            Scribe_Collections.Look(ref readyAtTick, "readyAtTick", LookMode.Def, LookMode.Value, ref scribeDefs, ref scribeTicks);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readyAtTick == null) readyAtTick = new Dictionary<AbilityDef, int>();
        }
    }
}
