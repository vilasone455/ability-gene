using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// What Samehada does when it hits: Feed on every landed melee hit on a flesh pawn, and Shark Skin's
    /// sweep of the cells in front. Called from Patches_Samehada.
    /// </summary>
    public static class SamehadaFeeding
    {
        private static readonly Func<Verb_MeleeAttack, LocalTargetInfo, DamageWorker.DamageResult> ApplyMelee =
            AccessTools.MethodDelegate<Func<Verb_MeleeAttack, LocalTargetInfo, DamageWorker.DamageResult>>(
                AccessTools.Method(typeof(Verb_MeleeAttack), "ApplyMeleeDamageToTarget"));

        private static List<Hediff_Injury> Injuries = new List<Hediff_Injury>();
        private static readonly List<Pawn> Swept = new List<Pawn>();

        /// <summary>
        /// Feed: the target gets a Drained stack (its time set again), the holder heals, the blade takes a
        /// charge. Nothing for a target that is not flesh (mechanoids, most entities that are not alive).
        /// Returns whether it fed.
        /// </summary>
        public static bool Feed(Pawn holder, CompSamehada blade, Pawn target)
        {
            if (holder == null || blade == null || target?.RaceProps == null || !target.RaceProps.IsFlesh) return false;
            CompProperties_Samehada p = blade.Props;
            if (!target.Dead && target.health != null) AddDrained(target, p);
            Heal(holder, p.healPerHit);
            bool full = blade.Charges >= p.maxCharges;
            int gained = blade.Fed();
            Map map = holder.MapHeld;
            map?.GetComponent<MapComponent_Samehada>()?.Hit(holder, target, gained > 0, full);
            SamehadaSound.Play(SamehadaSound.Bite, map, target.PositionHeld);
            return true;
        }

        public static void AddDrained(Pawn target, CompProperties_Samehada p)
        {
            Hediff drained = target.health.hediffSet.GetFirstHediffOfDef(SamehadaDefOf.AG_SamehadaDrained);
            if (drained == null)
            {
                drained = HediffMaker.MakeHediff(SamehadaDefOf.AG_SamehadaDrained, target);
                drained.Severity = 1f;
                target.health.AddHediff(drained);
            }
            else drained.Severity = Mathf.Min(p.maxDrainedStacks, Mathf.Round(drained.Severity) + 1f);
            drained.TryGetComp<HediffComp_Disappears>()?.SetDuration(Mathf.RoundToInt(p.drainedSeconds * 60f));
            target.MapHeld?.GetComponent<MapComponent_Samehada>()?.Drained(target);
        }

        public static int Stacks(Pawn pawn)
        {
            Hediff drained = pawn?.health?.hediffSet?.GetFirstHediffOfDef(SamehadaDefOf.AG_SamehadaDrained);
            return drained == null ? 0 : Mathf.RoundToInt(drained.Severity);
        }

        /// <summary>
        /// Heals <paramref name="amount"/> hit points off the pawn's injuries that are not scars, in the
        /// order the health tab lists them, as Core's regeneration (Pawn_HealthTracker) spreads its amount.
        /// </summary>
        public static float Heal(Pawn pawn, float amount)
        {
            if (pawn?.health?.hediffSet == null || amount <= 0f) return 0f;
            Injuries.Clear();
            pawn.health.hediffSet.GetHediffs(ref Injuries, h => !h.IsPermanent());
            float healed = 0f;
            for (int i = 0; i < Injuries.Count && amount > 0f; i++)
            {
                Hediff_Injury injury = Injuries[i];
                float part = Mathf.Min(amount, injury.Severity);
                if (part <= 0f) continue;
                injury.Heal(part);
                amount -= part;
                healed += part;
            }
            return healed;
        }

        /// <summary>
        /// Shark Skin's sweep, once per melee attack with the blade: every hostile pawn in the cells within
        /// sweepRadius of the holder and sweepHalfAngle degrees of <paramref name="aimCell"/>'s direction,
        /// other than <paramref name="mainTarget"/> (the attack's own roll decides that one), takes the
        /// attack's damage with no roll and is fed.
        /// </summary>
        public static int Sweep(Pawn holder, Verb_MeleeAttack verb, IntVec3 from, IntVec3 aimCell, Thing mainTarget)
        {
            Map map = holder?.Map;
            CompProperties_SamehadaSharkSkin props = SamehadaDefOf.AG_Samehada_SharkSkin.comps?.Find(c => c is CompProperties_SamehadaSharkSkin) as CompProperties_SamehadaSharkSkin;
            if (map == null || props == null) return 0;
            var aim = new Vector2(aimCell.x - from.x, aimCell.z - from.z);
            if (aim.sqrMagnitude < 0.01f) return 0;
            float aimDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            Swept.Clear();
            foreach (IntVec3 c in SweepCells(from, aimCell, props.sweepRadius, props.sweepHalfAngle))
            {
                if (!c.InBounds(map)) continue;
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i] is Pawn p && p != holder && p != mainTarget && !p.Dead && p.HostileTo(holder) && !Swept.Contains(p)) Swept.Add(p);
            }
            map.GetComponent<MapComponent_Samehada>()?.Attack(holder, aimDeg, props.sweepRadius, props.sweepHalfAngle);
            SamehadaSound.Play(SamehadaSound.Sweep, map, holder.Position);
            for (int i = 0; i < Swept.Count; i++)
            {
                Pawn p = Swept[i];
                if (p.Dead || !p.Spawned) continue;
                // The attack's own damage (the Feed postfix on it feeds the blade).
                ApplyMelee(verb, new LocalTargetInfo(p));
            }
            return Swept.Count;
        }

        /// <summary>The cells a sweep from <paramref name="from"/> toward <paramref name="aimCell"/> covers.</summary>
        public static IEnumerable<IntVec3> SweepCells(IntVec3 from, IntVec3 aimCell, float radius, float halfAngle)
        {
            var aim = new Vector2(aimCell.x - from.x, aimCell.z - from.z);
            if (aim.sqrMagnitude < 0.01f) yield break;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(from, radius, false))
            {
                var v = new Vector2(c.x - from.x, c.z - from.z);
                if (Vector2.Angle(aim, v) <= halfAngle + 0.01f) yield return c;
            }
        }
    }

    /// <summary>The kit's sounds: vanilla placeholders until it has its own.</summary>
    internal static class SamehadaSound
    {
        internal static SoundDef Bite => SoundDef.Named("Pawn_Melee_HumanBite_Hit");
        internal static SoundDef Tear => SoundDef.Named("Pawn_Melee_SmallScratch_Miss");
        internal static SoundDef Flare => SoundDef.Named("Hive_Spawn");
        internal static SoundDef Sweep => SoundDef.Named("Pawn_Melee_BigBash_Miss");
        internal static SoundDef Merge => SoundDef.Named("Hive_Spawn");
        internal static SoundDef Revert => SoundDef.Named("Hive_Spawn");

        public static void Play(SoundDef sound, Map map, IntVec3 cell)
        {
            if (sound == null || map == null || !cell.InBounds(map)) return;
            sound.PlayOneShot(new TargetInfo(cell, map, false));
        }
    }
}
