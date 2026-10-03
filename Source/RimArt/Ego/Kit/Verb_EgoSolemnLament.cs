using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Solemn Lament's pair: Core's shooting verb (warmup, burst, cooldown, skill gain, the Corrosion roll per shot through
    /// Notify_UsedWeapon) with the projectile replaced by a hit on the target, which never misses. The guns take turns,
    /// white first and on across bursts (<see cref="CompEgoSolemnLament.Spend"/>). White: no harm, whiteStacks Butterfly
    /// stacks. Black: the round's damage (AG_EgoSolemnLament_Round) and blackStacks stacks. Stacks never go past the cap,
    /// so the guns down a pawn at the cap and never kill it with Butterfly; a downed target still takes black damage. A
    /// target that is not a pawn takes only the black damage. The pool runs dry after ammo shots and the verb is
    /// unavailable while it reloads. The round is never spawned.
    /// </summary>
    public class Verb_EgoSolemnLament : Verb_Shoot
    {
        private CompEgoSolemnLament Gun => EquipmentSource?.GetComp<CompEgoSolemnLament>();

        public override bool Available() => base.Available() && Gun?.Ammo != 0;

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            CompEgoSolemnLament gun = Gun;
            if (gun?.Ammo == 0) return false;
            if (!base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast)) return false;
            if (gun != null && CasterIsPawn && CasterPawn.stances.curStance is Stance_Warmup warmup && warmup.verb == this)
                GameComponent_EgoSolemnLament.Instance?.Begin(new EgoSolemnLamentBurstCast(CasterPawn, this, castTarg));
            return true;
        }

        protected override bool TryCastShot()
        {
            CompEgoSolemnLament gun = Gun;
            GameComponent_EgoSolemnLament game = GameComponent_EgoSolemnLament.Instance;
            Thing thing = currentTarget.Thing;
            if (gun == null || game == null || gun.Ammo <= 0 || !CasterIsPawn || (thing != null && (thing.Map != caster.Map || thing.Destroyed))) return false;
            int now = Find.TickManager.TicksGame;
            lastShotTick = now;
            bool white = gun.Spend();
            CompProperties_EgoSolemnLament p = gun.Props;
            Vector3 at = thing?.DrawPos ?? currentTarget.Cell.ToVector3Shifted();
            float hitDelay = EgoSolemnLamentBurstCast.HitDelay(caster.DrawPos, at);
            Vector2 aim = (EgoSolemnLamentMarked.Ground(at) - EgoSolemnLamentMarked.Ground(caster.DrawPos)).normalized;

            int cap = EgoButterflyExtension.Of.cap, had = 0, add = 0;
            if (thing is Pawn victim && !victim.Dead)
            {
                had = EgoButterfly.Stacks(victim);
                add = Mathf.Max(0, Mathf.Min(white ? p.whiteStacks : p.blackStacks, cap - had));
                if (add > 0)
                {
                    game.Mark(victim).Shot(now, hitDelay, had, add, white, aim);
                    EgoButterfly.Add(victim, add, cap);
                }
            }
            if (!white && thing != null && !thing.Destroyed)
            {
                ThingDef round = verbProps.defaultProjectile;
                EgoRound.Hit(CasterPawn, EquipmentSource, round, thing, thing, round.projectile.GetDamageAmount(EquipmentSource), aim);
            }
            game.BurstFor(CasterPawn, this, currentTarget).Shot(now, white, hitDelay, had, add, had < cap && had + add >= cap);
            CasterPawn.records.Increment(RecordDefOf.ShotsFired);
            return true;
        }
    }
}
