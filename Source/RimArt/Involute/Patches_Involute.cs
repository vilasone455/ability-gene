using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where a blow finds out what it hit, one layer deeper than Resonance reads it.
    ///
    /// The marrow can use a postfix because it only wants to know which part was struck. This
    /// has to stop the strike landing, and by the time the damage worker has returned, the
    /// injury is already on the body. So it sits in front of the worker instead.
    ///
    /// The trap is that the part has not been chosen yet at that point, and choosing it is a
    /// weighted random roll. Rolling here and then letting vanilla roll again would give two
    /// different answers - the hole would eat hits that never landed on it and miss ones that
    /// did. The roll is therefore made exactly once and written back into dinfo, which sends
    /// GetExactPartFromDamageInfo down its already-decided branch.
    /// </summary>
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyDamageToPart")]
    public static class Patch_DamageWorker_AddInjury_ApplyDamageToPart
    {
        public static bool Prefix(ref DamageInfo dinfo, Pawn pawn)
        {
            if (InvoluteRegistry.ActiveCount == 0) return true;
            if (pawn == null || pawn.health == null) return true;

            Gene_Involute gene = InvoluteRegistry.VentedGeneOf(pawn);
            if (gene == null) return true;

            BodyPartRecord hole = gene.HolePart;
            if (hole == null) return true;
            if (pawn.health.hediffSet.PartIsMissing(hole)) return true;

            BodyPartRecord part = dinfo.HitPart;
            if (part == null)
            {
                part = pawn.health.hediffSet
                    .GetNotMissingParts(dinfo.Height, dinfo.Depth)
                    .RandomElementByWeightWithFallback(p => p.coverageAbsWithChildren, null);

                if (part == null) return true;
                dinfo.SetHitPart(part);
            }

            if (part != hole) return true;

            InvoluteUtility.PassThrough(gene, dinfo, pawn);
            return false;
        }
    }

    /// <summary>
    /// A carrier standing in their own volume is not standing on the map they left, and a map
    /// with nobody on it is a map the game will quietly remove - taking the way back with it.
    ///
    /// Blocking removal outright rather than pretending the aperture is a colonist keeps the
    /// rule where it belongs: the map has to survive because there is a door open on it.
    /// </summary>
    [HarmonyPatch(typeof(MapParent), nameof(MapParent.CheckRemoveMapNow))]
    public static class Patch_MapParent_CheckRemoveMapNow_Involute
    {
        public static bool Prefix(MapParent __instance)
        {
            if (__instance == null || !__instance.HasMap) return true;
            return !HoldsOpenAperture(__instance.Map);
        }

        private static bool HoldsOpenAperture(Map map)
        {
            if (map == null) return false;

            List<Thing> apertures = map.listerThings.ThingsOfDef(InvoluteDefOf.AG_InvoluteAperture);
            for (int i = 0; i < apertures.Count; i++)
            {
                Building_Aperture aperture = apertures[i] as Building_Aperture;
                if (aperture != null && !aperture.Destroyed) return true;
            }
            return false;
        }
    }
}
