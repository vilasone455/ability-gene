using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Double clap has no radius, but CompAbilityEffect_WithDest.DrawHighlight always draws one
    /// from its own range, and GenDraw.DrawRadiusRing has a precalculated cell list that runs
    /// out long before a map-wide number - it logs an error every frame while the targeter is
    /// open. DrawHighlight is not virtual on that class, so the only way past it is a prefix.
    ///
    /// What replaces it is more useful anyway: while the targeter is open, every mark still
    /// being held is outlined, so the player can see what the valid ends actually are. The
    /// second pick has no way to explain a refusal, and this is the compensation for that.
    /// </summary>
    [HarmonyPatch(typeof(CompAbilityEffect_WithDest), nameof(CompAbilityEffect_WithDest.DrawHighlight))]
    public static class Patch_CompAbilityEffect_WithDest_DrawHighlight
    {
        public static bool Prefix(CompAbilityEffect_WithDest __instance, LocalTargetInfo target)
        {
            CompAbilityEffect_DoubleClap clap = __instance as CompAbilityEffect_DoubleClap;
            if (clap == null) return true;

            Gene_Anchors gene = AnchorUtility.GeneOf(clap.parent.pawn);
            if (gene != null)
            {
                List<Anchor> anchors = gene.AnchorsRaw;
                for (int i = 0; i < anchors.Count; i++)
                {
                    if (!gene.Holds(anchors[i])) continue;

                    GenDraw.DrawFieldEdges(new List<IntVec3> { anchors[i].CurrentCell },
                        AnchorGraphics.EdgeColor, null, null);
                }
            }

            if (target.IsValid) GenDraw.DrawTargetHighlight(target);
            return false;
        }
    }
}
