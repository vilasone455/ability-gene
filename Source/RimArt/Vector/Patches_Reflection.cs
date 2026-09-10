using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The reflection itself. Every damage instance aimed at a reflecting pawn is cancelled and
    /// re-applied to whatever caused it - bullets, melee, explosions, and fire, whose instigator
    /// is the Fire thing, so reflected flame destroys the fire that was doing the burning.
    ///
    /// Damage with no live instigator is cancelled rather than returned. There is nothing to
    /// send it back to, and the vectors still do not land on the pawn.
    ///
    /// This sits at default priority, below the stasis field's Priority.First prefix on the same
    /// method. A prefix returning false skips the rest, so a frozen pawn is simply immune and
    /// never reflects - stopped time beats reversed vectors with no special case.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage_Reflect
    {
        /// <summary>
        /// Guards the re-application. Without it, two reflecting pawns hitting each other would
        /// bounce the same damage between them forever; with it, the second one takes the hit.
        /// </summary>
        private static bool reflecting;

        public static bool Prefix(Thing __instance, ref DamageInfo dinfo, ref DamageWorker.DamageResult __result)
        {
            if (ReflectionRegistry.ActiveCount == 0) return true;
            if (reflecting) return true;

            Pawn pawn = __instance as Pawn;
            if (pawn == null || !ReflectionRegistry.IsReflecting(pawn)) return true;

            __result = new DamageWorker.DamageResult();

            Thing instigator = dinfo.Instigator;
            if (instigator == null || instigator == pawn || instigator.Destroyed
                || !instigator.Spawned || instigator.Map != pawn.Map)
            {
                return false;
            }

            float angle = (instigator.DrawPos - pawn.DrawPos).AngleFlat();
            DamageInfo back = new DamageInfo(
                dinfo.Def, dinfo.Amount, dinfo.ArmorPenetrationInt, angle, pawn, null,
                dinfo.Weapon, DamageInfo.SourceCategory.ThingOrUnknown, pawn);

            reflecting = true;
            try
            {
                instigator.TakeDamage(back);
            }
            finally
            {
                reflecting = false;
            }

            return false;
        }
    }

    /// <summary>
    /// The cost, half of it: nothing can reach a reflecting pawn, including help. Reservations
    /// are the one choke point every friendly job goes through - tending, feeding, rescuing,
    /// arresting, hauling them to a bed - so refusing one here stops all of them without
    /// patching each WorkGiver. Melee reserves nothing and still connects, which is the point:
    /// attacks arrive and are returned, doctors never set out.
    /// </summary>
    [HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserve))]
    public static class Patch_ReservationManager_CanReserve
    {
        public static bool Prefix(Pawn claimant, LocalTargetInfo target, ref bool __result)
        {
            if (ReflectionRegistry.ActiveCount == 0) return true;

            Thing thing = target.Thing;
            if (thing == null || thing == claimant) return true;
            if (!ReflectionRegistry.IsReflecting(thing)) return true;

            __result = false;
            return false;
        }
    }
}
