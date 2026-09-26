using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    public class CompProperties_AbilityUnlimitedBladeWorks : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityUnlimitedBladeWorks()
        {
            compClass = typeof(CompAbilityEffect_UnlimitedBladeWorks);
        }
    }

    /// <summary>
    /// Unlimited Blade Works, the ability: casting it queues the chant (<see cref="JobDriver_UbwChant"/>);
    /// everything after that is <see cref="UbwCast"/>'s. The ability's own cooldown starts at the cast and
    /// comes back if the chant breaks before the release. Its buttons while it runs are added to the
    /// ability's gizmo by <see cref="Patch_UbwCommands"/>.
    /// </summary>
    public class CompAbilityEffect_UnlimitedBladeWorks : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            GameComponent_UnlimitedBladeWorks.Instance?.Queue(pawn);
            pawn.jobs.jobQueue.EnqueueFirst(JobMaker.MakeJob(UbwDefOf.AG_UbwChant));
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn pawn = parent.pawn;
            UbwCast cast = GameComponent_UnlimitedBladeWorks.Instance?.For(pawn);
            if (cast != null)
            {
                reason = cast.Standing ? "The world stands." : "Chanting.";
                return true;
            }
            if (pawn.MapHeld?.GetComponent<MapComponent_UnlimitedBladeWorks>()?.IsWorld == true)
            {
                reason = "Already inside a world.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }
    }

    /// <summary>
    /// The buttons of a cast in progress, after the ability's own: Release and Cancel while chanting, Close
    /// while its world stands. Shinra Tensei adds its Charge/Release the same way.
    /// </summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class Patch_UbwCommands
    {
        public static void Postfix(Ability __instance, ref IEnumerable<Command> __result)
        {
            if (__instance.def == UbwDefOf.AG_Trace_UnlimitedBladeWorks) __result = With(__result, __instance);
        }

        private static IEnumerable<Command> With(IEnumerable<Command> own, Ability ability)
        {
            foreach (Command c in own) yield return c;
            UbwCast cast = GameComponent_UnlimitedBladeWorks.Instance?.For(ability.pawn);
            if (cast == null) yield break;
            int now = Find.TickManager.TicksGame;
            UbwRules rules = UbwRules.Of;
            Texture2D icon = ability.def.uiIcon;

            if (cast.Chanting)
            {
                int verse = cast.VerseAt(cast.Seconds(now));
                var release = new Command_Action
                {
                    defaultLabel = "Release",
                    defaultDesc = "Open the world when this verse ends: everyone standing within " + rules.RadiusFor(verse).ToString("0") +
                                  " cells is taken in for " + rules.WorldSecondsFor(verse).ToString("0") + " s. Verse " + verse + " of 3; after verse 3 it opens by itself.",
                    icon = icon,
                    groupable = false,
                    action = () => cast.AskRelease(Find.TickManager.TicksGame),
                };
                if (cast.releaseAfter > 0) release.Disable("Opens after verse " + cast.releaseAfter + ".");
                yield return release;
                if (cast.releaseAfter == 0)
                    yield return new Command_Action
                    {
                        defaultLabel = "Stop chanting",
                        defaultDesc = "Stop the chant before the release. No cooldown is spent.",
                        icon = TexCommand.ClearPrioritizedWork,
                        groupable = false,
                        action = () => cast.Break(null),
                    };
            }
            else if (cast.Standing && cast.closeAt < 0f && !cast.closeOrdered)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Close (" + cast.WorldSecondsLeft(now).ToString("0") + " s)",
                    defaultDesc = "End the world now. Everyone goes back to where they were taken from; corpses and items drop round the cast point.",
                    icon = icon,
                    groupable = false,
                    action = () => cast.closeOrdered = true,
                };
            }
        }
    }

    /// <summary>
    /// The chant: the caster stands still, facing south, until the cast takes it into the world. If the job
    /// ends first (a move order, downed, a mental break), the cast sees it and the chant breaks.
    /// </summary>
    public class JobDriver_UbwChant : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil chant = ToilMaker.MakeToil("UbwChant");
            chant.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.Rotation = Rot4.South;
                if (GameComponent_UnlimitedBladeWorks.Instance?.ChantStarted(pawn) != true) EndJobWith(JobCondition.Incompletable);
            };
            chant.tickIntervalAction = delta =>
            {
                if (GameComponent_UnlimitedBladeWorks.Instance?.For(pawn) == null) EndJobWith(JobCondition.Succeeded);
            };
            chant.handlingFacing = true;
            chant.defaultCompleteMode = ToilCompleteMode.Never;
            yield return chant;
        }
    }
}
