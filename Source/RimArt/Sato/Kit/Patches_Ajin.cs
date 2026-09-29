using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// An Ajin never dies: every kill becomes a Reset. The Black Ghost never dies either: it dissolves.
    /// Runs before the game's own death handling, so no corpse, letter, grief or bond breaks.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    static class Patch_Pawn_Kill_Ajin
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(Pawn __instance)
        {
            if (__instance.Dead) return true;
            if (__instance.def == SatoDefOf.AG_BlackGhost)
            {
                __instance.TryGetComp<CompBlackGhost>()?.Dissolve();
                return false;
            }
            if (!AjinReset.IsAjin(__instance)) return true;
            AjinReset.Start(__instance, AjinCause.Killed);
            return false;
        }
    }

    /// <summary>
    /// A damaging explosion (the trait's explosionDamage, friendly fire included) Resets an Ajin at once with
    /// the body destroyed. The hit itself is absorbed: the body is going anyway.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    static class Patch_Pawn_PreApplyDamage_Ajin
    {
        static void Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed || dinfo.Amount <= 0f || __instance.Dead) return;
            if (!AjinExtension.Get.IsExplosion(dinfo.Def) || !AjinReset.IsAjin(__instance)) return;
            absorbed = true;
            AjinReset.Start(__instance, AjinCause.Explosion);
        }
    }

    /// <summary>A Resetting Ajin lies downed, but he is not a body to carry off: kidnappers pass him by.</summary>
    [HarmonyPatch(typeof(KidnapAIUtility), nameof(KidnapAIUtility.TryFindGoodKidnapVictim))]
    static class Patch_KidnapAIUtility_Ajin
    {
        static void Prefix(Pawn kidnapper, ref List<Thing> disallowed)
        {
            IReadOnlyList<Pawn> resetting = GameComponent_Sato.Instance?.Resetting;
            if (resetting == null || resetting.Count == 0 || kidnapper?.Map == null) return;
            foreach (Pawn pawn in resetting)
            {
                if (pawn == null || pawn.MapHeld != kidnapper.Map) continue;
                if (disallowed == null) disallowed = new List<Thing>();
                if (!disallowed.Contains(pawn)) disallowed.Add(pawn);
            }
        }
    }

    /// <summary>
    /// A colonist whose body is gone is held off the map by an anchor or remains until he rises. Like a corpse with
    /// Death Refusal, he keeps the colony from ending while he waits.
    /// </summary>
    [HarmonyPatch(typeof(GameEnder), nameof(GameEnder.CheckOrUpdateGameOver))]
    static class Patch_GameEnder_Ajin
    {
        static bool Prefix(GameEnder __instance)
        {
            IReadOnlyList<Pawn> resetting = GameComponent_Sato.Instance?.Resetting;
            if (resetting == null) return true;
            foreach (Pawn pawn in resetting)
                if (pawn != null && pawn.Faction == Faction.OfPlayer && AjinReset.Resetting(pawn)?.holder != null)
                {
                    __instance.gameEnding = false;
                    return false;
                }
            return true;
        }
    }

    /// <summary>No organ farming: a natural part surgery takes out of an Ajin crumbles instead of dropping.</summary>
    [HarmonyPatch(typeof(MedicalRecipesUtility), nameof(MedicalRecipesUtility.SpawnNaturalPartIfClean))]
    static class Patch_MedicalRecipesUtility_Ajin
    {
        static bool Prefix(Pawn pawn, ref Thing __result)
        {
            if (!AjinReset.IsAjin(pawn)) return true;
            __result = null;
            return false;
        }
    }

    /// <summary>
    /// Hero form only: downed for the comp's seconds and not being carried, he Resets himself (as Satō shoots
    /// himself in the source). The pool-empty collapse reverts him first, so it never triggers this.
    /// </summary>
    public class HediffCompProperties_AjinDowned : HediffCompProperties
    {
        public float downedSeconds = 5f;

        public HediffCompProperties_AjinDowned() { compClass = typeof(HediffComp_AjinDowned); }
    }

    public class HediffComp_AjinDowned : HediffComp
    {
        private int downedTicks;

        private HediffCompProperties_AjinDowned Props => (HediffCompProperties_AjinDowned)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            Pawn pawn = Pawn;
            if (!pawn.Downed || pawn.CarriedBy != null || AjinReset.Resetting(pawn) != null || !AjinReset.IsAjin(pawn))
            {
                downedTicks = 0;
                return;
            }
            if (++downedTicks >= Props.downedSeconds.SecondsToTicks())
            {
                downedTicks = 0;
                AjinReset.Start(pawn, AjinCause.Downed);
            }
        }

        public override void CompExposeData() => Scribe_Values.Look(ref downedTicks, "downedTicks");
    }
}
