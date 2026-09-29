using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>One pawn a word reaches, and what it adds to the throat cost before the word's base and the volume.</summary>
    public struct WordListener
    {
        public Pawn pawn;
        public float factor;
    }

    /// <summary>
    /// Everything the voice knows that is not about casting: who hears a word, what it costs to
    /// say, and where the cost lands.
    /// </summary>
    public static class LarynxUtility
    {
        /// <summary>
        /// Deaf pawns are immune: Hearing at or below <see cref="LarynxExtension.minHearing"/> (both
        /// ears missing gives 0), every mechanoid, and Satō's Black Ghost (a summoned figure).
        /// </summary>
        public static bool CanHear(Pawn target)
        {
            if (target == null || target.Dead || target.health?.capacities == null) return false;
            if (target.RaceProps.IsMechanoid || target.def == SatoDefOf.AG_BlackGhost) return false;
            return target.health.capacities.GetLevel(PawnCapacityDefOf.Hearing) > LarynxExtension.Get.minHearing;
        }

        /// <summary>The speaker's neck, or null once it is gone.</summary>
        public static BodyPartRecord NeckOf(Pawn speaker)
        {
            if (speaker?.health?.hediffSet == null) return null;
            return speaker.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(p => p.def == BodyPartDefOf.Neck);
        }

        /// <summary>Whether there is any voice left to spend.</summary>
        public static bool CanSpeak(Pawn speaker)
        {
            if (speaker?.health?.capacities == null) return false;
            if (NeckOf(speaker) == null) return false;
            return speaker.health.capacities.GetLevel(PawnCapacityDefOf.Talking) > LarynxDefaults.MinTalkingToSpeak;
        }

        /// <summary>The volume set on the speaker's hero form, or Speak when there is none.</summary>
        public static WordVolume VolumeOf(Pawn speaker)
        {
            List<Hediff> hediffs = speaker?.health?.hediffSet?.hediffs;
            if (hediffs == null) return WordVolume.Speak;
            for (int i = 0; i < hediffs.Count; i++)
            {
                HediffComp_WordVolume comp = hediffs[i].TryGetComp<HediffComp_WordVolume>();
                if (comp != null) return comp.volume;
            }
            return WordVolume.Speak;
        }

        public static float CurrentWear(Pawn speaker) =>
            speaker?.health?.hediffSet?.GetFirstHediffOfDef(LarynxDefOf.AG_LarynxWear)?.Severity ?? 0f;

        /// <summary>
        /// Every pawn this word would reach and act on if the speaker said it now at this volume.
        /// A pawn hears when the sound reached its cell within reach x its Hearing (Hearing capped at
        /// 1, so good ears do not hear further than the volume carries). A listener the word can do
        /// nothing to (drop to someone unarmed, stop to someone downed) is left out and costs nothing.
        /// </summary>
        public static List<WordListener> Listeners(Pawn speaker, ImperativeWord word, WordVolume volume)
        {
            var result = new List<WordListener>();
            if (speaker?.Map == null || !speaker.Spawned) return result;
            LarynxExtension ext = LarynxExtension.Get;
            float reach = ext.Reach(volume);
            Dictionary<IntVec3, float> heard = SoundSpread.From(speaker, reach, ext.doorwayCost);
            IReadOnlyList<Pawn> pawns = speaker.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == speaker || !heard.TryGetValue(pawn.Position, out float distance)) continue;
                if (!CanHear(pawn)) continue;
                float hearing = Mathf.Min(1f, pawn.health.capacities.GetLevel(PawnCapacityDefOf.Hearing));
                if (distance > reach * hearing) continue;
                if (!WouldObey(pawn, word)) continue;
                result.Add(new WordListener { pawn = pawn, factor = ListenerFactor(speaker, pawn, word, ext) });
            }
            return result;
        }

        /// <summary>Whether the word can do anything to this pawn.</summary>
        public static bool WouldObey(Pawn pawn, ImperativeWord word)
        {
            switch (word)
            {
                case ImperativeWord.Crush:
                case ImperativeWord.Explode:
                    return true;
                case ImperativeWord.Drop:
                    return !pawn.Downed && pawn.equipment?.Primary != null && pawn.jobs != null;
                default:
                    return !pawn.Downed && pawn.jobs != null;
            }
        }

        /// <summary>
        /// How hard this one listener is to talk to. Already doing what the word says: the cheap
        /// factor alone. Otherwise hostile to the speaker x2 and in a mental state x2.5. Then x
        /// consciousness (a downed pawn resists less, not below 0.2) and x body size, at least 1
        /// (a human x1, a thrumbo x4). Every term is something the game already tracks.
        /// </summary>
        public static float ListenerFactor(Pawn speaker, Pawn target, ImperativeWord word, LarynxExtension ext)
        {
            float factor = 1f;
            JobDef wanted = JobFor(word);
            if (wanted != null && target.CurJobDef == wanted)
            {
                factor = ext.alreadyObeyingFactor;
            }
            else
            {
                if (target.InMentalState) factor *= ext.mentalStateFactor;
                if (target.HostileTo(speaker)) factor *= ext.hostileFactor;
            }
            float consciousness = target.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            factor *= Mathf.Clamp(consciousness, ext.minConsciousnessFactor, 1f);
            factor *= Mathf.Max(1f, target.BodySize);
            return factor;
        }

        /// <summary>Throat cost of saying the word to these listeners: base x volume x the sum of their factors.</summary>
        public static float WearFor(List<WordListener> listeners, float baseCost, WordVolume volume)
        {
            float sum = 0f;
            for (int i = 0; i < listeners.Count; i++) sum += listeners[i].factor;
            return baseCost * LarynxExtension.Get.Factor(volume) * sum;
        }

        /// <summary>
        /// Puts the wear on the speaker's neck. Severity is cumulative and sheds slowly (12 % a
        /// day, see the hediff), so the throat is the limit on how often he speaks, not a cooldown.
        /// </summary>
        public static void ApplyWear(Pawn speaker, float amount)
        {
            BodyPartRecord neck = NeckOf(speaker);
            if (neck == null || amount <= 0f) return;

            Hediff existing = speaker.health.hediffSet.GetFirstHediffOfDef(LarynxDefOf.AG_LarynxWear);
            if (existing == null)
            {
                existing = HediffMaker.MakeHediff(LarynxDefOf.AG_LarynxWear, speaker, neck);
                existing.Severity = amount;
                speaker.health.AddHediff(existing, neck);
                return;
            }

            existing.Severity += amount;
        }

        /// <summary>The job a word resolves to, or null for words that are not a job.</summary>
        public static JobDef JobFor(ImperativeWord word)
        {
            switch (word)
            {
                case ImperativeWord.Stop: return JobDefOf.Wait;
                case ImperativeWord.Come: return JobDefOf.Goto;
                case ImperativeWord.Run:  return JobDefOf.Flee;
                case ImperativeWord.Drop: return JobDefOf.DropEquipment;
                default:                  return null;
            }
        }

        /// <summary>
        /// The one job a listener is handed for a job word. Null when the word is not a job or cannot
        /// be obeyed right now.
        /// </summary>
        public static Job BuildJob(Pawn speaker, Pawn target, CompProperties_AbilityImperative props)
        {
            switch (props.word)
            {
                case ImperativeWord.Stop:
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Wait, target.Position);
                    job.expiryInterval = props.holdTicks;
                    return job;
                }

                case ImperativeWord.Come:
                    return JobMaker.MakeJob(JobDefOf.Goto, speaker.Position);

                case ImperativeWord.Drop:
                {
                    // Vanilla already owns this exactly as the word wants it: the driver stops the
                    // pather, waits 30 ticks, and drops at the pawn's own position.
                    ThingWithComps weapon = target.equipment?.Primary;
                    if (weapon == null) return null;
                    return JobMaker.MakeJob(JobDefOf.DropEquipment, weapon);
                }

                case ImperativeWord.Run:
                {
                    IntVec3 away = CellFinderLoose.GetFleeDest(target, new List<Thing> { speaker }, props.fleeDistance);
                    return JobMaker.MakeJob(JobDefOf.Flee, away, speaker);
                }

                default:
                    return null;
            }
        }
    }
}
