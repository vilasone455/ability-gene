using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// Kamui: Phase (docs/hero-echo.md "Obito"): a toggle. While it is on Obito is intangible and spends his own
    /// pool; every hit on his whole body goes into the Kamui dimension, and he cannot attack, carry or cast
    /// anything but this. Turning solid takes <see cref="solidSeconds"/>. Cast cost 0 (its pool is its cost).
    /// </summary>
    public class CompProperties_AbilityKamuiPhase : CompProperties_AbilityEffect
    {
        /// <summary>A full pool, in seconds of phase.</summary>
        public float poolSeconds = 30f;
        /// <summary>Seconds of pool back per second solid: 0.25 is 1 s per 4 s.</summary>
        public float refillPerSolidSecond = 0.25f;
        /// <summary>From toggling off to solid: still intangible, unable to act.</summary>
        public float solidSeconds = 0.25f;
        /// <summary>The least pool that lets him phase, so an empty pool does not flicker on and off.</summary>
        public float minStartSeconds = 1f;

        public CompProperties_AbilityKamuiPhase()
        {
            compClass = typeof(CompAbilityEffect_KamuiPhase);
        }
    }

    public class CompAbilityEffect_KamuiPhase : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            InvoluteUtility.GeneOf(parent.pawn)?.TogglePhase();
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = null;
            Gene_Involute gene = InvoluteUtility.GeneOf(parent.pawn);
            if (gene == null)
            {
                reason = "AG_KamuiNoGene".Translate(parent.pawn.LabelShortCap);
                return true;
            }
            if (gene.Phase == KamuiPhaseState.TurningSolid)
            {
                reason = "AG_KamuiTurningSolid".Translate();
                return true;
            }
            if (gene.Phase == KamuiPhaseState.Solid && !gene.CanStartPhase(out reason)) return true;
            return base.GizmoDisabled(out reason);
        }
    }

    /// <summary>The Phase button: its label says what a click does, and the corner shows the pool in seconds.</summary>
    public class Command_KamuiPhase : Command_Ability
    {
        public Command_KamuiPhase(Ability ability, Pawn pawn) : base(ability, pawn) { }

        private Gene_Involute Gene => InvoluteUtility.GeneOf(Pawn);

        public override string TopRightLabel
        {
            get
            {
                Gene_Involute gene = Gene;
                return gene == null ? null : (gene.PoolTicks / 60f).ToString("0") + "s";
            }
        }

        public override string Label => Gene?.Intangible == true ? "AG_KamuiPhaseOffLabel".Translate().ToString() : base.Label;

        public override bool GroupsWith(Gizmo other) => false;

        /// <summary>
        /// A toggle, not a cast: no targeting, no job, nothing he is doing stops. The ability's Activate (and its
        /// comp) is not needed for it; Phase costs no charge and has no cooldown.
        /// </summary>
        public override void ProcessInput(Event ev)
        {
            CurActivateSound?.PlayOneShotOnCamera();
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            InvoluteUtility.GeneOf(Pawn)?.TogglePhase();
        }
    }

    /// <summary>
    /// The pawns that are intangible right now, so the damage, cast and carry patches cost one read when nobody
    /// is. A stale entry (a finished game, a destroyed pawn) is dropped when it is looked up.
    /// </summary>
    public static class KamuiPhaseRegistry
    {
        private static readonly Dictionary<Pawn, Gene_Involute> intangible = new Dictionary<Pawn, Gene_Involute>();

        public static int Count => intangible.Count;

        public static void Clear() => intangible.Clear();

        public static void Add(Gene_Involute gene)
        {
            if (gene?.pawn != null) intangible[gene.pawn] = gene;
        }

        public static void Remove(Gene_Involute gene)
        {
            if (gene?.pawn != null && intangible.TryGetValue(gene.pawn, out Gene_Involute held) && held == gene) intangible.Remove(gene.pawn);
        }

        /// <summary>The gene of <paramref name="pawn"/> if it is intangible now, else null.</summary>
        public static Gene_Involute Intangible(Pawn pawn)
        {
            if (pawn == null || intangible.Count == 0 || !intangible.TryGetValue(pawn, out Gene_Involute gene)) return null;
            if (gene.Intangible && gene.pawn == pawn && !pawn.Destroyed) return gene;
            intangible.Remove(pawn);
            return null;
        }
    }

    /// <summary>
    /// Every hit on an intangible Obito goes into the dimension: the damage is absorbed here, before armour, a
    /// shield or a body part is looked at, and handed to <see cref="Gene_Involute.PassedThrough"/>. The same
    /// exclusions as the Susanoo's mirror: only external violence, never his own hits or his own fire.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_Pawn_KamuiPhase
    {
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (KamuiPhaseRegistry.Count == 0) return true;
            Gene_Involute gene = KamuiPhaseRegistry.Intangible(__instance);
            if (gene == null || !Patch_Susanoo_YataMirror.Blocks(__instance, dinfo)) return true;
            absorbed = true;
            gene.PassedThrough(dinfo);
            return false;
        }
    }

    /// <summary>
    /// Intangible, he cannot attack or cast: every verb but Phase's own refuses to start. This covers a drafted
    /// pawn's auto-attack, ordered attacks, melee and every ability, his and the AI's alike.
    /// </summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
    public static class Patch_Verb_KamuiPhase
    {
        public static bool Prefix(Verb __instance, ref bool __result)
        {
            if (KamuiPhaseRegistry.Count == 0) return true;
            if (KamuiPhaseRegistry.Intangible(__instance.CasterPawn) == null || Gene_Involute.IsPhaseVerb(__instance)) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>Intangible, his other abilities are greyed out with the reason.</summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.GizmoDisabled))]
    public static class Patch_Ability_KamuiPhase
    {
        public static void Postfix(Ability __instance, ref bool __result, ref string reason)
        {
            if (__result || KamuiPhaseRegistry.Count == 0 || __instance.def == ObitoDefOf.AG_KamuiPhase) return;
            if (KamuiPhaseRegistry.Intangible(__instance.pawn) == null) return;
            __result = true;
            reason = "AG_KamuiPhasedCannot".Translate(__instance.pawn.LabelShortCap);
        }
    }

    /// <summary>Intangible, he cannot pick anything up (a rescue, a haul, a carry order).</summary>
    [HarmonyPatch(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryStartCarry), typeof(Thing), typeof(int), typeof(bool))]
    public static class Patch_CarryCount_KamuiPhase
    {
        public static bool Prefix(Pawn_CarryTracker __instance, ref int __result)
        {
            if (KamuiPhaseRegistry.Count == 0 || KamuiPhaseRegistry.Intangible(__instance.pawn) == null) return true;
            __result = 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryStartCarry), typeof(Thing))]
    public static class Patch_Carry_KamuiPhase
    {
        public static bool Prefix(Pawn_CarryTracker __instance, ref bool __result)
        {
            if (KamuiPhaseRegistry.Count == 0 || KamuiPhaseRegistry.Intangible(__instance.pawn) == null) return true;
            __result = false;
            return false;
        }
    }
}
