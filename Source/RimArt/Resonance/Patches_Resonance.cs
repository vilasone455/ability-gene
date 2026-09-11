using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where a blow finds out what it hit.
    ///
    /// This is a postfix rather than a prefix because the resonance is a property of the strike
    /// that landed, and until the damage worker has run there is no part to name - a melee swing
    /// carries no hit part into Thing.TakeDamage, it comes back out of it. Reading DamageResult
    /// is the only place the engine says which piece of body the blow actually found.
    ///
    /// Sitting at the damage layer rather than on the melee verb is the same choice the vector
    /// reflex made and for the same reason: one choke point covers every source of a melee hit,
    /// including tools this mod has never heard of.
    ///
    /// dinfo.Tool is the melee test - the same one the phase barrier uses to tell a swing
    /// from a blast. Bullets, explosions and fire carry no tool, so none of them ring, and
    /// neither does the shatter itself, which is what keeps this from recursing.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage_Resonance
    {
        /// <summary>
        /// Belt and braces over the tool test above. Shattering a part applies damage from
        /// inside this postfix, and a future change that gave that damage a tool would otherwise
        /// turn one broken arm into every part on the body.
        /// </summary>
        private static bool breaking;

        public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            if (ResonanceRegistry.CarrierCount == 0) return;
            if (breaking) return;
            if (dinfo.Tool == null) return;
            if (__result == null) return;

            Pawn victim = __instance as Pawn;
            if (victim == null || victim.Dead) return;

            List<BodyPartRecord> parts = __result.parts;
            if (parts == null || parts.Count == 0) return;

            // The outermost part the blow found that can hold a note, not the last one in the
            // list. A cut to a leg that carries on into the femur reports both, and the femur is
            // the deeper entry - taking the list's tail would mean a blow that broke a bone
            // inside the limb silently failed to ring the limb it went through.
            BodyPartRecord part = null;
            for (int i = 0; i < parts.Count; i++)
            {
                if (!ResonanceUtility.CanRing(victim, parts[i])) continue;
                part = parts[i];
                break;
            }
            if (part == null) return;

            breaking = true;
            try
            {
                // The blow the carrier landed.
                Pawn attacker = dinfo.Instigator as Pawn;
                if (attacker != null && attacker != victim)
                {
                    HediffComp_Resonance striker = ResonanceRegistry.CarrierOf(attacker);
                    if (striker != null) striker.Struck(victim, part);
                }

                // The cost. The resonance does not know which side of the blow it is on, so a
                // carrier who is still standing after this may be standing on one leg.
                HediffComp_Resonance struck = ResonanceRegistry.CarrierOf(victim);
                if (struck != null && struck.Props.selfResonance) struck.Struck(victim, part);
            }
            finally
            {
                breaking = false;
            }
        }
    }
}
