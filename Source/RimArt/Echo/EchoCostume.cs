using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A hero-form costume: a render node on the Echo's manifest hediff, given in XML with this class.
    /// It is only a picture. The Host keeps wearing its apparel, whose armour and insulation still count;
    /// while the costume is on, that apparel is only not drawn.
    /// </summary>
    public class PawnRenderNodeProperties_EchoCostume : PawnRenderNodeProperties
    {
        /// <summary>Worn clothes and armour (OnSkin, Middle, Shell) are not drawn. Belts, packs and other layers still are.</summary>
        public bool hideBodyApparel;

        /// <summary>Worn headgear is not drawn, and so no longer hides the hair.</summary>
        public bool hideHeadgear;
    }

    /// <summary>
    /// Draws the costume. The body worker hides it with the body (in a bed that hides its sleeper, or when
    /// the body is not drawn); on top of that it is skipped when the drawer asks for no clothes, as worn
    /// apparel is. The vanilla apparel worker cannot be used: it expects the node to belong to a worn Apparel.
    /// </summary>
    public class PawnRenderNodeWorker_EchoCostume : PawnRenderNodeWorker_Body
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) =>
            base.CanDrawNow(node, parms) && parms.flags.FlagSet(PawnRenderFlags.Clothes);
    }

    public static class EchoCostume
    {
        public static bool HidesBodyApparel(Pawn pawn) => Hides(pawn, head: false);

        public static bool HidesHeadgear(Pawn pawn) => Hides(pawn, head: true);

        // Asked while the render tree works out what to draw, which may run off the main thread, so
        // this only reads: a few hediffs, each with a short list of node properties.
        private static bool Hides(Pawn pawn, bool head)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return false;
            for (int i = 0; i < hediffs.Count; i++)
            {
                HediffDef def = hediffs[i].def;
                if (!def.HasDefinedGraphicProperties) continue;
                foreach (PawnRenderNodeProperties props in def.RenderNodeProperties)
                    if (props is PawnRenderNodeProperties_EchoCostume costume
                        && (head ? costume.hideHeadgear : costume.hideBodyApparel))
                        return true;
            }
            return false;
        }

        /// <summary>Clothes and armour: apparel on the skin, middle or shell layer that is not drawn as a pack.</summary>
        public static bool IsClothing(Apparel apparel)
        {
            if (apparel.RenderAsPack()) return false;
            foreach (ApparelLayerDef layer in apparel.def.apparel.layers)
                if (layer == ApparelLayerDefOf.OnSkin || layer == ApparelLayerDefOf.Middle || layer == ApparelLayerDefOf.Shell)
                    return true;
            return false;
        }
    }

    /// <summary>Worn clothes and armour are not drawn under a costume that hides them.</summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Body), nameof(PawnRenderNodeWorker_Apparel_Body.CanDrawNow))]
    static class Patch_ApparelBody_CanDrawNow_EchoCostume
    {
        static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
        {
            if (__result && node.apparel != null && EchoCostume.IsClothing(node.apparel)
                && EchoCostume.HidesBodyApparel(parms.pawn))
                __result = false;
        }
    }

    /// <summary>
    /// Headgear is not drawn under a costume that hides it. The render tree also asks this before it lets
    /// worn headgear hide the hair, beard and eyes, so a hidden helmet leaves the hair showing.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Head), nameof(PawnRenderNodeWorker_Apparel_Head.HeadgearVisible))]
    static class Patch_HeadgearVisible_EchoCostume
    {
        static void Postfix(PawnDrawParms parms, ref bool __result)
        {
            if (__result && EchoCostume.HidesHeadgear(parms.pawn)) __result = false;
        }
    }
}
