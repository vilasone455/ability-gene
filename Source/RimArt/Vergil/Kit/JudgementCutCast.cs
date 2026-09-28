using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.JudgementCutTiming;

namespace RimArt
{
    /// <summary>What every Vergil ability comp shares: Yamato must be in hand, and nothing while a cast holds him.</summary>
    public abstract class CompAbilityEffect_Vergil : CompAbilityEffect
    {
        public override bool GizmoDisabled(out string reason)
        {
            Pawn pawn = parent.pawn;
            if (!VergilKit.Wields(pawn))
            {
                reason = "Needs Yamato in hand.";
                return true;
            }
            if (GameComponent_Vergil.Instance?.Holding(pawn, Find.TickManager.TicksGame) != null)
            {
                reason = "Vergil is in the middle of a technique.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }
    }

    public class CompProperties_JudgementCut : CompProperties_AbilityEffect
    {
        public float radius = 1.9f;
        public int hits = 5;
        public float damagePerHit = 7f;
        public DamageDef damageDef;
        public float armorPenetration = 0.5f;
        /// <summary>How long the ball stays open; the hits are spread evenly over it.</summary>
        public float burstSeconds = 0.5f;
        /// <summary>How many cuts the picture draws across the ball.</summary>
        public int cuts = 24;
        public float styleGainPerPawn = 4f;

        public CompProperties_JudgementCut()
        {
            compClass = typeof(CompAbilityEffect_JudgementCut);
        }
    }

    /// <summary>Judgement Cut. On the fire tick (the draw) the ball opens on the target; the hits follow on the cast's own clock.</summary>
    public class CompAbilityEffect_JudgementCut : CompAbilityEffect_Vergil
    {
        public new CompProperties_JudgementCut Props => (CompProperties_JudgementCut)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            JudgementCutCast cast = VergilCasts.For<JudgementCutCast>(parent, target);
            if (cast == null) return;
            cast.target = target.Cell;
            cast.MarkFired(Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One Judgement Cut (the sketch vergil-judgement-cut.js). The warmup is the sheathed stance: the right
    /// hand goes to the hilt and a ring grows on the target. The fire tick is the draw: the ball opens there
    /// and <see cref="CompProperties_JudgementCut.hits"/> hits land on every pawn inside it, allies too,
    /// evenly spread over the burst; each pawn inside when a hit lands takes it. Vergil himself is never cut.
    /// </summary>
    public sealed class JudgementCutCast : VergilCast
    {
        public IntVec3 target;
        private List<Pawn> struck = new List<Pawn>();
        private readonly List<(Pawn pawn, float at, float deg)> pictureHits = new List<(Pawn, float, float)>();

        private CompProperties_JudgementCut Props => VergilKit.Props<CompProperties_JudgementCut>(VergilDefOf.AG_VergilJudgementCut) ?? new CompProperties_JudgementCut();
        private float Warm => VergilDefOf.AG_VergilJudgementCut.verbProperties.warmupTime;
        public override AbilityDef Def => VergilDefOf.AG_VergilJudgementCut;
        protected override float Lead => T.Lead;
        protected override float FireAt => T.OpenAt(Warm);

        /// <summary>Hit <paramref name="h"/> lands this many ticks after the fire: the middle of its share of the burst.</summary>
        private int HitTick(int h, CompProperties_JudgementCut props) =>
            Mathf.RoundToInt((h + 0.5f) * props.burstSeconds / Mathf.Max(1, props.hits) * 60f);

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            CompProperties_JudgementCut props = Props;
            for (int h = 0; h < props.hits; h++)
                if (now == fireTick + HitTick(h, props)) Strike(h, props);
            return Seconds(now) < T.Duration(Warm, props.burstSeconds);
        }

        private static readonly List<Pawn> inside = new List<Pawn>();

        private void Strike(int h, CompProperties_JudgementCut props)
        {
            if (home == null || !target.InBounds(home)) return;
            inside.Clear();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(target, home, props.radius, true))
                if (thing is Pawn pawn && pawn != caster && !pawn.Dead && pawn.Spawned) inside.Add(pawn);
            float at = T.OpenAt(Warm) + (h + 0.5f) * props.burstSeconds / Mathf.Max(1, props.hits);
            for (int i = 0; i < inside.Count; i++)
            {
                Pawn pawn = inside[i];
                float angle = (pawn.Position - target).AngleFlat;
                VergilKit.Cut(pawn, caster, props.damageDef, props.damagePerHit, props.armorPenetration, angle);
                pictureHits.Add((pawn, at, (h * 67 + i * 41) % 180));
                if (!struck.Contains(pawn))
                {
                    struck.Add(pawn);
                    VergilStyle.Hit(caster, pawn, props.styleGainPerPawn);
                }
            }
            inside.Clear();
        }

        public override void Pose(float s)
        {
            if (caster == null || !caster.Spawned) return;
            CompProperties_JudgementCut props = Props;
            float w = Mathf.Clamp01((s - T.CastAt) / Warm), sinceClose = s - T.CloseAt(Warm, props.burstSeconds);
            float hand = VfxMath.Smooth(w * 3f) * (1f - VfxMath.Smooth((sinceClose - 0.1f) / 0.3f));
            if (hand > 0f) VergilLooks.For(caster).hand = hand;
        }

        public override void Draw(float s)
        {
            if (home == null || caster == null) return;
            CompProperties_JudgementCut props = Props;
            Vector2 from = caster.Spawned && caster.Map == home ? VergilKit.Ground(caster.DrawPos) : VergilKit.Ground(caster.PositionHeld);
            Vector2 to = VergilKit.Ground(target), toward = to - from;
            float aim = toward.sqrMagnitude < 1e-4f ? 0f : Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
            Vector2 hilt = caster.Spawned ? YamatoDraw.Guard(caster) : from + new Vector2(0f, 0.3f);
            JudgementCutGraphics.Draw(from, hilt, aim, toward.magnitude, props.radius, props.cuts, Warm, props.burstSeconds, s, home);
            for (int i = 0; i < pictureHits.Count; i++)
            {
                (Pawn pawn, float at, float deg) = pictureHits[i];
                if (pawn.Spawned && pawn.Map == home) VergilGraphics.HitCut(VergilKit.Ground(pawn.DrawPos), deg, s - at);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref target, "target");
            Scribe_Collections.Look(ref struck, "struck", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (struck == null) struck = new List<Pawn>();
                struck.RemoveAll(p => p == null);
            }
        }
    }
}
