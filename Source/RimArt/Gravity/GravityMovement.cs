using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    public sealed class GravityMotion : IExposable
    {
        public Thing thing;
        public Vector3 position;
        public IntVec3 cell;
        public Vector3 walking;
        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref position, "position");
            Scribe_Values.Look(ref cell, "cell");
        }
    }

    public static class GravityMovement
    {
        private static readonly AccessTools.FieldRef<Pawn_PathFollower, PathEndMode> EndMode =
            AccessTools.FieldRefAccess<Pawn_PathFollower, PathEndMode>("peMode");

        public static bool Open(Map map, IntVec3 cell) => cell.InBounds(map) && cell.Walkable(map)
            && (!(cell.GetDoor(map) is Building_Door door) || door.Open);

        // Shared by targeting, influence, damage and displacement, including diagonal corners.
        public static bool Clear(Map map, IntVec3 from, IntVec3 to)
        {
            if (!Open(map, from) || !Open(map, to)) return false;
            Vector3 start = from.ToVector3Shifted(), delta = to.ToVector3Shifted() - start;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude * 10f));
            IntVec3 previous = from;
            for (int i = 1; i <= steps; i++)
            {
                IntVec3 cell = (start + delta * (i / (float)steps)).ToIntVec3();
                if (!Crossable(map, previous, cell)) return false;
                previous = cell;
            }
            return true;
        }

        private static bool Crossable(Map map, IntVec3 from, IntVec3 to) => Open(map, to)
            && Open(map, new IntVec3(from.x, 0, to.z)) && Open(map, new IntVec3(to.x, 0, from.z));

        public static void Step(MapComponent_Gravity component, GravityMotion motion, Vector3 walking)
        {
            Thing thing = motion.thing;
            if (!thing.Spawned) return;
            var cast = component.Owner(thing, motion.position);
            if (cast == null) return;
            Vector3 offset = (cast.Centre - motion.position).Yto0();
            float distance = offset.magnitude;
            Vector3 pull = distance < 0.0001f ? Vector3.zero : offset.normalized *
                Mathf.Min(distance, GravityRules.Pull(distance, MapComponent_Gravity.Resistance(thing)) / 60f);
            Vector3 delta = walking + pull;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude * 10f));
            Vector3 next = motion.position;
            for (int i = 0; i < steps; i++)
            {
                Vector3 candidate = next + delta / steps;
                if (!Crossable(thing.Map, next.ToIntVec3(), candidate.ToIntVec3())) break;
                next = candidate;
            }
            IntVec3 newCell = next.ToIntVec3();
            if (newCell != thing.Position)
            {
                if (thing is Pawn pawn)
                {
                    bool moving = pawn.pather.Moving;
                    LocalTargetInfo destination = pawn.pather.Destination;
                    PathEndMode mode = EndMode(pawn.pather);
                    pawn.Position = newCell;
                    // Rebuild the path from the displaced cell, keeping the current job and
                    // destination. Never StopAll: aiming and reloading continue normally.
                    pawn.pather.StopDead();
                    if (moving && destination.IsValid && !destination.ThingDestroyed)
                        pawn.pather.StartPath(destination, mode);
                }
                else
                {
                    // Thing.Position updates grid, region, cover and section meshes. Keep the
                    // instance spawned: despawning would release hauling reservations.
                    thing.Position = newCell;
                }
            }
            motion.position = next;
            motion.cell = thing.Position;
        }

        public static void Release(GravityMotion motion)
        {
            if (motion.thing is Pawn pawn && pawn.Spawned) pawn.Drawer.tweener.ResetTweenedPosToRoot();
            else if (motion.thing?.Spawned == true)
                motion.thing.Map.mapDrawer.MapMeshDirty(motion.thing.Position, MapMeshFlagDefOf.Things);
        }
    }

    [HarmonyPatch(typeof(SectionLayer_ThingsGeneral), "TakePrintFrom")]
    public static class Patch_GravityItemPrint
    {
        public static bool Prefix(Thing t) => t is Pawn || MapComponent_Gravity.On(t)?.DrawPosition(t, out _) != true;
    }

    // Let the native pather choose paths and honor busy stances/terrain/urgency. Redirect only
    // the locomotion budget into our integrator so two controllers cannot move the same pawn.
    [HarmonyPatch(typeof(Pawn_PathFollower), "PatherTick")]
    public static class Patch_GravityPather
    {
        public static void Prefix(Pawn ___pawn)
        {
            var motion = MapComponent_Gravity.On(___pawn)?.MotionFor(___pawn);
            if (motion != null) motion.walking = Vector3.zero;
        }
        public static void Postfix(Pawn ___pawn)
        {
            var component = MapComponent_Gravity.On(___pawn);
            var motion = component?.MotionFor(___pawn);
            if (motion != null) GravityMovement.Step(component, motion, motion.walking);
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToPayThisTick")]
    public static class Patch_GravityWalkBudget
    {
        public static void Postfix(Pawn_PathFollower __instance, Pawn ___pawn, ref float __result)
        {
            var motion = MapComponent_Gravity.On(___pawn)?.MotionFor(___pawn);
            if (motion == null) return;
            Vector3 direction = (__instance.nextCell.ToVector3Shifted() - ___pawn.Position.ToVector3Shifted()).Yto0();
            if (__instance.Moving && direction.sqrMagnitude > 0f)
                motion.walking = direction * (__result / Mathf.Max(1f, __instance.nextCellCostTotal));
            __result = 0f;
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class Patch_GravityCellEntry
    {
        public static bool Prefix(Pawn_PathFollower __instance, Pawn ___pawn)
        {
            if (__instance.nextCell == ___pawn.Position || MapComponent_Gravity.On(___pawn)?.MotionFor(___pawn) == null)
                return true; // Initial native path setup selects its first real next cell.
            __instance.nextCellCostLeft = Mathf.Max(1f, __instance.nextCellCostLeft);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "WillCollideWithPawnAt")]
    public static class Patch_GravityCrowding
    {
        public static void Postfix(Pawn ___pawn, IntVec3 c, ref bool __result)
        {
            // The native "unstuck" branch teleports overlapping pawns to a nearby free cell.
            // Gravity deliberately gathers bodies together; keep their current-cell overlap
            // while ordinary voluntary movement still respects its next-cell blockers.
            if (__result && c == ___pawn.Position && MapComponent_Gravity.On(___pawn)?.MotionFor(___pawn) != null)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_GravityPawnDraw
    {
        public static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (MapComponent_Gravity.On(___pawn)?.DrawPosition(___pawn, out var position) == true)
                __result = position.WithY(__result.y);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DrawPos), MethodType.Getter)]
    public static class Patch_GravityItemDraw
    {
        public static void Postfix(Thing __instance, ref Vector3 __result)
        {
            if (!(__instance is Pawn) && MapComponent_Gravity.On(__instance)?.DrawPosition(__instance, out var position) == true)
                __result = position.WithY(__result.y);
        }
    }
}
