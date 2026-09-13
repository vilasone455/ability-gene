using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    public enum RetrievalPhase { Launch, Drag, Drop }

    /// <summary>
    /// One shot of the retrieval hook, from launch to the target coming to rest.
    ///
    /// Launch: the net flies out. Nothing is split, reserved or moved yet, so an interrupted
    /// launch changes nothing but the belt, which was unloaded when the shot fired.
    ///
    /// Drag: the net connected. A pawn target stays spawned, is reserved by the wearer's pull
    /// job, has its jobs blocked, and is moved one cell at a time. An item target is split (if
    /// over the mass limit) and the moved part is held in <see cref="inner"/>, so it exists in
    /// exactly one place at every moment, including in a save.
    ///
    /// Drop: the item is placed back on the map. If placement fails the item stays held and
    /// placement is tried again next tick, so nothing is ever deleted.
    /// </summary>
    public class RetrievalPull : IExposable, IThingHolder
    {
        public Map map;

        public Pawn caster;
        public Apparel belt;

        /// <summary>The launch target; for a pawn, also the dragged pawn. Null once an item is held.</summary>
        public Thing target;

        public RetrievalPhase phase;
        public IntVec3 anchor;
        public int launchTicks;
        public int launchTotal;
        public int waitTicks;

        public List<IntVec3> path = new List<IntVec3>();
        public DragProgress drag = new DragProgress();

        public ThingOwner<Thing> inner;

        public string targetLabel;
        public string woundReport;
        public int taken;
        public int originalStack;

        /// <summary>Last target draw position seen while launching, kept for drawing.</summary>
        private Vector3 lastTargetDrawPos;

        public RetrievalPull()
        {
            inner = new ThingOwner<Thing>(this, false);
        }

        public RetrievalPull(Map map, Pawn caster, Apparel belt, Thing target) : this()
        {
            this.map = map;
            this.caster = caster;
            this.belt = belt;
            this.target = target;
            anchor = caster.Position;
            phase = RetrievalPhase.Launch;
            float distance = caster.Position.DistanceTo(target.Position);
            launchTotal = Mathf.Max(RetrievalHookDefaults.MinLaunchTicks,
                Mathf.CeilToInt(distance / RetrievalHookDefaults.LaunchCellsPerTick));
            targetLabel = target.LabelShort;
            lastTargetDrawPos = target.DrawPos;
        }

        public Pawn DraggedPawn => phase != RetrievalPhase.Launch ? target as Pawn : null;

        public Thing HeldItem => inner.Count > 0 ? inner[0] : null;

        /// <summary>True while the wearer is committed to this shot.</summary>
        public bool HoldsCaster => phase == RetrievalPhase.Launch || phase == RetrievalPhase.Drag;

        /// <summary>Whether this pull is aimed at, dragging, or holding the thing.</summary>
        public bool Involves(Thing thing)
        {
            if (thing == null) return false;
            if (phase == RetrievalPhase.Launch) return target == thing;
            return target == thing || inner.Contains(thing);
        }

        // ------------------------------------------------------------------ positions

        public Vector3 DragPosition
        {
            get
            {
                if (path.Count == 0) return anchor.ToVector3Shifted();
                int index = Mathf.Clamp(drag.index, 0, path.Count - 1);
                Vector3 to = path[index].ToVector3Shifted();
                if (index == 0) return to;
                Vector3 from = path[index - 1].ToVector3Shifted();
                return Vector3.Lerp(from, to, drag.StepFraction);
            }
        }

        public float LaunchFraction => launchTotal <= 0 ? 1f : Mathf.Clamp01(launchTicks / (float)launchTotal);

        public Vector3 TargetDrawPosition
        {
            get
            {
                if (target != null && target.Spawned && target.Map == map) lastTargetDrawPos = target.DrawPos;
                return lastTargetDrawPos;
            }
        }

        /// <summary>Where the net is drawn: in flight, then on the target.</summary>
        public Vector3 NetPosition
        {
            get
            {
                Vector3 from = caster != null && caster.Spawned ? caster.DrawPos : anchor.ToVector3Shifted();
                if (phase == RetrievalPhase.Launch) return Vector3.Lerp(from, TargetDrawPosition, LaunchFraction);
                return DragPosition;
            }
        }

        // ------------------------------------------------------------------ tick

        /// <summary>Advances the pull. Returns false when it is finished and can be removed.</summary>
        public bool Tick()
        {
            switch (phase)
            {
                case RetrievalPhase.Launch: return TickLaunch();
                case RetrievalPhase.Drag: return TickDrag();
                default: return !TryDropItem();
            }
        }

        private bool TickLaunch()
        {
            string problem = CasterProblem(true);
            if (problem != null)
            {
                Messages.Message("Retrieval hook interrupted: " + problem, caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (launchTicks < launchTotal)
            {
                launchTicks++;
                return true;
            }

            // The net has arrived. The wearer's cast job usually hands over to the pull job within a
            // tick or two; the connection, and with it the reservation, waits for that job.
            if (caster.CurJobDef != RetrievalHookDefOf.AG_RetrievalHookPull)
            {
                if (++waitTicks <= RetrievalHookDefaults.ConnectGraceTicks) return true;
                Messages.Message("Retrieval hook interrupted: " + caster.LabelShortCap + " was given another order.",
                    caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            string refusal = RetrievalTargets.Refusal(caster, target, out int take, this)
                             ?? RetrievalTargets.ReachRefusal(caster, target);
            if (refusal != null)
            {
                Messages.Message("Retrieval hook missed " + targetLabel + ": " + refusal,
                    new LookTargets(caster), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            Connect(take);
            return true;
        }

        private void Connect(int take)
        {
            path = RetrievalRules.DragPath(target.Position.x, target.Position.z, caster.Position.x, caster.Position.z)
                .Select(c => new IntVec3(c.x, 0, c.z)).ToList();
            drag = new DragProgress();

            if (target is Pawn pawn)
            {
                map.reservationManager.Reserve(caster, caster.CurJob, pawn, errorOnFailed: false);
                pawn.jobs?.StopAll();
                pawn.pather?.StopDead();
                phase = RetrievalPhase.Drag;
                MapComponent_RetrievalHooks.RegisterDragged(pawn, this);
                return;
            }

            originalStack = target.stackCount;
            RetrievalRules.SplitCounts(originalStack, take, out _, out taken);
            targetLabel = target.def.label;
            // SplitOff despawns and returns the thing itself when the whole stack is taken, and
            // otherwise returns a new unspawned stack; either way the moved part goes straight
            // into the holder, so it is never in two places or none.
            Thing moving = target.SplitOff(taken);
            if (!inner.TryAdd(moving, false))
            {
                // Could not hold it: put it back where it was rather than lose it.
                if (!moving.Spawned) GenPlace.TryPlaceThing(moving, path[0], map, ThingPlaceMode.Near);
                Messages.Message("Retrieval hook missed " + targetLabel + ".", caster, MessageTypeDefOf.RejectInput, false);
                phase = RetrievalPhase.Drop;
                target = null;
                return;
            }
            target = null;
            phase = RetrievalPhase.Drag;
        }

        private bool TickDrag()
        {
            string problem = CasterProblem(false) ?? TargetProblem();
            if (problem != null) return Finish(problem);

            DragEvent result = drag.Tick(path.Count, CellOpen, out bool firstMove);
            switch (result)
            {
                case DragEvent.StepStarted:
                    if (target is Pawn pawn)
                    {
                        pawn.pather?.StopDead();
                        pawn.Position = path[drag.index];
                        if (firstMove) woundReport = RetrievalWounds.Apply(pawn);
                    }
                    return true;
                case DragEvent.Arrived:
                    return Finish(null);
                case DragEvent.Blocked:
                    return Finish("the path is blocked.");
                default:
                    return true;
            }
        }

        private bool CellOpen(int index)
        {
            IntVec3 cell = path[index];
            if (!cell.InBounds(map) || !cell.Walkable(map)) return false;
            if (caster != null && cell == caster.Position) return false;
            if (cell.GetDoor(map) is Building_Door door && !door.Open) return false;

            // No squeezing diagonally between two walls.
            IntVec3 previous = path[index - 1];
            if (previous.x != cell.x && previous.z != cell.z)
            {
                IntVec3 sideA = new IntVec3(previous.x, 0, cell.z);
                IntVec3 sideB = new IntVec3(cell.x, 0, previous.z);
                if (!sideA.Walkable(map) || !sideB.Walkable(map)) return false;
            }
            return true;
        }

        /// <summary>Why the wearer can no longer hold the shot, or null.</summary>
        private string CasterProblem(bool launching)
        {
            if (caster == null || caster.Destroyed || caster.Dead) return "the wearer died.";
            string name = caster.LabelShortCap;
            if (!caster.Spawned || caster.Map != map) return name + " left the map.";
            if (caster.Downed) return name + " was downed.";
            if (caster.InMentalState) return name + " is having a mental break.";
            if (caster.stances?.stunner != null && caster.stances.stunner.Stunned) return name + " was stunned.";
            if (belt == null || belt.Destroyed || belt.Wearer != caster) return "the belt was removed.";
            if (caster.Position != anchor) return name + " moved.";

            if (caster.CurJobDef == RetrievalHookDefOf.AG_RetrievalHookPull) return null;
            if (launching)
            {
                if (caster.CurJob?.ability?.def == RetrievalHookDefOf.AG_RetrievalHook) return null;
                foreach (QueuedJob queued in caster.jobs.jobQueue)
                    if (queued.job.def == RetrievalHookDefOf.AG_RetrievalHookPull) return null;
            }
            return name + " was given another order.";
        }

        private string TargetProblem()
        {
            if (target is Pawn pawn)
            {
                if (pawn.Destroyed || pawn.Dead) return targetLabel + " died.";
                if (!pawn.Spawned || pawn.Map != map) return targetLabel + " is no longer on the ground.";
                return RetrievalTargets.Refusal(caster, pawn, out _, this, checkReservation: false);
            }
            return HeldItem == null ? "the item is gone." : null;
        }

        /// <summary>
        /// Ends the drag, successful or not. Pawns are released where they are. Items move to the
        /// drop phase. Returns whether the pull should stay in the list.
        /// </summary>
        private bool Finish(string problem)
        {
            string label = targetLabel;
            if (target is Pawn pawn)
            {
                ReleasePawn(pawn);
            }
            else if (taken > 0 && taken < originalStack)
            {
                label = taken + " of " + originalStack + " " + targetLabel;
            }
            else if (taken > 0)
            {
                label = HeldItem?.LabelShort ?? targetLabel;
            }

            string text = problem == null
                ? caster.LabelShortCap + " retrieved " + label + "."
                : "Retrieval of " + label + " stopped: " + problem;
            if (woundReport != null) text += " " + woundReport;

            MessageTypeDef type = woundReport != null ? MessageTypeDefOf.CautionInput
                : problem == null ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput;
            Thing look = (Thing)(target as Pawn) ?? caster;
            Messages.Message(text, look, type, false);

            if (target is Pawn) return false;
            phase = RetrievalPhase.Drop;
            return !TryDropItem();
        }

        private void ReleasePawn(Pawn pawn)
        {
            MapComponent_RetrievalHooks.UnregisterDragged(pawn);
            Job job = caster?.CurJob;
            if (job != null && map.reservationManager.ReservedBy(pawn, caster, job))
                map.reservationManager.Release(pawn, caster, job);
            if (pawn.Spawned) pawn.Notify_Teleported(true, true);
        }

        /// <summary>Places the held item. True when nothing is left to place.</summary>
        private bool TryDropItem()
        {
            Thing item = HeldItem;
            if (item == null) return true;
            IntVec3 cell = path.Count > 0 ? path[Mathf.Clamp(drag.index, 0, path.Count - 1)] : anchor;
            return inner.TryDrop(item, cell, map, ThingPlaceMode.Near, out _) && HeldItem == null;
        }

        /// <summary>
        /// The map is being removed or the pull is being discarded. A dragged pawn is released so
        /// nothing is left blocking its jobs.
        /// </summary>
        public void Abandon()
        {
            if (DraggedPawn != null) MapComponent_RetrievalHooks.UnregisterDragged(DraggedPawn);
        }

        // ------------------------------------------------------------------ holder and save

        public IThingHolder ParentHolder => map;

        public ThingOwner GetDirectlyHeldThings() => inner;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref belt, "belt");
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref launchTicks, "launchTicks");
            Scribe_Values.Look(ref launchTotal, "launchTotal");
            Scribe_Values.Look(ref waitTicks, "waitTicks");
            Scribe_Collections.Look(ref path, "path", LookMode.Value);
            Scribe_Values.Look(ref drag.index, "dragIndex");
            Scribe_Values.Look(ref drag.stepTicks, "dragStepTicks", -1);
            Scribe_Values.Look(ref drag.penaltyApplied, "penaltyApplied");
            Scribe_Deep.Look(ref inner, "inner", this);
            Scribe_Values.Look(ref targetLabel, "targetLabel");
            Scribe_Values.Look(ref woundReport, "woundReport");
            Scribe_Values.Look(ref taken, "taken");
            Scribe_Values.Look(ref originalStack, "originalStack");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                path ??= new List<IntVec3>();
                inner ??= new ThingOwner<Thing>(this, false);
            }
        }
    }
}
