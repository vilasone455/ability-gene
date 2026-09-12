using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The projection: a fake soldier that stands still, cannot act, and is worth shooting.
    ///
    /// A Thing rather than a Pawn, and that is the load-bearing decision in this feature. A
    /// humanlike pawn of the player's faction is a colonist by definition - Pawn.IsColonist tests
    /// nothing more than that - which brings the colonist bar, needs, mood, a health tab, a
    /// social log, recruitment, a caravan slot and a "colonist died" letter, every one of which
    /// this item is specified not to do, and each of which would need its own patch to suppress.
    /// It would also leave a corpse, and the whole point of the ending is that nothing is left.
    ///
    /// <see cref="ToyCar"/> records the cost of the Thing route as "enemy AI ignores the car, so
    /// it draws no fire and is not a decoy". That is true of a plain Thing and not true of one
    /// implementing <see cref="IAttackTarget"/>: AttackTargetsCache.Notify_ThingSpawned registers
    /// anything spawned that implements it, so being a target costs four members and no plumbing.
    /// RimWorld.Hive is the precedent - a building, not a turret, that raiders shoot and swing at.
    ///
    /// What it costs instead is the drawing, which is <see cref="MimicRender"/>.
    /// </summary>
    public class MimicDecoy : Building, IAttackTarget
    {
        /// <summary>
        /// The pawn this is a copy of. Not decoration: the decoy has no appearance of its own and
        /// is drawn by running that pawn's renderer at this position, so losing the reference
        /// means losing the picture. The decoy ends rather than falling back to something else.
        /// </summary>
        private Pawn source;

        /// <summary>
        /// The way the copy faces, captured once when it is spawned rather than read from the
        /// source every frame. The source turns as it fights; a decoy that turned with it would
        /// be two people moving as one, which is the tell this is meant not to have.
        /// </summary>
        private Rot4 facing = Rot4.South;

        /// <summary>Absolute game tick, not a countdown, so save and load need no arithmetic.</summary>
        private int expireTick;

        public Pawn Source => source;
        public Rot4 Facing => facing;

        /// <summary>
        /// Called by the projectile immediately after spawning. Separate from SpawnSetup because
        /// the two things it needs - who threw it and which way to look - are known to the
        /// thrower and not to the map.
        /// </summary>
        public void Project(Pawn from, Rot4 lookAt)
        {
            source = from;
            facing = lookAt;
            expireTick = Find.TickManager.TicksGame + MimicDefaults.LifetimeTicks;
        }

        // ---- IAttackTarget -----------------------------------------------------------------
        //
        // Four members, and only one of them says anything. Written explicitly on the interface
        // where the name would otherwise collide with Thing's own.

        Thing IAttackTarget.Thing => this;

        /// <summary>
        /// Nothing. The projection cannot attack, and this is read by the scorer to give a target
        /// +10 for already aiming at the shooter - a bonus the decoy should never earn, because
        /// it never threatens anybody.
        /// </summary>
        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

        /// <summary>The taunt. See <see cref="MimicDefaults.PriorityFactor"/> for the derivation.</summary>
        public float TargetPriorityFactor => MimicDefaults.PriorityFactor;

        /// <summary>
        /// Only despawning disables it. The scan flags raiders use - NeedThreat and
        /// NeedAutoTargetable - both route through here, and a decoy that reported itself
        /// harmless would be skipped by every hostile on the map, which is the one failure this
        /// class cannot have.
        /// </summary>
        public bool ThreatDisabled(IAttackTargetSearcher disabledFor) => !Spawned;

        // ---- Lifecycle ---------------------------------------------------------------------

        protected override void Tick()
        {
            base.Tick();

            if (!SourceStillProjecting())
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            if (Find.TickManager.TicksGame >= expireTick) Destroy(DestroyMode.Vanish);
        }

        /// <summary>
        /// Whether there is still a live signature to copy.
        ///
        /// The decoy is drawn by running the source's renderer, so this is first a rendering
        /// requirement and only afterwards a rule: a source that is dead, downed, carried or off
        /// the map either cannot be drawn or is drawn lying down, and a decoy lying down is worse
        /// than no decoy. Ending here rather than papering over it also disposes of every posture
        /// edge case at once, and the fiction carries it - the projector is copying somebody who
        /// is still standing there.
        /// </summary>
        private bool SourceStillProjecting()
        {
            if (source == null || source.Dead || !source.Spawned) return false;
            if (source.Map != Map) return false;
            if (source.Downed || source.GetPosture() != PawnPosture.Standing) return false;
            if (source.CarriedBy != null) return false;
            return true;
        }

        /// <summary>
        /// One exit for all three endings - the timer, the enemy, and the source going down.
        ///
        /// Destroyed rather than killed, and a Thing rather than a Pawn, so there is no corpse to
        /// suppress, no gear to strip and no death letter to cancel. The def carries no costList
        /// and leaves no resources, so KillFinalize drops nothing either.
        /// </summary>
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = Map;
            Vector3 where = DrawPos;
            bool wasSpawned = Spawned;

            base.Destroy(mode);

            if (wasSpawned && map != null) MimicRender.Collapse(where, map);
        }

        /// <summary>
        /// Deliberately nothing.
        ///
        /// The def carries the emitter's graphic so the thing has an inventory icon and a shape
        /// the selection bracket can find, but the emitter is the one object on this cell that
        /// must never be visible: a soldier standing over a machine is not a soldier. The copy
        /// itself is drawn a frame-phase later by <see cref="MapComponent_Mimics"/>, which is the
        /// only place the renderer trick is known to work.
        /// </summary>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false) { }

        public override string GetInspectString()
        {
            string basic = base.GetInspectString();
            if (source == null) return basic;

            int left = Mathf.Max(0, expireTick - Find.TickManager.TicksGame);
            string mine = "AG_MimicProjecting".Translate(
                source.LabelShortCap, left.ToStringSecondsFromTicks()).Resolve();

            return string.IsNullOrEmpty(basic) ? mine : basic + "\n" + mine;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref source, "source");
            Scribe_Values.Look(ref facing, "facing", Rot4.South);
            Scribe_Values.Look(ref expireTick, "expireTick", 0);
        }
    }
}
