using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// The whole contact surface with Combat Extended, and the only place in this mod that knows
    /// CE exists.
    ///
    /// Reflection rather than an assembly reference, for the same reason as
    /// <see cref="MeleeAnimation"/>: this dll has to load and run with CE absent, and a
    /// compile-time reference is resolved the moment any method touching one of their types is
    /// jitted. <see cref="Install"/> answers the question once at startup and leaves the answer
    /// in <see cref="Rounds.Foreign"/>.
    ///
    /// Their side of the contract is a class and its fields. ProjectileCE derives from
    /// ThingWithComps rather than Verse.Projectile - which is the whole reason this file exists -
    /// and carries its flight in shotSpeed, shotAngle, shotRotation, origin, Destination and a
    /// trajectory worker that turns those into a position each tick. None of it is private API
    /// in spirit: CE's own ammo, shields and turrets write the same fields.
    ///
    /// One decision worth stating plainly. A round this mod redirects is forced onto CE's
    /// LerpedTrajectoryWorker with its gravity zeroed, which is to say it is made to fly the way
    /// a vanilla round flies: a straight line from where it was caught to where the player
    /// pointed it, arriving in a known number of ticks. CE's ballistic model cannot express that
    /// - under ballistics a flat shot travels until it falls to the ground, so range is decided
    /// by height and gravity rather than by the editor - and the editor's one promise is that the
    /// line drawn on the paused map is the line the round takes. Zeroing gravity is what keeps
    /// that promise. It also makes damage behave: CE scales damage by remaining kinetic energy,
    /// shotSpeed squared over initialSpeed squared, so under ballistics a round thrown twice as
    /// hard would arrive four times as hard. The lerped worker reports full energy, which leaves
    /// force scaling damage linearly and puts CE on exactly the vanilla rule.
    /// </summary>
    public sealed class CombatExtendedRounds : RoundBackend
    {
        /// <summary>
        /// Flight height for a redirected round when its own is unusable.
        ///
        /// CE impacts a lerped round when its height reaches zero, so a round caught in the last
        /// moment before it hit the dirt cannot be sent back out at the height it was at - it
        /// would land on the first tick. Chest height on a standing human, which is where a round
        /// has to be to hit anything worth hitting.
        /// </summary>
        private const float MinimumFlightHeight = 0.85f;

        private sealed class Repelled { public Repelled() { } public bool value; }
        private static readonly ConditionalWeakTable<Thing, Repelled> repelled = new ConditionalWeakTable<Thing, Repelled>();
        private static Type projectileType;
        private static Type lerpedWorkerType;

        private static AccessTools.FieldRef<object, Vector3> exactPositionRef;
        private static AccessTools.FieldRef<object, Vector3> lastPosRef;
        private static AccessTools.FieldRef<object, Vector3> velocityRef;
        private static AccessTools.FieldRef<object, Vector2> originRef;
        private static AccessTools.FieldRef<object, Vector2> destinationRef;
        private static AccessTools.FieldRef<object, IntVec3> originCellRef;
        private static AccessTools.FieldRef<object, float> shotSpeedRef;
        private static AccessTools.FieldRef<object, float> initialSpeedRef;
        private static AccessTools.FieldRef<object, float> shotAngleRef;
        private static AccessTools.FieldRef<object, float> shotRotationRef;
        private static AccessTools.FieldRef<object, float> shotHeightRef;
        private static AccessTools.FieldRef<object, float> startingTicksRef;
        private static AccessTools.FieldRef<object, int> ticksToImpactRef;
        private static AccessTools.FieldRef<object, int> flightTicksRef;
        private static AccessTools.FieldRef<object, int> ticksToTruePositionRef;
        private static AccessTools.FieldRef<object, float> gravityPerWidthRef;
        private static AccessTools.FieldRef<object, double> gravityRef;
        private static AccessTools.FieldRef<object, bool> landedRef;
        private static AccessTools.FieldRef<object, bool> lerpPositionRef;
        private static AccessTools.FieldRef<object, Thing> launcherRef;
        private static AccessTools.FieldRef<object, LocalTargetInfo> intendedTargetRef;
        private static AccessTools.FieldRef<object, Sustainer> ambientSustainerRef;

        private static FieldInfo equipmentField;
        private static FieldInfo forcedWorkerField;
        private static FieldInfo predictedPositionsField;
        private static FieldInfo damageAmountField;
        private static MethodInfo exactPositionSetter;
        private static MethodInfo damageAmountGetter;
        private static MethodInfo damageAmountSetter;

        /// <summary>
        /// Looks for CE and, if it is there, hands this backend to <see cref="Rounds"/> and takes
        /// over the tick of any round a membrane is holding.
        ///
        /// Called once, from the mod's own static constructor, after PatchAll. The tick patch is
        /// applied by hand rather than by attribute for the obvious reason: there is no type to
        /// name in an attribute unless CE is loaded.
        /// </summary>
        public static void Install(Harmony harmony)
        {
            projectileType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
            if (projectileType == null) return;

            lerpedWorkerType = AccessTools.TypeByName("CombatExtended.LerpedTrajectoryWorker");

            exactPositionRef = FieldRef<Vector3>("exactPosition");
            lastPosRef = FieldRef<Vector3>("LastPos");
            velocityRef = FieldRef<Vector3>("velocity");
            originRef = FieldRef<Vector2>("origin");
            destinationRef = FieldRef<Vector2>("Destination");
            originCellRef = FieldRef<IntVec3>("OriginIV3");
            shotSpeedRef = FieldRef<float>("shotSpeed");
            initialSpeedRef = FieldRef<float>("initialSpeed");
            shotAngleRef = FieldRef<float>("shotAngle");
            shotRotationRef = FieldRef<float>("shotRotation");
            shotHeightRef = FieldRef<float>("shotHeight");
            startingTicksRef = FieldRef<float>("startingTicksToImpact");
            ticksToImpactRef = FieldRef<int>("intTicksToImpact");
            flightTicksRef = FieldRef<int>("FlightTicks");
            ticksToTruePositionRef = FieldRef<int>("ticksToTruePosition");
            gravityPerWidthRef = FieldRef<float>("GravityPerWidth");
            gravityRef = FieldRef<double>("gravity");
            landedRef = FieldRef<bool>("landed");
            lerpPositionRef = FieldRef<bool>("lerpPosition");
            launcherRef = FieldRef<Thing>("launcher");
            intendedTargetRef = FieldRef<LocalTargetInfo>("intendedTarget");
            ambientSustainerRef = FieldRef<Sustainer>("ambientSustainer");

            equipmentField = AccessTools.Field(projectileType, "equipmentDef");
            forcedWorkerField = AccessTools.Field(projectileType, "forcedTrajectoryWorker");
            predictedPositionsField = AccessTools.Field(projectileType, "cachedPredictedPositions");
            damageAmountField = AccessTools.Field(projectileType, "damageAmount");
            exactPositionSetter = AccessTools.PropertySetter(projectileType, "ExactPosition");
            damageAmountGetter = AccessTools.PropertyGetter(projectileType, "DamageAmount");
            damageAmountSetter = AccessTools.PropertySetter(projectileType, "DamageAmount");

            MethodInfo tick = AccessTools.Method(projectileType, "Tick");

            bool complete = equipmentField != null && lerpedWorkerType != null && forcedWorkerField != null
                && predictedPositionsField != null && damageAmountField != null
                && exactPositionSetter != null && damageAmountGetter != null
                && damageAmountSetter != null && tick != null
                && exactPositionRef != null && lastPosRef != null && velocityRef != null
                && originRef != null && destinationRef != null && originCellRef != null
                && shotSpeedRef != null && initialSpeedRef != null && shotAngleRef != null
                && shotRotationRef != null && shotHeightRef != null && startingTicksRef != null
                && ticksToImpactRef != null && flightTicksRef != null
                && ticksToTruePositionRef != null && gravityPerWidthRef != null
                && gravityRef != null && landedRef != null && lerpPositionRef != null
                && launcherRef != null && intendedTargetRef != null && ambientSustainerRef != null;

            if (!complete)
            {
                Log.Warning("[RimArt] Combat Extended is loaded but its projectile did not look "
                    + "the way this mod expects. Vector manipulation, the halving membrane and the "
                    + "involute will not act on CE rounds.");
                return;
            }

            harmony.Patch(tick, new HarmonyMethod(typeof(CombatExtendedRounds), nameof(TickPrefix)));
            harmony.Patch(AccessTools.Method(projectileType, "ExposeData"), postfix:
                new HarmonyMethod(typeof(CombatExtendedRounds), nameof(RepelledExposeData)));
            harmony.Patch(AccessTools.Method(projectileType, "MoveForward"), postfix:
                new HarmonyMethod(typeof(CombatExtendedRounds), nameof(RepelledMoveForward)));
            Rounds.Foreign = new CombatExtendedRounds();
        }

        /// <summary>
        /// Takes a held round off CE's clock entirely, which is the same thing the prefix on
        /// Projectile.TickInterval does for the engine's own rounds. Its position comes from the
        /// halving curve instead, written by <see cref="PlaceHeld"/> once per game tick.
        /// </summary>
        public static bool TickPrefix(Thing __instance)
        {
            if (!ShinraCombat.BeforeProjectileTick(__instance, 1)) return false;
            if (RecursionRegistry.CapturedCount == 0) return true;

            HalvingProjectile held;
            return !RecursionRegistry.TryGetCapture(__instance, out held);
        }

        public static void RepelledMoveForward(Thing __instance, ref Vector3 __result)
        {
            if (!repelled.TryGetValue(__instance, out var state) || !state.value || ticksToImpactRef(__instance) > 0) return;
            Vector2 end = destinationRef(__instance);
            __result.x = end.x;
            __result.z = end.y;
        }

        public static void RepelledExposeData(Thing __instance)
        {
            var state = repelled.GetOrCreateValue(__instance);
            Scribe_Values.Look(ref state.value, "rimArtRepelled");
            if (!state.value) return;
            float damage = Scribe.mode == LoadSaveMode.Saving
                ? (float)damageAmountGetter.Invoke(__instance, null) : 0f;
            Scribe_Values.Look(ref damage, "rimArtRepelledDamage");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                damageAmountSetter.Invoke(__instance, new object[] { damage });
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                forcedWorkerField.SetValue(__instance, Activator.CreateInstance(lerpedWorkerType));
                lerpPositionRef(__instance) = true;
                gravityPerWidthRef(__instance) = 0f;
                gravityRef(__instance) = 0.0;
            }
        }

        private static AccessTools.FieldRef<object, T> FieldRef<T>(string name)
        {
            FieldInfo field = AccessTools.Field(projectileType, name);
            if (field == null || field.FieldType != typeof(T)) return null;

            return AccessTools.FieldRefAccess<object, T>(field);
        }

        // ------------------------------------------------------------------ reading

        public override bool DirectFlight(Thing thing)
        {
            if (!base.DirectFlight(thing) || landedRef(thing)) return false;
            var equipment = equipmentField.GetValue(thing) as ThingDef;
            if (equipment?.thingCategories?.Exists(c => c.defName == "Grenades") == true) return false;
            return true;
        }

        public override float DirectDamage(Thing thing) => (float)damageAmountGetter.Invoke(thing, null);

        public override void Repel(Thing thing, Vector3 entry, Vector3 outward, Thing caster)
        {
            repelled.GetOrCreateValue(thing).value = true;
            float damage = DirectDamage(thing);
            float speed = CurrentSpeedPerTick(thing);
            float height = exactPositionRef(thing).y;
            Vector3 remaining = Destination(thing) - entry;
            remaining.y = 0f;
            int ticks = Mathf.Max(1, Mathf.CeilToInt(remaining.magnitude / speed));
            Redirect(thing, entry, entry + outward * remaining.magnitude, ticks,
                speed / Rounds.BaseSpeedPerTick(thing), caster);
            damageAmountSetter.Invoke(thing, new object[] { damage });
            shotHeightRef(thing) = height;
            startingTicksRef(thing) = remaining.magnitude / speed;
            Place(thing, new Vector3(entry.x, height, entry.z));
        }

        public override bool Owns(Thing thing)
        {
            return projectileType.IsInstanceOfType(thing);
        }

        public override void Bend(Thing thing, Vector3 position, Vector3 heading, float remaining, float speed)
        {
            float damage = DirectDamage(thing);
            float height = exactPositionRef(thing).y;
            repelled.GetOrCreateValue(thing).value = true;
            base.Bend(thing, position, heading, remaining, speed);
            damageAmountSetter.Invoke(thing, new object[] { damage });
            shotHeightRef(thing) = height;
            startingTicksRef(thing) = remaining / speed;
            Place(thing, new Vector3(position.x, height, position.z));
        }

        public override Vector3 Position(Thing thing)
        {
            return exactPositionRef(thing);
        }

        /// <summary>
        /// CE keeps this itself, and its own collision checks are built on the same segment -
        /// which is the tell that a single sampled point is not enough to catch a round with.
        /// </summary>
        public override Vector3 LastPosition(Thing thing)
        {
            return lastPosRef(thing);
        }

        public override Vector3 Origin(Thing thing)
        {
            Vector2 origin = originRef(thing);
            return new Vector3(origin.x, 0f, origin.y);
        }

        public override Vector3 Destination(Thing thing)
        {
            Vector2 destination = destinationRef(thing);
            return new Vector3(destination.x, 0f, destination.y);
        }

        /// <summary>
        /// Taken from the velocity CE is actually flying it on, and only from origin->destination
        /// when that is unreadable. A ballistic round's destination is where it is predicted to
        /// land rather than where it is pointed, so the two are not the same question.
        /// </summary>
        public override Vector3 Heading(Thing thing)
        {
            Vector3 velocity = velocityRef(thing);
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.0001f) return velocity.normalized;

            Vector3 travel = Destination(thing) - Origin(thing);
            travel.y = 0f;
            return travel.sqrMagnitude > 0.0001f ? travel.normalized : Vector3.forward;
        }

        public override float CurrentSpeedPerTick(Thing thing)
        {
            float speed = shotSpeedRef(thing) / 60f;
            return speed > 0f ? speed : Rounds.BaseSpeedPerTick(thing);
        }

        public override int TicksToImpact(Thing thing)
        {
            return ticksToImpactRef(thing);
        }

        public override Thing Launcher(Thing thing)
        {
            return launcherRef(thing);
        }

        // ------------------------------------------------------------------ writing

        /// <summary>
        /// The new flight, written the way CE's own Launch writes one but without its two
        /// unwanted habits: Launch clamps speed up to the def's figure, which would make the
        /// quarter-force setting impossible, and it derives the destination from the ballistics
        /// rather than taking the one it was given.
        /// </summary>
        public override void Redirect(Thing thing, Vector3 origin, Vector3 endpoint, int ticks,
            float force, Thing launcher)
        {
            Vector3 heading = endpoint - origin;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0001f) heading = Heading(thing);
            else heading = heading.normalized;

            float height = exactPositionRef(thing).y;
            if (height < MinimumFlightHeight) height = MinimumFlightHeight;

            float speed = Rounds.BaseSpeedPerTick(thing) * force;
            if (speed <= 0f) speed = Rounds.BaseSpeedPerTick(thing);

            // Flat, straight and unaffected by gravity, which is what makes the drawn line the
            // flight. Both halves are needed: the worker decides how position is computed, and
            // lerpPosition is the flag CE checks it against every tick.
            forcedWorkerField.SetValue(thing, Activator.CreateInstance(lerpedWorkerType));
            lerpPositionRef(thing) = true;
            predictedPositionsField.SetValue(thing, null);

            shotAngleRef(thing) = 0f;
            shotRotationRef(thing) = heading.AngleFlat();
            shotHeightRef(thing) = height;
            gravityPerWidthRef(thing) = 0f;
            gravityRef(thing) = 0.0;

            shotSpeedRef(thing) = speed * 60f;
            initialSpeedRef(thing) = speed * 60f;
            velocityRef(thing) = heading * speed;

            originRef(thing) = new Vector2(origin.x, origin.z);
            originCellRef(thing) = new Vector3(origin.x, 0f, origin.z).ToIntVec3();
            destinationRef(thing) = new Vector2(endpoint.x, endpoint.z);

            startingTicksRef(thing) = ticks;
            ticksToImpactRef(thing) = ticks;
            flightTicksRef(thing) = 0;

            // CE eases a new round from its shooter's muzzle onto its true line over the first
            // few ticks. A redirected round has no muzzle to ease out of and the easing would
            // put it visibly off the line the player drew.
            ticksToTruePositionRef(thing) = 0;

            landedRef(thing) = false;
            launcherRef(thing) = launcher;
            intendedTargetRef(thing) = new LocalTargetInfo(endpoint.ToIntVec3());

            Place(thing, new Vector3(origin.x, height, origin.z));

            ApplyForceToDamage(thing, force);
        }

        /// <summary>
        /// Damage on the same terms as vanilla: force times the round's own baseline, never times
        /// a multiplier it already carries.
        ///
        /// Clearing CE's cached figure first is what makes that true. The field is a nullable the
        /// getter fills in from the def and the weapon the first time it is asked, so nulling it
        /// and reading it back gives the round's pristine damage however many times it has been
        /// through the editor already.
        /// </summary>
        private static void ApplyForceToDamage(Thing thing, float force)
        {
            damageAmountField.SetValue(thing, null);

            float baseline = (float)damageAmountGetter.Invoke(thing, null);
            damageAmountSetter.Invoke(thing, new object[] { Mathf.Max(1f, baseline * force) });
        }

        /// <summary>
        /// Moves a held round rather than reporting it. CE reads its position out of a field that
        /// its own collision, drawing and impact checks all share, so there is no single getter to
        /// postfix the way the engine's own rounds allow - and with its tick skipped, nothing else
        /// is going to write it.
        /// </summary>
        public override void PlaceHeld(Thing thing, Vector3 at)
        {
            at.y = exactPositionRef(thing).y;
            Place(thing, at);
        }

        /// <summary>
        /// Back onto a straight line to wherever it was going, at its def's own speed, from
        /// wherever the halvings left it - the same trip it never stopped making.
        /// </summary>
        public override void Resume(Thing thing, Vector3 from)
        {
            Vector3 destination = Destination(thing);

            Vector3 remaining = destination - from;
            remaining.y = 0f;

            int ticks = Mathf.Max(1, Mathf.CeilToInt(remaining.magnitude / Rounds.BaseSpeedPerTick(thing)));
            Redirect(thing, from, destination, ticks, 1f, launcherRef(thing));
        }

        public override void MaintainSound(Thing thing)
        {
            Sustainer sustainer = ambientSustainerRef(thing);
            if (sustainer != null && !sustainer.Ended) sustainer.Maintain();
        }

        /// <summary>
        /// Through the property rather than the field, because CE's setter also moves the thing's
        /// cell - and a round whose cell never moves is one the map's own lists go on reporting in
        /// the place it started.
        /// </summary>
        private static void Place(Thing thing, Vector3 at)
        {
            exactPositionSetter.Invoke(thing, new object[] { at });
            lastPosRef(thing) = at;
        }
    }
}
