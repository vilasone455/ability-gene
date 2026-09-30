using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RimArt.CastleEffectGraphics;
using OT = RimArt.InfinityCastleOpenTiming;
using IT = RimArt.InfinityCastleInsideTiming;

namespace RimArt
{
    public enum CastleGuest { Enemy, Carrier, Summoned }

    /// <summary>One pawn a cast moved into the castle: where it came from, the lord it left, and its doors.</summary>
    public sealed class CastleTaken : IExposable
    {
        public Pawn pawn;
        public CastleGuest kind;
        /// <summary>The home-map cell it goes back to.</summary>
        public IntVec3 from;
        public Lord lord;
        /// <summary>Take: where its door opens, in cells from the target cell, and when (seconds from the start of the warm-up).</summary>
        public Vector2 at;
        public float door;
        /// <summary>Return: when its door opens, in seconds from the return; -1 until then. Shown again once it has.</summary>
        internal float back = -1f;
        internal bool up;

        public void ExposeData()
        {
            // A lord that has ended is saved nowhere, and a reference to it would not resolve on load.
            if (Scribe.mode == LoadSaveMode.Saving && lord != null && (lord.Map == null || !lord.Map.lordManager.lords.Contains(lord))) lord = null;
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref from, "from");
            Scribe_References.Look(ref lord, "lord");
            Scribe_Values.Look(ref at, "at");
            Scribe_Values.Look(ref door, "door");
        }
    }

    /// <summary>
    /// One cast of Infinity Castle, from the strum to the return. The rules (docs/infinity-castle-kit.md,
    /// Nakime in docs/hero-echo.md; the numbers are <see cref="CompProperties_AbilityInfinityCastle"/> on
    /// the ability def and <see cref="InfinityCastleRules"/> on the castle's map def):
    ///
    /// - At the strum (the end of the 1 s warm-up) the hostile pawns within the radius of the target cell,
    ///   not downed, nearest first, up to maxPawns and a total body size of maxBodySize, are chosen and
    ///   held (stunned) where they stand, and the castle is made. A door opens under each as the answer
    ///   ring passes it and it sinks; the carrier goes last. If nobody is left to take, or the castle
    ///   cannot be made, the cooldown and the Echo's charge come back.
    /// - When the carrier has sunk, everyone moves at once: each enemy to its own room, at least 3
    ///   doorways from the biwa room, and the carrier to the dais, where she plays (she cannot walk or
    ///   attack, <see cref="JobDriver_CastlePlay"/>). Each enemy first drops what it carries where it
    ///   stood and leaves its lord (ExitedMap, not a violent loss); inside, the hostile ones get an
    ///   assault lord of their faction. If the carrier is downed, gone or reverted before that, nobody
    ///   moves and the cooldown and charge stay spent.
    /// - The castle stands castleSeconds of game time from the move, and ends early on Release, or when
    ///   the carrier is downed, dies, leaves the castle or loses the ability (reverted, or the pool ran dry).
    /// - Release: a door opens under everyone in the castle and they sink, the carrier last, the castle
    ///   fades. Then everyone alive, downed or not, goes back to the cell they came from (a summoned
    ///   colonist to where it was summoned from), and enemies rejoin their old lord if it still exists,
    ///   else get a lord that leaves the map. Corpses and items come up round the target cell. The castle
    ///   map is removed.
    ///
    /// Two clocks: the home side (the take, the return) and every rule run on game ticks; the castle's
    /// own picture and its commands run on the castle map's real-time clock
    /// (<see cref="MapComponent_InfinityCastle"/>), which this cast waits on for the fade.
    /// </summary>
    public sealed class InfinityCastleCast : IExposable
    {
        public Pawn caster;
        public Map home, castle;
        public IntVec3 target;
        /// <summary>When the warm-up began (the home picture's 0) and how long it was: the strum is at <see cref="strumAt"/>.</summary>
        public int startTick;
        public float strumAt = 1f;
        public float radius = OT.Radius, castleSeconds = 60f;
        /// <summary>The charge the Echo took for the cast.</summary>
        public float paid;
        public int inTick = -1, returnTick = -1;
        public bool releaseOrdered, releasing, returned, fizzled;
        public List<CastleTaken> taken = new List<CastleTaken>();

        // The pictures, not saved: a loaded cast skips what it was drawing.
        private CastleOpenPlan takePlan, returnPlan;
        private readonly List<(Vector2 at, float realTime)> summonDoors = new List<(Vector2 at, float realTime)>();

        public bool Taking => !fizzled && inTick < 0;
        public bool Standing => !fizzled && inTick >= 0 && !returned;
        /// <summary>The cast still holds the carrier: taking, or its castle stands.</summary>
        public bool Busy => Taking || Standing;

        private Ability Ability => caster?.abilities?.GetAbility(InfinityCastleDefOf.AG_Nakime_InfinityCastle);
        internal MapComponent_InfinityCastle Component => castle != null && Find.Maps.Contains(castle) ? castle.GetComponent<MapComponent_InfinityCastle>() : null;
        private Vector2 Origin => new Vector2(target.x + 0.5f, target.z + 0.5f);

        /// <summary>The carrier's door on the take picture: a quarter second after the last pawn has sunk.</summary>
        private float CarrierDoor
        {
            get
            {
                float last = strumAt;
                foreach (CastleTaken t in taken) if (t.kind == CastleGuest.Enemy) last = Mathf.Max(last, t.door);
                return CastleOpenPlan.CasterAfter(last);
            }
        }

        private int TickAt(float seconds) => startTick + Mathf.CeilToInt(seconds * 60f);
        private int MoveTick => TickAt(CarrierDoor + DoorThrough + OT.Sink);

        /// <summary>Seconds of castle left, for the Release button.</summary>
        public float SecondsLeft(int now) => inTick < 0 ? castleSeconds : Mathf.Max(0f, castleSeconds - (now - inTick) / 60f);

        // ---- who is taken ------------------------------------------------------------------------------------

        /// <summary>
        /// The pawns a strum at <paramref name="cell"/> takes: hostile to the carrier, not downed, within
        /// the radius, nearest first, while the count and the total body size allow (a pawn too big for
        /// what is left is passed over for smaller ones behind it).
        /// </summary>
        public static List<Pawn> Candidates(Pawn caster, Map map, IntVec3 cell, CompProperties_AbilityInfinityCastle props)
        {
            var chosen = new List<Pawn>();
            if (caster == null || map == null) return chosen;
            float r2 = props.radius * props.radius, size = 0f;
            var near = new List<Pawn>();
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                if (p != caster && !p.Dead && !p.Downed && (p.Position - cell).LengthHorizontalSquared <= r2 && p.HostileTo(caster)) near.Add(p);
            foreach (Pawn p in near.OrderBy(p => (p.Position - cell).LengthHorizontalSquared))
            {
                if (chosen.Count >= props.maxPawns) break;
                if (size + p.BodySize > props.maxBodySize + 0.001f) continue;
                chosen.Add(p);
                size += p.BodySize;
            }
            return chosen;
        }

        /// <summary>The take picture for these pawns: a door under each as the ring passes it, the carrier's after the last.</summary>
        internal static CastleOpenPlan TakePlan(Pawn caster, IntVec3 cell, List<Pawn> pawns, float radius, float strumAt)
        {
            var o = new Vector2(cell.x + 0.5f, cell.z + 0.5f);
            var plan = new CastleOpenPlan { take = true, radius = radius, strumAt = strumAt, casterShaft = true };
            float last = strumAt;
            foreach (Pawn p in pawns)
            {
                Vector2 at = new Vector2(p.DrawPos.x, p.DrawPos.z) - o;
                float door = plan.DoorOf(at);
                plan.doors.Add((at, door));
                last = Mathf.Max(last, door);
            }
            plan.caster = new Vector2(caster.DrawPos.x, caster.DrawPos.z) - o;
            plan.casterTime = CastleOpenPlan.CasterAfter(last);
            return plan;
        }

        // ---- the strum ---------------------------------------------------------------------------------------

        public InfinityCastleCast() { }

        /// <summary>
        /// The strum: chooses the pawns, makes the castle and holds everyone where they stand. Null, with
        /// the reason, when nobody is left to take or the castle could not be made; the caller gives the
        /// cooldown and the charge back.
        /// </summary>
        public static InfinityCastleCast Begin(Pawn caster, IntVec3 cell, float warmup, float paid, CompProperties_AbilityInfinityCastle props, out string why)
        {
            why = null;
            Map home = caster.Map;
            List<Pawn> pawns = Candidates(caster, home, cell, props);
            if (pawns.Count == 0)
            {
                why = "Infinity Castle: nobody hostile is left within " + props.radius.ToString("0.#") + " cells. No cooldown or charge spent.";
                return null;
            }
            Map castle = InfinityCastleMap.Make(home);
            if (castle == null)
            {
                why = "Infinity Castle: the castle could not be made. No cooldown or charge spent.";
                return null;
            }

            int now = Find.TickManager.TicksGame;
            var cast = new InfinityCastleCast
            {
                caster = caster, home = home, castle = castle, target = cell, paid = paid,
                strumAt = warmup, startTick = now - Mathf.RoundToInt(warmup * 60f),
                radius = props.radius, castleSeconds = props.castleSeconds,
            };
            cast.takePlan = TakePlan(caster, cell, pawns, props.radius, warmup);
            for (int i = 0; i < pawns.Count; i++)
                cast.taken.Add(new CastleTaken { pawn = pawns[i], kind = CastleGuest.Enemy, from = pawns[i].Position, at = cast.takePlan.doors[i].at, door = cast.takePlan.doors[i].time });
            cast.taken.Add(new CastleTaken { pawn = caster, kind = CastleGuest.Carrier, from = caster.Position, at = cast.takePlan.caster.Value, door = cast.takePlan.casterTime });

            // Everyone waits over their door until the carrier has gone through hers.
            int hold = cast.MoveTick - now + 2;
            foreach (Pawn p in pawns) p.stances?.stunner.StunFor(hold, caster, false, false);
            caster.stances?.stunner.StunFor(hold, null, false, false);
            if (Find.CurrentMap == home) Find.CameraDriver.shaker.DoShake(OT.StrumShake);
            return cast;
        }

        // ---- the move in ----------------------------------------------------------------------------------------

        private bool CarrierHolds(Map on) =>
            caster != null && !caster.Dead && !caster.Downed && caster.Spawned && caster.Map == on && Ability != null;

        /// <summary>Downed, gone or reverted before the move: nobody moves, the castle is removed, the cooldown and charge stay spent.</summary>
        private void Fizzle(string why)
        {
            fizzled = true;
            foreach (CastleTaken t in taken) if (t.pawn != null) InfinityCastleRide.Release(t.pawn);
            if (castle != null && Find.Maps.Contains(castle)) InfinityCastleMap.CloseLater(castle);
            if (why != null) Messages.Message(why, MessageTypeDefOf.NegativeEvent, false);
        }

        private void MoveIn(int now)
        {
            MapComponent_InfinityCastle component = Component;
            if (component == null)
            {
                Fizzle("Infinity Castle: the castle was gone before anyone went in.");
                return;
            }
            bool watching = Find.CurrentMap == home;
            var selected = new HashSet<Pawn>(Find.Selector.SelectedPawns);

            // Killed or gone while waiting over the door: left behind.
            foreach (CastleTaken t in taken.ToList())
            {
                if (t.kind != CastleGuest.Enemy) continue;
                if (t.pawn != null && !t.pawn.Dead && t.pawn.Spawned && t.pawn.Map == home) continue;
                if (t.pawn != null) InfinityCastleRide.Release(t.pawn);
                taken.Remove(t);
            }

            List<CastleTaken> enemies = taken.Where(t => t.kind == CastleGuest.Enemy).ToList();
            List<CastleRoom> rooms = component.Castle.ArrivalRooms(enemies.Count);
            var arrived = new List<Pawn>();
            var hostile = new List<Pawn>();
            for (int i = 0; i < enemies.Count; i++)
            {
                CastleTaken t = enemies[i];
                Pawn p = t.pawn;
                t.from = p.Position;
                p.carryTracker?.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
                t.lord = p.GetLord();
                t.lord?.Notify_PawnLost(p, PawnLostCondition.ExitedMap);
                CastleRoom room = rooms.Count > 0 ? rooms[i % rooms.Count] : component.Castle.Rooms[component.Castle.Rooms.Count - 1];
                InfinityCastleRide.Release(p);
                Move(p, component.LandingIn(room), castle);
                p.stances?.stunner.StunFor(Mathf.CeilToInt((IT.EnemyLands(i) + DoorThrough + IT.Rise) * 60f) + 6, caster, false, false);
                arrived.Add(p);
                if (p.Faction != null && p.HostileTo(Faction.OfPlayer)) hostile.Add(p);
            }

            CastleTaken carrier = taken.First(t => t.kind == CastleGuest.Carrier);
            carrier.from = caster.Position;
            InfinityCastleRide.Release(caster);
            Move(caster, component.DaisCell, castle);
            caster.stances?.stunner.StunFor(Mathf.CeilToInt((IT.CasterLands + DoorThrough + IT.Rise) * 60f), null, false, false);
            Play();

            component.Arrive(arrived, caster);
            Assault(hostile, castle);
            inTick = now;

            if (watching)
            {
                CameraJumper.TryJump(new GlobalTargetInfo(component.DaisCell, castle));
                foreach (Pawn p in arrived.Append(caster)) if (selected.Contains(p)) Find.Selector.Select(p, false, false);
            }
            Messages.Message("Infinity Castle: " + arrived.Count + " taken in for " + castleSeconds.ToString("0") + " s.", caster, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>The carrier sits on the dais and plays; the job cannot be interrupted by orders.</summary>
        private void Play()
        {
            if (caster?.jobs == null) return;
            caster.jobs.ClearQueuedJobs();
            caster.jobs.StartJob(JobMaker.MakeJob(InfinityCastleDefOf.AG_CastlePlay), JobCondition.InterruptForced, null, false, true);
        }

        // ---- the castle stands -----------------------------------------------------------------------------------

        /// <summary>
        /// Summon: a colonist on the home map comes up in the room under <paramref name="cell"/>, and goes
        /// home with everyone else to the cell it left. Refused while releasing, for a pawn not a free
        /// colonist of the home map, downed or in a mental state, or for the castle's own reasons.
        /// </summary>
        public bool Summon(Pawn colonist, IntVec3 cell, out string why)
        {
            why = null;
            MapComponent_InfinityCastle component = Component;
            if (!Standing || releasing || component == null) { why = "The castle is closing."; return false; }
            if (colonist == null || colonist == caster || colonist.Dead || colonist.Downed || !colonist.Spawned || colonist.Map != home
                || !colonist.IsColonist || colonist.InMentalState)
            {
                why = "Only a free colonist standing on the home map can be summoned.";
                return false;
            }
            IntVec3 from = colonist.Position;
            if (!component.TrySummon(colonist, cell, out why)) return false;
            taken.Add(new CastleTaken { pawn = colonist, kind = CastleGuest.Summoned, from = from });
            summonDoors.Add((new Vector2(from.x + 0.5f, from.z + 0.5f), Time.realtimeSinceStartup));
            return true;
        }

        /// <summary>The colonists Summon can bring: free colonists on the home map, up and in their right mind.</summary>
        public IEnumerable<Pawn> SummonChoices() =>
            home == null || !Find.Maps.Contains(home) ? Enumerable.Empty<Pawn>()
                : home.mapPawns.FreeColonistsSpawned.Where(p => p != caster && !p.Downed && !p.InMentalState);

        /// <summary>Everyone in the castle but the carrier, alive: the pawns taken first, in their order, then anyone else.</summary>
        private List<Pawn> Leaving()
        {
            var leaving = new List<Pawn>();
            if (castle == null || !Find.Maps.Contains(castle)) return leaving;
            foreach (CastleTaken t in taken)
                if (t.kind != CastleGuest.Carrier && t.pawn != null && !t.pawn.Dead && t.pawn.Spawned && t.pawn.Map == castle) leaving.Add(t.pawn);
            foreach (Pawn p in castle.mapPawns.AllPawnsSpawned)
                if (p != caster && !p.Dead && !leaving.Contains(p)) leaving.Add(p);
            return leaving;
        }

        private void BeginRelease()
        {
            releasing = true;
            MapComponent_InfinityCastle component = Component;
            if (component == null) return;
            List<Pawn> leaving = Leaving();
            // Everyone stops where they stand until their door has taken them.
            for (int i = 0; i < leaving.Count; i++)
                if (!leaving[i].Downed) leaving[i].stances?.stunner.StunFor(Mathf.CeilToInt((IT.EnemyOut(i) - IT.Release + DoorThrough + IT.Sink) * 60f) + 30, null, false, false);
            component.Release(leaving, caster != null && caster.Spawned && caster.Map == castle ? caster : null);
        }

        // ---- the return ------------------------------------------------------------------------------------------

        private void Return(int now)
        {
            returned = true;
            returnTick = now;
            Map to = home != null && Find.Maps.Contains(home) ? home : Find.AnyPlayerHomeMap;
            if (castle == null || !Find.Maps.Contains(castle)) return;
            if (to == null)
            {
                Messages.Message("Infinity Castle: there is no map to return to; the castle stays.", MessageTypeDefOf.NegativeEvent, false);
                return;
            }
            IntVec3 drop = to == home ? target : to.Center;
            var o = new Vector2(drop.x + 0.5f, drop.z + 0.5f);
            Vector2 Offset(IntVec3 c) => new Vector2(c.x + 0.5f, c.z + 0.5f) - o;
            returnPlan = new CastleOpenPlan { take = false, radius = radius, casterShaft = true };
            bool watching = Find.CurrentMap == castle;
            var selected = new HashSet<Pawn>(Find.Selector.SelectedPawns);
            var moved = new List<Pawn>();
            var leavingMap = new List<Pawn>();
            int k = 0;

            void Back(Pawn p, IntVec3 want, CastleTaken t)
            {
                InfinityCastleRide.Release(p);
                p.GetLord()?.RemovePawn(p);
                IntVec3 cell = FreeCellNear(to, want);
                Move(p, cell, to);
                moved.Add(p);
                float door = t?.kind == CastleGuest.Carrier ? OT.CasterBackDoor : OT.BackDoorOf(k++);
                if (t?.kind == CastleGuest.Carrier) { returnPlan.caster = Offset(cell); returnPlan.casterTime = door; }
                else returnPlan.doors.Add((Offset(cell), door));
                if (t != null) { t.back = door; t.up = false; }
                // Hidden until its door is half open; held until it is up.
                InfinityCastleRide.Hide(p);
                p.stances?.stunner.StunFor(Mathf.CeilToInt((door + DoorThrough + OT.Rise) * 60f), null, false, false);
            }

            // Everyone alive, back where they came from. A pawn carried by another is inside its carrier and goes with it.
            foreach (CastleTaken t in taken)
            {
                Pawn p = t.pawn;
                if (p == null || p.Destroyed || p.Dead || !p.Spawned || p.Map != castle) continue;
                Back(p, to == home ? t.from : drop, t);
                if (t.kind != CastleGuest.Enemy) continue;
                if (t.lord != null && to.lordManager.lords.Contains(t.lord) && t.lord.CanAddPawn(p)) t.lord.AddPawn(p);
                else if (p.Faction != null && p.Faction != Faction.OfPlayer) leavingMap.Add(p);
            }
            // Anyone else in the castle by now, round the target cell.
            foreach (Pawn p in castle.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.Dead) continue;
                Back(p, drop, null);
                if (p.Faction != null && p.Faction != Faction.OfPlayer && p.GetLord() == null) leavingMap.Add(p);
            }
            // Corpses, dropped weapons and everything else lying in the castle, round the target cell; a door for each corpse.
            int c = 0;
            foreach (Thing thing in castle.listerThings.AllThings.ToList())
            {
                if (thing.Destroyed || !thing.Spawned || thing.def.category != ThingCategory.Item) continue;
                thing.DeSpawn();
                if (GenPlace.TryPlaceThing(thing, drop, to, ThingPlaceMode.Near) && thing is Corpse && thing.Spawned)
                    returnPlan.doors.Add((Offset(thing.Position), OT.CorpseDoor + 0.07f * c++));
            }
            foreach (IGrouping<Faction, Pawn> group in leavingMap.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_ExitMapBest(LocomotionUrgency.Jog), to, group);

            if (watching)
            {
                CameraJumper.TryJump(new GlobalTargetInfo(drop, to));
                foreach (Pawn p in moved) if (selected.Contains(p)) Find.Selector.Select(p, false, false);
            }
            InfinityCastleMap.CloseLater(castle);
        }

        /// <summary>Each pawn brought home is shown once its door is half open.</summary>
        private void ShowReturned(int now)
        {
            foreach (CastleTaken t in taken)
            {
                if (t.up || t.back < 0f || t.pawn == null) continue;
                if (now < returnTick + Mathf.CeilToInt((t.back + DoorThrough) * 60f)) continue;
                t.up = true;
                InfinityCastleRide.Release(t.pawn);
            }
            // Anyone else brought home has no entry: shown with the first doors.
            if (returnPlan != null && now >= returnTick + Mathf.CeilToInt((OT.First + DoorThrough) * 60f))
                foreach (Pawn p in home?.mapPawns?.AllPawnsSpawned ?? new List<Pawn>())
                    if (InfinityCastleRide.Hidden(p) && !taken.Any(t => t.pawn == p)) InfinityCastleRide.Release(p);
        }

        // ---- moving pawns ------------------------------------------------------------------------------------------

        /// <summary>Takes a pawn off its map and puts it on another, keeping it drafted if it was.</summary>
        private static void Move(Pawn p, IntVec3 cell, Map to)
        {
            bool drafted = p.Drafted;
            Rot4 facing = p.Rotation;
            p.DeSpawnOrDeselect();
            GenSpawn.Spawn(p, cell, to, facing);
            p.Notify_Teleported(true, true);
            if (drafted && p.drafter != null && !p.Downed) p.drafter.Drafted = true;
        }

        /// <summary>The nearest cell to <paramref name="want"/> a pawn can stand on with no other pawn on it.</summary>
        private static IntVec3 FreeCellNear(Map map, IntVec3 want)
        {
            want = new IntVec3(Mathf.Clamp(want.x, 1, map.Size.x - 2), 0, Mathf.Clamp(want.z, 1, map.Size.z - 2));
            int cells = GenRadial.NumCellsInRadius(8f);
            for (int i = 0; i < cells; i++)
            {
                IntVec3 c = want + GenRadial.RadialPattern[i];
                if (c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null) return c;
            }
            return CellFinder.StandableCellNear(want, map, 20f);
        }

        /// <summary>Hostile pawns fight on: an assault lord for each faction, with no fleeing and no kidnapping.</summary>
        private static void Assault(List<Pawn> pawns, Map map)
        {
            foreach (IGrouping<Faction, Pawn> group in pawns.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_AssaultColony(group.Key, false, false, false, false, false), map, group);
        }

        // ---- the clock ------------------------------------------------------------------------------------------

        /// <summary>One game tick. False once the cast is over and its picture has faded, so it can be dropped.</summary>
        public bool Tick(int now)
        {
            if (fizzled) return false;
            if (Taking)
            {
                if (!CarrierHolds(home))
                {
                    Fizzle(caster == null ? null : "Infinity Castle: " + caster.LabelShortCap + " lost the castle before it opened.");
                    return false;
                }
                // Each pawn is gone once it has sunk through its door; the carrier last.
                foreach (CastleTaken t in taken)
                    if (t.pawn != null && t.pawn.Spawned && t.pawn.Map == home && now >= TickAt(t.door + DoorThrough + OT.Sink))
                        InfinityCastleRide.Hide(t.pawn);
                if (now >= MoveTick) MoveIn(now);
                return !fizzled;
            }
            if (Standing)
            {
                MapComponent_InfinityCastle component = Component;
                if (component == null)
                {
                    // The castle went some other way: whoever was in it went with it.
                    returned = true;
                    returnTick = now;
                    return true;
                }
                if (!releasing)
                {
                    // The debug window's "castle map: release" releases the map itself: the cast follows.
                    if (releaseOrdered || component.Released || now - inTick >= Mathf.RoundToInt(castleSeconds * 60f) || !CarrierHolds(castle)) BeginRelease();
                    else if (!caster.InMentalState && caster.CurJobDef != InfinityCastleDefOf.AG_CastlePlay) Play();
                    return true;
                }
                // A castle saved during its Release is released again after loading.
                if (!component.Released) component.Release(Leaving(), caster != null && caster.Spawned && caster.Map == castle ? caster : null);
                if (component.Faded) Return(now);
                return true;
            }
            ShowReturned(now);
            return returnPlan != null && (now - returnTick) / 60f < returnPlan.Duration;
        }

        // ---- the home side's picture ------------------------------------------------------------------------------

        public void Draw()
        {
            if (fizzled || home == null) return;
            if (Taking || (inTick >= 0 && !returned))
            {
                if (takePlan == null) return;
                InfinityCastleOpenGraphics.Draw(Origin, takePlan, UbwClock.Since(startTick), home);
            }
            else if (returnPlan != null) InfinityCastleOpenGraphics.Draw(Origin, returnPlan, UbwClock.Since(returnTick), home);

            // Summon's door on the home map: it opens under the colonist, who is already gone.
            CastleLayers layers = CastleLayers.Pocket;
            for (int i = summonDoors.Count - 1; i >= 0; i--)
            {
                var (at, when) = summonDoors[i];
                float age = Time.realtimeSinceStartup - when - 0.1f;
                if (age > DoorEnd(OT.Sink + 0.05f)) { summonDoors.RemoveAt(i); continue; }
                if (!VfxDraw.Shown(at, home)) continue;
                VfxDraw.Begin(at);
                DoorAt(age, OT.Sink + 0.05f, out float alpha, out float open);
                FloorDoor(at, open, alpha, age, layers.Door);
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster", true);
            Scribe_References.Look(ref home, "home");
            Scribe_References.Look(ref castle, "castle");
            Scribe_Values.Look(ref target, "target");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref strumAt, "strumAt", 1f);
            Scribe_Values.Look(ref radius, "radius", OT.Radius);
            Scribe_Values.Look(ref castleSeconds, "castleSeconds", 60f);
            Scribe_Values.Look(ref paid, "paid");
            Scribe_Values.Look(ref inTick, "inTick", -1);
            Scribe_Values.Look(ref returnTick, "returnTick", -1);
            Scribe_Values.Look(ref releaseOrdered, "releaseOrdered");
            Scribe_Values.Look(ref releasing, "releasing");
            Scribe_Values.Look(ref returned, "returned");
            Scribe_Values.Look(ref fizzled, "fizzled");
            Scribe_Collections.Look(ref taken, "taken", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (taken == null) taken = new List<CastleTaken>();
                taken.RemoveAll(t => t == null || t.pawn == null);
                if (Taking && caster != null)
                {
                    // The take picture again from what was saved; nobody is hidden until the next tick.
                    var plan = new CastleOpenPlan { take = true, radius = radius, strumAt = strumAt, casterShaft = true };
                    foreach (CastleTaken t in taken)
                        if (t.kind == CastleGuest.Enemy) plan.doors.Add((t.at, t.door));
                        else if (t.kind == CastleGuest.Carrier) { plan.caster = t.at; plan.casterTime = t.door; }
                    takePlan = plan;
                }
            }
        }
    }
}
