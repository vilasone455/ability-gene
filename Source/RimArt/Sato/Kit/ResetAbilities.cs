using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_HeadshotReset : CompProperties_AbilityEffect
    {
        /// <summary>How long he plays dead: the Reset's delay for this cause, in seconds.</summary>
        public float resetSeconds = 6f;

        public CompProperties_HeadshotReset() { compClass = typeof(CompAbilityEffect_HeadshotReset); }
    }

    /// <summary>
    /// Headshot Reset: the shot starts a Reset in place with a short delay. It is paid like any Reset (the
    /// body's cost), and the button is greyed while the pool cannot pay, so it never becomes the slow Reset.
    /// </summary>
    public class CompAbilityEffect_HeadshotReset : CompAbilityEffect
    {
        public new CompProperties_HeadshotReset Props => (CompProperties_HeadshotReset)props;

        public override bool GizmoDisabled(out string reason)
        {
            float cost = AjinExtension.Get.Cost(AjinPiece.Body);
            if ((GameComponent_Echoes.Get?.charge ?? 0f) < cost)
            {
                reason = "AG_HeadshotNeedsCharge".Translate(cost.ToString("0"));
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (!AjinReset.IsAjin(pawn)) return;
            if (pawn.Spawned) SatoPictures.HeadshotFired(pawn);
            AjinReset.Start(pawn, AjinCause.Headshot, Props.resetSeconds.SecondsToTicks());
        }
    }

    public class CompProperties_GrenadeReset : CompProperties_AbilityEffect
    {
        public float radius = 3f;
        public int damage = 50;
        public float armorPenetration = 0.3f;
        public DamageDef damageDef;

        public CompProperties_GrenadeReset() { compClass = typeof(CompAbilityEffect_GrenadeReset); }
    }

    /// <summary>
    /// Grenade Reset: a vanilla explosion on his own cell with him as the instigator. The blast is what Resets
    /// him (its damage is on the Ajin explosion list), so it behaves exactly as any other explosion would.
    /// </summary>
    public class CompAbilityEffect_GrenadeReset : CompAbilityEffect
    {
        public new CompProperties_GrenadeReset Props => (CompProperties_GrenadeReset)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (!pawn.Spawned) return;
            DamageDef damage = Props.damageDef ?? DamageDefOf.Bomb;
            GenExplosion.DoExplosion(pawn.Position, pawn.Map, Props.radius, damage, pawn, Props.damage, Props.armorPenetration,
                damage.soundExplosion);
        }
    }

    public class CompProperties_TheGame : CompProperties_AbilityEffect
    {
        public float markSeconds = 30f;
        /// <summary>Multiplies his ranged damage on the marked pawn.</summary>
        public float damageFactor = 1.3f;
        /// <summary>Charge back to the pool when the marked pawn dies while marked, to anyone.</summary>
        public float killRefund = 15f;

        public CompProperties_TheGame() { compClass = typeof(CompAbilityEffect_TheGame); }
    }

    /// <summary>The Game: marks one enemy. One mark at a time: a new one clears his old one.</summary>
    public class CompAbilityEffect_TheGame : CompAbilityEffect
    {
        public new CompProperties_TheGame Props => (CompProperties_TheGame)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn victim = target.Pawn;
            if (victim == null || victim == parent.pawn || victim.Dead || !victim.HostileTo(parent.pawn))
            {
                if (throwMessages) Messages.Message("AG_TheGameEnemiesOnly".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn marker = parent.pawn, victim = target.Pawn;
            if (victim == null || victim.Dead) return;
            TheGame.ClearMarksBy(marker);
            var mark = (Hediff_SatoGameMark)HediffMaker.MakeHediff(SatoDefOf.AG_SatoGameMark, victim);
            mark.marker = marker;
            mark.damageFactor = Props.damageFactor;
            mark.killRefund = Props.killRefund;
            HediffComp_Disappears disappears = mark.TryGetComp<HediffComp_Disappears>();
            if (disappears != null) disappears.ticksToDisappear = Props.markSeconds.SecondsToTicks();
            victim.health.AddHediff(mark);
        }
    }
}
