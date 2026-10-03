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
    /// - The caster stands and channels with no time limit; Throw ends it after minChannelSeconds.
    ///   A stun, a downing or death ends it with nothing and the charge and cooldown stay spent;
    ///   Cancel, a move order or a revert gives them back.
    /// - Power, added every tick: the caster gives casterPerSecond x ki and every colonist in the lend
    ///   job (<see cref="JobDriver_GokuLend"/>) lendPerSecond x ki, where ki is life energy x Rest
    ///   (<see cref="GokuLifeEnergy.Ki"/>). Giving costs restPerSecond of the giver's Rest bar, so
    ///   the rate falls as it tires; below stopRest a pawn gives nothing and a lender stops. The
    ///   ball holds at most maxPower; when it is full the lenders stop and no more Rest is spent.
    ///   From full Rest one pawn gives about 23 power over 36 s. Each lender's stints are kept for
    ///   the picture, which draws a ribbon from each while it lends.
    /// - The throw: the ball flies flySeconds and detonates. Radius radiusBase + radiusPerPower x power.
    ///   Every hostile pawn in the radius takes hits hits of damageBase + damagePerPower x power, the
    ///   first when the dome's front reaches it and the rest evenly until the burst, each on a body
    ///   part the game picks; nothing else is hurt.
    /// </summary>
    public sealed class SpiritBombCast : GokuCast
    {
        public IntVec3 castCell, target;
        public bool throwOrdered;
        public int throwTick = -1;
        /// <summary>The caster is free: the ball has left its hands.</summary>
        public bool released;
        /// <summary>The power now; after the throw, the power it was thrown with.</summary>
        public float power;
        /// <summary>Power per second over the last tick, from everyone giving (for the buttons).</summary>
        public float rateNow;

        // Lender stints: who, when it began and when it stopped (-1 while it lends), on the plan's clock.
        private List<Pawn> stintPawns = new List<Pawn>();
        private List<float> joinAt = new List<float>();
        private List<float> leaveAt = new List<float>();
        // Hostile pawns under the dome: who, when the front reached it on the plan's clock (Never once it
        // takes no more hits), and how many hits have landed.
        private List<Pawn> struckPawns = new List<Pawn>();
        private List<float> struckAt = new List<float>();
        private List<int> hitsLanded = new List<int>();
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
        public bool Full => power >= P.maxPower - 0.001f;
        /// <summary>The power now, or at the throw once thrown.</summary>
        public float PowerNow(int now) => power;
        public float RadiusNow(int now) => P.radiusBase + P.radiusPerPower * PowerNow(now);
        /// <summary>The damage of one of the hits each hostile under the dome takes.</summary>
        public float HitDamageNow(int now) => P.damageBase + P.damagePerPower * PowerNow(now);
        /// <summary>How many hits have landed on <paramref name="pawn"/> under the dome.</summary>
        public int HitsOn(Pawn pawn)
        {
            int i = struckPawns.IndexOf(pawn);
            return i < 0 ? 0 : hitsLanded[i];
        }
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
            plan = T.Plan(n, P.minChannelSeconds, P.flySeconds, P.domeHoldSeconds, P.pace, P.lendPerSecond, P.radiusPerPower, P.sizePerPower, P.radiusBase, joins, leaves,
                P.maxPower, power);
            if (throwTick < 0) T.Release(ref plan, T.Never);
            else T.Release(ref plan, Seconds(throwTick));
            if (lenderPos.Length != n) lenderPos = new Vector2[n];
        }

        // ---- the lenders ------------------------------------------------------------------------------------------

        public static bool IsLending(Pawn pawn, Pawn caster) =>
            pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.CurJobDef == GokuDefOf.AG_GokuLend && pawn.CurJob?.targetA.Thing == caster;

        /// <summary>Below stopRest a pawn gives nothing: Goku's part stops growing, a lender stops lending.</summary>
        public static bool TooTired(Pawn pawn) => GokuLifeEnergy.Rest(pawn) < P.stopRest;

        /// <summary>Power per second <paramref name="pawn"/> would give now: the rate x its ki, 0 when too tired.</summary>
        public static float RateOf(Pawn pawn, bool isCaster) =>
            TooTired(pawn) ? 0f : (isCaster ? P.casterPerSecond : P.lendPerSecond) * GokuLifeEnergy.Ki(pawn);

        /// <summary>One tick of giving: the caster and every lender add their rate and pay Rest for it, up to the cap.</summary>
        private void Gather()
        {
            if (Full)
            {
                rateNow = 0f;
                return;
            }
            float perSecond = Give(caster, true);
            for (int i = 0; i < lendingNow.Count; i++) perSecond += Give(lendingNow[i], false);
            rateNow = perSecond;
            power = Mathf.Min(P.maxPower, power + perSecond / 60f);
            plan.Live = power;
        }

        private static float Give(Pawn pawn, bool isCaster)
        {
            float rate = RateOf(pawn, isCaster);
            if (rate <= 0f) return 0f;
            Need_Rest rest = pawn.needs?.rest;
            if (rest != null) rest.CurLevel -= P.restPerSecond / 60f;
            return rate;
        }

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
            struckPawns.Clear();
            struckAt.Clear();
            hitsLanded.Clear();
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

        /// <summary>
        /// The damage. A hostile pawn joins when the dome's front reaches it; its hits land from then
        /// to the burst, evenly spaced. A pawn that dies, leaves the map or leaves the radius takes no
        /// more.
        /// </summary>
        private void TickDome(float s)
        {
            if (s < plan.Dome) return;
            float radius = Radius, damage = P.damageBase + P.damagePerPower * power;
            if (s < plan.Burst)
            {
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
                    struckAt.Add(Mathf.Min(s, plan.Passes(under[i].Position.DistanceTo(target))));
                    hitsLanded.Add(0);
                }
                under.Clear();
            }
            for (int i = 0; i < struckPawns.Count; i++)
            {
                Pawn pawn = struckPawns[i];
                float between = (plan.Burst - struckAt[i]) / Mathf.Max(1, P.hits);
                while (hitsLanded[i] < P.hits && s >= struckAt[i] + hitsLanded[i] * between)
                {
                    if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != home || pawn.Position.DistanceTo(target) > radius)
                    {
                        struckAt[i] = T.Never;
                        break;
                    }
                    hitsLanded[i]++;
                    pawn.TakeDamage(new DamageInfo(P.DamageDef, damage, P.armorPenetration, -1f, caster));
                }
            }
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
                Gather();
                // A heavy bomb trembles the camera every 0.5 s, as the picture's shakes do.
                int half = Mathf.FloorToInt(Channelled(now) * 2f);
                if (half > lastTremble)
                {
                    lastTremble = half;
                    float c = power / plan.Full;
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
            float s = T.Lead + PictureClock.Since(channelTick);
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
            Scribe_Collections.Look(ref struckPawns, "struckPawns", LookMode.Reference);
            Scribe_Collections.Look(ref struckAt, "struckAt", LookMode.Value);
            Scribe_Collections.Look(ref hitsLanded, "hitsLanded", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (stintPawns == null) stintPawns = new List<Pawn>();
                if (joinAt == null) joinAt = new List<float>();
                if (leaveAt == null) leaveAt = new List<float>();
                if (struckPawns == null) struckPawns = new List<Pawn>();
                if (struckAt == null) struckAt = new List<float>();
                if (hitsLanded == null) hitsLanded = new List<int>();
                // A struck pawn gone from the save loads as null and takes no more hits (TickDome); the three lists are kept the same length.
                int struck = Mathf.Min(struckPawns.Count, Mathf.Min(struckAt.Count, hitsLanded.Count));
                while (struckPawns.Count > struck) struckPawns.RemoveAt(struckPawns.Count - 1);
                while (struckAt.Count > struck) struckAt.RemoveAt(struckAt.Count - 1);
                while (hitsLanded.Count > struck) hitsLanded.RemoveAt(hitsLanded.Count - 1);
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
