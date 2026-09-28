using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.JudgementCutEndTiming;

namespace RimArt
{
    public class CompProperties_JudgementCutEnd : CompProperties_AbilityEffect
    {
        public float radius = 10f;
        /// <summary>How long Vergil is gone, from the fire tick.</summary>
        public float goneSeconds = 1.5f;
        /// <summary>From coming back to the click.</summary>
        public float sheatheSeconds = 0.8f;
        /// <summary>How long he stays kneeling after the click.</summary>
        public float standUpSeconds = 0.7f;
        /// <summary>One cut per this much Style spent.</summary>
        public float stylePerCut = 5f;
        public float cutDamage = 10f;
        public DamageDef damageDef;
        public float armorPenetration = 0.5f;
        /// <summary>What each hostile or unowned building in range and in sight takes on the click, outside the cut pool.</summary>
        public float buildingDamage = 60f;
        /// <summary>The marked pawns stay stunned this long after the click.</summary>
        public float stunAfterClickSeconds = 0.5f;
        public HediffDef goneHediff;

        public CompProperties_JudgementCutEnd()
        {
            compClass = typeof(CompAbilityEffect_JudgementCutEnd);
        }
    }

    /// <summary>Judgement Cut End. Needs Style rank S; on the fire tick Vergil vanishes and the marks are set.</summary>
    public class CompAbilityEffect_JudgementCutEnd : CompAbilityEffect_Vergil
    {
        public new CompProperties_JudgementCutEnd Props => (CompProperties_JudgementCutEnd)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason)) return true;
            VergilStyleExtension rules = VergilStyle.Rules;
            float style = VergilStyle.Of(parent.pawn), needs = rules?.cutEndNeeds ?? 60f;
            if (style >= needs) return false;
            reason = "Needs Style rank " + (rules?.RankOf(needs) ?? "S") + " (" + needs.ToString("0") + "). Now " + style.ToString("0")
                + ", rank " + (rules?.RankOf(style) ?? "D") + ".";
            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            JudgementCutEndCast cast = VergilCasts.For<JudgementCutEndCast>(parent, target);
            cast?.Vanish(Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One Judgement Cut End (the sketch vergil-judgement-cut-end.js; the Style, shared cuts and space breaks
    /// agreed 2026-09-27). The warmup is the hand on the hilt. On the fire tick every hostile within the radius
    /// that Vergil can see is marked and stunned, all his Style is spent and becomes Style / stylePerCut cuts,
    /// and he is gone: not drawn, not targetable, and every projectile inside the radius is destroyed while he
    /// is away. He comes back on his own cell kneeling, facing the camera, and slides the blade into
    /// the upright scabbard. On the click the cuts are dealt one at a time round the marked pawns still
    /// standing, nearest first, and every hostile or unowned building inside the radius that he can see takes
    /// buildingDamage. He stays kneeling a moment, then the job lets him go.
    /// </summary>
    public sealed class JudgementCutEndCast : VergilCast
    {
        private List<Pawn> marked = new List<Pawn>();
        /// <summary>Where each marked pawn's feet were when he vanished, relative to his: the cuts pass through them.</summary>
        private List<Vector2> markedAt = new List<Vector2>();
        public int cuts;
        private bool back, clicked, aborted;
        private List<CutEndCut> layout;
        private readonly List<(Pawn pawn, float at, float deg)> hits = new List<(Pawn, float, float)>();

        private CompProperties_JudgementCutEnd Props => VergilKit.Props<CompProperties_JudgementCutEnd>(VergilDefOf.AG_VergilJudgementCutEnd) ?? new CompProperties_JudgementCutEnd();
        private CutEndTimes Times
        {
            get
            {
                CompProperties_JudgementCutEnd props = Props;
                return new CutEndTimes(VergilDefOf.AG_VergilJudgementCutEnd.verbProperties.warmupTime, props.goneSeconds, props.sheatheSeconds);
            }
        }
        public override AbilityDef Def => VergilDefOf.AG_VergilJudgementCutEnd;
        protected override float Lead => T.Lead;
        protected override float FireAt => Times.VanishAt;

        public IReadOnlyList<Pawn> Marked => marked;
        public bool Back => back;
        public bool Clicked => clicked;

        private List<CutEndCut> Layout => layout ?? (layout = T.Layout(Props.radius, Mathf.Max(1, cuts), markedAt));

        public void Vanish(int now)
        {
            MarkFired(now);
            CompProperties_JudgementCutEnd props = Props;
            Map map = caster.Map;
            float style = VergilStyle.SpendAll(caster);
            cuts = Mathf.Max(1, Mathf.FloorToInt(style / Mathf.Max(0.01f, props.stylePerCut)));
            marked.Clear();
            markedAt.Clear();
            layout = null;
            if (map == null) return;

            // Every hostile inside the radius that he can see, nearest first.
            var found = new List<(Pawn pawn, float d)>();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            float reach = props.radius * props.radius;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!VergilKit.Foe(caster, pawn) || pawn.Downed) continue;
                float d = (pawn.Position - caster.Position).LengthHorizontalSquared;
                if (d > reach || !GenSight.LineOfSight(caster.Position, pawn.Position, map, true)) continue;
                found.Add((pawn, d));
            }
            found.Sort((a, b) => a.d.CompareTo(b.d));
            Vector2 o = VergilKit.Ground(caster.Position);
            CutEndTimes times = Times;
            int stun = Mathf.RoundToInt((times.Gone + times.Sheathe + props.stunAfterClickSeconds) * 60f);
            foreach ((Pawn pawn, float _) in found)
            {
                marked.Add(pawn);
                markedAt.Add(VergilKit.Ground(pawn.Position) - o);
                pawn.stances?.stunner?.StunFor(stun, caster, false, true);
            }

            HediffDef gone = props.goneHediff ?? VergilDefOf.AG_VergilGone;
            if (gone != null && caster.health.hediffSet.GetFirstHediffOfDef(gone) == null) caster.health.AddHediff(gone);
            caster.stances?.CancelBusyStanceSoft();
        }

        public override bool Holds(int now) => Fired && !aborted && now < TickAt(Times.ClickAt + Props.standUpSeconds);

        /// <summary>
        /// Facing the camera while kneeling, so the hands, the guard and the blade going home show (the user,
        /// 2026-09-28; the sketch draws him from behind). The warm-up faces the camera too.
        /// </summary>
        public override Rot4 Facing(int now) => back ? Rot4.South : Rot4.Invalid;

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            CutEndTimes times = Times;
            if (!back && !aborted)
            {
                if (now < TickAt(times.BackAt)) BreakProjectiles();
                else Return();
            }
            if (back && !clicked && !aborted && now >= TickAt(times.ClickAt)) Click(now);
            return Seconds(now) < times.Duration;
        }

        private static readonly List<Thing> doomed = new List<Thing>();

        /// <summary>While he is gone, every projectile inside the radius is destroyed: bullets, rockets, mortar shells.</summary>
        private void BreakProjectiles()
        {
            if (caster == null || !caster.Spawned || caster.Map != home) return;
            float reach = Props.radius * Props.radius;
            List<Thing> flying = home.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            doomed.Clear();
            for (int i = 0; i < flying.Count; i++)
                if ((flying[i].Position - caster.Position).LengthHorizontalSquared <= reach) doomed.Add(flying[i]);
            for (int i = 0; i < doomed.Count; i++)
                if (!doomed[i].Destroyed) doomed[i].Destroy(DestroyMode.Vanish);
            doomed.Clear();
        }

        /// <summary>He is back on his cell, kneeling.</summary>
        private void Return()
        {
            back = true;
            RemoveGone();
            if (caster != null && caster.Spawned) caster.Rotation = Rot4.South;
        }

        private void RemoveGone()
        {
            HediffDef def = Props.goneHediff ?? VergilDefOf.AG_VergilGone;
            Hediff gone = caster?.health?.hediffSet.GetFirstHediffOfDef(def);
            if (gone != null) caster.health.RemoveHediff(gone);
        }

        private static readonly List<Pawn> standing = new List<Pawn>();
        private static readonly List<Building> struck = new List<Building>();

        /// <summary>The click: the cuts round the marked pawns still standing, nearest first, and the buildings.</summary>
        private void Click(int now)
        {
            clicked = true;
            if (caster == null || !caster.Spawned || caster.Map != home) return;
            CompProperties_JudgementCutEnd props = Props;
            CutEndTimes times = Times;

            standing.Clear();
            for (int i = 0; i < marked.Count; i++)
                if (marked[i] != null && marked[i].Spawned && !marked[i].Dead && marked[i].Map == home) standing.Add(marked[i]);
            standing.Sort((a, b) => (a.Position - caster.Position).LengthHorizontalSquared.CompareTo((b.Position - caster.Position).LengthHorizontalSquared));
            var dealt = new int[standing.Count];
            int next = 0;
            for (int c = 0; c < cuts && standing.Count > 0; c++)
            {
                // Round the pawns still standing, nearest first; one killed by an earlier cut is passed over.
                int tries = 0;
                while (tries < standing.Count && (standing[next].Dead || !standing[next].Spawned))
                {
                    next = (next + 1) % standing.Count;
                    tries++;
                }
                if (tries >= standing.Count) break;
                Pawn pawn = standing[next];
                VergilKit.Cut(pawn, caster, props.damageDef, props.cutDamage, props.armorPenetration, (pawn.Position - caster.Position).AngleFlat);
                float at = times.ClickAt + 0.04f + next * 0.025f + dealt[next] * 0.08f;
                if (dealt[next] < 6) hits.Add((pawn, at, (dealt[next] * 47 + next * 31 + 20) % 180 - (dealt[next] % 2 == 0 ? 90 : 0)));
                dealt[next]++;
                next = (next + 1) % standing.Count;
            }
            standing.Clear();

            // Hostile or unowned buildings in range and in sight; natural rock is not a building anyone owns.
            struck.Clear();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(caster.Position, home, props.radius, true))
            {
                if (!(thing is Building building) || !building.def.useHitPoints || building.def.building?.isNaturalRock == true) continue;
                if (building.Faction != null && !building.Faction.HostileTo(caster.Faction)) continue;
                if (!GenSight.LineOfSightToThing(caster.Position, building, home, true)) continue;
                struck.Add(building);
            }
            for (int i = 0; i < struck.Count; i++)
                if (!struck[i].Destroyed)
                    VergilKit.Cut(struck[i], caster, props.damageDef, props.buildingDamage, 0f, (struck[i].Position - caster.Position).AngleFlat);
            struck.Clear();
        }

        /// <summary>Downed or killed mid-technique: he is visible again, nothing is cut.</summary>
        public override void JobEnded(int now)
        {
            aborted = true;
            RemoveGone();
        }

        public override void Discard() => RemoveGone();

        public override void Pose(float s)
        {
            if (caster == null || !caster.Spawned) return;
            CutEndTimes times = Times;
            CompProperties_JudgementCutEnd props = Props;
            float w = Mathf.Clamp01((s - T.CastAt) / times.Warm), sinceClick = s - times.ClickAt;
            if (s < times.VanishAt || !Fired)
            {
                float hand = VfxMath.Smooth(w * 3f);
                if (hand > 0f) VergilLooks.For(caster).hand = hand;
                return;
            }
            if (!aborted)
            {
                VergilLook look = VergilLooks.For(caster);
                if (s < times.BackAt)
                {
                    float leaving = (s - times.VanishAt) / 0.08f;
                    if (leaving < 1f)
                    {
                        look.hand = 1f;
                        look.tint = VergilGraphics.Ice;
                        look.tintAmount = leaving;
                    }
                    else look.gone = true;
                }
                else if (sinceClick < props.standUpSeconds)
                {
                    float v = Mathf.Clamp01((s - times.BackAt) / times.Sheathe);
                    look.kneel = 1f;
                    if (caster.Rotation == Rot4.South) look.kneelPicture = EchoCostume.KneelPicture(caster);
                    look.sheathe = v < 0.9f ? 0.88f * VfxMath.Smooth(v / 0.9f) : 0.88f + 0.12f * (v - 0.9f) / 0.1f;
                    look.tint = VergilGraphics.Ice;
                    look.tintAmount = 1f - Mathf.Clamp01((s - times.BackAt) / 0.2f);
                }
            }

            // The marked pawns stop and turn dark blue while he is away, until the click.
            float dark = VfxMath.Smooth((s - times.VanishAt) / 0.12f);
            if (sinceClick < 0f)
                for (int i = 0; i < marked.Count; i++)
                    if (marked[i] != null) VergilLooks.Tint(marked[i], VergilGraphics.Deep, 0.5f * dark);
        }

        public override void Draw(float s)
        {
            if (home == null || caster == null) return;
            CutEndTimes times = Times;
            Vector2 o = caster.Spawned && caster.Map == home ? VergilKit.Ground(caster.Position) : VergilKit.Ground(caster.PositionHeld);
            Vector2 mouth = caster.Spawned ? YamatoDraw.KneelingMouth(caster) : o + new Vector2(-0.3f, 0.46f);
            List<CutEndCut> lines = Fired ? Layout : T.Layout(Props.radius, 1, markedAt);
            JudgementCutEndGraphics.Draw(o, Props.radius, lines, Mathf.Min(marked.Count, lines.Count), "cast " + startTick, mouth, times,
                Fired || s < times.VanishAt ? s : times.VanishAt - 0.001f, home);
            for (int i = 0; i < hits.Count; i++)
            {
                (Pawn pawn, float at, float deg) = hits[i];
                if (pawn.Spawned && pawn.Map == home) VergilGraphics.HitCut(VergilKit.Ground(pawn.DrawPos), deg, s - at);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cuts, "cuts");
            Scribe_Values.Look(ref back, "back");
            Scribe_Values.Look(ref clicked, "clicked");
            Scribe_Values.Look(ref aborted, "aborted");
            Scribe_Collections.Look(ref marked, "marked", LookMode.Reference);
            Scribe_Collections.Look(ref markedAt, "markedAt", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (marked == null) marked = new List<Pawn>();
                if (markedAt == null) markedAt = new List<Vector2>();
                while (markedAt.Count < marked.Count) markedAt.Add(Vector2.zero);
            }
        }
    }
}
