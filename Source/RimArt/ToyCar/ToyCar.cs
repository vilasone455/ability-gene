using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The remote vehicle: a Thing that drives itself along a path.
    ///
    /// A Thing rather than a Pawn, and that is the load-bearing decision in this whole feature.
    /// A Pawn would bring pathing, ordering, damage and targeting for free - but a pawn of the
    /// player's faction is always selectable and always orderable, so a parked car would stay
    /// drivable with nobody paying for it. The cost of driving is the entire design, so the
    /// parked state has to be genuinely inert, and the cheapest way to get an inert state is for
    /// the parked car to be an object rather than a colonist.
    ///
    /// What that costs is this file: movement is ours to write. It also means enemy AI ignores
    /// the car, so it draws no fire and is not a decoy.
    ///
    /// Building rather than an item, matching <see cref="PlantedBlade"/>, which is the same
    /// shape: a selectable, ticking object that sits on the map and belongs to somebody. An item
    /// would bring stacking, deterioration, forbidding and haul jobs, none of which mean
    /// anything here. The def keeps passability Standable and isEdifice false so the thing does
    /// not affect regions - ThingDef.AffectsRegions is true only for Impassable, doors and
    /// fences - which is what makes it legal to move by assigning Position.
    /// </summary>
    [StaticConstructorOnStartup]
    public class ToyCar : Building
    {
        /// <summary>
        /// The path being driven, or null when stopped.
        ///
        /// Never saved. PawnPath comes from a pool and handing a pooled object to Scribe would
        /// either leak it or resurrect it into the wrong pool on load, so the destination is
        /// saved instead and the path is rebuilt in <see cref="SpawnSetup"/>.
        /// </summary>
        private PawnPath path;

        private IntVec3 destination = IntVec3.Invalid;

        /// <summary>Where the car is driving from, kept only so the sprite can slide between cells.</summary>
        private IntVec3 previousCell = IntVec3.Invalid;

        private float stepProgress;
        private float stepLength;
        private float speed;
        private float stepHeading;
        private float heading;
        private float wheelTravel;
        private float visualSpeed;
        private float dustTravel;
        private int deploymentTicks = 24;
        private float steering;
        private float brakeDip;
        private float lastVisualSpeed;

        /// <summary>
        /// Re-entry guard for the blast.
        ///
        /// The car can be destroyed and explode from two directions - the player pressing the
        /// button, or the thing being shot - and the explosion is capable of reaching whatever
        /// started it. Without this flag a car killed by another car's blast can re-enter Kill
        /// while it is already being killed. Not saved: a half-detonated car cannot exist across
        /// a save, because detonating destroys it in the same call.
        /// </summary>
        private bool detonating;

        public bool Moving => path != null;

        public IntVec3 Destination => destination;

        /// <summary>
        /// Drawn between the cell it left and the cell it is in, rather than snapped to the grid.
        ///
        /// Finish each slide before consuming another node, including the final destination.
        /// </summary>
        public override Vector3 DrawPos
        {
            get
            {
                Vector3 here = base.DrawPos;
                if (!Moving || !previousCell.IsValid) return here;

                float travelled = Mathf.Clamp01(stepProgress / stepLength);
                Vector3 from = previousCell.ToVector3Shifted();
                from.y = here.y;
                return Vector3.Lerp(from, here, travelled);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            ToyCarGraphics.Draw(drawLoc, heading, wheelTravel, visualSpeed,
                deploymentTicks / 24f, steering, brakeDip);
        }

        /// <summary>
        /// Sends the car to a cell. Returns false if no route exists.
        ///
        /// A new order keeps the current slide and speed, then follows the replacement path.
        /// Failed orders leave the existing route intact. Replaced paths return to their pool.
        /// </summary>
        public bool DriveTo(IntVec3 cell)
        {
            if (!Spawned || Map == null) return false;
            if (!cell.IsValid || !cell.InBounds(Map) || cell.Impassable(Map)) return false;


            // No pawn to path for, so the pawn-free overload of TraverseParms. PassDoors rather
            // than ByPawn because there is no pawn whose faction could decide what a door means:
            // the car is an object and opens nothing, so it is routed through doors that already
            // stand open and around the rest.
            PawnPath found = Map.pathFinder.FindPathNow(
                Position, cell,
                TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false),
                null, PathEndMode.OnCell);

            if (found == null || !found.Found)
            {
                found?.ReleaseToPool();
                return false;
            }

            bool finishingStep = Moving && previousCell.IsValid;
            path?.ReleaseToPool();
            path = found;
            destination = cell;
            if (!finishingStep) AdvanceStep();
            return true;
        }

        /// <summary>
        /// Whether a door in this cell, if there is one, is standing open.
        ///
        /// Needed as its own check because a door is not impassable terrain. A door's
        /// passability is PassThroughOnly and its cell is walkable, since the game assumes
        /// whoever is walking there can open it. The car cannot open anything - it has no
        /// faction standing and no hands - so without this it would drive straight through a
        /// closed door, which is both a cheat and obviously wrong on screen.
        ///
        /// Stopping is the v1 answer. Having the operator open the door for it is a real option
        /// later (Building_Door.PawnCanOpen and StartManualOpenBy both exist), and it is the
        /// difference between "through a door" working for colony doors and not at all.
        /// </summary>
        private bool DoorOpen(IntVec3 cell)
        {
            Building_Door door = cell.GetDoor(Map);
            return door == null || door.Open;
        }

        /// <summary>Stops where it stands and gives the path back to the pool.</summary>
        public void Stop()
        {
            path?.ReleaseToPool();
            path = null;
            destination = IntVec3.Invalid;
            previousCell = IntVec3.Invalid;
            stepProgress = 0f;
            speed = 0f;
        }

        protected override void Tick()
        {
            base.Tick();
            if (deploymentTicks < 24) deploymentTicks++;
            steering = Mathf.MoveTowards(steering, Moving
                ? Mathf.Clamp(Mathf.DeltaAngle(heading, stepHeading), -28f, 28f) : 0f, 3f);
            float braking = Mathf.Clamp01((lastVisualSpeed - speed) / ToyCarMotion.Braking);
            brakeDip = Mathf.Lerp(brakeDip, braking, 0.25f);
            lastVisualSpeed = speed;
            visualSpeed = Mathf.MoveTowards(visualSpeed, speed / ToyCarMotion.MaxSpeed, 0.08f);
            if (!Spawned || path == null) return;

            float remaining = stepLength - stepProgress;
            float targetSpeed = ToyCarMotion.MaxSpeed;

            // Brake ahead of the next corner or destination along the actual route.
            // Looking only one node ahead misses braking on short diagonal/cardinal steps.
            float distance = remaining;
            float routeHeading = stepHeading;
            for (int i = 1; i < path.NodesLeftCount; i++)
            {
                Vector3 direction = path.Peek(i).ToVector3Shifted()
                                    - path.Peek(i - 1).ToVector3Shifted();
                float nextHeading = direction.AngleFlat();
                float cornerSpeed = ToyCarMotion.CornerSpeed(
                    Mathf.Abs(Mathf.DeltaAngle(routeHeading, nextHeading)));
                targetSpeed = Mathf.Min(targetSpeed,
                    ToyCarMotion.ApproachSpeed(cornerSpeed, distance));
                distance += direction.magnitude;
                routeHeading = nextHeading;
                if (distance > ToyCarMotion.BrakingDistance) break;
            }
            if (distance <= ToyCarMotion.BrakingDistance || path.NodesLeftCount <= 1)
                targetSpeed = Mathf.Min(targetSpeed, ToyCarMotion.ApproachSpeed(0f, distance));

            heading = Mathf.MoveTowardsAngle(heading, stepHeading, ToyCarDefaults.TurnDegreesPerTick);
            targetSpeed = Mathf.Min(targetSpeed, ToyCarMotion.CornerSpeed(
                Mathf.Abs(Mathf.DeltaAngle(heading, stepHeading))));
            speed = ToyCarMotion.ChangeSpeed(speed, targetSpeed);
            float travelled = Mathf.Min(remaining, speed);
            wheelTravel = Mathf.Repeat(wheelTravel + travelled, Mathf.PI * 2f);
            dustTravel += travelled;
            if (dustTravel >= 0.9f)
            {
                dustTravel = 0f;
                // Soft ground gets a light tyre puff; paved floors stay clean.
                if (!Position.Fogged(Map) && Position.GetTerrain(Map).fertility > 0f)
                {
                    Vector3 rear = DrawPos - Quaternion.Euler(0f, heading, 0f) * Vector3.forward * 0.28f;
                    FleckMaker.ThrowDustPuff(rear, Map, 0.25f);
                }
            }
            stepProgress = Mathf.Min(stepLength, stepProgress + speed);
            if (stepLength - stepProgress <= ToyCarMotion.ArrivalDistance) AdvanceStep();
        }

        private void AdvanceStep()
        {
            // PawnPath keeps the current cell as its last remaining node.
            // ConsumeNextNode advances past it and returns the following cell.
            if (path.NodesLeftCount <= 1)
            {
                Stop();
                return;
            }

            IntVec3 next = path.ConsumeNextNode();

            // Checked at the moment of stepping rather than trusted from when the path was
            // found. A door can close and a wall can go up mid-drive, and a car that walked
            // through either would be a bug the player sees rather than one the log records.
            if (!next.InBounds(Map) || next.Impassable(Map) || !DoorOpen(next))
            {
                Stop();
                return;
            }

            previousCell = Position;
            Vector3 direction = next.ToVector3Shifted() - previousCell.ToVector3Shifted();
            stepHeading = direction.AngleFlat();
            stepLength = direction.magnitude;
            Position = next;
            stepProgress = 0f;
        }

        /// <summary>
        /// Sets the charge off and destroys the car.
        ///
        /// The car is destroyed first and the blast is raised afterwards, at the cell it was
        /// standing on. Done the other way round the explosion damages the car that is raising
        /// it, which routes back into <see cref="Kill"/> mid-detonation for no gain - the car is
        /// going to be gone either way, and an explosion it is no longer standing in behaves
        /// identically to everything else on the map.
        /// </summary>
        public void Detonate(Thing instigator = null)
        {
            if (detonating || !Spawned) return;

            Map map = Map;
            IntVec3 cell = Position;

            detonating = true;
            Stop();
            Destroy(DestroyMode.Vanish);
            Blast(map, cell, instigator);
        }

        /// <summary>
        /// Picks the car up. No explosion, chassis kept.
        ///
        /// Until the rig and its supply exist the chassis has nowhere to go, so this only
        /// removes the car. Returning a carryable chassis to the operator is part of the item's
        /// acquisition, which arrives with the rig.
        /// </summary>
        public void Recall()
        {
            if (!Spawned) return;
            Stop();
            Destroy(DestroyMode.Vanish);
        }

        /// <summary>
        /// Anything that kills the car sets it off, because the car is full of chemfuel.
        ///
        /// This is what stops a long leash from being free reconnaissance: a car that is seen
        /// and shot does not quietly stop being a problem, it goes off where it stands. Vanilla
        /// destruction runs first, then the blast is raised at the remembered cell - by then
        /// this object is despawned and Position is no longer meaningful.
        /// </summary>
        public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
        {
            if (detonating)
            {
                base.Kill(dinfo, exactCulprit);
                return;
            }

            Map map = Map;
            IntVec3 cell = Position;

            detonating = true;
            Stop();
            base.Kill(dinfo, exactCulprit);
            Blast(map, cell, dinfo?.Instigator);
        }

        private void Blast(Map map, IntVec3 cell, Thing instigator)
        {
            if (map == null || !cell.IsValid) return;

            ToyCarGraphics.DetonationFlash(cell.ToVector3Shifted(), map);

            // Vanilla Bomb rather than a damage def of this mod's own. The frost bomb needed
            // AG_Cryo because the freeze rides on that def's damage worker; nothing rides on
            // this one, and a def added only to rename the death message is ceremony.
            //
            // No explosion sound is passed, which is not an omission: DoExplosion falls back to
            // the damage def's own soundExplosion, and Bomb already carries the right one.
            GenExplosion.DoExplosion(cell, map, ToyCarDefaults.BlastRadius, DamageDefOf.Bomb,
                instigator, ToyCarDefaults.BlastDamage);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                heading = Rotation.AsAngle;
                deploymentTicks = 0;
            }

            // The saved destination is re-pathed rather than restored, because the path itself
            // was never saved. A route that no longer exists simply stops the car where it is.
            if (respawningAfterLoad && destination.IsValid && destination != Position)
                DriveTo(destination);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Stop();
            base.DeSpawn(mode);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;

            ToyCarLink link = MapComponent_ToyCars.LinkFor(this);

            if (link == null)
            {
                Pawn candidate = MapComponent_ToyCars.FindOperator(this);
                Command_Action take = new Command_Action
                {
                    defaultLabel = "Take control",
                    defaultDesc = candidate == null
                        ? "Nobody in range can take control of this car."
                        : "Have " + candidate.LabelShortCap + " drive this car. They cannot move "
                          + "or act until control is released.",
                    icon = ControlIcon,
                    action = () =>
                    {
                        string refused = MapComponent_ToyCars.Begin(candidate, this);
                        if (refused != null)
                            Messages.Message(refused, this, MessageTypeDefOf.RejectInput, false);
                    }
                };

                if (candidate == null)
                    take.Disable("No able colonist within "
                                 + ToyCarDefaults.LeashRadius.ToString("F0") + " cells.");

                yield return take;
            }
            else
            {
                yield return new Command_Action
                {
                    defaultLabel = "Drive to",
                    defaultDesc = "Send the car to a cell within range of its operator.",
                    icon = DriveIcon,
                    action = () => BeginDriveTargeting(link)
                };

                yield return new Command_Action
                {
                    defaultLabel = "Release control",
                    defaultDesc = "Stop driving. The car stays where it is and the operator is "
                                  + "free to act again.",
                    icon = ControlIcon,
                    action = () => MapComponent_ToyCars.Release(this)
                };

                // Only while controlled, which is the cost the whole design turns on: setting
                // the charge off means somebody has to be holding the handset, and holding the
                // handset means standing still. A parked car cannot be triggered from safety.
                yield return new Command_Action
                {
                    defaultLabel = "Detonate",
                    defaultDesc = "Set off the charge. The car is destroyed.",
                    icon = DetonateIcon,
                    action = () => Detonate(link.Operator)
                };
            }

            yield return new Command_Action
            {
                defaultLabel = "Recall",
                defaultDesc = "Pick the car up. The charge is not spent.",
                icon = RecallIcon,
                action = () =>
                {
                    MapComponent_ToyCars.Release(this);
                    Recall();
                }
            };
        }

        /// <summary>
        /// Asks the player for a cell and refuses anything outside the operator's leash.
        ///
        /// Refusing at the order is what makes an out-of-range car impossible rather than
        /// merely unlikely, so nothing downstream has to handle one. It works because the
        /// operator cannot move while driving: the centre of the ring is fixed for as long as
        /// the order it validates can matter.
        ///
        /// The validator only guards the destination. A route that bulges outside the ring on
        /// its way around a wall is still allowed, which is a deliberate simplification - the
        /// leash is about where you can send it, not about the shape of the road.
        /// </summary>
        private void BeginDriveTargeting(ToyCarLink link)
        {
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetSelf = false,
                validator = target => target.Cell.IsValid
                                      && target.Cell.InBounds(Map)
                                      && !target.Cell.Impassable(Map)
                                      && target.Cell.DistanceTo(link.Operator.Position)
                                         <= ToyCarDefaults.LeashRadius
            };

            Find.Targeter.BeginTargeting(parameters, target =>
            {
                if (!DriveTo(target.Cell))
                    Messages.Message("No route there.", this, MessageTypeDefOf.RejectInput, false);
            });
        }

        // Resolved once and kept, the way the other gizmo-bearing comps in this mod do it.
        // reportFailure is false throughout: a missing texture leaves a button with no picture,
        // which still works, where a reported failure is a red error for a cosmetic miss.
        private static Texture2D detonateIcon;
        private static Texture2D recallIcon;
        private static Texture2D controlIcon;
        private static Texture2D driveIcon;

        private static Texture2D DetonateIcon =>
            detonateIcon ??= ContentFinder<Texture2D>.Get("UI/Abilities/MechSmokepop", false);

        private static Texture2D RecallIcon =>
            recallIcon ??= ContentFinder<Texture2D>.Get("RimArt/ToyCar/Car", false);

        private static Texture2D ControlIcon =>
            controlIcon ??= ContentFinder<Texture2D>.Get("UI/Abilities/MechResurrection", false);

        private static Texture2D DriveIcon =>
            driveIcon ??= ContentFinder<Texture2D>.Get("RimArt/ToyCar/Car", false);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref destination, "destination", IntVec3.Invalid);
            Scribe_Values.Look(ref heading, "heading", 0f);
        }
    }
}
