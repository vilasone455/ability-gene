using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A loosed blade, in flight.
    ///
    /// Everything that decides whether it lands is vanilla: it is a real projectile, so cover,
    /// line of sight and the intervening bodies are resolved by the same code a rifle round
    /// goes through. That is the point of firing each blade from the cell it was standing in
    /// rather than from the carrier - the geometry of where the rain fell is the geometry of
    /// the volley, and a blade planted behind a raider ignores the sandbag in front of him.
    ///
    /// It is a Bullet rather than a bare Projectile so that the damage on the far end is
    /// vanilla's - the same method that resolves a rifle round against armour, a body part and
    /// a hit roll resolves this, and the numbers come off the def.
    ///
    /// It plants itself again where it stops. A blade is not consumed by being thrown; it is
    /// somewhere else now, which is usually somewhere worse for the carrier to have left it.
    /// </summary>
    public class Projectile_Blade : Bullet
    {
        private Pawn owner;

        public void SetOwner(Pawn newOwner)
        {
            owner = newOwner;
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 cell = Position;

            base.Impact(hitThing, blockedByShield);

            if (map == null || !cell.InBounds(map)) return;

            PanoplyUtility.ImpactEffect(cell, map);

            if (blockedByShield) return;

            Thing spawned = GenSpawn.Spawn(PanoplyDefOf.AG_PanoplyBlade, cell, map);
            PlantedBlade blade = spawned as PlantedBlade;
            if (blade != null)
            {
                blade.Configure(owner, Rand.Range(-PanoplyDefaults.PlantedLeanRange,
                    PanoplyDefaults.PlantedLeanRange));
            }
        }

        /// <summary>
        /// Point first, along the line it is travelling. A tumbling blade would be the wrong
        /// read here - a thrown sword that is spinning is one nobody aimed.
        /// </summary>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float heading = (destination - origin).AngleFlat();

            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.Projectile.AltitudeFor();

            PanoplyGraphics.DrawBlade(pos, heading, PanoplyDefaults.BladeDrawSize, 1f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
        }
    }
}
