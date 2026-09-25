using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.CastleRoomGraphics;

namespace RimArt
{
    /// <summary>
    /// The Infinity Castle's effects: the floor door that opens under a pawn to take it in or bring it
    /// out, the outline where one will open, the shaft's dark and streaks while a pawn goes through, the
    /// strum's rings, and the flash of a room the castle answers with. The port of floorDoor, doorMark,
    /// doorAt, sinking and rising (their shaft half: the stand-in pawn is not drawn), strum, thinRing,
    /// roomFlash, wallDoor, outline and dustLine in Tools/VfxLab/web/sketches/lib/infinity-castle.js. Every one is level, so it looks the
    /// same from every side, and takes ages, not state, so a preview can be scrubbed.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class CastleEffectGraphics
    {
        private static readonly Color FrameWood = new Color(0.16f, 0.09f, 0.05f), ShaftWall = new Color(0.20f, 0.11f, 0.06f);
        /// <summary>A floor door's frame is 1.3 cells across.</summary>
        public const float DoorSize = 1.3f;

        // One door's life from its opening moment (age 0): the frame fades in over 0.1 s before, the leaves
        // snap open in 0.18 s, stay open for the hold, close in 0.25 s, and the frame fades over 0.3 s.
        public const float DoorFadeIn = 0.1f, DoorOpens = 0.18f, DoorCloses = 0.25f, DoorFadeOut = 0.3f;
        /// <summary>A pawn starts through a floor door once the leaves are half open.</summary>
        public const float DoorThrough = 0.09f;

        public static float Clamp(float x) => Mathf.Clamp01(x);
        public static float EaseOut(float x) { float u = 1f - Clamp(x); return 1f - u * u * u; }
        public static float Bump(float x) => x > 0f && x < 1f ? Mathf.Sin(x * Mathf.PI) : 0f;

        /// <summary>A door's alpha and how open its leaves are, <paramref name="age"/> seconds from its opening moment.</summary>
        public static void DoorAt(float age, float hold, out float alpha, out float open)
        {
            open = 0f;
            if (age < -DoorFadeIn) { alpha = 0f; return; }
            if (age < 0f) { alpha = 1f + age / DoorFadeIn; return; }
            float shut = DoorOpens + hold;
            alpha = 1f;
            if (age < shut) { open = EaseOut(age / DoorOpens); return; }
            if (age < shut + DoorCloses) { open = 1f - Smooth((age - shut) / DoorCloses); return; }
            alpha = 1f - Clamp((age - shut - DoorCloses) / DoorFadeOut);
        }

        /// <summary>How long a door with this hold is on screen from its opening moment.</summary>
        public static float DoorEnd(float hold) => DoorOpens + hold + DoorCloses + DoorFadeOut;

        /// <summary>
        /// A pair of shoji leaves lying in the floor inside a dark wood frame. <paramref name="open"/>
        /// slides them apart east-west; under them is the shaft: black, its far (north) wall faintly lit, a
        /// warm glow deep down.
        /// </summary>
        public static void FloorDoor(Vector2 c, float open, float alpha, float s, float altitude)
        {
            if (alpha <= 0f) return;
            const float F = 0.13f, H = DoorSize - 2f * F;
            float y = altitude, a = alpha;
            Sprite(c, DoorSize, DoorSize, Fade(FrameWood, a), solid, y);
            Sprite(c, DoorSize - 0.05f, DoorSize - 0.05f, Fade(WallTop, a * 0.6f), solid, y + 0.001f);
            Sprite(c, H + 0.02f, H + 0.02f, Fade(FrameWood, a), solid, y + 0.002f);
            Sprite(c, H, H, Fade(VoidDeep, a), solid, y + 0.003f);
            if (open > 0f)
            {
                Sprite(new Vector2(c.x, c.y + H / 2f - 0.15f), H, 0.3f, Fade(ShaftWall, a * Mathf.Min(1f, open * 2f)), solid, y + 0.004f);
                Sprite(new Vector2(c.x, c.y + H / 2f - 0.32f), H, 0.06f, Fade(ShaftWall, a * 0.5f * Mathf.Min(1f, open * 2f)), solid, y + 0.005f);
                Sprite(new Vector2(c.x, c.y + H / 2f - 0.015f), H, 0.03f, Fade(WallTop, a * open), solid, y + 0.006f);
                float pulse = 0.85f + 0.15f * Mathf.Sin(s * 3.1f + c.x);
                Sprite(new Vector2(c.x, c.y - 0.1f), H * 0.75f, H * 0.55f, Fade(Lantern, 0.3f * a * open * pulse), glow, y + 0.007f);
                Sprite(new Vector2(c.x, c.y - 0.12f), H * 0.22f, H * 0.16f, Fade(LanternCore, 0.35f * a * open * pulse), glow, y + 0.008f);
            }
            float length = H / 2f * (1f - open);
            for (int side = -1; side <= 1; side += 2)
            {
                if (length < 0.005f) continue;
                float edge = side * H / 2f, lead = side * (H / 2f - length), x0 = Mathf.Min(edge, lead), x1 = Mathf.Max(edge, lead);
                Sprite(new Vector2(c.x + (x0 + x1) / 2f, c.y), x1 - x0, H, Fade(Paper, a), solid, y + 0.01f);
                // Lattice fixed to the leaf, so it slides with it.
                for (float u = H / 6f; u < H / 2f; u += H / 6f)
                {
                    float p = lead + side * u;
                    if ((p - x0) * (p - x1) < 0f) Sprite(new Vector2(c.x + p, c.y), 0.022f, H, Fade(Lattice, a), solid, y + 0.012f);
                }
                Sprite(new Vector2(c.x + (x0 + x1) / 2f, c.y - H / 6f), x1 - x0, 0.022f, Fade(Lattice, a), solid, y + 0.012f);
                Sprite(new Vector2(c.x + (x0 + x1) / 2f, c.y + H / 6f), x1 - x0, 0.022f, Fade(Lattice, a), solid, y + 0.0121f);
                Sprite(new Vector2(c.x + lead + side * 0.02f, c.y), 0.045f, H, Fade(Lattice, a), solid, y + 0.013f);
            }
        }

        // ---- doors in walls ---------------------------------------------------------------------------

        private static readonly Color BarRed = new Color(0.72f, 0.30f, 0.20f), BarIron = new Color(0.25f, 0.25f, 0.27f);
        private static readonly Color Dust = new Color(0.62f, 0.52f, 0.40f);
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);

        /// <summary>
        /// One wall cell of a doorway (a doorway is two such cells, one per room), <paramref name="c"/> its
        /// centre. <paramref name="alongZ"/>: the wall runs north-south and the leaves slide along z.
        /// <paramref name="open"/> 0..1 slides the two paper leaves into the wall; <paramref name="sealed"/>
        /// 0..1 slides the bar in from the side and locks it. The port of wallDoor. At open 1 and sealed 0
        /// it is what <see cref="CastleRoomGraphics.CastleBatch"/> bakes for a resting doorway.
        /// </summary>
        public static void WallDoor(Vector2 c, bool alongZ, float open, float sealedBar, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            void Rect(float a, float b, float la, float lb, float y, Color col)
            {
                // a..b along the wall, la..lb across it, cell-centred.
                float cx = c.x + (alongZ ? (la + lb) / 2f : (a + b) / 2f), cz = c.y + (alongZ ? (a + b) / 2f : (la + lb) / 2f);
                float sx = alongZ ? lb - la : b - a, sz = alongZ ? b - a : lb - la;
                if (sx > 0.001f && sz > 0.001f) Sprite(new Vector2(cx, cz), sx, sz, Fade(col, col.a * alpha), solid, y);
            }
            Rect(-0.5f, 0.5f, -0.5f, 0.5f, altitude, WoodFloor);                       // the threshold: floor through the wall
            Rect(-0.5f, 0.5f, -0.5f, -0.42f, altitude + 0.001f, WoodSeam);
            Rect(-0.5f, 0.5f, 0.42f, 0.5f, altitude + 0.001f, WoodSeam);
            // Leaves: each half a cell long, 0.24 thick; opening slides them into the wall pockets.
            float length = 0.5f * (1f - open);
            for (int side = -1; side <= 1; side += 2)
            {
                if (length < 0.01f) continue;
                float e = side * 0.5f, i = side * (0.5f - length);
                float a = Mathf.Min(e, i), b = Mathf.Max(e, i);
                Rect(a, b, -0.14f, 0.14f, altitude + 0.004f, Lattice);
                Rect(a + 0.03f, b - 0.03f, -0.1f, 0.1f, altitude + 0.006f, Paper);
                for (float u = 0.125f; u < 0.5f; u += 0.125f)
                {
                    float p = i + side * u;
                    if ((p - a) * (p - b) < 0f) Rect(p - 0.012f, p + 0.012f, -0.1f, 0.1f, altitude + 0.008f, Lattice);
                }
                Rect(Mathf.Min(i, i + side * 0.04f), Mathf.Max(i, i + side * 0.04f), -0.14f, 0.14f, altitude + 0.009f, Lattice);
            }
            Rect(-0.5f, -0.4f, -0.22f, 0.22f, altitude + 0.01f, WallWood);            // jambs
            Rect(0.4f, 0.5f, -0.22f, 0.22f, altitude + 0.01f, WallWood);
            if (sealedBar > 0f)
            {
                // The bar (kannuki): dark lacquered wood across both leaves, sliding in from the south/east.
                float off = (1f - Smooth(sealedBar)) * 1.2f, show = Mathf.Min(1f, sealedBar * 3f);
                Rect(-0.55f + off, 0.55f + off, -0.07f, 0.07f, altitude + 0.014f, Fade(Lacquer, show));
                Rect(-0.55f + off, 0.55f + off, 0.02f, 0.05f, altitude + 0.015f, Fade(BarRed, show));
                Rect(-0.3f + off, -0.22f + off, -0.12f, 0.12f, altitude + 0.016f, Fade(BarIron, show));
                Rect(0.22f + off, 0.3f + off, -0.12f, 0.12f, altitude + 0.016f, Fade(BarIron, show));
            }
        }

        /// <summary>A pale outline round a room's walls (Shift's afterimages). The port of outline.</summary>
        public static void Outline(CastleRoom room, Vector2 centre, float alpha, float wallAltitude, float width = 0.16f) =>
            OutlineRect(centre, room.W, room.H, alpha, wallAltitude, width);

        /// <summary>A pale outline round a w x h rectangle at <paramref name="centre"/> (a doorway lit by a command).</summary>
        public static void OutlineRect(Vector2 centre, float w, float h, float alpha, float wallAltitude, float width = 0.16f)
        {
            if (alpha <= 0.005f) return;
            float t = width;
            Color line = Fade(CastleRoomGraphics.Strum, alpha);
            Sprite(new Vector2(centre.x, centre.y - h / 2f + t / 2f), w, t, line, whiteGlow, wallAltitude + 0.05f);
            Sprite(new Vector2(centre.x, centre.y + h / 2f - t / 2f), w, t, line, whiteGlow, wallAltitude + 0.0501f);
            Sprite(new Vector2(centre.x - w / 2f + t / 2f, centre.y), t, h, line, whiteGlow, wallAltitude + 0.0502f);
            Sprite(new Vector2(centre.x + w / 2f - t / 2f, centre.y), t, h, line, whiteGlow, wallAltitude + 0.0503f);
        }

        /// <summary>Dust thrown along a segment (a room meeting another, a wall slamming): puffs that rise and fade. The port of dustLine.</summary>
        public static void DustLine(Vector2 a, Vector2 b, float age, float altitude, float life = 0.7f, int count = 10, float alpha = 0.6f, int seed = 0, float spread = 0.5f)
        {
            if (age < 0f || age > life) return;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count, u = Clamp(age / (life * (0.6f + Rand(i + seed) * 0.4f)));
                if (u >= 1f) continue;
                float x = Mathf.Lerp(a.x, b.x, t) + (Rand(i + seed + 10) - 0.5f) * spread, z = Mathf.Lerp(a.y, b.y, t) + (Rand(i + seed + 20) - 0.5f) * spread;
                float h = u * 0.5f * (0.5f + Rand(i + seed + 30));
                Sprite(new Vector2(x, z + h * Lift), 0.45f + u * 0.5f, 0.35f + u * 0.4f, Fade(Dust, Bump(Mathf.Min(1f, u * 1.3f + 0.1f)) * alpha), puff, altitude + 0.01f + i * 0.0001f);
            }
        }

        /// <summary>The lab's Lift: how far a thing rising one cell shows on a top-down screen.</summary>
        private const float Lift = 0.6f;

        /// <summary>A thin pale outline where a floor door will open, shown while the caster winds up.</summary>
        public static void DoorMark(Vector2 c, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            const float t = 0.045f, h = DoorSize / 2f;
            Sprite(new Vector2(c.x, c.y - h), DoorSize, t, Fade(CastleRoomGraphics.Strum, alpha), whiteGlow, altitude);
            Sprite(new Vector2(c.x, c.y + h), DoorSize, t, Fade(CastleRoomGraphics.Strum, alpha), whiteGlow, altitude + 0.0001f);
            Sprite(new Vector2(c.x - h, c.y), t, DoorSize, Fade(CastleRoomGraphics.Strum, alpha), whiteGlow, altitude + 0.0002f);
            Sprite(new Vector2(c.x + h, c.y), t, DoorSize, Fade(CastleRoomGraphics.Strum, alpha), whiteGlow, altitude + 0.0003f);
        }

        /// <summary>
        /// The shaft's part of a pawn going down through an open floor door, <paramref name="u"/> 0 to 1:
        /// its dark closing over the pawn, and pale streaks. The pawn itself is the game's.
        /// </summary>
        public static void Sinking(Vector2 at, float u, in CastleLayers layers)
        {
            if (u >= 1f) return;
            float e = Smooth(u);
            Sprite(at, DoorSize - 0.26f, DoorSize - 0.26f, Fade(VoidDeep, 0.85f * Mathf.Pow(Mathf.Max(0f, e), 1.4f)), solid, layers.Pawn + 0.01f);
            Streaks(at, u, layers);
        }

        /// <summary>The reverse: the shaft's dark lifting off a pawn coming up, and the streaks.</summary>
        public static void Rising(Vector2 at, float u, in CastleLayers layers)
        {
            if (u <= 0f) return;
            float up = Smooth(Mathf.Min(1f, u / 0.7f));
            Sprite(at, DoorSize - 0.26f, DoorSize - 0.26f, Fade(VoidDeep, 0.85f * Mathf.Pow(Mathf.Max(0f, 1f - up), 1.4f)), solid, layers.Pawn + 0.01f);
            Streaks(at, u, layers);
        }

        /// <summary>Thin pale lines inside the shaft, running past the pawn going through.</summary>
        private static void Streaks(Vector2 at, float u, in CastleLayers layers)
        {
            float a = Bump(u) * 0.5f;
            if (a <= 0.01f) return;
            for (int i = 0; i < 5; i++)
            {
                float x = at.x + (Rand(i + 40) - 0.5f) * 0.8f, phase = (u * 2.2f + Rand(i + 50)) % 1f, z = at.y - 0.45f + phase * 0.9f;
                Sprite(new Vector2(x, z), 0.025f, 0.22f + Rand(i + 60) * 0.2f, Fade(CastleRoomGraphics.Strum, a * Bump(phase)), whiteGlow, layers.Pawn + 0.012f + i * 0.0001f);
            }
        }

        /// <summary>A level ring <paramref name="width"/> cells wide at any radius (a scaled ring mesh would thicken with it).</summary>
        public static void ThinRing(Vector2 at, float radius, float width, Color colour, float altitude, Material material = null)
        {
            if (radius <= 0f || colour.a <= 0.001f) return;
            int n = Mathf.Max(24, Mathf.Min(96, CastleLayout.RoundHalfUp(radius * 10.0)));
            float r0 = Mathf.Max(0f, radius - width / 2f), r1 = radius + width / 2f;
            Sides(n + 1, out Vector2[] inner, out Vector2[] outer);
            for (int i = 0; i <= n; i++)
            {
                float angle = i / (float)n * Mathf.PI * 2f, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                inner[i] = new Vector2(at.x + c * r0, at.y + s * r0);
                outer[i] = new Vector2(at.x + c * r1, at.y + s * r1);
            }
            Strip(inner, outer, colour, material ?? whiteGlow, altitude);
        }

        /// <summary>
        /// The strum: three rings out from the biwa, 0.07 s apart, and small sound arcs either side.
        /// <paramref name="age"/> is seconds since the bachi crossed the strings.
        /// </summary>
        public static void Strum(Vector2 at, float age, float reach, float life, float altitude, float alpha = 1f)
        {
            if (age < 0f || age > life + 0.2f) return;
            for (int i = 0; i < 3; i++)
            {
                float a = age - i * 0.07f;
                if (a < 0f || a > life) continue;
                float u = a / life;
                ThinRing(at, reach * EaseOut(u), 0.09f - i * 0.02f, Fade(CastleRoomGraphics.Strum, (1f - u) * (0.75f - i * 0.2f) * alpha), altitude + i * 0.002f);
            }
            float sound = 1f - Clamp(age / 0.35f);
            if (sound <= 0f) return;
            for (int side = -1; side <= 1; side += 2)
                for (int j = 0; j < 3; j++)
                {
                    float r0 = 0.32f + j * 0.14f + age * 0.8f;
                    Sides(7, out Vector2[] near, out Vector2[] far);
                    for (int i = 0; i <= 6; i++)
                    {
                        float a = (side > 0 ? 0f : Mathf.PI) + (i / 6f - 0.5f) * 1.1f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                        near[i] = new Vector2(at.x + c * r0, at.y + s * r0 * 0.9f);
                        far[i] = new Vector2(at.x + c * (r0 + 0.035f), at.y + s * (r0 + 0.035f) * 0.9f);
                    }
                    Strip(near, far, Fade(CastleRoomGraphics.Strum, sound * (0.7f - j * 0.18f) * alpha), solid, altitude + 0.01f + (side + 1) * 0.0005f + j * 0.0001f);
                }
        }

        /// <summary>
        /// The castle answering a strum: the room's outline lights up warm and fades over
        /// <paramref name="life"/> seconds. <paramref name="centre"/> is the room's middle.
        /// </summary>
        public static void RoomFlash(CastleRoom room, Vector2 centre, float age, float wallAltitude, float life = 0.45f, float alpha = 1f)
        {
            if (age < 0f || age > life) return;
            float f = (1f - age / life) * alpha, w = room.W, h = room.H;
            const float t = 0.22f;
            Color line = Fade(CastleRoomGraphics.Strum, 0.55f * f);
            Sprite(new Vector2(centre.x, centre.y - h / 2f + t / 2f), w, t, line, whiteGlow, wallAltitude + 0.05f);
            Sprite(new Vector2(centre.x, centre.y + h / 2f - t / 2f), w, t, line, whiteGlow, wallAltitude + 0.0501f);
            Sprite(new Vector2(centre.x - w / 2f + t / 2f, centre.y), t, h, line, whiteGlow, wallAltitude + 0.0502f);
            Sprite(new Vector2(centre.x + w / 2f - t / 2f, centre.y), t, h, line, whiteGlow, wallAltitude + 0.0503f);
            Sprite(centre, w * 1.1f, h * 1.1f, Fade(CastleRoomGraphics.Strum, 0.1f * f), glow, wallAltitude + 0.049f);
        }
    }
}
