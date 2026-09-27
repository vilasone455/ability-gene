using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.GokuSpiritBombTiming;

namespace RimArt
{
    /// <summary>
    /// One Spirit Bomb, from the channel to the scorch. The rules (docs/heroes.md; the numbers are
    /// <see cref="CompProperties_SpiritBomb"/> on the ability def):
    ///
    /// - The caster stands and channels with no upper limit; Throw ends it after minChannelSeconds.
    ///   A stun, a downing or death ends it with nothing and the charge and cooldown stay spent;
    ///   Cancel, a move order or a revert gives them back.
    /// - Power: 1 per second from the caster, lendPerSecond from every colonist in the lend job
    ///   (<see cref="JobDriver_GokuLend"/>) while it lends. Each lender's stints are kept for the
    ///   picture, which draws a ribbon from each while it lends.
    /// - The throw: the ball flies flySeconds and detonates. Radius radiusBase + radiusPerPower x power;
    ///   damage damageBase + damagePerPower x power to every hostile pawn in the radius when the dome's
    ///   front reaches it; nothing else is hurt.
    /// </summary>
    public sealed class SpiritBombCast : GokuCast
    {
        public IntVec3 castCell, target;
        public bool throwOrdered;
        public int throwTick = -1;
        /// <summary>The caster is free: the ball has left its hands.</summary>
        public bool released;
        /// <summary>The power at the throw.</summary>
        public float power;

        // Lender stints: who, when it began and when it stopped (-1 while it lends), on the plan's clock.
        private List<Pawn> stintPawns = new List<Pawn>();
        private List<float> joinAt = new List<float>();
        private List<float> leaveAt = new List<float>();
        private readonly List<Pawn> struckPawns = new List<Pawn>();
        private readonly List<Pawn> sparedPawns = new List<Pawn>();
        private Vector2[] lenderPos = new Vector2[0];
        private Vector2[] sparedPos = new Vector2[0];
        private Vector2[] wallPos = new Vector2[0];
        private SpiritBombPlan plan;
        private List<GokuShake> shakes = new List<GokuShake>();
        private int shaken, lastTremble = -1, sparedTick = -1;

        private static CompProperties_SpiritBomb P => GokuBusy.Props<CompProperties_SpiritBomb>(GokuDefOf.AG_GokuSpiritBomb);
        private static readonly List<Pawn> lendingNow = new List<Pawn>();

        public bool Channelling => !broken && channelTick >= 0 && throwTick < 0;
        public bool Thrown => !broken && throwTick >= 0;
        public override bool Busy => !broken && !released;
        public override string BusyReason => Thrown ? "Throwing." : "Channelling a Spirit Bomb.";
        public override IntVec3 FacingCell => target;
        public override IntVec3 ChannelCell => castCell;
        protected override AbilityDef Def => GokuDefOf.AG_GokuSpiritBomb;
        protected override string Name => "Spirit Bomb";
        protected override float Lead => T.Lead;

        public bool CanThrow(int now) => Channelling && Channelled(now) >= P.minChannelSeconds;
        /// <summary>The power now, or at the throw once thrown.</summary>
        public float PowerNow(int now) => Thrown ? power : plan.PowerAt(Seconds(now));
        public float RadiusNow(int now) => P.radiusBase + P.radiusPerPower * PowerNow(now);
        public float DamageNow(int now) => P.damageBase + P.damagePerPower * PowerNow(now);
        public int LenderCount { get { int n = 0; for (int i = 0; i < leaveAt.Count; i++) if (leaveAt[i] < 0f) n++; return n; } }

        public SpiritBombCast() { }

        public SpiritBombCast(Pawn caster, IntVec3 target, int now, float paid)
        {
            this.caster = caster;
            this.target = target;
            this.paid = paid;
            home = caster.Map;
            castCell = caster.Position;
            queuedTick = now;
            Rebuild();
        }

        public override void StartChannel(int now)
        {
            base.StartChannel(now);
            castCell = caster.Position;
            Rebuild();
        }

        /// <summary>The plan from the stints so far; the throw never comes until <see cref="Throw"/> sets it.</summary>
        private void Rebuild()
        {
            int n = Mathf.Min(stintPawns.Count, GokuSpiritBombGraphics.MostLenders);
            var joins = new float[n];
            var leaves = new float[n];
            for (int i = 0; i < n; i++)
            {
                joins[i] = joinAt[i];
                leaves[i] = leaveAt[i] < 0f ? T.Never : leaveAt[i];
            }
            plan = T.Plan(n, P.minChannelSeconds, P.flySeconds, P.domeHoldSeconds, P.pace, P.lendPerSecond, P.radiusPerPower, P.sizePerPower, P.radiusBase, joins, leaves);
            if (throwTick < 0) T.Release(ref plan, T.Never);
            else T.Release(ref plan, Seconds(throwTick));
            if (lenderPos.Length != n) lenderPos = new Vector2[n];
        }

        // ---- the lenders ------------------------------------------------------------------------------------------

        public static bool IsLending(Pawn pawn, Pawn caster) =>
            pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.CurJobDef == GokuDefOf.AG_GokuLend && pawn.CurJob?.targetA.Thing == caster;

        private void UpdateLenders(int now)
        {
            float s = Seconds(now);
            lendingNow.Clear();
            IReadOnlyList<Pawn> colonists = home.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                if (colonists[i] != caster && IsLending(colonists[i], caster)) lendingNow.Add(colonists[i]);
            bool changed = false;
            // Stints that ended.
            for (int i = 0; i < stintPawns.Count; i++)
            {
                if (leaveAt[i] >= 0f || lendingNow.Contains(stintPawns[i])) continue;
                leaveAt[i] = s;
                changed = true;
            }
            // New stints.
            for (int i = 0; i < lendingNow.Count; i++)
            {
                Pawn pawn = lendingNow[i];
                bool open = false;
                for (int k = 0; k < stintPawns.Count; k++)
                    if (stintPawns[k] == pawn && leaveAt[k] < 0f) { open = true; break; }
                if (open || stintPawns.Count >= GokuSpiritBombGraphics.MostLenders) continue;
                stintPawns.Add(pawn);
                joinAt.Add(s);
                leaveAt.Add(-1f);
                changed = true;
            }
            if (changed) Rebuild();
        }

        // ---- the throw --------------------------------------------------------------------------------------------

        private void Throw(int now)
        {
            throwTick = now;
            float s = Seconds(now);
            for (int i = 0; i < leaveAt.Count; i++) if (leaveAt[i] < 0f) leaveAt[i] = s;
            Rebuild();
            power = plan.PowerAt(s);
            struckPawns.Clear();
            Walls();
            Spared(now);
            shakes = new List<GokuShake>();
            foreach (GokuShake shake in T.Shakes(plan)) if (shake.At >= plan.Release) shakes.Add(shake);
            GokuTiming.Sorted(shakes);
            shaken = 0;
        }

        private float Radius => plan.BlastAt(plan.Release);

        /// <summary>Structure cells under the dome, nearest first, as many as the picture draws.</summary>
        private void Walls()
        {
            var cells = new List<IntVec3>();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(target, Radius, true))
            {
                if (!c.InBounds(home) || !c.Filled(home)) continue;
                cells.Add(c);
                if (cells.Count >= 3) break;
            }
            wallPos = new Vector2[cells.Count];
            for (int i = 0; i < cells.Count; i++) wallPos[i] = Vec(cells[i]);
        }

        /// <summary>Pawns under the dome it spares (not hostile to the caster), for the blue shells.</summary>
        private void Spared(int now)
        {
            sparedTick = now;
            sparedPawns.Clear();
            float radius = Radius;
            foreach (Pawn pawn in home.mapPawns.AllPawnsSpawned)
            {
                if (pawn == caster || pawn.Dead || pawn.HostileTo(caster) || sparedPawns.Count >= 8) continue;
                if (pawn.Position.DistanceTo(target) <= radius) sparedPawns.Add(pawn);
            }
            if (sparedPos.Length != sparedPawns.Count) sparedPos = new Vector2[sparedPawns.Count];
        }

        private static readonly List<Pawn> under = new List<Pawn>();

        /// <summary>The damage: each hostile pawn under the dome takes it when the front reaches it.</summary>
        private void TickDome(float s)
        {
            if (s < plan.Dome || s >= plan.Burst) return;
            float radius = Radius, damage = P.damageBase + P.damagePerPower * power;
            under.Clear();
            IReadOnlyList<Pawn> pawns = home.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == caster || pawn.Dead || struckPawns.Contains(pawn) || !pawn.HostileTo(caster)) continue;
                float d = pawn.Position.DistanceTo(target);
                if (d <= radius && s >= plan.Passes(d)) under.Add(pawn);
            }
            for (int i = 0; i < under.Count; i++)
            {
                struckPawns.Add(under[i]);
                under[i].TakeDamage(new DamageInfo(P.DamageDef, damage, P.armorPenetration, -1f, caster));
            }
            under.Clear();
        }

        // ---- the clock --------------------------------------------------------------------------------------------

        public override bool Tick(int now)
        {
            if (broken) return false;
            if (Queued)
            {
                if (now - queuedTick > QueueTimeout) Cancel(false);
                return !broken;
            }
            if (Channelling)
            {
                if (!Held())
                {
                    Cancel(BrokenBySelf());
                    return false;
                }
                UpdateLenders(now);
                // A heavy bomb trembles the camera every 0.5 s, as the picture's shakes do.
                int half = Mathf.FloorToInt(Channelled(now) * 2f);
                if (half > lastTremble)
                {
                    lastTremble = half;
                    float c = plan.PowerAt(Seconds(now)) / T.FullPower;
                    if (half > 0 && c > 0.5f && Find.CurrentMap == home) Find.CameraDriver.shaker.DoShake(0.012f + 0.02f * Mathf.Min(1f, c));
                }
                if (throwOrdered && CanThrow(now)) Throw(now);
                return true;
            }
            float s = Seconds(now);
            while (shaken < shakes.Count && s >= shakes[shaken].At)
            {
                if (Find.CurrentMap == home) Find.CameraDriver.shaker.DoShake(shakes[shaken].Size);
                shaken++;
            }
            if (now - sparedTick >= 15 && s < plan.Gone) Spared(now);
            TickDome(s);
            if (!released && s >= plan.Fly) released = true;
            return s < plan.End;
        }

        // ---- the picture ------------------------------------------------------------------------------------------

        public override void Draw()
        {
            if (broken || channelTick < 0 || home == null) return;
            float s = T.Lead + UbwClock.Since(channelTick);
            for (int i = 0; i < lenderPos.Length; i++)
            {
                Pawn p = stintPawns[i];
                if (p.Spawned && p.Map == home) lenderPos[i] = Vec(p.DrawPos);
            }
            for (int i = 0; i < sparedPawns.Count && i < sparedPos.Length; i++)
            {
                Pawn p = sparedPawns[i];
                if (p.Spawned && p.Map == home) sparedPos[i] = Vec(p.DrawPos);
            }
            GokuSpiritBombGraphics.Draw(new SpiritBombShot
            {
                Caster = Vec(castCell), Target = Vec(target), Seconds = s, Plan = plan, Lenders = lenderPos,
                Spared = sparedPos, SparedCount = Mathf.Min(sparedPawns.Count, sparedPos.Length), Walls = wallPos, WallCount = wallPos.Length,
            }, home);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref castCell, "castCell");
            Scribe_Values.Look(ref target, "target");
            Scribe_Values.Look(ref throwOrdered, "throwOrdered");
            Scribe_Values.Look(ref throwTick, "throwTick", -1);
            Scribe_Values.Look(ref released, "released");
            Scribe_Values.Look(ref power, "power");
            Scribe_Collections.Look(ref stintPawns, "stintPawns", LookMode.Reference);
            Scribe_Collections.Look(ref joinAt, "joinAt", LookMode.Value);
            Scribe_Collections.Look(ref leaveAt, "leaveAt", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (stintPawns == null) stintPawns = new List<Pawn>();
                if (joinAt == null) joinAt = new List<float>();
                if (leaveAt == null) leaveAt = new List<float>();
                // A stint whose pawn is gone keeps its power but has no one to draw a ribbon from.
                for (int i = stintPawns.Count - 1; i >= 0; i--)
                    if (stintPawns[i] == null) { stintPawns.RemoveAt(i); joinAt.RemoveAt(i); leaveAt.RemoveAt(i); }
                while (joinAt.Count > stintPawns.Count) joinAt.RemoveAt(joinAt.Count - 1);
                while (leaveAt.Count > stintPawns.Count) leaveAt.RemoveAt(leaveAt.Count - 1);
                if (caster != null)
                {
                    Rebuild();
                    if (throwTick >= 0 && home != null)
                    {
                        Walls();
                        shakes = new List<GokuShake>();
                    }
                }
            }
        }
    }
}
