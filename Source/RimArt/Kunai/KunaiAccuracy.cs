using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Kunai hit chance with Melee skill in place of Shooting.
    ///
    /// ShotReport.HitReportFor builds the whole gun roll (distance, cover, weather, smoke,
    /// darkness, target size) and puts the shooter's part in one private field,
    /// factorFromShooterAndDist = ShootingAccuracyPawn ^ distance. <see cref="For"/> builds the
    /// report the normal way and overwrites that one field, so every public reading of the report
    /// - aim chance, cover chance, total - uses Melee.
    ///
    /// The per-tile accuracy is ShootingAccuracyPawn's own formula with the Melee level as the
    /// skill term: level (or the stat's noSkillOffset for a pawn without skills), plus its Sight
    /// and Manipulation capacity offsets, through its post-process curve and child factor, all read
    /// from the StatDef. Melee 10 therefore throws exactly as well as Shooting 10 shoots. Shooting
    /// traits, gear and hediff offsets to ShootingAccuracyPawn do not apply, and neither do the
    /// ShootingAccuracyFactor_* stats.
    /// </summary>
    public static class KunaiAccuracy
    {
        private static readonly AccessTools.StructFieldRef<ShotReport, float> ShooterFactor =
            AccessTools.StructFieldRefAccess<ShotReport, float>("factorFromShooterAndDist");

        /// <summary>The kunai shot report for <paramref name="caster"/> throwing at <paramref name="target"/>.</summary>
        public static ShotReport For(Pawn caster, Verb verb, LocalTargetInfo target)
        {
            ShotReport report = ShotReport.HitReportFor(caster, verb, target);
            if (verb.verbProps.canGoWild)
            {
                float distance = (target.Cell - caster.Position).LengthHorizontal;
                ShooterFactor(ref report) = Mathf.Pow(PerTile(caster), distance);
            }
            return report;
        }

        /// <summary>Chance not to miss per cell of distance, from Melee skill.</summary>
        public static float PerTile(Pawn pawn)
        {
            StatDef stat = StatDefOf.ShootingAccuracyPawn;

            float value = pawn.skills != null ? pawn.skills.GetSkill(SkillDefOf.Melee).Level : stat.noSkillOffset;

            if (stat.capacityOffsets != null && pawn.health?.capacities != null)
            {
                for (int i = 0; i < stat.capacityOffsets.Count; i++)
                {
                    PawnCapacityOffset offset = stat.capacityOffsets[i];
                    value += offset.GetOffset(pawn.health.capacities.GetLevel(offset.capacity));
                }
            }

            if (stat.postProcessCurve != null) value = stat.postProcessCurve.Evaluate(value);

            if (stat.postProcessStatFactors != null)
            {
                for (int i = 0; i < stat.postProcessStatFactors.Count; i++)
                    value *= pawn.GetStatValue(stat.postProcessStatFactors[i]);
            }

            return Mathf.Max(0f, value);
        }

        /// <summary>
        /// Melee XP for one throw, on Verb_Shoot.WarmupComplete's rule with Melee for Shooting:
        /// 170 XP per second of full cycle against a standing hostile pawn, 20 against any other
        /// standing pawn, none otherwise. The full cycle is warmup plus cooldown.
        /// </summary>
        public static void Learn(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            if (caster?.skills == null || !(target.Thing is Pawn pawn) || pawn.Downed || pawn.IsColonyMech) return;

            float perSecond = pawn.HostileTo(caster) ? 170f : 20f;
            float cycle = ability.def.verbProperties.warmupTime + ability.def.cooldownTicksRange.Average / 60f;
            caster.skills.Learn(SkillDefOf.Melee, perSecond * cycle);
        }
    }
}
