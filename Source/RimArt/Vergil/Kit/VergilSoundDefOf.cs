using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Vergil's sounds (1.6/Defs/SoundDefs/AG_Vergil_Sounds.xml), picked in the VFX lab against the sketches'
    /// sound markers of the same names. A class of their own because two would share a field name with the
    /// AbilityDefs in <see cref="VergilDefOf"/>.
    /// </summary>
    [DefOf]
    public static class VergilSoundDefOf
    {
        public static SoundDef AG_VergilJudgementCutOpen;
        public static SoundDef AG_VergilJudgementCutBreak;
        /// <summary>The blade clicking home, in Judgement Cut, Yamato Dash and Judgement Cut End.</summary>
        public static SoundDef AG_VergilSheathe;
        public static SoundDef AG_VergilDash;
        public static SoundDef AG_VergilDashCuts;
        public static SoundDef AG_VergilSwordFire;
        public static SoundDef AG_VergilSwordHit;
        public static SoundDef AG_VergilSwordsSpin;
        public static SoundDef AG_VergilSwordsBreak;
        public static SoundDef AG_VergilCutEndVanish;
        public static SoundDef AG_VergilCutEndCuts;

        static VergilSoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VergilSoundDefOf));
        }
    }
}
