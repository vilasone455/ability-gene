using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
    static class Patch_TraitSet_GainTrait_Ability
    {
        static void Postfix(Pawn ___pawn, Trait trait)
        {
            TraitAbilityUtility.GrantAbilities(___pawn, trait);
        }
    }

    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.RemoveTrait))]
    static class Patch_TraitSet_RemoveTrait_Ability
    {
        static void Prefix(Pawn ___pawn, Trait trait)
        {
            TraitAbilityUtility.RevokeAbilities(___pawn, trait);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    static class Patch_Pawn_SpawnSetup_TraitAbilities
    {
        static void Postfix(Pawn __instance)
        {
            TraitAbilityUtility.SyncAll(__instance);
        }
    }
}
