using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The real pawn takes the sketch's pose. Each patch does nothing unless <see cref="VergilLooks"/> has a
    /// look for the pawn being drawn this frame, or the pawn holds Yamato.
    ///
    /// The body: the game carries one root matrix and one tint through the whole render tree
    /// (PawnRenderTree.TryGetMatrix starts every node from parms.matrix, and the node worker multiplies
    /// parms.tint into every material), so a crouch or a kneel is that matrix squashed toward the feet,
    /// and the dash's glow and a marked pawn's blue are that tint. Body, head, hair and apparel all follow.
    /// The same two hooks the Mimic decoy uses (Patches_Mimic.cs).
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    static class Patch_PawnRenderer_VergilLook
    {
        static void Postfix(Pawn ___pawn, ref PawnDrawParms __result)
        {
            if ((__result.flags & (PawnRenderFlags.Portrait | PawnRenderFlags.Cache)) != 0) return;
            if (VergilGhost.Drawing == ___pawn)
            {
                // An afterimage: the plain standing body in blue-white light. The invisible flag drops the shadow;
                // Patch_GhostMaterial_Vergil puts every part on the glow shader. The tint is set, not multiplied:
                // while he is gone his own tint carries the invisibility fade.
                __result.tint = VergilGhost.Tint;
                __result.flags |= PawnRenderFlags.Invisible;
                return;
            }
            if (!VergilLooks.TryGet(___pawn, out VergilLook look)) return;
            if (look.tintAmount > 0f)
            {
                Color was = __result.tint, to = Color.Lerp(was, look.tint, look.tintAmount);
                __result.tint = new Color(to.r, to.g, to.b, was.a);
            }
            float squash = look.Squash;
            if (squash < 0.999f)
                __result.matrix *= Matrix4x4.TRS(new Vector3(0f, 0f, VergilLook.Feet * (1f - squash)), Quaternion.identity,
                    new Vector3(look.Widen, 1f, squash));
        }
    }

    /// <summary>
    /// The head is never squashed. With the kneel picture it is moved down by the picture's drop; without one
    /// (a body type that has no kneel picture) the whole pawn is squashed and the head node is scaled back up,
    /// so it keeps its shape and only sits lower. Hair, beard and headgear are children of the head node and
    /// follow it.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Head), nameof(PawnRenderNodeWorker_Head.OffsetFor))]
    static class Patch_HeadOffset_VergilKneel
    {
        static void Postfix(PawnDrawParms parms, ref Vector3 __result)
        {
            if (VergilGhost.Drawing == parms.pawn) return;
            if (VergilLooks.TryGet(parms.pawn, out VergilLook look) && look.kneelPicture != null && parms.facing == Rot4.South)
                __result.z -= VergilLook.KneelDrop;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.ScaleFor))]
    static class Patch_HeadScale_VergilKneel
    {
        static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (VergilGhost.Drawing == parms.pawn) return;
            if (!VergilLooks.TryGet(parms.pawn, out VergilLook look) || node.Props.tagDef != PawnRenderNodeTagDefOf.Head) return;
            float squash = look.Squash;
            if (squash >= 0.999f) return;
            __result.z /= squash;
            __result.x /= look.Widen;
        }
    }

    /// <summary>
    /// Kneeling in the picture, the standing body is not drawn: it is given a clear material rather than
    /// skipped, because a body node that draws nothing takes its children, the coat among them, with it.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.GetFinalizedMaterial))]
    static class Patch_BodyMaterial_VergilKneel
    {
        static bool Prefix(PawnRenderNode node, PawnDrawParms parms, ref Material __result)
        {
            if (node.Props.tagDef != PawnRenderNodeTagDefOf.Body || parms.facing != Rot4.South || VergilGhost.Drawing == parms.pawn) return true;
            if (!VergilLooks.TryGet(parms.pawn, out VergilLook look) || look.kneelPicture == null) return true;
            __result = BaseContent.ClearMat;
            return false;
        }
    }

    /// <summary>
    /// An afterimage is drawn in light: every part's own texture on the additive glow shader, coloured by the
    /// afterimage's tint, so it reads as a blue-white copy of him over the dark and the ground alike. The game's
    /// see-through shader was tried first and showed the ground through him in its own colour.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.GetFinalizedMaterial))]
    static class Patch_GhostMaterial_Vergil
    {
        static void Postfix(PawnDrawParms parms, ref Material __result)
        {
            if (VergilGhost.Drawing != parms.pawn || __result == null) return;
            Texture texture = __result.mainTexture;
            if (texture == null) return;
            __result = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.MoteGlow, Color.white));
        }
    }

    /// <summary>
    /// Kneeling in the picture, nothing else hung on the body is drawn either: worn belts and packs, wounds and
    /// tattoos all sit where the standing body was. Only the costume (the picture itself) and the apparel root it
    /// hangs from stay.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow))]
    static class Patch_BodyParts_VergilKneel
    {
        static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
        {
            if (!__result) return;
            if (VergilGhost.Drawing == parms.pawn)
            {
                // The afterimage is his light, not his body: no wounds or firefoam on it.
                if (node.Props.Worker is PawnRenderNodeWorker_Overlay) __result = false;
                return;
            }
            if (parms.facing != Rot4.South) return;
            if (!VergilLooks.TryGet(parms.pawn, out VergilLook look) || look.kneelPicture == null) return;
            PawnRenderNodeTagDef tag = node.Props.tagDef;
            if (tag == PawnRenderNodeTagDefOf.Body || tag == PawnRenderNodeTagDefOf.ApparelBody || node.Props is PawnRenderNodeProperties_EchoCostume) return;
            for (PawnRenderNode up = node.parent; up != null; up = up.parent)
                if (up.Props.tagDef == PawnRenderNodeTagDefOf.Body)
                {
                    __result = false;
                    return;
                }
        }
    }

    /// <summary>
    /// Zoomed out, the game blits humanlike pawns from a cached atlas frame, which ignores the matrix and the
    /// tint above. A pawn whose body is posed is drawn through the render tree instead, as the Mimic decoy is.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    static class Patch_PawnRenderer_VergilUncached
    {
        static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (VergilGhost.Drawing == ___pawn || VergilLooks.TryGet(___pawn, out VergilLook look) && look.ChangesBody) disableCache = true;
        }
    }

    /// <summary>Judgement Cut End's vanish: the pawn and its shadow are not drawn at all.</summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    static class Patch_PawnRenderer_VergilGone
    {
        static bool Prefix(Pawn ___pawn) =>
            VergilGhost.Drawing == ___pawn || !(VergilLooks.TryGet(___pawn, out VergilLook look) && look.gone);
    }

    /// <summary>While he is gone, his name and health bar are not drawn over the empty cell either.</summary>
    [HarmonyPatch(typeof(PawnUIOverlay), nameof(PawnUIOverlay.DrawPawnGUIOverlay))]
    static class Patch_PawnUIOverlay_VergilGone
    {
        static bool Prefix(Pawn ___pawn) => !(VergilLooks.TryGet(___pawn, out VergilLook look) && look.gone);
    }

    /// <summary>Yamato is never drawn by the game (<see cref="HeldWeaponHide"/>): <see cref="YamatoDraw"/> draws it (sheathed, in hand or kneeling).</summary>
    [StaticConstructorOnStartup]
    static class Patches_VergilYamato
    {
        static Patches_VergilYamato() => HeldWeaponHide.Register(VergilDefOf.AG_Yamato, _ => true);
    }

    /// <summary>
    /// Where the game draws a held weapon every frame, zoomed in (the carried node's PostDraw) and zoomed out
    /// (the cached path): Yamato and the hand are drawn here, next to the pawn's own altitude.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
    static class Patch_PawnRenderUtility_DrawYamato
    {
        static void Postfix(Pawn pawn, Vector3 drawPos, Rot4 facing, PawnRenderFlags flags)
        {
            if (!VergilKit.Wields(pawn) || VergilGhost.Drawing == pawn) return;
            if ((flags & (PawnRenderFlags.Portrait | PawnRenderFlags.Cache)) != 0) return;
            if (!pawn.Spawned || pawn.Dead || pawn.GetPosture() != PawnPosture.Standing) return;
            VergilLooks.TryGet(pawn, out VergilLook look);
            YamatoDraw.Draw(pawn, drawPos, facing, look);
        }
    }
}
