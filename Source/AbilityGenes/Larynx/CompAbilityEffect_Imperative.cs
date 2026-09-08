using RimWorld;
using Verse;
using Verse.AI;

namespace AbilityGenes
{
    public class CompProperties_AbilityImperative : CompProperties_AbilityEffect
    {
        /// <summary>Which word this AbilityDef says. The only thing that differs between them.</summary>
        public ImperativeWord word = ImperativeWord.Stop;

        /// <summary>
        /// How long STOP and KNEEL hold before the target's own think tree takes over again.
        /// This is an expiry on the inserted job, not a stun: they are free the moment it ends,
        /// and free immediately if something more urgent reaches them first.
        /// </summary>
        public int holdTicks = 180;

        public CompProperties_AbilityImperative()
        {
            compClass = typeof(CompAbilityEffect_Imperative);
        }
    }

    /// <summary>
    /// Inserts exactly one job at the front of a target's queue and hands them straight back to
    /// their own AI afterwards.
    ///
    /// This is deliberately not mind control and not a mental state - vanilla has both, and both
    /// take the person away. Nothing here is cancelled or suppressed: the target keeps their
    /// faction, their hostility, their memory and their think tree, and does one thing they did
    /// not choose on the way through. StartJob's own resumeCurJobAfterwards does the handing
    /// back, so the interruption is the game's own, not an imitation of it.
    ///
    /// The cost falls entirely on the speaker - see <see cref="LarynxUtility.WearFor"/> - and it
    /// is measured from how hard the target was going to be to talk to, which means the ability
    /// prices itself against the situation instead of against a number in XML.
    /// </summary>
    public class CompAbilityEffect_Imperative : CompAbilityEffect
    {
        public new CompProperties_AbilityImperative Props => (CompProperties_AbilityImperative)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn speaker = parent.pawn;
            Pawn victim = target.Pawn;
            if (speaker == null || victim == null) return;

            // Re-checked here and not only in Valid(): a word takes a moment to say, and the
            // target can be deafened, downed or dead inside the warmup.
            if (!LarynxUtility.CanHear(victim))
            {
                Messages.Message("AG_LarynxNotHeard".Translate(victim.LabelShort),
                    victim, MessageTypeDefOf.RejectInput, false);
                return;
            }

            // The bill is worked out before the order lands, because obeying changes CurJobDef
            // and would make every word look like one the target was already following.
            float wear = LarynxUtility.WearFor(speaker, victim, Props.word);

            Speak(speaker, victim);

            LarynxUtility.ApplyWear(speaker, wear);
        }

        /// <summary>
        /// All five words are the same operation: one job, inserted, then handed back. DROP
        /// looked like it would need special handling and does not - JobDefOf.DropEquipment is
        /// vanilla and behaves exactly as the word describes.
        /// </summary>
        private void Speak(Pawn speaker, Pawn victim)
        {
            Job job = LarynxUtility.BuildJob(speaker, victim, Props.word, Props.holdTicks);
            if (job == null || victim.jobs == null) return;

            job.playerForced = true;

            victim.jobs.StartJob(job, JobCondition.InterruptForced,
                jobGiver: null, resumeCurJobAfterwards: true);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn speaker = parent.pawn;
            Pawn victim = target.Pawn;

            if (victim == null) return false;

            if (!LarynxUtility.CanSpeak(speaker))
            {
                if (throwMessages)
                {
                    Messages.Message("AG_LarynxNoVoice".Translate(speaker.LabelShort),
                        speaker, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (!LarynxUtility.CanHear(victim))
            {
                if (throwMessages)
                {
                    Messages.Message("AG_LarynxNotHeard".Translate(victim.LabelShort),
                        victim, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            // The only word with a precondition of its own. Everything else can be said to
            // anything that can hear it.
            if (Props.word == ImperativeWord.Drop && victim.equipment?.Primary == null)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_DisarmNoWeapon".Translate(victim.LabelShort),
                        victim, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        /// <summary>
        /// Shows what this word will cost against this particular target, before it is said.
        /// The whole decision the ability offers is whether this one is worth the voice, so the
        /// number has to be visible at targeting time rather than discovered afterwards.
        /// </summary>
        public override string ExtraTooltipPart()
        {
            return "AG_LarynxTooltip".Translate(
                LarynxDefaults.BaseWearFor(Props.word).ToString("F3"));
        }
    }
}
