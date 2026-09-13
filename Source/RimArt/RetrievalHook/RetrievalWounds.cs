using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The cost of dragging an injured person: one wound gets worse and loses its bandage.
    ///
    /// Uses the existing injury rather than adding a new hediff, so doctors see an ordinary
    /// untended wound and re-tend it the normal way. Wound age, infection progress and every
    /// other injury are left alone.
    /// </summary>
    public static class RetrievalWounds
    {
        private static readonly AccessTools.FieldRef<Hediff, float> SeverityInt =
            AccessTools.FieldRefAccess<Hediff, float>("severityInt");

        /// <summary>
        /// Applies the penalty once. Returns a sentence describing what happened, or null when the
        /// pawn had no eligible wound.
        /// </summary>
        public static string Apply(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead) return null;

            HediffSet set = pawn.health.hediffSet;
            List<Hediff_Injury> injuries = new List<Hediff_Injury>();
            List<HookWoundFacts> facts = new List<HookWoundFacts>();
            for (int i = 0; i < set.hediffs.Count; i++)
            {
                if (!(set.hediffs[i] is Hediff_Injury injury)) continue;
                injuries.Add(injury);
                facts.Add(FactsFor(pawn, injury));
            }

            int index = RetrievalRules.PickWound(facts, n => Rand.Range(0, n));
            if (index < 0) return null;

            Hediff_Injury chosen = injuries[index];
            float increase = RetrievalRules.CappedIncrease(RetrievalHookDefaults.MaxSeverityIncrease,
                RetrievalHookDefaults.SeverityStep, amount => Safe(pawn, chosen, amount));

            HediffComp_TendDuration tend = chosen.TryGetComp<HediffComp_TendDuration>();
            bool wasTended = tend != null && tend.IsTended;

            if (increase > 0f) chosen.Severity += increase;
            if (tend != null)
            {
                tend.tendTicksLeft = -1;
                tend.tendQuality = 0f;
            }
            pawn.health.Notify_HediffChanged(chosen);

            string wound = pawn.LabelShortCap + "'s " + chosen.LabelBase + " on the " + (chosen.Part?.LabelShort ?? "body");
            if (increase <= 0f)
                return wound + (wasTended ? " lost its bandage." : " was not worsened.")
                       + " Any severity increase would have killed them or destroyed the part.";
            return wound + " worsened by " + increase.ToString("0.00") + (wasTended ? " and lost its bandage." : ".");
        }

        public static HookWoundFacts FactsFor(Pawn pawn, Hediff_Injury injury)
        {
            HediffSet set = pawn.health.hediffSet;
            BodyPartRecord part = injury.Part;
            bool present = part != null && !set.PartIsMissing(part);
            bool organic = present && part.def.alive && !set.PartOrAnyAncestorHasDirectlyAddedParts(part);
            HediffComp_TendDuration tend = injury.TryGetComp<HediffComp_TendDuration>();

            return new HookWoundFacts
            {
                Permanent = injury.IsPermanent(),
                PartPresent = present,
                External = present && part.depth == BodyPartDepth.Outside,
                Organic = organic,
                Tended = tend != null && tend.IsTended,
                CanBleed = organic && pawn.health.CanBleed && injury.def.injuryProps != null
                           && injury.def.injuryProps.bleedRate > 0f && part.def.bleedRate > 0f
                           && !part.def.IsSolid(part, set.hediffs),
            };
        }

        /// <summary>
        /// True when adding <paramref name="amount"/> to the injury would neither kill the pawn
        /// nor destroy the part.
        ///
        /// Tested by writing the severity field directly and asking the game's own checks, then
        /// putting the value back. The property setter is avoided because it notifies the health
        /// tracker, which would kill the pawn for real on the lethal amounts this is probing for.
        /// </summary>
        private static bool Safe(Pawn pawn, Hediff_Injury injury, float amount)
        {
            HediffSet set = pawn.health.hediffSet;
            float original = SeverityInt(injury);
            SeverityInt(injury) = original + amount;
            set.DirtyCache();
            try
            {
                if (pawn.health.ShouldBeDead()) return false;
                BodyPartRecord part = injury.Part;
                if (part != null && part != pawn.RaceProps.body.corePart && set.GetPartHealth(part) <= 0f)
                    return false;
                return true;
            }
            finally
            {
                SeverityInt(injury) = original;
                set.DirtyCache();
            }
        }
    }
}
