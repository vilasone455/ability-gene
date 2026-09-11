using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Per-instance flight speed, which the engine has no other way to express.
    ///
    /// A projectile's speed comes from its def, and its position is a lerp along
    /// origin -> destination driven by ticksToImpact against this computed total. Scaling the
    /// total by the round's force and setting ticksToImpact to the same figure at commit keeps
    /// the two halves of that lerp in step: the round covers its recalculated range at its
    /// chosen speed, and every other thing the engine does with it - interception checks, cover,
    /// shields, the impact itself - runs unchanged.
    ///
    /// Nothing on the def is touched, so a rifle round edited to half speed does not make every
    /// other round of its kind slow.
    ///
    /// The engine's own rounds only. Combat Extended's carry a per-instance speed of their own -
    /// shotSpeed - so there is nothing to express and force is written straight into the round at
    /// commit; see <see cref="CombatExtendedRounds"/>.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "StartingTicksToImpact", MethodType.Getter)]
    public static class Patch_Projectile_StartingTicksToImpact
    {
        public static void Postfix(Projectile __instance, ref float __result)
        {
            if (VectorEditRegistry.EditedCount == 0) return;

            float force = VectorEditRegistry.ForceFor(__instance);
            if (force <= 0f || Mathf.Approximately(force, 1f)) return;

            __result /= force;
            if (__result <= 0f) __result = 0.001f;
        }
    }

    /// <summary>
    /// Primary damage, on the same terms as speed: a round thrown twice as hard arrives twice
    /// as hard.
    ///
    /// Only the primary figure. Armour penetration, extra damage rolls and everything an impact
    /// does afterwards are left alone deliberately - force is meant to be a decision about
    /// momentum, not a general damage multiplier that quietly rewrites how a round interacts
    /// with armour.
    ///
    /// The engine's own rounds only, for the same reason as the speed postfix above: a CE round's
    /// damage is a settable figure on the instance, so force is written into it at commit rather
    /// than applied on the way out.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.DamageAmount), MethodType.Getter)]
    public static class Patch_Projectile_DamageAmount
    {
        public static void Postfix(Projectile __instance, ref int __result)
        {
            if (VectorEditRegistry.EditedCount == 0) return;

            float force = VectorEditRegistry.ForceFor(__instance);
            if (force <= 0f || Mathf.Approximately(force, 1f)) return;

            __result = Mathf.Max(1, Mathf.RoundToInt(__result * force));
        }
    }

    /// <summary>
    /// Reflex surge: the world's clock, slowed.
    ///
    /// There is no sub-normal TimeSpeed - the enum bottoms out at Paused and then jumps to
    /// Normal - so the only place a quarter rate can be expressed is the multiplier every other
    /// part of the tick loop is derived from. CurTimePerTick becomes 1/15th of a second, the
    /// loop affords one tick every four frames at 60fps, and PawnTweener scales its
    /// interpolation by the same figure, so pawns glide at quarter speed rather than stuttering
    /// through it.
    ///
    /// Forced, not scaled. Multiplying the result would leave superfast at six times a quarter,
    /// which is faster than normal - the ability would be bypassable by pressing a speed button.
    /// Paused is the one result left alone: a zero here means the player stopped the game, or
    /// the editor did, and neither should be started again by a surge.
    ///
    /// Priority.Last for a postfix means it runs after every other one, which is the whole point
    /// of putting it there. This is a getter any mod that retunes RimWorld's speed tiers has a
    /// reason to touch - Smart Speed is the obvious one - and a surge that could be overwritten
    /// by whichever of those happened to load later would fail at random. Running last makes the
    /// surge authoritative for as long as it is up, and changes nothing the rest of the time.
    /// </summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickRateMultiplier), MethodType.Getter)]
    public static class Patch_TickManager_TickRateMultiplier
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            if (VectorSurgeRegistry.HolderCount == 0) return;
            if (__result <= 0f) return;

            __result = VectorSurgeRegistry.Rate;
        }
    }

    /// <summary>
    /// Who gets the map's clicks while the editor is open.
    ///
    /// This is the last stop in the frame for anything the mouse does over the map, and the
    /// point where a drag would otherwise become a selection box and a right-click an order.
    /// Both are exactly the gestures the editor needs, so while a session is open the whole
    /// method is replaced by the session's own handling: no pawn is selected, no order is
    /// issued, and no designator is started by drawing a group over a field of bullets.
    ///
    /// The panel has already had its turn by the time this runs - windows are drawn and
    /// processed earlier in UIRoot_Play.UIRootOnGUI - so nothing here can steal a click that
    /// belonged to a slider.
    /// </summary>
    [HarmonyPatch(typeof(Selector), nameof(Selector.SelectorOnGUI))]
    public static class Patch_Selector_SelectorOnGUI
    {
        public static bool Prefix()
        {
            VectorEditSession session = VectorEditSession.Current;
            if (session == null) return true;

            if (!session.StillValid())
            {
                session.Cancel();
                return true;
            }

            session.HandleMapInput();
            return false;
        }
    }
}
