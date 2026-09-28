using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_ChibakuTensei : CompProperties_AbilityEffect
    {
        /// <summary>Cells round the target cell whose ground is torn up and pulled in.</summary>
        public float radius = 6f;
        /// <summary>Cells per second the core flies from Pain's palm to its place over the cell.</summary>
        public float coreSpeed = 14f;
        /// <summary>Seconds the formed ball holds before it bursts.</summary>
        public float holdSeconds = 12f;
        /// <summary>Blunt per second held, blunt for the fall, and the largest single hit they come in.</summary>
        public float crushPerSecond = 2f, fallDamage = 8f, hitSize = 8f;
        public float stunSeconds = 2.5f;
        /// <summary>Real rock chunks landing at the burst: one for every this many plates pulled, clamped to min..max.</summary>
        public int platesPerChunk = 16, minChunks = 6, maxChunks = 10;

        public CompProperties_ChibakuTensei()
        {
            compClass = typeof(CompAbilityEffect_ChibakuTensei);
        }
    }

    /// <summary>
    /// Chibaku Tensei (the sketch pain-chibaku-tensei.js): a cell up to the verb's range in sight. One ball per map; not
    /// while Shinra Tensei charges or another of Pain's casts holds him. The flight is <see cref="ChibakuCast"/>, the
    /// ball <see cref="MapComponent_ChibakuPlates"/>.
    /// </summary>
    public class CompAbilityEffect_ChibakuTensei : CompAbilityEffect_Pain
    {
        public new CompProperties_ChibakuTensei Props => (CompProperties_ChibakuTensei)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (GameComponent_Shinra.Instance?.For(parent.pawn).active == true)
            {
                reason = "Shinra Tensei is charging.";
                return true;
            }
            if (BallUp(parent.pawn.Map))
            {
                reason = "A Chibaku Tensei ball is still up on this map.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        private static bool BallUp(Map map) => MapComponent_ChibakuPlates.Of(map)?.Live == true;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Map map = parent.pawn.Map;
            string problem = null;
            if (!target.Cell.IsValid || map == null || !target.Cell.InBounds(map)) problem = "Chibaku Tensei needs a cell on the map.";
            else if (target.Cell.Fogged(map)) problem = "Chibaku Tensei cannot target an unseen cell.";
            else if (BallUp(map)) problem = "A Chibaku Tensei ball is still up on this map.";
            if (problem == null) return base.Valid(target, throwMessages);
            Reject(problem, parent.pawn, throwMessages);
            return false;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (target.IsValid) GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!target.Cell.IsValid || parent.pawn.Map == null || BallUp(parent.pawn.Map)) return;
            PainCasts.For<ChibakuCast>(parent, target)?.Launch(target.Cell, Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One Chibaku Tensei, from the raised hand to the ball being formed. The warmup is the hand coming up at the cell
    /// and the core growing over the palm; the fire tick is the launch: the core flies at <c>coreSpeed</c> from the palm
    /// to 5 cells over the cell (<see cref="ChibakuBall.DefaultHeight"/>), and on arrival the map's
    /// <see cref="MapComponent_ChibakuPlates"/> takes over (the pull, the ball, the burst). The cast job holds Pain with
    /// his hand up until the ball is formed and the hand is down again; the ball itself goes on without him standing.
    /// If Pain is downed, killed, leaves the map or hero form while the core flies, the core fades and nothing happens
    /// (the charge and cooldown are spent); after it arrives, the map component bursts the ball instead.
    /// </summary>
    public sealed class ChibakuCast : PainCast
    {
        public IntVec3 cell = IntVec3.Invalid;
        /// <summary>The unit way from Pain to the cell (x, z), fixed at the launch.</summary>
        public Vector2 aim = Vector2.up;
        /// <summary>The core's start over the palm and its place over the cell: (x, height, z) in cells.</summary>
        public Vector3 from, to;
        /// <summary>Cells the core flies.</summary>
        public float run;
        public bool handed, aborted;
        public int abortTick = -1;

        // Sketch timing and shape (pain-chibaku-tensei.js): rest before the warmup, the raised hand's height, the palm
        // core's radius and its gap past the hand, when the hand comes down after the ball is formed and how long it takes.
        public const float LeadTime = .2f, RaisedH = .72f, PalmR = .2f, PalmGap = .3f, ArmDownAfter = .3f, ArmDownTime = .4f, FadeSeconds = .3f;

        public override AbilityDef Def => PainDefOf.AG_PainChibakuTensei;
        protected override float Lead => LeadTime;
        protected override float FireAt => LeadTime + Warmup;

        public static float Warmup => PainDefOf.AG_PainChibakuTensei.verbProperties.warmupTime;
        private static CompProperties_ChibakuTensei P => PainKit.ChibakuProps;

        // ---- the sketch's times -------------------------------------------------------------------------------
        public float LaunchAt => FireAt;
        public float Arrive => LaunchAt + run / Mathf.Max(.1f, P.coreSpeed);
        public float Formed => Arrive + ChibakuBall.Formed;
        /// <summary>The hand is down again: the cast job lets Pain go.</summary>
        public float Free => Formed + ArmDownAfter + ArmDownTime;

        /// <summary>The palm core's point on the floor (its height is <see cref="RaisedH"/> + 0.12), from Pain's ground point.</summary>
        public static Vector2 PalmGround(Vector2 pain, Vector2 aim) => PainGraphics.Place(pain, aim, .12f + PainGraphics.Reach + PalmGap, -.1f);

        /// <summary>The unit way from <paramref name="pawn"/> to <paramref name="target"/>, level.</summary>
        public static Vector2 AimFrom(Pawn pawn, IntVec3 target)
        {
            Vector3 d = target.ToVector3Shifted() - pawn.Position.ToVector3Shifted();
            var flat = new Vector2(d.x, d.z);
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector2.up;
        }

        public void Launch(IntVec3 target, int now)
        {
            MarkFired(now);
            cell = target;
            aim = AimFrom(caster, target);
            Vector2 palm = PalmGround(PainKit.Ground(caster.DrawPos), aim);
            from = new Vector3(palm.x, RaisedH + .12f, palm.y);
            Vector3 middle = target.ToVector3Shifted();
            to = new Vector3(middle.x, ChibakuBall.DefaultHeight, middle.z);
            run = (to - from).magnitude;
        }

        public override bool Holds(int now)
        {
            if (!Fired || aborted || Seconds(now) >= Free) return false;
            // After a load the ball is not restored (its takings are put down), so nothing is left to hold.
            return !handed || MapComponent_ChibakuPlates.Of(home)?.Caster == caster;
        }

        public override Rot4 Facing(int now) => Rot4.FromAngleFlat(Mathf.Atan2(aim.x, aim.y) * Mathf.Rad2Deg);

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            if (aborted) return now - abortTick < Mathf.CeilToInt(FadeSeconds * 60f);
            float s = Seconds(now);
            if (!handed)
            {
                if (!CasterFit || !PainKit.Has(caster, Def))
                {
                    aborted = true;
                    abortTick = now;
                    return true;
                }
                if (s >= Arrive) Hand();
            }
            return s < Free;
        }

        /// <summary>The core is over the cell: the map's ball takes over from here.</summary>
        private void Hand()
        {
            handed = true;
            MapComponent_ChibakuPlates.Of(home)?.BeginBall(cell, P.radius, null, ChibakuBall.DefaultHeight, caster);
            PainPictures.Shake(home, .4f);
        }

        public override void Discard()
        {
            aborted = true;
            handed = true;
            abortTick = -9999;
        }

        public override void Draw(float s) => PainPictures.Chibaku(this, s);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref aim, "aim", Vector2.up);
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref to, "to");
            Scribe_Values.Look(ref run, "run");
            Scribe_Values.Look(ref handed, "handed");
            Scribe_Values.Look(ref aborted, "aborted");
            Scribe_Values.Look(ref abortTick, "abortTick", -1);
        }
    }
}
