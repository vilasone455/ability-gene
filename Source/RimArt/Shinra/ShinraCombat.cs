using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public static class ShinraCombat
    {
        public static void Push(ShinraPawnState s)
        {
            foreach (Pawn pawn in s.map.mapPawns.AllPawnsSpawned.ToArray())
            {
                if (pawn == s.pawn || pawn.Dead) continue;
                Vector3 start = pawn.Position.ToVector3Shifted();
                Vector3 direction = start - s.centre;
                direction.y = 0f;
                if (direction.magnitude > ShinraCharge.Radius
                    || !GenSight.LineOfSight(s.centre.ToIntVec3(), pawn.Position, s.map)) continue;
                if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
                direction.Normalize();
                float distance = s.charge.Push / Mathf.Max(1f, pawn.BodySize);
                IntVec3 last = pawn.Position;
                bool collision = false;
                // Sub-cell steps visit every crossed cell, including diagonal corner blockers.
                for (float travel = 0.1f; travel <= distance + 0.001f; travel += 0.1f)
                {
                    IntVec3 cell = (start + direction * travel).ToIntVec3();
                    if (!cell.InBounds(s.map)) break;
                    if (!cell.Walkable(s.map)
                        || !new IntVec3(cell.x, 0, last.z).Walkable(s.map)
                        || !new IntVec3(last.x, 0, cell.z).Walkable(s.map))
                    { collision = true; break; }
                    last = cell;
                }
                pawn.pather?.StopDead();
                pawn.Position = last;
                pawn.Notify_Teleported();
                pawn.stances.stagger.StaggerFor(30);
                if (collision) pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, s.charge.CollisionDamage,
                    0f, -1f, s.pawn));
            }
        }

        private static bool Reflectable(Thing round, RoundBackend backend, ShinraPawnState s) =>
            backend.DirectFlight(round) && backend.DirectDamage(round) <= s.charge.ProjectileLimit
            && (round.def.projectile.explosionRadius <= 0f || s.charge.Power >= 1f);

        public static bool Threatened(ShinraPawnState s, float seconds)
        {
            foreach (Thing round in s.map.listerThings.AllThings)
            {
                var backend = Rounds.For(round);
                if (backend == null || !Reflectable(round, backend, s)) continue;
                Vector3 from = backend.Position(round); from.y = 0f;
                Vector3 heading = backend.Heading(round);
                Vector3 offset = s.centre - from;
                float along = Vector3.Dot(offset, heading);
                float reach = Mathf.Min(backend.CurrentSpeedPerTick(round) * seconds * 60f,
                    (backend.Destination(round).Yto0() - from).magnitude);
                if (along < 0f || along > reach + 0.5f) continue;
                if ((offset - heading * along).sqrMagnitude <= 0.5f * 0.5f
                    && GenSight.LineOfSight(from.ToIntVec3(), s.pawn.Position, s.map)) return true;
            }
            return false;
        }

        // Prefix, not map polling: even a round crossing the entire field in one tick is
        // redirected before the engine gets an opportunity to resolve its impact.
        public static bool BeforeProjectileTick(Thing round, int delta)
        {
            if (!round.Spawned || round.Destroyed) return true;
            if (RecursionRegistry.TryGetCapture(round, out _)) return true;
            var backend = Rounds.For(round);
            if (backend == null || backend.TicksToImpact(round) <= 0) return true;
            Vector3 from = backend.Position(round); from.y = 0f;
            Vector3 heading = backend.Heading(round);
            float travel = Mathf.Min(backend.CurrentSpeedPerTick(round) * delta,
                (backend.Destination(round).Yto0() - from).magnitude);
            Vector3 to = from + heading * travel;
            ShinraPawnState chosen = null;
            Vector3 hit = default;
            float nearest = float.MaxValue;
            foreach (var s in GameComponent_Shinra.Instance.States)
            {
                if (s.map != round.Map || !s.Protected || s.redirected.Contains(round)
                    || !Reflectable(round, backend, s)) continue;
                Vector3 offset = from - s.centre;
                if (Vector3.Dot(offset, heading) >= 0f) continue; // Outgoing fire passes.
                if (!Rounds.SegmentEntersCircle(from, to, s.centre, ShinraCharge.Radius, out var entry)
                    || !GenSight.LineOfSight(s.centre.ToIntVec3(), entry.ToIntVec3(), s.map)) continue;
                float distance = (entry - from).sqrMagnitude;
                if (distance < nearest) { chosen = s; hit = entry; nearest = distance; }
            }
            if (chosen == null) return true;
            Vector3 outward = hit - chosen.centre;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f) outward = -heading;
            backend.Repel(round, hit, outward.normalized, chosen.pawn);
            chosen.redirected.Add(round);
            backend.MaintainSound(round);
            return false;
        }
    }

    [HarmonyPatch(typeof(Projectile), "TickInterval")]
    public static class Patch_ShinraProjectileTick
    {
        public static bool Prefix(Projectile __instance, int delta) => ShinraCombat.BeforeProjectileTick(__instance, delta);
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.UpdateRateTicks), MethodType.Getter)]
    public static class Patch_ShinraProjectileRate
    {
        public static void Postfix(Projectile __instance, ref int __result)
        {
            if (__instance.Spawned && GameComponent_Shinra.Instance.States.Any(s => s.map == __instance.Map
                && (s.active || s.Protected))) __result = 1;
        }
    }
}
