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
    /// One Chibaku Tensei, from the cupped hands to the ball being formed (the sketch pain-chibaku-tensei-v2.js). The
    /// warmup is Pain cupping his hands at his chest while the core forms between them in a white glow, and in its last
    /// <see cref="ThrowTime"/> the near hand throwing it straight up; the fire tick is the launch: the core leaves the
    /// raised hand at <c>coreSpeed</c>, climbs over its place and comes down 5 cells over the cell
    /// (<see cref="ChibakuBall.CorePoint"/>, <see cref="ChibakuBall.DefaultHeight"/>), and on arrival the map's
    /// <see cref="MapComponent_ChibakuPlates"/> takes over (the pull, the ball, the burst). From just after the launch
    /// Pain holds his palms pressed together at his chest, trembling harder through the pull (<see cref="Hands"/>); the
    /// cast job holds him until the ball is formed and his hands are down again; the ball goes on without him standing.
    /// If Pain is downed, killed, leaves the map or hero form while the core flies, the core fades and nothing happens
    /// (the charge and cooldown are spent); after it arrives, the map component bursts the ball instead.
    /// </summary>
    public sealed class ChibakuCast : PainCast
    {
        public IntVec3 cell = IntVec3.Invalid;
        /// <summary>The unit way from Pain to the cell (x, z), fixed at the launch.</summary>
        public Vector2 aim = Vector2.up;
        /// <summary>The core's start over the raised hand and its place over the cell: (x, height, z) in cells.</summary>
        public Vector3 from, to;
        /// <summary>Cells the core flies (along its curve).</summary>
        public float run;
        public bool handed, aborted;
        public int abortTick = -1;

        // Sketch timing and shape (pain-chibaku-tensei-v2.js): rest before the warmup, the core's radius between the hands,
        // when the hands come down after the ball is formed and how long it takes; the hands cupping, the throw at the end
        // of the warmup, the seal after the launch; the raised hand's height.
        public const float LeadTime = .2f, PalmR = .08f, ArmDownAfter = .3f, ArmDownTime = .4f, FadeSeconds = .3f;
        public const float CupIn = .3f, ThrowTime = .18f, SealAfter = .1f, SealIn = .35f, ThrowH = 1.05f;
        // Pain's hands, (cells along the aim, across it to the left, up) from his ground point: the near hand first.
        private static readonly Vector3 Rest0 = new Vector3(.04f, .2f, .3f), Rest1 = new Vector3(.04f, -.2f, .3f), Cup0 = new Vector3(.24f, .06f, .5f),
            Cup1 = new Vector3(.24f, -.06f, .5f), Raised = new Vector3(.1f, .14f, ThrowH), Seal0 = new Vector3(.2f, .028f, .56f), Seal1 = new Vector3(.2f, -.028f, .56f);

        /// <summary>One of Pain's hands: where (along the aim, across it, up), whether the fingers point up, how far they are closed.</summary>
        public struct PainHand
        {
            public Vector3 at;
            public bool up;
            public float grip;
        }

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

        /// <summary>Seconds on this cast's clock when the ball starts pulling.</summary>
        public float PullAt => Arrive + ChibakuBall.Pull;

        /// <summary>
        /// Pain's two hands at <paramref name="s"/> seconds on the cast's clock (the sketch's hands()): cupping at the chest
        /// over <see cref="CupIn"/>, the near hand throwing the core straight up over the last <see cref="ThrowTime"/> of
        /// the warmup, both palms pressed together at the chest from <see cref="SealAfter"/> after the launch, trembling
        /// harder through the pull, and down to his sides once the ball has formed. False when neither is drawn.
        /// </summary>
        public bool Hands(float s, out PainHand near, out PainHand far)
        {
            near = far = default;
            if (s < LeadTime || (Fired && s >= Free)) return false;
            float cup = Smooth((s - LeadTime) / CupIn), throwAt = LaunchAt - ThrowTime;
            Vector3 h0 = Vector3.Lerp(Rest0, Cup0, cup), h1 = Vector3.Lerp(Rest1, Cup1, cup);
            bool up0 = false, up1 = false;
            float g0 = .55f, g1 = .55f;
            if (s >= throwAt)
            {
                float up = Mathf.Pow(Mathf.Clamp01((s - throwAt) / ThrowTime), 2f);
                h0 = Vector3.Lerp(Cup0, Raised, up);
                g0 = Mathf.Lerp(.55f, .05f, up);
                up0 = up > .5f;
            }
            if (Fired && s >= LaunchAt + SealAfter)
            {
                float k = Smooth((s - LaunchAt - SealAfter) / SealIn);
                h0 = Vector3.Lerp(Raised, Seal0, k);
                h1 = Vector3.Lerp(Cup1, Seal1, k);
                if (k > .5f)
                {
                    up0 = up1 = true;
                    g0 = g1 = .9f;
                }
                float A = (s < PullAt ? .003f : Mathf.Lerp(.004f, .02f, Mathf.Clamp01((s - PullAt) / ChibakuBall.PullSeconds))) * k;
                var jig = new Vector3(Mathf.Sin(s * 47f) * A, Mathf.Sin(s * 59f + 1f) * A * .6f, Mathf.Sin(s * 53f + 2f) * A);
                h0 += jig;
                h1 += jig;
            }
            if (Fired && s >= Formed + ArmDownAfter)
            {
                float k = Smooth((s - Formed - ArmDownAfter) / ArmDownTime);
                h0 = Vector3.Lerp(h0, Rest0, k);
                h1 = Vector3.Lerp(h1, Rest1, k);
                if (k > .5f)
                {
                    up0 = up1 = false;
                    g0 = g1 = .5f;
                }
            }
            near = new PainHand { at = h0, up = up0, grip = g0 };
            far = new PainHand { at = h1, up = up1, grip = g1 };
            return true;
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

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
            // The core leaves just over the raised near hand.
            Vector2 hand = PainGraphics.Place(PainKit.Ground(caster.DrawPos), aim, Raised.x + .02f, Raised.y);
            from = new Vector3(hand.x, ThrowH + .12f, hand.y);
            Vector3 middle = target.ToVector3Shifted();
            to = new Vector3(middle.x, ChibakuBall.DefaultHeight, middle.z);
            run = ChibakuBall.CorePathLength(from, to);
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
