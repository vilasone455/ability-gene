using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Shark = RimArt.SamehadaSharkSkinTiming;
using Fusion = RimArt.SamehadaFusionTiming;

namespace RimArt
{
    public enum SamehadaCastKind { SharkSkin, Fusion }

    /// <summary>
    /// Everything Samehada draws on a map, and the cast bookkeeping its job asks about:
    /// - Shark Skin and Fusion casts: known from the warmup's start (<see cref="Begin"/>, from
    ///   JobDriver_CastSamehada), fired at <see cref="Land"/>; the job holds while <see cref="Holds"/>;
    /// - the picture of each fed hit (bite, drain, heal) and of each Shark Skin attack (the floor arc);
    /// - the Drained haze on drained pawns, the shark form on fused wielders, and the charge tally under a
    ///   selected wielder.
    ///
    /// The clock is game ticks. Nothing here is saved: a game loaded mid-picture loses the rest of it.
    /// The rules' state (charges, Shark Skin, the hediffs) is on the weapon and the pawns.
    /// </summary>
    public class MapComponent_Samehada : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster;
            public SamehadaCastKind kind;
            public int startTick, warmupTicks, untilTick;
            public bool landed, home, flared;
            public SamehadaSharkCast shark;
            public float startCharges;
            public int most;

            /// <summary>Seconds on the picture's clock: the ability fires at Lead.</summary>
            public float Seconds(int now) => SamehadaGraphics.Lead + (now - startTick - warmupTicks) / 60f;
            public float HoldUntil => kind == SamehadaCastKind.SharkSkin ? Shark.Sweep0 : Fusion.Fused0;
        }

        private struct HitPicture
        {
            public Pawn holder, target;
            public Vector2 lastTarget;
            public int tick;
            public bool gained, full;
        }

        private struct ArcPicture
        {
            public Pawn holder;
            public int tick;
            public float aim, radius, half;
        }

        /// <summary>A cast whose warmup ran out this long ago without firing was interrupted.</summary>
        private const float Overdue = 0.25f;

        private readonly List<Cast> casts = new List<Cast>();
        private readonly List<HitPicture> hits = new List<HitPicture>();
        private readonly List<ArcPicture> arcs = new List<ArcPicture>();
        private readonly List<Pawn> holders = new List<Pawn>(), drained = new List<Pawn>(), fused = new List<Pawn>();

        public MapComponent_Samehada(Map map) : base(map) { }

        public void Register(Pawn pawn)
        {
            if (pawn != null && !holders.Contains(pawn)) holders.Add(pawn);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Rescan();
        }

        private void Rescan()
        {
            holders.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (CompSamehada.HeldBy(p) != null) holders.Add(p);
                if (SamehadaFeeding.Stacks(p) > 0 && !drained.Contains(p)) drained.Add(p);
                if (FusedHediff(p) != null && !fused.Contains(p)) fused.Add(p);
            }
            drained.RemoveAll(p => p.Destroyed || SamehadaFeeding.Stacks(p) == 0);
            fused.RemoveAll(p => p.Destroyed || FusedHediff(p) == null);
        }

        private static Hediff FusedHediff(Pawn p) => p?.health?.hediffSet?.GetFirstHediffOfDef(SamehadaDefOf.AG_SamehadaFused);

        private static Vector2 Feet(Pawn pawn) => new Vector2(pawn.DrawPos.x, pawn.DrawPos.z);

        // ------------------------------------------------------------------ casts

        /// <summary>A cast's warmup has begun.</summary>
        public void Begin(Pawn caster, Ability ability)
        {
            Ended(caster);
            CompSamehada blade = CompSamehada.HeldBy(caster);
            if (caster == null || ability == null || blade == null) return;
            casts.Add(NewCast(caster, ability, blade, Find.TickManager.TicksGame));
        }

        private static Cast NewCast(Pawn caster, Ability ability, CompSamehada blade, int startTick)
        {
            bool shark = ability.CompOfType<CompAbilityEffect_SamehadaSharkSkin>() != null;
            float facing = SamehadaHeld.Facing(caster);
            var cast = new Cast
            {
                caster = caster, kind = shark ? SamehadaCastKind.SharkSkin : SamehadaCastKind.Fusion, startTick = startTick,
                warmupTicks = Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f),
                startCharges = blade.Charges, most = blade.Props.maxCharges,
            };
            if (shark)
            {
                SamehadaHeld.Pose(caster, false, out Vector2 hand, out float deg, out bool behind);
                cast.shark = new SamehadaSharkCast
                {
                    Caster = Feet(caster), CasterAt = Feet(caster), Aim = facing, StartCharges = blade.Charges, Most = blade.Props.maxCharges,
                    Hand = hand, HandAtCast = hand, BladeDeg = deg, Layer = SamehadaHeld.Layer(caster, behind), Step = SamehadaHeld.Step,
                };
            }
            return cast;
        }

        /// <summary>The ability fires: the tear or the merge starts now.</summary>
        public void Land(Pawn caster, Ability ability, CompSamehada blade)
        {
            Cast cast = casts.Find(c => c.caster == caster && !c.landed);
            if (cast == null)
            {
                int warmup = Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f);
                cast = NewCast(caster, ability, blade, Find.TickManager.TicksGame - warmup);
                casts.Add(cast);
            }
            cast.landed = true;
            int now = Find.TickManager.TicksGame;
            if (cast.kind == SamehadaCastKind.SharkSkin)
            {
                float seconds = ability.CompOfType<CompAbilityEffect_SamehadaSharkSkin>()?.Props.seconds ?? 10f;
                cast.untilTick = now + Mathf.RoundToInt((seconds + Shark.StripFade) * 60f);
                SamehadaSound.Play(SamehadaSound.Tear, map, caster.Position);
            }
            else
            {
                cast.untilTick = now + Mathf.RoundToInt((Fusion.Merge + 0.2f) * 60f);
                SamehadaSound.Play(SamehadaSound.Merge, map, caster.Position);
            }
        }

        /// <summary>A fusion has started on <paramref name="pawn"/>: draw its shark form while the hediff lasts.</summary>
        public void Fused(Pawn pawn)
        {
            if (pawn != null && !fused.Contains(pawn)) fused.Add(pawn);
        }

        public void Drained(Pawn pawn)
        {
            if (pawn != null && !drained.Contains(pawn)) drained.Add(pawn);
        }

        /// <summary>The caster's cast job is over. A cast that never fired has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (casts[i].caster != caster) continue;
                if (!casts[i].landed) casts.RemoveAt(i);
                else casts[i].home = true;
            }
        }

        /// <summary>Whether the caster's cast has fired and its job has not ended.</summary>
        public bool Fired(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && !casts[i].home) return true;
            return false;
        }

        /// <summary>Whether the caster's cast job should still hold it in place: until the flare or the merge is done.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                if (cast.caster == caster && cast.landed && !cast.home && cast.Seconds(now) < cast.HoldUntil) return true;
            }
            return false;
        }

        private Cast Casting(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && !casts[i].home) return casts[i];
            return null;
        }

        // ------------------------------------------------------------------ hits

        public void Hit(Pawn holder, Pawn target, bool gained, bool full)
        {
            hits.Add(new HitPicture { holder = holder, target = target, lastTarget = Feet(target), tick = Find.TickManager.TicksGame, gained = gained, full = full });
        }

        public void Attack(Pawn holder, float aim, float radius, float half)
        {
            arcs.Add(new ArcPicture { holder = holder, tick = Find.TickManager.TicksGame, aim = aim, radius = radius, half = half });
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                if (!cast.landed)
                {
                    if (cast.Seconds(now) > SamehadaGraphics.Lead + Overdue || !cast.caster.Spawned || cast.caster.Map != map) casts.RemoveAt(i);
                    continue;
                }
                if (cast.kind == SamehadaCastKind.SharkSkin && !cast.flared && cast.Seconds(now) >= Shark.Flare0)
                {
                    cast.flared = true;
                    SamehadaSound.Play(SamehadaSound.Flare, map, cast.caster.PositionHeld);
                }
                if (cast.home && now >= cast.untilTick) casts.RemoveAt(i);
            }
            hits.RemoveAll(h => (now - h.tick) / 60f >= SamehadaFeedTiming.BiteLife);
            arcs.RemoveAll(a => (now - a.tick) / 60f >= Shark.ArcLife);
            for (int i = 0; i < fused.Count; i++)
            {
                // The revert's sound, when the fusion has its last Revert seconds left.
                HediffComp_Disappears left = FusedHediff(fused[i])?.TryGetComp<HediffComp_Disappears>();
                if (left != null && left.ticksToDisappear == Mathf.RoundToInt(Fusion.Revert * 60f))
                    SamehadaSound.Play(SamehadaSound.Revert, map, fused[i].Position);
            }
            if (now % 60 == 0) Rescan();
        }

        // ------------------------------------------------------------------ drawing

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            float clock = Time.realtimeSinceStartup;

            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                float s = cast.Seconds(now);
                // Before the ability fires the picture holds at the end of the warmup.
                if (!cast.landed) s = Mathf.Min(s, SamehadaGraphics.Lead - 0.001f);
                bool weapon = !cast.home && cast.caster.Spawned && cast.caster.Map == map;
                if (cast.kind == SamehadaCastKind.SharkSkin)
                {
                    if (weapon)
                    {
                        cast.shark.CasterAt = Feet(cast.caster);
                        SamehadaHeld.Pose(cast.caster, false, out cast.shark.Hand, out cast.shark.BladeDeg, out bool behind);
                        cast.shark.Layer = SamehadaHeld.Layer(cast.caster, behind);
                    }
                    cast.shark.StripAlpha = Mathf.Clamp01((cast.untilTick - now) / (60f * Shark.StripFade));
                    SamehadaSharkSkinGraphics.DrawCast(cast.shark, s, map, weapon);
                }
                else if (weapon) DrawFusionCast(cast, s);
            }

            for (int i = 0; i < hits.Count; i++)
            {
                HitPicture h = hits[i];
                if (h.target.Spawned && h.target.Map == map) h.lastTarget = Feet(h.target);
                CompSamehada blade = CompSamehada.HeldBy(h.holder);
                if (!h.holder.Spawned || h.holder.Map != map || blade == null) continue;
                var shot = new SamehadaFeedHit
                {
                    Holder = Feet(h.holder), Target = h.lastTarget, Tip = SamehadaHeld.Tip(h.holder, blade),
                    Aim = Degrees(h.lastTarget - Feet(h.holder)), Gained = h.gained, Full = h.full,
                    TargetAltitude = h.target.Spawned ? h.target.DrawPos.y : AltitudeLayer.Pawn.AltitudeFor(),
                };
                SamehadaFeedGraphics.DrawHit(shot, (now - h.tick) / 60f, map);
                hits[i] = h;
            }

            for (int i = 0; i < arcs.Count; i++)
            {
                ArcPicture a = arcs[i];
                if (!a.holder.Spawned || a.holder.Map != map) continue;
                SamehadaSharkSkinGraphics.DrawAttackArc(Feet(a.holder), a.aim, (now - a.tick) / 60f, a.radius, a.half, map);
            }

            for (int i = 0; i < drained.Count; i++)
            {
                Pawn p = drained[i];
                int stacks = SamehadaFeeding.Stacks(p);
                if (stacks <= 0 || !p.Spawned || p.Map != map || p.Position.Fogged(map)) continue;
                VfxDraw.Begin(Feet(p));
                SamehadaGraphics.Drained(Feet(p), stacks, clock, 1f, p.DrawPos.y);
            }

            for (int i = 0; i < fused.Count; i++)
            {
                Pawn p = fused[i];
                Hediff hediff = FusedHediff(p);
                if (hediff == null || !p.Spawned || p.Map != map) continue;
                HediffComp_Disappears left = hediff.TryGetComp<HediffComp_Disappears>();
                float seconds = left != null ? left.disappearsAfterTicks / 60f : 15f;
                float s = Fusion.Merge0 + (left != null ? left.disappearsAfterTicks - left.ticksToDisappear : hediff.ageTicks) / 60f;
                SamehadaFusionGraphics.DrawFused(Feet(p), SamehadaHeld.Facing(p), s, seconds, map, p.DrawPos.y);
            }

            for (int i = 0; i < holders.Count; i++)
            {
                Pawn p = holders[i];
                CompSamehada blade = CompSamehada.HeldBy(p);
                if (blade == null || !p.Spawned || p.Map != map || p.Position.Fogged(map)) continue;
                Cast cast = Casting(p);
                if (cast == null && !Find.Selector.IsSelected(p)) continue;
                float charges = blade.ShownCharges;
                if (cast != null)
                {
                    float s = cast.Seconds(now);
                    charges = cast.kind == SamehadaCastKind.SharkSkin
                        ? Mathf.Max(0f, cast.startCharges - Shark.Cost * Shark.TearAt(s))
                        : Fusion.ChargesAt(s, cast.startCharges);
                }
                VfxDraw.Begin(Feet(p));
                SamehadaGraphics.Tally(Feet(p), charges, SamehadaHeld.Facing(p), 1f, blade.Props.maxCharges);
            }
        }

        /// <summary>The blade in Core's carried pose while Fusion's merge runs: its charges run down to none, and it stays in the hand.</summary>
        private void DrawFusionCast(Cast cast, float s)
        {
            if (!VfxDraw.Shown(Feet(cast.caster), map)) return;
            SamehadaHeld.Pose(cast.caster, false, out Vector2 hand, out float deg, out bool behind);
            VfxDraw.Begin(hand);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            SamehadaGraphics.Blade(hand, deg, Fusion.ChargesAt(s, cast.startCharges), sun, strength,
                layer: SamehadaHeld.Layer(cast.caster, behind), step: SamehadaHeld.Step, most: cast.most);
        }

        private static float Degrees(Vector2 run) => run.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;

        public override void MapRemoved()
        {
            casts.Clear();
            hits.Clear();
            arcs.Clear();
            holders.Clear();
            drained.Clear();
            fused.Clear();
        }
    }
}
