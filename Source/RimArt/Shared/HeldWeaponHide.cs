using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Kit weapons whose picture draws them instead of Core: Core's held-weapon drawing (PawnRenderUtility.DrawEquipmentAiming)
    /// is skipped for a registered def while its condition holds for the holder (a cast's picture draws the vacuum's wand, the
    /// pole, the water gun, the Fuma's pieces; the Last Prism's picture always draws the prism). A kit registers from a
    /// [StaticConstructorOnStartup] class, after the defs are loaded. One Harmony prefix for all of them.
    /// </summary>
    public static class HeldWeaponHide
    {
        private static readonly Dictionary<ThingDef, Func<Pawn, bool>> hidden = new Dictionary<ThingDef, Func<Pawn, bool>>();

        /// <param name="whileHeldBy">True while Core must not draw it; the pawn is null when the weapon's holder is not a pawn's equipment.</param>
        public static void Register(ThingDef def, Func<Pawn, bool> whileHeldBy)
        {
            if (def != null && whileHeldBy != null) hidden[def] = whileHeldBy;
        }

        /// <summary>Whether Core draws <paramref name="eq"/>: one dictionary miss for every weapon not registered.</summary>
        public static bool Shown(Thing eq)
        {
            if (eq == null || !hidden.TryGetValue(eq.def, out Func<Pawn, bool> hide)) return true;
            return !hide((eq.ParentHolder as Pawn_EquipmentTracker)?.pawn);
        }
    }

    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_DrawEquipmentAiming_HeldWeaponHide
    {
        public static bool Prefix(Thing eq) => HeldWeaponHide.Shown(eq);
    }
}
