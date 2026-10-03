using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    [DefOf]
    public static class GravityDefOf
    {
        public static HediffDef AG_AttractionEye;
        public static SoundDef AG_GravityHum, AG_GravityImplode;
        static GravityDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(GravityDefOf)); }
    }

    public static class GravityAcquisition
    {
        // The well is gated on the ability itself (the Echo's grant, or the old eye's hediff grant).
        public static bool HasAbility(Pawn pawn, AbilityDef def) =>
            def != null && pawn?.abilities?.GetAbility(def, includeTemporary: true) != null;
        public static bool HasEye(Pawn pawn) => pawn?.health?.hediffSet.HasHediff(GravityDefOf.AG_AttractionEye) == true;
        // For the old debug kit entry: installs the attraction eye, which carries the ability.
        public static string Grant(Pawn pawn)
        {
            if (pawn?.health == null || !pawn.RaceProps.Humanlike) return "requires a humanlike pawn";
            if (HasEye(pawn)) return "already has an attraction eye";
            var eye = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def.defName == "Eye"
                && p.parent != null && !pawn.health.hediffSet.PartIsMissing(p.parent)
                && !pawn.health.hediffSet.hediffs.Any(h => h.Part == p && h is Hediff_AddedPart));
            if (eye == null) return "requires a free eye socket";
            pawn.health.RestorePart(eye);
            pawn.health.AddHediff(GravityDefOf.AG_AttractionEye, eye);
            return null;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class GravityCommands
    {
        // Held in a well's clip. A well that leaves its caster free (Gojo's Blue) never makes him busy.
        public static bool Busy(Pawn pawn) => MapComponent_Gravity.Live(pawn)?.Holding(pawn) != null;
        public static bool ValidTarget(Pawn pawn, IntVec3 cell, float range) => pawn?.Map != null && cell.InBounds(pawn.Map)
            && !cell.Fogged(pawn.Map) && cell.DistanceTo(pawn.Position) <= range
            && GravityMovement.Clear(pawn.Map, pawn.Position, cell);
        public static void Postfix(Ability __instance, ref IEnumerable<Command> __result)
        {
            if (__instance.def.comps?.Any(c => c is CompProperties_AbilityGravityWell) == true) __result = Commands(__instance);
        }
        private static IEnumerable<Command> Commands(Ability ability)
        {
            Pawn pawn = ability.pawn;
            if (pawn.abilities.AllAbilitiesForReading.FirstOrDefault(a => a.def == ability.def) != ability) yield break;
            var props = CompProperties_AbilityGravityWell.For(ability.def);
            var cast = MapComponent_Gravity.On(pawn)?.For(pawn, ability.def);
            int remaining = GameComponent_Gravity.Instance.Remaining(pawn, ability.def);
            // A well that leaves its caster free runs its time out: no Implode, no Cancel.
            bool holds = props.holdsCaster, implode = holds && cast?.Field == true;
            var button = new Command_Action
            {
                groupable = false,
                defaultLabel = implode ? "Implode" : ability.def.LabelCap.ToString(),
                defaultDesc = implode
                    ? $"Implode now: {props.Damage(cast.eaten):0.#} blunt damage within {props.BurstRadius(cast.eaten):0.#} cells."
                    : ability.def.description,
                icon = props.look == GravityLook.Well ? GravityGraphics.Icon : ability.def.uiIcon,
                action = () =>
                {
                    if (implode) { cast.Finish(true); return; }
                    Find.Targeter.BeginTargeting(new TargetingParameters
                    {
                        canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false,
                        canTargetItems = false, validator = target => ValidTarget(pawn, target.Cell, props.range)
                    }, target => MapComponent_Gravity.On(pawn)?.Begin(pawn, target.Cell, ability.def));
                }
            };
            if (cast != null && !holds) button.Disable(ability.def.LabelCap + " is open.");
            else if (cast != null && !cast.Field) button.Disable(cast.Active ? "Opening the well." : "Recovering from implosion.");
            else if (cast == null && remaining > 0) button.Disable($"Cooldown: {remaining / 60f:0.0}s");
            else if (cast == null && holds && (pawn.InMentalState || pawn.stances.stunner.Stunned || !GravityCastAnimation.Clip.CanAnimate(pawn)))
                button.Disable("Requires a standing humanlike caster and Melee Animation.");
            else if (cast == null && !holds && (pawn.Downed || pawn.InMentalState || pawn.stances.stunner.Stunned))
                button.Disable("Requires a caster who can act.");
            else if (cast == null && !MapComponent_Gravity.CanPay(pawn, ability.def, out float cost))
                button.Disable("AG_EchoCastNoCharge".Translate(cost.ToString("0"), GameComponent_Echoes.Get.charge.ToString("0")));
            yield return button;
            if (cast?.Active == true && holds)
                yield return new Command_Action { groupable = false, defaultLabel = "Cancel",
                    defaultDesc = "Close the well without an implosion. A well that has opened still starts its cooldown.",
                    action = () => cast.Finish(false) };
        }
    }

    public class CompAbilityEffect_GravityWell : CompAbilityEffect
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.pawn.abilities.AllAbilitiesForReading.FirstOrDefault(a => a.def == parent.def) != parent) yield break;
            var cast = MapComponent_Gravity.On(parent.pawn)?.For(parent.pawn, parent.def);
            if (cast?.Active == true) yield return new Gizmo_Gravity(cast);
        }
    }
    public sealed class Gizmo_Gravity : Gizmo
    {
        private readonly GravityCast cast;
        public Gizmo_Gravity(GravityCast cast) { this.cast = cast; }
        public override float GetWidth(float maxWidth) => 240f;
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            var props = cast.Props;
            Widgets.Label(rect.ContractedBy(6f), $"Eaten: {cast.eaten:0} (pull {cast.Radius:0.0} cells)\n"
                + $"Implosion: {props.Damage(cast.eaten):0.#} blunt, {props.BurstRadius(cast.eaten):0.#} cells");
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 49f, rect.width - 12f, 22f), $"{cast.TicksLeft / 60f:0.0} s left");
            return new GizmoResult(GizmoState.Clear);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class Patch_GravityOrderedJob
    {
        public static bool Prefix(Pawn ___pawn, Job job, ref bool __result)
        {
            var cast = MapComponent_Gravity.On(___pawn)?.Holding(___pawn);
            if (cast == null) return true;
            if (job.def == JobDefOf.Goto) { cast.Finish(false); cast.StopAnimation(); return true; }
            __result = false; return false;
        }
    }
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new[] { typeof(LocalTargetInfo),
        typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Patch_GravityBlockAttack
    {
        public static bool Prefix(Verb __instance, ref bool __result)
        {
            if (!GravityCommands.Busy(__instance.CasterPawn)) return true;
            __result = false; return false;
        }
    }
}
