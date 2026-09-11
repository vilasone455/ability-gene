using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// A round in flight, whichever engine is flying it.
    ///
    /// Three kits in this mod take hold of rounds that are already in the air - vector
    /// manipulation redirects them, the halving membrane parks them on a curve, and the involute
    /// looses one inside its own volume - and all three were written against Verse.Projectile.
    /// Combat Extended's rounds are not Verse.Projectile. CombatExtended.ProjectileCE derives
    /// straight from ThingWithComps, so every `as Projectile` in this mod is null for anything a
    /// CE weapon fired, and every Harmony patch typed to Projectile never runs. The three kits
    /// did not misbehave under CE so much as silently stop seeing the fight.
    ///
    /// What they all actually need of a round is small and the same: where it is, where it was
    /// last tick, which way and how fast it is going, who fired it, and the ability to write a
    /// new flight over the old one. That is this class. A backend answers those for one engine's
    /// rounds; <see cref="For"/> picks the backend, and the kits above never mention either
    /// engine again.
    ///
    /// The dispatch is a type test rather than a virtual call on the round because neither
    /// engine's projectile has anywhere to put one, and because the vanilla case has to stay
    /// cheap - it is reached from the scan that runs every frame the gizmo is on screen.
    /// </summary>
    public static class Rounds
    {
        /// <summary>Verse.Projectile and everything deriving from it, which is most of the game.</summary>
        public static readonly RoundBackend Vanilla = new VanillaRounds();

        /// <summary>
        /// Combat Extended's rounds, or null when CE is not loaded - which is the normal case and
        /// the one every call here is written to be correct in. Installed once at startup by
        /// <see cref="CombatExtendedRounds.Install"/>.
        /// </summary>
        public static RoundBackend Foreign;

        /// <summary>The backend that owns this thing, or null if it is not a round at all.</summary>
        public static RoundBackend For(Thing thing)
        {
            if (thing == null) return null;
            if (thing is Projectile) return Vanilla;
            if (Foreign != null && Foreign.Owns(thing)) return Foreign;
            return null;
        }

        public static bool Is(Thing thing)
        {
            return For(thing) != null;
        }

        /// <summary>
        /// The speed the round's def says it travels, in cells per tick, before anything this
        /// mod has done to it.
        ///
        /// Read from the def rather than from the instance on purpose, and this is the whole
        /// reason force can be absolute rather than cumulative: a round already edited to twice
        /// its speed still reports its def's figure here, so editing it again to twice is twice,
        /// not four times. CE keeps its own per-instance speed in shotSpeed and it is deliberately
        /// not what this answers.
        /// </summary>
        /// <summary>
        /// Where a round crossed into a circle on the ground plane, if it did, given the two ends
        /// of the step it took this tick.
        ///
        /// A field has to be tested against the segment rather than against the round's sampled
        /// position, and the difference is the whole ballgame once the rounds are fast. A
        /// vanilla rifle round moves 1.2 cells a tick and is sampled six or seven times crossing
        /// a four-cell field; a Combat Extended round at 240 cells a second moves four, and the
        /// fastest CE rounds move sixteen - straight through a field that never saw them. CE
        /// solves the same problem the same way for its own collision checks, which is the tell.
        ///
        /// A round already inside the circle at the start of its step enters at the start of it.
        /// </summary>
        public static bool SegmentEntersCircle(Vector3 from, Vector3 to, Vector3 centre,
            float radius, out Vector3 entry)
        {
            entry = from;

            Vector3 step = to - from;
            step.y = 0f;
            Vector3 offset = from - centre;
            offset.y = 0f;

            float inside = offset.sqrMagnitude - radius * radius;

            // A round that was already inside when the step began is caught where it began.
            if (inside <= 0f) return true;

            float a = step.sqrMagnitude;
            if (a < 0.000001f) return false;

            float b = 2f * Vector3.Dot(offset, step);
            float discriminant = b * b - 4f * a * inside;
            if (discriminant < 0f) return false;

            float t = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            if (t < 0f || t > 1f) return false;

            entry = from + step * t;
            return true;
        }

        public static float BaseSpeedPerTick(Thing thing)
        {
            if (thing == null || thing.def == null || thing.def.projectile == null) return 1f;

            float speed = thing.def.projectile.SpeedTilesPerTick;
            return speed > 0f ? speed : 1f;
        }
    }

    /// <summary>
    /// Everything this mod does to a round in flight, for one engine's rounds.
    ///
    /// Deliberately narrow. Anything that can be answered the same way for both engines - the
    /// def's speed, whether a round is arced or explosive, whether it is spawned - is asked of
    /// the Thing directly by the caller and is not here.
    /// </summary>
    public abstract class RoundBackend
    {
        /// <summary>Whether this backend is the one that knows how to fly this thing.</summary>
        public abstract bool Owns(Thing thing);

        /// <summary>Where the round is now, flattened to the ground plane by the caller if needed.</summary>
        public abstract Vector3 Position(Thing thing);

        /// <summary>
        /// Where the round was one tick ago. The segment between the two is what a field has to
        /// test against, because a fast round is never sampled inside a small one.
        /// </summary>
        public abstract Vector3 LastPosition(Thing thing);

        public abstract Vector3 Origin(Thing thing);
        public abstract Vector3 Destination(Thing thing);

        /// <summary>Unit heading in the ground plane.</summary>
        public abstract Vector3 Heading(Thing thing);

        /// <summary>How fast the round is actually going right now, in cells per tick.</summary>
        public abstract float CurrentSpeedPerTick(Thing thing);

        public abstract int TicksToImpact(Thing thing);

        public abstract Thing Launcher(Thing thing);

        /// <summary>
        /// Writes a new flight over the old one: from <paramref name="origin"/>, to
        /// <paramref name="endpoint"/>, arriving in <paramref name="ticks"/>, at
        /// <paramref name="force"/> times the round's own baseline speed and damage, answerable
        /// to <paramref name="launcher"/>.
        ///
        /// The instance, its def, its equipment and whatever its own impact does are untouched.
        /// </summary>
        public abstract void Redirect(Thing thing, Vector3 origin, Vector3 endpoint, int ticks,
            float force, Thing launcher);

        /// <summary>
        /// Puts a round that has been taken off the engine's clock at a given point.
        ///
        /// Vanilla needs nothing here - a held round's position is reported by the postfix on
        /// Projectile.ExactPosition and the engine never asks again. CE has no such single
        /// reading point, so its rounds are moved rather than reported.
        /// </summary>
        public abstract void PlaceHeld(Thing thing, Vector3 at);

        /// <summary>Hands a held round back to the engine from wherever it was let go.</summary>
        public abstract void Resume(Thing thing, Vector3 from);

        /// <summary>
        /// Ambient sound is normally maintained from the round's own tick, which is skipped while
        /// it is held. Rockets and the like would otherwise log a stale sustainer.
        /// </summary>
        public abstract void MaintainSound(Thing thing);
    }

    /// <summary>
    /// The engine's own rounds. Every field here is reached by name through a Harmony field ref
    /// for the same reason it always was: they are protected, and the alternative is a subclass
    /// this mod has no way to make the game use.
    /// </summary>
    public sealed class VanillaRounds : RoundBackend
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
        private static readonly AccessTools.FieldRef<Projectile, Sustainer> AmbientSustainerRef =
            AccessTools.FieldRefAccess<Projectile, Sustainer>("ambientSustainer");

        public override bool Owns(Thing thing)
        {
            return thing is Projectile;
        }

        public override Vector3 Position(Thing thing)
        {
            return ((Projectile)thing).ExactPosition;
        }

        /// <summary>
        /// One tick back along the round's own line. A vanilla projectile keeps no previous
        /// position, but its flight is a straight lerp at a constant speed, so stepping back by
        /// one tick's travel is exact rather than an estimate.
        /// </summary>
        public override Vector3 LastPosition(Thing thing)
        {
            return Position(thing) - Heading(thing) * CurrentSpeedPerTick(thing);
        }

        public override Vector3 Origin(Thing thing)
        {
            return OriginRef((Projectile)thing);
        }

        public override Vector3 Destination(Thing thing)
        {
            return DestinationRef((Projectile)thing);
        }

        public override Vector3 Heading(Thing thing)
        {
            Projectile projectile = (Projectile)thing;

            Vector3 heading = DestinationRef(projectile) - OriginRef(projectile);
            heading.y = 0f;
            return heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
        }

        /// <summary>
        /// The def's speed times whatever force the round is carrying, because that is exactly
        /// what the postfix on StartingTicksToImpact makes the engine fly it at.
        /// </summary>
        public override float CurrentSpeedPerTick(Thing thing)
        {
            return Rounds.BaseSpeedPerTick(thing) * VectorEditRegistry.ForceFor(thing);
        }

        public override int TicksToImpact(Thing thing)
        {
            return TicksToImpactRef((Projectile)thing);
        }

        public override Thing Launcher(Thing thing)
        {
            return ((Projectile)thing).Launcher;
        }

        /// <summary>
        /// Written by hand rather than by calling Launch. Launch scatters the destination by up
        /// to a third of a cell and re-reads the origin from the projectile's current cell; both
        /// are wrong here, because the whole promise of the editor is that the line drawn on the
        /// paused map is the line the round takes.
        ///
        /// Force is not applied to anything in here. Speed reaches the engine through the
        /// postfix on StartingTicksToImpact and damage through the one on DamageAmount, both of
        /// which read the force back out of <see cref="VectorEditRegistry"/>; the caller
        /// registers it.
        /// </summary>
        public override void Redirect(Thing thing, Vector3 origin, Vector3 endpoint, int ticks,
            float force, Thing launcher)
        {
            Projectile projectile = (Projectile)thing;

            OriginRef(projectile) = origin;
            DestinationRef(projectile) = endpoint;
            TicksToImpactRef(projectile) = ticks;
            LifetimeRef(projectile) = ticks;
            LandedRef(projectile) = false;
            LauncherRef(projectile) = launcher;
            PreventFriendlyFireRef(projectile) = false;

            LocalTargetInfo target = new LocalTargetInfo(endpoint.ToIntVec3());
            projectile.usedTarget = target;
            projectile.intendedTarget = target;
        }

        /// <summary>
        /// Nothing. A held vanilla round is reported at its curve position by the postfix on
        /// Projectile.ExactPosition, which is the single place the engine ever asks.
        /// </summary>
        public override void PlaceHeld(Thing thing, Vector3 at)
        {
        }

        /// <summary>
        /// Position is a lerp of origin->destination by (1 - ticksToImpact/StartingTicksToImpact),
        /// so moving origin to the held position and setting ticksToImpact back to a full
        /// timeline puts the lerp at zero - the round resumes exactly where it was drawn, at its
        /// def's normal speed, and finishes the trip it never stopped making.
        /// </summary>
        public override void Resume(Thing thing, Vector3 from)
        {
            Projectile projectile = (Projectile)thing;

            Vector3 remaining = DestinationRef(projectile) - from;
            remaining.y = 0f;

            float speed = Rounds.BaseSpeedPerTick(thing);

            OriginRef(projectile) = from;
            TicksToImpactRef(projectile) = Mathf.Max(1, Mathf.CeilToInt(remaining.magnitude / speed));
        }

        public override void MaintainSound(Thing thing)
        {
            Sustainer sustainer = AmbientSustainerRef((Projectile)thing);
            if (sustainer != null && !sustainer.Ended) sustainer.Maintain();
        }
    }
}
