using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// A blade standing in the ground where it landed.
    ///
    /// It is not an item, and that is a decision rather than a shortcut. An item obeys two
    /// vanilla rules that would quietly wreck this: only one item stack may sit in a cell, so
    /// rain could not put two blades near each other, and a haulable on the floor generates
    /// haul jobs, so a colonist would tidy the field away mid-fight. A thing of its own has
    /// neither problem, draws itself at whatever angle it likes, and can act later if sentinel
    /// or hedge are ever added.
    ///
    /// What it costs to leave one standing is not stored here. The debt is recomputed from the
    /// registry every time a blade appears or leaves - see <see cref="PanoplyUtility.UpdateDebt"/>.
    /// </summary>
    public class PlantedBlade : Building
    {
        private Pawn owner;

        /// <summary>Degrees off vertical, rolled on landing. Purely how it looks.</summary>
        private float lean;

        /// <summary>Absolute game tick at which the body takes this blade back.</summary>
        private int expireTick;

        /// <summary>Ticks left before this blade launches, or -1 when it is standing still.</summary>
        private int launchTicks = -1;

        private LocalTargetInfo launchTarget = LocalTargetInfo.Invalid;

        public Pawn Owner => owner;

        public bool Launching => launchTicks >= 0;

        public void Configure(Pawn newOwner, float newLean)
        {
            owner = newOwner;
            lean = newLean;
            expireTick = Find.TickManager.TicksGame + PanoplyDefaults.BladeLifetimeTicks;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (expireTick <= 0) expireTick = Find.TickManager.TicksGame + PanoplyDefaults.BladeLifetimeTicks;
            PanoplyRegistry.Register(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            PanoplyRegistry.Deregister(this);
            base.DeSpawn(mode);
        }

        /// <summary>
        /// Order this blade at something. The wait is kept on the blade rather than run from the
        /// ability, which is what makes a volley possible at all: each blade counts its own few
        /// ticks down, so they leave in sequence and nothing has to hold a timer for the group.
        /// </summary>
        public void OrderLaunch(LocalTargetInfo target, int delayTicks)
        {
            launchTarget = target;
            launchTicks = delayTicks;
        }

        protected override void Tick()
        {
            base.Tick();

            if (launchTicks >= 0)
            {
                launchTicks--;
                if (launchTicks < 0) Loose();
                return;
            }

            if (Find.TickManager.TicksGame >= expireTick) Withdraw();
        }

        /// <summary>
        /// Off the ground and away. The projectile carries the carrier as its launcher so a kill
        /// is credited to them, and the blade itself is spent - what lands at the far end is a
        /// new blade planted by the shot, not this one arriving.
        /// </summary>
        private void Loose()
        {
            if (!Spawned || !launchTarget.IsValid)
            {
                launchTicks = -1;
                return;
            }

            Map map = Map;
            IntVec3 from = Position;

            // The order was given up to a second ago and the thing it named may be dead,
            // despawned or carried off since. The cell it was standing in is still there, so
            // the blade goes to the place rather than not going at all.
            LocalTargetInfo target = launchTarget;
            if (target.HasThing && (target.Thing == null || target.Thing.Destroyed || !target.Thing.Spawned))
            {
                target = new LocalTargetInfo(target.Cell);
            }
            if (!target.IsValid)
            {
                launchTicks = -1;
                return;
            }

            Thing spawned = GenSpawn.Spawn(PanoplyDefOf.AG_PanoplyBladeShot, from, map);
            Projectile_Blade shot = spawned as Projectile_Blade;
            if (shot == null)
            {
                if (spawned != null && !spawned.Destroyed) spawned.Destroy();
                launchTicks = -1;
                return;
            }

            shot.SetOwner(owner);
            shot.Launch(owner, from.ToVector3Shifted(), target, target, ProjectileHitFlags.All,
                false, null, null);

            Destroy(DestroyMode.Vanish);
        }

        /// <summary>The blade's time is up and the frame it came out of gets it back.</summary>
        private void Withdraw()
        {
            PanoplyUtility.ImpactEffect(Position, Map);
            Destroy(DestroyMode.Vanish);
        }

        /// <summary>Taken out of the ground by hand rather than by time. Used by grasp.</summary>
        public void PullOut()
        {
            if (Spawned) Destroy(DestroyMode.Vanish);
        }

        /// <summary>
        /// Leaning where it landed, with a shiver on it so a field of them does not read as
        /// scenery. While a blade is winding up to launch it rises, turns to face what it was
        /// pointed at, and goes - all of which is this method and no animation system.
        /// </summary>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float heading = lean;
            float lift = 0f;
            float alpha = 1f;

            if (launchTicks >= 0)
            {
                // 0 at the moment of the order, 1 at the moment it leaves.
                float progress = 1f - (launchTicks / (float)PanoplyDefaults.LooseWindupTicks);
                progress = Mathf.Clamp01(progress);

                float aim = launchTarget.IsValid
                    ? (launchTarget.CenterVector3 - drawLoc).AngleFlat()
                    : heading;

                heading = Mathf.LerpAngle(lean, aim, progress);
                lift = progress * 0.45f;
            }
            else
            {
                heading += Mathf.Sin((Find.TickManager.TicksGame + thingIDNumber) * 0.06f) * 1.6f;

                // The last few seconds of a blade's life, fading as the body reels it in.
                int left = expireTick - Find.TickManager.TicksGame;
                if (left < 120) alpha = Mathf.Clamp01(left / 120f);
            }

            if (lift > 0f)
            {
                // Out of the ground and turning: drawn whole, because the point is no longer
                // in anything.
                Vector3 lifted = drawLoc;
                lifted.z += lift;
                lifted.y = AltitudeLayer.Skyfaller.AltitudeFor();

                PanoplyGraphics.DrawShadow(drawLoc, 0.5f, 0.35f);
                PanoplyGraphics.DrawBlade(lifted, heading, PanoplyDefaults.BladeDrawSize, alpha);
                return;
            }

            PanoplyGraphics.DrawPlantedBlade(drawLoc, heading, PanoplyDefaults.PlantedDrawSize, alpha);
        }

        public override string GetInspectString()
        {
            if (owner == null) return base.GetInspectString();

            int left = Mathf.Max(0, expireTick - Find.TickManager.TicksGame);
            string basic = base.GetInspectString();
            string mine = "AG_PanoplyBladeOwner".Translate(owner.LabelShortCap,
                left.ToStringSecondsFromTicks()).Resolve();

            return string.IsNullOrEmpty(basic) ? mine : basic + "\n" + mine;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
            Scribe_Values.Look(ref lean, "lean", 0f);
            Scribe_Values.Look(ref expireTick, "expireTick", 0);
            Scribe_Values.Look(ref launchTicks, "launchTicks", -1);
            Scribe_TargetInfo.Look(ref launchTarget, "launchTarget");
        }
    }
}
