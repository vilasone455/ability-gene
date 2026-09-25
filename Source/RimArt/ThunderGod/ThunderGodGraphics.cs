using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by the Flying Thunder God jump, the chain, Guiding Thunder and
    /// Rasengan: the star glint, what is left where the caster jumped from, the line between two
    /// places, the floor script and brackets, the slash and the sparks. The port of the lab's
    /// lib/flying-thunder-god.js. Everything is flat on the ground or a line between two points, so
    /// it turns with the direction it is given. Every function takes ages and times and keeps no
    /// state. The stand-in pawns and the kunai texture of the sketches are not drawn here.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ThunderGodGraphics
    {
        internal static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        internal static readonly Material whiteGlow =
            new Material(ShaderDatabase.MoteGlow) { mainTexture = BaseContent.WhiteTex };
        internal static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        internal static readonly Material glow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        internal static readonly Mesh disc = SixPathsBurstGraphics.Band(0f, "Thunder God disc");
        private static readonly Mesh ring = SixPathsBurstGraphics.Band(0.965f, "Thunder God ring");

        internal static readonly Color Gold = new Color(1f, 0.78f, 0.18f);
        internal static readonly Color Pale = new Color(1f, 0.95f, 0.68f);
        internal static readonly Color Ink = new Color(0.13f, 0.09f, 0.02f);

        /// <summary>The sketches' Y and Floor.</summary>
        internal static readonly float Overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
        internal static readonly float Floor = AltitudeLayer.Filth.AltitudeFor();

        // Star glint: angle, half length and width of each ray, as a share of the flash radius.
        private static readonly float[] RayAngle = { 0f, 90f, 45f, 135f }, RayReach = { 1.25f, 1f, 0.5f, 0.5f },
            RayWidth = { 0.1f, 0.1f, 0.06f, 0.06f };
        private static readonly float[] CrossTurns = { 0f, 90f, 180f, 270f };

        // One mesh per thing drawn in a frame: Graphics.DrawMesh reads a mesh when the frame
        // renders. Strips are handed out from a pool that starts again each frame, so two effects
        // in one frame never share one. The Spirit Bomb dome's flame ring takes 361 points.
        internal const int MostPoints = 361;
        private static readonly List<SixPathsStrip>[] pool = new List<SixPathsStrip>[MostPoints + 1];
        private static readonly int[] taken = new int[MostPoints + 1];
        private static readonly Vector2[][] sideA = new Vector2[MostPoints + 1][], sideB = new Vector2[MostPoints + 1][];
        private static int frame = -1;
        /// <summary>Strip meshes are written relative to this ground point and drawn at it.</summary>
        private static Vector2 anchor;

        /// <summary>Call once at the start of an effect's Draw with the effect's ground point.</summary>
        internal static void Begin(Vector2 ground) => anchor = ground;

        internal static bool Shown(Vector2 at, Map map)
        {
            IntVec3 cell = new Vector3(at.x, 0f, at.y).ToIntVec3();
            return cell.InBounds(map) && !cell.Fogged(map);
        }

        internal static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);
        internal static float Rand(int index) => SixPathsBloomTiming.Rand(index);
        internal static Color Fade(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);
        internal static Vector2 Turn(float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        /// <summary>
        /// <paramref name="angle"/> is clockwise seen from above, as Unity turns about the vertical,
        /// so a shape that follows a direction of d degrees (0 east, 90 north) is given -d.
        /// </summary>
        internal static void DrawMesh(Mesh mesh, Vector2 at, float altitude, float width, float depth, float angle,
            Color colour, Material material)
        {
            if (colour.a <= 0.001f) return;
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(at.x, altitude, at.y), Quaternion.AngleAxis(angle, Vector3.up),
                new Vector3(width, 1f, depth)), material, 0, null, 0, properties);
        }

        internal static void Sprite(Vector2 at, float width, float depth, Color colour, Material material, float altitude, float angle = 0f) =>
            DrawMesh(MeshPool.plane10, at, altitude, width, depth, angle, colour, material);

        internal static void Circle(Vector2 at, float radius, float alpha, float altitude, Color colour)
        {
            if (radius <= 0f) return;
            DrawMesh(ring, at, altitude, radius, radius, 0f, Fade(colour, alpha), solid);
        }

        /// <summary>Two lines of <paramref name="points"/> points to fill in, then hand to <see cref="Strip"/>.</summary>
        internal static void Sides(int points, out Vector2[] a, out Vector2[] b)
        {
            a = sideA[points] ?? (sideA[points] = new Vector2[points]);
            b = sideB[points] ?? (sideB[points] = new Vector2[points]);
        }

        /// <summary>A ribbon between the two lines from <see cref="Sides"/>, in map coordinates.</summary>
        internal static void Strip(Vector2[] a, Vector2[] b, Color colour, Material material, float altitude)
        {
            if (colour.a <= 0.001f) return;
            for (int i = 0; i < a.Length; i++) { a[i] -= anchor; b[i] -= anchor; }
            SixPathsStrip strip = Next(a.Length);
            strip.Between(a, b);
            DrawMesh(strip.mesh, anchor, altitude, 1f, 1f, 0f, colour, material);
        }

        private static SixPathsStrip Next(int points)
        {
            if (Time.frameCount != frame)
            {
                frame = Time.frameCount;
                for (int i = 0; i < taken.Length; i++) taken[i] = 0;
            }
            List<SixPathsStrip> made = pool[points] ?? (pool[points] = new List<SixPathsStrip>());
            if (taken[points] == made.Count) made.Add(new SixPathsStrip("Thunder God strip " + points + " " + made.Count, points));
            return made[taken[points]++];
        }

        /// <summary>A straight line from a to b, widest in the middle.</summary>
        internal static void Streak(Vector2 a, Vector2 b, float width, Color colour, Material material, float altitude, int steps = 8)
        {
            if (colour.a <= 0.001f) return;
            Vector2 along = b - a;
            float length = along.magnitude;
            if (length < 1e-5f) length = 1f;
            var normal = new Vector2(-along.y / length, along.x / length);
            Sides(steps + 1, out Vector2[] left, out Vector2[] right);
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps, half = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * width / 2f;
                left[i] = a + along * u + normal * half;
                right[i] = a + along * u - normal * half;
            }
            Strip(left, right, colour, material, altitude);
        }

        /// <summary>
        /// The teleport flash: a bright core, a thin ring that opens round it, 2 long rays and 2 short
        /// diagonal rays through the centre. A light at chest height, not a mark on the floor.
        /// </summary>
        internal static void Star(Vector2 at, float age, float life, float size)
        {
            if (age < 0f || age >= life) return;
            float u = age / life, open = Smooth(u / 0.25f), f = (1f - u) * (1f - u), turn = ThunderGodTiming.StarTurn * u;
            Sprite(at, size * 1.5f, size * 1.5f, Fade(Gold, 0.7f * f), glow, Overhead + 0.04f);
            Sprite(at, size * 0.55f, size * 0.55f, Fade(Pale, f), glow, Overhead + 0.05f);
            Circle(at, size * (0.22f + 0.5f * Smooth(u)), 0.9f * (1f - u), Overhead + 0.05f, Pale);
            Circle(at, size * (0.2f + 0.46f * Smooth(u)), 0.5f * (1f - u), Overhead + 0.0505f, Gold);
            for (int i = 0; i < RayAngle.Length; i++)
            {
                Vector2 reach = Turn(RayAngle[i] + turn) * (size * RayReach[i] * open * (1f - 0.35f * u));
                float width = size * RayWidth[i] * (1f - 0.6f * u);
                Streak(at - reach, at + reach, width * 2.6f, Fade(Gold, 0.6f * f), whiteGlow, Overhead + 0.055f + i * 0.0002f);
                Streak(at - reach, at + reach, width, Fade(Pale, Mathf.Min(1f, f * 1.4f)), solid, Overhead + 0.06f + i * 0.0002f);
            }
        }

        internal static void Sparks(Vector2 centre, float age, int count)
        {
            if (age < 0f || age > ThunderGodTiming.SparkLife) return;
            for (int i = 0; i < count; i++)
            {
                float u = age / (ThunderGodTiming.SparkLife * (0.6f + 0.4f * Rand(i + 9)));
                if (u > 1f) continue;
                float angle = i * 2.399f + Rand(i) * 0.8f, reach = 0.15f + u * (0.5f + Rand(i + 3) * 0.7f);
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                var tip = new Vector2(centre.x + c * reach, centre.y + 0.3f + s * reach * 0.8f);
                Streak(new Vector2(tip.x - c * 0.22f, tip.y - s * 0.18f), tip, 0.05f, Fade(Pale, 1f - u), whiteGlow,
                    Overhead + 0.06f + i * 0.0002f, 2);
            }
        }

        /// <summary>
        /// A pale sliver where the caster narrows out of sight or widens back into it, thin 0 to 1.
        /// The sketches narrow their stand-in pawn; a real pawn cannot be narrowed, so this is what
        /// is drawn over it.
        /// </summary>
        internal static void Sliver(Vector2 feet, float thin)
        {
            if (thin <= 0f) return;
            float width = 1f - thin * 0.92f, height = 1f + thin * 0.8f;
            DrawMesh(disc, new Vector2(feet.x, feet.y + 0.18f * height), Overhead, 0.22f * width, 0.32f * height, 0f, Fade(Pale, thin), solid);
            DrawMesh(disc, new Vector2(feet.x, feet.y + 0.58f * height), Overhead + 0.002f, 0.16f * width, 0.17f * height, 0f, Fade(Pale, thin), solid);
        }

        /// <summary>
        /// What stays where the caster left from. <paramref name="gone"/> is seconds since they
        /// began to narrow. The chain leaves a scorch only at the start.
        /// </summary>
        internal static void Leave(Vector2 from, float gone, float squeeze, float starSize, bool scorch = true)
        {
            if (gone < 0f) return;
            if (scorch) Sprite(from, 0.95f, 0.55f, Fade(Ink, 0.42f * (1f - Smooth(gone / ThunderGodTiming.Scorch))), soft, Floor + 0.01f);
            Circle(from, 0.3f + gone * 0.5f, 0.7f * (1f - Mathf.Clamp01(gone / 0.5f)), Floor + 0.02f, Gold);
            if (gone >= squeeze && gone < squeeze + ThunderGodTiming.Afterimage)
            {
                float fade = 1f - (gone - squeeze) / ThunderGodTiming.Afterimage;
                DrawMesh(disc, new Vector2(from.x, from.y + 0.18f), Overhead, 0.22f, 0.32f, 0f, Fade(Pale, 0.5f * fade), solid);
                DrawMesh(disc, new Vector2(from.x, from.y + 0.58f), Overhead + 0.002f, 0.16f, 0.17f, 0f, Fade(Pale, 0.5f * fade), solid);
                Sprite(new Vector2(from.x, from.y + 0.35f), 1.1f, 1.4f, Fade(Gold, 0.45f * fade * fade), glow, Overhead + 0.004f);
            }
            Star(new Vector2(from.x, from.y + 0.3f), gone, ThunderGodTiming.LeaveStar, starSize * ThunderGodTiming.LeaveStarScale);
            Sparks(from, gone, 8);
        }

        /// <summary>The line between two places. <paramref name="u"/> is 0 to 1 through its life.</summary>
        internal static void JumpLine(Vector2 from, Vector2 to, float u, float width)
        {
            if (u < 0f || u >= 1f) return;
            float alpha = 1f - Smooth((u - 0.6f) / 0.4f);
            Vector2 a = new Vector2(from.x, from.y + 0.3f), b = new Vector2(to.x, to.y + 0.3f);
            Streak(a, b, width * 3.2f, Fade(Gold, 0.5f * alpha), whiteGlow, Overhead + 0.02f, 12);
            Streak(a, b, width, Fade(Pale, alpha), solid, Overhead + 0.03f, 12);
        }

        /// <summary>
        /// One glyph at <paramref name="centre"/>, in a strip running in direction <paramref name="degrees"/>:
        /// a stroke across the strip, a short one along it, and sometimes a second short one across.
        /// <paramref name="fresh"/> 0 to 1 adds the glow of a glyph just written or just touched.
        /// </summary>
        private static void Glyph(Vector2 centre, float degrees, int n, Color colour, float fresh)
        {
            Vector2 u = Turn(degrees), v = new Vector2(-u.y, u.x);
            float length = 0.15f + Rand(n) * 0.06f, side = Rand(n + 40) > 0.5f ? 1f : -1f, off = (Rand(n + 80) - 0.5f) * length * 0.8f;
            float stroke = ThunderGodTiming.Stroke, altitude = Floor + 0.03f;
            DrawMesh(MeshPool.plane10, centre, altitude, stroke, length, -degrees, colour, solid);
            DrawMesh(MeshPool.plane10, centre + u * (side * 0.04f) + v * off, altitude, 0.075f, stroke, -degrees, colour, solid);
            if (Rand(n + 120) > 0.45f)
                DrawMesh(MeshPool.plane10, centre - u * (side * 0.045f) - v * (off * 0.6f), altitude, stroke, length * 0.45f, -degrees, colour, solid);
            if (fresh > 0f) Sprite(centre, 0.35f, 0.35f, Fade(Pale, 0.7f * fresh), glow, Floor + 0.035f);
        }

        /// <summary>
        /// One arm of floor script from <paramref name="origin"/> in direction <paramref name="degrees"/>:
        /// glyphs written one after another over <paramref name="writeTime"/> from <paramref name="writeStart"/>,
        /// and burnt away in the same order as <paramref name="burn"/> goes 0 to 1.
        /// </summary>
        internal static void Script(Vector2 origin, float degrees, float start, int count, float seconds,
            float writeStart, float writeTime, float burn, int seed = 0)
        {
            Vector2 u = Turn(degrees);
            for (int j = 0; j < count; j++)
            {
                float f = j / (float)(count - 1), since = seconds - (writeStart + writeTime * f * 0.9f);
                if (since < 0f) continue;
                float fresh = 1f - Mathf.Clamp01(since / ThunderGodTiming.GlyphFlash), left = 1f - Mathf.Clamp01((burn - f * 0.85f) / 0.15f);
                if (left <= 0f) continue;
                Glyph(origin + u * (start + j * ThunderGodTiming.GlyphPitch), degrees, seed * 16 + j,
                    Fade(Color.Lerp(Gold, Pale, Mathf.Max(fresh, 1f - left)), left), fresh);
            }
        }

        /// <summary>
        /// Script written round a circle, turned by <paramref name="turn"/> degrees. The glyphs within
        /// FlashArc degrees of a flash go white for FlashTime seconds, which is how a barrier shows
        /// where it was touched.
        /// </summary>
        internal static void ScriptRing(Vector2 origin, float radius, int count, float seconds, float writeStart, float writeTime,
            float burn, float turn, float[] flashDegrees, float[] flashAges, int flashes)
        {
            for (int j = 0; j < count; j++)
            {
                float f = j / (float)count, since = seconds - (writeStart + writeTime * f * 0.9f);
                if (since < 0f) continue;
                float fresh = 1f - Mathf.Clamp01(since / ThunderGodTiming.GlyphFlash), left = 1f - Mathf.Clamp01((burn - f * 0.85f) / 0.15f);
                if (left <= 0f) continue;
                float degrees = turn + 360f * f, hot = 0f;
                for (int k = 0; k < flashes; k++)
                {
                    if (flashAges[k] < 0f || flashAges[k] >= ThunderGodTiming.FlashTime) continue;
                    float apart = Mathf.Abs(((degrees - flashDegrees[k]) % 360f + 540f) % 360f - 180f);
                    hot = Mathf.Max(hot, (1f - flashAges[k] / ThunderGodTiming.FlashTime) * Mathf.Clamp01(1f - apart / ThunderGodTiming.FlashArc));
                }
                Glyph(origin + Turn(degrees) * radius, degrees + 90f, j,
                    Fade(Color.Lerp(Gold, Pale, Mathf.Max(fresh, Mathf.Max(1f - left, hot))), left), Mathf.Max(fresh, hot * 0.8f));
            }
        }

        /// <summary>The cross of four short arms used where the landing cell is the marked cell itself.</summary>
        internal static void ScriptCross(Vector2 origin, float degrees, float seconds, float writeStart, float writeTime, float burn, int seed = 0)
        {
            for (int k = 0; k < CrossTurns.Length; k++)
                Script(origin, degrees + CrossTurns[k], ThunderGodTiming.CrossStart, ThunderGodTiming.CrossGlyphs, seconds,
                    writeStart, writeTime, burn, seed + k);
        }

        /// <summary>Four corner brackets round the cell the caster lands on, turned to <paramref name="degrees"/>.</summary>
        internal static void Brackets(Vector2 cell, float degrees, float alpha)
        {
            if (alpha <= 0f) return;
            Vector2 along = Turn(degrees), across = new Vector2(-along.y, along.x);
            float half = ThunderGodTiming.Bracket / 2f, reach = ThunderGodTiming.BracketArm / 2f;
            Color colour = Fade(Gold, alpha);
            for (int su = -1; su <= 1; su += 2)
                for (int sv = -1; sv <= 1; sv += 2)
                {
                    DrawMesh(MeshPool.plane10, cell + along * (su * (half - reach)) + across * (sv * half), Floor + 0.03f,
                        ThunderGodTiming.BracketArm, ThunderGodTiming.Stroke, -degrees, colour, solid);
                    DrawMesh(MeshPool.plane10, cell + along * (su * half) + across * (sv * (half - reach)), Floor + 0.03f,
                        ThunderGodTiming.Stroke, ThunderGodTiming.BracketArm, -degrees, colour, solid);
                }
        }

        /// <summary>The melee cut: an arc swept round the caster, centred on direction <paramref name="mid"/> degrees.</summary>
        internal static void Slash(Vector2 caster, float mid, float age)
        {
            if (age < 0f || age >= ThunderGodTiming.SlashTime + ThunderGodTiming.SlashFade) return;
            float swept = Smooth(age / ThunderGodTiming.SlashTime), alpha = 1f - Mathf.Clamp01((age - ThunderGodTiming.SlashTime) / ThunderGodTiming.SlashFade);
            var pivot = new Vector2(caster.x, caster.y + 0.25f);
            Arc(pivot, mid, swept, 0.34f, ThunderGodTiming.SlashRadius, Fade(Gold, 0.6f * alpha), whiteGlow, Overhead + 0.07f);
            Arc(pivot, mid, swept, 0.13f, ThunderGodTiming.SlashRadius + 0.03f, Fade(Pale, alpha), solid, Overhead + 0.08f);
        }

        private static void Arc(Vector2 pivot, float mid, float swept, float width, float radius, Color colour, Material material, float altitude)
        {
            const int steps = 14;
            Sides(steps + 1, out Vector2[] outer, out Vector2[] inner);
            for (int i = 0; i <= steps; i++)
            {
                float v = i / (float)steps;
                Vector2 direction = Turn(mid - ThunderGodTiming.SlashHalfArc + 2f * ThunderGodTiming.SlashHalfArc * swept * v);
                // No blob before the arc has any length.
                float w = Mathf.Max(0f, Mathf.Sin(v * Mathf.PI)) * width * (0.4f + 0.6f * v) * Mathf.Clamp01(swept * 4f);
                outer[i] = pivot + direction * (radius + w / 2f);
                inner[i] = pivot + direction * (radius - w / 2f);
            }
            Strip(outer, inner, colour, material, altitude);
        }

        /// <summary>The spark on the body that was cut. <paramref name="age"/> is seconds since the hit.</summary>
        internal static void HitSpark(Vector2 victim, float age)
        {
            if (age < 0f || age >= 0.16f) return;
            float u = age / 0.16f;
            Sprite(new Vector2(victim.x, victim.y + 0.3f), 0.5f + u * 0.9f, 0.5f + u * 0.9f, Fade(Pale, 0.9f * (1f - u)), glow, Overhead + 0.09f);
        }

        /// <summary>
        /// The seal lit on a kunai stuck in a pawn that was thrown in direction <paramref name="degrees"/>.
        /// The kunai itself is the game's own drawing (PawnRenderNodeWorker_EmbeddedKunai).
        /// </summary>
        internal static void SealOnPawn(Vector2 victim, float degrees, float lit)
        {
            Vector2 thrown = Turn(degrees);
            Sprite(new Vector2(victim.x - thrown.x * 0.1f, victim.y + 0.2f - thrown.y * 0.1f), 0.55f, 0.55f, Fade(Gold, 0.85f * lit), glow, Overhead + 0.01f);
        }

        /// <summary>The seal lit on a kunai lying on the ground.</summary>
        internal static void SealOnGround(Vector2 at, float lit) =>
            Sprite(at, 0.55f, 0.55f, Fade(Gold, 0.85f * lit), glow, Overhead + 0.01f);
    }
}
