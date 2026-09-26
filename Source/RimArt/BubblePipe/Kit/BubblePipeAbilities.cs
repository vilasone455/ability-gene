using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class BubblePipeDefOf
    {
        public static ThingDef AG_BubblePipe;
        public static AbilityDef AG_BubblePipe_DriftingBurst;
        public static AbilityDef AG_BubblePipe_EyePop;

        static BubblePipeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BubblePipeDefOf));
        }
    }

    /// <summary>What both pipe abilities share: the pipe in hand, hands and breath to blow with, and enough soap in the jar.</summary>
    public abstract class CompAbilityEffect_BubblePipe : CompAbilityEffect
    {
        protected CompBubblePipe Pipe => CompBubblePipe.HeldBy(parent.pawn);

        /// <summary>The blows one cast takes out of the jar.</summary>
        protected abstract int BlowsNeeded { get; }

        public override bool CanCast => base.CanCast && Unavailable() == null;

        private string Unavailable()
        {
            CompBubblePipe pipe = Pipe;
            Pawn pawn = parent.pawn;
            if (pipe == null) return "Requires a bubble pipe in hand.";
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return pawn.LabelShortCap + " cannot manipulate.";
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Breathing)) return pawn.LabelShortCap + " cannot blow.";
            if (pipe.Blows < BlowsNeeded)
                return "No soap: needs " + BlowsNeeded + " blows, the jar has " + pipe.Blows + ". Stand on or next to water to refill it.";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override string ExtraTooltipPart()
        {
            CompBubblePipe pipe = Pipe;
            return pipe == null ? null : "Soap: " + pipe.LabelRemaining + " blows";
        }

        protected static Vector2 Feet(Thing thing)
        {
            Vector3 stands = thing.DrawPos;
            return new Vector2(stands.x, stands.z);
        }
    }

    public class CompProperties_DriftingBurst : CompProperties_AbilityEffect
    {
        public int blows = 2;
        public int count = 6;
        /// <summary>The whole fan, in degrees, centred on the aim.</summary>
        public float spreadDegrees = 50f;
        /// <summary>Cells per second along the bubble's line of the fan.</summary>
        public float driftSpeed = 0.7f;
        /// <summary>Seconds from the first blow until the bubbles nobody touched pop on their own.</summary>
        public float lifeSeconds = 8f;
        /// <summary>A pawn whose middle comes this close to a bubble's middle pops it: the bubble's 0.28 radius plus 0.28 of body.</summary>
        public float touchRadius = 0.56f;
        public float blastRadius = 0.8f;
        public float damage = 5f;
        public float armorPenetration = 0f;
        public int staggerTicks = 60;
        /// <summary>Move speed while staggered, as a share of normal (Core's default stagger is 0.17).</summary>
        public float staggerMoveSpeedFactor = 0.17f;
        public SoundDef soundPop;

        public CompProperties_DriftingBurst()
        {
            compClass = typeof(CompAbilityEffect_DriftingBurst);
        }
    }

    /// <summary>
    /// Drifting Burst. The target tile sets only the direction. The bubbles and their pops are
    /// MapComponent_BubblePipe's.
    /// </summary>
    public class CompAbilityEffect_DriftingBurst : CompAbilityEffect_BubblePipe
    {
        private static readonly List<IntVec3> fan = new List<IntVec3>();

        public new CompProperties_DriftingBurst Props => (CompProperties_DriftingBurst)props;

        protected override int BlowsNeeded => Props.blows;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
            base.Valid(target, throwMessages) && target.Cell != parent.pawn.Position;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompBubblePipe pipe = Pipe;
            if (caster?.Map == null || pipe == null || pipe.Blows < Props.blows || !target.IsValid) return;
            float before = pipe.Soap;
            pipe.Spend(Props.blows);
            caster.Map.GetComponent<MapComponent_BubblePipe>().Blow(caster, target.Cell, before, pipe.Props.maxBlows, Props);
        }

        /// <summary>The fan the bubbles can drift through before they pop on their own.</summary>
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid || target.Cell == caster.Position) return;
            Vector2 toward = new Vector2(target.Cell.x - caster.Position.x, target.Cell.z - caster.Position.z).normalized;
            float reach = BubblePipeGraphics.TipAlong + Props.driftSpeed * Props.lifeSeconds, half = Props.spreadDegrees / 2f + 8f;
            fan.Clear();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(caster.Position, reach, false))
            {
                if (!cell.InBounds(caster.Map)) continue;
                Vector2 run = new Vector2(cell.x - caster.Position.x, cell.z - caster.Position.z);
                if (Vector2.Angle(run, toward) <= half) fan.Add(cell);
            }
            if (fan.Count > 0) GenDraw.DrawFieldEdges(fan);
        }
    }

    public class CompProperties_EyePop : CompProperties_AbilityEffect
    {
        public int blows = 1;
        /// <summary>Cells per second of the bubble's flight; the soap lands when the picture's bubble arrives.</summary>
        public float flightSpeed = 5f;
        public float debuffSeconds = 5f;
        public HediffDef hediff;
        public SoundDef soundHit;

        public CompProperties_EyePop()
        {
            compClass = typeof(CompAbilityEffect_EyePop);
        }
    }

    /// <summary>
    /// Eye Pop. The hit is decided at cast: the bubble always reaches the face (the flight is the
    /// picture and the delay). The flight and the soaping are MapComponent_BubblePipe's.
    /// </summary>
    public class CompAbilityEffect_EyePop : CompAbilityEffect_BubblePipe
    {
        public new CompProperties_EyePop Props => (CompProperties_EyePop)props;

        protected override int BlowsNeeded => Props.blows;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompBubblePipe pipe = Pipe;
            if (caster?.Map == null || pipe == null || pipe.Blows < Props.blows || !(target.Thing is Pawn victim) || !victim.Spawned) return;
            float before = pipe.Soap;
            pipe.Spend(Props.blows);
            caster.Map.GetComponent<MapComponent_BubblePipe>().Shoot(caster, victim, before, pipe.Props.maxBlows, Props);
        }
    }
}
