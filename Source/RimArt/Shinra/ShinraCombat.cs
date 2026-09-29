using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public static class ShinraCombat
    {
        /// <summary>
        /// Moves every pawn in reach at once. Returns how each moved pawn is drawn getting there (<see cref="ShinraFlight"/>):
        /// the game moves it now, the picture flies it once the dome's front reaches it.
        /// </summary>
        public static List<ShinraFlight> Push(ShinraPawnState s)
        {
            var flights = new List<ShinraFlight>();
            foreach (Pawn pawn in s.map.mapPawns.AllPawnsSpawned.ToArray())
            {
                if (pawn == s.pawn || pawn.Dead) continue;
                // A pawn pinned by Black Receiver or carried by Banshō Ten'in is not moved; a pinned one's rods flare.
                if (PainKit.Unmovable(pawn))
                {
                    if ((pawn.Position.ToVector3Shifted() - s.centre).Yto0().magnitude <= s.Radius) PainRods.Of(pawn)?.Flared();
                    continue;
                }
                Vector3 start = pawn.Position.ToVector3Shifted();
                Vector3 direction = start - s.centre;
                direction.y = 0f;
                if (direction.magnitude > s.Radius
                    || !GenSight.LineOfSight(s.centre.ToIntVec3(), pawn.Position, s.map)) continue;
                float fromPain = direction.magnitude;
                if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
                direction.Normalize();
                float distance = s.PushCells / Mathf.Max(1f, pawn.BodySize);
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
                if (last != pawn.Position) flights.Add(new ShinraFlight(pawn, pawn.DrawPos, last, fromPain, s.Radius, collision));
                pawn.Position = last;
                pawn.Notify_Teleported();
                pawn.stances.stagger.StaggerFor(Mathf.RoundToInt(ShinraTuning.Get.staggerSeconds * 60f));
                if (collision) pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, s.WallDamage,
                    0f, -1f, s.pawn));
            }
            return flights;
        }

        private static bool Reflectable(Thing round, RoundBackend backend, ShinraPawnState s) =>
            backend.DirectFlight(round) && backend.DirectDamage(round) <= s.ShotLimit
            && (round.def.projectile.explosionRadius <= 0f || s.TurnsExplosives);

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
                if (!Rounds.SegmentEntersCircle(from, to, s.centre, s.Radius, out var entry)
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
