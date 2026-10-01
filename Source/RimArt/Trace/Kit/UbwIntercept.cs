using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.UbwCommandTiming;

namespace RimArt
{
    /// <summary>One shot a sword is on its way to meet: where on the shot's line, when, and whether it has been stopped.</summary>
    public sealed class UbwMeet : IExposable
    {
        public Thing shot;
        public int seed, launch, meet;
        /// <summary>Cells along the shot's line from where it was fired, and the meeting point in cells from the world's middle corner.</summary>
        public float along;
        public Vector2 point;
        public bool explosive;
        /// <summary>When the shot was stopped or vanished (int.MinValue while the sword is on its way).</summary>
        public int done = int.MinValue;

        public void ExposeData()
        {
            // A shot already stopped is destroyed: no reference to save.
            if (Scribe.mode == LoadSaveMode.Saving && shot != null && shot.Destroyed) shot = null;
            Scribe_References.Look(ref shot, "shot");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref launch, "launch");
            Scribe_Values.Look(ref meet, "meet");
            Scribe_Values.Look(ref along, "along");
            Scribe_Values.Look(ref point, "point");
            Scribe_Values.Look(ref explosive, "explosive");
            Scribe_Values.Look(ref done, "done", int.MinValue);
        }
    }

    /// <summary>
    /// Intercept (the caster's toggle, <see cref="UbwCast.intercept"/>). While it is on, each shot a pawn hostile to the
    /// caster fires inside the world is checked once, the tick it is first seen: of the swords that can reach its line
    /// first (interceptRiseSeconds plus the distance at interceptSpeed, no more than the shot needs to get there), the one
    /// nearest the line leaves the ground (swordCostSeconds of the world) and meets it, no nearer than
    /// interceptClearOfGun to where it was fired and at least interceptShortOfTarget short of where it was aimed. There a
    /// plain shot ends without hitting anything and an explosive one bursts; the sword breaks into light. A shot no sword
    /// can reach in time (fired from close by, or across bare ground) is not stopped and costs nothing. Only direct-flight
    /// rounds count (<see cref="RoundBackend.DirectFlight"/>), and not one Recursion holds.
    ///
    /// The check runs in a prefix on Projectile.TickInterval (<see cref="Patch_Projectile_TickInterval_UbwIntercept"/>), a
    /// no-op while no cast intercepts; while one does, the world's projectiles tick every tick, as Shinra Tensei's do, so
    /// none flies past its meeting point between two updates.
    /// </summary>
    public sealed class UbwIntercept : IExposable
    {
        public List<UbwMeet> meets = new List<UbwMeet>();
        /// <summary>Shots already checked; not saved, so a shot in flight across a load is checked again.</summary>
        private readonly HashSet<int> seen = new HashSet<int>();

        /// <summary>The casts with Intercept on or a sword still on its way, refreshed every tick by the game component.</summary>
        internal static readonly List<UbwCast> Live = new List<UbwCast>();

        private static readonly AccessTools.FieldRef<Projectile, Vector3> Origin = AccessTools.FieldRefAccess<Projectile, Vector3>("origin");
        private static readonly AccessTools.FieldRef<Projectile, Vector3> Destination = AccessTools.FieldRefAccess<Projectile, Vector3>("destination");
        private static readonly AccessTools.FieldRef<Projectile, int> TicksToImpact = AccessTools.FieldRefAccess<Projectile, int>("ticksToImpact");
        private static readonly MethodInfo Impact = AccessTools.Method(typeof(Projectile), "Impact");

        public bool Busy => meets.Exists(m => m.done == int.MinValue);

        /// <summary>For the prefix: false when the shot was stopped this tick and must not move.</summary>
        internal static bool BeforeTick(Projectile shot, int delta)
        {
            if (shot == null || !shot.Spawned || shot.Destroyed) return true;
            for (int i = 0; i < Live.Count; i++)
                if (Live[i].world == shot.Map) return Live[i].intercepts.Before(Live[i], shot, delta);
            return true;
        }

        private bool Before(UbwCast cast, Projectile shot, int delta)
        {
            UbwMeet meet = meets.Find(m => m.shot == shot && m.done == int.MinValue);
            if (meet == null && cast.intercept && cast.CommandsOpen && seen.Add(shot.thingIDNumber)) meet = Plan(cast, shot);
            return meet == null || !TryStop(meet, shot, delta);
        }

        /// <summary>The sword that meets this shot, taken out of the ground, or null if none can get there first.</summary>
        private UbwMeet Plan(UbwCast cast, Projectile shot)
        {
            RoundBackend round = Rounds.Vanilla;
            if (!round.DirectFlight(shot) || round.TicksToImpact(shot) <= 0 || RecursionRegistry.TryGetCapture(shot, out _)) return null;
            Thing launcher = shot.Launcher;
            if (!(launcher is Pawn) || cast.caster == null || !launcher.HostileTo(cast.caster)) return null;
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (inside == null) return null;
            UbwRules rules = UbwRules.Of;

            Vector2 o = Flat(Origin(shot)), d = Flat(Destination(shot)), p = Flat(round.Position(shot)), h = d - o;
            float total = h.magnitude;
            if (total < 1e-3f) return null;
            h /= total;
            float now = Vector2.Dot(p - o, h), speed = round.CurrentSpeedPerTick(shot) * 60f;
            float lo = Mathf.Max(rules.interceptClearOfGun, now), hi = total - rules.interceptShortOfTarget;
            if (hi <= lo || speed <= 0f) return null;

            UbwSword best = null;
            float bestPerp = float.MaxValue, bestAlong = 0f, bestTime = 0f;
            foreach (UbwSword sw in inside.Field.Swords)
            {
                if (!UbwFieldState.Usable(sw)) continue;
                Vector2 g = inside.Origin + new Vector2((float)sw.X, (float)sw.Z);
                float along = Vector2.Dot(g - o, h);
                if (along < lo || along > hi) continue;
                float perp = (g - (o + h * along)).magnitude;
                if (perp >= bestPerp) continue;
                float shotTime = (along - now) / speed;
                if (rules.interceptRiseSeconds + perp / rules.interceptSpeed > shotTime) continue;
                best = sw;
                bestPerp = perp;
                bestAlong = along;
                bestTime = shotTime;
            }
            if (best == null) return null;

            inside.Field.Take(best);
            cast.Spend(1);
            int tick = Find.TickManager.TicksGame;
            ProjectileProperties props = shot.def.projectile;
            var meet = new UbwMeet
            {
                shot = shot, seed = best.Seed, launch = tick, meet = tick + Mathf.Max(1, Mathf.CeilToInt(bestTime * 60f)), along = bestAlong,
                point = o + h * bestAlong - inside.Origin, explosive = props.explosionRadius > 0f,
            };
            meets.Add(meet);
            return meet;
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Stops the shot at the meeting point if it gets there within this step: a plain shot vanishes, an explosive one bursts there.</summary>
        private static bool TryStop(UbwMeet meet, Projectile shot, int delta)
        {
            RoundBackend round = Rounds.Vanilla;
            Vector2 o = Flat(Origin(shot)), h = Flat(Destination(shot)) - o;
            if (h.sqrMagnitude < 1e-6f) return false;
            h.Normalize();
            float now = Vector2.Dot(Flat(round.Position(shot)) - o, h), step = round.CurrentSpeedPerTick(shot) * delta;
            if (now + step < meet.along && round.TicksToImpact(shot) > delta) return false;

            Vector2 at = o + h * meet.along;
            meet.done = Find.TickManager.TicksGame;
            var exact = new Vector3(at.x, shot.def.Altitude, at.y);
            Origin(shot) = exact - new Vector3(h.x, 0f, h.y) * 0.5f;
            Destination(shot) = exact;
            TicksToImpact(shot) = 0;
            IntVec3 cell = exact.ToIntVec3();
            if (cell.InBounds(shot.Map)) shot.Position = cell;
            if (meet.explosive) Impact.Invoke(shot, new object[] { null, true });
            else shot.Destroy();
            return true;
        }

        public void Tick(UbwCast cast, int now)
        {
            for (int i = meets.Count - 1; i >= 0; i--)
            {
                UbwMeet m = meets[i];
                // A shot that ended some other way (it hit someone first): the sword still breaks where it was going.
                if (m.done == int.MinValue && (m.shot == null || m.shot.Destroyed || !m.shot.Spawned) && now >= m.meet) m.done = now;
                if (m.done != int.MinValue && now - Mathf.Max(m.done, m.meet) > Mathf.RoundToInt((float)(T.Shatter * 60.0))) meets.RemoveAt(i);
            }
        }

        /// <summary>Each sword rising from its hole and flying to its meeting point, the spark there and the sword breaking into light.</summary>
        internal void Draw(MapComponent_UnlimitedBladeWorks inside, in UbwCommandLook k)
        {
            foreach (UbwMeet m in meets)
            {
                UbwSword sw = inside.Field.BySeed(m.seed);
                if (sw == null) continue;
                var point = new UbwXZ(m.point.x, m.point.y);
                UbwXZ dir = T.Toward(sw.X, sw.Z, point.X, point.Z, out _);
                var shot = new UbwSwordShot
                {
                    Launch = 0, Lift = T.InterceptLift, Fly = Mathf.Max(0.04f, (m.meet - m.launch) / 60f - (float)T.InterceptLift), Dir = dir, Meet = true,
                    Start = new UbwV3(sw.X + dir.X * 0.1, 0.4, sw.Z + dir.Z * 0.1), Hit = new UbwV3(point.X, T.HitHeight, point.Z),
                };
                UbwCommandGraphics.Meeting(k, "ubw meet " + m.seed, sw, shot, UbwClock.Since(m.launch));
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref meets, "meets", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && meets == null) meets = new List<UbwMeet>();
        }
    }
}
