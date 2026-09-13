using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One grenade that has been thrown but has not left the hand yet.
    ///
    /// The animation and the projectile are two separate things that have to agree on one moment.
    /// Melee Animation draws the arm; it knows nothing about grenades and will not tell anybody
    /// when the hand opens. So the cast works out which tick that is - the clip's length times
    /// <see cref="ThrowAnimation.Clips.ReleaseFraction"/> - and parks the launch here until then.
    ///
    /// Without an animation there is nothing to wait for and the fuse is zero, which is the path
    /// every pawn takes when Melee Animation is not installed.
    /// </summary>
    public class PendingThrow : IExposable
    {
        private Pawn thrower;
        private LocalTargetInfo target;
        private ThingDef projectile;
        private int releaseTick;

        // What the thrower aimed at, when a miss sends the object somewhere else, and what the
        // projectile may collide with. Grenades leave both at their defaults: aimed at the cell
        // they land on, hitting only that.
        private LocalTargetInfo intendedTarget = LocalTargetInfo.Invalid;
        private ProjectileHitFlags hitFlags = ProjectileHitFlags.IntendedTarget;

        /// <summary>Required by Scribe. Every field is written back by ExposeData.</summary>
        public PendingThrow() { }

        public PendingThrow(Pawn thrower, LocalTargetInfo target, ThingDef projectile, int releaseTick)
        {
            this.thrower = thrower;
            this.target = target;
            this.projectile = projectile;
            this.releaseTick = releaseTick;
        }

        public PendingThrow(Pawn thrower, LocalTargetInfo target, ThingDef projectile, int releaseTick,
                            LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags)
            : this(thrower, target, projectile, releaseTick)
        {
            this.intendedTarget = intendedTarget;
            this.hitFlags = hitFlags;
        }

        /// <summary>Ticks the throw. Returns false once it has been launched or given up on.</summary>
        public bool Tick(Map map)
        {
            if (Find.TickManager.TicksGame < releaseTick) return true;

            Launch(map);
            return false;
        }

        /// <summary>
        /// Puts the grenade in the air.
        ///
        /// A thrower who died, was downed or left the map during the wind-up drops the throw
        /// rather than launching from an empty cell. The half-second of animation they did play
        /// reads perfectly well as someone being hit before they could finish the throw - and the
        /// alternative, a grenade from nowhere, does not.
        /// </summary>
        private void Launch(Map map)
        {
            if (map == null || projectile == null) return;
            if (thrower == null || !thrower.Spawned || thrower.Map != map) return;
            if (thrower.Dead || thrower.Downed) return;
            if (!target.IsValid) return;

            Projectile shot = ThingMaker.MakeThing(projectile) as Projectile;
            if (shot == null) return;

            GenSpawn.Spawn(shot, thrower.Position, map);
            LocalTargetInfo intended = intendedTarget.IsValid ? intendedTarget : target;
            shot.Launch(thrower, thrower.DrawPos, target, intended, hitFlags);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref thrower, "thrower");
            Scribe_TargetInfo.Look(ref target, "target");
            Scribe_Defs.Look(ref projectile, "projectile");
            Scribe_Values.Look(ref releaseTick, "releaseTick", 0);
            Scribe_TargetInfo.Look(ref intendedTarget, "intendedTarget");
            Scribe_Values.Look(ref hitFlags, "hitFlags", ProjectileHitFlags.IntendedTarget);
        }
    }
}
