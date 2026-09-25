using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The aim frame of a Bubble Pipe cast: along the cast direction, across it (to the left, counter-
    /// clockwise), and a height drawn north at SixPathsHeight.Lift. The port of lib/water-gun.js's frame().
    /// </summary>
    internal readonly struct BubbleFrame
    {
        public readonly Vector2 Along, Across, SunVector;

        public BubbleFrame(Vector2 toward, Vector2 sun)
        {
            Along = toward;
            Across = new Vector2(-toward.y, toward.x);
            SunVector = sun;
        }

        /// <summary>The drawn point: <paramref name="height"/> is shifted north.</summary>
        public Vector2 Place(Vector2 feet, float along, float across, float height = 0f) =>
            feet + Along * along + Across * across + new Vector2(0f, height * SixPathsHeight.Lift);

        /// <summary>Where the same point's shadow falls on the floor.</summary>
        public Vector2 Cast(Vector2 feet, float along, float across, float height = 0f) =>
            feet + Along * along + Across * across + SunVector * height;
    }

    /// <summary>
    /// The drawing pieces shared by the Bubble Pipe's Drifting Burst and Eye Pop: the bamboo pipe, the
    /// soap jar at the hip with its visible level, a soap bubble at any height, a bubble forming on the
    /// tip, and a bubble popping. The port of Tools/VfxLab/web/sketches/lib/bubble-pipe.js; its numbers
    /// are that file's.
    ///
    /// A bubble is a sphere, so it is a level circle shifted north by its height with a shadow at its
    /// base, the same from every facing. The pipe lies along the aim and the jar is a level cylinder,
    /// so nothing here has a per-facing method. Strips, sprites and rings come from ThunderGodGraphics.
    /// Every routine takes ages and amounts and keeps no state. The sketch's stand-in hands on the pipe
    /// are not drawn.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class BubblePipeGraphics
    {
        internal static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        internal static readonly Color Film = new Color(0.90f, 0.96f, 1f), FilmFill = new Color(0.85f, 0.93f, 1f), Shade = new Color(0.35f, 0.45f, 0.70f);
        internal static readonly Color Iris1 = new Color(1f, 0.70f, 0.85f), Iris2 = new Color(0.65f, 0.95f, 0.85f);
        internal static readonly Color Bamboo = new Color(0.74f, 0.64f, 0.36f), BambooDark = new Color(0.42f, 0.34f, 0.16f), BambooLit = new Color(0.88f, 0.80f, 0.52f);
        internal static readonly Color Jar = new Color(0.20f, 0.36f, 0.40f), JarLit = new Color(0.34f, 0.54f, 0.58f), JarDark = new Color(0.08f, 0.16f, 0.18f), Cork = new Color(0.60f, 0.46f, 0.28f);
        internal static readonly Color Soap = new Color(0.70f, 0.88f, 0.95f, 0.85f);
        internal static readonly Color Foam = new Color(0.92f, 0.97f, 1f), WaterLit = new Color(0.62f, 0.86f, 1f);

        /// <summary>The pipe's height at the mouth, its length and half width, in cells.</summary>
        internal const float MouthH = 0.62f, PipeLen = 0.85f, PipeW = 0.055f;
        /// <summary>The jar the sketch draws: 10 blows, its radius, height, bottom, and place from the feet in the aim frame.</summary>
        internal const int JarCap = 10;
        internal const float JarR = 0.11f, JarH = 0.30f, JarBase = 0.30f, JarAlong = -0.02f, JarAcross = -0.30f;
        /// <summary>Rest, raising the pipe, lowering it, rest again.</summary>
        internal const float Lead = 0.2f, Raise = 0.25f, Lower = 0.3f, Tail = 0.2f;
        /// <summary>The raised pipe's tip in the aim frame: along, across, height.</summary>
        internal const float TipAlong = 0.08f + PipeLen, TipAcross = 0f, TipH = MouthH + 0.06f;

        internal static readonly float Shadows = AltitudeLayer.Shadows.AltitudeFor();
        internal static readonly float PawnAltitude = AltitudeLayer.Pawn.AltitudeFor();

        private static readonly Mesh ring = VfxDraw.Ring(0.93f, "Bubble Pipe ring");
        private static readonly Mesh thin = VfxDraw.Ring(0.965f, "Bubble Pipe thin ring");

        internal static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;

        private static void Disc(Mesh mesh, Vector2 at, float altitude, float sx, float sz, Color colour) =>
            DrawMesh(mesh, at, altitude, sx, sz, 0f, colour, solid);

        /// <summary>A thick arc from <paramref name="a0"/> to <paramref name="a1"/> degrees (0 east, counter-clockwise) on an ellipse, widest in its middle.</summary>
        internal static void Arc(Vector2 c, float rx, float rz, float a0, float a1, float w, Color colour, float altitude)
        {
            if (colour.a <= 0.001f) return;
            const int n = 10;
            Sides(n + 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                float t = (a0 + (a1 - a0) * i / n) * Mathf.Deg2Rad, ww = w * Mathf.Max(0f, Mathf.Sin(i / (float)n * Mathf.PI));
                a[i] = new Vector2(c.x + Mathf.Cos(t) * (rx - ww / 2f), c.y + Mathf.Sin(t) * (rz - ww / 2f));
                b[i] = new Vector2(c.x + Mathf.Cos(t) * (rx + ww / 2f), c.y + Mathf.Sin(t) * (rz + ww / 2f));
            }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A straight tube from a to b, from <paramref name="lo"/> to <paramref name="hi"/> times <paramref name="w"/> to its left.</summary>
        private static void Tube(Vector2 p, Vector2 q, float w, Color colour, float altitude, float lo = -1f, float hi = 1f)
        {
            Vector2 d = q - p;
            float length = d.magnitude;
            if (length < 1e-6f) length = 1f;
            var n = new Vector2(-d.y / length, d.x / length);
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = p + n * (w * lo); a[1] = q + n * (w * lo);
            b[0] = p + n * (w * hi); b[1] = q + n * (w * hi);
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A rectangle <paramref name="length"/> by <paramref name="width"/> centred on c, its long side along <paramref name="degrees"/>.</summary>
        private static void Rect(Vector2 c, float length, float width, float degrees, Color colour, float altitude)
        {
            float r = degrees * Mathf.Deg2Rad, cx = Mathf.Cos(r), sx = Mathf.Sin(r);
            var ax = new Vector2(cx * length / 2f, sx * length / 2f);
            var bx = new Vector2(-sx * width / 2f, cx * width / 2f);
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = c - ax - bx; a[1] = c + ax - bx;
            b[0] = c - ax + bx; b[1] = c + ax + bx;
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>
        /// One bubble at ground point <paramref name="g"/>, <paramref name="height"/> up, radius <paramref name="r"/>.
        /// <paramref name="wobble"/> stretches it along x or z, turned by <paramref name="phase"/>.
        /// </summary>
        internal static void Bubble(Vector2 g, float height, float r, float seconds, Vector2 sun, float shadow, float wobble = 0.06f,
            float phase = 0f, float alpha = 1f, float altitude = float.NaN)
        {
            if (r <= 0f || alpha <= 0f) return;
            float layer = float.IsNaN(altitude) ? Overhead + 0.05f : altitude;
            var c = new Vector2(g.x, g.y + height * SixPathsHeight.Lift);
            float wave = wobble * Mathf.Sin(seconds * 7f + phase), sx = r * (1f + wave), sz = r * (1f - wave);
            Sprite(g + sun * (height * 0.5f), r * 2.2f, r * 1.4f, Fade(Body, shadow * 0.25f * alpha), soft, Shadows);
            Disc(disc, c, layer, sx, sz, Fade(FilmFill, 0.10f * alpha));
            Disc(ring, c, layer + 0.002f, sx * 0.97f, sz * 0.97f, Fade(Iris1, 0.35f * alpha));
            Disc(ring, c, layer + 0.003f, sx * 0.91f, sz * 0.91f, Fade(Iris2, 0.28f * alpha));
            Disc(thin, c, layer + 0.004f, sx, sz, Fade(Film, 0.85f * alpha));
            // Highlight crescent upper left, shade crescent lower right: short thick arcs.
            Arc(c, sx * 0.72f, sz * 0.72f, 115f, 160f, r * 0.14f, Fade(Foam, 0.85f * alpha), layer + 0.006f);
            Arc(c, sx * 0.86f, sz * 0.86f, 290f, 350f, r * 0.06f, Fade(Shade, 0.35f * alpha), layer + 0.005f);
        }

        /// <summary>
        /// A bubble popping at ground point <paramref name="g"/>, <paramref name="height"/> up, radius
        /// <paramref name="r"/>, <paramref name="age"/> seconds after the pop. The rim tears at one point
        /// and the film retracts round the circle over 0.12 s, thickening; 12 iridescent specks fly off
        /// and fade within 0.32 s, drifting down a little; a thin ring opens and thins for 0.1 s. Nothing
        /// is left on the floor and there is no water.
        /// </summary>
        internal static void Pop(Vector2 g, float height, float r, float age, int seed = 0, float altitude = float.NaN)
        {
            if (age < 0f) return;
            float layer = float.IsNaN(altitude) ? Overhead + 0.05f : altitude;
            var c = new Vector2(g.x, g.y + height * SixPathsHeight.Lift);
            float tear = Rand(seed + 1300) * 360f;
            if (age < 0.12f)
            {
                float u = age / 0.12f, gone = u * 180f;
                if (gone < 178f)
                {
                    float w = r * (0.03f + 0.08f * u);
                    Arc(c, r, r, tear + gone, tear + 180f, w, Fade(Film, 0.9f), layer + 0.004f);
                    Arc(c, r, r, tear + 180f, tear + 360f - gone, w, Fade(Film, 0.9f), layer + 0.0041f);
                    Arc(c, r * 0.94f, r * 0.94f, tear + gone, tear + 180f, r * 0.05f, Fade(Iris1, 0.4f * (1f - u)), layer + 0.003f);
                    Arc(c, r * 0.94f, r * 0.94f, tear + 180f, tear + 360f - gone, r * 0.05f, Fade(Iris2, 0.35f * (1f - u)), layer + 0.0031f);
                }
            }
            if (age < 0.1f) Disc(thin, c, layer + 0.002f, r * (1f + age * 4f), r * (1f + age * 4f), Fade(Film, 0.6f * (1f - age / 0.1f)));
            for (int i = 0; i < 12; i++)
            {
                float life = 0.18f + Rand(i + seed + 1310) * 0.14f, u = age / life;
                if (u > 1f) continue;
                float a = (tear + 30f + i / 12f * 300f + Rand(i + seed + 1320) * 20f) * Mathf.Deg2Rad;
                float far = r * (1f + u * 1.8f * (0.6f + Rand(i + seed + 1330)));
                var q = new Vector2(c.x + Mathf.Cos(a) * far, c.y + Mathf.Sin(a) * far - u * u * 0.12f);
                Color col = i % 3 == 0 ? Iris1 : i % 3 == 1 ? Iris2 : Foam;
                Disc(disc, q, layer + 0.008f + i * 0.00001f, 0.022f * (1f - u * 0.5f), 0.03f * (1f - u * 0.5f), Fade(col, 0.9f * (1f - u)));
            }
        }

        /// <summary>
        /// The soap jar at the hip: a level cylinder with the soap inside it up to <paramref name="blows"/> of
        /// <paramref name="cap"/>, a lit side, a lid with a rim, a cork and a strap to the belt. It is drawn
        /// under the pawn when the hip is on the far (north) side.
        /// </summary>
        internal static void DrawJar(Vector2 feet, BubbleFrame f, float blows, float cap, float shadow)
        {
            Vector2 j = f.Place(feet, JarAlong, JarAcross, JarBase);
            bool behind = j.y > feet.y + 0.06f;
            float jl = behind ? PawnAltitude - 0.03f : Overhead + 0.001f;
            Sprite(f.Cast(feet, JarAlong, JarAcross, JarBase + JarH * 0.5f), JarR * 2.6f, JarR * 1.5f, Fade(Body, shadow * 0.5f), soft, Shadows);

            float level = Mathf.Clamp01(cap > 0f ? blows / cap : 0f) * JarH;
            Side(j, JarR, 0f, JarH, 14, 0f, JarDark, jl);
            if (level > 0f) Side(j, JarR * 0.9f, 0.01f, level, 14, 0f, Soap, jl + 0.002f);
            Side(j, JarR * 0.55f, 0.02f, JarH * 0.96f, 6, -JarR * 0.3f, Fade(JarLit, 0.5f), jl + 0.003f);
            var top = new Vector2(j.x, j.y + JarH * SixPathsHeight.Lift);
            Disc(disc, top, jl + 0.004f, JarR, JarR, Jar);
            Circle(top, JarR, 0.9f, jl + 0.005f, JarLit);
            Disc(disc, new Vector2(top.x, top.y + 0.01f), jl + 0.006f, JarR * 0.5f, JarR * 0.5f, Cork);
            Tube(f.Place(feet, JarAlong, JarAcross, JarBase + JarH), f.Place(feet, 0.02f, -0.18f, 0.42f), 0.02f, BambooDark, jl + 0.007f);
        }

        /// <summary>The jar alone on a pawn standing at <paramref name="feet"/> facing <paramref name="toward"/>, while the pipe is held and not in use.</summary>
        internal static void DrawJarOn(Vector2 feet, Vector2 toward, float blows, float cap, Map map)
        {
            if (!Shown(feet, map)) return;
            Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            DrawJar(feet, new BubbleFrame(toward, sun), blows, cap, shadow);
        }

        /// <summary>The front half of the jar's side, from <paramref name="h0"/> to <paramref name="h1"/> up, shifted <paramref name="dx"/> east.</summary>
        private static void Side(Vector2 j, float r, float h0, float h1, int n, float dx, Color colour, float altitude)
        {
            Sides(n + 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                float th = Mathf.PI + i / (float)n * Mathf.PI, x = j.x + Mathf.Cos(th) * r + dx, z = j.y + Mathf.Sin(th) * r;
                a[i] = new Vector2(x, z + h0 * SixPathsHeight.Lift);
                b[i] = new Vector2(x, z + h1 * SixPathsHeight.Lift);
            }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>
        /// The pipe and the jar for one frame, for a pawn standing at <paramref name="feet"/> aiming along the
        /// frame. <paramref name="raise"/> 0 is at rest, hanging at the side, 1 up at the mouth along the aim.
        /// <paramref name="forming"/> is the radius of a bubble growing on the tip now, 0 for none.
        /// </summary>
        internal static void DrawPipe(Vector2 feet, BubbleFrame f, float raise, float blows, float cap, float forming, float shadow)
        {
            DrawJar(feet, f, blows, cap, shadow);

            float lift = Smooth(raise);
            float a0 = Mathf.Lerp(0.05f, 0.08f, lift), a1 = Mathf.Lerp(0.24f, 0.03f, lift), a2 = Mathf.Lerp(0.45f, MouthH, lift);
            float b0 = Mathf.Lerp(0.55f, TipAlong, lift), b1 = Mathf.Lerp(0.36f, TipAcross, lift), b2 = Mathf.Lerp(0.05f, TipH, lift);
            Vector2 pa = f.Place(feet, a0, a1, a2), pb = f.Place(feet, b0, b1, b2);
            Vector2 sa = f.Cast(feet, a0, a1, a2), sb = f.Cast(feet, b0, b1, b2);
            float pl = Overhead + 0.014f;
            Tube(sa, sb, PipeW * 1.2f, Fade(Body, shadow * 0.6f), Shadows);
            Tube(pa, pb, PipeW + 0.02f, BambooDark, pl);
            Tube(pa, pb, PipeW, Bamboo, pl + 0.002f);
            Tube(pa, pb, PipeW, BambooLit, pl + 0.004f, 0.1f, 0.5f);
            float deg = Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg + 90f;
            for (int k = 1; k <= 3; k++)                           // bamboo nodes
                Rect(Vector2.Lerp(pa, pb, k / 4f), PipeW + 0.02f, 0.025f, deg, BambooDark, pl + 0.006f + k * 0.0001f);
            Disc(disc, pb, pl + 0.007f, PipeW * 0.55f, PipeW * 0.55f, JarDark);   // the bore

            // A bubble forming on the tip: a film that bulges out along the aim as it grows.
            if (forming > 0f)
            {
                Vector2 g = f.Place(feet, b0 + forming * 0.9f, b1), c = new Vector2(g.x, g.y + b2 * SixPathsHeight.Lift);
                Disc(disc, c, pl + 0.010f, forming, forming * 0.9f, Fade(FilmFill, 0.12f));
                Disc(thin, c, pl + 0.012f, forming, forming * 0.9f, Fade(Film, 0.8f));
                Disc(ring, c, pl + 0.011f, forming * 0.95f, forming * 0.85f, Fade(Iris1, 0.3f));
            }
        }

        /// <summary>How far the pipe is up at <paramref name="seconds"/>, when it is lowered from <paramref name="lowerAt"/>.</summary>
        internal static float Raised(float seconds, float lowerAt)
        {
            if (seconds < Lead) return 0f;
            if (seconds < lowerAt) return Smooth((seconds - Lead) / Raise);
            return 1f - Smooth((seconds - lowerAt) / Lower);
        }
    }
}
