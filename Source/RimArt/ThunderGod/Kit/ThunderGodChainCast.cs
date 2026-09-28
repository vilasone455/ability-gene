using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.ThunderGodChainTiming;

namespace RimArt
{
    public class CompProperties_ThunderGodChain : CompProperties_AbilityEffect
    {
        /// <summary>How far a marked enemy may be from Minato to join the chain.</summary>
        public float range = 29.9f;
        public int maxTargets = 5;
        public int minTargets = 2;
        /// <summary>Share of Minato's melee damage in each cut.</summary>
        public float damageFactor = 1f;

        public CompProperties_ThunderGodChain()
        {
            compClass = typeof(CompAbilityEffect_ThunderGodChain);
        }
    }

    /// <summary>Flying Thunder God: Chain. No target: it takes the marked enemies in range (<see cref="ThunderGodMarks.ChainRoute"/>).</summary>
    public class CompAbilityEffect_ThunderGodChain : CompAbilityEffect_Minato
    {
        public new CompProperties_ThunderGodChain Props => (CompProperties_ThunderGodChain)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason)) return true;
            int marked = ThunderGodMarks.ChainRoute(parent.pawn, Props.range, Props.maxTargets).Count;
            if (marked >= Props.minTargets) return false;
            reason = "Needs " + Props.minTargets + " marked enemies within " + Props.range.ToString("0.#") + " tiles (" + marked + " now).";
            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            ThunderGodChainCast cast = MinatoCasts.For<ThunderGodChainCast>(parent, target);
            cast?.Launch(ThunderGodMarks.ChainRoute(parent.pawn, Props.range, Props.maxTargets), Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One chain (the sketch kunai-flying-thunder-god-chain.js, staying at the last target). On the click every
    /// mark's strip is written; then one jump every 0.24 s, nearest first: at each he lands in the free cell most
    /// behind the pawn, seen from where he came from, and cuts once at x1.0, a sure hit. A pawn that died, went
    /// down or left before its turn is skipped, and so is one with no free cell round it. He stays at the last.
    /// </summary>
    public sealed class ThunderGodChainCast : MinatoCast
    {
        private List<Pawn> targets = new List<Pawn>();
        /// <summary>For each target: where he left from and landed (planned on the click, set on arrival), and whether he got there.</summary>
        private List<IntVec3> froms = new List<IntVec3>(), landings = new List<IntVec3>();
        private List<Vector2> seen = new List<Vector2>();
        private List<bool> reached = new List<bool>();
        private int arrivedCount, struckCount;
        private IntVec3 start;
        private bool aborted;

        private readonly ChainHop[] hops = new ChainHop[T.MostTargets + 1];

        public override AbilityDef Def => MinatoDefOf.AG_ThunderGodChain;
        protected override float Lead => T.Lead;
        protected override float FireAt => T.CastAt;

        public IReadOnlyList<Pawn> Targets => targets;
        /// <summary>Whether he got to target <paramref name="k"/> (tests).</summary>
        public bool Reached(int k) => k >= 0 && k < reached.Count && reached[k];
        private float DamageFactor => MinatoKit.Props<CompProperties_ThunderGodChain>(Def)?.damageFactor ?? 1f;

        public void Launch(List<Pawn> route, int now)
        {
            MarkFired(now);
            start = caster.Position;
            targets = route ?? new List<Pawn>();
            froms.Clear();
            landings.Clear();
            seen.Clear();
            reached.Clear();
            IntVec3 at = start;
            for (int i = 0; i < targets.Count; i++)
            {
                IntVec3 land = ThunderGodMarks.Behind(caster, at, targets[i]);
                froms.Add(at);
                landings.Add(land);
                seen.Add(MinatoKit.Ground(targets[i].DrawPos));
                reached.Add(false);
                if (land.IsValid) at = land;
            }
        }

        private int Count => targets.Count;
        public override bool Holds(int now) => Fired && !aborted && struckCount < Count;

        public override Rot4 Facing(int now)
        {
            int k = Mathf.Min(arrivedCount, Count - 1);
            if (k < 0 || targets[k] == null) return Rot4.Invalid;
            Vector2 look = MinatoKit.Ground(targets[k].Position) - MinatoKit.Ground(caster.Position);
            return look.sqrMagnitude < 1e-4f ? Rot4.Invalid : Rot4.FromAngleFlat(90f - ThunderGodTiming.Degrees(look));
        }

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            while (arrivedCount < Count && now >= TickAt(T.ArriveAt(arrivedCount))) Arrive(arrivedCount++);
            while (struckCount < Count && now >= TickAt(T.HitAt(struckCount))) Strike(struckCount++);
            return Seconds(now) < T.Duration(Count, false);
        }

        private void Arrive(int k)
        {
            Pawn target = targets[k];
            IntVec3 land = IntVec3.Invalid;
            if (!aborted && CasterFit && target != null && target.Spawned && target.Map == home && !target.Dead && !target.Downed)
                land = ThunderGodMarks.Behind(caster, caster.Position, target);
            if (land.IsValid)
            {
                froms[k] = caster.Position;
                landings[k] = land;
                seen[k] = MinatoKit.Ground(target.DrawPos);
                Teleport(land);
                reached[k] = caster.Position == land;
            }
            // Skipped: its strip goes, and nothing is drawn for it.
            else landings[k] = IntVec3.Invalid;
            // The next jump starts from wherever he is now.
            if (k + 1 < Count && caster != null && caster.Spawned) froms[k + 1] = caster.Position;
        }

        private void Strike(int k)
        {
            Pawn target = targets[k];
            if (!reached[k] || aborted || !CasterFit || target == null || !target.Spawned || target.Dead) return;
            if (!caster.Position.AdjacentTo8WayOrInside(target.Position)) return;
            ThunderGodStrike.Hit(caster, target, DamageFactor);
            Find.CameraDriver.shaker.DoShake(T.Shake);
        }

        public override void JobEnded(int now)
        {
            aborted = true;
        }

        public override void Pose(float s)
        {
            if (!Fired || aborted || caster == null || !caster.Spawned || caster.Map != home || Count == 0) return;
            int stop = 0;
            while (stop < Count && s >= T.ArriveAt(stop)) stop++;
            float thin = Mathf.Max(stop > 0 ? 1f - VfxMath.Smooth((s - T.ArriveAt(stop - 1)) / T.Squeeze) : 0f,
                stop < Count ? VfxMath.Smooth((s - T.GoAt(stop)) / T.Squeeze) : 0f);
            MinatoLooks.Thin(caster, thin);
        }

        public override void Draw(float s)
        {
            if (!Fired || home == null || Count == 0) return;
            Vector2 origin = MinatoKit.Ground(start);
            int count = Mathf.Min(Count, hops.Length);
            for (int k = 0; k < count; k++)
            {
                Pawn target = targets[k];
                Vector2 at = target != null && target.Spawned && target.Map == home && s < T.HitAt(k) + 0.2f ? MinatoKit.Ground(target.DrawPos) : seen[k];
                Vector2 from = MinatoKit.Ground(froms[k]), step = at - from;
                // A jump that did not happen keeps its place, so the ones after it keep their times.
                bool skipped = !landings[k].IsValid || !froms[k].IsValid || aborted && k >= arrivedCount;
                hops[k] = new ChainHop
                {
                    from = from, to = skipped ? from : MinatoKit.Ground(landings[k]), target = at,
                    along = step.sqrMagnitude > 1e-4f ? step.normalized : Vector2.right, hasTarget = true, skipped = skipped,
                    degrees = ThunderGodTiming.Degrees(step), thrown = ThunderGodTiming.Degrees(at - origin),
                };
            }
            ThunderGodChainGraphics.Draw(hops, count, T.Duration(Count, false), s, home);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
            Scribe_Collections.Look(ref froms, "froms", LookMode.Value);
            Scribe_Collections.Look(ref landings, "landings", LookMode.Value);
            Scribe_Collections.Look(ref seen, "seen", LookMode.Value);
            Scribe_Collections.Look(ref reached, "reached", LookMode.Value);
            Scribe_Values.Look(ref arrivedCount, "arrivedCount");
            Scribe_Values.Look(ref struckCount, "struckCount");
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref aborted, "aborted");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                targets ??= new List<Pawn>();
                froms ??= new List<IntVec3>();
                landings ??= new List<IntVec3>();
                seen ??= new List<Vector2>();
                reached ??= new List<bool>();
                // A lost reference leaves a null in its place, so the lists stay in step; pad anything short.
                while (froms.Count < targets.Count) froms.Add(IntVec3.Invalid);
                while (landings.Count < targets.Count) landings.Add(IntVec3.Invalid);
                while (seen.Count < targets.Count) seen.Add(Vector2.zero);
                while (reached.Count < targets.Count) reached.Add(false);
            }
        }
    }
}
