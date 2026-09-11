using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Tracks who is holding a phase barrier open, and every projectile currently captured
    /// by one.
    ///
    /// Same self-healing shape as <see cref="TimeAlterRegistry"/>: the hediff comp reports
    /// itself every tick and drops itself on removal, so nothing here depends on a callback
    /// firing.
    ///
    /// The two counts exist because both hot paths are extremely hot.
    /// Projectile.ExactPosition is read for every projectile every frame *and* from the
    /// engine's own collision checks; Thing.TakeDamage runs for everything on every map. Both
    /// must cost one static integer read when no membrane is open, which is almost always.
    /// </summary>
    public static class RecursionRegistry
    {
        private static readonly List<HediffComp_Recursion> holders = new List<HediffComp_Recursion>();
        private static readonly Dictionary<Projectile, HalvingProjectile> captured =
            new Dictionary<Projectile, HalvingProjectile>();
        private static readonly List<Projectile> scratch = new List<Projectile>();

        public static int HolderCount => holders.Count;
        public static int CapturedCount => captured.Count;
        public static List<HediffComp_Recursion> Holders => holders;

        public static void Report(HediffComp_Recursion comp)
        {
            if (!holders.Contains(comp)) holders.Add(comp);
        }

        public static void Drop(HediffComp_Recursion comp)
        {
            holders.Remove(comp);
        }

        /// <summary>
        /// The membrane holding this pawn, or null. Only reached once the holder count is
        /// non-zero; the list is at most a handful of entries, so a scan beats a dictionary.
        /// </summary>
        public static HediffComp_Recursion HolderFor(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null) return null;

            for (int i = 0; i < holders.Count; i++)
            {
                if (holders[i].Pawn == pawn) return holders[i];
            }
            return null;
        }

        public static void Capture(Projectile projectile, HalvingProjectile state)
        {
            captured[projectile] = state;
        }

        public static bool TryGetCapture(Projectile projectile, out HalvingProjectile state)
        {
            return captured.TryGetValue(projectile, out state);
        }

        public static void ReleaseCapture(Projectile projectile)
        {
            captured.Remove(projectile);
        }

        /// <summary>
        /// Steps every round this membrane is holding one tick further along its curve, releases
        /// any whose carrier has moved out from behind it, and forgets any destroyed elsewhere. Iteration is over a scratch
        /// list because the callees may remove entries.
        /// </summary>
        public static void AdvanceHeldBy(HediffComp_Recursion comp)
        {
            if (captured.Count == 0) return;

            scratch.Clear();
            foreach (KeyValuePair<Projectile, HalvingProjectile> pair in captured)
            {
                if (pair.Value.Holder == comp) scratch.Add(pair.Key);
            }

            float fieldRadius = comp.Props.fieldRadius;
            for (int i = 0; i < scratch.Count; i++)
            {
                Projectile projectile = scratch[i];
                if (projectile.Destroyed)
                {
                    captured.Remove(projectile);
                    continue;
                }

                HalvingProjectile state = captured[projectile];

                // The carrier walking off their own line hands the round straight back.
                if (state.CarrierHasLeft(fieldRadius))
                {
                    state.Release(projectile);
                    captured.Remove(projectile);
                    continue;
                }

                state.Advance(1);
                state.MaintainSound(projectile);
            }
        }

        /// <summary>
        /// Hands every projectile held by one membrane back to the engine. Called when the
        /// membrane closes for any reason - expiry, cancellation, the holder dying.
        /// </summary>
        public static void ReleaseAllHeldBy(HediffComp_Recursion comp)
        {
            if (captured.Count == 0) return;

            scratch.Clear();
            foreach (KeyValuePair<Projectile, HalvingProjectile> pair in captured)
            {
                if (pair.Value.Holder == comp) scratch.Add(pair.Key);
            }

            for (int i = 0; i < scratch.Count; i++)
            {
                Projectile projectile = scratch[i];
                captured[projectile].Release(projectile);
                captured.Remove(projectile);
            }
        }
    }
}
