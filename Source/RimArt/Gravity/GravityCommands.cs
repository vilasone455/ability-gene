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
        public static bool HasEye(Pawn pawn) => pawn?.health?.hediffSet.HasHediff(GravityDefOf.AG_AttractionEye) == true;
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
        public static bool Busy(Pawn pawn) => MapComponent_Gravity.On(pawn)?.For(pawn) != null;
        public static bool ValidTarget(Pawn pawn, IntVec3 cell) => pawn?.Map != null && cell.InBounds(pawn.Map)
            && !cell.Fogged(pawn.Map) && cell.DistanceTo(pawn.Position) <= GravityRules.Range
            && GravityMovement.Clear(pawn.Map, pawn.Position, cell);
        public static void Postfix(Ability __instance, ref IEnumerable<Command> __result)
        {
            if (__instance.def.defName == "AG_GravityWell") __result = Commands(__instance);
        }
        private static IEnumerable<Command> Commands(Ability ability)
        {
            Pawn pawn = ability.pawn;
            if (pawn.abilities.AllAbilitiesForReading.FirstOrDefault(a => a.def == ability.def) != ability) yield break;
            var cast = MapComponent_Gravity.On(pawn)?.For(pawn);
            int remaining = GameComponent_Gravity.Instance.Remaining(pawn);
            var button = new Command_Action
            {
                groupable = false,
                defaultLabel = cast == null ? "Gravity Well" : cast.Field ? "Implode" : "Gravity Well",
                defaultDesc = "Channel a gravity well for up to six seconds. Pulls allies and enemies, bends ordinary bullets, and gathers loose objects. Implosion deals 15–45 blunt damage based on the mass inside the core. Moving cancels it. Requires Melee Animation.",
                icon = GravityGraphics.Icon,
                action = () =>
                {
                    if (cast?.Field == true) { cast.Finish(true); return; }
                    Find.Targeter.BeginTargeting(new TargetingParameters
                    {
                        canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false,
                        canTargetItems = false, validator = target => ValidTarget(pawn, target.Cell)
                    }, target => MapComponent_Gravity.On(pawn)?.Begin(pawn, target.Cell));
                }
            };
            if (cast != null && !cast.Field) button.Disable(cast.Active ? "Opening the well." : "Recovering from implosion.");
            else if (cast == null && remaining > 0) button.Disable($"Cooldown: {remaining / 60f:0.0}s");
            else if (cast == null && (!GravityAcquisition.HasEye(pawn) || pawn.InMentalState
                || pawn.stances.stunner.Stunned || !GravityCastAnimation.Clip.CanAnimate(pawn)))
                button.Disable("Requires an attraction eye, a standing humanlike caster, and Melee Animation.");
            yield return button;
            if (cast?.Active == true)
                yield return new Command_Action { groupable = false, defaultLabel = "Cancel",
                    defaultDesc = "Dissipate without an implosion. An activated well still incurs cooldown.", action = () => cast.Finish(false) };
        }
    }

    public class CompProperties_AbilityGravityWell : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityGravityWell() { compClass = typeof(CompAbilityEffect_GravityWell); }
    }
    public class CompAbilityEffect_GravityWell : CompAbilityEffect
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.pawn.abilities.AllAbilitiesForReading.FirstOrDefault(a => a.def == parent.def) != parent) yield break;
            var cast = MapComponent_Gravity.On(parent.pawn)?.For(parent.pawn);
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
            float mass = cast.Field ? cast.map.GetComponent<MapComponent_Gravity>().CoreMass(cast) : 0f;
            Widgets.Label(rect.ContractedBy(6f), $"Gathered mass: {mass:0} kg\nImplosion: {GravityRules.Damage(mass):0.#} damage");
            float seconds = cast.Field ? (GravityRules.DurationTicks - cast.clock.ticks) / 60f : 6f;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 49f, rect.width - 12f, 22f), $"{seconds:0.0}s remaining");
            return new GizmoResult(GizmoState.Clear);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class Patch_GravityOrderedJob
    {
        public static bool Prefix(Pawn ___pawn, Job job, ref bool __result)
        {
            var cast = MapComponent_Gravity.On(___pawn)?.For(___pawn);
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
