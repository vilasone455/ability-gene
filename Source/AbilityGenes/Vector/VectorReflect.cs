using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>Turning one projectile around. Used by reflection's per-tick bounce.</summary>
    public static class VectorReflect
    {
        /// <summary>
        /// Destroys the incoming round and launches a fresh one of the same def back along its
        /// own line. The reflector is set as the launcher, so the new round will not hit them
        /// and the kill is credited where it belongs.
        /// </summary>
        public static void SendBack(Projectile projectile, Thing reflector, Thing target)
        {
            Map map = projectile.Map;
            if (map == null || target == null || !target.Spawned) return;

            ThingDef def = projectile.def;
            Vector3 origin = projectile.ExactPosition;
            IntVec3 cell = origin.ToIntVec3();
            if (!cell.InBounds(map)) return;

            projectile.Destroy();

            Projectile sentBack = (Projectile)GenSpawn.Spawn(def, cell, map);
            sentBack.Launch(reflector, origin, target, target, ProjectileHitFlags.IntendedTarget);
        }
    }
}
