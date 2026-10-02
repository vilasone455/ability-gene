using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The Corrosion rules every E.G.O. weapon shares (docs/ego-weapons.md): the roll on each use (rule 1), entering the
    /// corroded state (rule 2, <see cref="MentalState_EgoCorroded"/>), the targets of a firing, the exhaustion after it,
    /// and the mood cost of Overclock (rule 3, <see cref="JobDriver_EgoOverclock"/>).
    /// </summary>
    public static class EgoCorrosion
    {
        /// <summary>
        /// The wielder's mood band now: 0 above the minor break threshold, 1 minor to major, 2 major to extreme, 3 below
        /// extreme. The thresholds are the pawn's own, so Neurotic, Sanguine and the like move the risk, and the player can
        /// read it off the mood bar. A pawn with no mood is in band 0.
        /// </summary>
        public static int Band(Pawn pawn)
        {
            MentalBreaker breaker = pawn.mindState?.mentalBreaker;
            if (breaker == null || pawn.needs?.mood == null) return 0;
            float mood = breaker.CurMood;
            if (mood < breaker.BreakThresholdExtreme) return 3;
            if (mood < breaker.BreakThresholdMajor) return 2;
            if (mood < breaker.BreakThresholdMinor) return 1;
            return 0;
        }

        /// <summary>The weapon's skill requirement. A pawn without skills, or with the skill disabled, does not meet one.</summary>
        public static bool MeetsRequirement(Pawn pawn, CompProperties_EgoWeapon props)
        {
            if (props.requirementSkill == null) return true;
            SkillRecord skill = pawn.skills?.GetSkill(props.requirementSkill);
            return skill != null && !skill.TotallyDisabled && skill.Level >= props.requirementLevel;
        }

        /// <summary>The chance that one use corrodes: the band's chance, one band worse when the requirement is not met.</summary>
        public static float Chance(Pawn pawn, CompProperties_EgoWeapon props)
        {
            int band = Band(pawn);
            if (!MeetsRequirement(pawn, props)) band = Math.Min(3, band + 1);
            switch (band)
            {
                case 1: return props.corrosionMinor;
                case 2: return props.corrosionMajor;
                case 3: return props.corrosionExtreme;
                default: return 0f;
            }
        }

        /// <summary>
        /// Rule 1, one use of the weapon. A pawn already in a mental state of any kind (corroded, berserk, a False Face)
        /// does not roll, so the roll never replaces another state.
        /// </summary>
        public static bool Roll(Pawn pawn, CompEgoWeapon weapon)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || pawn.InMentalState || pawn.mindState == null) return false;
            float chance = Chance(pawn, weapon.Props);
            return chance > 0f && Rand.Chance(chance) && Corrode(pawn, weapon);
        }

        /// <summary>Rule 2: the weapon takes its wielder. False when the game refuses the state (asleep, tutorial).</summary>
        public static bool Corrode(Pawn pawn, CompEgoWeapon weapon)
        {
            CompProperties_EgoWeapon props = weapon.Props;
            string reason = weapon.parent.LabelCap + ": fires every " + props.corrodedInterval.ToString("0.#") + " s for "
                + props.corrodedDuration.ToString("0.#") + " s.";
            MentalState_EgoCorroded.starting = weapon;
            try
            {
                return pawn.mindState.mentalStateHandler.TryStartMentalState(EgoDefOf.AG_EgoCorroded, reason, forced: true)
                    && pawn.MentalState is MentalState_EgoCorroded;
            }
            finally
            {
                MentalState_EgoCorroded.starting = null;
            }
        }

        /// <summary>The nearest spawned pawn to the wielder on its map, any faction, downed or not; null when it is alone.</summary>
        public static Pawn Nearest(Pawn wielder) => NearestWhere(wielder, float.MaxValue, p => true);

        /// <summary>The nearest hostile pawn, not downed, within <paramref name="range"/> cells; null when there is none.</summary>
        public static Pawn NearestHostile(Pawn wielder, float range) =>
            NearestWhere(wielder, range, p => !p.Downed && p.HostileTo(wielder));

        public static bool HostileInRange(Pawn wielder, float range) => NearestHostile(wielder, range) != null;

        private static Pawn NearestWhere(Pawn wielder, float range, Predicate<Pawn> ok)
        {
            if (!wielder.Spawned) return null;
            float best = range >= float.MaxValue ? float.MaxValue : range * range;
            Pawn nearest = null;
            IReadOnlyList<Pawn> pawns = wielder.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == wielder || p.Dead || !ok(p)) continue;
                float d = (p.Position - wielder.Position).LengthHorizontalSquared;
                if (d <= best)
                {
                    best = d;
                    nearest = p;
                }
            }
            return nearest;
        }

        /// <summary>
        /// One firing of the weapon's action. A targeted action turns the wielder to its target first and does not fire
        /// when there is none. <paramref name="hostilesOnly"/> is Overclock.
        /// </summary>
        public static bool Fire(Pawn wielder, CompEgoWeapon weapon, bool hostilesOnly)
        {
            EgoCorrosionAction action = weapon.Props.Action;
            Pawn target = null;
            if (action.TakesTarget)
            {
                target = hostilesOnly ? NearestHostile(wielder, weapon.Props.overclockRange) : Nearest(wielder);
                if (target == null) return false;
                wielder.rotationTracker.FaceTarget(target);
            }
            action.Fire(wielder, weapon, target, hostilesOnly);
            return true;
        }

        /// <summary>
        /// After the state ends: AG_EgoExhausted for <paramref name="hours"/> game hours. A new hediff starts at the def's
        /// fallback time, so it is set to exactly this; a pawn still exhausted from before keeps the longer of the two.
        /// </summary>
        public static void Exhaust(Pawn pawn, float hours)
        {
            if (hours <= 0f || pawn.health == null) return;
            int ticks = Mathf.RoundToInt(hours * GenDate.TicksPerHour);
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(EgoDefOf.AG_EgoExhausted);
            bool fresh = hediff == null;
            if (fresh) hediff = pawn.health.AddHediff(EgoDefOf.AG_EgoExhausted);
            HediffComp_Disappears timer = hediff.TryGetComp<HediffComp_Disappears>();
            if (timer == null) return;
            timer.ticksToDisappear = fresh ? ticks : Math.Max(timer.ticksToDisappear, ticks);
            timer.disappearsAfterTicks = fresh ? ticks : Math.Max(timer.disappearsAfterTicks, timer.ticksToDisappear);
        }

        /// <summary>
        /// Rule 3's cost: AG_EgoOverclocked with the weapon's mood and days. The def's stage is 0; the offset is the
        /// memory's own (Thought_Memory.moodOffset), so each weapon sets its cost.
        /// </summary>
        public static void PayOverclock(Pawn pawn, CompProperties_EgoWeapon props)
        {
            if (pawn.needs?.mood == null) return;
            var thought = (Thought_Memory)ThoughtMaker.MakeThought(EgoDefOf.AG_EgoOverclocked);
            thought.moodOffset = props.overclockMood;
            thought.durationTicksOverride = Mathf.RoundToInt(props.overclockMoodDays * GenDate.TicksPerDay);
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
        }
    }
}
