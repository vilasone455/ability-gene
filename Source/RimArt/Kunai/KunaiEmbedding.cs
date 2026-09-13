using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Kunai sticking in pawns and being pulled back out.
    ///
    /// A kunai that hits a pawn and does not break sticks in the body part its injury landed on,
    /// as a <see cref="Hediff_EmbeddedKunai"/>, up to <see cref="KunaiDefaults.MaxEmbeddedPerPawn"/>.
    /// A hit that armour stopped (no new injury), a killing hit, or a hit past the cap drops the
    /// kunai on the ground as before.
    ///
    /// Pulling a kunai out gives the item back and adds a cut to that part. From a standing,
    /// awake, hostile pawn it is a melee move with a roll; from anyone else it always works.
    /// </summary>
    public static class KunaiEmbedding
    {
        /// <summary>Active stuck kunai on this pawn.</summary>
        public static int CountOn(Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return 0;
            int count = 0;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i] is Hediff_EmbeddedKunai kunai && kunai.Active) count++;
            return count;
        }

        /// <summary>The most recently stuck active kunai on this pawn, or null.</summary>
        public static Hediff_EmbeddedKunai Newest(Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            for (int i = hediffs.Count - 1; i >= 0; i--)
                if (hediffs[i] is Hediff_EmbeddedKunai kunai && kunai.Active) return kunai;
            return null;
        }

        /// <summary>
        /// Sticks a kunai into <paramref name="pawn"/> after a hit. <paramref name="before"/> is the
        /// pawn's hediffs from just before the damage; the kunai goes into the part of the first new
        /// injury. Returns false if nothing was stuck, and the caller drops the kunai.
        /// </summary>
        public static bool TryEmbed(Pawn pawn, HashSet<Hediff> before)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || before == null) return false;
            if (CountOn(pawn) >= KunaiDefaults.MaxEmbeddedPerPawn) return false;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            Hediff_Injury wound = null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury injury && injury.Part != null && !before.Contains(injury))
                {
                    wound = injury;
                    break;
                }
            }
            if (wound == null || pawn.health.hediffSet.PartIsMissing(wound.Part)) return false;

            var kunai = (Hediff_EmbeddedKunai)HediffMaker.MakeHediff(KunaiDefOf.AG_EmbeddedKunai, pawn, wound.Part);
            kunai.wound = wound;
            pawn.health.AddHediff(kunai, wound.Part);
            return true;
        }

        /// <summary>True when pulling from this pawn is a fight: standing, awake, hostile, not a prisoner.</summary>
        public static bool IsFighting(Pawn puller, Pawn target)
        {
            return !target.Downed && target.Awake() && !target.IsPrisonerOfColony && target.HostileTo(puller);
        }

        /// <summary>
        /// Chance a pull succeeds. Against a fighting target this is Verb_MeleeAttack's roll: the
        /// puller's melee hit chance, then the target's melee dodge chance unless it is stunned.
        /// </summary>
        public static float SuccessChance(Pawn puller, Pawn target)
        {
            if (!IsFighting(puller, target)) return 1f;
            float hit = puller.GetStatValue(StatDefOf.MeleeHitChance);
            float dodge = target.stances?.stunner != null && target.stances.stunner.Stunned
                ? 0f
                : target.GetStatValue(StatDefOf.MeleeDodgeChance);
            return Mathf.Clamp01(hit * (1f - dodge));
        }

        /// <summary>
        /// Pulls the newest stuck kunai out of <paramref name="target"/>. Rolls first against a
        /// fighting target. Returns true if a kunai came out.
        /// </summary>
        public static bool TryPull(Pawn puller, Pawn target)
        {
            Hediff_EmbeddedKunai kunai = Newest(target);
            if (kunai == null || puller == null) return false;

            bool fighting = IsFighting(puller, target);
            if (fighting && !Rand.Chance(SuccessChance(puller, target)))
            {
                MoteMaker.ThrowText(target.DrawPos, target.Map, "Missed", 3.65f);
                return false;
            }

            BodyPartRecord part = kunai.Part;
            kunai.MarkPulled();
            target.health.RemoveHediff(kunai);
            GiveKunai(puller);

            // The cut. Ignores armour (the blade is already inside) and does not spread to other
            // parts. Kept below the part's remaining health so the pull itself never removes a
            // limb; the bleeding can still kill. Only flesh bleeds, so mechs just lose the kunai.
            if (part != null && target.RaceProps.IsFlesh && !target.health.hediffSet.PartIsMissing(part))
            {
                float severity = Mathf.Min(KunaiDefaults.PullCutSeverity,
                                           target.health.hediffSet.GetPartHealth(part) - 1f);
                if (severity >= 1f)
                {
                    var dinfo = new DamageInfo(DamageDefOf.Cut, severity, 0f, -1f,
                                               fighting || target.HostileTo(puller) ? puller : null, part);
                    dinfo.SetIgnoreArmor(true);
                    dinfo.SetAllowDamagePropagation(false);
                    target.TakeDamage(dinfo);
                }
            }
            return true;
        }

        /// <summary>
        /// Gives one pulled kunai to <paramref name="puller"/>: into their kunai belt if it has room,
        /// otherwise onto the ground next to them.
        /// </summary>
        private static void GiveKunai(Pawn puller)
        {
            Thing item = ThingMaker.MakeThing(KunaiDefOf.AG_Kunai);
            CompApparelReloadable belt = KunaiBelt.WornBy(puller);
            if (belt != null && belt.NeedsReload(true)) belt.ReloadFrom(item);
            if (!item.Destroyed && item.stackCount > 0 && puller.Spawned)
                GenPlace.TryPlaceThing(item, puller.Position, puller.Map, ThingPlaceMode.Near);
        }

        /// <summary>Places one kunai item near <paramref name="cell"/>.</summary>
        public static void DropKunai(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map)) return;
            GenPlace.TryPlaceThing(ThingMaker.MakeThing(KunaiDefOf.AG_Kunai), cell, map, ThingPlaceMode.Near);
        }
    }
}
