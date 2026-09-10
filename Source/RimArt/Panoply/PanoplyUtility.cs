using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// The bookkeeping the three abilities share: what the field costs, what is standing in it,
    /// and the two Core effects a blade makes when it arrives.
    /// </summary>
    public static class PanoplyUtility
    {
        private static SoundDef impactSound;
        private static bool impactSoundResolved;

        private static FleckDef dustFleck;
        private static bool dustFleckResolved;

        /// <summary>
        /// Writes the debt to match the field, and takes the hediff off entirely when the last
        /// blade is gone.
        ///
        /// Severity is not accumulated here - it is assigned. The blades *are* the cost, so the
        /// hediff can only ever say what is currently out, and there is no path by which a
        /// carrier with no blades standing is still paying for some.
        /// </summary>
        public static void UpdateDebt(Pawn owner)
        {
            if (owner == null || owner.health == null) return;

            int count = PanoplyRegistry.CountOf(owner);
            Hediff debt = owner.health.hediffSet.GetFirstHediffOfDef(PanoplyDefOf.AG_PanoplyDebt);

            if (count <= 0)
            {
                if (debt != null) owner.health.RemoveHediff(debt);
                return;
            }

            if (debt == null)
            {
                debt = HediffMaker.MakeHediff(PanoplyDefOf.AG_PanoplyDebt, owner);
                debt.Severity = count * PanoplyDefaults.DebtPerBlade;
                owner.health.AddHediff(debt);
                return;
            }

            debt.Severity = count * PanoplyDefaults.DebtPerBlade;
        }

        /// <summary>Blades of one carrier within a radius of a cell, nearest first.</summary>
        public static List<PlantedBlade> BladesNear(Pawn owner, Map map, IntVec3 centre, float radius)
        {
            List<PlantedBlade> near = new List<PlantedBlade>();
            float radiusSquared = radius * radius;

            foreach (PlantedBlade blade in PanoplyRegistry.BladesOf(owner))
            {
                if (blade.Map != map) continue;
                if ((blade.Position - centre).LengthHorizontalSquared > radiusSquared) continue;
                near.Add(blade);
            }

            near.Sort((a, b) =>
                (a.Position - centre).LengthHorizontalSquared.CompareTo(
                    (b.Position - centre).LengthHorizontalSquared));
            return near;
        }

        /// <summary>The blade nearest a point, or null if the carrier has none standing.</summary>
        public static PlantedBlade NearestBlade(Pawn owner, Map map, IntVec3 near)
        {
            PlantedBlade best = null;
            int bestDistance = int.MaxValue;

            foreach (PlantedBlade blade in PanoplyRegistry.BladesOf(owner))
            {
                if (blade.Map != map) continue;
                int distance = (blade.Position - near).LengthHorizontalSquared;
                if (distance >= bestDistance) continue;
                best = blade;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// A blade arriving. Steel into ground, and the dust it lifts.
        ///
        /// Resolved by name rather than through a DefOf so that a missing Core def is a quiet
        /// no-sound rather than a startup error - the same call the involute organ makes.
        /// </summary>
        public static void ImpactEffect(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map)) return;

            if (!impactSoundResolved)
            {
                impactSound = DefDatabase<SoundDef>.GetNamedSilentFail("MeleeHit_Metal_Sharp");
                impactSoundResolved = true;
            }
            if (!dustFleckResolved)
            {
                dustFleck = DefDatabase<FleckDef>.GetNamedSilentFail("DustPuff");
                dustFleckResolved = true;
            }

            if (impactSound != null) impactSound.PlayOneShot(new TargetInfo(cell, map, false));
            if (dustFleck != null) FleckMaker.Static(cell, map, dustFleck, 1.2f);
        }
    }
}
