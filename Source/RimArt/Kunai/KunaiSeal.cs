using Verse;

namespace RimArt
{
    /// <summary>
    /// Minato's seal on his kunai. Every kunai thrown in Minato's hero form carries his Flying Thunder
    /// God formula: it flies as <see cref="KunaiDefOf.AG_KunaiProjectileMinato"/> and lands, sticks or
    /// drops as a sealed kunai, drawn as his three-pronged kunai with the formula on the handle
    /// (<see cref="KunaiDefaults.MinatoTexture"/>). The seal stays on the kunai item - on the ground, in a
    /// stockpile, carried - and only goes when it is loaded into a belt, whose charges are just a count.
    /// Flying Thunder God will jump to sealed kunai.
    /// </summary>
    public static class KunaiSeal
    {
        /// <summary>Minato's hero form (AG_Echo_Hediffs.xml).</summary>
        public const string MinatoForm = "AG_EchoManifest_Minato";

        private static HediffDef minatoForm;
        private static bool looked;

        /// <summary>True while the pawn is in Minato's hero form, so what it throws is sealed.</summary>
        public static bool ThrowsSealed(Pawn pawn)
        {
            if (!looked)
            {
                minatoForm = DefDatabase<HediffDef>.GetNamedSilentFail(MinatoForm);
                looked = true;
            }
            return minatoForm != null && pawn?.health?.hediffSet != null && pawn.health.hediffSet.HasHediff(minatoForm);
        }
    }
}
