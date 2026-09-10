using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where the body stops being there.
    ///
    /// This is the only place in the mod that makes a damage instance not have happened. Every
    /// other answer this mod gives to incoming damage is a transformation of it - the vector
    /// reflex sends it back, the halving membrane scales it toward zero without ever arriving,
    /// the arrears ledger defers it in full. Scatter is the exemption those three are all
    /// carefully not, and the argument for it is in <see cref="Scatter.Applies"/>: the hit is
    /// only cancelled when there was a direction it came from and a place the carrier could
    /// have been instead.
    ///
    /// Priority.VeryHigh puts it above every other TakeDamage prefix in this mod and below one:
    ///
    /// - The stasis field sits at Priority.First and returns false for anything frozen, so a
    ///   carrier inside a bubble is simply immune and does not spend a charge on a hit that was
    ///   never going to land. Stopped time beats a body coming apart, and it should - there is
    ///   no tick in which the crows could leave.
    /// - Everything below runs on damage that is going to be taken by somebody. The vector
    ///   reflex cannot return a blow that was aimed at a place the carrier was not standing;
    ///   the membrane cannot halve it; the ledger must not record a debt for it. All three are
    ///   correct only because this one has already decided the hit exists.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage_Scatter
    {
        /// <summary>
        /// Guards the dispersal against itself. Nothing in <see cref="Scatter.Disperse"/>
        /// damages the carrier today, but it adds blood loss and a hediff on a pawn who is
        /// mid-teleport, and a future edit that reaches TakeDamage from in there would
        /// otherwise scatter out of its own scatter.
        /// </summary>
        private static bool scattering;

        [HarmonyPriority(Priority.VeryHigh)]
        public static bool Prefix(Thing __instance, ref DamageInfo dinfo, ref DamageWorker.DamageResult __result)
        {
            if (DispersalRegistry.CarrierCount == 0) return true;
            if (scattering) return true;

            Pawn pawn = __instance as Pawn;
            if (pawn == null) return true;

            Gene_Dispersal gene = DispersalRegistry.CarrierFor(pawn);
            if (gene == null) return true;
            if (!Scatter.Applies(pawn, gene, dinfo)) return true;

            // Found before anything is cancelled, so that a carrier with nowhere to go takes
            // the blow rather than being handed a free negate the flock could not have earned.
            IntVec3 landing;
            if (!Scatter.TryFindLanding(pawn, gene, dinfo.Instigator, out landing)) return true;

            __result = new DamageWorker.DamageResult();

            scattering = true;
            try
            {
                Scatter.Disperse(pawn, gene, landing);
            }
            finally
            {
                scattering = false;
            }

            return false;
        }
    }
}
