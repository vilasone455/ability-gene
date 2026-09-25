using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_BankShotGun : CompProperties
    {
        /// <summary>Granted while the gun is equipped: AG_BankShot_Charge.</summary>
        public List<AbilityDef> abilities;

        public CompProperties_BankShotGun()
        {
            compClass = typeof(CompBankShotGun);
        }
    }

    /// <summary>
    /// Grants the Bank Shot pistol's charge mode to whoever holds it, and takes it back when it is
    /// put down. A copy of CompPowerPole: the gun's normal shots are its own verb, and the
    /// charge is an ability.
    ///
    /// The cooldown is kept here, on the weapon, as CompApparelAbility keeps a belt's: an ability
    /// granted on equip starts ready, so without this the cooldown would be skipped by dropping the
    /// gun and picking it up again.
    /// </summary>
    public class CompBankShotGun : ThingComp
    {
        private Dictionary<AbilityDef, int> readyAtTick = new Dictionary<AbilityDef, int>();
        private List<AbilityDef> scribeDefs;
        private List<int> scribeTicks;

        public CompProperties_BankShotGun Props => (CompProperties_BankShotGun)props;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (pawn?.abilities == null || Props.abilities == null) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < Props.abilities.Count; i++)
            {
                AbilityDef def = Props.abilities[i];
                pawn.abilities.GainAbility(def);
                if (!readyAtTick.TryGetValue(def, out int ready) || ready <= now) continue;
                pawn.abilities.GetAbility(def, true)?.StartCooldown(ready - now);
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (pawn?.abilities == null || Props.abilities == null) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < Props.abilities.Count; i++)
            {
                AbilityDef def = Props.abilities[i];
                Ability ability = pawn.abilities.GetAbility(def, true);
                if (ability != null && ability.CooldownTicksRemaining > 0) readyAtTick[def] = now + ability.CooldownTicksRemaining;
                else readyAtTick.Remove(def);

                if (!TraitAbilityUtility.GrantedByOtherSource(pawn, def, null, this))
                    pawn.abilities.RemoveAbility(def);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref readyAtTick, "readyAtTick", LookMode.Def, LookMode.Value, ref scribeDefs, ref scribeTicks);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readyAtTick == null) readyAtTick = new Dictionary<AbilityDef, int>();
        }
    }
}
