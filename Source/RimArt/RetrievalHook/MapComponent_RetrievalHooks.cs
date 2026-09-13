using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Every retrieval hook shot on this map, saved with the map.
    ///
    /// A map component for the same reason <see cref="MapComponent_ToyCars"/> is one: a pull is a
    /// relationship between the wearer and the target that has to be checked every tick while
    /// either can die, move or be removed.
    ///
    /// The dragged-pawn table is static because the Harmony patches that read it (draw position,
    /// job blocking) run for every pawn and cannot afford a map component lookup each time. It is
    /// rebuilt from the saved pulls on load and cleared when a different game is running.
    /// </summary>
    public class MapComponent_RetrievalHooks : MapComponent
    {
        private List<RetrievalPull> pulls = new List<RetrievalPull>();

        private static readonly Dictionary<Pawn, RetrievalPull> dragged = new Dictionary<Pawn, RetrievalPull>();
        private static Game draggedGame;

        public MapComponent_RetrievalHooks(Map map) : base(map) { }

        // ------------------------------------------------------------------ queries

        /// <summary>The shot this pawn is currently holding, or null.</summary>
        public static RetrievalPull PullOf(Pawn pawn)
        {
            MapComponent_RetrievalHooks component = pawn?.Map?.GetComponent<MapComponent_RetrievalHooks>();
            if (component == null) return null;
            for (int i = 0; i < component.pulls.Count; i++)
            {
                RetrievalPull pull = component.pulls[i];
                if (pull.caster == pawn && pull.HoldsCaster) return pull;
            }
            return null;
        }

        public static bool IsPulling(Pawn pawn) => PullOf(pawn) != null;

        /// <summary>True when some pull other than <paramref name="except"/> involves the thing.</summary>
        public static bool IsTargeted(Thing thing, RetrievalPull except = null)
        {
            MapComponent_RetrievalHooks component = thing?.MapHeld?.GetComponent<MapComponent_RetrievalHooks>();
            if (component == null) return false;
            for (int i = 0; i < component.pulls.Count; i++)
            {
                RetrievalPull pull = component.pulls[i];
                if (pull != except && pull.Involves(thing)) return true;
            }
            return false;
        }

        public static bool AnyDragged => dragged.Count > 0;

        public static bool IsDragged(Pawn pawn)
        {
            return dragged.Count > 0 && pawn != null && dragged.ContainsKey(pawn);
        }

        public static bool TryGetDragPosition(Pawn pawn, out Vector3 position)
        {
            position = default;
            if (dragged.Count == 0 || pawn == null || !dragged.TryGetValue(pawn, out RetrievalPull pull)) return false;
            position = pull.DragPosition;
            return true;
        }

        public static void RegisterDragged(Pawn pawn, RetrievalPull pull)
        {
            ForgetOtherGames();
            dragged[pawn] = pull;
        }

        /// <summary>Drops entries left over from a game that is no longer running.</summary>
        private static void ForgetOtherGames()
        {
            if (draggedGame == Current.Game) return;
            dragged.Clear();
            draggedGame = Current.Game;
        }

        public static void UnregisterDragged(Pawn pawn)
        {
            if (pawn != null) dragged.Remove(pawn);
        }

        // ------------------------------------------------------------------ launch

        /// <summary>
        /// Starts a shot. The wearer's pull job is queued first, so it starts as soon as the cast
        /// job ends; starting it here, inside the verb that is still casting, would end the cast job
        /// under its own feet.
        /// </summary>
        public static void Launch(Pawn caster, Apparel belt, Thing target)
        {
            MapComponent_RetrievalHooks component = caster?.Map?.GetComponent<MapComponent_RetrievalHooks>();
            if (component == null || target == null) return;

            component.pulls.Add(new RetrievalPull(caster.Map, caster, belt, target));

            Job job = JobMaker.MakeJob(RetrievalHookDefOf.AG_RetrievalHookPull, target);
            job.playerForced = true;
            caster.jobs.jobQueue.EnqueueFirst(job);
        }

        // ------------------------------------------------------------------ lifecycle

        public override void MapComponentTick()
        {
            for (int i = pulls.Count - 1; i >= 0; i--)
            {
                RetrievalPull pull = pulls[i];
                if (pull.Tick()) continue;
                pull.Abandon();
                pulls.Remove(pull);
            }
        }

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap) return;
            for (int i = 0; i < pulls.Count; i++) RetrievalHookGraphics.Draw(pulls[i]);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ForgetOtherGames();
            for (int i = 0; i < pulls.Count; i++)
            {
                pulls[i].map = map;
                Pawn pawn = pulls[i].DraggedPawn;
                if (pawn != null && pulls[i].phase == RetrievalPhase.Drag) RegisterDragged(pawn, pulls[i]);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pulls, "retrievalPulls", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pulls ??= new List<RetrievalPull>();
                for (int i = 0; i < pulls.Count; i++) pulls[i].map = map;
            }
        }

        public override void MapRemoved()
        {
            for (int i = 0; i < pulls.Count; i++) pulls[i].Abandon();
            pulls.Clear();
            base.MapRemoved();
        }
    }
}
