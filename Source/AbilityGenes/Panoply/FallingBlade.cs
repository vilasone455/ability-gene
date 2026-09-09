using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// A blade on its way down.
    ///
    /// This is a `Skyfaller`, which is the thing RimWorld already has for an object arriving out
    /// of the sky - it is what a drop pod, a meteorite and a shuttle all are. The first version
    /// of this class was not, and hand-rolled the fall out of its own position maths; it looked
    /// wrong for the reason home-made versions of engine features usually do. Vanilla's fall is
    /// not a straight line and a shrinking offset. It is an accelerating approach along a fixed
    /// angle, with a drop-spot shadow on the landing cell, a roof check, an impact sound and
    /// dust thrown up on arrival, and every one of those is a field in the def rather than a
    /// line of code here.
    ///
    /// What is left for this class is the two things vanilla cannot know: that the blade hurts
    /// whatever it lands on, and that it stays standing afterwards.
    /// </summary>
    public class FallingBlade : Skyfaller
    {
        private Pawn owner;
        private float lean;
        private int damage;

        /// <summary>
        /// Sets the blade up and pushes its arrival back by <paramref name="delayTicks"/>.
        ///
        /// Adding to `ticksToImpact` rather than spawning the blade later is what makes a rain
        /// look like rain: every blade is in the air at once and they are at different heights,
        /// because a skyfaller's distance from the ground is derived from how long it still has
        /// to fall. A dozen of them therefore arrive in sequence without anything holding a
        /// timer for the group.
        /// </summary>
        public void Configure(Pawn newOwner, int delayTicks, int impactDamage)
        {
            owner = newOwner;
            damage = impactDamage;
            lean = Rand.Range(-PanoplyDefaults.PlantedLeanRange, PanoplyDefaults.PlantedLeanRange);
            ticksToImpact += delayTicks;
        }

        /// <summary>
        /// The sprite is drawn point-up, and a blade that is falling is pointing where it is
        /// going. `rotateGraphicTowardsDirection` on the def has already tilted it to match the
        /// line it is travelling; this turns it over so the point leads.
        /// </summary>
        protected override void GetDrawPositionAndRotation(ref Vector3 drawLoc, out float extraRotation)
        {
            base.GetDrawPositionAndRotation(ref drawLoc, out extraRotation);
            extraRotation += 180f;
        }

        /// <summary>
        /// What it hits on the way in, and what it leaves behind.
        ///
        /// One thing per cell takes the hit and a pawn is preferred over whatever is built
        /// there - a blade that lands on somebody standing in a doorway has not hit the door.
        /// The blade is planted either way, including through the body it just went into.
        ///
        /// `base.Impact` destroys this thing, so the map and cell are taken first and the
        /// planted blade goes down after - a skyfaller cannot spawn anything from inside its
        /// own impact.
        /// </summary>
        protected override void Impact()
        {
            Map map = Map;
            IntVec3 cell = Position;

            if (map != null && cell.InBounds(map) && damage > 0)
            {
                Thing hit = cell.GetFirstPawn(map);
                if (hit == null) hit = cell.GetFirstBuilding(map);

                if (hit != null)
                {
                    hit.TakeDamage(new DamageInfo(DamageDefOf.Stab, damage, 0.35f, -1f, owner,
                        null, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
                }
            }

            base.Impact();

            if (map == null || !cell.InBounds(map)) return;

            Thing spawned = GenSpawn.Spawn(PanoplyDefOf.AG_PanoplyBlade, cell, map);
            PlantedBlade blade = spawned as PlantedBlade;
            if (blade != null) blade.Configure(owner, lean);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
            Scribe_Values.Look(ref lean, "lean", 0f);
            Scribe_Values.Look(ref damage, "damage", 0);
        }
    }
}
