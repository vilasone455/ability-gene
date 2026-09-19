using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for stuck kunai.</summary>
    public static class DebugActions_Kunai
    {
        /// <summary>Stabs the clicked pawn for 8 on a random outside body part and sticks a kunai in it.</summary>
        [RimArtDebug("Kunai", "stick one in pawn", RimArtDebugKind.Pawn)]
        private static void Stick(Pawn pawn)
        {
            BodyPartRecord part = pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside)
                .Where(p => pawn.health.hediffSet.GetPartHealth(p) > 8f).RandomElementWithFallback();
            if (part == null)
            {
                Messages.Message("no outside part with more than 8 health", MessageTypeDefOf.RejectInput, false);
                return;
            }

            var before = new HashSet<Hediff>(pawn.health.hediffSet.hediffs);
            var dinfo = new DamageInfo(DamageDefOf.Stab, 8f, 0f, -1f, null, part);
            dinfo.SetIgnoreArmor(true);
            dinfo.SetAllowDamagePropagation(false);
            pawn.TakeDamage(dinfo);

            if (!KunaiEmbedding.TryEmbed(pawn, before))
                Messages.Message("kunai did not stick (dead, no new injury, or already 3)", MessageTypeDefOf.RejectInput, false);
        }
    }
}
