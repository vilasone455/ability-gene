using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    // Owns the actual equipped weapon, not a replacement created on impact.
    public class Projectile_Fuma : Projectile, IThingHolder
    {
        private ThingOwner<ThingWithComps> inner;
        private List<int> hitPawns = new List<int>();
        private IntVec3 lastSafeCell = IntVec3.Invalid;
        private Pawn originalThrower;
        private bool dropping;

        public Projectile_Fuma() => inner = new ThingOwner<ThingWithComps>(this, true);
        public ThingWithComps HeldWeapon => inner.Count == 0 ? null : inner[0];
        public override int UpdateRateTicks => 1;
        public override int DamageAmount => Mathf.Max(1, Mathf.RoundToInt(FumaRules.Damage(hitPawns.Count)
            * VectorEditRegistry.ForceFor(this)));
        public override float ArmorPenetration => 0.2f;

        public static bool Release(Pawn pawn, ThingWithComps weapon, IntVec3 target)
        {
            var shot = (Projectile_Fuma)ThingMaker.MakeThing(FumaDefOf.AG_FumaProjectile);
            try
            {
                GenSpawn.Spawn(shot, pawn.Position, pawn.Map);
                shot.Launch(pawn, pawn.Position.ToVector3Shifted(), target, target, ProjectileHitFlags.All,
                    false, weapon);
                // Launch normally jitters the endpoint. This weapon deliberately does not.
                shot.destination = target.ToVector3Shifted();
                shot.ticksToImpact = Mathf.Max(1, Mathf.CeilToInt(shot.StartingTicksToImpact));
                shot.lifetime = shot.ticksToImpact;
                shot.lastSafeCell = pawn.Position;
                shot.originalThrower = pawn;
                if (pawn.equipment?.Primary != weapon || !pawn.equipment.TryTransferEquipmentToContainer(weapon, shot.inner))
                {
                    shot.Destroy();
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                shot.Destroy();
                Log.Error("[RimArt] Fūma Shuriken launch failed: " + e);
                return false;
            }
        }

        protected override void TickInterval(int delta)
        {
            if (dropping) { Destroy(); return; }
            // This override bypasses Projectile.TickInterval's Harmony prefix.
            if (RecursionRegistry.TryGetCapture(this, out _)) return;
            float from = DistanceCoveredFraction;
            ticksToImpact -= delta;
            lifetime -= delta;
            float to = DistanceCoveredFraction;
            foreach (FumaRules.Cell step in FumaRules.Sweep(origin.x, origin.z, destination.x, destination.z, from, to))
            {
                var cell = new IntVec3(step.X, 0, step.Z);
                if (CompFuma.Blocked(cell, Map)) { Destroy(); return; }
                if (step.Guard) continue;
                Vector3 begin = Vector3.Lerp(origin, destination, (float)step.Entry);
                Vector3 end = Vector3.Lerp(origin, destination, (float)step.Exit);
                foreach (Thing thing in Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor).ToArray())
                    if (thing.TryGetComp<CompProjectileInterceptor>()?.CheckIntercept(this, begin, end) == true)
                    { Destroy(); return; }
                lastSafeCell = cell;
                Position = cell;
                foreach (Pawn pawn in cell.GetThingList(Map).OfType<Pawn>().OrderBy(p => p.thingIDNumber).ToArray())
                {
                    if (pawn == originalThrower || pawn.Dead || hitPawns.Contains(pawn.thingIDNumber)) continue;
                    int amount = DamageAmount;
                    hitPawns.Add(pawn.thingIDNumber);
                    var entry = new BattleLogEntry_RangedImpact(launcher, pawn, intendedTarget.Thing,
                        equipmentDef, def, targetCoverDef);
                    Find.BattleLog.Add(entry);
                    var damage = new DamageInfo(DamageDef, amount, ArmorPenetration, ExactRotation.eulerAngles.y,
                        launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing,
                        !(launcher is Pawn firingPawn) || !firingPawn.Drafted);
                    damage.SetWeaponQuality(equipmentQuality);
                    pawn.TakeDamage(damage).AssociateWithLog(entry);
                }
            }
            if (ticksToImpact <= 0) Destroy();
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false) => Destroy();

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Destroyed) return;
            if (HeldWeapon != null)
            {
                dropping = true;
                Map map = MapHeld;
                IntVec3 cell = lastSafeCell.IsValid ? lastSafeCell : PositionHeld;
                if (map == null || !cell.InBounds(map)) return;
                Position = cell;
                origin = destination = cell.ToVector3Shifted();
                ticksToImpact = 0;
                if (!inner.TryDrop(HeldWeapon, cell, map, ThingPlaceMode.Near, out ThingWithComps dropped)) return;
                dropped.SetForbidden(false, false);
            }
            base.Destroy(mode);
        }

        public ThingOwner GetDirectlyHeldThings() => inner;
        public void GetChildHolders(List<IThingHolder> outChildren)
            => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, inner);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref inner, "fumaWeapon", this);
            Scribe_Collections.Look(ref hitPawns, "fumaHitPawns", LookMode.Value);
            Scribe_Values.Look(ref lastSafeCell, "fumaLastSafeCell", IntVec3.Invalid);
            Scribe_References.Look(ref originalThrower, "fumaThrower");
            Scribe_Values.Look(ref dropping, "fumaDropping");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                inner ??= new ThingOwner<ThingWithComps>(this, true);
                hitPawns ??= new List<int>();
            }
        }
    }
}
