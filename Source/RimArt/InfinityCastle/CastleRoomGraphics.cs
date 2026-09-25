using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The heights the castle is drawn at. Only the order matters: back, the rooms at other depths, the
    /// void's fog, floors, floor doors, walls (and doorways and flashes on them), the shaft's dark over
    /// pawns, effects.
    /// </summary>
    internal readonly struct CastleLayers
    {
        public readonly float Back, Depth, Fog, Floor, Door, Wall, Pawn, Fx;
        /// <summary>Scales the steps inside the depth group, so the preview can pack it tighter.</summary>
        public readonly float DepthStep;

        public CastleLayers(float back, float depth, float fog, float floor, float door, float wall, float pawn, float fx, float depthStep)
        {
            Back = back; Depth = depth; Fog = fog; Floor = floor; Door = door; Wall = wall; Pawn = pawn; Fx = fx; DepthStep = depthStep;
        }

        /// <summary>
        /// The castle's own map, and the floor doors on the home map: the game's own layers. The void
        /// terrain is opaque and the mod draws everything over it: the back just above the terrain, the
        /// rooms at other depths (0.24 deep), the void's fog and the floors, all under TerrainScatter;
        /// floor doors at Filth as the sketch has them; walls 0.01 over Building, so they cover the real
        /// wall's plain graphic; the shaft's dark over pawns; effects overhead. (The sketch draws the
        /// depth rooms under a see-through void terrain instead; the picture is the same.)
        /// </summary>
        public static readonly CastleLayers Pocket = new CastleLayers(
            AltitudeLayer.Terrain.AltitudeFor() + 0.005f, AltitudeLayer.Terrain.AltitudeFor() + 0.01f,
            AltitudeLayer.Terrain.AltitudeFor() + 0.26f, AltitudeLayer.Terrain.AltitudeFor() + 0.27f,
            AltitudeLayer.Filth.AltitudeFor() + 0.02f, AltitudeLayer.Building.AltitudeFor() + 0.01f,
            AltitudeLayer.Pawn.AltitudeFor(), AltitudeLayer.MoteOverhead.AltitudeFor(), 1f);

        /// <summary>
        /// A home map, for the preview: from the back to the walls' flashes the castle is packed between
        /// ItemImportant + 0.1 and + 0.28 (the depth group at a quarter of its steps), as
        /// VoidLayers.Preview is, so the back covers the ground, plants, buildings and items and pawns
        /// stay on top.
        /// </summary>
        public static readonly CastleLayers Preview = new CastleLayers(
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.1f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.105f,
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.17f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.175f,
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.195f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.22f,
            AltitudeLayer.Pawn.AltitudeFor(), AltitudeLayer.MoteOverhead.AltitudeFor(), 0.25f);
    }

    /// <summary>
    /// The castle's rooms, their lanterns, and the void with its rooms at other depths. The port of
    /// roomParts, drawRoom, lanternsOf, flight and drawVoid in Tools/VfxLab/web/sketches/lib/infinity-castle.js,
    /// with the same shapes, colours and steps.
    ///
    /// Rooms at rest are not drawn part by part as the sketch does (more than 1,000 draws a frame for a
    /// castle): <see cref="CastleBatch"/> bakes every part of every room, the open doorways and the
    /// lantern bodies into one mesh per (height, colour), once per castle. Rooms keep 2 cells of void
    /// between them, so nothing baked into one mesh overlaps anything in another at the same height and
    /// the picture is the sketch's. What moves or flickers is drawn each frame: the lantern glows and the
    /// rooms and stair flights below the void. A room that slides (Shift, later) draws its parts with
    /// <see cref="MeshOf"/>'s meshes.
    ///
    /// Every hand-built mesh keeps the sketch's rect order (x0,z0), (x0,z1), (x1,z1), (x1,z0): clockwise
    /// on screen, so it survives the game's backface culling.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class CastleRoomGraphics
    {
        // ---- palette (the sketch's) ---------------------------------------------------------------------
        internal static readonly Color Void = new Color(0.030f, 0.024f, 0.045f), VoidDeep = new Color(0.010f, 0.008f, 0.016f);
        internal static readonly Color WoodFloor = new Color(0.40f, 0.25f, 0.14f), WoodSeam = new Color(0.22f, 0.13f, 0.07f);
        internal static readonly Color Tatami = new Color(0.58f, 0.55f, 0.34f), TatamiShade = new Color(0.52f, 0.49f, 0.29f), Heri = new Color(0.11f, 0.13f, 0.09f);
        internal static readonly Color WallWood = new Color(0.14f, 0.08f, 0.045f), WallTop = new Color(0.30f, 0.18f, 0.10f), Lacquer = new Color(0.52f, 0.12f, 0.07f);
        internal static readonly Color Paper = new Color(0.88f, 0.82f, 0.68f), Lattice = new Color(0.22f, 0.13f, 0.07f), Gold = new Color(0.78f, 0.60f, 0.28f);
        internal static readonly Color Lantern = new Color(1f, 0.60f, 0.24f), LanternPaper = new Color(1f, 0.80f, 0.52f), LanternCore = new Color(1f, 0.92f, 0.70f);
        /// <summary>Strum rings and command flashes.</summary>
        internal static readonly Color Strum = new Color(1f, 0.93f, 0.78f);

        /// <summary>How far each depth room drifts from its place: a slow sway, as in the sketch.</summary>
        private const float DriftRate = 0.18f;
        private const int DiscSegments = 32;
        /// <summary>One baked mesh stays under Unity's 16-bit index limit.</summary>
        private const int MostVertices = 60000;

        // ---- room parts ---------------------------------------------------------------------------------
        internal enum Part
        {
            Floor, Shade, Heri, Boards, Runner, Gold, StairBed, Treads, Stringers, StairDark0, StairDark1, StairDark2,
            Dais, DaisMat, DaisHeri, Screen, Walls, Edge, Lip, Paper, Lattice, Beam, Posts,
        }

        internal readonly struct Box
        {
            public readonly double X0, Z0, X1, Z1;
            public Box(double x0, double z0, double x1, double z1) { X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; }
        }

        private sealed class Shape
        {
            public readonly Dictionary<Part, List<Box>> boxes = new Dictionary<Part, List<Box>>();
            public readonly Dictionary<Part, Mesh> meshes = new Dictionary<Part, Mesh>();
            public string name;
        }

        private static readonly Dictionary<(CastleKind, int, int), Shape> shapes = new Dictionary<(CastleKind, int, int), Shape>();
        private static readonly Dictionary<int, (Mesh treads, Mesh sides)> flights = new Dictionary<int, (Mesh, Mesh)>();
        private static readonly Dictionary<(int, float), List<CastleDepthItem>> depthItems = new Dictionary<(int, float), List<CastleDepthItem>>();
        private static readonly Dictionary<(int, int), CastleBatch> batches = new Dictionary<(int, int), CastleBatch>();
        private static readonly Dictionary<(int, int), CastleLayout> layouts = new Dictionary<(int, int), CastleLayout>();

        private static bool PaperWalls(CastleKind kind) => kind == CastleKind.Tatami || kind == CastleKind.Corridor;
        private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Max(0.0, Math.Min(1.0, t));

        /// <summary>A room's parts in cells about its centre, built once per kind and size. The port of roomParts.</summary>
        private static Shape ShapeOf(CastleKind kind, int w, int h)
        {
            if (shapes.TryGetValue((kind, w, h), out Shape shape)) return shape;
            shape = new Shape { name = "castle " + kind + " " + w + "x" + h };
            shapes[(kind, w, h)] = shape;

            double X0 = -w / 2.0, Z0 = -h / 2.0, X1 = w / 2.0, Z1 = h / 2.0;
            double fx0 = X0 + 1, fz0 = Z0 + 1, fx1 = X1 - 1, fz1 = Z1 - 1;              // the floor inside the walls
            List<Box> New(Part part) => shape.boxes[part] = new List<Box>();

            New(Part.Floor).Add(new Box(fx0, fz0, fx1, fz1));
            var walls = New(Part.Walls);
            walls.Add(new Box(X0, Z0, X1, Z0 + 1)); walls.Add(new Box(X0, Z1 - 1, X1, Z1));
            walls.Add(new Box(X0, Z0 + 1, X0 + 1, Z1 - 1)); walls.Add(new Box(X1 - 1, Z0 + 1, X1, Z1 - 1));
            {
                const double e = 0.09;
                var lip = New(Part.Lip);
                lip.Add(new Box(fx0, fz0 - e, fx1, fz0)); lip.Add(new Box(fx0, fz1, fx1, fz1 + e));
                lip.Add(new Box(fx0 - e, fz0, fx0, fz1)); lip.Add(new Box(fx1, fz0, fx1 + e, fz1));
            }
            {
                const double e = 0.07;
                var edge = New(Part.Edge);
                edge.Add(new Box(X0, Z0, X1, Z0 + e)); edge.Add(new Box(X0, Z1 - e, X1, Z1));
                edge.Add(new Box(X0, Z0, X0 + e, Z1)); edge.Add(new Box(X1 - e, Z0, X1, Z1));
            }
            {
                // Pillars at the corners and about every 4 cells along each wall.
                const double p = 0.2;
                var posts = New(Part.Posts);
                void Post(double x, double z) => posts.Add(new Box(x - p, z - p, x + p, z + p));
                void Along(double a0, double a1, Action<double> each)
                {
                    int n = Math.Max(1, CastleLayout.RoundHalfUp((a1 - a0) / 4));
                    for (int i = 0; i <= n; i++) each(Lerp(a0, a1, i / (double)n));
                }
                Along(X0 + 0.5, X1 - 0.5, x => { Post(x, Z0 + 0.5); Post(x, Z1 - 0.5); });
                Along(Z0 + 0.5, Z1 - 0.5, z => { Post(X0 + 0.5, z); Post(X1 - 0.5, z); });
            }
            if (PaperWalls(kind))
            {
                // Shoji: a pale paper strip along each wall with dark lattice bars every half cell.
                const double a = 0.3, b = 0.7, t = 0.025;
                var paper = New(Part.Paper);
                paper.Add(new Box(X0 + 0.5, Z0 + a, X1 - 0.5, Z0 + b)); paper.Add(new Box(X0 + 0.5, Z1 - b, X1 - 0.5, Z1 - a));
                paper.Add(new Box(X0 + a, Z0 + 0.5, X0 + b, Z1 - 0.5)); paper.Add(new Box(X1 - b, Z0 + 0.5, X1 - a, Z1 - 0.5));
                var lattice = New(Part.Lattice);
                for (double x = X0 + 1; x < X1 - 0.6; x += 0.5)
                {
                    lattice.Add(new Box(x - t, Z0 + 0.3, x + t, Z0 + 0.7));
                    lattice.Add(new Box(x - t, Z1 - 0.7, x + t, Z1 - 0.3));
                }
                for (double z = Z0 + 1; z < Z1 - 0.6; z += 0.5)
                {
                    lattice.Add(new Box(X0 + 0.3, z - t, X0 + 0.7, z + t));
                    lattice.Add(new Box(X1 - 0.7, z - t, X1 - 0.3, z + t));
                }
                lattice.Add(new Box(X0 + 0.5, Z0 + 0.49, X1 - 0.5, Z0 + 0.51)); lattice.Add(new Box(X0 + 0.5, Z1 - 0.51, X1 - 0.5, Z1 - 0.49));
                lattice.Add(new Box(X0 + 0.49, Z0 + 0.5, X0 + 0.51, Z1 - 0.5)); lattice.Add(new Box(X1 - 0.51, Z0 + 0.5, X1 - 0.49, Z1 - 0.5));
            }
            else
            {
                const double a = 0.38, b = 0.62;
                var beam = New(Part.Beam);
                beam.Add(new Box(X0 + 0.5, Z0 + a, X1 - 0.5, Z0 + b)); beam.Add(new Box(X0 + 0.5, Z1 - b, X1 - 0.5, Z1 - a));
                beam.Add(new Box(X0 + a, Z0 + 0.5, X0 + b, Z1 - 0.5)); beam.Add(new Box(X1 - b, Z0 + 0.5, X1 - a, Z1 - 0.5));
            }
            if (kind == CastleKind.Tatami)
            {
                // Mats 2 x 1 in running bond; dark heri cloth on each mat's long sides; every other mat a
                // shade darker, as the weave turns.
                var mats = new List<(double a, double z, double b)>();
                int row = 0;
                for (double z = fz0; z < fz1 - 0.01; z += 1, row++)
                    for (double x = fx0 - (row % 2); x < fx1 - 0.01; x += 2) mats.Add((Math.Max(x, fx0), z, Math.Min(x + 2, fx1)));
                var shade = New(Part.Shade);
                var heri = New(Part.Heri);
                for (int i = 0; i < mats.Count; i++)
                {
                    var (a, z, b) = mats[i];
                    if (i % 2 == 1) shade.Add(new Box(a + 0.03, z + 0.06, b - 0.03, z + 0.94));
                    heri.Add(new Box(a, z, b, z + 0.06)); heri.Add(new Box(a, z + 0.94, b, z + 1)); heri.Add(new Box(b - 0.012, z, b + 0.012, z + 1));
                }
            }
            else
            {
                // Wooden boards along the room's long side, seams every half cell, joints staggered.
                bool flat = w >= h;
                double L0 = flat ? fx0 : fz0, L1 = flat ? fx1 : fz1, A0 = flat ? fz0 : fx0, A1 = flat ? fz1 : fx1;
                const double t = 0.018;
                var boards = New(Part.Boards);
                void Line(double l0, double l1, double a0, double a1) => boards.Add(flat ? new Box(l0, a0, l1, a1) : new Box(a0, l0, a1, l1));
                int row = 0;
                for (double a = A0; a < A1 - 0.01; a += 0.5, row++)
                {
                    if (a > A0) Line(L0, L1, a - t, a + t);
                    for (double j = L0 + 0.6 + (row * 1.37) % 2.4; j < L1 - 0.3; j += 2.4) Line(j - t, j + t, a, a + 0.5);
                }
            }
            if (kind == CastleKind.Hall)
            {
                bool flat = w >= h;
                const double half = 1.5, g = 0.06, i = 0.22;
                var runner = New(Part.Runner);
                runner.Add(flat ? new Box(fx0 + 0.5, -half, fx1 - 0.5, half) : new Box(-half, fz0 + 0.5, half, fz1 - 0.5));
                var gold = New(Part.Gold);
                if (flat)
                {
                    gold.Add(new Box(fx0 + 0.5, -half + i, fx1 - 0.5, -half + i + g));
                    gold.Add(new Box(fx0 + 0.5, half - i - g, fx1 - 0.5, half - i));
                }
                else
                {
                    gold.Add(new Box(-half + i, fz0 + 0.5, -half + i + g, fz1 - 0.5));
                    gold.Add(new Box(half - i - g, fz0 + 0.5, half - i, fz1 - 0.5));
                }
            }
            if (kind == CastleKind.Stair)
            {
                // A flight 3 cells wide down the long side, steps every 0.4 cells; the far end goes dark
                // (it leads down into the castle; look only).
                bool flat = w >= h;
                const double half = 1.5;
                double L0 = (flat ? fx0 : fz0) + 0.6, L1 = (flat ? fx1 : fz1) - 0.6;
                Box Across(double a0, double a1, double b0, double b1) => flat ? new Box(a0, b0, a1, b1) : new Box(b0, a0, b1, a1);
                New(Part.StairBed).Add(Across(L0, L1, -half, half));
                var treads = New(Part.Treads);
                for (double a = L0; a < L1 - 0.2; a += 0.4) treads.Add(Across(a, a + 0.2, -half + 0.15, half - 0.15));
                var stringers = New(Part.Stringers);
                stringers.Add(Across(L0, L1, -half, -half + 0.15)); stringers.Add(Across(L0, L1, half - 0.15, half));
                for (int i = 0; i < 3; i++)
                    New(Part.StairDark0 + i).Add(Across(Lerp(L0, L1, i / 3.0), L1, -half, half));
            }
            if (kind == CastleKind.Biwa)
            {
                // The dais: a raised tatami platform with a red lacquer border at the north, a gold folding
                // screen along the north wall behind it.
                New(Part.Dais).Add(new Box(-2.5, fz1 - 3.2, 2.5, fz1 - 0.3));
                New(Part.DaisMat).Add(new Box(-2.3, fz1 - 3.0, 2.3, fz1 - 0.5));
                var daisHeri = New(Part.DaisHeri);
                daisHeri.Add(new Box(-2.3, fz1 - 3.0, 2.3, fz1 - 2.94)); daisHeri.Add(new Box(-2.3, fz1 - 1.8, 2.3, fz1 - 1.74));
                daisHeri.Add(new Box(-2.3, fz1 - 0.56, 2.3, fz1 - 0.5)); daisHeri.Add(new Box(-0.02, fz1 - 3.0, 0.02, fz1 - 0.5));
                var screen = New(Part.Screen);
                for (int i = 0; i < 6; i++) screen.Add(new Box(-3 + i, fz1 - 0.28, -2.04 + i, fz1 - 0.02));
            }
            return shape;
        }

        /// <summary>One part of a room as a mesh in cells about the room's centre, drawn at the centre with scale k.</summary>
        internal static Mesh MeshOf(CastleKind kind, int w, int h, Part part)
        {
            Shape shape = ShapeOf(kind, w, h);
            if (shape.meshes.TryGetValue(part, out Mesh mesh)) return mesh;
            if (!shape.boxes.TryGetValue(part, out List<Box> boxes)) return null;
            var builder = new MeshBuilder();
            foreach (Box b in boxes) builder.Box(b, 0.0, 0.0);
            return shape.meshes[part] = builder.Build(shape.name + " " + part)[0];
        }

        /// <summary>
        /// Every part of a room at rest in the sketch's drawRoom order: whether it sits on the wall layer,
        /// its step above that layer, and its colour.
        /// </summary>
        private static IEnumerable<(Part part, bool wall, float step, Color colour)> RestingParts(CastleKind kind)
        {
            Color Tone(Color colour, float extra = 0f) => Color.Lerp(colour, Void, Mathf.Min(1f, extra));
            yield return (Part.Floor, false, 0f, Tone(kind == CastleKind.Tatami ? Tatami : WoodFloor, kind == CastleKind.Biwa ? 0.12f : 0f));
            if (kind == CastleKind.Tatami)
            {
                yield return (Part.Shade, false, 0.002f, Tone(TatamiShade));
                yield return (Part.Heri, false, 0.004f, Tone(Heri));
            }
            else yield return (Part.Boards, false, 0.002f, Tone(WoodSeam));
            if (kind == CastleKind.Hall)
            {
                yield return (Part.Runner, false, 0.006f, Tone(Lacquer, 0.2f));
                yield return (Part.Gold, false, 0.008f, Tone(Gold, 0.1f));
            }
            if (kind == CastleKind.Stair)
            {
                yield return (Part.StairBed, false, 0.006f, Tone(WallTop, 0.05f));
                yield return (Part.Treads, false, 0.008f, Tone(WoodFloor, 0.05f));
                yield return (Part.Stringers, false, 0.01f, Tone(WallWood));
                for (int i = 0; i < 3; i++) yield return (Part.StairDark0 + i, false, 0.012f + i * 0.001f, Fade(VoidDeep, 0.3f));
            }
            if (kind == CastleKind.Biwa)
            {
                yield return (Part.Dais, false, 0.006f, Tone(Lacquer, 0.1f));
                yield return (Part.DaisMat, false, 0.008f, Tone(Tatami));
                yield return (Part.DaisHeri, false, 0.01f, Tone(Heri));
                yield return (Part.Screen, true, 0.012f, Tone(Gold));
            }
            yield return (Part.Walls, true, 0f, Tone(WallWood));
            yield return (Part.Edge, true, 0.002f, Tone(Lacquer, 0.15f));
            yield return (Part.Lip, true, 0.004f, Tone(WallTop));
            if (PaperWalls(kind))
            {
                yield return (Part.Paper, true, 0.006f, Tone(Paper, 0.08f));
                yield return (Part.Lattice, true, 0.008f, Tone(Lattice));
            }
            else yield return (Part.Beam, true, 0.006f, Tone(WallTop, 0.05f));
            yield return (Part.Posts, true, 0.01f, Tone(WallWood, -0.05f));
        }

        // ---- a castle at rest -----------------------------------------------------------------------------

        /// <summary>The castle for a seed and room count, generated once and kept.</summary>
        internal static CastleLayout LayoutFor(int seed, int rooms)
        {
            if (!layouts.TryGetValue((seed, rooms), out CastleLayout layout)) layouts[(seed, rooms)] = layout = CastleLayout.Generate(seed, rooms);
            return layout;
        }

        /// <summary>The baked rooms of a castle, built once per seed and room count.</summary>
        internal static CastleBatch BatchFor(CastleLayout castle)
        {
            if (!batches.TryGetValue((castle.Seed, castle.Rooms.Count), out CastleBatch batch))
                batches[(castle.Seed, castle.Rooms.Count)] = batch = CastleBatch.Build(castle);
            return batch;
        }

        /// <summary>
        /// Everything of a castle at rest: the void (<paramref name="depth"/> adds its rooms at other
        /// depths), the rooms and open doorways, the lanterns. <paramref name="corner"/> is where the
        /// castle's cell (0, 0) has its lower-left corner. <paramref name="view"/> skips what moves and
        /// lies outside it; the baked rooms are a few meshes and always drawn.
        /// </summary>
        internal static void DrawCastle(CastleLayout castle, Vector2 corner, float seconds, in CastleLayers layers, bool depth = true, CellRect? view = null,
            CastleBatch batch = null, int skipRoom = -1)
        {
            var middle = new Vector2(corner.x + CastleLayout.Size / 2f, corner.y + CastleLayout.Size / 2f);
            DrawVoid(middle, seconds, castle.Seed, VoidReach, depth, 1f, layers, view);
            (batch ?? BatchFor(castle)).Draw(corner, layers);
            foreach (CastleRoom room in castle.Rooms)
            {
                if (room.Id == skipRoom) continue;
                if (view.HasValue && !Overlaps(view.Value, corner.x + room.X, corner.y + room.Z, corner.x + room.X + room.W, corner.y + room.Z + room.H, 2f))
                    continue;
                LanternGlows(room, CentreOf(corner, room), seconds, layers);
            }
        }

        /// <summary>The Castle sketch's void reaches 90 cells round the castle's middle.</summary>
        internal const float VoidReach = 90f;

        internal static Vector2 CentreOf(Vector2 corner, CastleRoom room) =>
            new Vector2(corner.x + room.X + room.W / 2f, corner.y + room.Z + room.H / 2f);

        private static bool Overlaps(CellRect view, float x0, float z0, float x1, float z1, float margin) =>
            x1 + margin >= view.minX && x0 - margin <= view.maxX + 1 && z1 + margin >= view.minZ && z0 - margin <= view.maxZ + 1;

        /// <summary>A resting room's lanterns: a warm pool on the floor and a bright core over the baked body, flickering a little.</summary>
        internal static void LanternGlows(CastleRoom room, Vector2 centre, float s, in CastleLayers layers)
        {
            List<(double x, double z)> lanterns = CastleLayout.LanternsOf(room);
            for (int i = 0; i < lanterns.Count; i++)
            {
                var at = new Vector2(centre.x + (float)lanterns[i].x, centre.y + (float)lanterns[i].z);
                float flicker = 0.9f + 0.1f * Mathf.Sin(s * (5f + Rand(room.Id * 13 + i) * 4f) + i * 1.7f) * Mathf.Sin(s * 1.9f + room.Id + i);
                Sprite(at, 3.6f, 3.6f, Fade(Lantern, 0.13f * flicker), glow, layers.Floor + 0.014f);
                Sprite(at, 0.55f, 0.55f, Fade(LanternCore, 0.55f * flicker), glow, layers.Wall + 0.022f);
            }
        }

        // ---- the void -----------------------------------------------------------------------------------

        /// <summary>The rooms and flights below the void for a seed and reach, made once and kept.</summary>
        internal static List<CastleDepthItem> DepthItemsFor(int seed, float reach)
        {
            if (!depthItems.TryGetValue((seed, reach), out List<CastleDepthItem> items))
                depthItems[(seed, reach)] = items = CastleLayout.DepthItems(seed, reach);
            return items;
        }

        /// <summary>
        /// Black under everything, rooms and stair flights at two depths below it (dimmer, smaller,
        /// drifting, some turned), and the void's dark fog over them. The port of drawVoid.
        /// </summary>
        internal static void DrawVoid(Vector2 centre, float s, int seed, float reach, bool depth, float alpha, in CastleLayers layers, CellRect? view = null)
        {
            DrawMesh(MeshPool.plane10, centre, layers.Back, reach * 3f, reach * 3f, 0f, Fade(VoidDeep, alpha), solid);
            if (depth)
            {
                List<CastleDepthItem> items = DepthItemsFor(seed, reach);
                for (int i = 0; i < items.Count; i++)
                {
                    CastleDepthItem it = items[i];
                    bool further = it.Level == 2;
                    float k = further ? 0.5f : 0.72f, dim = further ? 0.72f : 0.52f;
                    float drift = Mathf.Sin(s * DriftRate + (float)it.DriftP) * (float)it.DriftA * (further ? 0.6f : 1f);
                    var c = new Vector2(centre.x + (float)it.X + drift, centre.y + (float)it.Z + drift * 0.4f);
                    if (view.HasValue)
                    {
                        float half = Mathf.Max(it.Flight, Mathf.Max(it.W, it.H)) * k * 0.75f + 1f;
                        if (!Overlaps(view.Value, c.x - half, c.y - half, c.x + half, c.y + half, 0f)) continue;
                    }
                    float y = layers.Depth + ((further ? 0f : 0.12f) + (i % 20) * 0.004f) * layers.DepthStep;
                    if (it.Flight > 0) Flight(it.Flight, c, k, (float)it.Rot, dim, y, alpha, layers.DepthStep);
                    else FarRoom(it, c, k, (float)it.Rot + (float)it.Spin * s, dim, y, y + 0.03f * layers.DepthStep, s, further ? 0.7f : 1f, alpha, layers.DepthStep);
                }
            }
            // The void terrain's look: dark and see-through, so what lies below reads as far away.
            DrawMesh(MeshPool.plane10, centre, layers.Fog, reach * 3f, reach * 3f, 0f, Fade(Void, 0.38f * alpha), solid);
        }

        /// <summary>A room below the void: floor, walls, runner or dark stair, paper, and two lantern glows (the sketch's far drawRoom).</summary>
        private static void FarRoom(CastleDepthItem it, Vector2 c, float k, float rot, float dim, float layer, float wallLayer, float s,
            float lamps, float alpha, float step)
        {
            Color Tone(Color colour, float extra = 0f) => Fade(Color.Lerp(colour, Void, Mathf.Min(1f, dim + extra)), alpha);
            DrawMesh(MeshPool.plane10, c, layer, (it.W - 2) * k, (it.H - 2) * k, rot, Tone(it.Kind == CastleKind.Tatami ? Tatami : WoodFloor), solid);
            if (it.Kind == CastleKind.Hall) DrawMesh(MeshOf(it.Kind, it.W, it.H, Part.Runner), c, layer + 0.006f * step, k, k, rot, Tone(Lacquer, 0.1f), solid);
            if (it.Kind == CastleKind.Stair) DrawMesh(MeshOf(it.Kind, it.W, it.H, Part.StairDark1), c, layer + 0.006f * step, k, k, rot, Fade(VoidDeep, alpha * 0.5f), solid);
            DrawMesh(MeshOf(it.Kind, it.W, it.H, Part.Walls), c, wallLayer, k, k, rot, Tone(WallWood), solid);
            if (PaperWalls(it.Kind)) DrawMesh(MeshOf(it.Kind, it.W, it.H, Part.Paper), c, wallLayer + 0.006f * step, k, k, rot, Tone(Paper, 0.1f), solid);
            float cr = Mathf.Cos(-rot * Mathf.Deg2Rad), sr = Mathf.Sin(-rot * Mathf.Deg2Rad);
            List<(double x, double z)> lanterns = CastleLayout.LanternsOf(it.Kind, it.W, it.H);
            for (int i = 0; i < Math.Min(2, lanterns.Count); i++)
            {
                float lx = (float)lanterns[i].x, lz = (float)lanterns[i].z;
                var at = new Vector2(c.x + (lx * cr - lz * sr) * k, c.y + (lx * sr + lz * cr) * k);
                float flicker = 0.85f + 0.15f * Mathf.Sin(s * (1.3f + Rand(it.Id * 13 + i)) + i * 1.7f + it.Id);
                Sprite(at, 2.6f * k, 2.6f * k, Fade(Lantern, 0.2f * flicker * lamps * alpha), glow, wallLayer + 0.01f * step);
                Sprite(at, 0.5f * k, 0.5f * k, Fade(LanternCore, 0.6f * flicker * lamps * alpha), glow, wallLayer + 0.012f * step);
            }
        }

        /// <summary>A flight of stairs with no room round it: 3 cells wide, steps every 0.4 cells, the far end going dark.</summary>
        private static void Flight(int length, Vector2 c, float k, float rot, float dim, float layer, float alpha, float step)
        {
            if (!flights.TryGetValue(length, out var meshes))
            {
                const double half = 1.5;
                double L0 = -length / 2.0, L1 = length / 2.0;
                var treads = new MeshBuilder();
                for (double a = L0; a < L1 - 0.2; a += 0.4) treads.Box(new Box(a, -half + 0.12, a + 0.2, half - 0.12), 0.0, 0.0);
                var sides = new MeshBuilder();
                sides.Box(new Box(L0, -half, L1, -half + 0.12), 0.0, 0.0);
                sides.Box(new Box(L0, half - 0.12, L1, half), 0.0, 0.0);
                flights[length] = meshes = (treads.Build("castle flight " + length + " treads")[0], sides.Build("castle flight " + length + " sides")[0]);
            }
            Color Tone(Color colour) => Fade(Color.Lerp(colour, Void, dim), alpha);
            DrawMesh(MeshPool.plane10, c, layer, length * k, 3f * k, rot, Tone(WallTop), solid);
            DrawMesh(meshes.treads, c, layer + 0.002f * step, k, k, rot, Tone(WoodFloor), solid);
            DrawMesh(meshes.sides, c, layer + 0.004f * step, k, k, rot, Tone(WallWood), solid);
            float r = -rot * Mathf.Deg2Rad, ux = Mathf.Cos(r), uz = Mathf.Sin(r), l0 = -length / 2f, l1 = length / 2f;
            float[] from = { 0.35f, 0.7f }, dark = { 0.35f, 0.5f };
            for (int i = 0; i < 2; i++)
            {
                float mid = (l0 + length * from[i] + l1) / 2f, span = l1 - (l0 + length * from[i]);
                DrawMesh(MeshPool.plane10, new Vector2(c.x + ux * mid * k, c.y + uz * mid * k), layer + (0.006f + i * 0.0002f) * step,
                    span * k, 3f * k, rot, Fade(VoidDeep, alpha * dark[i]), solid);
            }
        }

        // ---- building meshes ----------------------------------------------------------------------------

        /// <summary>Vertices and triangles gathered for one or more meshes; a new mesh every <see cref="MostVertices"/> vertices.</summary>
        internal sealed class MeshBuilder
        {
            private readonly List<List<Vector3>> vertices = new List<List<Vector3>> { new List<Vector3>() };
            private readonly List<List<int>> triangles = new List<List<int>> { new List<int>() };

            private List<Vector3> Room(int more)
            {
                if (vertices[vertices.Count - 1].Count + more > MostVertices) { vertices.Add(new List<Vector3>()); triangles.Add(new List<int>()); }
                return vertices[vertices.Count - 1];
            }

            /// <summary>A rectangle offset by (x, z), in the sketch's clockwise order.</summary>
            public void Box(Box b, double x, double z)
            {
                List<Vector3> v = Room(4);
                List<int> t = triangles[triangles.Count - 1];
                int n = v.Count;
                v.Add(new Vector3((float)(b.X0 + x), 0f, (float)(b.Z0 + z)));
                v.Add(new Vector3((float)(b.X0 + x), 0f, (float)(b.Z1 + z)));
                v.Add(new Vector3((float)(b.X1 + x), 0f, (float)(b.Z1 + z)));
                v.Add(new Vector3((float)(b.X1 + x), 0f, (float)(b.Z0 + z)));
                t.Add(n); t.Add(n + 1); t.Add(n + 2); t.Add(n); t.Add(n + 2); t.Add(n + 3);
            }

            /// <summary>An ellipse at (x, z) with radii rx, rz: a fan written clockwise on screen.</summary>
            public void Disc(double x, double z, double rx, double rz)
            {
                List<Vector3> v = Room(DiscSegments + 2);
                List<int> t = triangles[triangles.Count - 1];
                int centre = v.Count;
                v.Add(new Vector3((float)x, 0f, (float)z));
                for (int i = 0; i <= DiscSegments; i++)
                {
                    double a = i * Math.PI * 2.0 / DiscSegments;
                    v.Add(new Vector3((float)(x + Math.Cos(a) * rx), 0f, (float)(z + Math.Sin(a) * rz)));
                    if (i > 0) { t.Add(centre); t.Add(centre + i + 1); t.Add(centre + i); }
                }
            }

            public bool Empty => vertices[0].Count == 0;

            public List<Mesh> Build(string name)
            {
                var meshes = new List<Mesh>();
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (vertices[i].Count == 0) continue;
                    var mesh = new Mesh { name = name + (i > 0 ? " " + i : ""), vertices = vertices[i].ToArray(), triangles = triangles[i].ToArray() };
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    meshes.Add(mesh);
                }
                return meshes;
            }
        }

        /// <summary>
        /// Every room of a castle at rest, its open doorways and its lantern bodies, baked into one mesh
        /// per (layer, step, colour) in cells from the castle's corner. Drawn at the corner with scale 1.
        /// </summary>
        internal sealed class CastleBatch
        {
            private readonly List<(bool wall, float step, Color colour, Mesh mesh)> parts = new List<(bool, float, Color, Mesh)>();

            public int MeshCount => parts.Count;

            public void Draw(Vector2 corner, in CastleLayers layers)
            {
                foreach (var (wall, step, colour, mesh) in parts)
                    DrawMesh(mesh, corner, (wall ? layers.Wall : layers.Floor) + step, 1f, 1f, 0f, colour, solid);
            }

            /// <summary>One room on its own, no doorways: what a sliding room is drawn with, at its offset.</summary>
            public static CastleBatch BuildRoom(CastleRoom room) => Build(CastleLayout.FromRooms(new[] { room }, 0));

            /// <param name="skipRoom">A room left out, with every doorway into it: a room that is sliding is drawn on its own.</param>
            /// <param name="sealedDoors">Doorway keys left out: a sealed doorway is drawn shut and barred each frame.</param>
            public static CastleBatch Build(CastleLayout castle, int skipRoom = -1, ICollection<string> sealedDoors = null)
            {
                // Insertion order is draw order within a layer, which only matters where two parts overlap:
                // never at one height, since rooms keep apart.
                var builders = new Dictionary<(bool, float, float, float, float, float), (Color colour, MeshBuilder builder)>();
                var order = new List<(bool, float, float, float, float, float)>();
                MeshBuilder Into(bool wall, float step, Color colour)
                {
                    var key = (wall, step, colour.r, colour.g, colour.b, colour.a);
                    if (!builders.TryGetValue(key, out var entry)) { builders[key] = entry = (colour, new MeshBuilder()); order.Add(key); }
                    return entry.builder;
                }

                foreach (CastleRoom room in castle.Rooms)
                {
                    if (room.Id == skipRoom) continue;
                    double cx = room.X + room.W / 2.0, cz = room.Z + room.H / 2.0;
                    Shape shape = ShapeOf(room.Kind, room.W, room.H);
                    foreach (var (part, wall, step, colour) in RestingParts(room.Kind))
                    {
                        MeshBuilder into = Into(wall, step, colour);
                        foreach (Box b in shape.boxes[part]) into.Box(b, cx, cz);
                    }
                    // The lantern bodies: a dark wooden frame under a pale paper disc.
                    foreach (var (lx, lz) in CastleLayout.LanternsOf(room))
                    {
                        Into(true, 0.02f, WallWood).Disc(cx + lx, cz + lz - 0.03, 0.19, 0.17);
                        Into(true, 0.021f, LanternPaper).Disc(cx + lx, cz + lz, 0.15, 0.15);
                    }
                }

                // Every doorway open: in each of its two wall cells, the floor through the wall with a seam at
                // each face, and the jambs (the sketch's wallDoor at open 1, whose leaves are in the pockets).
                foreach (CastleDoorway door in castle.Doorways)
                    foreach (var (x, z) in door.Cells)
                    {
                        if (door.A == skipRoom || door.B == skipRoom) continue;
                        if (sealedDoors != null && sealedDoors.Contains(door.Key)) continue;
                        double cx = x + 0.5, cz = z + 0.5;
                        Box Rect(double a, double b, double la, double lb) =>
                            door.AlongZ ? new Box(la, a, lb, b) : new Box(a, la, b, lb);
                        Into(true, 0.03f, WoodFloor).Box(Rect(-0.5, 0.5, -0.5, 0.5), cx, cz);
                        Into(true, 0.031f, WoodSeam).Box(Rect(-0.5, 0.5, -0.5, -0.42), cx, cz);
                        Into(true, 0.031f, WoodSeam).Box(Rect(-0.5, 0.5, 0.42, 0.5), cx, cz);
                        Into(true, 0.04f, WallWood).Box(Rect(-0.5, -0.4, -0.22, 0.22), cx, cz);
                        Into(true, 0.04f, WallWood).Box(Rect(0.4, 0.5, -0.22, 0.22), cx, cz);
                    }

                var batch = new CastleBatch();
                foreach (var key in order)
                {
                    var (colour, builder) = builders[key];
                    if (builder.Empty) continue;
                    foreach (Mesh mesh in builder.Build("castle " + castle.Seed + " baked " + batch.parts.Count))
                        batch.parts.Add((key.Item1, key.Item2, colour, mesh));
                }
                return batch;
            }
        }
    }
}
