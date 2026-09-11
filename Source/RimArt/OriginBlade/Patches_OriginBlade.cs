using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimArt
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    static class Patch_Pawn_BladeStudyGizmo
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result) yield return gizmo;
            if (!OriginBladeUtility.CanStudy(__instance) || OriginBladeUtility.HasOrigin(__instance)) yield break;
            // Once every requirement is met the command stops being a progress readout and
            // becomes the way in, so a player who dismissed the letter -- or who was not
            // looking when it arrived -- is never locked out of the origin.
            bool ready = OriginBladeUtility.ReadyToAwaken(__instance);
            Pawn pawn = __instance;
            yield return new Command_Action
            {
                defaultLabel = ready
                    ? "AG_OriginBladeAwakenGizmo".Translate()
                    : "AG_OriginBladeStudyGizmo".Translate(),
                defaultDesc = OriginBladeUtility.ProgressText(pawn),
                icon = ContentFinder<Texture2D>.Get("RimArt/Panoply/IconGene"),
                action = () =>
                {
                    if (!ready)
                    {
                        Find.WindowStack.Add(new Dialog_MessageBox(OriginBladeUtility.ProgressText(pawn)));
                        return;
                    }
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "AG_OriginBladeAwakenText".Translate(pawn.LabelShortCap)
                            + "\n\n" + "AG_OriginBladeWarning".Translate(),
                        () => OriginBladeUtility.Awaken(pawn), true));
                }
            };
        }
    }

    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
    static class Patch_TraitSet_OriginBlade
    {
        static void Postfix(Pawn ___pawn, Trait trait)
        {
            if (trait.def != OriginBladeDefOf.AG_OriginBlade || !OriginBladeUtility.HasOrigin(___pawn)) return;
            Current.Game?.GetComponent<GameComponent_BladeStudy>().RecordFor(___pawn);
            OriginBladeUtility.EnforceRestrictions(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    static class Patch_Pawn_OriginBladeSpawn
    {
        static void Postfix(Pawn __instance)
        {
            if (!OriginBladeUtility.HasOrigin(__instance)) return;
            Current.Game.GetComponent<GameComponent_BladeStudy>().RecordFor(__instance);
            OriginBladeUtility.EnforceRestrictions(__instance);
        }
    }

    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip),
        new Type[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    static class Patch_EquipmentUtility_OriginBlade
    {
        static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (!thing.def.IsRangedWeapon || !OriginBladeUtility.HasOrigin(pawn)) return;
            cantReason = "AG_OriginBladeNoRanged".Translate();
            __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.AddEquipment))]
    static class Patch_EquipmentTracker_OriginBlade
    {
        static void Postfix(Pawn_EquipmentTracker __instance) => OriginBladeUtility.EnforceRestrictions(__instance.pawn);
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new Type[] {
        typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    static class Patch_Verb_OriginBladeRanged
    {
        static bool Prefix(Verb __instance) => __instance.EquipmentSource?.def.IsRangedWeapon != true
            || !OriginBladeUtility.HasOrigin(__instance.CasterPawn);
    }

    [HarmonyPatch(typeof(Pawn_AbilityTracker), nameof(Pawn_AbilityTracker.GainAbility))]
    static class Patch_AbilityTracker_OriginBlade
    {
        static bool Prefix(Pawn_AbilityTracker __instance, AbilityDef def) =>
            !def.IsPsycast || !OriginBladeUtility.HasOrigin(__instance.pawn);
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.CanCast), MethodType.Getter)]
    static class Patch_Ability_OriginBlade
    {
        static void Postfix(Ability __instance, ref AcceptanceReport __result)
        {
            if (__instance.def.IsPsycast && OriginBladeUtility.HasOrigin(__instance.pawn))
                __result = "AG_OriginBladeNoPsycasts".Translate().ToString();
        }
    }

    // A pawn can awaken while a previously queued psycast is warming up.
    [HarmonyPatch(typeof(Psycast), nameof(Psycast.Activate),
        new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    static class Patch_Psycast_OriginBladeActivate
    {
        static bool Prefix(Psycast __instance) => !OriginBladeUtility.HasOrigin(__instance.pawn);
    }

    [HarmonyPatch(typeof(Psycast), nameof(Psycast.Activate), new Type[] { typeof(GlobalTargetInfo) })]
    static class Patch_Psycast_OriginBladeActivateWorld
    {
        static bool Prefix(Psycast __instance) => !OriginBladeUtility.HasOrigin(__instance.pawn);
    }

    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.ChangePsylinkLevel))]
    static class Patch_PawnUtility_OriginBladePsylink
    {
        static bool Prefix(Pawn pawn) => !OriginBladeUtility.HasOrigin(pawn);
    }

    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.ChangeLevel),
        new Type[] { typeof(int), typeof(bool) })]
    static class Patch_Psylink_OriginBlade
    {
        static bool Prefix(Hediff_Psylink __instance, int levelOffset) =>
            levelOffset <= 0 || !OriginBladeUtility.HasOrigin(__instance.pawn);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    static class Patch_HealthTracker_OriginBlade
    {
        static bool Prefix(Pawn ___pawn, Hediff hediff) =>
            !(hediff is Hediff_Psylink) || !OriginBladeUtility.HasOrigin(___pawn);
    }

    [HarmonyPatch(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.CanBeUsedBy))]
    static class Patch_ImplantUse_OriginBlade
    {
        static bool Prefix(CompUseEffect_InstallImplant __instance, Pawn p, ref AcceptanceReport __result)
        {
            if (!OriginBladeUtility.HasOrigin(p)
                || !typeof(Hediff_Psylink).IsAssignableFrom(__instance.Props.hediffDef.hediffClass)) return true;
            __result = "AG_OriginBladeNoPsycasts".Translate().ToString();
            return false;
        }
    }

    [HarmonyPatch(typeof(CompUseEffect_GainAbility), nameof(CompUseEffect_GainAbility.CanBeUsedBy))]
    static class Patch_PsytrainerUse_OriginBlade
    {
        static bool Prefix(CompUseEffect_GainAbility __instance, Pawn p, ref AcceptanceReport __result)
        {
            if (!OriginBladeUtility.HasOrigin(p) || !__instance.Props.ability.IsPsycast) return true;
            __result = "AG_OriginBladeNoPsycasts".Translate().ToString();
            return false;
        }
    }
}
