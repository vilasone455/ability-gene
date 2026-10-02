using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Heals hit points off a pawn's injuries (Samehada's Feed and Fusion, Mimicry's lifesteal). Only injuries that are not
    /// permanent: a scar stays, and a missing part is not an injury, so nothing grows back.
    /// </summary>
    public static class InjuryHeal
    {
        private static List<Hediff_Injury> injuries = new List<Hediff_Injury>();

        /// <summary>
        /// Heals <paramref name="amount"/> hit points off the pawn's injuries that are not scars, in the order the health tab
        /// lists them, as Core's regeneration (Pawn_HealthTracker) spreads its amount. Returns what was healed, which is less
        /// than the amount when the injuries add up to less.
        /// </summary>
        public static float Heal(Pawn pawn, float amount)
        {
            if (pawn?.health?.hediffSet == null || amount <= 0f) return 0f;
            injuries.Clear();
            pawn.health.hediffSet.GetHediffs(ref injuries, h => !h.IsPermanent());
            float healed = 0f;
            for (int i = 0; i < injuries.Count && amount > 0f; i++)
            {
                Hediff_Injury injury = injuries[i];
                float part = Mathf.Min(amount, injury.Severity);
                if (part <= 0f) continue;
                injury.Heal(part);
                amount -= part;
                healed += part;
            }
            return healed;
        }
    }
}
