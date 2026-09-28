using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimArt
{
    public static class DebugActions_ChibakuPlates
    {
        public const float Radius = 6f;

        [RimArtDebug("Pain", "Chibaku ground plates")]
        public static void Plates() => MapComponent_ChibakuPlates.Of(Find.CurrentMap)?.Begin(UI.MouseCell(), Radius);
        [RimArtDebug("Pain", "Chibaku ground plates held up")]
        public static void PlatesHeld() => MapComponent_ChibakuPlates.Of(Find.CurrentMap)?.Begin(UI.MouseCell(), Radius, 3f);
        [RimArtDebug("Pain", "Chibaku ball")]
        public static void Ball() => MapComponent_ChibakuPlates.Of(Find.CurrentMap)?.BeginBall(UI.MouseCell(), Radius);
        [RimArtDebug("Pain", "Chibaku ball held")]
        public static void BallHeld() => MapComponent_ChibakuPlates.Of(Find.CurrentMap)?.BeginBall(UI.MouseCell(), Radius, ChibakuBall.Formed + 1f);
        [RimArtDebug("Pain", "Chibaku ground plates clear", RimArtDebugKind.Now)]
        public static void Clear() => MapComponent_ChibakuPlates.Of(Find.CurrentMap)?.Stop();
    }

    /// <summary>
    /// Plays the Chibaku previews on a map: the ground round a cell is captured and cut, then either the plates
    /// lift 1 cell and land again (<see cref="ChibakuGround.End"/> seconds of real time, also while paused) or
    /// the ball forms, holds and bursts (<see cref="ChibakuBall.End"/> seconds of game time), or either is held
    /// at one moment when frozen. The ground is not changed.
    ///
    /// The live ball takes pawns and items (<see cref="ChibakuPull"/>): it holds them in this component, which the
    /// map counts among its thing holders, so they stay on the map while off it. A save made while they are held
    /// keeps them; the picture is not saved, so on load they are put down at once, unhurt, where the ball was.
    /// Stopping the preview early does the same.
    ///
    /// Polish, after the ability itself is built: the crater's drawing (holes, ribs, drawn rocks) ends with the
    /// preview; it should fade out over about a day, leaving the stony soil, chunks and rubble.
    /// </summary>
    public sealed class MapComponent_ChibakuPlates : MapComponent, IThingHolder
    {
        private ChibakuGround ground;
        private ChibakuBall ball;
        private ChibakuPull pull;
        private float seconds;
        private float? frozenAt;
        private int startTick = -1;
        private ThingOwner<Thing> inner;
        // Saved only while something is held, so a loaded game can put it down.
        private List<ChibakuHeld> savedPawns;
        private IntVec3 savedCell = IntVec3.Invalid;
        private bool releaseAfterLoad;

        public MapComponent_ChibakuPlates(Map map) : base(map)
        {
            inner = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public static MapComponent_ChibakuPlates Of(Map map) => map?.GetComponent<MapComponent_ChibakuPlates>();

        public ChibakuGround Ground => ground;
        public ChibakuBall Ball => ball;
        public ChibakuPull Pull => pull;
        public ThingOwner<Thing> Inner => inner;
        /// <summary>Seconds on the live ball's timeline (game time since it began).</summary>
        public float LiveSeconds => startTick < 0 ? 0f : (Find.TickManager.TicksGame - startTick) / 60f;

        public IThingHolder ParentHolder => map;
        public ThingOwner GetDirectlyHeldThings() => inner;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public ChibakuGround Begin(IntVec3 cell, float radius, float? frozen = null)
        {
            Stop();
            if (!cell.InBounds(map)) return null;
            ground = ChibakuGround.Capture(map, cell, radius);
            seconds = frozen ?? 0f;
            frozenAt = frozen;
            return ground;
        }

        /// <summary>The ball; live (taking pawns, on game time) unless <paramref name="frozen"/> holds it at one moment.</summary>
        public ChibakuBall BeginBall(IntVec3 cell, float radius, float? frozen = null)
        {
            if (Begin(cell, radius, frozen) == null) return null;
            ball = new ChibakuBall(ground);
            if (!frozen.HasValue)
            {
                startTick = Find.TickManager.TicksGame;
                pull = new ChibakuPull(this, ball, cell);
            }
            return ball;
        }

        /// <summary>Holds the preview at <paramref name="at"/> seconds of the timeline (a frozen ball takes no pawns).</summary>
        public void Freeze(float at) => frozenAt = seconds = at;

        public void Stop()
        {
            pull?.ReleaseAll();
            pull = null;
            startTick = -1;
            ball?.Dispose();
            ball = null;
            ground?.Dispose();
            ground = null;
        }

        public override void MapComponentTick()
        {
            if (releaseAfterLoad) ReleaseLoaded();
            if (inner.Count > 0) inner.DoTick();
            if (pull == null || frozenAt.HasValue) return;
            float s = LiveSeconds;
            pull.Tick(s);
            if (s > ChibakuBall.End) Stop();
        }

        public override void MapComponentUpdate()
        {
            if (ground == null || Find.CurrentMap != map) return;
            if (frozenAt.HasValue) seconds = frozenAt.Value;
            else if (pull != null) seconds = LiveSeconds;
            else
            {
                seconds += Time.unscaledDeltaTime;
                if (seconds > (ball != null ? ChibakuBall.End : ChibakuGround.End))
                {
                    Stop();
                    return;
                }
            }
            if (ball != null) ball.Draw(seconds, pull?.pawns);
            else ground.Draw(seconds);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                savedPawns = pull?.pawns.Where(h => h.Thing != null && inner.Contains(h.Thing)).ToList();
                savedCell = ground?.cell ?? IntVec3.Invalid;
            }
            Scribe_Deep.Look(ref inner, "chibakuInner", this);
            Scribe_Collections.Look(ref savedPawns, "chibakuPawns", LookMode.Deep);
            Scribe_Values.Look(ref savedCell, "chibakuCell", IntVec3.Invalid);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (inner == null) inner = new ThingOwner<Thing>(this, false, LookMode.Deep);
                releaseAfterLoad = inner.Count > 0;
            }
        }

        /// <summary>After a load: everything the ball held is put down where it was, unhurt, back in its raid group.</summary>
        private void ReleaseLoaded()
        {
            releaseAfterLoad = false;
            IntVec3 cell = savedCell.IsValid && savedCell.InBounds(map) ? savedCell : map.Center;
            var assault = new List<Pawn>();
            for (int i = inner.Count - 1; i >= 0; i--)
            {
                Thing thing = inner[i];
                inner.Remove(thing);
                if (thing is Pawn) GenSpawn.Spawn(thing, CompNezukoBox.StandableNear(cell, map, null), map);
                else GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
                if (!(thing is Pawn p) || p.Dead) continue;
                Lord lord = savedPawns?.FirstOrDefault(h => h.pawn == p)?.lord;
                if (lord != null && map.lordManager.lords.Contains(lord)) lord.AddPawn(p);
                else if (p.Faction != null && p.Faction != Faction.OfPlayer && p.HostileTo(Faction.OfPlayer)) assault.Add(p);
            }
            foreach (IGrouping<Faction, Pawn> group in assault.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_AssaultColony(group.Key, false, false, false, false, false), map, group);
            savedPawns = null;
        }

        public override void MapRemoved()
        {
            pull = null;
            ball?.Dispose();
            ball = null;
            ground?.Dispose();
            ground = null;
        }
    }
}
