using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>On the coil gun's bolt def: what it does more than a bullet.</summary>
    public class CoilBoltExtension : DefModExtension
    {
        /// <summary>A mechanoid hit takes this many times the damage (the EMP idea, as damage only: no stun).</summary>
        public float mechDamageFactor = 1.5f;
    }

    /// <summary>
    /// The coil gun's round. Aiming, the hit roll, armour and the battle log are vanilla
    /// <see cref="Bullet"/>; the damage def is AG_CoilBurn (Core's Bullet with an electrical burn). Two additions: a mechanoid hit takes
    /// mechDamageFactor times the damage, and it is drawn as a short jagged bolt (CoilGunShotGraphics)
    /// instead of a texture, with a blue muzzle flash when it leaves and a flash, sparks and a scorch
    /// where it lands (MapComponent_CoilGun draws those two, since the round is gone by then).
    /// </summary>
    public class Projectile_CoilBolt : Bullet
    {
        private static readonly CoilBoltExtension Defaults = new CoilBoltExtension();
        /// <summary>What Impact is hitting, for <see cref="DamageAmount"/>. Not saved: it is set and read within one Impact.</summary>
        private Thing hitting;

        private CoilBoltExtension Ext => def.GetModExtension<CoilBoltExtension>() ?? Defaults;

        public override int DamageAmount
        {
            get
            {
                int amount = base.DamageAmount;
                return hitting is Pawn pawn && CoilArcChain.IsMech(pawn) ? Mathf.RoundToInt(amount * Ext.mechDamageFactor) : amount;
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            Vector3 run = destination - origin;
            float aim = Mathf.Atan2(run.z, run.x) * Mathf.Rad2Deg;
            Map?.GetComponent<MapComponent_CoilGun>()?.AddFlash(new Vector2(origin.x, origin.z), aim);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 at = hitThing is Pawn pawn ? pawn.DrawPos : ExactPosition;
            bool onPawn = hitThing is Pawn, mech = hitThing is Pawn p && CoilArcChain.IsMech(p) && !blockedByShield;
            map?.GetComponent<MapComponent_CoilGun>()?.AddImpact(new Vector2(at.x, at.z), onPawn, mech, thingIDNumber);
            hitting = hitThing;
            try
            {
                map?.GetComponent<MapComponent_CoilGun>()?.Note(hitThing, hitThing != null ? DamageAmount : 0, 0f, false, ExactPosition);
                base.Impact(hitThing, blockedByShield);
            }
            finally
            {
                hitting = null;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector2 feet = new Vector2(origin.x, origin.z);
            Vector2 end = new Vector2(destination.x, destination.z + (usedTarget.Thing is Pawn ? CoilGunGraphics.Lifted(CoilGunGraphics.ChestH) : 0f));
            CoilGunShotGraphics.InFlight(feet, end, DistanceCoveredFraction, Find.TickManager.TicksGame / 60f, thingIDNumber, Map);
        }
    }
}
