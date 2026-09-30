using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// How long Instant Transmission (and the Warp Kamehameha's jump) takes to lock onto a cell. The
    /// numbers are on <see cref="CompProperties_InstantTransmission"/>:
    ///
    /// - Life energy of one pawn: <see cref="GokuLifeEnergy.Of"/>. Mechanoids have none.
    /// - The lock: the strongest life energy within lockRadius cells of the destination, Goku and his
    ///   passenger left out (they travel with him). 0 when nobody is there.
    /// - Channel = (baseSeconds + secondsPerCell x distance) x (1 + emptyFactor x (1 - lock)), at most
    ///   maxSeconds. With the defaults a healthy lock 20 cells away is 0.7 s, an empty cell 2.8 s.
    /// </summary>
    public static class GokuTransmissionLock
    {
        private static CompProperties_InstantTransmission Props => GokuBusy.Props<CompProperties_InstantTransmission>(GokuDefOf.AG_GokuInstantTransmission);

        /// <summary>The pawn with the strongest life energy within the lock radius of <paramref name="cell"/>, or null; <paramref name="energy"/> is its value.</summary>
        public static Pawn Strongest(Map map, IntVec3 cell, Pawn caster, Pawn passenger, out float energy)
        {
            energy = 0f;
            CompProperties_InstantTransmission p = Props;
            if (map == null || p == null || !cell.InBounds(map)) return null;
            Pawn best = null;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(cell, map, p.lockRadius, true))
            {
                if (!(thing is Pawn pawn) || pawn == caster || pawn == passenger) continue;
                float e = GokuLifeEnergy.Of(pawn);
                if (e > energy) { energy = e; best = pawn; }
            }
            return best;
        }

        /// <summary>Channel seconds for a jump of <paramref name="distance"/> cells onto a lock of <paramref name="energy"/>.</summary>
        public static float Seconds(float distance, float energy)
        {
            CompProperties_InstantTransmission p = Props;
            if (p == null) return 0.5f;
            float s = (p.baseSeconds + p.secondsPerCell * distance) * (1f + p.emptyFactor * (1f - Mathf.Clamp01(energy)));
            return Mathf.Min(s, p.maxSeconds);
        }

        /// <summary>Channel seconds for <paramref name="caster"/> to jump from <paramref name="from"/> to <paramref name="dest"/>, and the pawn it locks onto.</summary>
        public static float Seconds(Pawn caster, IntVec3 from, IntVec3 dest, Pawn passenger, out Pawn locked, out float energy)
        {
            locked = Strongest(caster?.Map, dest, caster, passenger, out energy);
            return Seconds((dest - from).LengthHorizontal, energy);
        }

        /// <summary>The label at the mouse while a destination is picked: the channel time and what Goku locks onto.</summary>
        public static string Label(Pawn caster, IntVec3 from, IntVec3 dest, Pawn passenger)
        {
            float s = Seconds(caster, from, dest, passenger, out Pawn locked, out float energy);
            string lockText = locked == null ? "no life energy near" : "locked on " + locked.LabelShort + " (" + energy.ToStringPercent() + ")";
            return s.ToString("0.0") + " s, " + lockText;
        }

        /// <summary>While a destination is picked: the lock radius round the cell and a highlight on the pawn Goku would lock onto.</summary>
        public static void DrawLock(Pawn caster, IntVec3 dest, Pawn passenger)
        {
            Map map = caster?.Map;
            CompProperties_InstantTransmission p = Props;
            if (map == null || p == null || !dest.InBounds(map)) return;
            GenDraw.DrawRadiusRing(dest, p.lockRadius);
            Pawn locked = Strongest(map, dest, caster, passenger, out _);
            if (locked != null) GenDraw.DrawTargetHighlight(locked);
        }
    }

    /// <summary>
    /// Instant Transmission's verb: the warmup is the lock's channel time for the chosen destination
    /// instead of the def's fixed warmupTime.
    /// </summary>
    public class Verb_GokuTransmission : Verb_CastAbility
    {
        /// <summary>
        /// The vanilla warmup multiplies this by AimingDelayFactor (Trigger-happy halves it, Careful
        /// shooter adds 25 %). Feeling for ki is not aiming a gun, so the factor is divided out here and
        /// the stance lasts the lock's seconds.
        /// </summary>
        public override float WarmupTime
        {
            get
            {
                Pawn caster = CasterPawn;
                if (caster == null || !caster.Spawned || !currentDestination.IsValid) return base.WarmupTime;
                Pawn passenger = currentTarget.Pawn == caster ? null : currentTarget.Pawn;
                float seconds = GokuTransmissionLock.Seconds(caster, caster.Position, currentDestination.Cell, passenger, out _, out _);
                return seconds / Mathf.Max(0.01f, caster.GetStatValue(StatDefOf.AimingDelayFactor));
            }
        }

        /// <summary>
        /// Stance_Warmup drops the cast when the target leaves range. The passenger is not held: if it
        /// walks off during the channel, Goku goes alone (Apply leaves out a passenger that is not next
        /// to him), so during this verb's own warmup the range check always passes.
        /// </summary>
        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            if (CasterPawn?.stances?.curStance is Stance_Warmup warmup && warmup.verb == this) return true;
            return base.CanHitTargetFrom(root, targ);
        }
    }
}
