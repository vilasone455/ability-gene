using System.Collections.Generic;
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

    /// <summary>
    /// Unlimited Blade Works' Intercept: a shot in an intercepting world is checked once and stopped where a sword meets
    /// it (<see cref="UbwIntercept"/>). A no-op while no cast intercepts. Another kit's prefix may already have skipped
    /// the original this tick (<c>__runOriginal</c> false); then the shot is left alone.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "TickInterval")]
    static class Patch_Projectile_TickInterval_UbwIntercept
    {
        static bool Prefix(Projectile __instance, int delta, bool __runOriginal) =>
            !__runOriginal ? false : UbwIntercept.Live.Count == 0 || UbwIntercept.BeforeTick(__instance, delta);
    }

    /// <summary>While a world intercepts, its projectiles tick every tick, so none passes its meeting point between two updates.</summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.UpdateRateTicks), MethodType.Getter)]
    static class Patch_Projectile_UpdateRateTicks_UbwIntercept
    {
        static void Postfix(Projectile __instance, ref int __result)
        {
            if (UbwIntercept.Live.Count == 0 || !__instance.Spawned) return;
            for (int i = 0; i < UbwIntercept.Live.Count; i++)
                if (UbwIntercept.Live[i].world == __instance.Map) __result = 1;
        }
    }

    /// <summary>Unlimited Blade Works' Arm button on every player colonist inside a standing world other than its caster.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    static class Patch_Pawn_GetGizmos_UbwArm
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result) yield return gizmo;
            Command arm = UbwCommands.ArmButton(__instance);
            if (arm != null) yield return arm;
        }
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
