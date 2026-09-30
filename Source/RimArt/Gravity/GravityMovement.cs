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
        // Exact position; for an item it runs ahead of Thing.Position between commits.
        public Vector3 position;
        // Where the thing was when a well first took it (Blue's drag marks). Not saved: a loaded motion
        // starts from where it is.
        public Vector3 start;
        // Thing.Position as last committed or seen; a different Thing.Position means something else moved it.
        public IntVec3 cell;
        public Vector3 walking;
        public int repathTick = -9999;
        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref position, "position");
            Scribe_Values.Look(ref cell, "cell");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) start = position;
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
                if (cell == previous) continue;
                if (!Crossable(map, previous, cell)) return false;
                previous = cell;
            }
            return true;
        }

        public static bool Crossable(Map map, IntVec3 from, IntVec3 to) => Open(map, to)
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
                Mathf.Min(distance, cast.Props.Pull(distance, cast.Props.Resistance(thing), cast.Radius) / 60f);
            Vector3 delta = walking + pull;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude * 10f));
            Vector3 next = motion.position;
            for (int i = 0; i < steps; i++)
            {
                Vector3 candidate = next + delta / steps;
                IntVec3 from = next.ToIntVec3(), to = candidate.ToIntVec3();
                if (from != to && !Crossable(thing.Map, from, to)) break;
                next = candidate;
            }
            IntVec3 newCell = next.ToIntVec3();
            motion.position = next;
            if (thing is Pawn pawn)
            {
                if (newCell != pawn.Position) Displace(pawn, newCell);
                Keep(pawn, motion);
            }
            else if (newCell != thing.Position
                && (Find.TickManager.TicksGame + thing.thingIDNumber) % GravityRules.ItemCommitTicks == 0)
            {
                // Thing.Position updates grid, region, cover and the section mesh. Keep the instance
                // spawned: despawning would release hauling reservations.
                thing.Position = newCell;
            }
            motion.cell = thing.Position;
        }

        // Moves a pawn to the cell the well dragged it into, keeping its job and destination.
        private static void Displace(Pawn pawn, IntVec3 newCell)
        {
            var pather = pawn.pather;
            IntVec3 next = pather.nextCell;
            pawn.Position = newCell;
            // Standing pawns: point the pather at the new cell. Never StopAll: aiming and
            // reloading continue normally.
            if (!pather.Moving) { pather.StopDead(); return; }
            // Reached its next path cell: let the native pather take the next node on its next tick.
            if (next == newCell) pather.nextCellCostLeft = 0f;
        }

        // A moving pawn whose next path cell is not one step away from where the well put it gets a
        // new path from its cell, at most every RepathTicks. A pawn still one step from its next
        // cell keeps its path and walks on.
        private static void Keep(Pawn pawn, GravityMotion motion)
        {
            var pather = pawn.pather;
            if (!pather.Moving || pather.curPath == null) return;
            IntVec3 next = pather.nextCell, at = pawn.Position;
            if (next == at || (next.AdjacentTo8Way(at) && Crossable(pawn.Map, at, next))) return;
            int now = Find.TickManager.TicksGame;
            if (now - motion.repathTick < GravityRules.RepathTicks) return;
            motion.repathTick = now;
            LocalTargetInfo destination = pather.Destination;
            PathEndMode mode = EndMode(pather);
            pather.StopDead();
            if (destination.IsValid && !destination.ThingDestroyed) pather.StartPath(destination, mode);
        }

        public static void Release(GravityMotion motion)
        {
            Thing thing = motion.thing;
            if (thing is Pawn pawn) { if (pawn.Spawned) pawn.Drawer.tweener.ResetTweenedPosToRoot(); return; }
            if (thing?.Spawned != true) return;
            // An item leaves the well where it was drawn.
            IntVec3 cell = motion.position.ToIntVec3();
            if (cell != thing.Position && Open(thing.Map, cell)) thing.Position = cell;
            else thing.Map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlagDefOf.Things);
        }
    }

    [HarmonyPatch(typeof(SectionLayer_ThingsGeneral), "TakePrintFrom")]
    public static class Patch_GravityItemPrint
    {
        public static bool Prefix(Thing t) => t is Pawn || MapComponent_Gravity.Live(t)?.DrawPosition(t, out _) != true;
    }

    // Let the native pather choose paths and honor busy stances/terrain/urgency. Redirect only
    // the locomotion budget into our integrator so two controllers cannot move the same pawn.
    [HarmonyPatch(typeof(Pawn_PathFollower), "PatherTick")]
    public static class Patch_GravityPather
    {
        public static void Prefix(Pawn ___pawn)
        {
            var motion = MapComponent_Gravity.Live(___pawn)?.MotionFor(___pawn);
            if (motion != null) motion.walking = Vector3.zero;
        }
        public static void Postfix(Pawn ___pawn)
        {
            var component = MapComponent_Gravity.Live(___pawn);
            var motion = component?.MotionFor(___pawn);
            if (motion != null) GravityMovement.Step(component, motion, motion.walking);
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToPayThisTick")]
    public static class Patch_GravityWalkBudget
    {
        public static void Postfix(Pawn_PathFollower __instance, Pawn ___pawn, ref float __result)
        {
            var motion = MapComponent_Gravity.Live(___pawn)?.MotionFor(___pawn);
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
            if (__instance.nextCell == ___pawn.Position || MapComponent_Gravity.Live(___pawn)?.MotionFor(___pawn) == null)
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
            if (__result && c == ___pawn.Position && MapComponent_Gravity.Live(___pawn)?.MotionFor(___pawn) != null)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_GravityPawnDraw
    {
        public static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (MapComponent_Gravity.Live(___pawn)?.DrawPosition(___pawn, out var position) == true)
                __result = position.WithY(__result.y);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DrawPos), MethodType.Getter)]
    public static class Patch_GravityItemDraw
    {
        public static void Postfix(Thing __instance, ref Vector3 __result)
        {
            if (!(__instance is Pawn) && MapComponent_Gravity.Live(__instance)?.DrawPosition(__instance, out var position) == true)
                __result = position.WithY(__result.y);
        }
    }
}
