using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
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
        private List<CastleVoidDrop> drops = new List<CastleVoidDrop>();
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

        private CastleRoomGraphics.CastleBatch Batch => batch ?? (batch = CastleRoomGraphics.CastleBatch.Build(Castle, SlidingRoom));

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

        /// <summary>
        /// The Void rule: <paramref name="pawn"/> drops through the void and comes up in a random room
        /// (not the biwa room, not <paramref name="notRoom"/>) through a floor door, unharmed. The move
        /// is now; the picture hides the pawn until it is up.
        /// </summary>
        public void VoidDrop(Pawn pawn, int notRoom = -1)
        {
            List<CastleRoom> choices = Castle.Rooms.Where(r => r.Kind != CastleKind.Biwa && r.Id != notRoom).ToList();
            if (choices.Count == 0 || !pawn.Spawned) return;
            CastleRoom room = choices.RandomElement();
            IntVec3 from = pawn.Position, to = new IntVec3(room.X + room.W / 2, 0, room.Z + room.H / 2);
            pawn.DeSpawnOrDeselect();
            GenSpawn.Spawn(pawn, to, map);
            pawn.Notify_Teleported(true, true);
            pawn.stances?.stunner.StunFor(Mathf.CeilToInt((CastleVoidDrop.Land + CastleVoidDrop.Rise + InfinityCastleRules.Of.voidDropStunSeconds) * 60f), null, false, false);
            InfinityCastleRide.Hide(pawn);
            drops.Add(new CastleVoidDrop { pawn = pawn, from = from, to = to, startAt = seconds });
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
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                CastleVoidDrop drop = drops[i];
                if (drop.pawn == null || drop.DoneAt(seconds)) { if (drop.pawn != null) InfinityCastleRide.Release(drop.pawn); drops.RemoveAt(i); continue; }
                if (drop.AgeAt(seconds) >= CastleVoidDrop.Land + DoorThrough) InfinityCastleRide.Release(drop.pawn);
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
            ThunderGodGraphics.Begin(centre);
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

        /// <summary>Each Void-rule drop: a floor door where the pawn stood, and one it comes up through, the shaft's dark lifting.</summary>
        private void DrawDrops()
        {
            CastleLayers layers = CastleLayers.Pocket;
            foreach (CastleVoidDrop drop in drops)
            {
                float age = drop.AgeAt(seconds);
                var from = new Vector2(drop.from.x + 0.5f, drop.from.z + 0.5f);
                var to = new Vector2(drop.to.x + 0.5f, drop.to.z + 0.5f);
                DoorAt(age, 0.3f, out float fromAlpha, out float fromOpen);
                if (age < DoorEnd(0.3f)) FloorDoor(from, fromOpen, fromAlpha, seconds, layers.Door);
                float landAge = age - CastleVoidDrop.Land;
                DoorAt(landAge, CastleVoidDrop.Rise * 0.75f, out float toAlpha, out float toOpen);
                if (landAge >= -0.2f && landAge < DoorEnd(CastleVoidDrop.Rise * 0.75f)) FloorDoor(to, toOpen, toAlpha, seconds, layers.Door);
                if (landAge >= DoorThrough) Rising(to, Clamp((landAge - DoorThrough) / CastleVoidDrop.Rise), layers);
            }
        }

        public override void MapRemoved()
        {
            if (!IsCastle) return;
            if (shift != null) foreach (Pawn rider in shift.riders) InfinityCastleRide.Release(rider);
            foreach (CastleVoidDrop drop in drops) if (drop.pawn != null) InfinityCastleRide.Release(drop.pawn);
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // A loaded castle opens in its hold: the arrival doors are not played again. A slide in
                // progress finishes without its picture; a drop in progress keeps its doors.
                roomPlaces = roomPlaces ?? new List<int>();
                drops = drops ?? new List<CastleVoidDrop>();
                drops.RemoveAll(d => d.pawn == null);
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
