using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Everything the larynx knows that is not about casting: who can be spoken to, what a word
    /// costs to say, and where the cost lands.
    /// </summary>
    public static class LarynxUtility
    {
        /// <summary>
        /// A word is a sound, so the counter is deafness and nothing else. RimWorld already
        /// tracks Hearing as a capacity and no vanilla mechanic ever attacks or defends it,
        /// which makes this a real, discoverable answer that costs no code: an EMP-deafened
        /// mech, a colonist whose ears are gone, anyone wearing the right thing.
        /// </summary>
        public static bool CanHear(Pawn target)
        {
            if (target?.health?.capacities == null) return false;
            return target.health.capacities.GetLevel(PawnCapacityDefOf.Hearing) > LarynxDefaults.MinHearingToObey;
        }

        /// <summary>The speaker's neck, or null once it is gone.</summary>
        public static BodyPartRecord NeckOf(Pawn speaker)
        {
            if (speaker?.health?.hediffSet == null) return null;
            return speaker.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(p => p.def == BodyPartDefOf.Neck);
        }

        /// <summary>
        /// How much this word costs to force into this particular head.
        ///
        /// The honest version of "how far the command sits from what they wanted" is not
        /// retrievable - a think tree does not keep the scores it rejected. What it does leave
        /// behind is three things worth reading, and they are enough:
        ///
        ///   - whether they are already doing the thing (a fleeing raider told to RUN is free)
        ///   - whether they are in a mental state (nobody in a tantrum is listening)
        ///   - how conscious they are (a downed pawn barely resists at all)
        ///
        /// Body size is in there too, so shouting a thrumbo down costs more than a rabbit.
        /// The point of the formula is that every term is a number the game is already holding
        /// for its own reasons - there is no difficulty constant to tune later.
        /// </summary>
        public static float WearFor(Pawn speaker, Pawn target, ImperativeWord word)
        {
            float cost = LarynxDefaults.BaseWearFor(word);

            JobDef wanted = JobFor(word);
            if (wanted != null && target.CurJobDef == wanted)
            {
                // They were already going to. Saying it out loud is nearly free.
                return cost * LarynxDefaults.AlreadyObeyingFactor;
            }

            if (target.InMentalState) cost *= LarynxDefaults.MentalStateFactor;
            if (target.HostileTo(speaker)) cost *= LarynxDefaults.HostileFactor;

            float consciousness = target.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            cost *= Mathf_Clamp(consciousness, LarynxDefaults.MinConsciousnessFactor, 1f);

            cost *= target.BodySize;

            return cost;
        }

        /// <summary>
        /// Puts the wear on the speaker's neck. Severity is cumulative and sheds slowly - the
        /// same shape as temporal strain, and for the same reason: a cost that clears before the
        /// next fight is not a cost, it is a cooldown.
        /// </summary>
        public static void ApplyWear(Pawn speaker, float amount)
        {
            BodyPartRecord neck = NeckOf(speaker);
            if (neck == null) return;

            Hediff existing = speaker.health.hediffSet.GetFirstHediffOfDef(LarynxDefOf.AG_LarynxWear);
            if (existing == null)
            {
                existing = speaker.health.AddHediff(LarynxDefOf.AG_LarynxWear, neck);
                existing.Severity = amount;
                return;
            }

            existing.Severity += amount;
        }

        /// <summary>Whether there is any voice left to spend.</summary>
        public static bool CanSpeak(Pawn speaker)
        {
            if (speaker?.health?.capacities == null) return false;
            if (NeckOf(speaker) == null) return false;
            return speaker.health.capacities.GetLevel(PawnCapacityDefOf.Talking) > LarynxDefaults.MinTalkingToSpeak;
        }

        /// <summary>The job a word resolves to, or null for words that are not a job at all.</summary>
        public static JobDef JobFor(ImperativeWord word)
        {
            switch (word)
            {
                case ImperativeWord.Stop:  return JobDefOf.Wait;
                case ImperativeWord.Kneel: return JobDefOf.LayDownAwake;
                case ImperativeWord.Come:  return JobDefOf.Goto;
                case ImperativeWord.Run:   return JobDefOf.Flee;
                case ImperativeWord.Drop:  return JobDefOf.DropEquipment;
                default:                   return null;
            }
        }

        /// <summary>
        /// Builds the one job the target is about to be handed. Null means the word cannot be
        /// said to this target right now - an animal told to DROP, most obviously.
        /// </summary>
        public static Job BuildJob(Pawn speaker, Pawn target, ImperativeWord word, int holdTicks)
        {
            switch (word)
            {
                case ImperativeWord.Stop:
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Wait, target.Position);
                    job.expiryInterval = holdTicks;
                    return job;
                }

                case ImperativeWord.Kneel:
                {
                    Job job = JobMaker.MakeJob(JobDefOf.LayDownAwake, target.Position);
                    job.expiryInterval = holdTicks;
                    return job;
                }

                case ImperativeWord.Come:
                    return JobMaker.MakeJob(JobDefOf.Goto, speaker.Position);

                case ImperativeWord.Drop:
                {
                    // Vanilla already owns this exactly as the word wants it: the driver stops
                    // the pather dead, waits 30 ticks, and drops at the pawn's own position.
                    // No scatter, which is the whole difference from the corrosive glands -
                    // acid throws the weapon clear, a shout only makes them let go of it.
                    ThingWithComps weapon = target.equipment?.Primary;
                    if (weapon == null) return null;
                    return JobMaker.MakeJob(JobDefOf.DropEquipment, weapon);
                }

                case ImperativeWord.Run:
                {
                    IntVec3 away = CellFinderLoose.GetFleeDest(target, new System.Collections.Generic.List<Thing> { speaker },
                        LarynxDefaults.FleeDistance);
                    Job job = JobMaker.MakeJob(JobDefOf.Flee, away, speaker);
                    return job;
                }

                default:
                    return null;
            }
        }

        private static float Mathf_Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
