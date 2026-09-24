using System;
using System.Collections.Generic;
using System.Linq;

namespace RimArt
{
    internal enum CastleKind { Biwa, Tatami, Corridor, Hall, Stair }

    /// <summary>
    /// One room of the castle in whole cells: it covers X..X+W-1, Z..Z+H-1 with its wall ring included,
    /// so a 9 x 9 room has a 7 x 7 floor.
    /// </summary>
    internal sealed class CastleRoom
    {
        public int Id, X, Z, W, H;
        public CastleKind Kind;

        public bool Contains(int x, int z) => x >= X && x < X + W && z >= Z && z < Z + H;
        public bool IsWall(int x, int z) => Contains(x, z) && (x == X || x == X + W - 1 || z == Z || z == Z + H - 1);
    }

    /// <summary>
    /// A doorway: two wall cells side by side, one in each room. Cells[0] is in room A's wall, Cells[1] in
    /// room B's. AlongZ: the walls run north-south (the rooms sit east and west of each other).
    /// </summary>
    internal sealed class CastleDoorway
    {
        public int A, B;
        public bool AlongZ;
        public (int x, int z)[] Cells;
        public string Key => A + "-" + B;
    }

    /// <summary>A room or a flight of stairs drawn at another depth under the void. Never walked.</summary>
    internal sealed class CastleDepthItem
    {
        public int Id;
        /// <summary>0 for a room; otherwise a flight of stairs this many cells long.</summary>
        public int Flight;
        public CastleKind Kind;
        public int W, H;
        /// <summary>2 is further down than 1.</summary>
        public int Level;
        public double X, Z, Rot, DriftA, DriftP, Spin;
    }

    /// <summary>
    /// The Infinity Castle's layout: rooms hanging in the void, the doorways where their walls touch, and
    /// how many doorways each room is from the biwa room. The port of the generator in
    /// Tools/VfxLab/web/sketches/lib/infinity-castle.js (generate, local, contact, doorsOf, distances,
    /// arrivalRooms, lanternsOf, and the rooms at other depths from drawVoid).
    ///
    /// It is exact, not only the same rules: the sketch's Mulberry32 numbers are drawn in the same order
    /// and the maths is in double precision, so seed N with M rooms is the same castle as the sketch's
    /// seed N with M rooms. Tests/InfinityCastle checks it against the JS. Mind the JS habits kept here:
    /// Math.round rounds halves up (<see cref="RoundHalfUp"/>, not Math.Round), and the sketch's sorts are
    /// stable (OrderBy, not List.Sort). System only, so the test links it without stubs.
    /// </summary>
    internal sealed class CastleLayout
    {
        // The rules. Sizes include the wall ring; the agreed range is 5 x 5 to 17 x 13 either way round.
        public const int Size = 100, Margin = 2, Gap = 2, MinDoor = 3, BiwaSize = 9, Tries = 9000;
        public const int DefaultRooms = 38, DepthRooms = 70, Flights = 12;
        private const double Tau = Math.PI * 2.0;

        private static readonly CastleKind[] KindOrder = { CastleKind.Tatami, CastleKind.Corridor, CastleKind.Hall, CastleKind.Stair };
        private static readonly int[] KindWeight = { 34, 30, 12, 24 };
        private const double KindTotal = 100.0;

        public readonly List<CastleRoom> Rooms;
        public readonly List<CastleDoorway> Doorways;
        /// <summary>Doorways from the biwa room, by room id; <see cref="Unreachable"/> when cut off.</summary>
        public readonly int[] Dist;
        public readonly int Seed;
        public const int Unreachable = int.MaxValue;

        private CastleLayout(List<CastleRoom> rooms, int seed)
        {
            Rooms = rooms;
            Seed = seed;
            Doorways = DoorsOf(rooms);
            Dist = Distances(rooms, Doorways);
        }

        public CastleRoom Biwa => Rooms[0];

        // ---- random ---------------------------------------------------------------------------------------

        /// <summary>Mulberry32, as the sketch's rng(seed): the same 32-bit steps, the same doubles out.</summary>
        internal sealed class Mulberry32
        {
            private uint a;

            public Mulberry32(int seed) { a = unchecked((uint)seed * 2654435761u) ^ 0x9e3779b9u; }

            public double Next()
            {
                unchecked
                {
                    a += 0x6D2B79F5u;
                    uint t = a;
                    t = (t ^ (t >> 15)) * (t | 1u);
                    t ^= t + (t ^ (t >> 7)) * (t | 61u);
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            }

            /// <summary>The sketch's int(R, lo, hi): lo to hi inclusive.</summary>
            public int Int(int lo, int hi) => lo + (int)Math.Floor(Next() * (hi - lo + 1));
        }

        /// <summary>The lab's rand(i), the sine hash, in double as it is there (SixPathsBloomTiming.Rand returns a float).</summary>
        public static double Hash(int i)
        {
            double n = Math.Sin(i * 127.1 + 17) * 43758.5453;
            return n - Math.Floor(n);
        }

        /// <summary>JavaScript's Math.round: halves go up. Math.Round would send 2.5 to 2.</summary>
        public static int RoundHalfUp(double v) => (int)Math.Floor(v + 0.5);

        private static CastleKind PickKind(Mulberry32 r)
        {
            double left = r.Next() * KindTotal;
            for (int i = 0; i < KindOrder.Length; i++)
                if ((left -= KindWeight[i]) < 0) return KindOrder[i];
            return KindOrder[0];
        }

        /// <summary>A room's size for its kind. Every draw is in the sketch's order: width, then height.</summary>
        private static void Dims(CastleKind kind, Mulberry32 r, bool flat, out int w, out int h)
        {
            switch (kind)
            {
                case CastleKind.Tatami:
                    w = r.Int(5, 11); h = r.Int(5, 11);
                    return;
                case CastleKind.Corridor:
                    if (flat) { w = r.Int(11, 17); h = 5; }
                    else { w = 5; h = r.Int(11, 17); }
                    return;
                case CastleKind.Hall:
                    if (flat) { w = r.Int(13, 17); h = r.Int(11, 13); }
                    else { w = r.Int(11, 13); h = r.Int(13, 17); }
                    return;
                default:
                    if (flat) { w = r.Int(9, 13); h = r.Int(7, 9); }
                    else { w = r.Int(7, 9); h = r.Int(9, 13); }
                    return;
            }
        }

        // ---- contact and doorways -------------------------------------------------------------------------

        /// <summary>
        /// Where two rooms' walls touch, corners excluded: lo..hi along the shared wall, and the wall
        /// column (AlongZ) or row of each room. False when they do not touch.
        /// </summary>
        public static bool Contact(CastleRoom a, CastleRoom b, out bool alongZ, out int lo, out int hi, out int wa, out int wb)
        {
            if (a.X + a.W == b.X || b.X + b.W == a.X)
            {
                lo = Math.Max(a.Z, b.Z) + 1;
                hi = Math.Min(a.Z + a.H, b.Z + b.H) - 1;
                if (hi - lo >= 1)
                {
                    alongZ = true;
                    bool east = a.X + a.W == b.X;
                    wa = east ? a.X + a.W - 1 : a.X;
                    wb = east ? b.X : b.X + b.W - 1;
                    return true;
                }
            }
            if (a.Z + a.H == b.Z || b.Z + b.H == a.Z)
            {
                lo = Math.Max(a.X, b.X) + 1;
                hi = Math.Min(a.X + a.W, b.X + b.W) - 1;
                if (hi - lo >= 1)
                {
                    alongZ = false;
                    bool north = a.Z + a.H == b.Z;
                    wa = north ? a.Z + a.H - 1 : a.Z;
                    wb = north ? b.Z : b.Z + b.H - 1;
                    return true;
                }
            }
            alongZ = false; lo = hi = wa = wb = 0;
            return false;
        }

        private static int Span(CastleRoom a, CastleRoom b) =>
            Contact(a, b, out _, out int lo, out int hi, out _, out _) ? hi - lo : 0;

        private static bool Near(CastleRoom a, CastleRoom b, int gap) =>
            a.X - gap < b.X + b.W && b.X - gap < a.X + a.W && a.Z - gap < b.Z + b.H && b.Z - gap < a.Z + a.H;

        /// <summary>A doorway wherever two rooms' walls touch along MinDoor cells or more, in the middle of that stretch.</summary>
        public static List<CastleDoorway> DoorsOf(List<CastleRoom> rooms)
        {
            var doorways = new List<CastleDoorway>();
            for (int i = 0; i < rooms.Count; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (!Contact(rooms[i], rooms[j], out bool alongZ, out int lo, out int hi, out int wa, out int wb) || hi - lo < MinDoor)
                        continue;
                    int mid = lo + (hi - lo - 1) / 2;
                    doorways.Add(new CastleDoorway
                    {
                        A = rooms[i].Id,
                        B = rooms[j].Id,
                        AlongZ = alongZ,
                        Cells = alongZ ? new[] { (wa, mid), (wb, mid) } : new[] { (mid, wa), (mid, wb) },
                    });
                }
            return doorways;
        }

        /// <summary>Doorways from room <paramref name="from"/> to every room, breadth first, as the sketch walks them.</summary>
        public static int[] Distances(List<CastleRoom> rooms, List<CastleDoorway> doorways, int from = 0)
        {
            int[] dist = new int[rooms.Count];
            for (int i = 0; i < dist.Length; i++) dist[i] = Unreachable;
            if (dist.Length == 0) return dist;
            var queue = new Queue<int>();
            dist[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int r = queue.Dequeue();
                foreach (CastleDoorway d in doorways)
                {
                    int other = d.A == r ? d.B : d.B == r ? d.A : -1;
                    if (other >= 0 && dist[other] == Unreachable)
                    {
                        dist[other] = dist[r] + 1;
                        queue.Enqueue(other);
                    }
                }
            }
            return dist;
        }

        // ---- the generator --------------------------------------------------------------------------------

        /// <summary>
        /// Rooms grow off each other, touch only where they join and keep Gap cells of void from every
        /// other room, so the castle starts as a tree of rooms. The biwa room (9 x 9) is at the west edge.
        /// Fewer than <paramref name="count"/> rooms come back only if every try failed.
        /// </summary>
        public static CastleLayout Generate(int seed, int count = DefaultRooms)
        {
            var r = new Mulberry32(seed);
            var rooms = new List<CastleRoom>
            {
                new CastleRoom
                {
                    Id = 0, X = Margin, Z = RoundHalfUp(Size / 2.0 - BiwaSize / 2.0 + (r.Next() - 0.5) * 30.0),
                    W = BiwaSize, H = BiwaSize, Kind = CastleKind.Biwa,
                },
            };
            for (int tries = 0; rooms.Count < count && tries < Tries; tries++)
            {
                // Newer rooms are picked a little more often, so the castle grows outward.
                CastleRoom parent = rooms[Math.Min(rooms.Count - 1, (int)Math.Floor(Math.Pow(r.Next(), 0.75) * rooms.Count))];
                int side = r.Int(0, 3);                                  // 0 east, 1 north, 2 west, 3 south
                bool flat = side % 2 == 0;
                CastleKind kind = PickKind(r);
                Dims(kind, r, flat, out int w, out int h);
                int x, z;
                if (flat)
                {
                    x = side == 0 ? parent.X + parent.W : parent.X - w;
                    z = r.Int(parent.Z + 5 - h, parent.Z + parent.H - 5);
                }
                else
                {
                    z = side == 1 ? parent.Z + parent.H : parent.Z - h;
                    x = r.Int(parent.X + 5 - w, parent.X + parent.W - 5);
                }
                var room = new CastleRoom { Id = rooms.Count, X = x, Z = z, W = w, H = h, Kind = kind };
                if (x < Margin || z < Margin || x + w > Size - Margin || z + h > Size - Margin) continue;
                if (Span(parent, room) < MinDoor) continue;
                bool crowded = false;
                foreach (CastleRoom other in rooms)
                    if (other != parent && Near(other, room, Gap)) { crowded = true; break; }
                if (crowded) continue;
                rooms.Add(room);
            }
            return new CastleLayout(rooms, seed);
        }

        /// <summary>A hand-placed castle for the command sketches: (kind, x, z, w, h) per room, the first the biwa room.</summary>
        public static CastleLayout Local(IEnumerable<(CastleKind kind, int x, int z, int w, int h)> specs, int seed = 0)
        {
            var rooms = specs.Select((s, id) => new CastleRoom { Id = id, Kind = s.kind, X = s.x, Z = s.z, W = s.w, H = s.h }).ToList();
            return new CastleLayout(rooms, seed);
        }

        /// <summary>
        /// Rooms for <paramref name="n"/> arrivals: each a different room at least <paramref name="min"/>
        /// doorways from the biwa room, in an order fixed by the seed; closer rooms when there are too few.
        /// </summary>
        public List<CastleRoom> ArrivalRooms(int n, int min = 3)
        {
            // Kept: drawing asks for them every frame.
            if (arrivals.TryGetValue((n, min), out List<CastleRoom> kept)) return kept;
            List<CastleRoom> order = Rooms.Where(q => q.Kind != CastleKind.Biwa).OrderBy(q => Hash(q.Id * 7 + Seed)).ToList();
            List<CastleRoom> chosen = new List<CastleRoom>();
            for (int m = min; m >= 1; m--)
            {
                List<CastleRoom> pick = order.Where(q => Dist[q.Id] >= m && Dist[q.Id] != Unreachable).ToList();
                if (pick.Count >= n || m == 1) { chosen = pick.Take(n).ToList(); break; }
            }
            return arrivals[(n, min)] = chosen;
        }

        private readonly Dictionary<(int, int), List<CastleRoom>> arrivals = new Dictionary<(int, int), List<CastleRoom>>();

        /// <summary>
        /// The wall cells on the way from a room to the biwa room, two per doorway (this side, then the
        /// far side), nearest first. The Castle sketch walks its stand-in enemies along them.
        /// </summary>
        public List<(int x, int z)> WayHome(CastleRoom room)
        {
            var cells = new List<(int x, int z)>();
            int at = room.Id;
            while (Dist[at] > 0 && Dist[at] != Unreachable)
            {
                CastleDoorway door = Doorways.FirstOrDefault(d =>
                    (d.A == at && Dist[d.B] == Dist[at] - 1) || (d.B == at && Dist[d.A] == Dist[at] - 1));
                if (door == null) break;
                int mine = door.A == at ? 0 : 1;
                cells.Add(door.Cells[mine]);
                cells.Add(door.Cells[1 - mine]);
                at = mine == 0 ? door.B : door.A;
            }
            return cells;
        }

        // ---- lanterns and the carrier's seat --------------------------------------------------------------

        /// <summary>Lanterns in a room, in cells about its centre: two rows along a corridor, the corners of a room, more along a hall.</summary>
        public static List<(double x, double z)> LanternsOf(CastleRoom room) => LanternsOf(room.Kind, room.W, room.H);

        public static List<(double x, double z)> LanternsOf(CastleKind kind, int w, int h)
        {
            var lanterns = new List<(double x, double z)>();
            double X = w / 2.0 - 1.55, Z = h / 2.0 - 1.55;
            if (kind == CastleKind.Corridor)
            {
                bool flat = w >= h;
                double length = (flat ? w : h) / 2.0 - 2.0;
                for (double a = -length; a <= length + 0.01; a += 3.0)
                {
                    lanterns.Add(flat ? (a, -Z) : (-X, a));
                    lanterns.Add(flat ? (a + 1.5, Z) : (X, a + 1.5));
                }
                return lanterns.Where(l => Math.Abs(l.x) <= w / 2.0 - 1.5 && Math.Abs(l.z) <= h / 2.0 - 1.5).ToList();
            }
            if (w <= 5 || h <= 5) return new List<(double x, double z)> { (X, Z) };
            if (kind == CastleKind.Biwa) return new List<(double x, double z)> { (-X, -Z), (X, -Z), (-3.1, Z - 0.6), (3.1, Z - 0.6) };
            lanterns.Add((-X, -Z)); lanterns.Add((X, -Z)); lanterns.Add((-X, Z)); lanterns.Add((X, Z));
            if (kind == CastleKind.Hall) { lanterns.Add((0.0, -Z)); lanterns.Add((0.0, Z)); }
            return lanterns;
        }

        /// <summary>Where the carrier sits on the dais, in castle cells: facing south into the biwa room.</summary>
        public static (double x, double z) SeatOf(CastleRoom biwa) => (biwa.X + biwa.W / 2.0, biwa.Z + biwa.H - 2.9);

        /// <summary>The biwa's body in her lap, where each strum's rings start.</summary>
        public static (double x, double z) BiwaOf((double x, double z) seat) => (seat.x + 0.05, seat.z + 0.2);

        // ---- the rooms at other depths ----------------------------------------------------------------------

        /// <summary>
        /// The drawn-only rooms and stair flights below the void, from drawVoid: 70 rooms and 12 flights
        /// spread <paramref name="reach"/> cells about the centre, sorted further ones (level 2) first.
        /// </summary>
        public static List<CastleDepthItem> DepthItems(int seed, double reach)
        {
            var r = new Mulberry32(seed * 31 + 7);
            var items = new List<CastleDepthItem>();
            for (int i = 0; i < DepthRooms; i++)
            {
                CastleKind kind = PickKind(r);
                bool flat = r.Next() < 0.5;
                Dims(kind, r, flat, out int w, out int h);
                int level = r.Next() < 0.45 ? 2 : 1;
                double x = (r.Next() - 0.5) * reach * 2.0, z = (r.Next() - 0.5) * reach * 2.0;
                bool turned = r.Next() < 0.3;
                double rot = 0.0;
                if (turned) rot = r.Next() < 0.5 ? 90.0 : (r.Next() - 0.5) * 30.0;
                double driftA = (r.Next() - 0.5) * 1.4, driftP = r.Next() * Tau;
                double spin = 0.0;
                if (turned && r.Next() < 0.4) spin = (r.Next() - 0.5) * 6.0;
                items.Add(new CastleDepthItem
                {
                    Id = 1000 + i, Kind = kind, W = w, H = h, Level = level, X = x, Z = z, Rot = rot, DriftA = driftA, DriftP = driftP, Spin = spin,
                });
            }
            for (int i = 0; i < Flights; i++)
            {
                int length = r.Int(8, 16);
                int level = r.Next() < 0.5 ? 2 : 1;
                double rot;
                if (r.Next() < 0.6)
                {
                    double square = r.Next() < 0.5 ? 0.0 : 90.0;
                    rot = square + (r.Next() - 0.5) * 8.0;
                }
                else rot = (r.Next() - 0.5) * 70.0;
                double x = (r.Next() - 0.5) * reach * 2.0, z = (r.Next() - 0.5) * reach * 2.0;
                double driftA = r.Next() - 0.5, driftP = r.Next() * Tau;
                items.Add(new CastleDepthItem { Id = 2000 + i, Flight = length, Level = level, X = x, Z = z, Rot = rot, DriftA = driftA, DriftP = driftP });
            }
            return items.OrderByDescending(item => item.Level).ToList();
        }
    }
}
