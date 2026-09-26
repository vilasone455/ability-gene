using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>On the frost rifle's bolt def: the Chilled stack each hit adds.</summary>
    public class FrostGunBoltExtension : DefModExtension
    {
        /// <summary>A pawn carries at most this many Chilled stacks (the hediff's stages hold what each does).</summary>
        public int maxChilledStacks = 3;
        /// <summary>Each hit sets the Chilled stacks to last this long again.</summary>
        public float chilledSeconds = 10f;
    }

    /// <summary>
    /// The frost rifle's bolt. Aiming, the hit roll, damage and the battle log are vanilla
    /// <see cref="Bullet"/>. Added: a pawn it hits (not stopped by a shield) takes a Chilled stack, and
    /// every impact leaves the picture's frost puff (MapComponent_FrostGun). It is drawn from code
    /// (<see cref="FrostGunGraphics.Bolt"/>): a pale core, a cold glow and a frost trail; the def's
    /// texture is only a fallback for anything that draws the def itself.
    /// </summary>
    public class Projectile_FrostGun : Bullet
    {
        private static readonly FrostGunBoltExtension Defaults = new FrostGunBoltExtension();

        private Vector2 Aim
        {
            get
            {
                Vector3 run = (destination - origin).Yto0();
                return run.sqrMagnitude < 1e-4f ? Vector2.right : new Vector2(run.x, run.z).normalized;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector2 from = new Vector2(origin.x, origin.z), head = new Vector2(drawLoc.x, drawLoc.z);
            VfxDraw.Begin(head);
            FrostGunGraphics.Bolt(from, head, def.projectile.SpeedTilesPerTick * 60f, drawLoc.y, Time.realtimeSinceStartup);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 at = ExactPosition;
            Vector2 aim = Aim;
            base.Impact(hitThing, blockedByShield);
            if (map == null) return;
            Pawn pawn = hitThing as Pawn;
            bool onPawn = pawn != null && pawn.Spawned && pawn.Map == map;
            map.GetComponent<MapComponent_FrostGun>()?.ShotHit(onPawn ? pawn.DrawPos : at, onPawn, aim);
            if (pawn == null || blockedByShield || pawn.Dead) return;
            FrostGunBoltExtension ext = def.GetModExtension<FrostGunBoltExtension>() ?? Defaults;
            FlashFreeze.AddChill(pawn, ext.maxChilledStacks, ext.chilledSeconds);
        }
    }
}
