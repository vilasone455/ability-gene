using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The beacon in flight. It arrives, projects, and is gone.
    ///
    /// A projectile class of our own rather than a damage def with a worker on it, which is how
    /// the frost bomb's effect is hung. The two are not the same shape: the freeze is something an
    /// explosion does, so it belongs on the explosion and fires for a bomb cooking off in a fire
    /// as readily as for a thrown one. A decoy is something a *thrower* does, and needs to know
    /// who threw it - there is nobody to copy in a warehouse fire.
    ///
    /// Nothing is left behind. The emitter is not spawned as a separate object under the decoy:
    /// the decoy is the whole of what arrives, which is what makes "it leaves nothing" a fact
    /// about the code rather than a promise about cleanup.
    /// </summary>
    public class Projectile_MimicBeacon : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // All of it read before the base call, which destroys this.
            Map map = Map;
            IntVec3 cell = ExactPosition.ToIntVec3();
            Pawn thrower = launcher as Pawn;

            base.Impact(hitThing, blockedByShield);

            if (map == null || thrower == null) return;

            if (MimicProjection.Spawn(thrower, cell, map) == null)
            {
                Messages.Message("AG_MimicNoRoom".Translate(), new TargetInfo(cell, map),
                    MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}
