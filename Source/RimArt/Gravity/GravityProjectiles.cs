using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    public sealed class GravityFlight : IExposable
    {
        public Thing round;
        public Vector3 position, heading, originalOrigin, expectedDestination;
        public float remaining, speed;
        public void ExposeData()
        {
            Scribe_References.Look(ref round, "round");
            Scribe_Values.Look(ref position, "position");
            Scribe_Values.Look(ref heading, "heading");
            Scribe_Values.Look(ref originalOrigin, "originalOrigin");
            Scribe_Values.Look(ref expectedDestination, "expectedDestination");
            Scribe_Values.Look(ref remaining, "remaining");
            Scribe_Values.Look(ref speed, "speed");
        }
    }

    public sealed class GameComponent_GravityFlights : GameComponent
    {
        private List<GravityFlight> flights = new List<GravityFlight>();
        private readonly Dictionary<Thing, GravityFlight> index = new Dictionary<Thing, GravityFlight>();
        public GameComponent_GravityFlights(Game game) { }
        public static GameComponent_GravityFlights Instance => Current.Game?.GetComponent<GameComponent_GravityFlights>();
        public GravityFlight Get(Thing round) => index.TryGetValue(round, out var flight) ? flight : null;
        public void Add(GravityFlight flight) { flights.Add(flight); index[flight.round] = flight; }
        public void Remove(Thing round)
        {
            if (!index.TryGetValue(round, out var flight)) return;
            flights.Remove(flight); index.Remove(round);
        }
        public override void GameComponentTick()
        {
            for (int i = flights.Count - 1; i >= 0; i--)
                if (flights[i].round == null || !flights[i].round.Spawned || flights[i].round.Destroyed)
                {
                    if (flights[i].round != null) index.Remove(flights[i].round);
                    flights.RemoveAt(i);
                }
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref flights, "gravityFlights", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                flights ??= new List<GravityFlight>(); index.Clear();
                foreach (var flight in flights) if (flight.round != null) index[flight.round] = flight;
            }
        }
    }

    public static class GravityProjectiles
    {
        private static readonly MethodInfo VanillaCollision = AccessTools.Method(typeof(Projectile), "CheckForFreeInterceptBetween");
        private static readonly MethodInfo VanillaImpact = AccessTools.Method(typeof(Projectile), "ImpactSomething");
        private static readonly AccessTools.FieldRef<Projectile, Vector3> VanillaOrigin =
            AccessTools.FieldRefAccess<Projectile, Vector3>("origin");
        private static MethodInfo ceCollision, ceImpact, cePosition;
        private static FieldInfo ceLast, ceOrigin, ceTicks, ceLanded;
        private static readonly List<Trail> trails = new List<Trail>();
        private static Game trailGame;
        private struct Trail { public Map map; public Vector3 from, to; public int until; }

        public static void Install(Harmony harmony)
        {
            var type = AccessTools.TypeByName("CombatExtended.ProjectileCE");
            if (type == null || Rounds.Foreign == null) return;
            ceCollision = AccessTools.Method(type, "CheckForCollisionBetween", Type.EmptyTypes);
            ceImpact = AccessTools.Method(type, "ImpactSomething", Type.EmptyTypes);
            cePosition = AccessTools.PropertySetter(type, "ExactPosition");
            ceLast = AccessTools.Field(type, "LastPos"); ceOrigin = AccessTools.Field(type, "origin");
            ceTicks = AccessTools.Field(type, "intTicksToImpact"); ceLanded = AccessTools.Field(type, "landed");
            if (ceCollision == null || ceImpact == null || cePosition == null || ceLast == null
                || ceOrigin == null || ceTicks == null || ceLanded == null)
            { Log.Error("[RimArt] Gravity Well cannot resolve CE's collision bridge; CE bending is disabled."); return; }
            harmony.Patch(AccessTools.Method(type, "Tick"), prefix:
                new HarmonyMethod(typeof(GravityProjectiles), nameof(CeTick)) { priority = Priority.Last });
        }

        public static bool CeTick(Thing __instance) => BeforeTick(__instance, 1);

        private static GravityCast Field(MapComponent_Gravity component, Vector3 from, Vector3 to)
        {
            GravityCast selected = null;
            float nearest = float.MaxValue;
            foreach (var cast in component.Casts)
            {
                if (!cast.Field || !cast.Valid || !Rounds.SegmentEntersCircle(from, to, cast.Centre,
                    GravityRules.BulletRadius, out var entry)
                    || !GravityMovement.Clear(cast.map, cast.cell, entry.ToIntVec3())) continue;
                float distance = (entry - from).Yto0().sqrMagnitude;
                if (distance < nearest || (distance == nearest && (selected == null || cast.id < selected.id)))
                { selected = cast; nearest = distance; }
            }
            return selected;
        }

        public static bool BeforeTick(Thing round, int delta)
        {
            if (!round.Spawned || round.Destroyed) return true;
            var registry = GameComponent_GravityFlights.Instance;
            var backend = Rounds.For(round);
            if (backend == null || !backend.DirectFlight(round) || round.def.projectile.explosionRadius > 0f) return true;
            if (RecursionRegistry.TryGetCapture(round, out _)) { registry.Remove(round); return true; }
            var component = MapComponent_Gravity.On(round);
            var flight = registry.Get(round);
            // An external redirect takes ownership; do not overwrite its new destination.
            if (flight != null && (backend.Destination(round) - flight.expectedDestination).Yto0().sqrMagnitude > 0.001f)
            { registry.Remove(round); flight = null; }
            if (flight == null)
            {
                if (!component.AnyField || backend.TicksToImpact(round) <= 0) return true;
                Vector3 from = backend.Position(round), heading = backend.Heading(round);
                float speed = backend.CurrentSpeedPerTick(round);
                float remaining = (backend.Destination(round) - from).Yto0().magnitude;
                if (Field(component, from, from + heading * Mathf.Min(remaining, speed * delta)) == null) return true;
                flight = new GravityFlight { round = round, position = from, heading = heading,
                    speed = speed, remaining = remaining, originalOrigin = backend.Origin(round),
                    expectedDestination = backend.Destination(round) };
                registry.Add(flight);
                backend.Bend(round, from, heading, remaining, speed);
                flight.expectedDestination = backend.Destination(round);
            }

            float budget = Mathf.Min(flight.remaining, flight.speed * delta);
            while (budget > 0.00001f && round.Spawned && !round.Destroyed)
            {
                float travel = Mathf.Min(0.2f, budget);
                Vector3 from = flight.position;
                GravityCast cast = Field(component, from, from + flight.heading * travel);
                if (cast != null)
                {
                    Vector3 inward = (cast.Centre - from).Yto0();
                    if (inward.sqrMagnitude > 0.00001f)
                        flight.heading = Vector3.RotateTowards(flight.heading, inward.normalized,
                            GravityRules.BendDegrees(inward.magnitude, travel) * Mathf.Deg2Rad, 0f).normalized;
                }
                Vector3 to = from + flight.heading * travel;
                Vector3 core = default;
                bool absorb = cast != null && Rounds.SegmentEntersCircle(from, to, cast.Centre, GravityRules.Core, out core);
                if (absorb) to = core.WithY(from.y);
                if (!to.ToIntVec3().InBounds(round.Map)) { round.Destroy(); break; }

                flight.position = to;
                if (Sweep(round, backend, flight, from, to)) { registry.Remove(round); return false; }
                AddTrail(round.Map, from, to);
                if (absorb) { round.Destroy(); break; }
                flight.remaining = Mathf.Max(0f, flight.remaining - travel);
                budget -= travel;
                if (flight.remaining <= 0.00001f)
                {
                    if (round is Projectile) VanillaImpact.Invoke(round, null);
                    else ceImpact.Invoke(round, null);
                    registry.Remove(round); return false;
                }
            }
            if (!round.Spawned || round.Destroyed) { registry.Remove(round); return false; }
            // Leave native flight metadata valid for saves and other kits that subsequently
            // capture or redirect this round. The saved distance budget never grows on a turn.
            backend.Bend(round, flight.position, flight.heading, flight.remaining, flight.speed);
            flight.expectedDestination = backend.Destination(round);
            backend.MaintainSound(round);
            return false;
        }

        private static bool Sweep(Thing round, RoundBackend backend, GravityFlight flight, Vector3 from, Vector3 to)
        {
            round.Position = to.ToIntVec3();
            if (round is Projectile vanilla)
            {
                vanilla.HitFlags = ProjectileHitFlags.All;
                Vector3 origin = VanillaOrigin(vanilla);
                // Preserve native distance-dependent interception after repeated bending.
                VanillaOrigin(vanilla) = flight.originalOrigin;
                try { return (bool)VanillaCollision.Invoke(round, new object[] { from, to }); }
                finally { VanillaOrigin(vanilla) = origin; }
            }
            cePosition.Invoke(round, new object[] { to });
            ceLast.SetValue(round, from);
            ceTicks.SetValue(round, Mathf.Max(1, backend.TicksToImpact(round)));
            object savedOrigin = ceOrigin.GetValue(round);
            ceOrigin.SetValue(round, new Vector2(flight.originalOrigin.x, flight.originalOrigin.z));
            try { return (bool)ceCollision.Invoke(round, null) || (bool)ceLanded.GetValue(round); }
            finally { ceOrigin.SetValue(round, savedOrigin); }
        }

        private static void AddTrail(Map map, Vector3 from, Vector3 to)
        {
            if (trailGame != Current.Game) { trails.Clear(); trailGame = Current.Game; }
            if (trails.Count >= 4096) trails.RemoveRange(0, 512);
            trails.Add(new Trail { map = map, from = from, to = to, until = Find.TickManager.TicksGame + 8 });
        }
        public static void Draw(Map map)
        {
            int now = Find.TickManager.TicksGame;
            trails.RemoveAll(t => t.until <= now || t.map == null);
            foreach (var trail in trails)
                if (trail.map == map && !trail.to.ToIntVec3().Fogged(map))
                    GenDraw.DrawLineBetween(trail.from.WithY(AltitudeLayer.MoteOverhead.AltitudeFor()),
                        trail.to.WithY(AltitudeLayer.MoteOverhead.AltitudeFor()), GravityGraphics.TrailMaterial, 0.035f);
        }
    }

    [HarmonyPatch(typeof(Projectile), "TickInterval")]
    public static class Patch_GravityProjectileTick
    {
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(Projectile __instance, int delta) => GravityProjectiles.BeforeTick(__instance, delta);
    }
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.ExactPosition), MethodType.Getter)]
    public static class Patch_GravityProjectilePosition
    {
        public static void Postfix(Projectile __instance, ref Vector3 __result)
        {
            var flight = GameComponent_GravityFlights.Instance?.Get(__instance);
            if (flight != null && !RecursionRegistry.TryGetCapture(__instance, out _)
                && (Rounds.Vanilla.Destination(__instance) - flight.expectedDestination).Yto0().sqrMagnitude <= 0.001f)
                __result = flight.position;
        }
    }
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.UpdateRateTicks), MethodType.Getter)]
    public static class Patch_GravityProjectileRate
    {
        public static void Postfix(Projectile __instance, ref int __result)
        {
            if (GameComponent_GravityFlights.Instance?.Get(__instance) != null
                || MapComponent_Gravity.On(__instance)?.AnyField == true) __result = 1;
        }
    }
}
