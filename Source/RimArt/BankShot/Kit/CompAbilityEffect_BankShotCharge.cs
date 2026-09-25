using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_BankShotCharge : CompProperties_AbilityEffect
    {
        /// <summary>Damage with no bounce, and what each bounce taken adds: 18 / 24 / 30 / 36.</summary>
        public float baseDamage = 18f;
        public float damagePerBounce = 6f;
        /// <summary>Armour penetration per point of damage. 0.015 is the rule vanilla bullets use when their def gives none.</summary>
        public float armorPenetrationPerDamage = 0.015f;
        /// <summary>Wall contacts that bounce; the next contact embeds.</summary>
        public int maxBounces = 3;
        /// <summary>Cells of flight in all, bounces included.</summary>
        public float range = 30f;
        /// <summary>Cells per second. The hit lands when the drawn bullet reaches the pawn.</summary>
        public float speed = 28f;
        /// <summary>Bullet (sharp) when left out.</summary>
        public DamageDef damageDef;
        public SoundDef soundFire;

        public CompProperties_BankShotCharge()
        {
            compClass = typeof(CompAbilityEffect_BankShotCharge);
        }
    }

    /// <summary>
    /// Bank Shot's charge mode. The target cell gives the aim only; the bullet then follows the
    /// ricochet rule on the map (BankShotMap). While the player aims, the whole flight is drawn with
    /// the picture's own aim line: dashes along every leg, a square on the first wall cell it meets,
    /// a dot at each contact. The charge (the ability's warmup) and the flight are played by
    /// MapComponent_BankShot, which deals the hit when the drawn bullet reaches the pawn.
    /// </summary>
    public class CompAbilityEffect_BankShotCharge : CompAbilityEffect
    {
        public new CompProperties_BankShotCharge Props => (CompProperties_BankShotCharge)props;

        private float Charge => parent.def.verbProperties?.warmupTime ?? 0f;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid) return;
            caster.Map.GetComponent<MapComponent_BankShot>().Fire(caster, target.Cell, Charge, Props);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
            target.IsValid && parent.pawn != null && target.Cell != parent.pawn.Position && base.Valid(target, throwMessages);

        public override bool AICanTargetNow(LocalTargetInfo target) => false;

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid || target.Cell == caster.Position) return;
            BankShotGraphics.DrawAim(BankShotMap.Shot(caster, target.Cell, Charge, Props), caster.Map);
        }

        /// <summary>What the shot will do, under the mouse: the bounces and the damage, or where it stops.</summary>
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid || target.Cell == caster.Position) return null;
            BankShotPath path = BankShotMap.Shot(caster, target.Cell, Charge, Props).Path;
            int bounces = path.Bounces.Count;
            Pawn victim = BankShotMap.Victim(caster.Map, path, caster);
            if (victim != null)
                return bounces + " bounce" + (bounces == 1 ? "" : "s") + ": " + Mathf.RoundToInt(BankShotMap.Damage(Props, bounces)) + " damage to " + victim.LabelShort;
            return bounces + " bounce" + (bounces == 1 ? "" : "s") + (path.End == BankShotEnd.Embed ? ", then embeds" : ", then spent");
        }
    }
}
