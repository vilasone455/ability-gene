using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The two questions the resonance has to answer: whether a part can hold a note, and what
    /// happens to one that was struck in phase.
    /// </summary>
    public static class ResonanceUtility
    {
        /// <summary>
        /// Whether a standing wave can live in this part.
        ///
        /// Four conditions, and all four are the same condition stated four ways: the note needs
        /// a piece of body it can shake without shaking the person apart with it.
        ///
        /// A part below the surface is never struck directly - the blow lands on what covers
        /// it - so there is nothing for the wave to start on. The core part has nothing to ring
        /// against; it is what everything else rings against. And anything whose loss would take
        /// a vital part with it is damped by definition, because the body is built to stop
        /// exactly that from happening.
        ///
        /// A conceptual part is not flesh at all. The waist is the clearest case - its label is
        /// "utility slot" and it exists to hang a belt on - but it sits Outside, under the torso,
        /// with no vital descendant, so every other test here waves it through. There is no bone
        /// in it to ring, and breaking it would take the pelvis and spine with it on the way to
        /// destroying an apparel slot.
        ///
        /// The vital test walks the whole subtree rather than the part itself. A neck carries no
        /// vital tag of its own, but a head hangs off it and a brain hangs off that, so removing
        /// one kills - the tag that matters is never on the part you would name.
        ///
        /// Reading this off the game's own body data rather than a list of part defs is what
        /// makes it hold for animals, mechs and modded races without a patch: whatever a body is
        /// built from, the parts that can ring are the ones the body can survive losing.
        /// </summary>
        public static bool CanRing(Pawn pawn, BodyPartRecord part)
        {
            if (pawn == null || part == null) return false;
            if (pawn.health == null || pawn.health.hediffSet == null) return false;

            if (part.depth != BodyPartDepth.Outside) return false;
            if (part.parent == null) return false;
            if (part.def == null || part.def.conceptual) return false;
            if (pawn.RaceProps != null && pawn.RaceProps.body != null
                && part == pawn.RaceProps.body.corePart) return false;

            if (SubtreeHoldsLife(part)) return false;

            // A part already gone cannot be struck again, and the engine will happily report a
            // hit on one that was destroyed by the same blow.
            return !pawn.health.hediffSet.PartIsMissing(part);
        }

        private static bool SubtreeHoldsLife(BodyPartRecord part)
        {
            if (part.def != null && part.def.tags != null)
            {
                for (int i = 0; i < part.def.tags.Count; i++)
                {
                    BodyPartTagDef tag = part.def.tags[i];
                    if (tag != null && tag.vital) return true;
                }
            }

            if (part.parts == null) return false;
            for (int i = 0; i < part.parts.Count; i++)
            {
                if (SubtreeHoldsLife(part.parts[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// Takes the part off. This goes through the ordinary damage path rather than adding a
        /// missing-part hediff directly, so everything downstream of losing a limb happens by
        /// itself - the bleeding, the pain, the drop, the combat log line, the notification, and
        /// the death check for a body that could not afford it after all.
        ///
        /// Damage propagation is off because the note is in one part and nowhere else, and the
        /// armour penetration is absurd because armour has already had its say: the blow that
        /// set the resonance and the blow that finished it were both rolled against it normally.
        /// This is what those two blows did, not a third one.
        /// </summary>
        public static void Shatter(Pawn pawn, BodyPartRecord part, Pawn instigator, DamageDef damageDef)
        {
            if (pawn == null || part == null || pawn.Dead) return;

            float remaining = pawn.health.hediffSet.GetPartHealth(part);
            int amount = Mathf.Max(1, Mathf.CeilToInt(remaining) + 1);

            DamageInfo dinfo = new DamageInfo(
                damageDef ?? DamageDefOf.Crush, amount, 999f, -1f, instigator, part, null,
                DamageInfo.SourceCategory.ThingOrUnknown);
            dinfo.SetAllowDamagePropagation(false);

            pawn.TakeDamage(dinfo);
        }
    }
}
