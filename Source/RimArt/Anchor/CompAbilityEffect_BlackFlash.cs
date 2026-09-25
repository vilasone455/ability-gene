using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    public class CompProperties_AbilityBlackFlash : CompProperties_AbilityEffect
    {
        /// <summary>The punch's damage before the melee damage stat and the flash.</summary>
        public float damage = 9f;

        public float armorPenetration = 0.15f;

        public DamageDef damageDef;

        /// <summary>Ticks after a clap swap during which the punch is a Black Flash. 180 = 3 s.</summary>
        public int windowTicks = 180;

        /// <summary>Damage factor of a Black Flash.</summary>
        public float flashFactor = 2.5f;

        /// <summary>Ticks a Black Flash stuns the target.</summary>
        public int stunTicks = 60;

        /// <summary>Given to the carrier after a Black Flash; its def holds the length and the stat offsets.</summary>
        public HediffDef zoneHediff;

        public CompProperties_AbilityBlackFlash()
        {
            compClass = typeof(CompAbilityEffect_BlackFlash);
        }
    }

    /// <summary>
    /// A bare-handed punch. Within windowTicks of a clap that moved someone it is a Black Flash:
    /// flashFactor damage, a stun, and the carrier is in the zone for a while. One Black Flash per
    /// swap - the hit spends the window. Outside the window it is an ordinary punch, so the button is
    /// never dead but only pays after a clap.
    ///
    /// The window is read when the hit lands, not when it is ordered: the carrier may walk to the
    /// target first and the walk counts against the 3 seconds.
    /// </summary>
    public class CompAbilityEffect_BlackFlash : CompAbilityEffect
    {
        public new CompProperties_AbilityBlackFlash Props => (CompProperties_AbilityBlackFlash)props;

        public bool InWindow(Gene_Anchors gene)
        {
            if (gene == null) return false;
            int since = gene.TicksSinceSwap;
            return since >= 0 && since <= Props.windowTicks;
        }

        /// <summary>Seconds of window left, or 0 when shut.</summary>
        public float WindowSecondsLeft(Gene_Anchors gene)
        {
            return InWindow(gene) ? (Props.windowTicks - gene.TicksSinceSwap) / 60f : 0f;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Thing victim = target.Thing;
            if (caster == null || victim == null || !victim.Spawned) return;

            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            bool flash = InWindow(gene);
            Map map = caster.Map;
            Vector3 at = victim.DrawPos;

            float amount = Props.damage * caster.GetStatValue(StatDefOf.MeleeDamageFactor);
            if (flash) amount *= Props.flashFactor;
            float angle = (victim.Position - caster.Position).AngleFlat;
            victim.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Blunt, amount, Props.armorPenetration, angle, caster,
                null, null, DamageInfo.SourceCategory.ThingOrUnknown, victim));

            if (!flash)
            {
                SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(victim.Position, map));
                return;
            }

            gene.SpendSwap();
            if (victim is Pawn pawn && pawn.Spawned && !pawn.Dead) pawn.stances?.stunner?.StunFor(Props.stunTicks, caster, false, true);
            if (Props.zoneHediff != null)
            {
                Hediff old = caster.health.hediffSet.GetFirstHediffOfDef(Props.zoneHediff);
                if (old != null) caster.health.RemoveHediff(old);
                caster.health.AddHediff(Props.zoneHediff);
            }

            // Placeholder picture and sound until the Black Flash sketch is made.
            SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(victim.Position, map));
            FleckMaker.Static(at, map, FleckDefOf.ExplosionFlash, 3f);
            for (int i = 0; i < 4; i++) FleckMaker.ThrowMicroSparks(at, map);
            MoteMaker.ThrowText(at, map, "AG_BlackFlashText".Translate(), new Color(0.9f, 0.1f, 0.15f), 2f);
        }
    }
}
