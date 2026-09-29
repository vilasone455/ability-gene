using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using P = RimArt.ShadowPlexusTiming;

namespace RimArt
{
    /// <summary>
    /// Everything Shikamaru's kit does on a map:
    /// - keeps every live Shadow imitation hold, Shadow seam, Shadow grasp slide, Shadow double and
    ///   Shadow neck bind, and applies each one's rule every tick: the drag, the leash, the slide,
    ///   the double's steps, the choke;
    /// - checks every shadow line each tick with <see cref="ShadowLines"/> and lets go when it breaks;
    /// - answers where the caster's abilities are cast from (<see cref="OriginCell"/>): the standing
    ///   double's cell, or the caster's own;
    /// - draws each record with the kit's pictures (<see cref="ShadowImitationGraphics"/> and the rest)
    ///   from live positions.
    ///
    /// A cast is known from the start of its warmup (<see cref="Begin"/>, from
    /// <see cref="JobDriver_CastShadowPlexus"/>), so the line runs out during the warmup and holds at
    /// the fire. The clock is game ticks. Nothing here is saved: a game loaded mid-hold sees the
    /// stun run out on its own and the drawing gone. Neck bind's suffocation is a hediff and saves
    /// itself.
    /// </summary>
    public class MapComponent_ShadowPlexus : MapComponent
    {
        /// <summary>A cast whose warmup ran out this long ago without landing was interrupted.</summary>
        private const float Overdue = 0.25f;
        /// <summary>How many one-cell pulls a seam makes in one tick before it gives up.</summary>
        private const int MostPulls = 8;

        private static readonly AccessTools.FieldRef<Pawn_PathFollower, PathEndMode> EndMode =
            AccessTools.FieldRefAccess<Pawn_PathFollower, PathEndMode>("peMode");

        private readonly List<ShadowHold> holds = new List<ShadowHold>();
        private readonly List<ShadowSeamLink> seams = new List<ShadowSeamLink>();
        private readonly List<ShadowGraspSlide> slides = new List<ShadowGraspSlide>();
        private readonly List<ShadowDoubleRecord> doubles = new List<ShadowDoubleRecord>();
        private readonly List<ShadowNeckBindCast> binds = new List<ShadowNeckBindCast>();
        /// <summary>Casters whose cast has landed while their cast job still runs (CastJobFail).</summary>
        private readonly HashSet<Pawn> fired = new HashSet<Pawn>();

        public MapComponent_ShadowPlexus(Map map) : base(map) { }

        public static MapComponent_ShadowPlexus Of(Map map) => map?.GetComponent<MapComponent_ShadowPlexus>();

        private static int Now => Find.TickManager.TicksGame;

        private static int Ticks(float seconds) => Mathf.RoundToInt(seconds * 60f);

        private static float Degrees(Vector2 run) => run.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;

        private static Vector2 CellGround(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        /// <summary>
        /// How far below a standing humanlike's draw position (the sprite's centre) its feet are, in
        /// cells; a downed one lies lower. The sketches anchor every pool and line at the feet, with
        /// the body rising from that point, so the shadow lies on the ground and not on the body.
        /// </summary>
        public const float StandingFeet = 0.35f, DownedFeet = 0.12f;
        /// <summary>From the feet up to the neck of a standing humanlike sprite, for the neck bind's hands.</summary>
        public const float NeckAboveFeet = 0.55f;

        /// <summary>Where a thing meets the ground now: a pawn's feet, an item's draw position, or its cell's centre when it is not on this map.</summary>
        private Vector2 Ground(Thing thing)
        {
            if (thing == null) return Vector2.zero;
            if (thing is Pawn pawn)
            {
                Vector2 at = ChainSickleCombat.Ground(pawn, map) ?? CellGround(pawn.Position);
                return new Vector2(at.x, at.y - Feet(pawn));
            }
            if (thing.Spawned && thing.Map == map) return new Vector2(thing.DrawPos.x, thing.DrawPos.z);
            return CellGround(thing.Position);
        }

        /// <summary>How far below a pawn's draw position its feet are. Animals and mechs are drawn lying on the ground already.</summary>
        public static float Feet(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike) return 0f;
            return pawn.Downed ? DownedFeet : StandingFeet;
        }

        private static float Size(Thing thing) => thing is Pawn pawn ? pawn.BodySize : 0f;

        private static bool Gone(Pawn pawn, Map map) => pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned || pawn.Map != map;

        private static bool Open(Map map, IntVec3 cell) =>
            cell.InBounds(map) && cell.Walkable(map) && (!(cell.GetDoor(map) is Building_Door door) || door.Open);

        // ------------------------------------------------------------------ queries

        /// <summary>The caster's double that is out: cast, not yet gone.</summary>
        public ShadowDoubleRecord DoubleOf(Pawn caster) => doubles.Find(d => d.caster == caster && d.Out);

        /// <summary>Whether the caster's abilities are cast from a standing double right now.</summary>
        public bool CastsFromDouble(Pawn caster) => DoubleOf(caster)?.Stands(Now) ?? false;

        /// <summary>The cell the caster's abilities are cast from: the standing double's, or the caster's own.</summary>
        public IntVec3 OriginCell(Pawn caster)
        {
            ShadowDoubleRecord d = DoubleOf(caster);
            return d != null && d.Stands(Now) ? d.cell : caster.Position;
        }

        /// <summary>The ground point the caster's shadow lines start from.</summary>
        public Vector2 OriginGround(Pawn caster)
        {
            ShadowDoubleRecord d = DoubleOf(caster);
            // A standing double's lines start at its feet, fitted to a real pawn as ShadowDoubleGraphics draws it (PawnFit).
            return d != null && d.Stands(Now) ? CellGround(d.cell) + new Vector2(0f, PawnFit.FitY(0f)) : Ground(caster);
        }

        public ShadowHold HoldOf(Pawn caster, Pawn target) => holds.Find(h => h.caster == caster && h.target == target && h.Holding);

        public ShadowSeamLink SeamOf(Pawn caster, Thing thing) => seams.Find(s => s.caster == caster && s.Holds(thing));

        /// <summary>Whether the caster holds this pawn by Imitation or has it sewn by Seam: what Neck bind needs.</summary>
        public bool Held(Pawn caster, Pawn target) => HoldOf(caster, target) != null || SeamOf(caster, target) != null;

        /// <summary>Whether the caster holds or has sewn any pawn at all.</summary>
        public bool HoldsAnyone(Pawn caster) =>
            holds.Exists(h => h.caster == caster && h.Holding) || seams.Exists(s => s.caster == caster && s.Sewn && (s.a is Pawn || s.b is Pawn));

        public ShadowNeckBindCast BindOf(Pawn caster) => binds.Find(b => b.caster == caster && b.Choking);

        public ShadowGraspSlide SlideOf(Pawn caster) => slides.Find(s => s.caster == caster && s.Sliding);

        // ------------------------------------------------------------------ casts

        /// <summary>A cast's warmup has begun: the line starts running out. Neck bind has no warmup and lands in its Apply.</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target, LocalTargetInfo dest)
        {
            Ended(caster);
            if (caster == null || ability == null) return;
            AbilityDef def = ability.def;
            float warmup = def.verbProperties.warmupTime;
            int now = Now;
            if (def == ShadowPlexusDefOf.AG_ShadowImitation && target.Thing is Pawn pawn)
                holds.Add(new ShadowHold { caster = caster, target = pawn, startTick = now, cast = warmup, props = ability.CompOfType<CompAbilityEffect_ShadowImitation>()?.Props });
            else if (def == ShadowPlexusDefOf.AG_ShadowSeam && target.HasThing && dest.HasThing)
                seams.Add(new ShadowSeamLink { caster = caster, a = target.Thing, b = dest.Thing, startTick = now, cast = warmup, props = ability.CompOfType<CompAbilityEffect_ShadowSeam>()?.Props });
            else if (def == ShadowPlexusDefOf.AG_ShadowGrasp && target.HasThing && dest.IsValid)
                slides.Add(NewSlide(caster, target.Thing, dest.Cell, now, warmup, ability.CompOfType<CompAbilityEffect_ShadowGrasp>()?.Props));
            else if (def == ShadowPlexusDefOf.AG_ShadowDouble && target.IsValid)
                doubles.Add(new ShadowDoubleRecord { caster = caster, cell = target.Cell, startTick = now, cast = warmup, props = ability.CompOfType<CompAbilityEffect_ShadowDouble>()?.Props, aim = Degrees(CellGround(target.Cell) - Ground(caster)) });
        }

        private ShadowGraspSlide NewSlide(Pawn caster, Thing thing, IntVec3 dest, int startTick, float warmup, CompProperties_ShadowGrasp props)
        {
            Vector2 origin = OriginGround(caster), item = Ground(thing), to = CellGround(dest);
            float aim = Degrees(item - origin);
            return new ShadowGraspSlide
            {
                caster = caster, thing = thing, startTick = startTick, cast = warmup, props = props,
                origin = origin, item = item, dest = to, aim = aim, turn = Mathf.DeltaAngle(aim, Degrees(to - item)),
                handSize = thing is Pawn ? ShadowGraspTiming.BodyHand : 1f,
            };
        }

        public void LandImitation(Pawn caster, Pawn target, float warmup, CompProperties_ShadowImitation props)
        {
            int now = Now;
            ShadowHold hold = holds.Find(h => h.caster == caster && !h.Landed);
            if (hold == null)
            {
                hold = new ShadowHold { caster = caster, startTick = now - Ticks(warmup), cast = warmup };
                holds.Add(hold);
            }
            // One hold per caster: a new one lets the old go.
            for (int i = 0; i < holds.Count; i++)
                if (holds[i] != hold && holds[i].caster == caster && holds[i].Holding) Release(holds[i], ImitationEnd.Released, null);
            hold.target = target;
            hold.props = props;
            hold.fireTick = now;
            hold.endTick = now + Ticks(props.holdSeconds);
            hold.lastCasterCell = caster.Position;
            // The stun alone holds the target: a stunned pawn's path waits (FullBodyBusy), and its job
            // carries on after the release. No pather.StopDead(), for the reason MapComponent_ChainSickle gives.
            target.stances?.stunner?.StunFor(Ticks(props.holdSeconds), caster, false, false);
            fired.Add(caster);
        }

        public void LandSeam(Pawn caster, Thing a, Thing b, float warmup, CompProperties_ShadowSeam props)
        {
            int now = Now;
            ShadowSeamLink link = seams.Find(s => s.caster == caster && !s.Landed);
            if (link == null)
            {
                link = new ShadowSeamLink { caster = caster, startTick = now - Ticks(warmup), cast = warmup };
                seams.Add(link);
            }
            link.a = a;
            link.b = b;
            link.props = props;
            link.fireTick = now;
            link.endTick = now + Ticks(props.holdSeconds);
            link.lastA = a.Position;
            link.lastB = b.Position;
            link.lastCasterCell = caster.Position;
            fired.Add(caster);
        }

        public void LandGrasp(Pawn caster, Thing thing, IntVec3 dest, float warmup, CompProperties_ShadowGrasp props)
        {
            int now = Now;
            ShadowGraspSlide slide = slides.Find(s => s.caster == caster && !s.Landed);
            if (slide == null)
            {
                slide = NewSlide(caster, thing, dest, now - Ticks(warmup), warmup, props);
                slides.Add(slide);
            }
            slide.thing = thing;
            slide.props = props;
            slide.fireTick = now;
            slide.item = Ground(thing);
            slide.dest = CellGround(dest);
            slide.aim = Degrees(slide.item - slide.origin);
            slide.turn = Mathf.DeltaAngle(slide.aim, Degrees(slide.dest - slide.item));
            slide.path = new List<IntVec3>(GenSight.PointsOnLineOfSight(thing.Position, dest));
            if (slide.path.Count == 0 || slide.path[0] != thing.Position) slide.path.Insert(0, thing.Position);
            if (slide.path[slide.path.Count - 1] != dest) slide.path.Add(dest);
            slide.index = 0;
            slide.stopIndex = StopIndex(slide, 1);
            float speed = thing is Pawn ? props.bodySpeed : props.itemSpeed;
            slide.ticksPerCell = Mathf.Max(1, Mathf.RoundToInt(60f / Mathf.Max(0.1f, speed)));
            slide.nextStepTick = now + Ticks(ShadowGraspTiming.Open + ShadowGraspTiming.Curl) + slide.ticksPerCell;
            if (slide.stopIndex <= 0) slide.arriveTick = slide.nextStepTick;
            fired.Add(caster);
        }

        /// <summary>
        /// The last cell of the path the thing reaches, looking from <paramref name="from"/> on: it stops
        /// in the cell of the first pawn on the path, and before a wall, a closed door or another item.
        /// </summary>
        private int StopIndex(ShadowGraspSlide slide, int from)
        {
            List<IntVec3> path = slide.path;
            for (int i = from; i < path.Count; i++)
            {
                IntVec3 cell = path[i];
                if (!Open(map, cell) || HasOtherItem(cell, slide.thing)) return i - 1;
                if (HasOtherPawn(cell, slide.thing)) return i;
            }
            return path.Count - 1;
        }

        private bool HasOtherPawn(IntVec3 cell, Thing but)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn pawn && pawn != but && !pawn.Dead) return true;
            return false;
        }

        private bool HasOtherItem(IntVec3 cell, Thing but)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] != but && things[i].def.category == ThingCategory.Item) return true;
            return false;
        }

        public void LandDouble(Pawn caster, IntVec3 cell, float warmup, CompProperties_ShadowDouble props)
        {
            int now = Now;
            ShadowDoubleRecord d = doubles.Find(x => x.caster == caster && !x.Landed);
            if (d == null)
            {
                d = new ShadowDoubleRecord { caster = caster, startTick = now - Ticks(warmup), cast = warmup, aim = Degrees(CellGround(cell) - Ground(caster)) };
                doubles.Add(d);
            }
            // One double per caster: a new one sends the old home.
            for (int i = 0; i < doubles.Count; i++)
                if (doubles[i] != d && doubles[i].caster == caster && doubles[i].Out) GoneNow(doubles[i], DoubleEnd.TimeRunsOut, null);
            d.cell = cell;
            d.props = props;
            d.fireTick = now;
            d.endTick = now + Ticks(props.standSeconds);
            d.lastCasterCell = caster.Position;
            fired.Add(caster);
        }

        public void LandNeckBind(Pawn caster, Pawn target, CompProperties_ShadowNeckBind props)
        {
            ShadowHold hold = HoldOf(caster, target);
            ShadowSeamLink seam = hold == null ? SeamOf(caster, target) : null;
            if (hold == null && seam == null) return;
            int now = Now;
            binds.Add(new ShadowNeckBindCast
            {
                caster = caster, target = target, props = props, hold = hold, seam = seam, startTick = now, fireTick = now, cast = 0f,
                hideLine = seam != null, lastCasterCell = caster.Position,
            });
            fired.Add(caster);
        }

        // ------------------------------------------------------------------ the cast job

        /// <summary>Whether the caster's cast has landed while its job still runs (CastJobFail.FailBeforeFired).</summary>
        public bool Fired(Pawn caster) => fired.Contains(caster);

        /// <summary>Whether the caster's cast job should still hold it in place: only a Neck bind channel does.</summary>
        public bool Holds(Pawn caster) => BindOf(caster) != null;

        /// <summary>The caster's cast job is over. A cast that never landed has nothing to show; a bind's hands fall off.</summary>
        public void Ended(Pawn caster)
        {
            fired.Remove(caster);
            holds.RemoveAll(h => h.caster == caster && !h.Landed);
            seams.RemoveAll(s => s.caster == caster && !s.Landed);
            slides.RemoveAll(s => s.caster == caster && !s.Landed);
            doubles.RemoveAll(d => d.caster == caster && !d.Landed);
            ShadowNeckBindCast bind = BindOf(caster);
            if (bind != null) ReleaseBind(bind, cut: true, lineBroke: false);
        }

        /// <summary>Lets go of everything on the map (debug).</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < binds.Count; i++) if (binds[i].Choking) ReleaseBind(binds[i], true, false);
            for (int i = 0; i < holds.Count; i++) if (holds[i].Holding) Release(holds[i], ImitationEnd.Released, null);
            for (int i = 0; i < seams.Count; i++) if (seams[i].Sewn) Undo(seams[i], null);
            for (int i = 0; i < slides.Count; i++) if (slides[i].Sliding) Arrive(slides[i], Now);
            for (int i = 0; i < doubles.Count; i++) if (doubles[i].Out) GoneNow(doubles[i], DoubleEnd.TimeRunsOut, null);
        }

        // ------------------------------------------------------------------ ticking

        public override void MapComponentTick()
        {
            int now = Now;
            for (int i = doubles.Count - 1; i >= 0; i--) if (!TickDouble(doubles[i], now)) doubles.RemoveAt(i);
            for (int i = holds.Count - 1; i >= 0; i--) if (!TickHold(holds[i], now)) holds.RemoveAt(i);
            for (int i = seams.Count - 1; i >= 0; i--) if (!TickSeam(seams[i], now)) seams.RemoveAt(i);
            for (int i = slides.Count - 1; i >= 0; i--) if (!TickSlide(slides[i], now)) slides.RemoveAt(i);
            for (int i = binds.Count - 1; i >= 0; i--) if (!TickBind(binds[i], now)) binds.RemoveAt(i);
        }

        /// <summary>A record that began its warmup and never landed: kept until the warmup is overdue.</summary>
        private bool Pending(ShadowCast cast, int now) =>
            cast.Seconds(now) <= cast.cast + Overdue && cast.caster != null && cast.caster.Spawned && cast.caster.Map == map;

        private void Say(Pawn caster, string text)
        {
            if (text != null && caster != null && caster.Faction == Faction.OfPlayer)
                Messages.Message("AG_ShadowReleased".Translate(text), caster, MessageTypeDefOf.NeutralEvent, false);
        }

        // ---- Imitation

        private bool TickHold(ShadowHold h, int now)
        {
            if (!h.Landed) return Pending(h, now);
            float s = h.Seconds(now);
            if (h.releaseTick >= 0) return s < h.Seconds(h.releaseTick) + ShadowImitationTiming.After(h.end) + ShadowImitationTiming.Tail;

            Pawn caster = h.caster, target = h.target;
            if (Gone(caster, map) || caster.Downed) { Release(h, ImitationEnd.Released, "AG_ShadowLetGo".Translate(caster?.LabelShort ?? "")); return true; }
            if (Gone(target, map)) { Release(h, ImitationEnd.Released, null); return true; }
            if (now >= h.endTick) { Release(h, ImitationEnd.Released, null); return true; }

            IntVec3 origin = OriginCell(caster);
            LineBreak broke = ShadowLines.Check(map, origin, target.Position, caster, target, light: true, smoke: true, out IntVec3 at, out Pawn by);
            if (broke != LineBreak.None)
            {
                h.cutShare = ShadowLines.Share(origin, target.Position, at);
                Release(h, broke == LineBreak.Dark ? ImitationEnd.Dark : ImitationEnd.Cut, ShadowLines.Word(broke, by));
                return true;
            }

            // Every step the caster takes drags the target one cell the same way; a wall or a pawn stops that step.
            IntVec3 delta = caster.Position - h.lastCasterCell;
            h.lastCasterCell = caster.Position;
            if (delta != IntVec3.Zero)
            {
                IntVec3 to = target.Position + delta;
                if (Open(map, to) && !HasOtherPawn(to, target))
                {
                    target.Position = to;
                    // Only the path follower is told: the draw position keeps lerping, which is the lurch.
                    target.Notify_Teleported(false, false);
                    h.moved = true;
                }
            }
            return true;
        }

        private void Release(ShadowHold h, ImitationEnd end, string reason)
        {
            if (!h.Holding) return;
            int now = Now;
            h.releaseTick = now;
            h.end = end;
            Pawn target = h.target;
            if (target != null && !target.Dead && target.Spawned)
            {
                int left = h.endTick - now;
                StunHandler stunner = target.stances?.stunner;
                // Only the hold's own stun: a longer one from something else is left alone.
                if (stunner != null && stunner.Stunned && stunner.StunTicksLeft <= left + 2) stunner.StopStun();
            }
            for (int i = 0; i < binds.Count; i++)
                if (binds[i].hold == h && binds[i].Choking) ReleaseBind(binds[i], cut: true, lineBroke: end != ImitationEnd.Released);
            Say(h.caster, reason);
        }

        // ---- Seam

        private bool TickSeam(ShadowSeamLink link, int now)
        {
            if (!link.Landed) return Pending(link, now);
            float s = link.Seconds(now);
            if (link.undoTick >= 0) return s < link.Seconds(link.undoTick) + ShadowSeamTiming.Undo + ShadowSeamTiming.Tail;

            Pawn caster = link.caster;
            Thing a = link.a, b = link.b;
            if (Gone(caster, map) || caster.Downed) { Undo(link, "AG_ShadowLetGo".Translate(caster?.LabelShort ?? "")); return true; }
            if (!OnMap(a) || !OnMap(b)) { Undo(link, null); return true; }
            if (now >= link.endTick) { Undo(link, null); return true; }

            LineBreak broke = ShadowLines.Check(map, a.Position, b.Position, a, b, light: true, smoke: true, out _, out Pawn by);
            if (broke != LineBreak.None) { Undo(link, ShadowLines.Word(broke, by)); return true; }

            // The leash: past maxApart the one that did not move is pulled after the one that did; when
            // both moved, the smaller body is pulled. A pull that cannot be made undoes the mover's step.
            float maxApart = link.props.maxApart;
            if (a.Position.DistanceTo(b.Position) > maxApart)
            {
                bool movedA = a.Position != link.lastA, movedB = b.Position != link.lastB;
                Thing dragged = movedA && !movedB ? b : movedB && !movedA ? a : Size(a) <= Size(b) ? a : b;
                Thing anchor = link.Other(dragged);
                for (int pulls = 0; pulls < MostPulls && dragged.Position.DistanceTo(anchor.Position) > maxApart; pulls++)
                {
                    IntVec3 next = ShadowLines.NextToward(dragged.Position, anchor.Position);
                    if (!CanEnter(next, dragged))
                    {
                        IntVec3 last = anchor == a ? link.lastA : link.lastB;
                        if (anchor.Position != last && CanEnter(last, anchor)) Move(anchor, last);
                        break;
                    }
                    Move(dragged, next);
                }
                if (link.tautTick < 0) link.tautTick = now;
            }
            link.lastA = a.Position;
            link.lastB = b.Position;
            return true;
        }

        private bool OnMap(Thing thing)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != map) return false;
            return !(thing is Pawn pawn) || !pawn.Dead;
        }

        /// <summary>Whether a thing can be put in a cell: open ground, and no other pawn (for a pawn) or item (for an item) there.</summary>
        private bool CanEnter(IntVec3 cell, Thing thing) =>
            Open(map, cell) && (thing is Pawn ? !HasOtherPawn(cell, thing) : !HasOtherItem(cell, thing));

        /// <summary>Moves a thing one cell, keeping a pawn's job and destination (GravityMovement's recipe).</summary>
        private static void Move(Thing thing, IntVec3 cell)
        {
            if (thing is Pawn pawn)
            {
                Pawn_PathFollower pather = pawn.pather;
                bool moving = pather.Moving;
                LocalTargetInfo destination = pather.Destination;
                PathEndMode mode = EndMode(pather);
                pawn.Position = cell;
                pather.StopDead();
                if (moving && destination.IsValid && !destination.ThingDestroyed) pather.StartPath(destination, mode);
            }
            else thing.Position = cell;
        }

        private void Undo(ShadowSeamLink link, string reason)
        {
            if (!link.Sewn) return;
            link.undoTick = Now;
            for (int i = 0; i < binds.Count; i++)
                if (binds[i].seam == link && binds[i].Choking) ReleaseBind(binds[i], cut: true, lineBroke: true);
            Say(link.caster, reason);
        }

        // ---- Grasp

        private bool TickSlide(ShadowGraspSlide slide, int now)
        {
            if (!slide.Landed) return Pending(slide, now);
            float s = slide.Seconds(now);
            if (slide.arriveTick >= 0) return s < slide.Seconds(slide.arriveTick) + ShadowGraspTiming.LetGo + ShadowGraspTiming.Back + ShadowGraspTiming.Tail;

            Thing thing = slide.thing;
            // Gone, or moved by something else (hauled, picked up): the hand lets go where it is.
            if (!OnMap(thing) || thing.Position != slide.path[slide.index]) { Arrive(slide, now); return true; }
            if (now < slide.nextStepTick) return true;
            int next = slide.index + 1;
            if (next > slide.stopIndex || !Open(map, slide.path[next]) || HasOtherItem(slide.path[next], thing)) { Arrive(slide, now); return true; }
            // A pawn that stepped onto the path since the cast stops the thing in its cell.
            if (HasOtherPawn(slide.path[next], thing)) slide.stopIndex = next;
            thing.Position = slide.path[next];
            if (thing is Pawn pawn) pawn.Notify_Teleported(false, false);
            slide.index = next;
            slide.nextStepTick = now + slide.ticksPerCell;
            if (slide.index >= slide.stopIndex) Arrive(slide, now);
            return true;
        }

        private static void Arrive(ShadowGraspSlide slide, int now)
        {
            if (!slide.Sliding) return;
            slide.arriveTick = now;
            slide.stopIndex = Mathf.Min(slide.stopIndex, slide.index);
        }

        // ---- Double

        private bool TickDouble(ShadowDoubleRecord d, int now)
        {
            if (!d.Landed) return Pending(d, now);
            float s = d.Seconds(now);
            if (d.goneTick >= 0) return s < d.Seconds(d.goneTick) + ShadowDoubleTiming.After(d.end);

            Pawn caster = d.caster;
            if (Gone(caster, map) || caster.Downed) { GoneNow(d, DoubleEnd.TimeRunsOut, null); return true; }
            if (now >= d.endTick) { GoneNow(d, DoubleEnd.TimeRunsOut, null); return true; }
            if (ShadowLight.Dark(map, d.cell)) { GoneNow(d, DoubleEnd.FireGoesOut, "AG_ShadowDoubleDark".Translate()); return true; }
            // The tie line back to the caster may cross dark cells and smoke, but not a pawn or a wall.
            LineBreak broke = ShadowLines.Check(map, caster.Position, d.cell, caster, null, light: false, smoke: false, out _, out Pawn by);
            if (broke != LineBreak.None) { GoneNow(d, DoubleEnd.FireGoesOut, ShadowLines.Word(broke, by)); return true; }

            // It copies the caster's steps one for one; a wall stops it.
            IntVec3 delta = caster.Position - d.lastCasterCell;
            d.lastCasterCell = caster.Position;
            if (delta != IntVec3.Zero && Open(map, d.cell + delta)) d.cell += delta;
            return true;
        }

        private void GoneNow(ShadowDoubleRecord d, DoubleEnd end, string reason)
        {
            if (!d.Out) return;
            d.goneTick = Now;
            d.end = end;
            Say(d.caster, reason);
        }

        // ---- Neck bind

        private bool TickBind(ShadowNeckBindCast b, int now)
        {
            float s = b.Seconds(now);
            if (b.releaseTick >= 0)
                return s < b.Seconds(b.releaseTick) + (b.cut ? P.SnapTime + 1.6f : ShadowNeckBindTiming.LineBack + ShadowNeckBindTiming.Tail);

            Pawn caster = b.caster, target = b.target;
            bool linked = b.hold != null ? b.hold.Holding : b.seam != null && b.seam.Holds(target);
            if (!linked || Gone(caster, map) || caster.Downed) { ReleaseBind(b, cut: true, lineBroke: false); return true; }
            if (Gone(target, map)) { ReleaseBind(b, cut: true, lineBroke: false); return true; }

            float closed = b.Closed;
            if (s >= closed)
            {
                Hediff choked = b.choked ?? target.health.hediffSet.GetFirstHediffOfDef(ShadowPlexusDefOf.AG_ShadowChoked);
                if (choked == null)
                {
                    choked = HediffMaker.MakeHediff(ShadowPlexusDefOf.AG_ShadowChoked, target);
                    choked.Severity = 0.001f;
                    target.health.AddHediff(choked);
                }
                b.choked = choked;
                choked.Severity = Mathf.Min(1f, choked.Severity + b.props.suffocationPerSecond / 60f);
                choked.TryGetComp<HediffComp_ShadowChoke>()?.Fed(now);
                if (choked.Severity >= HediffComp_ShadowChoke.Full) { PassOut(b); return true; }
            }
            if (s >= closed + b.props.chokeSeconds) PassOut(b);
            return true;
        }

        /// <summary>The hands let go on their own: the target dropped, or the channel ran its course. A hold ends with it.</summary>
        private void PassOut(ShadowNeckBindCast b)
        {
            ReleaseBind(b, cut: false, lineBroke: false);
            if (b.hold != null && b.hold.Holding) Release(b.hold, ImitationEnd.Released, null);
        }

        /// <summary>
        /// Ends a bind. <paramref name="cut"/>: the hands come apart in shreds and the suffocation
        /// drains. <paramref name="lineBroke"/>: the hold's line broke with it, so the bind draws the
        /// broken line; otherwise the line's own record keeps drawing it and the bind shows only its hands.
        /// </summary>
        private void ReleaseBind(ShadowNeckBindCast b, bool cut, bool lineBroke)
        {
            if (!b.Choking) return;
            b.releaseTick = Now;
            b.cut = cut;
            b.cutShare = lineBroke && b.hold != null ? b.hold.cutShare : 0.5f;
            b.hideLine = b.seam != null || (cut && !lineBroke);
        }

        // ------------------------------------------------------------------ drawing

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Now;
            // The double is drawn on a real pawn's scale (see PawnFit).
            PawnFit.Begin();
            try
            {
                for (int i = 0; i < doubles.Count; i++) DrawDouble(doubles[i], now);
            }
            finally
            {
                PawnFit.End();
            }
            for (int i = 0; i < holds.Count; i++) DrawHold(holds[i], now);
            for (int i = 0; i < seams.Count; i++) DrawSeam(seams[i], now);
            for (int i = 0; i < slides.Count; i++) DrawSlide(slides[i], now);
            for (int i = 0; i < binds.Count; i++) DrawBind(binds[i], now);
        }

        private float ReachNow(Pawn caster, float full) => ShadowLight.Reach(full, map, OriginCell(caster));

        private void DrawHold(ShadowHold h, int now)
        {
            // A bind drawing the line draws the pool and the grip with it.
            for (int i = 0; i < binds.Count; i++) if (binds[i].hold == h && !binds[i].hideLine) return;
            float s = h.Seconds(now);
            ShadowImitationGraphics.Draw(new ImitationShot
            {
                Carrier = OriginGround(h.caster), Target = Ground(h.target), Seconds = s, Cast = h.cast,
                Release = h.releaseTick < 0 ? float.PositiveInfinity : h.Seconds(h.releaseTick), End = h.end, CutShare = h.cutShare,
                Range = s < h.cast ? ReachNow(h.caster, h.props?.reach ?? ShadowImitationTiming.FullRange) : 0f,
                Width = ShadowImitationTiming.Width, Sway = ShadowImitationTiming.Sway,
            }, map);
        }

        private void DrawSeam(ShadowSeamLink link, int now)
        {
            float s = link.Seconds(now);
            float ratio = Mathf.Clamp(Size(link.b) / Mathf.Max(Size(link.a), 0.5f), 0.8f, 2f);
            ShadowSeamGraphics.Draw(new SeamShot
            {
                Carrier = OriginGround(link.caster), A = Ground(link.a), B = Ground(link.b), Seconds = s, Cast = link.cast,
                Taut = link.tautTick < 0 ? float.PositiveInfinity : link.Seconds(link.tautTick),
                Undo = link.undoTick < 0 ? float.PositiveInfinity : link.Seconds(link.undoTick),
                PoolB = ratio, StitchB = ratio,
                Range = s < link.cast ? ReachNow(link.caster, link.props?.reach ?? ShadowSeamTiming.FullRange) : 0f,
                Width = ShadowSeamTiming.Width, Sway = ShadowSeamTiming.Sway,
            }, map);
        }

        private void DrawSlide(ShadowGraspSlide slide, int now)
        {
            float s = slide.Seconds(now), slideStart = ShadowGraspTiming.SlideStart(slide.cast);
            int steps = slide.path != null ? slide.path.Count - 1 : 0, stop = slide.path != null ? slide.stopIndex : 0;
            float arrive = slide.arriveTick >= 0 ? slide.Seconds(slide.arriveTick) : slideStart + stop * slide.ticksPerCell / 60f;
            ShadowGraspGraphics.Draw(new GraspShot
            {
                Carrier = slide.origin, Item = slide.item, Dest = slide.dest, Seconds = s, Cast = slide.cast,
                SlideStart = slideStart, Arrive = Mathf.Max(arrive, slideStart + 0.05f), Share = steps > 0 ? stop / (float)steps : 0f,
                Aim = slide.aim, Turn = slide.turn, HandSize = slide.handSize,
                Range = s < slide.cast ? ReachNow(slide.caster, slide.props?.reach ?? ShadowGraspTiming.FullRange) : 0f,
                Width = ShadowGraspTiming.Width, Sway = ShadowGraspTiming.Sway,
            }, map);
        }

        private void DrawDouble(ShadowDoubleRecord d, int now)
        {
            float s = d.Seconds(now);
            ShadowDoubleGraphics.Draw(new DoubleShot
            {
                Carrier = Ground(d.caster), Spot = CellGround(d.cell), Enemy = CellGround(d.cell), Aim = d.aim, Seconds = s, Cast = d.cast,
                CastStart = float.PositiveInfinity, HeldAt = float.PositiveInfinity, Release = float.PositiveInfinity,
                Gone = d.goneTick < 0 ? float.PositiveInfinity : d.Seconds(d.goneTick), End = d.end, Range = 0f,
            }, map);
        }

        private void DrawBind(ShadowNeckBindCast b, int now)
        {
            Vector2 carrier = b.hold != null ? OriginGround(b.caster) : Ground(b.seam?.Other(b.target)), target = Ground(b.target);
            ShadowNeckBindGraphics.Draw(new NeckBindShot
            {
                Carrier = carrier, Target = target, Aim = Degrees(target - carrier), Seconds = b.Seconds(now), Climb = b.props.climbSeconds,
                Release = b.releaseTick < 0 ? float.PositiveInfinity : b.Seconds(b.releaseTick),
                Choke = 1f / Mathf.Max(0.001f, b.props.suffocationPerSecond), Cut = b.cut, CutAt = b.cutShare, HideLine = b.hideLine,
                NeckHeight = b.target != null && b.target.RaceProps.Humanlike ? NeckAboveFeet : 0f,
                Width = ShadowNeckBindTiming.Width, Sway = ShadowNeckBindTiming.Sway,
            }, map);
        }

        public override void MapRemoved()
        {
            holds.Clear();
            seams.Clear();
            slides.Clear();
            doubles.Clear();
            binds.Clear();
            fired.Clear();
        }
    }
}
