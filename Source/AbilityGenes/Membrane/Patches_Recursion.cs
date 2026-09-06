using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Where a held round is actually drawn and collided against. The engine still believes the
    /// projectile is where it left it; this reports the point on the halving curve instead.
    ///
    /// This getter is read for every projectile every frame and again from the engine's own
    /// interception checks, so the count test in front of it is not optional.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.ExactPosition), MethodType.Getter)]
    public static class Patch_Projectile_ExactPosition
    {
        public static void Postfix(Projectile __instance, ref Vector3 __result)
        {
            if (RecursionRegistry.CapturedCount == 0) return;

            HalvingProjectile state;
            if (!RecursionRegistry.TryGetCapture(__instance, out state)) return;

            Vector3 position = state.CurrentPosition();

            // Keep the engine's altitude; only the ground-plane position is ours.
            position.y = __result.y;
            __result = position;
        }
    }

    /// <summary>
    /// Takes a held round off the engine's clock entirely. Skipping this stops movement,
    /// lifetime and the impact check in one place - which is what makes the round genuinely
    /// still in flight rather than frozen or cancelled. Its position comes from the curve in
    /// <see cref="HalvingProjectile"/>, stepped once per game tick by the holder.
    ///
    /// Both entry points are patched because a subclass may override either.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_Projectile_Ticking
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Projectile), "TickInterval")]
        public static bool TickIntervalPrefix(Projectile __instance)
        {
            return !IsHeld(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Projectile), "Tick")]
        public static bool TickPrefix(Projectile __instance)
        {
            return !IsHeld(__instance);
        }

        private static bool IsHeld(Projectile projectile)
        {
            if (RecursionRegistry.CapturedCount == 0) return false;

            HalvingProjectile state;
            return RecursionRegistry.TryGetCapture(projectile, out state);
        }
    }

    /// <summary>
    /// Melee. A swing has no position and no travel - Verb_MeleeAttack resolves damage in the
    /// instant it is cast - so there is no object for the curve to move. What there is instead
    /// is the roll the game already makes to decide whether the blow connects, and RimWorld
    /// already has a word for a blow that did not: a miss.
    ///
    /// Scaling that chance by the same halving curve gives the whole presentation for free -
    /// the miss mote, the miss sound, the combat log line - and it never reaches zero, so a
    /// swing that beats the odds still lands in full. That last part is deliberate: the moment
    /// melee is cancelled outright the membrane stops being a receding distance and becomes an
    /// invulnerability shield, which is the reflection gene's job, not this one's.
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetNonMissChance")]
    public static class Patch_Verb_MeleeAttack_GetNonMissChance
    {
        public static void Postfix(Verb_MeleeAttack __instance, LocalTargetInfo target, ref float __result)
        {
            if (RecursionRegistry.HolderCount == 0) return;

            HediffComp_Recursion holder = RecursionRegistry.HolderFor(target.Thing);
            if (holder == null) return;

            __result *= holder.MeleeConnectFactor(__instance.CasterPawn);
        }
    }

    /// <summary>
    /// The remainder: damage that arrives with no verb behind it and nothing in flight to hold.
    /// Explosions, fire, a collapsing roof, and rounds fired from inside the field, which spawn
    /// too close to ever be captured. There is no roll to bias and no object to move, so the
    /// only thing left to scale is the damage itself.
    ///
    /// Melee is skipped here on purpose - it was already handled by the miss roll, and taking a
    /// second bite would mean the rare connecting blow lands for a fraction, which is the
    /// binary-shield failure mode in a quieter form.
    ///
    /// Sits at default priority, below the stasis field's Priority.First prefix on the same
    /// method: a frozen pawn is immune before any of this is reached.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage_Recursion
    {
        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (RecursionRegistry.HolderCount == 0) return;

            HediffComp_Recursion holder = RecursionRegistry.HolderFor(__instance);
            if (holder == null) return;

            if (dinfo.Tool != null) return;
            if (dinfo.Instigator == holder.Pawn) return;

            float factor = holder.Props.verblessDamageFactor;
            if (factor >= 1f) return;

            dinfo.SetAmount(dinfo.Amount * Mathf.Max(0.01f, factor));
        }
    }
}
