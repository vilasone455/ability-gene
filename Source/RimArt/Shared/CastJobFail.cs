using System;
using RimWorld;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The fail condition of a kit cast job that holds the caster after the ability fires. The
    /// vanilla conditions (target despawned, ability can no longer be cast) are right until the
    /// ability fires and wrong after it: Ability.PreActivate starts the cooldown, and the kit job defs
    /// leave abilityCasting false, so Ability.Casting is false too and the job ended on the tick the
    /// ability fired. The hold toil never ran and an undrafted caster walked off mid-picture. A
    /// target killed, pushed into a flyer or destroyed by the effect ended the job the same way.
    ///
    /// <paramref name="fired"/> is the kit MapComponent's answer to "has this caster's cast landed in
    /// the job still running". From then on only the job's own toils end it.
    /// </summary>
    public static class CastJobFail
    {
        public static void FailBeforeFired(this JobDriver_CastAbility driver, Func<bool> fired)
        {
            driver.FailOn(() => !fired()
                && (ToilFailConditions.DespawnedOrNull(driver.job.targetA, driver.pawn) || !driver.job.ability.CanCast && !driver.job.ability.Casting));
        }
    }
}
