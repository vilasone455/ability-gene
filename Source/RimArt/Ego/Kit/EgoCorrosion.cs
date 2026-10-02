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
        private struct Pending
        {
            public Pawn pawn;
            public CompEgoWeapon weapon;
        }

        /// <summary>
        /// Uses that passed the roll and wait for the pawn's verb to finish its burst (<see cref="Tick"/>). The roll runs
        /// inside Verb.TryCastNextBurstShot; starting the state there would stop the attack job while the verb goes on
        /// bursting at the old target and then calls the ended job's castCompleteCallback. Not saved: an entry lives a
        /// few ticks.
        /// </summary>
        private static readonly List<Pending> pending = new List<Pending>();

        /// <summary>True while a weapon's action fires, so a use the action itself makes through the verb never rolls (Overclock, the corroded firing).</summary>
        private static bool firing;

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
        /// Rule 1, one use of the weapon. True when the roll passed; the pawn is then corroded once its verb has finished
        /// the burst (<see cref="Tick"/>), the same tick for a single shot. A pawn already in a mental state of any kind
        /// (corroded, berserk, a False Face) does not roll, and neither does a use the weapon's own action makes.
        /// </summary>
        public static bool Roll(Pawn pawn, CompEgoWeapon weapon)
        {
            if (firing || pawn == null || pawn.mindState == null || pawn.Dead || pawn.Downed || pawn.InMentalState) return false;
            float chance = Chance(pawn, weapon.Props);
            if (chance <= 0f || !Rand.Chance(chance)) return false;
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].pawn == pawn) return true;
            pending.Add(new Pending { pawn = pawn, weapon = weapon });
            return true;
        }

        /// <summary>
        /// Every tick from <see cref="MapComponent_EgoCorrosion"/> (MapPostTick, after the pawns): corrodes each pending
        /// pawn on <paramref name="map"/> whose verb is no longer bursting. A pawn that went down, died, left the map or
        /// put the weapon away in the meantime is dropped.
        /// </summary>
        public static void Tick(Map map)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending p = pending[i];
                if (p.pawn.Dead || p.pawn.Downed || !p.pawn.Spawned || p.pawn.equipment?.Primary != p.weapon.parent)
                {
                    pending.RemoveAt(i);
                    continue;
                }
                if (p.pawn.Map != map) continue;
                Verb verb = p.weapon.PrimaryVerb;
                if (verb != null && verb.Bursting) continue;
                pending.RemoveAt(i);
                Corrode(p.pawn, p.weapon);
            }
        }

        /// <summary>
        /// Rule 2: the weapon takes its wielder. False when the pawn is already in a mental state of any kind (the game
        /// would end that state first; corrosion never replaces Berserk or a False Face) or the game refuses the state
        /// (asleep, tutorial).
        /// </summary>
        public static bool Corrode(Pawn pawn, CompEgoWeapon weapon)
        {
            if (pawn.Dead || pawn.mindState == null || pawn.InMentalState) return false;
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
        public static Pawn Nearest(Pawn wielder)
        {
            if (!wielder.Spawned) return null;
            float best = float.MaxValue;
            Pawn nearest = null;
            IReadOnlyList<Pawn> pawns = wielder.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == wielder || p.Dead) continue;
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
        /// The nearest hostile pawn, not downed, within <paramref name="range"/> cells; null when there is none. Reads the
        /// map's attack-target cache (hostile factions, aggro mental states, factionless humanlikes), not every pawn: the
        /// Overclock button asks every frame the wielder is selected.
        /// </summary>
        public static Pawn NearestHostile(Pawn wielder, float range)
        {
            if (!wielder.Spawned) return null;
            float best = range * range;
            Pawn nearest = null;
            List<IAttackTarget> targets = wielder.Map.attackTargetsCache.GetPotentialTargetsFor(wielder);
            for (int i = 0; i < targets.Count; i++)
            {
                if (!(targets[i].Thing is Pawn p) || p == wielder || p.Dead || p.Downed || !p.Spawned) continue;
                float d = (p.Position - wielder.Position).LengthHorizontalSquared;
                if (d <= best)
                {
                    best = d;
                    nearest = p;
                }
            }
            return nearest;
        }

        public static bool HostileInRange(Pawn wielder, float range) => NearestHostile(wielder, range) != null;

        /// <summary>
        /// The target of one firing: the nearest pawn of any faction while corroded, the nearest hostile within
        /// overclockRange for Overclock (<paramref name="hostilesOnly"/>). Null when there is none.
        /// </summary>
        public static Pawn Target(Pawn wielder, CompEgoWeapon weapon, bool hostilesOnly) =>
            hostilesOnly ? NearestHostile(wielder, weapon.Props.overclockRange) : Nearest(wielder);

        /// <summary>One firing at <see cref="Target"/>.</summary>
        public static bool Fire(Pawn wielder, CompEgoWeapon weapon, bool hostilesOnly) =>
            Fire(wielder, weapon, Target(wielder, weapon, hostilesOnly), hostilesOnly);

        /// <summary>
        /// One firing of the weapon's action at <paramref name="target"/> (from <see cref="Target"/>). A targeted action
        /// turns the wielder to it first and does not fire when it is null; an area action ignores it. Uses of the verb
        /// the action makes do not roll.
        /// </summary>
        public static bool Fire(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly)
        {
            EgoCorrosionAction action = weapon.Props.Action;
            if (action.TakesTarget)
            {
                if (target == null) return false;
                wielder.rotationTracker.FaceTarget(target);
            }
            firing = true;
            try
            {
                action.Fire(wielder, weapon, target, hostilesOnly);
            }
            finally
            {
                firing = false;
            }
            return true;
        }

        /// <summary>
        /// After the state ends: AG_EgoExhausted for <paramref name="hours"/> game hours. A new hediff starts at the def's
        /// fallback time, so it is set to exactly this; a pawn still exhausted from before keeps the longer of the two.
        /// </summary>
        public static void Exhaust(Pawn pawn, float hours)
        {
            if (hours <= 0f || pawn.health == null) return;
            SetTimedHediff(pawn, EgoDefOf.AG_EgoExhausted, Mathf.RoundToInt(hours * GenDate.TicksPerHour));
        }

        /// <summary>
        /// <paramref name="def"/> (a hediff with HediffComp_Disappears) for <paramref name="ticks"/>: a new one is set to
        /// exactly that, since its def's time is only a fallback, and one the pawn already has keeps the longer of the two.
        /// </summary>
        public static void SetTimedHediff(Pawn pawn, HediffDef def, int ticks)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            bool fresh = hediff == null;
            if (fresh) hediff = pawn.health.AddHediff(def);
            HediffComp_Disappears timer = hediff.TryGetComp<HediffComp_Disappears>();
            timer?.SetDuration(fresh ? ticks : Math.Max(timer.ticksToDisappear, ticks));
        }

        /// <summary>
        /// A memory of <paramref name="def"/> giving <paramref name="mood"/> (Thought_Memory.moodOffset) for
        /// <paramref name="ticks"/>: <paramref name="existing"/> renewed with these numbers, or a new memory when it is null.
        /// The def's stage mood and duration are fallbacks.
        /// </summary>
        public static void SetMemory(Pawn pawn, ThoughtDef def, Thought_Memory existing, int mood, int ticks)
        {
            Thought_Memory thought = existing ?? (Thought_Memory)ThoughtMaker.MakeThought(def);
            thought.moodOffset = mood;
            thought.durationTicksOverride = ticks;
            if (existing != null) thought.Renew();
            else pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
        }

        /// <summary>
        /// An area action's targets, into <paramref name="into"/>: every other living spawned pawn within
        /// <paramref name="radius"/> cells of the wielder, any faction, downed ones too; with <paramref name="hostilesOnly"/>
        /// (Overclock) only hostiles that are standing. A copy, so the action can kill from it while the map's list changes.
        /// </summary>
        public static List<Pawn> PawnsAround(Pawn wielder, float radius, bool hostilesOnly, List<Pawn> into)
        {
            into.Clear();
            IReadOnlyList<Pawn> pawns = wielder.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == wielder || pawn.Dead || !pawn.Position.InHorDistOf(wielder.Position, radius)) continue;
                if (hostilesOnly && (pawn.Downed || !pawn.HostileTo(wielder))) continue;
                into.Add(pawn);
            }
            return into;
        }

        /// <summary>
        /// Rule 3's cost: AG_EgoOverclocked with the weapon's mood and days. The def's stage is 0; the offset is the
        /// memory's own (Thought_Memory.moodOffset), so each weapon sets its cost. At the def's stack limit the game would
        /// only renew the oldest memory and keep its numbers, so that memory is renewed here with this weapon's instead.
        /// </summary>
        public static void PayOverclock(Pawn pawn, CompProperties_EgoWeapon props)
        {
            if (pawn.needs?.mood == null) return;
            ThoughtDef def = EgoDefOf.AG_EgoOverclocked;
            MemoryThoughtHandler memories = pawn.needs.mood.thoughts.memories;
            Thought_Memory oldest = memories.NumMemoriesOfDef(def) >= def.stackLimit ? memories.OldestMemoryOfDef(def) : null;
            SetMemory(pawn, def, oldest, props.overclockMood, Mathf.RoundToInt(props.overclockMoodDays * GenDate.TicksPerDay));
        }
    }
}
