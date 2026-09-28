using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.GuidingThunderTiming;

namespace RimArt
{
    public class CompProperties_GuidingThunder : CompProperties_AbilityEffect
    {
        /// <summary>How long the barrier stands.</summary>
        public float seconds = 6f;
        /// <summary>Cells from Minato's feet where a shot is taken: the drawn ring as well.</summary>
        public float radius = 1.3f;
        /// <summary>The barrier ends early after this many shots.</summary>
        public int maxShots = 12;

        public CompProperties_GuidingThunder()
        {
            compClass = typeof(CompAbilityEffect_GuidingThunder);
        }
    }

    /// <summary>Guiding Thunder. The target is one of Minato's marks, as for the jump; the barrier is <see cref="GuidingThunderCast"/>.</summary>
    public class CompAbilityEffect_GuidingThunder : CompAbilityEffect_Minato
    {
        public new CompProperties_GuidingThunder Props => (CompProperties_GuidingThunder)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string problem = null;
            if (!ThunderGodMarks.TryResolve(target, parent.pawn.Map, out Pawn pawn, out _))
                problem = "Needs one of Minato's kunai: in a pawn, sealed by his touch, or on the ground.";
            else if (pawn == parent.pawn) problem = "The shots cannot come out at Minato himself.";
            if (problem == null) return base.Valid(target, throwMessages);
            Reject(problem, parent.pawn, throwMessages);
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!ThunderGodMarks.TryResolve(target, parent.pawn.Map, out Pawn pawn, out KunaiItem item)) return;
            MinatoCasts.For<GuidingThunderCast>(parent, target)?.Launch(pawn, item, Props, Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One Guiding Thunder (the sketch kunai-guiding-thunder.js). From the click, for 6 s, a ring of script stands
    /// round Minato and the job holds him: he cannot move or attack. Every projectile coming at him is taken when it
    /// reaches the ring and comes out at the exit kunai instead (<see cref="TryTake"/>):
    ///   a bullet or arrow aimed at him and on course to hit (its roll hit him; a miss flies on past),
    ///   an explosive shell or grenade coming down inside the ring.
    /// Kunai in a pawn: the shot hits that pawn with its own damage, whoever fired it. If that pawn has died, the
    /// shots land where it fell. Kunai on the ground: the shot lands on its cell and hits whoever stands there, or
    /// the floor; if the kunai is picked up the barrier ends. Explosives go off at the exit. Ends early after 12
    /// shots. Melee is not stopped. Combat Extended's projectiles are not vanilla projectiles and are not taken.
    /// </summary>
    public sealed class GuidingThunderCast : MinatoCast
    {
        /// <summary>Barriers standing now, for the projectile patch: nothing is looked up while this is empty.</summary>
        internal static readonly List<GuidingThunderCast> Live = new List<GuidingThunderCast>();

        public Pawn anchorPawn;
        public KunaiItem anchorItem;
        /// <summary>Where the exit last was: a dead pawn's shots land here.</summary>
        public IntVec3 anchorCell;
        public int taken, endTick = -1;
        public bool ended;
        private float radius = 1.3f;
        private int maxShots = 12;
        /// <summary>The sketch clock when the barrier began to burn away.</summary>
        private float overAt = -1f;

        private GuidedShot[] shots = new GuidedShot[T.MostShots];
        private int drawnShots;

        public override AbilityDef Def => MinatoDefOf.AG_GuidingThunder;
        protected override float Lead => T.Lead;
        protected override float FireAt => T.CastAt;

        public bool Standing => Fired && !ended;

        public void Launch(Pawn pawn, KunaiItem item, CompProperties_GuidingThunder props, int now)
        {
            MarkFired(now);
            anchorPawn = pawn;
            anchorItem = item;
            anchorCell = pawn != null ? pawn.Position : item.Position;
            radius = props.radius;
            maxShots = props.maxShots;
            endTick = now + Mathf.RoundToInt(props.seconds * 60f);
            overAt = T.CastAt + props.seconds;
            taken = drawnShots = 0;
            ended = false;
            if (!Live.Contains(this)) Live.Add(this);
        }

        public override bool Holds(int now) => Standing;

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            if (Standing)
            {
                if (!Live.Contains(this)) Live.Add(this);
                if (anchorPawn != null && anchorPawn.Spawned && anchorPawn.Map == home) anchorCell = anchorPawn.Position;
                bool kunaiGone = anchorPawn == null && (anchorItem == null || !anchorItem.Spawned || anchorItem.Map != home);
                if (now >= endTick || !CasterFit || kunaiGone) End(now);
            }
            return overAt < 0f || Seconds(now) < overAt + T.Fade + T.Tail;
        }

        private void End(int now)
        {
            if (ended) return;
            ended = true;
            overAt = Mathf.Min(overAt < 0f ? float.MaxValue : overAt, Seconds(now));
            Live.Remove(this);
        }

        public override void JobEnded(int now) => End(now);

        public override void Discard()
        {
            ended = true;
            Live.Remove(this);
        }

        private bool ExitInPawn => anchorPawn != null && anchorPawn.Spawned && !anchorPawn.Dead && anchorPawn.Map == home;

        private Vector3 ExitPos => ExitInPawn ? anchorPawn.DrawPos
            : anchorItem != null && anchorItem.Spawned ? anchorItem.Position.ToVector3Shifted() : anchorCell.ToVector3Shifted();

        // ---- taking a shot ----------------------------------------------------------------------------------------

        private static readonly AccessTools.FieldRef<Projectile, Vector3> Origin = AccessTools.FieldRefAccess<Projectile, Vector3>("origin");
        private static readonly AccessTools.FieldRef<Projectile, Vector3> Destination = AccessTools.FieldRefAccess<Projectile, Vector3>("destination");
        private static readonly AccessTools.FieldRef<Projectile, int> TicksToImpact = AccessTools.FieldRefAccess<Projectile, int>("ticksToImpact");
        private static readonly AccessTools.FieldRef<Projectile, bool> Landed = AccessTools.FieldRefAccess<Projectile, bool>("landed");
        private static readonly MethodInfo Impact = AccessTools.Method(typeof(Projectile), "Impact");

        /// <summary>
        /// Called for every vanilla projectile before it moves (<see cref="Patch_Projectile_GuidingThunder"/>). True
        /// when a barrier took it: it has come out at the kunai and hit there, and must not move this tick.
        /// </summary>
        internal static bool TryTake(Projectile shot, int delta)
        {
            if (shot == null || !shot.Spawned || Landed(shot)) return false;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                GuidingThunderCast barrier = Live[i];
                if (barrier.Standing && barrier.home == shot.Map && barrier.Takes(shot, delta))
                {
                    barrier.Take(shot);
                    return true;
                }
            }
            return false;
        }

        private bool Takes(Projectile shot, int delta)
        {
            if (!CasterFit || shot.Launcher == caster) return false;
            Vector3 feet = caster.DrawPos, at = shot.ExactPosition;
            ProjectileProperties props = shot.def.projectile;
            bool explosive = props.explosionRadius > 0f || props.flyOverhead;
            bool aimed = shot.usedTarget.Thing == caster
                || explosive && (Destination(shot) - feet).Yto0().magnitude <= radius;
            if (!aimed) return false;
            // At the ring, or it would get there (or land) before the next check.
            float reach = radius + props.SpeedTilesPerTick * delta;
            return (at - feet).Yto0().magnitude <= reach || TicksToImpact(shot) <= delta;
        }

        private void Take(Projectile shot)
        {
            Vector3 feet = caster.DrawPos, heading = (Destination(shot) - Origin(shot)).Yto0();
            Vector3 from = (shot.ExactPosition - feet).Yto0();
            if (from.sqrMagnitude < 1e-4f) from = -heading;
            if (from.sqrMagnitude < 1e-4f) from = Vector3.forward;
            from.Normalize();
            if (heading.sqrMagnitude < 1e-4f) heading = -from;
            heading.Normalize();

            // The picture: where it went into the ring, at chest height.
            float s = Seconds(Find.TickManager.TicksGame);
            Vector2 ground = MinatoKit.Ground(feet);
            if (drawnShots < shots.Length)
                shots[drawnShots++] = new GuidedShot
                {
                    entry = ground + new Vector2(from.x, from.z) * radius + new Vector2(0f, ThunderGodTiming.Chest),
                    degrees = ThunderGodTiming.Degrees(new Vector2(from.x, from.z)), reached = s, leftAt = s + T.ExitDelay,
                };

            // Out at the exit, still flying the way it came, and it hits there now.
            Vector3 exit = ExitPos;
            exit.y = shot.def.Altitude;
            Origin(shot) = exit - heading * 0.5f;
            Destination(shot) = exit;
            TicksToImpact(shot) = 0;
            IntVec3 cell = exit.ToIntVec3();
            if (cell.InBounds(home)) shot.Position = cell;
            Thing hit;
            if (ExitInPawn)
            {
                hit = anchorPawn;
                shot.usedTarget = anchorPawn;
                shot.intendedTarget = anchorPawn;
            }
            else
            {
                hit = null;
                foreach (Thing thing in cell.GetThingList(home))
                    if (thing is Pawn standing && standing != caster)
                    {
                        hit = standing;
                        break;
                    }
                shot.usedTarget = hit != null ? new LocalTargetInfo(hit) : new LocalTargetInfo(cell);
            }
            Impact.Invoke(shot, new object[] { hit, false });

            if (++taken >= maxShots) End(Find.TickManager.TicksGame);
        }

        // ---- picture ----------------------------------------------------------------------------------------------

        public override void Draw(float s)
        {
            if (!Fired || home == null || caster == null || !caster.Spawned || caster.Map != home) return;
            bool inPawn = ExitInPawn;
            Vector2 exit = inPawn ? MinatoKit.Ground(anchorPawn.DrawPos) : MinatoKit.Flat(anchorItem != null && anchorItem.Spawned ? anchorItem.Position : anchorCell);
            Vector2 feet = MinatoKit.Ground(caster.DrawPos), toward = exit - feet;
            float aim = toward.sqrMagnitude > 1e-4f ? ThunderGodTiming.Degrees(toward) : 0f;
            bool sealShown = inPawn || anchorItem != null && anchorItem.Spawned;
            GuidingThunderGraphics.Draw(feet, exit, aim, inPawn, sealShown, shots, drawnShots, radius,
                overAt < 0f ? float.MaxValue : overAt, s, home);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref anchorPawn, "anchorPawn");
            Scribe_References.Look(ref anchorItem, "anchorItem");
            Scribe_Values.Look(ref anchorCell, "anchorCell");
            Scribe_Values.Look(ref taken, "taken");
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref radius, "radius", 1.3f);
            Scribe_Values.Look(ref maxShots, "maxShots", 12);
            Scribe_Values.Look(ref overAt, "overAt", -1f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) shots = new GuidedShot[T.MostShots];
        }
    }

    /// <summary>
    /// Guiding Thunder takes a projectile before it moves, the hook the gravity well uses (GravityProjectiles.cs).
    /// Nothing happens while no barrier stands.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "TickInterval")]
    static class Patch_Projectile_GuidingThunder
    {
        static bool Prefix(Projectile __instance, int delta) =>
            GuidingThunderCast.Live.Count == 0 || !GuidingThunderCast.TryTake(__instance, delta);
    }
}
