using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.CastleEffectGraphics;
using T = RimArt.InfinityCastleInsideTiming;

namespace RimArt
{
    /// <summary>
    /// Draws the Infinity Castle on its own pocket map, with the same code as the lab's recordings: the
    /// void and its rooms at other depths, the rooms at rest (baked), the lantern glows, and the castle's
    /// timeline at the map's own layers (<see cref="CastleLayers.Pocket"/>). It plays the arrival doors
    /// once when the castle opens, waits in the hold with nothing but the castle on screen, and plays
    /// Release when asked; when Release has faded to black the map is closed.
    ///
    /// It also owns the castle's layout, which the commands change: Shift moves a room
    /// (<see cref="CastleShift"/>), so after the first one the castle is no longer its seed's and the
    /// rooms' places are saved. Every map has one of these (vanilla makes every MapComponent everywhere);
    /// it does nothing unless its map is a castle (a seed is set) and is the map on screen.
    /// </summary>
    public sealed class MapComponent_InfinityCastle : MapComponent
    {
        public int seed, rooms;
        public Map source;
        private float seconds, releasedAt = -1f;
        private bool closing;
        private CastleLayout castle;
        private CastleRoomGraphics.CastleBatch batch;
        /// <summary>The rooms' places, x then z per room, once a command has moved one; empty for a castle as generated.</summary>
        private List<int> roomPlaces = new List<int>();
        private CastleShift shift;
        private List<CastleDrop> drops = new List<CastleDrop>();
        private CastleCrush crush;
        private float lastCrushAt = -1000f;
        /// <summary>Pawns hit by the last crush, for the stun stars over them.</summary>
        private readonly List<Pawn> crushed = new List<Pawn>();
        /// <summary>Sealed doorways, by key (A-B): shut, barred, and walled in both cells.</summary>
        private List<string> sealedDoors = new List<string>();
        /// <summary>Seals and openings still being drawn: the leaves and the bar on the move.</summary>
        private readonly List<CastleSealAnim> seals = new List<CastleSealAnim>();
        private float lastStrumAt = -1000f;
        private const float Backstop = 700f;

        public MapComponent_InfinityCastle(Map map) : base(map) { }

        public bool IsCastle => seed > 0;

        /// <summary>The castle as it is now: generated from its seed, then moved room by room by the commands.</summary>
        internal CastleLayout Castle
        {
            get
            {
                if (castle != null || !IsCastle) return castle;
                castle = CastleRoomGraphics.LayoutFor(seed, rooms);
                if (roomPlaces.Count == castle.Rooms.Count * 2)
                {
                    List<CastleRoom> placed = castle.Rooms.Select(r => new CastleRoom { Id = r.Id, Kind = r.Kind, W = r.W, H = r.H, X = roomPlaces[r.Id * 2], Z = roomPlaces[r.Id * 2 + 1] }).ToList();
                    castle = CastleLayout.FromRooms(placed, seed);
                }
                return castle;
            }
        }

        /// <summary>The room drawn on its own this frame, or -1: a sliding room, until it stops.</summary>
        private int SlidingRoom => shift != null && shift.picture && !shift.stopped ? shift.room : -1;

        private CastleRoomGraphics.CastleBatch Batch => batch ?? (batch = CastleRoomGraphics.CastleBatch.Build(Castle, SlidingRoom, sealedDoors));

        /// <summary>The carrier's cell on the dais.</summary>
        public IntVec3 DaisCell
        {
            get
            {
                var (x, z) = CastleLayout.SeatOf(Castle.Biwa);
                return new IntVec3((int)x, 0, (int)z);
            }
        }

        /// <summary>Called by the GenStep: this map is the castle for this seed and room count.</summary>
        public void Begin(int seed, int rooms)
        {
            this.seed = seed;
            this.rooms = rooms;
            castle = null;
            batch = null;
            roomPlaces.Clear();
            shift = null;
            drops.Clear();
            sealedDoors.Clear();
            seals.Clear();
            crush = null;
            crushed.Clear();
            seconds = 0f;
            releasedAt = -1f;
            closing = false;
        }

        /// <summary>Plays Release: every room answers the strum, the doors, the fade; then the map closes.</summary>
        public void Release()
        {
            if (IsCastle && releasedAt < 0f) releasedAt = seconds;
        }

        // ---- Shift --------------------------------------------------------------------------------------

        /// <summary>
        /// Shift the room under <paramref name="cell"/> one of the four ways (dx, dz one of -1, 0, 1). False,
        /// with the reason, when there is no room there, it is the biwa room, the last strum was too
        /// recent, a room is still sliding, or the room cannot move that way at all.
        /// </summary>
        public bool TryShift(IntVec3 cell, int dx, int dz, out string why)
        {
            why = null;
            if (!IsCastle || closing || releasedAt >= 0f) { why = "The castle is closing."; return false; }
            CastleRoom room = Castle.RoomAt(cell.x, cell.z);
            if (room == null) { why = "No room there."; return false; }
            if (room.Kind == CastleKind.Biwa) { why = "The biwa room does not move."; return false; }
            if (shift != null && !shift.stopped) { why = "A room is still sliding."; return false; }
            if (seconds - lastStrumAt < InfinityCastleRules.Of.strumGapSeconds) { why = "The last strum is still sounding."; return false; }
            int d = Castle.SlideDistance(room.Id, dx, dz, InfinityCastleRules.Of.shiftMaxCells, out bool blocked);
            if (d == 0) { why = "The room cannot move that way."; return false; }
            ApplyShift(room, dx, dz, d, blocked);
            return true;
        }

        /// <summary>
        /// The strum: the cells move now. The room's things are lifted out, its old cells go back to void,
        /// its new cells get floor, walls (but the new doorways' cells) and lanterns, the neighbours' side
        /// of every broken doorway is walled, and the things are put down at their new cells; pawns are
        /// stunned for the slide and drawn behind their cells by the picture's offset. A pawn in a breaking
        /// doorway takes the Void rule instead.
        /// </summary>
        private void ApplyShift(CastleRoom room, int dx, int dz, int d, bool blocked)
        {
            var step = InfinityCastleDefOf.AG_InfinityCastleRooms?.genStep as GenStep_InfinityCastle;
            if (step == null) return;
            CastleLayout before = Castle, after = before.Moved(room.Id, dx * d, dz * d);
            CastleLayout.DoorChanges(before, after, out _, out List<CastleDoorway> broken, out List<CastleDoorway> made);
            var moved = new CastleShift
            {
                room = room.Id, dx = dx, dz = dz, distance = d, blocked = blocked, startAt = seconds,
                oldX = room.X, oldZ = room.Z, broken = broken, made = made, picture = true,
            };
            var offset = new IntVec3(dx * d, 0, dz * d);
            var oldRect = new CellRect(room.X, room.Z, room.W, room.H);
            CastleRoom landed = after.Rooms[room.Id];
            var newRect = new CellRect(landed.X, landed.Z, landed.W, landed.H);
            var madeCells = new HashSet<IntVec3>();
            foreach (CastleDoorway door in made) foreach (var (x, z) in door.Cells) madeCells.Add(new IntVec3(x, 0, z));

            // The Void rule first: whoever stands in a breaking doorway is not in the room any more.
            foreach (CastleDoorway door in broken)
                foreach (var (x, z) in door.Cells)
                    foreach (Pawn pawn in new IntVec3(x, 0, z).GetThingList(map).OfType<Pawn>().ToList())
                        VoidDrop(pawn, room.Id);

            // Lift everything out of the room: pawns, items, corpses; filth is left behind and swept.
            var lifted = new List<(Thing thing, IntVec3 cell)>();
            foreach (IntVec3 cell in oldRect)
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map).ToList())
                {
                    if (thing.def == step.wall || thing.def == step.lantern) continue;
                    if (thing.def.category == ThingCategory.Filth) { thing.Destroy(); continue; }
                    if (thing.def.category != ThingCategory.Pawn && thing.def.category != ThingCategory.Item) continue;
                    if (thing is Pawn p) p.DeSpawnOrDeselect(); else thing.DeSpawn();
                    lifted.Add((thing, cell));
                }
            }

            // The old cells back to void; the new cells built. Rooms never overlap, so a neighbour's
            // walls are never in either rectangle.
            foreach (IntVec3 cell in oldRect)
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map).ToList())
                    // The wall and the lantern are indestructible (destroyable false): they leave the map by despawning.
                    if (thing.def == step.wall || thing.def == step.lantern) thing.DeSpawn();
                map.terrainGrid.SetTerrain(cell, step.voidTerrain);
            }
            foreach (IntVec3 cell in newRect)
            {
                if (!cell.InBounds(map)) continue;
                map.terrainGrid.SetTerrain(cell, step.floorTerrain);
                if (step.wall != null && landed.IsWall(cell.x, cell.z) && !madeCells.Contains(cell))
                    GenSpawn.Spawn(ThingMaker.MakeThing(step.wall), cell, map);
            }
            if (step.lantern != null)
            {
                double cx = landed.X + landed.W / 2.0, cz = landed.Z + landed.H / 2.0;
                foreach (var (lx, lz) in CastleLayout.LanternsOf(landed))
                {
                    var cell = new IntVec3((int)System.Math.Floor(cx + lx), 0, (int)System.Math.Floor(cz + lz));
                    if (cell.InBounds(map) && cell.GetFirstThing(map, step.lantern) == null) GenSpawn.Spawn(ThingMaker.MakeThing(step.lantern), cell, map);
                }
            }
            // The neighbours' side of each broken doorway is walled; the far side of each new one stays
            // walled until the stop.
            foreach (CastleDoorway door in broken)
                foreach (var (x, z) in door.Cells)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (oldRect.Contains(cell) || newRect.Contains(cell) || !cell.InBounds(map) || step.wall == null) continue;
                    if (cell.GetFirstThing(map, step.wall) == null) GenSpawn.Spawn(ThingMaker.MakeThing(step.wall), cell, map);
                }
            foreach (IntVec3 cell in madeCells)
            {
                if (newRect.Contains(cell) || !cell.InBounds(map) || step.wall == null) continue;
                if (cell.GetFirstThing(map, step.wall) == null) GenSpawn.Spawn(ThingMaker.MakeThing(step.wall), cell, map);
                moved.madeCells.Add(cell);
            }

            // Everything put down where it now is; pawns ride the picture.
            int slideTicks = Mathf.CeilToInt(moved.SlideTime * 60f) + 6;
            foreach (var (thing, cell) in lifted)
            {
                IntVec3 at = cell + offset;
                if (!at.InBounds(map)) at = landed.Contains(cell.x, cell.z) ? cell : newRect.CenterCell;
                GenSpawn.Spawn(thing, at, map);
                if (thing is Pawn pawn)
                {
                    pawn.Notify_Teleported(true, true);
                    pawn.stances?.stunner.StunFor(slideTicks, null, false, false);
                    moved.riders.Add(pawn);
                }
            }

            foreach (CastleDoorway door in broken) sealedDoors.Remove(door.Key);
            castle = after;
            roomPlaces = after.Rooms.SelectMany(r => new[] { r.X, r.Z }).ToList();
            shift = moved;
            batch = null;
            moved.movingRoom = CastleRoomGraphics.CastleBatch.BuildRoom(landed);
            lastStrumAt = seconds;
            if (blocked) moved.seam = SeamWith(before, room, dx, dz, d);
        }

        /// <summary>Where the slid room's leading wall meets the room that stopped it: the dust line's ends, in cells.</summary>
        private static (Vector2 a, Vector2 b)? SeamWith(CastleLayout before, CastleRoom room, int dx, int dz, int d)
        {
            var next = new CastleRoom { X = room.X + dx * (d + 1), Z = room.Z + dz * (d + 1), W = room.W, H = room.H };
            CastleRoom other = before.Rooms.FirstOrDefault(q => q.Id != room.Id && CastleLayout.Overlaps(next, q));
            if (other == null) return null;
            int x = room.X + dx * d, z = room.Z + dz * d;
            if (dx != 0)
            {
                float seamX = dx > 0 ? x + room.W : x;
                float z0 = Mathf.Max(z, other.Z), z1 = Mathf.Min(z + room.H, other.Z + other.H);
                return (new Vector2(seamX, z0 + 0.5f), new Vector2(seamX, z1 - 0.5f));
            }
            float seamZ = dz > 0 ? z + room.H : z;
            float x0 = Mathf.Max(x, other.X), x1 = Mathf.Min(x + room.W, other.X + other.W);
            return (new Vector2(x0 + 0.5f, seamZ), new Vector2(x1 - 0.5f, seamZ));
        }

        // ---- Crush ---------------------------------------------------------------------------------------

        /// <summary>
        /// Crush the room under <paramref name="cell"/>: its walls slam crushBand cells in and draw back, and
        /// every pawn in that band of the floor takes crushDamage blunt and a stun. Refused for the biwa
        /// room, a room under crushMinSize each way, during its own cooldown, while a room slides, or
        /// before the last strum has faded.
        /// </summary>
        public bool TryCrush(IntVec3 cell, out string why)
        {
            why = null;
            if (!IsCastle || closing || releasedAt >= 0f) { why = "The castle is closing."; return false; }
            CastleRoom room = Castle.RoomAt(cell.x, cell.z);
            if (room == null) { why = "No room there."; return false; }
            if (room.Kind == CastleKind.Biwa) { why = "The biwa room is never crushed."; return false; }
            InfinityCastleRules rules = InfinityCastleRules.Of;
            if (room.W < rules.crushMinSize || room.H < rules.crushMinSize) { why = $"Too small: a room must be {rules.crushMinSize} x {rules.crushMinSize} or more."; return false; }
            if (shift != null && !shift.stopped) { why = "A room is still sliding."; return false; }
            if (crush != null && !crush.hitDone) { why = "The walls are still moving."; return false; }
            if (seconds - lastCrushAt < rules.crushCooldownSeconds) { why = $"Crush is not ready: {Mathf.CeilToInt(rules.crushCooldownSeconds - (seconds - lastCrushAt))} s."; return false; }
            if (seconds - lastStrumAt < rules.strumGapSeconds) { why = "The last strum is still sounding."; return false; }
            crush = new CastleCrush { room = room.Id, startAt = seconds };
            crushed.Clear();
            lastStrumAt = lastCrushAt = seconds;
            return true;
        }

        /// <summary>The floor cells a crush hits: the outer <paramref name="band"/> cells inside the wall ring.</summary>
        private static bool InBand(CastleRoom room, int x, int z, int band)
        {
            int fx0 = room.X + 1, fz0 = room.Z + 1, fx1 = room.X + room.W - 2, fz1 = room.Z + room.H - 2;
            if (x < fx0 || x > fx1 || z < fz0 || z > fz1) return false;
            return x - fx0 < band || fx1 - x < band || z - fz0 < band || fz1 - z < band;
        }

        /// <summary>The impact: everyone in the band, own pawns included, takes the blow and is stunned; the camera shakes.</summary>
        private void LandCrush()
        {
            crush.hitDone = true;
            CastleRoom room = Castle.Rooms[crush.room];
            InfinityCastleRules rules = InfinityCastleRules.Of;
            var centre = new Vector3(room.X + room.W / 2f, 0f, room.Z + room.H / 2f);
            foreach (IntVec3 cell in new CellRect(room.X, room.Z, room.W, room.H))
            {
                if (!cell.InBounds(map) || !InBand(room, cell.x, cell.z, rules.crushBand)) continue;
                foreach (Pawn pawn in cell.GetThingList(map).OfType<Pawn>().ToList())
                {
                    float angle = (centre - pawn.DrawPos).AngleFlat();
                    pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, rules.crushDamage, 0f, angle));
                    if (!pawn.Dead) pawn.stances?.stunner.StunFor(Mathf.CeilToInt(rules.crushStunSeconds * 60f), null, false, false);
                    crushed.Add(pawn);
                }
            }
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.06f);
        }

        // ---- Seal and Open ---------------------------------------------------------------------------------

        /// <summary>The doorway with a door cell at <paramref name="cell"/>, or null.</summary>
        private CastleDoorway DoorwayAt(IntVec3 cell) =>
            Castle.Doorways.FirstOrDefault(d => d.Cells[0] == (cell.x, cell.z) || d.Cells[1] == (cell.x, cell.z));

        /// <summary>
        /// Seal the doorway with a door cell at <paramref name="cell"/>: its leaves shut, a bar across, a wall
        /// in both cells. Refused for the biwa room's own doorways, a doorway already sealed, one with a
        /// pawn standing in it, while a room slides, or before the last strum has faded.
        /// </summary>
        public bool TrySeal(IntVec3 cell, out string why) => TrySealOrOpen(cell, true, out why);

        /// <summary>Open a sealed doorway again: the bar slides away, the leaves open, the walls go.</summary>
        public bool TryOpen(IntVec3 cell, out string why) => TrySealOrOpen(cell, false, out why);

        private bool TrySealOrOpen(IntVec3 cell, bool seal, out string why)
        {
            why = null;
            if (!IsCastle || closing || releasedAt >= 0f) { why = "The castle is closing."; return false; }
            CastleDoorway door = DoorwayAt(cell);
            if (door == null) { why = "No doorway there: click one of its two door cells."; return false; }
            if (door.A == Castle.Biwa.Id || door.B == Castle.Biwa.Id) { why = "The biwa room's doorways cannot be sealed."; return false; }
            bool sealedNow = sealedDoors.Contains(door.Key);
            if (seal && sealedNow) { why = "That doorway is already sealed."; return false; }
            if (!seal && !sealedNow) { why = "That doorway is not sealed."; return false; }
            if (shift != null && !shift.stopped) { why = "A room is still sliding."; return false; }
            if (seconds - lastStrumAt < InfinityCastleRules.Of.strumGapSeconds) { why = "The last strum is still sounding."; return false; }
            var step = InfinityCastleDefOf.AG_InfinityCastleRooms?.genStep as GenStep_InfinityCastle;
            if (step?.wall == null) return false;
            var cells = new[] { new IntVec3(door.Cells[0].x, 0, door.Cells[0].z), new IntVec3(door.Cells[1].x, 0, door.Cells[1].z) };
            if (seal && cells.Any(c => c.InBounds(map) && c.GetThingList(map).Any(t => t is Pawn))) { why = "Someone is standing in that doorway."; return false; }

            foreach (IntVec3 c in cells)
            {
                if (!c.InBounds(map)) continue;
                if (seal) { if (c.GetFirstThing(map, step.wall) == null) GenSpawn.Spawn(ThingMaker.MakeThing(step.wall), c, map); }
                else c.GetFirstThing(map, step.wall)?.DeSpawn();
            }
            if (seal) sealedDoors.Add(door.Key); else sealedDoors.Remove(door.Key);
            seals.RemoveAll(a => a.key == door.Key);
            seals.Add(new CastleSealAnim { key = door.Key, door = door, sealing = seal, startAt = seconds });
            lastStrumAt = seconds;
            batch = null;
            return true;
        }

        // ---- Drop and the Void rule ---------------------------------------------------------------------

        /// <summary>
        /// Drop: <paramref name="pawn"/> goes through the floor and comes up in the room under
        /// <paramref name="cell"/>. No damage; stunned only while in transit. Refused for a pawn not in the
        /// castle, no room under the cell, the pawn's own room, while a room slides, or before the last
        /// strum has faded.
        /// </summary>
        public bool TryDrop(Pawn pawn, IntVec3 cell, out string why)
        {
            why = null;
            if (!IsCastle || closing || releasedAt >= 0f) { why = "The castle is closing."; return false; }
            if (pawn == null || !pawn.Spawned || pawn.Map != map) { why = "That pawn is not in the castle."; return false; }
            CastleRoom room = Castle.RoomAt(cell.x, cell.z);
            if (room == null) { why = "No room there."; return false; }
            if (Castle.RoomAt(pawn.Position.x, pawn.Position.z) == room) { why = "The pawn is already in that room."; return false; }
            if (shift != null && !shift.stopped) { why = "A room is still sliding."; return false; }
            if (seconds - lastStrumAt < InfinityCastleRules.Of.strumGapSeconds) { why = "The last strum is still sounding."; return false; }
            if (drops.Any(d => d.pawn == pawn)) { why = "That pawn is already falling."; return false; }
            DropPawn(pawn, room, 0.1f, true, 0f);
            lastStrumAt = seconds;
            return true;
        }

        /// <summary>
        /// The Void rule: <paramref name="pawn"/> drops through the void and comes up in a random room
        /// (not the biwa room, not <paramref name="notRoom"/>) through a floor door, unharmed but stunned
        /// a moment. No strum: the castle does this on its own.
        /// </summary>
        public void VoidDrop(Pawn pawn, int notRoom = -1)
        {
            List<CastleRoom> choices = Castle.Rooms.Where(r => r.Kind != CastleKind.Biwa && r.Id != notRoom).ToList();
            if (choices.Count == 0 || !pawn.Spawned) return;
            DropPawn(pawn, choices.RandomElement(), 0f, false, InfinityCastleRules.Of.voidDropStunSeconds);
        }

        /// <summary>
        /// The move, now: the pawn goes to a free floor cell near the room's middle and is stunned for the
        /// transit (plus <paramref name="stunAfter"/>); the drop's picture then plays over it.
        /// </summary>
        private void DropPawn(Pawn pawn, CastleRoom room, float doorUnder, bool strum, float stunAfter)
        {
            IntVec3 from = pawn.Position, middle = new IntVec3(room.X + room.W / 2, 0, room.Z + room.H / 2);
            bool Free(IntVec3 c) => c.InBounds(map) && !room.IsWall(c.x, c.z) && room.Contains(c.x, c.z) && c.Standable(map) && !c.GetThingList(map).Any(t => t is Pawn);
            IntVec3 to = Free(middle) ? middle : CellFinder.TryFindRandomCellNear(middle, map, 3, Free, out IntVec3 near) ? near : middle;
            var drop = new CastleDrop
            {
                pawn = pawn, from = from, to = to, startAt = seconds, doorUnder = doorUnder, strum = strum,
                fromRoom = Castle.RoomAt(from.x, from.z)?.Id ?? -1, toRoom = room.Id,
            };
            pawn.DeSpawnOrDeselect();
            GenSpawn.Spawn(pawn, to, map);
            pawn.Notify_Teleported(true, true);
            pawn.stances?.stunner.StunFor(Mathf.CeilToInt((drop.Arrive + CastleDrop.Rise + stunAfter) * 60f), null, false, false);
            InfinityCastleRide.Ride(pawn, new Vector2(from.x - to.x, from.z - to.z));
            drops.Add(drop);
        }

        // ---- ticking and drawing --------------------------------------------------------------------------

        /// <summary>
        /// The slide's bookkeeping, on the map's clock so it happens paused or not: the stop (the far side
        /// of each new doorway opens, the riders are let go, the room joins the baked castle), the end of
        /// the picture, and each Void drop's landing.
        /// </summary>
        private void Advance()
        {
            if (shift != null)
            {
                float t = shift.AgeAt(seconds);
                if (!shift.stopped && t >= shift.StopAt)
                {
                    var step = InfinityCastleDefOf.AG_InfinityCastleRooms?.genStep as GenStep_InfinityCastle;
                    foreach (IntVec3 cell in shift.madeCells)
                        if (step?.wall != null && cell.InBounds(map)) cell.GetFirstThing(map, step.wall)?.DeSpawn();
                    foreach (Pawn rider in shift.riders) InfinityCastleRide.Release(rider);
                    shift.riders.Clear();
                    shift.stopped = true;
                    batch = null;
                }
                if (t >= shift.EndAt || !shift.picture && shift.stopped) { shift = null; batch = null; }
            }
            seals.RemoveAll(a => seconds - a.startAt > CastleSealAnim.Length);
            if (crush != null)
            {
                float t = crush.AgeAt(seconds);
                if (!crush.hitDone && t >= CastleCrush.Hit) LandCrush();
                if (t >= CastleCrush.Length) { crush = null; crushed.Clear(); }
            }
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                // Where the pawn is drawn: back at its old cell while it sinks, nowhere in between, then up.
                CastleDrop drop = drops[i];
                if (drop.pawn == null || drop.DoneAt(seconds)) { if (drop.pawn != null) InfinityCastleRide.Release(drop.pawn); drops.RemoveAt(i); continue; }
                float t = drop.AgeAt(seconds);
                if (t < drop.SinkEnd) InfinityCastleRide.Ride(drop.pawn, new Vector2(drop.from.x - drop.to.x, drop.from.z - drop.to.z));
                else if (t < drop.Arrive + DoorThrough) InfinityCastleRide.Hide(drop.pawn);
                else InfinityCastleRide.Release(drop.pawn);
            }
        }

        public override void MapComponentUpdate()
        {
            if (!IsCastle || closing || Find.CurrentMap != map) return;
            // Unscaled, as the previews: the lanterns, the drift and the commands go on while the game is paused.
            seconds += Time.unscaledDeltaTime;
            Advance();
            float timeline = releasedAt < 0f ? Mathf.Min(seconds, T.Quiet) : T.Release + (seconds - releasedAt);
            if (timeline >= T.Duration)
            {
                closing = true;
                InfinityCastleMap.CloseLater(map);
                return;
            }
            // The generator def turns the game's grey map-edge frame off (disableMapClippers), so the
            // void plane (270 cells) and the depth rooms show past the edge. This plane, under it, is
            // what the far corners see at full zoom-out, where the camera reaches ~110 cells outside.
            DrawMesh(MeshPool.plane10, new Vector2(map.Size.x / 2f, map.Size.z / 2f), CastleLayers.Pocket.Back - 0.002f,
                Backstop, Backstop, 0f, CastleRoomGraphics.VoidDeep, solid);
            CellRect view = Find.CameraDriver.CurrentViewRect;
            InfinityCastleInsideGraphics.Draw(Castle, Vector2.zero, timeline, seconds, false, CastleLayers.Pocket, view, Batch, SlidingRoom);
            if (shift != null && shift.picture) DrawShift(view);
            DrawSeals();
            if (crush != null) DrawCrush();
            DrawDrops();
        }

        /// <summary>The Shift sketch's picture over the real cells: the room sliding from where it was, its doors, the dust.</summary>
        private void DrawShift(CellRect view)
        {
            CastleLayers layers = CastleLayers.Pocket;
            CastleShift s = shift;
            float t = s.AgeAt(seconds);
            CastleRoom room = Castle.Rooms[s.room];
            Vector2 behind = s.Behind(t), slid = new Vector2(s.dx * s.Slid(t), s.dz * s.Slid(t));
            Vector2 centre = CastleRoomGraphics.CentreOf(behind, room);
            VfxDraw.Begin(centre);
            for (int i = 0; i < s.riders.Count; i++) InfinityCastleRide.Ride(s.riders[i], behind);

            // The room on its own while it slides; once stopped it is back in the baked castle.
            if (!s.stopped)
            {
                s.movingRoom.Draw(behind, layers);
                CastleRoomGraphics.LanternGlows(room, centre, seconds, layers);
            }
            RoomFlash(room, centre, t - CastleShift.Close0, layers.Wall);

            // Afterimages trail the room while it moves fast.
            if (t > CastleShift.Slide0 && t < s.StopAt + 0.15f)
            {
                float u = Clamp((t - CastleShift.Slide0) / s.SlideTime), fade = 1f - Clamp((t - s.StopAt) / 0.15f);
                float[] lag = { 0.05f, 0.1f }, alpha = { 0.22f, 0.11f };
                for (int i = 0; i < 2; i++)
                {
                    float back = s.Slid(t - lag[i]) - s.distance;
                    Outline(room, CastleRoomGraphics.CentreOf(new Vector2(s.dx * back, s.dz * back), room), alpha[i] * fade * Clamp(u * 4f), layers.Wall);
                }
            }

            // Doorways: broken ones shut before the slide (the room's own cell rides with it), new ones open after the stop.
            float shut = 1f - Smooth((t - CastleShift.Close0) / CastleShift.CloseFor);
            foreach (CastleDoorway door in s.broken)
                for (int i = 0; i < 2; i++)
                {
                    var (x, z) = door.Cells[i];
                    bool rides = (i == 0 ? door.A : door.B) == s.room;
                    var at = new Vector2(x + 0.5f + (rides ? slid.x : 0f), z + 0.5f + (rides ? slid.y : 0f));
                    WallDoor(at, door.AlongZ, shut, 0f, 1f, layers.Wall + 0.03f);
                }
            if (t >= s.OpenAt)
                foreach (CastleDoorway door in s.made)
                    foreach (var (x, z) in door.Cells)
                        WallDoor(new Vector2(x + 0.5f, z + 0.5f), door.AlongZ, EaseOut((t - s.OpenAt) / CastleShift.OpenFor), 0f, 1f, layers.Wall + 0.03f);

            // The stop against a room: dust along the seam where the walls met, and a small shake.
            if (s.blocked && s.seam.HasValue) DustLine(s.seam.Value.a, s.seam.Value.b, t - s.StopAt, layers.Fx, 0.7f, 12, 0.55f, s.room, 0.6f);
            if (s.blocked && !s.shook && t >= s.StopAt) { s.shook = true; Find.CameraDriver.shaker.DoShake(0.03f); }

            // The strum from the biwa: nobody plays it yet, the rings still leave the dais.
            var (seatX, seatZ) = CastleLayout.SeatOf(Castle.Biwa);
            var (biwaX, biwaZ) = CastleLayout.BiwaOf((seatX, seatZ));
            Strum(new Vector2((float)biwaX, (float)biwaZ), t, 5f, 0.6f, layers.Fx);
        }

        /// <summary>
        /// Every sealed doorway shut and barred, and the ones on the move: Seal shuts the leaves over
        /// 0.15 s from 0.05 s after the strum and slides the bar in over 0.25 s from 0.2 s; Open slides the
        /// bar out over 0.2 s from 0.05 s and opens the leaves over 0.2 s from 0.2 s. The doorway's
        /// outline lights as the command lands. The Seal and Open sketch's timings.
        /// </summary>
        private void DrawSeals()
        {
            CastleLayers layers = CastleLayers.Pocket;
            var (seatX, seatZ) = CastleLayout.SeatOf(Castle.Biwa);
            var (biwaX, biwaZ) = CastleLayout.BiwaOf((seatX, seatZ));
            foreach (string key in sealedDoors)
            {
                if (seals.Any(a => a.key == key)) continue;
                CastleDoorway door = Castle.Doorways.FirstOrDefault(d => d.Key == key);
                if (door == null) continue;
                foreach (var (x, z) in door.Cells) WallDoor(new Vector2(x + 0.5f, z + 0.5f), door.AlongZ, 0f, 1f, 1f, layers.Wall + 0.03f);
            }
            foreach (CastleSealAnim a in seals)
            {
                float t = seconds - a.startAt, open, bar;
                if (a.sealing)
                {
                    open = 1f - Smooth((t - 0.05f) / 0.15f);
                    bar = Smooth((t - 0.2f) / 0.25f);
                }
                else
                {
                    bar = 1f - Smooth((t - 0.05f) / 0.2f);
                    open = EaseOut((t - 0.2f) / 0.2f);
                }
                foreach (var (x, z) in a.door.Cells) WallDoor(new Vector2(x + 0.5f, z + 0.5f), a.door.AlongZ, open, bar, 1f, layers.Wall + 0.03f);
                var (ax, az) = a.door.Cells[0];
                var (bx, bz) = a.door.Cells[1];
                var mid = new Vector2((ax + bx) / 2f + 0.5f, (az + bz) / 2f + 0.5f);
                float age = t - 0.05f;
                if (age >= 0f && age < 0.45f)
                    OutlineRect(mid, a.door.AlongZ ? 2.6f : 1.6f, a.door.AlongZ ? 1.6f : 2.6f, 0.6f * (1f - age / 0.45f), layers.Wall, 0.12f);
                Strum(new Vector2((float)biwaX, (float)biwaZ), t, 5f, 0.6f, layers.Fx);
            }
        }

        private static readonly Color PanelWood = Color.Lerp(CastleRoomGraphics.WallWood, CastleRoomGraphics.WallTop, 0.35f);
        private static readonly Color PanelPaper = Color.Lerp(CastleRoomGraphics.Paper, CastleRoomGraphics.WallWood, 0.25f);

        /// <summary>
        /// The Crush sketch's picture over the real room: the room's outline flashes at the strum, the hit
        /// band lights and pulses until the walls come in, slabs slide out of the wall ring over the
        /// pawns and draw back, dust and splinters at the impact, a flash and stun stars over each pawn
        /// hit. Nothing on the map moves; the real pawns are not squashed.
        /// </summary>
        private void DrawCrush()
        {
            CastleLayers layers = CastleLayers.Pocket;
            CastleCrush c = crush;
            float t = c.AgeAt(seconds);
            CastleRoom room = Castle.Rooms[c.room];
            int band = InfinityCastleRules.Of.crushBand;
            var centre = CastleRoomGraphics.CentreOf(Vector2.zero, room);
            VfxDraw.Begin(centre);
            RoomFlash(room, centre, t - CastleCrush.Mark, layers.Wall);
            var (seatX, seatZ) = CastleLayout.SeatOf(Castle.Biwa);
            var (biwaX, biwaZ) = CastleLayout.BiwaOf((seatX, seatZ));
            Strum(new Vector2((float)biwaX, (float)biwaZ), t, 5f, 0.6f, layers.Fx);

            // The floor inside the walls, in cells; the four bands d cells deep along its sides.
            float fx0 = room.X + 1, fz0 = room.Z + 1, fx1 = room.X + room.W - 1, fz1 = room.Z + room.H - 1;
            void Rect(float x0, float z0, float x1, float z1, Color colour, float altitude, Material material) =>
                Sprite(new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f), x1 - x0, z1 - z0, colour, material, altitude);
            (float x0, float z0, float x1, float z1, char side)[] Bands(float d) => new[]
            {
                (fx0, fz1 - d, fx1, fz1, 'n'), (fx0, fz0, fx1, fz0 + d, 's'), (fx0, fz0 + d, fx0 + d, fz1 - d, 'w'), (fx1 - d, fz0 + d, fx1, fz1 - d, 'e'),
            };

            // The hit area: the outer band lights up and pulses until the walls come in.
            float markA = t >= CastleCrush.Mark && t < CastleCrush.Hit ? Smooth((t - CastleCrush.Mark) / 0.1f) * (0.75f + 0.25f * Mathf.Sin((t - CastleCrush.Mark) * 30f)) : 0f;
            if (markA > 0f)
                foreach (var (x0, z0, x1, z1, _) in Bands(band))
                    Rect(x0, z0, x1, z1, Fade(CastleRoomGraphics.Strum, 0.16f * markA), layers.Floor + 0.03f, whiteGlow);

            // The walls: slabs out of the wall ring, drawn over the pawns while they are in.
            float d = band * CastleCrush.In(t);
            if (d > 0.01f)
            {
                float y = layers.Fx - 0.1f;
                foreach (var (x0, z0, x1, z1, side) in Bands(d))
                {
                    Rect(x0, z0, x1, z1, CastleRoomGraphics.WallWood, y, solid);
                    bool alongX = side == 'n' || side == 's';
                    float inset = Mathf.Min(0.3f, d * 0.2f);
                    // A shoji strip down the middle of each slab, lattice every half cell.
                    if (alongX)
                    {
                        Rect(x0 + 0.2f, z0 + inset, x1 - 0.2f, z1 - inset, PanelPaper, y + 0.002f, solid);
                        for (float x = x0 + 0.5f; x < x1 - 0.3f; x += 0.5f) Rect(x - 0.02f, z0 + inset, x + 0.02f, z1 - inset, CastleRoomGraphics.Lattice, y + 0.003f, solid);
                    }
                    else
                    {
                        Rect(x0 + inset, z0 + 0.2f, x1 - inset, z1 - 0.2f, PanelPaper, y + 0.002f, solid);
                        for (float z = z0 + 0.5f; z < z1 - 0.3f; z += 0.5f) Rect(x0 + inset, z - 0.02f, x1 - inset, z + 0.02f, CastleRoomGraphics.Lattice, y + 0.003f, solid);
                    }
                    // The front edge: red lacquer and a lit lip, where the slab meets the room.
                    const float e = 0.09f;
                    switch (side)
                    {
                        case 'n': Rect(x0, z0, x1, z0 + e, CastleRoomGraphics.Lacquer, y + 0.004f, solid); Rect(x0, z0 + e, x1, z0 + e + 0.05f, PanelWood, y + 0.004f, solid); break;
                        case 's': Rect(x0, z1 - e, x1, z1, CastleRoomGraphics.Lacquer, y + 0.004f, solid); Rect(x0, z1 - e - 0.05f, x1, z1 - e, PanelWood, y + 0.004f, solid); break;
                        case 'w': Rect(x1 - e, z0, x1, z1, CastleRoomGraphics.Lacquer, y + 0.004f, solid); Rect(x1 - e - 0.05f, z0, x1 - e, z1, PanelWood, y + 0.004f, solid); break;
                        default: Rect(x0, z0, x0 + e, z1, CastleRoomGraphics.Lacquer, y + 0.004f, solid); Rect(x0 + e, z0, x0 + e + 0.05f, z1, PanelWood, y + 0.004f, solid); break;
                    }
                }
            }

            // Impact: dust along the four wall fronts, splinters thrown into the room.
            float age = t - CastleCrush.Hit;
            var fronts = new[]
            {
                (new Vector2(fx0 + band, fz1 - band), new Vector2(fx1 - band, fz1 - band)), (new Vector2(fx0 + band, fz0 + band), new Vector2(fx1 - band, fz0 + band)),
                (new Vector2(fx0 + band, fz0 + band), new Vector2(fx0 + band, fz1 - band)), (new Vector2(fx1 - band, fz0 + band), new Vector2(fx1 - band, fz1 - band)),
            };
            for (int k = 0; k < 4; k++) DustLine(fronts[k].Item1, fronts[k].Item2, age, layers.Fx, 0.9f, 9, 0.6f, k * 17, 0.5f);
            if (age >= 0f && age < 0.8f)
            {
                const float Lift = 0.6f;
                for (int i = 0; i < 24; i++)
                {
                    var (a, b) = fronts[i % 4];
                    float u = Rand(i + 300);
                    var at = new Vector2(a.x + (b.x - a.x) * u, a.y + (b.y - a.y) * u);
                    float cx = (fx0 + fx1) / 2f - at.x, cz = (fz0 + fz1) / 2f - at.y, len = Mathf.Max(0.001f, Mathf.Sqrt(cx * cx + cz * cz));
                    float life = 0.45f + Rand(i + 310) * 0.35f, k = Clamp(age / life);
                    if (k >= 1f) continue;
                    float reach = 0.6f + Rand(i + 320) * 1.2f, h = 1.4f * k * (1f - k) * (0.6f + Rand(i + 330));
                    var q = new Vector2(at.x + cx / len * reach * k, at.y + cz / len * reach * k + h * Lift);
                    Rect(q.x - 0.06f, q.y - 0.02f, q.x + 0.06f, q.y + 0.02f, Fade(CastleRoomGraphics.WallTop, 1f - k * k), layers.Fx + 0.02f, solid);
                }
            }

            // Each pawn hit: a flash as the walls land, stun stars once they draw back.
            foreach (Pawn pawn in crushed)
            {
                if (pawn == null || !pawn.Spawned || pawn.Map != map) continue;
                var pos = new Vector2(pawn.DrawPos.x, pawn.DrawPos.z);
                if (age >= 0f && age < 0.18f) Sprite(new Vector2(pos.x, pos.y + 0.45f), 1.1f, 1.1f, Fade(CastleRoomGraphics.Strum, 0.7f * (1f - age / 0.18f)), glow, layers.Fx + 0.08f);
                float stars = t - CastleCrush.Back, life = InfinityCastleRules.Of.crushStunSeconds - CastleCrush.HoldFor;
                if (stars < 0f || stars > life) continue;
                float sa = Mathf.Min(1f, stars / 0.1f) * (1f - Clamp((stars - life + 0.2f) / 0.2f));
                for (int i = 0; i < 4; i++)
                {
                    float ang = stars * 5f + i * Mathf.PI / 2f;
                    Sprite(new Vector2(pos.x + Mathf.Cos(ang) * 0.2f, pos.y + 0.95f + Mathf.Sin(ang) * 0.08f), 0.11f, 0.11f, Fade(CastleRoomGraphics.Strum, 0.9f * sa), glow, layers.Fx + 0.03f);
                }
            }
        }

        /// <summary>
        /// Each drop: the room's flash and the strum for a Drop, a floor door under the pawn with the
        /// shaft's dark closing over it as it sinks, then the far room's flash, its floor door and the
        /// dark lifting off the pawn as it rises. The Drop sketch's picture.
        /// </summary>
        private void DrawDrops()
        {
            CastleLayers layers = CastleLayers.Pocket;
            var (seatX, seatZ) = CastleLayout.SeatOf(Castle.Biwa);
            var (biwaX, biwaZ) = CastleLayout.BiwaOf((seatX, seatZ));
            foreach (CastleDrop drop in drops)
            {
                float t = drop.AgeAt(seconds);
                var from = new Vector2(drop.from.x + 0.5f, drop.from.z + 0.5f);
                var to = new Vector2(drop.to.x + 0.5f, drop.to.z + 0.5f);
                if (drop.strum)
                {
                    Strum(new Vector2((float)biwaX, (float)biwaZ), t, 5f, 0.6f, layers.Fx);
                    if (drop.fromRoom >= 0) RoomFlash(Castle.Rooms[drop.fromRoom], CastleRoomGraphics.CentreOf(Vector2.zero, Castle.Rooms[drop.fromRoom]), t - 0.05f, layers.Wall);
                }
                float under = t - drop.doorUnder;
                DoorAt(under, CastleDrop.Sink + 0.05f, out float fromAlpha, out float fromOpen);
                if (under < DoorEnd(CastleDrop.Sink + 0.05f)) FloorDoor(from, fromOpen, fromAlpha, seconds, layers.Door);
                if (under >= DoorThrough && t < drop.SinkEnd) Sinking(from, Clamp((under - DoorThrough) / CastleDrop.Sink), layers);
                float land = t - drop.Arrive;
                if (drop.toRoom >= 0) RoomFlash(Castle.Rooms[drop.toRoom], CastleRoomGraphics.CentreOf(Vector2.zero, Castle.Rooms[drop.toRoom]), land, layers.Wall);
                DoorAt(land, CastleDrop.Rise * 0.75f, out float toAlpha, out float toOpen);
                if (land >= -0.2f && land < DoorEnd(CastleDrop.Rise * 0.75f)) FloorDoor(to, toOpen, toAlpha, seconds, layers.Door);
                if (land >= DoorThrough) Rising(to, Clamp((land - DoorThrough) / CastleDrop.Rise), layers);
            }
        }

        public override void MapRemoved()
        {
            if (!IsCastle) return;
            if (shift != null) foreach (Pawn rider in shift.riders) InfinityCastleRide.Release(rider);
            foreach (CastleDrop drop in drops) if (drop.pawn != null) InfinityCastleRide.Release(drop.pawn);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref rooms, "rooms");
            Scribe_References.Look(ref source, "source");
            Scribe_Values.Look(ref lastStrumAt, "lastStrumAt", -1000f);
            Scribe_Collections.Look(ref roomPlaces, "roomPlaces", LookMode.Value);
            Scribe_Deep.Look(ref shift, "shift");
            Scribe_Collections.Look(ref drops, "drops", LookMode.Deep);
            Scribe_Collections.Look(ref sealedDoors, "sealedDoors", LookMode.Value);
            Scribe_Deep.Look(ref crush, "crush");
            Scribe_Values.Look(ref lastCrushAt, "lastCrushAt", -1000f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // A loaded castle opens in its hold: the arrival doors are not played again. A slide in
                // progress finishes without its picture; a drop in progress keeps its doors.
                roomPlaces = roomPlaces ?? new List<int>();
                drops = drops ?? new List<CastleDrop>();
                drops.RemoveAll(d => d.pawn == null);
                sealedDoors = sealedDoors ?? new List<string>();
                seals.Clear();
                lastCrushAt = -1000f;
                crushed.Clear();
                lastStrumAt = -1000f;
                castle = null;
                batch = null;
                seconds = T.Quiet;
                releasedAt = -1f;
                closing = false;
            }
        }
    }
}
