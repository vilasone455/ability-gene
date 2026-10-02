using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Paradise Lost's shot: Core's shooting verb (warmup, cooldown, skill gain, the Corrosion roll through
    /// Notify_UsedWeapon) with the projectile replaced by the room hit (<see cref="EgoParadiseLost.RoomHit"/>), which never
    /// misses. The aimed thing needs line of sight when the shot goes off, as at the order; the others in its room do not.
    /// The verb's defaultProjectile (AG_EgoParadiseLost_Round) is never spawned: it carries the damage def, the damage to a
    /// single target and the info card's numbers.
    /// </summary>
    public class Verb_EgoParadiseLost : Verb_Shoot
    {
        private CompEgoParadiseLost Staff => EquipmentSource?.GetComp<CompEgoParadiseLost>();

        protected override bool TryCastShot()
        {
            CompEgoParadiseLost staff = Staff;
            Thing aimed = currentTarget.Thing;
            if (staff == null || !CasterIsPawn || aimed == null || aimed.Destroyed || aimed.Map != caster.Map) return false;
            if (!TryFindShootLineFromTo(caster.Position, currentTarget, out _)) return false;
            lastShotTick = Find.TickManager.TicksGame;
            EgoParadiseLost.RoomHit(CasterPawn, staff, verbProps.defaultProjectile, aimed);
            CasterPawn.records.Increment(RecordDefOf.ShotsFired);
            return true;
        }
    }
}
