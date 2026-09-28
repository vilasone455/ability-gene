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

        /// <summary>
        /// A mask over the whole face: face parts another mod draws over headgear are not drawn
        /// (Facial Animation's eyebrows, see Patch_CanDrawNow_FaceCovered).
        /// </summary>
        public bool coversFace;

        /// <summary>
        /// For a piece on the head, fitted to the average heads: its width on a narrow head, as a factor
        /// of the picture, x facing south or north and y facing east or west. The vanilla narrow heads are
        /// about 0.84 as wide from the front and 0.7 as deep in profile. (1, 1) draws it as it is.
        /// </summary>
        public UnityEngine.Vector2 narrowHeadScale = UnityEngine.Vector2.one;

        /// <summary>
        /// A kneeling picture, facing south, for a kit that kneels its hero (Vergil's Judgement Cut End):
        /// the texture path without the body type, which is added as _Thin and so on. A body type without
        /// its texture has no kneel picture. Null for none.
        /// </summary>
        public string kneelTexPath;
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

        /// <summary>A head piece is narrowed on a narrow head (narrowHeadScale), about the head's middle.</summary>
        public override UnityEngine.Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            UnityEngine.Vector3 scale = base.ScaleFor(node, parms);
            if (node.Props is PawnRenderNodeProperties_EchoCostume costume && parms.pawn.story?.headType?.narrow == true)
                scale.x *= parms.facing.IsHorizontal ? costume.narrowHeadScale.y : costume.narrowHeadScale.x;
            return scale;
        }

        /// <summary>
        /// The kneel picture while the kit says the hero kneels in it (for Vergil, VergilLooks; worked out on
        /// the main thread, so this only reads).
        /// </summary>
        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            if (parms.facing == Rot4.South && VergilGhost.Drawing != parms.pawn && VergilLooks.TryGet(parms.pawn, out VergilLook look) && look.kneelPicture != null)
                return look.kneelPicture;
            return base.GetGraphic(node, parms);
        }
    }

    public static class EchoCostume
    {
        public static bool HidesBodyApparel(Pawn pawn) => Any(pawn, c => c.hideBodyApparel);

        public static bool HidesHeadgear(Pawn pawn) => Any(pawn, c => c.hideHeadgear);

        public static bool CoversFace(Pawn pawn) => Any(pawn, c => c.coversFace);

        // Asked while the render tree works out what to draw, which may run off the main thread, so
        // this only reads: a few hediffs, each with a short list of node properties.
        private static bool Any(Pawn pawn, System.Predicate<PawnRenderNodeProperties_EchoCostume> test)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return false;
            for (int i = 0; i < hediffs.Count; i++)
            {
                HediffDef def = hediffs[i].def;
                if (!def.HasDefinedGraphicProperties) continue;
                foreach (PawnRenderNodeProperties props in def.RenderNodeProperties)
                    if (props is PawnRenderNodeProperties_EchoCostume costume && test(costume))
                        return true;
            }
            return false;
        }

        private static readonly Dictionary<string, Graphic> kneelPictures = new Dictionary<string, Graphic>();

        /// <summary>
        /// This pawn's costume kneel picture for its body type, or null when its costume has none or the texture
        /// for that body type does not exist. Main thread only: it may load the graphic.
        /// </summary>
        public static Graphic KneelPicture(Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            string body = pawn?.story?.bodyType?.defName;
            if (hediffs == null || body == null) return null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                HediffDef def = hediffs[i].def;
                if (!def.HasDefinedGraphicProperties) continue;
                foreach (PawnRenderNodeProperties props in def.RenderNodeProperties)
                {
                    if (!(props is PawnRenderNodeProperties_EchoCostume costume) || costume.kneelTexPath.NullOrEmpty()) continue;
                    string path = costume.kneelTexPath + "_" + body;
                    if (!kneelPictures.TryGetValue(path, out Graphic graphic))
                    {
                        graphic = ContentFinder<UnityEngine.Texture2D>.Get(path, false) == null ? null
                            : GraphicDatabase.Get<Graphic_Single>(path, costume.shaderTypeDef?.Shader ?? ShaderDatabase.Cutout, UnityEngine.Vector2.one, UnityEngine.Color.white);
                        kneelPictures[path] = graphic;
                    }
                    return graphic;
                }
            }
            return null;
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

    /// <summary>
    /// Facial Animation draws its eyebrows at layer 100 when its "draw the eyebrows above the hat" setting
    /// is on (the default), which puts them over a mask. Its face part nodes keep the base CanDrawNow, so
    /// this postfix hides the eyebrow nodes (debug label "BrowControllerComp_...") under a costume that
    /// covers the face. Its other face parts are at layers 50-60, under the mask already. Nothing here
    /// needs the mod: without it no node has that label.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow))]
    static class Patch_CanDrawNow_FaceCovered
    {
        public const string BrowLabel = "BrowControllerComp";

        static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
        {
            if (__result && node.Props.debugLabel != null
                && node.Props.debugLabel.StartsWith(BrowLabel, System.StringComparison.Ordinal)
                && EchoCostume.CoversFace(parms.pawn))
                __result = false;
        }
    }
}
