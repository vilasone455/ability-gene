using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class ArcDefOf
    {
        /// <summary>
        /// Severity is the number of people the arc reached, at
        /// <see cref="ArcDefaults.RecoilPerHop"/> each. Nothing else writes to it.
        ///
        /// MayRequire on the def means this is null without Melee Animation loaded, which is
        /// why every use of it is behind a run that could not have started in the first place.
        /// </summary>
        [MayRequire("co.uk.epicguru.meleeanimation")]
        public static HediffDef AG_ArcRecoil;

        static ArcDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ArcDefOf));
        }
    }
}
