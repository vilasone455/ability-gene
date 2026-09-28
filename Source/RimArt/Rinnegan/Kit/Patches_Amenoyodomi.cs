using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Takes a held weapon off the engine's clock, the way Phase Guard holds rounds (Patches_Recursion): skipping
    /// TickInterval stops its movement, lifetime and impact in one place. Also where a kunai thrown at a cell is
    /// caught, on the tick it would land. Projectile_Fuma overrides TickInterval without calling this one, so it asks
    /// <see cref="GameComponent_Rinnegan"/> itself.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "TickInterval")]
    public static class Patch_Projectile_TickInterval_Amenoyodomi
    {
        public static bool Prefix(Projectile __instance, int delta)
        {
            if (GameComponent_Rinnegan.HeldCount == 0 && GameComponent_Rinnegan.OnCount == 0) return true;
            return GameComponent_Rinnegan.Instance?.BeforeTick(__instance, delta) ?? true;
        }
    }

    /// <summary>A held weapon is where Amenoyodomi holds it, on the ground plane; the engine keeps its altitude.</summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.ExactPosition), MethodType.Getter)]
    public static class Patch_Projectile_ExactPosition_Amenoyodomi
    {
        public static void Postfix(Projectile __instance, ref Vector3 __result)
        {
            if (GameComponent_Rinnegan.HeldCount == 0) return;
            HeldWeapon w = GameComponent_Rinnegan.Instance?.HeldFor(__instance);
            if (w == null) return;
            __result = new Vector3(w.at.x, __result.y, w.at.z);
        }
    }

    /// <summary>A held weapon is drawn by <see cref="RinneganPictures"/>, up in the air with its marks, not by the engine.</summary>
    [HarmonyPatch(typeof(Projectile), "DrawAt")]
    public static class Patch_Projectile_DrawAt_Amenoyodomi
    {
        public static bool Prefix(Projectile __instance)
        {
            if (GameComponent_Rinnegan.HeldCount == 0) return true;
            return GameComponent_Rinnegan.Instance?.IsHeld(__instance) != true;
        }
    }

    /// <summary>
    /// While Amenoyodomi is on, throw kunai can also target a ground cell. The ability's own def keeps cells off, so
    /// the kunai belt (and Minato's throw) is unchanged for everyone else.
    /// </summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.targetParams), MethodType.Getter)]
    public static class Patch_Verb_TargetParams_Amenoyodomi
    {
        private static TargetingParameters withCells;

        public static void Postfix(Verb __instance, ref TargetingParameters __result)
        {
            if (GameComponent_Rinnegan.OnCount == 0) return;
            if (!(__instance is Verb_CastAbility cast) || cast.ability?.def != KunaiDefOf.AG_ThrowKunai) return;
            if (!SasukeKit.Holding(cast.CasterPawn)) return;
            if (withCells == null)
            {
                withCells = new TargetingParameters();
                foreach (System.Reflection.FieldInfo field in typeof(TargetingParameters).GetFields())
                    if (!field.IsStatic) field.SetValue(withCells, field.GetValue(__result));
                withCells.canTargetLocations = true;
            }
            __result = withCells;
        }
    }

    /// <summary>
    /// A let-go kunai carrying Raikō Kusari's charge or Amaterasu's fire: what it hit is stunned or lit, and a lit one
    /// that hit no pawn burns on the cell it came down on. Read before the impact, which destroys the projectile.
    /// </summary>
    [HarmonyPatch(typeof(Projectile_Kunai), "Impact")]
    public static class Patch_ProjectileKunai_Impact_Rinnegan
    {
        public struct State
        {
            public FlyingOn flying;
            public Map map;
            public IntVec3 cell;
            public Vector3 stop;
            public bool conjured;
        }

        public static void Prefix(Projectile_Kunai __instance, out State __state)
        {
            __state = default;
            if (GameComponent_Rinnegan.FlyingCount == 0) return;
            FlyingOn f = GameComponent_Rinnegan.Instance?.TakeFlying(__instance);
            if (f == null) return;
            __state = new State
            {
                flying = f,
                map = __instance.Map,
                cell = __instance.Position,
                stop = __instance.ExactPosition,
                conjured = __instance.def == SasukeDefOf.AG_KunaiProjectileConjured
            };
        }

        public static void Postfix(Thing hitThing, bool blockedByShield, State __state)
        {
            if (__state.flying == null) return;
            Pawn pawn = blockedByShield ? null : hitThing as Pawn;
            GameComponent_Rinnegan.Instance?.KunaiHit(__state.flying, pawn, __state.cell, __state.map, __state.conjured, __state.stop);
        }
    }

    /// <summary>The Fūma lying in black fire cannot be picked up until the fire is out.</summary>
    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip),
        new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    static class Patch_CanEquip_BlackFire
    {
        static void Postfix(Thing thing, ref string cantReason, ref bool __result)
        {
            if (!__result || thing == null || GameComponent_Rinnegan.Instance?.Burning(thing) != true) return;
            __result = false;
            cantReason = "Burning with Amaterasu's black flames.";
        }
    }
}
