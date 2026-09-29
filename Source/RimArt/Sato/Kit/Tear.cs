using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The Black Ghost's Tear (agreed 2026-09-26, picture 2026-09-29): it walks to the enemy, grabs and lifts it,
    /// and rips an arm (the weapon drops) or a leg (a second leg downs it) off with a bleeding stump, then
    /// throws the limb behind it. Once per summon; fleshy enemies with that limb only. The timing is the
    /// picture's (<see cref="TearGraphics"/>).
    /// </summary>
    public static class BlackGhostTear
    {
        public static bool CanTear(Pawn victim, bool arm) =>
            victim != null && !victim.Dead && victim.RaceProps.IsFlesh && Limbs(victim, arm).Any();

        /// <summary>The victim's natural arms or legs still on it.</summary>
        public static IEnumerable<BodyPartRecord> Limbs(Pawn victim, bool arm)
        {
            BodyPartDef def = arm ? BodyPartDefOf.Arm : BodyPartDefOf.Leg;
            HediffSet set = victim.health.hediffSet;
            return set.GetNotMissingParts().Where(p => p.def == def && !set.PartOrAnyAncestorHasDirectlyAddedParts(p));
        }

        public static void Rip(Pawn ghost, Pawn victim, bool arm)
        {
            BodyPartRecord part = Limbs(victim, arm).RandomElementWithFallback();
            if (part == null) return;
            CompAbilityEffect_Sever.Cut(victim, part);
            if (arm && victim.equipment?.Primary != null && victim.Spawned)
                victim.equipment.TryDropEquipment(victim.equipment.Primary, out _, victim.Position);
            ghost.TryGetComp<CompBlackGhost>().tearUsed = true;
        }
    }

    public class JobDriver_BlackGhostTear : JobDriver
    {
        private bool ripped, thrown;

        private Pawn Victim => job.targetA.Pawn;
        private bool Arm => job.count == 1;
        private CompBlackGhost Ghost => pawn.TryGetComp<CompBlackGhost>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Ghost == null || Ghost.Dissolving || Ghost.tearUsed && !ripped);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOn(() => !BlackGhostTear.CanTear(Victim, Arm));

            Toil tear = ToilMaker.MakeToil("Tear");
            tear.defaultCompleteMode = ToilCompleteMode.Delay;
            tear.defaultDuration = SatoPictures.TearTotalTicks;
            tear.initAction = () =>
            {
                Ghost.StartTear(Victim, Arm);
                Victim.stances?.stunner?.StunFor(SatoPictures.TearHeldTicks, pawn, addBattleLog: false, showMote: false);
                Victim.pather?.StopDead();
                pawn.rotationTracker.FaceTarget(Victim);
            };
            tear.tickAction = () =>
            {
                int t = Find.TickManager.TicksGame - Ghost.tearStart;
                if (!ripped && t >= SatoPictures.TearRipTicks)
                {
                    ripped = true;
                    BlackGhostTear.Rip(pawn, Victim, Arm);
                }
                if (ripped && !thrown && t >= SatoPictures.TearLandTicks)
                {
                    thrown = true;
                    SatoPictures.SpawnTornLimb(pawn, Victim, Arm);
                }
            };
            tear.AddFinishAction(() => Ghost?.EndTear());
            yield return tear;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ripped, "ripped");
            Scribe_Values.Look(ref thrown, "thrown");
        }
    }

    /// <summary>The held enemy is drawn lifted, shaken and, after a leg, tilted; the offset and angle come from the picture.</summary>
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    static class Patch_PawnDrawTracker_TearLift
    {
        static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (SatoPictures.TearPose(___pawn, out Vector3 offset, out _)) __result += offset;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    static class Patch_PawnRenderer_TearTilt
    {
        static void Postfix(Pawn ___pawn, ref PawnDrawParms __result)
        {
            if ((__result.flags & (PawnRenderFlags.Portrait | PawnRenderFlags.Cache)) != 0) return;
            if (!SatoPictures.TearPose(___pawn, out _, out float angle) || Mathf.Abs(angle) < 0.01f) return;
            __result.matrix *= Matrix4x4.Rotate(Quaternion.AngleAxis(angle, Vector3.up));
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    static class Patch_PawnRenderer_TearUncached
    {
        static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (SatoPictures.TearPose(___pawn, out _, out _)) disableCache = true;
        }
    }
}
