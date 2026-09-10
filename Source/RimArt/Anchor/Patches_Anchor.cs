using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The carrier does not carry a weapon. This is the gene's whole cost - not a number on a
    /// stat, a permanent decision about what that colonist is - and it is enforced at the point
    /// every equip goes through rather than by dropping weapons after the fact, so the game
    /// never offers the player a gun it is going to take away again.
    ///
    /// The overload is chosen by parameter count rather than named, because CanEquip has a short
    /// form that forwards to the long one and the long one is the only place a reason can be
    /// reported. Anything that bypasses this and equips directly is still caught at cast time by
    /// <see cref="AnchorClapCheck"/>.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_EquipmentUtility_CanEquip
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(EquipmentUtility))
                .Where(m => m.Name == "CanEquip")
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        public static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (!__result || thing == null || !thing.def.IsWeapon) return;

            Gene_Anchors gene = AnchorUtility.GeneOf(pawn);
            if (gene == null || !gene.BansWeapons) return;

            __result = false;
            cantReason = "AG_AnchorCannotEquip".Translate();
        }
    }
}
