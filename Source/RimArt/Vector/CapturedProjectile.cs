using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One round as it stood when the editor opened, and the arithmetic that turns a rotation
    /// and a force into the flight it will have when the editor closes.
    ///
    /// The snapshot is frozen at capture and never re-read. Everything the player is shown -
    /// the marker, the original heading, the preview line - is drawn from these fields, so the
    /// picture cannot drift while the game is paused, and a round destroyed mid-session is a
    /// member that fails revalidation rather than a null dereference in the middle of drawing.
    ///
    /// Writing the new flight is done by hand rather than by calling Launch. Launch scatters the
    /// destination by up to a third of a cell and re-reads the origin from the projectile's
    /// current cell; both are wrong here, because the whole promise of the editor is that the
    /// line drawn on the paused map is the line the round takes.
    /// </summary>
    public class CapturedProjectile
    {
        private static readonly AccessTools.FieldRef<Projectile, Vector3> OriginRef =
            AccessTools.FieldRefAccess<Projectile, Vector3>("origin");
        private static readonly AccessTools.FieldRef<Projectile, Vector3> DestinationRef =
            AccessTools.FieldRefAccess<Projectile, Vector3>("destination");
        private static readonly AccessTools.FieldRef<Projectile, int> TicksToImpactRef =
            AccessTools.FieldRefAccess<Projectile, int>("ticksToImpact");
        private static readonly AccessTools.FieldRef<Projectile, int> LifetimeRef =
            AccessTools.FieldRefAccess<Projectile, int>("lifetime");
        private static readonly AccessTools.FieldRef<Projectile, Thing> LauncherRef =
            AccessTools.FieldRefAccess<Projectile, Thing>("launcher");
        private static readonly AccessTools.FieldRef<Projectile, bool> LandedRef =
            AccessTools.FieldRefAccess<Projectile, bool>("landed");
        private static readonly AccessTools.FieldRef<Projectile, bool> PreventFriendlyFireRef =
            AccessTools.FieldRefAccess<Projectile, bool>("preventFriendlyFire");

        public readonly Projectile Projectile;

        /// <summary>Where the round was when it was caught. The new flight starts here.</summary>
        public readonly Vector3 Position;

        /// <summary>Unit heading at capture. Rotation is applied to this, never to a running total.</summary>
        public readonly Vector3 Heading;

        /// <summary>Cells per tick the round's own def says it travels, before force.</summary>
        public readonly float BaseSpeed;

        /// <summary>Which group owns it, or -1 while it is still unassigned.</summary>
        public int Group = -1;

        public CapturedProjectile(Projectile projectile)
        {
            Projectile = projectile;

            Vector3 position = projectile.ExactPosition;
            position.y = 0f;
            Position = position;

            Vector3 heading = DestinationRef(projectile) - OriginRef(projectile);
            heading.y = 0f;
            Heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;

            float speed = projectile.def.projectile != null
                ? projectile.def.projectile.SpeedTilesPerTick
                : 1f;
            BaseSpeed = speed > 0f ? speed : 1f;
        }

        public bool StillValid(Map map)
        {
            return Projectile != null && !Projectile.Destroyed && Projectile.Spawned
                   && Projectile.Map == map;
        }

        /// <summary>Heading after a group's rotation, which turns the whole group as one piece.</summary>
        public Vector3 HeadingAfter(float rotationDegrees)
        {
            if (Mathf.Abs(rotationDegrees) < 0.01f) return Heading;
            return Quaternion.AngleAxis(rotationDegrees, Vector3.up) * Heading;
        }

        /// <summary>
        /// Where the round ends up: twenty cells times force, along the rotated heading, from
        /// where it was caught - clipped where that line leaves the map, because a round that
        /// crosses the edge is destroyed there and the preview must say so.
        /// </summary>
        public Vector3 EndpointFor(float rotationDegrees, float force, Map map)
        {
            Vector3 heading = HeadingAfter(rotationDegrees);
            float range = VectorEditDefaults.BaseRangeCells * force;
            return Position + heading * ClipToMap(Position, heading, range, map);
        }

        /// <summary>Ticks the new flight takes, which is also what StartingTicksToImpact reports.</summary>
        public int TicksFor(Vector3 endpoint, float force)
        {
            Vector3 travel = endpoint - Position;
            travel.y = 0f;
            int ticks = Mathf.CeilToInt(travel.magnitude / (BaseSpeed * force));
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>
        /// Whether committing this setting would actually change the round's flight. A group
        /// left at no rotation and ×1 force still counts as an edit whenever the recalculated
        /// range differs from what the round has left, which is the usual case - renewing a
        /// spent round's travel is a real effect and has to be paid for.
        /// </summary>
        public bool WouldChange(float rotationDegrees, float force, Map map)
        {
            Vector3 endpoint = EndpointFor(rotationDegrees, force, map);
            if ((endpoint - DestinationRef(Projectile)).sqrMagnitude > 0.01f) return true;
            if ((Position - OriginRef(Projectile)).sqrMagnitude > 0.01f) return true;
            if (TicksFor(endpoint, force) != TicksToImpactRef(Projectile)) return true;
            return !Mathf.Approximately(force, VectorEditRegistry.ForceFor(Projectile));
        }

        /// <summary>
        /// Rewrites the round's flight in place. The instance, its def, its equipment and
        /// anything its own Impact does - a blade planting itself, an arrow's extra damage -
        /// are all untouched; only where it starts, where it is going, how long it takes and
        /// who is answerable for it change.
        /// </summary>
        public void Commit(float rotationDegrees, float force, Pawn caster, Map map)
        {
            Vector3 endpoint = EndpointFor(rotationDegrees, force, map);
            int ticks = TicksFor(endpoint, force);

            OriginRef(Projectile) = Position;
            DestinationRef(Projectile) = endpoint;
            TicksToImpactRef(Projectile) = ticks;
            LifetimeRef(Projectile) = ticks;
            LandedRef(Projectile) = false;
            LauncherRef(Projectile) = caster;
            PreventFriendlyFireRef(Projectile) = false;

            LocalTargetInfo target = new LocalTargetInfo(endpoint.ToIntVec3());
            Projectile.usedTarget = target;
            Projectile.intendedTarget = target;

            VectorEditRegistry.Register(Projectile, force);
        }

        /// <summary>
        /// How far along a heading a round can travel before it leaves the map, capped at the
        /// range it was given. Slab clip rather than a stepping loop: the answer has to be exact
        /// enough that the drawn endpoint and the committed tick count agree.
        /// </summary>
        private static float ClipToMap(Vector3 start, Vector3 heading, float range, Map map)
        {
            if (map == null) return range;

            float limit = range;
            IntVec3 size = map.Size;
            limit = ClipAxis(start.x, heading.x, size.x, limit);
            limit = ClipAxis(start.z, heading.z, size.z, limit);
            return limit < 0f ? 0f : limit;
        }

        private static float ClipAxis(float start, float direction, int size, float limit)
        {
            // Half a cell inside the edge: the destination is truncated to a cell, and a round
            // aimed exactly at the boundary would otherwise round outward and be destroyed on
            // the first tick rather than at the end of the drawn line.
            const float Inset = 0.5f;
            if (Mathf.Abs(direction) < 0.0001f) return limit;

            float edge = direction > 0f ? size - Inset : Inset;
            float distance = (edge - start) / direction;
            return distance < limit ? distance : limit;
        }
    }
}
