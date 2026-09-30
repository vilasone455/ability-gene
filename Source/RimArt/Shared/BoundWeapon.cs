using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Weapons that must never land or be carried off: a manifested Host's hero weapon (Echo/EchoWeapon.cs) and
    /// Shirou's traced copies (Trace/Kit/TraceCopies.cs). Each owner registers a handler that returns true when the
    /// weapon is its own and it has dealt with it (destroyed it, with whatever picture or timer it keeps).
    ///
    /// Every vanilla drop (the drop order, downing, death, Disarm, making room for another weapon, Chain Sickle's
    /// Stake, Inumaki's "drop") goes through Pawn_EquipmentTracker.TryDropEquipment, patched here; a kit that takes a
    /// weapon out of a hand some other way (Vacuum's Suck) asks <see cref="Leave"/> first.
    /// </summary>
    public static class BoundWeapon
    {
        private static readonly List<Func<ThingWithComps, bool>> owners = new List<Func<ThingWithComps, bool>>();

        /// <summary>Called once at startup by each owner.</summary>
        public static void Register(Func<ThingWithComps, bool> leave) => owners.Add(leave);

        /// <summary>The weapon is leaving a hand: true if an owner took it (it is gone), false if it may go on as normal.</summary>
        public static bool Leave(Thing thing)
        {
            if (!(thing is ThingWithComps eq)) return false;
            for (int i = 0; i < owners.Count; i++)
                if (owners[i](eq)) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
    static class Patch_TryDropEquipment_BoundWeapon
    {
        static bool Prefix(ThingWithComps eq, out ThingWithComps resultingEq, ref bool __result)
        {
            resultingEq = null;
            if (!BoundWeapon.Leave(eq)) return true;
            __result = true;
            return false;
        }
    }
}
