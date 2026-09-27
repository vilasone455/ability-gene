using Verse;

namespace RimArt
{
    /// <summary>
    /// The balance numbers of Inumaki's voice that every word shares: how far each volume carries,
    /// what it multiplies the throat cost by, the listener factors, and the throat syrup. Sits on
    /// AG_LarynxWear, the throat hediff, because the throat is what all of them are about. Numbers
    /// that belong to one word (its base throat cost, damage, stun) are on
    /// <see cref="CompProperties_AbilityImperative"/>.
    /// </summary>
    public class LarynxExtension : DefModExtension
    {
        public float whisperReach = 3f;
        public float speakReach = 8f;
        public float shoutReach = 16f;

        public float whisperFactor = 0.5f;
        public float speakFactor = 1f;
        public float shoutFactor = 2f;

        /// <summary>Reach, in cells, a word loses entering an open door's cell.</summary>
        public float doorwayCost = 3f;

        /// <summary>Hearing at or below this is deaf. Above it, a listener hears within reach x Hearing (at most x1).</summary>
        public float minHearing = 0.15f;

        public float hostileFactor = 2f;
        public float mentalStateFactor = 2.5f;
        /// <summary>Replaces the hostile and mental state factors when the listener is already doing what the word says.</summary>
        public float alreadyObeyingFactor = 0.15f;
        /// <summary>Consciousness multiplies each listener's factor, but not below this.</summary>
        public float minConsciousnessFactor = 0.2f;

        /// <summary>A word whose cost would take the throat past this cannot be said.</summary>
        public float maxWear = 1f;

        /// <summary>What is drunk as throat syrup (herbal medicine), how much wear one removes, and how long drinking takes.</summary>
        public ThingDef syrupThing;
        public float syrupRelief = 0.3f;
        public int syrupTicks = 90;

        /// <summary>Played at Inumaki when a word is said, louder and lower the louder the volume.</summary>
        public SoundDef wordSound;
        public float whisperSoundVolume = 0.5f;
        public float speakSoundVolume = 1f;
        public float shoutSoundVolume = 1.6f;

        public float SoundVolume(WordVolume volume) =>
            volume == WordVolume.Whisper ? whisperSoundVolume : volume == WordVolume.Shout ? shoutSoundVolume : speakSoundVolume;

        private static readonly LarynxExtension Fallback = new LarynxExtension();

        public static LarynxExtension Get => LarynxDefOf.AG_LarynxWear?.GetModExtension<LarynxExtension>() ?? Fallback;

        public float Reach(WordVolume volume)
        {
            switch (volume)
            {
                case WordVolume.Whisper: return whisperReach;
                case WordVolume.Shout: return shoutReach;
                default: return speakReach;
            }
        }

        public float Factor(WordVolume volume)
        {
            switch (volume)
            {
                case WordVolume.Whisper: return whisperFactor;
                case WordVolume.Shout: return shoutFactor;
                default: return speakFactor;
            }
        }
    }
}
