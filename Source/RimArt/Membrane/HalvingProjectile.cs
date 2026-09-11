using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One projectile caught by a membrane, and the Zeno curve it is now following.
    ///
    /// The engine cannot express this on its own. A projectile's speed comes from its def and
    /// its position is a straight lerp along origin->destination driven by ticksToImpact, and
    /// StartingTicksToImpact is a computed property with no setter - so there is no per-instance
    /// way to make one round travel slower than its def says. Instead the projectile is taken
    /// off the engine's clock entirely (Projectile.TickInterval is skipped, and ProjectileCE.Tick
    /// for a round Combat Extended is flying) and its reported position is overridden:
    ///
    ///     d = entryDistance * 0.5 ^ (ticksHeld / halfLifeTicks)
    ///
    /// distance from the point it was caught heading for. That anchor is fixed in world space at
    /// capture, not re-read from the holder: a round is a thing in the world travelling its own
    /// straight line, and re-anchoring it every tick would drag held rounds along behind a
    /// walking carrier, which is nonsense. The carrier stepping aside is instead a release - the
    /// round was never stopped, and it goes back to finishing its original trip.
    ///
    /// The distance halves forever, never reaches zero, and - because each step is a fraction of
    /// the last - the per-tick movement shrinks smoothly rather than stuttering. Nothing is ever
    /// cancelled; the round is still arriving.
    /// </summary>
    public class HalvingProjectile
    {
        public readonly HediffComp_Recursion Holder;

        /// <summary>Where the round was heading when it was caught. Fixed in world space.</summary>
        private readonly Vector3 anchor;

        /// <summary>Unit vector from the anchor back out along the round's line of approach.</summary>
        private readonly Vector3 approach;
        private readonly float entryDistance;
        private readonly float halfLifeTicks;
        private readonly float minDistance;

        private int ticksHeld;

        /// <summary>
        /// <paramref name="entry"/> is where the round crossed into the field, which is not the
        /// same as where it was standing when the scan noticed it. A round moving faster than the
        /// field is wide is already past the carrier by the time it is sampled, and measuring
        /// from there would put the offset on the far side - the curve would hold the round
        /// behind the carrier and recede away from them rather than toward them.
        /// </summary>
        public HalvingProjectile(Thing round, HediffComp_Recursion holder, Vector3 entry,
            float halfLifeTicks, float minDistance)
        {
            Holder = holder;
            this.halfLifeTicks = Mathf.Max(1f, halfLifeTicks);
            this.minDistance = minDistance;

            anchor = holder.Pawn.DrawPos;
            anchor.y = 0f;

            Vector3 offset = entry - anchor;
            offset.y = 0f;

            entryDistance = Mathf.Max(offset.magnitude, minDistance);
            approach = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.forward;
        }

        public void Advance(int delta)
        {
            ticksHeld += delta;
        }

        /// <summary>
        /// Where the round has got to. Clamped at minDistance only so it does not disappear
        /// inside the anchor once the halvings take it below a pixel - the curve itself has no
        /// floor.
        /// </summary>
        public Vector3 CurrentPosition()
        {
            float distance = entryDistance * Mathf.Pow(0.5f, ticksHeld / halfLifeTicks);
            if (distance < minDistance) distance = minDistance;
            return anchor + approach * distance;
        }

        /// <summary>
        /// True once the carrier has walked out from behind their own membrane. The round is
        /// handed straight back rather than following them: it is still on the line it was
        /// fired along, and that line no longer has anyone standing on it.
        /// </summary>
        public bool CarrierHasLeft(float fieldRadius)
        {
            Pawn pawn = Holder.Pawn;
            if (pawn == null || !pawn.Spawned) return true;

            Vector3 offset = pawn.DrawPos - anchor;
            offset.y = 0f;
            return offset.sqrMagnitude > fieldRadius * fieldRadius;
        }

        /// <summary>
        /// Puts the round where the curve says it is.
        ///
        /// Nothing at all for one of the engine's own rounds, whose position is reported by the
        /// postfix on Projectile.ExactPosition. CE reads its rounds out of a field that its
        /// drawing, collision and impact checks all share rather than through one getter, so its
        /// held rounds have to be moved instead of described - and with their tick skipped,
        /// nothing else would move them.
        /// </summary>
        public void PlaceHeld(Thing round)
        {
            RoundBackend backend = Rounds.For(round);
            if (backend == null) return;

            backend.PlaceHeld(round, CurrentPosition());
        }

        /// <summary>
        /// Ambient sound is normally maintained from the round's own tick, which is skipped while
        /// it is held. Rockets and the like would otherwise log a stale sustainer.
        /// </summary>
        public void MaintainSound(Thing round)
        {
            RoundBackend backend = Rounds.For(round);
            if (backend != null) backend.MaintainSound(round);
        }

        /// <summary>
        /// Hands the round back to the engine from wherever the halvings left it, on a straight
        /// line to wherever it was going at its def's own speed - the trip it never stopped
        /// making.
        /// </summary>
        public void Release(Thing round)
        {
            if (round == null || round.Destroyed) return;

            RoundBackend backend = Rounds.For(round);
            if (backend == null) return;

            Vector3 held = CurrentPosition();
            held.y = 0f;

            backend.Resume(round, held);
        }
    }
}
