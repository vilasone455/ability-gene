using System.Linq;
using Verse;

namespace RimArt
{
    public static class ShinraAcquisition
    {
        public static string Grant(Pawn pawn)
        {
            var def = DefDatabase<HediffDef>.GetNamed("AG_RepulsionEye");
            if (pawn.health.hediffSet.HasHediff(def)) return "already has it";
            var eye = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def.defName == "Eye"
                && p.parent != null && !pawn.health.hediffSet.PartIsMissing(p.parent));
            if (eye == null) return "requires an eye socket";
            pawn.health.RestorePart(eye);
            pawn.health.AddHediff(def, eye);
            return null;
        }
        public static void Migrate(Pawn pawn)
        {
            if (pawn.health == null) return;
            var legacy = pawn.health.hediffSet.hediffs.Where(h => h.def.defName == "AG_ShinraTenseiKit").ToArray();
            if (legacy.Length == 0) return;
            string result = Grant(pawn);
            if (result != null && result != "already has it") return;
            foreach (var hediff in legacy) pawn.health.RemoveHediff(hediff);
        }
    }
}
