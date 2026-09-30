using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where the game draws a watched pawn's weapon this frame (Trace On and Reinforcement draw on it), and no drawing
    /// of a real weapon on its way to the inventory. Costs one bool read while no pawn is watched.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    static class Patch_DrawEquipmentAiming_Trace
    {
        static bool Prefix(Thing eq, Vector3 drawLoc, float aimAngle) => TraceHands.Drawing(eq, drawLoc, aimAngle);
    }

    /// <summary>A melee hit that landed: a reinforced attacker's gets Reinforcement's slash.</summary>
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    static class Patch_MeleeHit_TraceReinforced
    {
        static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            if (__instance.CasterPawn is Pawn attacker && target.Thing is Pawn foe) GameComponent_Trace.Instance?.AddHit(attacker, foe);
        }
    }
}
