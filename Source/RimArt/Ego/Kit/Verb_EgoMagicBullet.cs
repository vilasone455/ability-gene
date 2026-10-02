using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Magic Bullet's shot: Core's shooting verb (warmup, cooldown, skill gain, the battle log's "fired at", the Corrosion
    /// roll through Notify_UsedWeapon) with the projectile replaced by <see cref="EgoMagicBulletCast"/>'s line, which never
    /// misses, ignores cover and walls, and hits every pawn on it, and the target when it is not a pawn. The def sets requireLineOfSight false, so a target behind
    /// a wall can be ordered; drafted pawns still only fire by themselves at what they see. The verb's defaultProjectile
    /// (AG_EgoMagicBullet_Round) is never spawned: it carries the damage, the armour penetration and the info card's
    /// numbers. After the seventh the verb is unavailable for the cooldown, so the wielder falls back to melee.
    /// </summary>
    public class Verb_EgoMagicBullet : Verb_Shoot
    {
        private CompEgoMagicBullet Gun => EquipmentSource?.GetComp<CompEgoMagicBullet>();

        public override bool Available() => base.Available() && Gun?.CoolingDown != true;

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            CompEgoMagicBullet gun = Gun;
            if (gun?.CoolingDown == true) return false;
            if (!base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast)) return false;
            if (gun != null && CasterIsPawn && CasterPawn.stances.curStance is Stance_Warmup warmup && warmup.verb == this)
                GameComponent_EgoMagicBullet.Instance?.Begin(EgoMagicBulletCast.Aim(CasterPawn, gun, this, castTarg, warmup.ticksLeft));
            return true;
        }

        protected override bool TryCastShot()
        {
            CompEgoMagicBullet gun = Gun;
            if (gun == null || gun.CoolingDown || !CasterIsPawn || (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)) return false;
            GameComponent_EgoMagicBullet game = GameComponent_EgoMagicBullet.Instance;
            if (game == null) return false;
            lastShotTick = Find.TickManager.TicksGame;
            game.Shoot(CasterPawn, gun, this, currentTarget);
            CasterPawn.records.Increment(RecordDefOf.ShotsFired);
            return true;
        }

        /// <summary>
        /// While aiming: the range ring, and the line the shot would take, to the range; on six, the seventh's line to the
        /// pawn it will hit instead, in red.
        /// </summary>
        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            CompEgoMagicBullet gun = Gun;
            if (gun == null || !CasterIsPawn || !caster.Spawned) return;
            Vector3 from = caster.Position.ToVector3Shifted();
            if (gun.NextIsSeventh)
            {
                Pawn beloved = EgoMagicBullet.Beloved(CasterPawn);
                if (beloved != CasterPawn) GenDraw.DrawLineBetween(from, beloved.DrawPos, SimpleColor.Red);
                return;
            }
            if (!target.IsValid) return;
            Vector3 d = target.Cell.ToVector3Shifted() - from;
            if (d.sqrMagnitude < 0.01f) return;
            GenDraw.DrawLineBetween(from, from + d.normalized * EffectiveRange, SimpleColor.White, 0.1f);
        }
    }
}
