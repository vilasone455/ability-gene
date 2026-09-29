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
    /// Plays Chibaku Tensei on a map, one ball at a time: Pain's cast (<see cref="ChibakuCast"/>) hands its core
    /// over here when it arrives over the cell, and the debug previews start here directly. The ground round the
    /// cell is captured and cut, then either the plates lift 1 cell and land again (<see cref="ChibakuGround.End"/>
    /// seconds of real time, also while paused) or the ball forms, holds and bursts (<see cref="ChibakuBall.End"/>
    /// seconds of game time), or either is held at one moment when frozen (previews only; the ground is not changed).
    ///
    /// The live ball takes pawns and items (<see cref="ChibakuPull"/>): it holds them in this component, which the
    /// map counts among its thing holders, so they stay on the map while off it. A save made while they are held
    /// keeps them; the picture is not saved, so on load they are put down at once, unhurt, where the ball was.
    /// Stopping the preview early does the same.
    ///
    /// Pain's ball bursts early when Pain can no longer hold it (downed, dead, off the map, out of hero form): the
    /// seams open at once, or as soon as it is formed (<see cref="ChibakuBall.BreakAt"/>). He can also let it go
    /// himself once it has formed (<see cref="Release"/>, the Release button). While it holds, his Shinra Tensei and
    /// Banshō Ten'in wait (<see cref="HoldLeft"/>).
    ///
    /// Polish, after the ability itself is built: the crater's drawing (holes, ribs, drawn rocks) ends with the
    /// preview; it should fade out over about a day, leaving the stony soil, chunks and rubble.
    /// </summary>
    public sealed class MapComponent_ChibakuPlates : MapComponent, IThingHolder
    {
        private ChibakuGround ground;
        private ChibakuBall ball;
        private ChibakuPull pull;
        /// <summary>Pain, for the live ball he cast; null for a preview.</summary>
        private Pawn caster;
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
        public Pawn Caster => caster;
        /// <summary>A live ball (taking pawns, on game time) is up.</summary>
        public bool Live => pull != null && !frozenAt.HasValue;
        public ThingOwner<Thing> Inner => inner;
        /// <summary>Seconds on the live ball's timeline (game time since it began).</summary>
        public float LiveSeconds => startTick < 0 ? 0f : (Find.TickManager.TicksGame - startTick) / 60f;

        public IThingHolder ParentHolder => map;
        public ThingOwner GetDirectlyHeldThings() => inner;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public ChibakuGround Begin(IntVec3 cell, float radius, float? frozen = null, ICollection<IntVec3> keep = null)
        {
            Stop();
            if (!cell.InBounds(map)) return null;
            ground = ChibakuGround.Capture(map, cell, radius, keep: keep);
            seconds = frozen ?? 0f;
            frozenAt = frozen;
            return ground;
        }

        /// <summary>
        /// The ball; live (taking pawns, on game time) unless <paramref name="frozen"/> holds it at one moment.
        /// <paramref name="caster"/> is Pain for his cast: he is not caught, and the plates under him and under every
        /// pinned pawn in the circle stay. The numbers are Chibaku Tensei's XML fields (the previews use them too).
        /// </summary>
        public ChibakuBall BeginBall(IntVec3 cell, float radius, float? frozen = null, float height = ChibakuBall.DefaultHeight, Pawn caster = null)
        {
            var keep = new HashSet<IntVec3>();
            if (caster != null && caster.Spawned && caster.Map == map) keep.Add(caster.Position);
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                if (PainKit.Unmovable(p) && p.Position.InHorDistOf(cell, radius + 1.5f)) keep.Add(p.Position);
            if (Begin(cell, radius, frozen, keep) == null) return null;
            CompProperties_ChibakuTensei props = PainKit.ChibakuProps ?? new CompProperties_ChibakuTensei();
            ball = new ChibakuBall(ground, props, height);
            if (!frozen.HasValue)
            {
                startTick = Find.TickManager.TicksGame;
                this.caster = caster;
                pull = new ChibakuPull(this, ball, cell, caster, props);
            }
            return ball;
        }

        /// <summary>Seconds until the live ball <paramref name="pawn"/> cast bursts, or 0.</summary>
        public float HoldLeft(Pawn pawn) => pawn != null && caster == pawn && Live ? Mathf.Max(0f, ball.Burst - LiveSeconds) : 0f;

        /// <summary>Pain can let his ball go now: it is his live ball, it has formed and its seams have not opened yet.</summary>
        public bool CanRelease(Pawn pawn) => pawn != null && caster == pawn && Live && LiveSeconds >= ChibakuBall.Formed && LiveSeconds < ball.Crack;

        /// <summary>
        /// Pain lets his ball go before its time (the Release button, <see cref="Patch_ChibakuRelease"/>): the seams open
        /// now and it bursts <see cref="ChibakuBall.CrackTime"/> later. Those inside take the crush only for the seconds
        /// they were held, and Shinra Tensei and Banshō Ten'in are free after the burst.
        /// </summary>
        public void Release(Pawn pawn)
        {
            if (CanRelease(pawn)) ball.BreakAt(LiveSeconds);
        }

        /// <summary>Pain can still hold his ball: on this map, standing, and in hero form.</summary>
        private bool CasterHolds() => caster.Spawned && caster.Map == map && !caster.Dead && !caster.Downed && PainKit.Has(caster, PainDefOf.AG_PainChibakuTensei);

        /// <summary>Holds the preview at <paramref name="at"/> seconds of the timeline (a frozen ball takes no pawns).</summary>
        public void Freeze(float at) => frozenAt = seconds = at;

        public void Stop()
        {
            pull?.ReleaseAll();
            pull = null;
            caster = null;
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
            if (caster != null && s < ball.Crack && !CasterHolds()) ball.BreakAt(s);
            pull.Tick(s);
            if (s > ball.End) Stop();
        }

        public override void MapComponentUpdate()
        {
            if (ground == null || Find.CurrentMap != map) return;
            if (frozenAt.HasValue) seconds = frozenAt.Value;
            else if (pull != null) seconds = LiveSeconds;
            else
            {
                seconds += Time.unscaledDeltaTime;
                if (seconds > (ball != null ? ball.End : ChibakuGround.End))
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
            caster = null;
            ball?.Dispose();
            ball = null;
            ground?.Dispose();
            ground = null;
        }
    }
}
