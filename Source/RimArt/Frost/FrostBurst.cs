using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What the frost bomb does when it lands.
    ///
    /// Two effects, and the split matters. The stun is the immobilisation: it is the one mechanic
    /// every job, verb and AI in the game already obeys, so a stunned pawn genuinely stops rather
    /// than finishing the swing it had started. The hediff is the thaw: a severity that sheds on
    /// its own and drags movement and manipulation back up with it, so a target comes out of the
    /// cloud slow rather than coming out of it at full speed.
    ///
    /// Everything in the radius is caught, allies included. That is the design and not an
    /// oversight - a control tool that reads friend from foe is a damage ability that has been
    /// told to be polite, and the reason to aim carefully is that it will not.
    /// </summary>
    public static class FrostBurst
    {
        /// <summary>
        /// Freezes everyone within <see cref="FrostDefaults.BurstRadius"/> of <paramref name="centre"/>.
        /// </summary>
        public static void At(Map map, IntVec3 centre, Thing instigator)
        {
            if (map == null || !centre.InBounds(map)) return;

            // Gathered before anything is frozen rather than frozen as they are found. The cell
            // lists being walked are the map's own, and stunning a pawn is not obviously free of
            // anything that writes back to them - a mod that reacts to being stunned by moving
            // the pawn would be mutating the list underneath this loop.
            List<Pawn> caught = new List<Pawn>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(centre, FrostDefaults.BurstRadius, true))
            {
                if (!cell.InBounds(map)) continue;

                // Line of sight from the burst, so a cloud does not reach through a wall into the
                // room next door. The explosion it rides on already stops there.
                if (!GenSight.LineOfSight(centre, cell, map, true)) continue;

                List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    // Contains, because a pawn that ever occupies more than one cell would
                    // otherwise be registered twice and frozen twice as hard.
                    if (things[i] is Pawn pawn && !pawn.Dead && !caught.Contains(pawn)) caught.Add(pawn);
                }
            }

            for (int i = 0; i < caught.Count; i++)
            {
                if (!caught[i].Dead) Freeze(caught[i], centre, instigator);
            }
        }

        private static void Freeze(Pawn pawn, IntVec3 centre, Thing instigator)
        {
            float fraction = Resistance(pawn) * Falloff(pawn.Position, centre);
            if (fraction < FrostDefaults.MinimumFreezeFraction) return;

            // disableRotation, because a pawn frozen in place that still swivels to watch people
            // walk past reads as paused rather than as frozen. Not battle-logged: the cold is a
            // delay, and a combat log full of it buries the shots that actually landed.
            int ticks = Mathf.RoundToInt(FrostDefaults.FreezeTicks * fraction);
            if (ticks > 0) pawn.stances?.stunner?.StunFor(ticks, instigator, false, true, true);

            // The rime lands at the same fraction, so somebody who was barely clipped thaws in a
            // second and somebody at the centre spends the full ten getting their hands back.
            HealthUtility.AdjustSeverity(pawn, FrostDefOf.AG_Frostbound,
                FrostDefaults.RimeSeverity * fraction);
        }

        /// <summary>
        /// How much of the cold a target actually takes: less the bigger it is, less again if it
        /// is a machine.
        /// </summary>
        private static float Resistance(Pawn pawn)
        {
            float bodySize = Mathf.Max(pawn.BodySize, 0.1f);
            float fraction = 1f / (1f + (bodySize - 1f) * FrostDefaults.BodySizeResistance);

            if (pawn.RaceProps != null && pawn.RaceProps.IsMechanoid)
                fraction *= FrostDefaults.MechanicalMultiplier;

            return Mathf.Clamp01(fraction);
        }

        /// <summary>Full effect at the centre, tapering to the edge of the cloud.</summary>
        private static float Falloff(IntVec3 cell, IntVec3 centre)
        {
            float distance = cell.DistanceTo(centre);
            float t = Mathf.Clamp01(distance / FrostDefaults.BurstRadius);
            return Mathf.Lerp(1f, FrostDefaults.EdgeFreezeFraction, t);
        }
    }
}
