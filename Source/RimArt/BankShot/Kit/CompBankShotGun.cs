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
    /// put down. The gun's normal shots are its own verb, and the charge is an ability.
    /// <see cref="ItemAbilityGrant"/> keeps its cooldown on the gun.
    /// </summary>
    public class CompBankShotGun : ThingComp
    {
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_BankShotGun Props => (CompProperties_BankShotGun)props;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            grant.Give(pawn, Props.abilities);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            grant.Take(pawn, Props.abilities, this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            grant.ExposeData();
        }
    }
}
