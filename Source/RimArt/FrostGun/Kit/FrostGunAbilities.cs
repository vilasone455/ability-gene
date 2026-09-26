using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_FlashFreeze : CompProperties_AbilityEffect
    {
        /// <summary>Coolant one cast spends.</summary>
        public float cost = 10f;
        /// <summary>Seconds the target stays frozen (stunned) if nothing breaks the ice.</summary>
        public float freezeSeconds = 5f;
        /// <summary>The target must be Soaked, or carry at least this many Chilled stacks.</summary>
        public int requiredChilledStacks = 3;
        /// <summary>The freeze uses up what allowed it: Soaked and every Chilled stack are removed.</summary>
        public bool consumeCondition = true;
        /// <summary>The next damage the frozen pawn takes shatters the ice, which deals this much more.</summary>
        public float shatterDamage = 15f;
        public DamageDef shatterDamageDef;
        public float shatterArmorPenetration = 0.2f;
        public SoundDef soundFreeze, soundShatter, soundThaw;

        public CompProperties_FlashFreeze()
        {
            compClass = typeof(CompAbilityEffect_FlashFreeze);
        }
    }

    /// <summary>
    /// Flash Freeze. One pawn in line of sight that is Soaked (the water gun) or carries the required
    /// Chilled stacks (the rifle's shots) is frozen solid in ice: stunned, without the stun mote, for
    /// freezeSeconds. The next damage it takes shatters the ice for shatterDamage more; otherwise it
    /// thaws. The freeze lands when the picture's beam arrives (MapComponent_FrostGun).
    /// </summary>
    public class CompAbilityEffect_FlashFreeze : CompAbilityEffect
    {
        public new CompProperties_FlashFreeze Props => (CompProperties_FlashFreeze)props;

        private CompFrostGun Gun => CompFrostGun.HeldBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        private string Unavailable()
        {
            CompFrostGun gun = Gun;
            if (gun == null) return "Requires a frost gun in hand.";
            if (gun.Coolant + 0.0001f < Props.cost)
                return "Not enough coolant: needs " + Props.cost.ToString("0") + ", the tank has " + gun.Units + ".";
            return null;
        }

        /// <summary>Why this target cannot be frozen, or null if it can.</summary>
        public string Refusal(LocalTargetInfo target) => FlashFreeze.Refusal(target.Pawn, Props);

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string refusal = Refusal(target);
            if (refusal != null)
            {
                if (throwMessages) Messages.Message(refusal, target.Thing, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Refusal(target) == null && base.CanApplyOn(target, dest);

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target) => Refusal(target);

        public override string ExtraTooltipPart()
        {
            CompFrostGun gun = Gun;
            return gun == null ? null : "Coolant: " + gun.LabelRemaining;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompFrostGun gun = Gun;
            if (caster?.Map == null || gun == null || !(target.Thing is Pawn victim) || gun.Coolant + 0.0001f < Props.cost) return;
            gun.Spend(Props.cost);
            caster.Map.GetComponent<MapComponent_FrostGun>().Land(caster, victim, parent.def.verbProperties.warmupTime, Props);
        }
    }

    /// <summary>The Chilled stacks the rifle's bolts leave, and who can be frozen.</summary>
    public static class FlashFreeze
    {
        private static CompProperties_FlashFreeze props;

        /// <summary>Flash Freeze's properties from its def.</summary>
        public static CompProperties_FlashFreeze Props =>
            props ?? (props = FrostGunDefOf.AG_FrostGun_FlashFreeze?.comps?.Find(c => c is CompProperties_FlashFreeze) as CompProperties_FlashFreeze);

        public static int ChilledStacks(Pawn pawn)
        {
            Hediff chilled = pawn?.health?.hediffSet?.GetFirstHediffOfDef(FrostGunDefOf.AG_FrostGunChilled);
            return chilled == null ? 0 : Mathf.RoundToInt(chilled.Severity);
        }

        public static bool IsFrozen(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(FrostGunDefOf.AG_FrostGunFrozen) != null;

        /// <summary>Adds a Chilled stack (up to <paramref name="maxStacks"/>) and sets it to last <paramref name="seconds"/> again.</summary>
        public static void AddChill(Pawn pawn, int maxStacks, float seconds)
        {
            if (pawn?.health == null || pawn.Dead || maxStacks <= 0) return;
            Hediff chilled = pawn.health.hediffSet.GetFirstHediffOfDef(FrostGunDefOf.AG_FrostGunChilled);
            if (chilled == null)
            {
                chilled = HediffMaker.MakeHediff(FrostGunDefOf.AG_FrostGunChilled, pawn);
                chilled.Severity = 1f;
                pawn.health.AddHediff(chilled);
            }
            else chilled.Severity = Mathf.Min(maxStacks, Mathf.Round(chilled.Severity) + 1f);
            chilled.TryGetComp<HediffComp_Disappears>()?.SetDuration(Mathf.RoundToInt(seconds * 60f));
            pawn.MapHeld?.GetComponent<MapComponent_FrostGun>()?.Chilled(pawn);
        }

        public static string Refusal(Pawn pawn, CompProperties_FlashFreeze p)
        {
            if (pawn == null) return "Flash Freeze needs a pawn as its target.";
            if (pawn.Dead) return pawn.LabelShortCap + " is dead.";
            if (IsFrozen(pawn)) return pawn.LabelShortCap + " is already frozen.";
            if (pawn.Downed) return pawn.LabelShortCap + " is down.";
            int stacks = ChilledStacks(pawn), needed = p?.requiredChilledStacks ?? 3;
            if (!WaterGunSoak.IsSoaked(pawn) && stacks < needed)
                return pawn.LabelShortCap + " must be soaked or chilled " + needed + " times (chilled " + stacks + ").";
            return null;
        }
    }
}
