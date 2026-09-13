using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class Patch_ShinraCommands
    {
        public static void Postfix(Ability __instance, ref IEnumerable<Command> __result)
        {
            if (__instance.def.defName == "AG_ShinraTensei") __result = Commands(__instance);
        }
        private static IEnumerable<Command> Commands(Ability ability)
        {
            Pawn pawn = ability.pawn;
            // Hediff abilities are separate instances. Only the first source supplies controls.
            if (pawn.abilities.AllAbilitiesForReading.FirstOrDefault(a => a.def == ability.def) != ability) yield break;
            var s = GameComponent_Shinra.Instance.For(pawn);
            var action = new Command_Action
            {
                groupable = false,
                onHover = () => { if (pawn.Spawned) GenDraw.DrawRadiusRing(pawn.Position, ShinraCharge.Radius); },
                defaultLabel = s.active ? "Release" : "Charge Shinra Tensei",
                defaultDesc = "Hold a repulsion charge for up to 3 seconds of power. Release commits a 20-second cooldown. Allies can be hit.",
                icon = ContentFinder<Texture2D>.Get("RimArt/Shinra/IconPush"),
                action = () =>
                {
                    if (s.active) s.Release();
                    else if (GameComponent_Shinra.HasEye(pawn) && ShinraCastAnimation.TryStart(pawn, out var animation))
                        GameComponent_Shinra.Instance.Begin(pawn, animation);
                }
            };
            if (s.active && s.charge.releasing) action.Disable("Recovering from release.");
            else if (!s.active && s.cooldownUntil > Find.TickManager.TicksGame)
                action.Disable($"Cooldown: {(s.cooldownUntil - Find.TickManager.TicksGame) / 60f:0.0}s");
            else if (!s.active && (!GameComponent_Shinra.HasEye(pawn) || !ShinraCastAnimation.CanAnimate(pawn)))
                action.Disable("Requires a repulsion eye, a standing humanlike caster, and Melee Animation.");
            yield return action;
            if (s.active && !s.charge.releasing)
                yield return new Command_Action { groupable = false, defaultLabel = $"Cancel ({s.charge.Power:P0})",
                    defaultDesc = "Discard the charge without cooldown.", action = s.Cancel };
            yield return new Command_Toggle { groupable = false, defaultLabel = "Auto-release", defaultDesc =
                "At full charge, release when a reflectable incoming shot is about to threaten the caster. Close shots may arrive before the burst.",
                isActive = () => s.autoRelease, toggleAction = () => s.autoRelease = !s.autoRelease };
        }
    }

    public sealed class Gizmo_ShinraCharge : Gizmo
    {
        private readonly ShinraPawnState state;
        public Gizmo_ShinraCharge(ShinraPawnState state) { this.state = state; }
        public override float GetWidth(float maxWidth) => 150f;
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Widgets.Label(rect.ContractedBy(6f), $"Shinra charge: {state.charge.Power:P0}");
            Widgets.FillableBar(new Rect(rect.x + 6f, rect.y + 40f, rect.width - 12f, 22f), state.charge.Power);
            return new GizmoResult(GizmoState.Clear);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class Patch_ShinraOrderedJob
    {
        public static bool Prefix(Pawn ___pawn, Job job, ref bool __result)
        {
            var s = GameComponent_Shinra.Instance.States.FirstOrDefault(s => s.pawn == ___pawn && s.active);
            if (s == null) return true;
            if (job.def == JobDefOf.Goto) { s.Cancel(); return true; }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new[] { typeof(LocalTargetInfo),
        typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Patch_ShinraBlockAttacks
    {
        public static bool Prefix(Verb __instance, ref bool __result)
        {
            if (!GameComponent_Shinra.Instance.States.Any(s => s.active && s.pawn == __instance.CasterPawn)) return true;
            __result = false;
            return false;
        }
    }
}
