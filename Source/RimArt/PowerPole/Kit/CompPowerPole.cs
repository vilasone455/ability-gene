using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_PowerPole : CompProperties
    {
        public List<AbilityDef> abilities;

        public CompProperties_PowerPole()
        {
            compClass = typeof(CompPowerPole);
        }
    }

    /// <summary>
    /// Grants the pole's three abilities to whoever holds it, and takes them back when it is put
    /// down. <see cref="ItemAbilityGrant"/> keeps their cooldowns on the pole.
    /// </summary>
    public class CompPowerPole : ThingComp
    {
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_PowerPole Props => (CompProperties_PowerPole)props;

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
