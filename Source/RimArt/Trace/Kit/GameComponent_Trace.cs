using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxMath;
using R = RimArt.TraceReinforcementTiming;
using T = RimArt.TraceOnTiming;

namespace RimArt
{
    /// <summary>
    /// Shirou's Trace On and Reinforcement in the game: the casts under way, the copies in hands (the only thing
    /// saved), and the pictures that outlive a cast: a copy breaking, footprints, the slash of a reinforced hit, the
    /// circuit running back when the buff ends. Rules tick on game time; <see cref="GameComponentUpdate"/> draws the
    /// map on screen, fitted to real pawns (<see cref="PawnFit"/>).
    /// </summary>
    public sealed class GameComponent_Trace : GameComponent
    {
        private List<TraceCopy> copies = new List<TraceCopy>();
        private readonly List<TraceCast> casts = new List<TraceCast>();
        private readonly List<Break> breaks = new List<Break>();
        private readonly HashSet<Pawn> reinforced = new HashSet<Pawn>();
        private readonly List<Print> prints = new List<Print>();
        private readonly List<Hit> hits = new List<Hit>();
        private readonly List<End> ends = new List<End>();
        private readonly Dictionary<Pawn, int> hitCount = new Dictionary<Pawn, int>();

        /// <summary>A copy breaking where it was drawn, or slipping from a falling hand first.</summary>
        private sealed class Break
        {
            public Map map;
            public TraceShape shape;
            public UbwPose pose;
            public float altitude, side;
            public Vector2 dust;
            public bool fall;
            public int tick;
        }

        private struct Print
        {
            public Map map;
            public Vector2 at, heading;
            public int k, tick;
        }

        private struct Hit
        {
            public Pawn foe;
            public Vector2 from;
            public int index, tick;
        }

        private struct End
        {
            public Pawn pawn;
            public int tick;
        }

        public GameComponent_Trace(Game game) { }

        public static GameComponent_Trace Instance => Current.Game?.GetComponent<GameComponent_Trace>();

        // ---- copies ----------------------------------------------------------------------------------------------

        public TraceCopy CopyOf(Thing thing)
        {
            if (thing == null) return null;
            for (int i = 0; i < copies.Count; i++)
                if (copies[i].thing == thing) return copies[i];
            return null;
        }

        public void AddCopy(TraceCopy copy) => copies.Add(copy);

        public void RemoveCopy(Thing thing) => copies.RemoveAll(c => c.thing == thing);

        /// <summary>The Unlimited Blade Works Arm copies still held, as a new list (breaking one removes it from this component's).</summary>
        public List<TraceCopy> ArmCopies() => copies.FindAll(c => c.ubwArm && c.thing != null && !c.thing.Destroyed);

        /// <summary>The copy in this pawn's hand, or null.</summary>
        public ThingWithComps HeldCopy(Pawn pawn)
        {
            ThingWithComps held = pawn?.equipment?.Primary;
            return CopyOf(held) != null ? held : null;
        }

        /// <summary>Records the picture of a copy leaving the hand, at the pose it was last drawn in (or carried in).</summary>
        public void AddBreak(Pawn holder, ThingWithComps copy, bool fall)
        {
            TraceShape shape = TraceWeaponShapes.For(copy);
            if (shape == null) return;
            UbwPose pose;
            float altitude;
            if (TraceHands.DrawnAt(holder, copy, 3, out Vector3 loc, out float aim))
            {
                pose = TraceHands.PoseAt(shape, copy, loc, aim);
                altitude = loc.y;
            }
            else if (!TraceHands.Pose(holder, copy, shape, true, out pose, out altitude)) return;
            TraceBody body = TraceHands.Body(holder, pose);
            breaks.Add(new Break { map = holder.Map, shape = shape, pose = pose, altitude = altitude, side = body.Side, dust = body.Me, fall = fall, tick = Find.TickManager.TicksGame });
        }

        // ---- casts -----------------------------------------------------------------------------------------------

        /// <summary>A cast job began: its cast replaces one this caster never fired.</summary>
        public void Begin(TraceCast cast)
        {
            casts.RemoveAll(c => c.caster == cast.caster && !c.Fired);
            casts.Add(cast);
        }

        /// <summary>The latest cast of this ability by this caster, fired or not.</summary>
        public TraceCast CastOf(Pawn caster, AbilityDef def)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster && casts[i].def == def) return casts[i];
            return null;
        }

        public void Fired(Pawn caster, AbilityDef def)
        {
            TraceCast cast = CastOf(caster, def);
            if (cast != null && !cast.Fired) cast.MarkFired(Find.TickManager.TicksGame);
        }

        public bool FiredSince(Pawn caster, int sinceTick)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Fired && casts[i].fireTick >= sinceTick) return true;
            return false;
        }

        public TraceCast Holding(Pawn caster, int now)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Holds(now)) return casts[i];
            return null;
        }

        /// <summary>The cast job ended: a cast that never fired is dropped.</summary>
        public void JobEnded(Pawn caster) => casts.RemoveAll(c => c.caster == caster && !c.Fired);

        /// <summary>The real weapon this pawn is putting away, which the game must not draw, or null.</summary>
        public ThingWithComps Stowing(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == pawn && casts[i].stowing != null && casts[i].Seconds < casts[i].lead) return casts[i].stowing;
            return null;
        }

        /// <summary>Whether a picture here needs to know where the game draws this pawn's weapon.</summary>
        public bool Watches(Pawn pawn)
        {
            if (reinforced.Contains(pawn)) return true;
            for (int i = 0; i < casts.Count; i++) if (casts[i].caster == pawn) return true;
            for (int i = 0; i < copies.Count; i++) if (copies[i].holder == pawn) return true;
            for (int i = 0; i < ends.Count; i++) if (ends[i].pawn == pawn) return true;
            return false;
        }

        // ---- Reinforcement ---------------------------------------------------------------------------------------

        /// <summary>Called every tick by the buff (<see cref="HediffComp_TraceReinforced"/>), so a loaded game finds its pawns again.</summary>
        public void Reinforced(Pawn pawn) => reinforced.Add(pawn);

        public bool IsReinforced(Pawn pawn) =>
            pawn != null && reinforced.Contains(pawn) && pawn.health?.hediffSet?.HasHediff(TraceDefOf.AG_TraceReinforced) == true;

        public void ReinforceEnded(Pawn pawn)
        {
            reinforced.Remove(pawn);
            if (pawn != null && pawn.Spawned) ends.Add(new End { pawn = pawn, tick = Find.TickManager.TicksGame });
        }

        public void AddPrint(Pawn pawn, Vector2 heading, int k) =>
            prints.Add(new Print { map = pawn.Map, at = new Vector2(pawn.DrawPos.x, pawn.DrawPos.z), heading = heading, k = k, tick = Find.TickManager.TicksGame });

        /// <summary>A melee hit landed: a reinforced attacker's gets the slash.</summary>
        public void AddHit(Pawn attacker, Pawn foe)
        {
            if (!IsReinforced(attacker) || foe == null || !foe.Spawned) return;
            hitCount.TryGetValue(attacker, out int n);
            hitCount[attacker] = n + 1;
            Vector3 d = foe.DrawPos - attacker.DrawPos;
            var from = new Vector2(d.x, d.z);
            hits.Add(new Hit { foe = foe, from = from.sqrMagnitude > 1e-4f ? from.normalized : Vector2.right, index = n, tick = Find.TickManager.TicksGame });
        }

        /// <summary>For game tests: how many of this pawn's hits got the slash.</summary>
        public int HitsBy(Pawn attacker) => hitCount.TryGetValue(attacker, out int n) ? n : 0;

        // ---- ticking and drawing ---------------------------------------------------------------------------------

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
                if (!casts[i].Tick(now)) casts.RemoveAt(i);
            if (now % TraceCopies.CheckTicks == 0) TraceCopies.Check(copies);
            reinforced.RemoveWhere(p => p == null || p.Destroyed || !p.health.hediffSet.HasHediff(TraceDefOf.AG_TraceReinforced));
            breaks.RemoveAll(b => (now - b.tick) / 60f > (b.fall ? T.Fall : 0f) + Mathf.Max(T.Break, T.Dust));
            prints.RemoveAll(p => (now - p.tick) / 60f > R.Print);
            hits.RemoveAll(h => (now - h.tick) / 60f > R.Slash || h.foe == null || !h.foe.Spawned);
            ends.RemoveAll(e => (now - e.tick) / 60f > Mathf.Max(R.EndLines, R.GlowFade) || e.pawn == null || !e.pawn.Spawned);
        }

        public override void GameComponentUpdate()
        {
            TraceHands.Watching = casts.Count > 0 || copies.Count > 0 || reinforced.Count > 0 || ends.Count > 0;
            Map map = Find.CurrentMap;
            if (map == null || (!TraceHands.Watching && breaks.Count == 0 && prints.Count == 0 && hits.Count == 0)) return;
            PawnFit.Begin();
            try
            {
                for (int i = 0; i < casts.Count; i++) casts[i].Draw(map);
                DrawBreaks(map);
                DrawReinforced(map);
            }
            finally
            {
                PawnFit.End();
            }
        }

        private void DrawBreaks(Map map)
        {
            for (int i = 0; i < breaks.Count; i++)
            {
                Break b = breaks[i];
                if (b.map != map) continue;
                float age = UbwClock.Since(b.tick);
                string key = "trace break " + i;
                if (b.fall) TraceOnGraphics.Falling(key, b.shape, b.pose, age, b.side, b.dust, b.altitude, false);
                else TraceHandGraphics.Shatter(key, b.shape, b.pose, age / T.Break, 11, b.altitude);
            }
        }

        private void DrawReinforced(Map map)
        {
            float clock = UbwClock.Since(0);
            foreach (Pawn pawn in reinforced)
            {
                if (!pawn.Spawned || pawn.Map != map) continue;
                Vector2 me = new Vector2(pawn.DrawPos.x, pawn.DrawPos.z);
                Vector2 heading = TraceHands.Heading(pawn);
                if (heading != Vector2.zero) TraceReinforcementGraphics.Streaks("reinforce heels " + pawn.thingIDNumber, me, heading);
                TraceCast cast = CastOf(pawn, TraceDefOf.AG_Trace_Reinforcement);
                if (cast != null) continue;
                ThingWithComps weapon = pawn.equipment?.Primary;
                TraceShape shape = weapon != null ? TraceWeaponShapes.For(weapon) : null;
                if (TraceHands.Pose(pawn, weapon, shape, false, out UbwPose pose, out float altitude))
                    TraceReinforcementGraphics.WeaponGlow("reinforce glow " + pawn.thingIDNumber, shape, pose, 1f, 99f, clock, 1f, altitude);
            }
            for (int i = 0; i < prints.Count; i++)
                if (prints[i].map == map) TraceReinforcementGraphics.Footprint(prints[i].at, prints[i].heading, prints[i].k, UbwClock.Since(prints[i].tick));
            for (int i = 0; i < hits.Count; i++)
            {
                Hit h = hits[i];
                if (h.foe.Map != map) continue;
                TraceReinforcementGraphics.Slash("reinforce hit " + i, new Vector2(h.foe.DrawPos.x, h.foe.DrawPos.z), h.index, UbwClock.Since(h.tick), h.from);
            }
            for (int i = 0; i < ends.Count; i++)
            {
                Pawn pawn = ends[i].pawn;
                if (pawn.Map != map) continue;
                float age = UbwClock.Since(ends[i].tick);
                ThingWithComps weapon = pawn.equipment?.Primary;
                TraceShape shape = weapon != null ? TraceWeaponShapes.For(weapon) : null;
                bool drawn = TraceHands.Pose(pawn, weapon, shape, false, out UbwPose pose, out float altitude);
                string key = "reinforce end " + pawn.thingIDNumber;
                TraceReinforcementGraphics.EndLines(key, TraceHands.Body(pawn, pose), age);
                if (drawn) TraceReinforcementGraphics.WeaponGlow(key, shape, pose, 1f, 99f, clock, 1f - Smooth(age / R.GlowFade), altitude);
            }
        }

        /// <summary>For game tests: every cast and picture dropped, every copy destroyed.</summary>
        public void ResetForTests()
        {
            foreach (TraceCopy c in copies)
                if (c.thing != null && !c.thing.Destroyed)
                {
                    if (c.holder?.equipment != null && c.holder.equipment.Contains(c.thing)) c.holder.equipment.DestroyEquipment(c.thing);
                    else c.thing.Destroy();
                }
            copies.Clear();
            casts.Clear();
            breaks.Clear();
            reinforced.Clear();
            prints.Clear();
            hits.Clear();
            ends.Clear();
            hitCount.Clear();
            TraceHands.Forget();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) copies.RemoveAll(c => c.thing == null || c.thing.Destroyed || c.holder == null);
            Scribe_Collections.Look(ref copies, "traceCopies", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (copies == null) copies = new List<TraceCopy>();
                copies.RemoveAll(c => c == null || c.thing == null || c.holder == null);
            }
        }
    }
}
