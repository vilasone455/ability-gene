using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The aim frame of a Chain Sickle picture: cells along the cast direction and across it.
    /// </summary>
    public readonly struct ChainSickleFrame
    {
        public readonly float ca, sa, radians;
        public readonly Vector2 sun;

        public ChainSickleFrame(float aimDegrees, Vector2 sun)
        {
            radians = aimDegrees * Mathf.Deg2Rad;
            ca = Mathf.Cos(radians);
            sa = Mathf.Sin(radians);
            this.sun = sun;
        }

        public Vector2 Ground(Vector2 at, float along, float across) =>
            new Vector2(at.x + along * ca - across * sa, at.y + along * sa + across * ca);
    }

    /// <summary>
    /// The drawing pieces shared by the Chain Sickle's Snag and Stake: the sickle, the chain with its
    /// links, the weight, the coil that wraps a pawn, the floor stake's crack, drag scuffs, kicked-up
    /// dust and the range ring. The port of Tools/VfxLab/web/sketches/lib/chain-sickle.js; its numbers
    /// are that file's. The stand-in pawns, animals and rifle of the sketches are not drawn here.
    ///
    /// A point with height is a Vector3 (x east, y = cells up, z north). It is drawn height x Lift
    /// cells north (<see cref="Screen"/>) and its shadow falls along the sun (<see cref="Shadow"/>).
    /// Everything lies at one height or is a level circle, so nothing has a per-facing method.
    /// Strips, sprites, discs and rings come from VfxDraw; call its Begin first.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ChainSickleGraphics
    {
        internal static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        internal static readonly Color Skin = new Color(0.83f, 0.70f, 0.54f), Cream = new Color(1f, 0.96f, 0.85f), Dust = new Color(0.80f, 0.74f, 0.63f);
        internal static readonly Color Iron = new Color(0.30f, 0.31f, 0.34f), IronLit = new Color(0.52f, 0.54f, 0.58f), IronDark = new Color(0.09f, 0.09f, 0.11f);
        internal static readonly Color Steel = new Color(0.66f, 0.69f, 0.74f), SteelLit = new Color(0.90f, 0.92f, 0.95f),
            Wood = new Color(0.36f, 0.22f, 0.11f), WoodDark = new Color(0.16f, 0.09f, 0.04f);
        internal static readonly Color Blood = new Color(0.50f, 0.07f, 0.06f), Flash = new Color(1f, 0.93f, 0.70f);

        /// <summary>The hand that holds the chain, cells up.</summary>
        internal const float HandH = 0.5f;
        /// <summary>Where the weight hits a standing pawn, cells up.</summary>
        internal const float ChestH = 0.45f;
        /// <summary>The picture's chain length and Snag range, cells. In game the range is an XML field.</summary>
        internal const float Range = 7f;
        /// <summary>One chain link on screen.</summary>
        internal const float LinkLen = 0.11f, LinkW = 0.055f;
        /// <summary>The coil around a standing pawn.</summary>
        internal const float CoilR = 0.26f, CoilTurns = 2f;
        internal const float Lead = 0.2f, Tail = 0.4f;
        internal const float Lift = SixPathsHeight.Lift;

        internal static readonly float Y = AltitudeLayer.MoteOverhead.AltitudeFor();
        internal static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();

        /// <summary>Most links one chain draws; a longer chain draws its first this many.</summary>
        private const int MostLinks = 240;
        /// <summary>Most points of a path handed to Chain or Coil.</summary>
        internal const int MostPath = 64;
        private static readonly Vector2[] Scr = new Vector2[MostPath], Sh = new Vector2[MostPath], Links = new Vector2[MostLinks];
        private static readonly Vector3[] Run = new Vector3[MostPath];
        private static readonly Vector2[] T = new Vector2[16];

        internal static float Clamp01(float x) => Mathf.Clamp01(x);
        internal static float SmoothStep(float t) => VfxMath.Smooth(t);
        internal static float EaseOut(float x) { float u = 1f - Mathf.Clamp01(x); return 1f - u * u * u; }
        internal static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;
        /// <summary>JavaScript's Math.round: halves go up.</summary>
        internal static int Round(float x) => Mathf.FloorToInt(x + 0.5f);

        internal static Vector2 Screen(Vector3 q) => new Vector2(q.x, q.z + q.y * Lift);
        internal static Vector2 Shadow(Vector3 q, Vector2 sun) => new Vector2(q.x + sun.x * q.y, q.z + sun.y * q.y);
        internal static Vector3 At(Vector2 ground, float h) => new Vector3(ground.x, h, ground.y);

        internal static void Disc(Vector2 at, float altitude, float width, float depth, Color colour) =>
            DrawMesh(disc, at, altitude, width, depth, 0f, colour, solid);

        /// <summary>A quad <paramref name="length"/> long along <paramref name="degrees"/> (0 east, 90 north) and <paramref name="width"/> across.</summary>
        internal static void Rect(Vector2 at, float length, float width, float degrees, Color colour, float altitude) =>
            Sprite(at, length, width, colour, solid, altitude, -degrees);

        /// <summary>
        /// A ribbon through the first <paramref name="count"/> points of <paramref name="pts"/>, its
        /// half-width going from <paramref name="w0"/> at the first point to <paramref name="w1"/> at
        /// the last, measured across the line's own direction on screen.
        /// </summary>
        internal static void Tube(Vector2[] pts, int count, float w0, float w1, Color colour, float altitude)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            int n = count - 1;
            Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                Vector2 pr = pts[Mathf.Max(0, i - 1)], nx = pts[Mathf.Min(n, i + 1)];
                float dx = nx.x - pr.x, dz = nx.y - pr.y, L = Mathf.Sqrt(dx * dx + dz * dz);
                if (L <= 0f) L = 1f;
                dx /= L; dz /= L;
                float w = Mathf.Lerp(w0, w1, i / (float)n);
                Vector2 q = pts[i];
                a[i] = new Vector2(q.x + dz * w, q.y - dx * w);
                b[i] = new Vector2(q.x - dz * w, q.y + dx * w);
            }
            Strip(a, b, colour, solid, altitude);
        }

        // ------------------------------------------------------------------ paths

        /// <summary>
        /// A hanging chain between two points with height: straight in plan, drooping by
        /// <paramref name="sag"/> cells at the middle. <paramref name="wobble"/> adds a small sideways
        /// wave (a chain under load hums). Writes n + 1 points into <paramref name="into"/>.
        /// </summary>
        internal static int ChainPath(Vector3[] into, Vector3 a, Vector3 b, float sag, int n = 24, float wobble = 0f, float s = 0f)
        {
            float dx = b.x - a.x, dz = b.z - a.z, L = Mathf.Sqrt(dx * dx + dz * dz);
            if (L <= 0f) L = 1f;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, droop = sag * 4f * u * (1f - u);
                float w = wobble * Mathf.Sin(u * Mathf.PI * 3f + s * 55f) * Mathf.Sin(u * Mathf.PI);
                into[i] = new Vector3(Mathf.Lerp(a.x, b.x, u) - dz / L * w, Mathf.Max(0.02f, Mathf.Lerp(a.y, b.y, u) - droop),
                    Mathf.Lerp(a.z, b.z, u) + dx / L * w);
            }
            return n + 1;
        }

        /// <summary>
        /// A level spiral round a pawn: <paramref name="turns"/> round <paramref name="centre"/> at
        /// radius <paramref name="r"/>, height <paramref name="h0"/> to <paramref name="h1"/>. The first
        /// point sits at <paramref name="startDeg"/>, the side the chain arrives from.
        /// </summary>
        internal static int CoilPath(Vector3[] into, Vector2 centre, float turns, float r, float h0, float h1, float startDeg, int n = 36)
        {
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, a = startDeg * Mathf.Deg2Rad + u * turns * Mathf.PI * 2f;
                into[i] = new Vector3(centre.x + Mathf.Cos(a) * r, Mathf.Lerp(h0, h1, u), centre.y + Mathf.Sin(a) * r);
            }
            return n + 1;
        }

        /// <summary>Resamples screen points to points <paramref name="step"/> apart along their length.</summary>
        private static int Resample(Vector2[] pts, int count, float step, Vector2[] into)
        {
            int k = 0;
            into[k++] = pts[0];
            float carry = 0f;
            for (int i = 1; i < count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float L = (b - a).magnitude, d = step - carry;
                while (d <= L && L > 0f)
                {
                    if (k >= into.Length) return k;
                    into[k++] = Vector2.Lerp(a, b, d / L);
                    d += step;
                }
                carry = L - (d - step);
            }
            return k;
        }

        // ------------------------------------------------------------------ chain, coil, weight

        /// <summary>
        /// The chain: a soft shadow on the floor, a dark core, then alternating flat (lit, wide) and
        /// edge-on (dark, narrow) links along it. <paramref name="alpha"/> fades all of it.
        /// </summary>
        internal static void Chain(Vector3[] path, int count, Vector2 sun, float strength, float layer, float alpha = 1f)
        {
            if (count < 2) return;
            count = Mathf.Min(count, MostPath);
            for (int i = 0; i < count; i++)
            {
                Scr[i] = Screen(path[i]);
                Sh[i] = Shadow(path[i], sun);
            }
            Tube(Sh, count, 0.045f, 0.045f, Fade(Body, strength * 0.55f * alpha), ShadowLayer);
            Tube(Scr, count, 0.022f, 0.022f, Fade(IronDark, alpha), layer);
            int links = Resample(Scr, count, LinkLen, Links);
            for (int i = 0; i + 1 < links; i++)
            {
                Vector2 a = Links[i], b = Links[i + 1], mid = (a + b) / 2f;
                float deg = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                bool flat = i % 2 == 0;
                Rect(mid, LinkLen * 0.92f, flat ? LinkW : LinkW * 0.55f, deg, Fade(flat ? IronDark : Iron, alpha), layer + 0.002f);
                if (flat) Rect(mid, LinkLen * 0.70f, LinkW * 0.45f, deg, Fade(IronLit, alpha), layer + 0.004f);
            }
        }

        /// <summary>
        /// A coil round a pawn: the runs north of <paramref name="splitZ"/> draw under the pawn layer,
        /// the runs south of it over it, so the pawn stands between the two halves and reads as wrapped.
        /// </summary>
        internal static void Coil(Vector3[] pts, int count, float splitZ, Vector2 sun, float strength)
        {
            int run = 0;
            bool side = false, started = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 q = pts[i];
                bool back = q.z >= splitZ;
                if (started && back != side)
                {
                    if (run < MostPath) Run[run++] = q;
                    if (run > 1) Chain(Run, run, sun, strength, side ? PawnLayer - 0.02f : Y + 0.02f);
                    run = 0;
                    Run[run++] = q;
                }
                else if (run < MostPath) Run[run++] = q;
                side = back;
                started = true;
            }
            if (run > 1) Chain(Run, run, sun, strength, side ? PawnLayer - 0.02f : Y + 0.02f);
        }

        /// <summary>
        /// The weight: an iron ball with a lit top and a dark underside. <paramref name="staked"/> 1 sits
        /// it in the floor with its spike buried: smaller, and no shadow.
        /// </summary>
        internal static void Weight(Vector3 q, Vector2 sun, float strength, float layer, float staked = 0f, float size = 0.16f)
        {
            Vector2 p = Screen(q);
            float h = q.y, sz = Mathf.Lerp(size, size * 0.8f, staked);
            if (staked < 1f)
            {
                float k = 1f - staked;
                Sprite(Shadow(q, sun), sz * 2.2f * k, sz * 1.4f * k, Fade(Body, strength * k * (1f / (1f + h))), soft, ShadowLayer);
            }
            Disc(p, layer, sz, sz, IronDark);
            Disc(new Vector2(p.x, p.y + sz * 0.18f), layer + 0.002f, sz * 0.78f, sz * 0.72f, Iron);
            Disc(new Vector2(p.x - sz * 0.18f, p.y + sz * 0.38f), layer + 0.004f, sz * 0.32f, sz * 0.24f, Fade(IronLit, 0.9f));
        }

        /// <summary>Cracked floor round a stake: six short dark radial lines and a dark patch, which stay.</summary>
        internal static void Crack(Vector2 pos, float amount, int seed = 0)
        {
            if (amount <= 0f) return;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + Rand(i + seed + 400) * 0.6f, len = (0.18f + Rand(i + seed + 410) * 0.2f) * amount;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 p0 = pos + dir * 0.1f, p1 = pos + dir * (0.1f + len);
                T[0] = p0;
                T[1] = (p0 + p1) / 2f + new Vector2(Mathf.Sin(a) * 0.03f, -Mathf.Cos(a) * 0.03f);
                T[2] = p1;
                Tube(T, 3, 0.035f, 0.035f * 0.2f, Fade(Body, 0.6f * amount), Floor + 0.02f + i * 0.0001f);
            }
            Sprite(pos, 0.5f * amount, 0.32f * amount, Fade(Body, 0.35f * amount), soft, Floor + 0.015f);
        }

        /// <summary>
        /// The sickle: a wooden handle along <paramref name="degrees"/> from the hand and a curved steel
        /// blade at its far end hooking 100 degrees to the left. It lies level at height <paramref name="h"/>,
        /// a flat shape that turns freely, with its shadow cast along the sun.
        /// </summary>
        internal static void Sickle(Vector2 hand, float h, float degrees, Vector2 sun, float strength, float layer, float alpha = 1f)
        {
            SicklePart(Shadow(At(hand, h), sun), degrees, ShadowLayer, strength * 0.6f * alpha, true);
            SicklePart(Screen(At(hand, h)), degrees, layer, alpha, false);
        }

        private const float Handle = 0.48f, BladeR = 0.21f, Sweep = 100f;

        private static void SicklePart(Vector2 at, float degrees, float lay, float alpha, bool shadow)
        {
            float r = degrees * Mathf.Deg2Rad, cx = Mathf.Cos(r), sx = Mathf.Sin(r);
            var along = new Vector2(cx, sx);
            Vector2 hc = at + along * (Handle * 0.42f);
            Rect(hc, Handle, 0.07f, degrees, shadow ? Fade(Body, alpha) : Fade(WoodDark, alpha), lay);
            if (!shadow) Rect(hc, Handle * 0.96f, 0.045f, degrees, Fade(Wood, alpha), lay + 0.002f);
            // The blade: an arc centred BladeR to the left of the handle's tip.
            Vector2 tip = at + along * (Handle * 0.92f);
            var c = new Vector2(tip.x - sx * BladeR, tip.y + cx * BladeR);
            const int n = 14;
            Sides(n + 1, out Vector2[] inner, out Vector2[] outer);
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, a = r - Mathf.PI / 2f + u * Sweep * Mathf.Deg2Rad, w = 0.085f * (1f - u * u);
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                inner[i] = c + d * (BladeR - w * 0.1f);
                outer[i] = c + d * (BladeR + w);
            }
            if (shadow)
            {
                Strip(inner, outer, Fade(Body, alpha), solid, lay);
                return;
            }
            // Strip moves the points it is given, so each band is filled again from the arc.
            for (int pass = 0; pass < 3; pass++)
            {
                Sides(n + 1, out Vector2[] a, out Vector2[] b);
                for (int i = 0; i <= n; i++)
                {
                    float u = i / (float)n, ang = r - Mathf.PI / 2f + u * Sweep * Mathf.Deg2Rad, w = 0.085f * (1f - u * u);
                    var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Vector2 inn = c + d * (BladeR - w * 0.1f), outr = c + d * (BladeR + w);
                    if (pass == 0) { a[i] = inn + (inn - outr) * 0.18f; b[i] = outr + (outr - inn) * 0.18f; }
                    else if (pass == 1) { a[i] = inn; b[i] = outr; }
                    else { a[i] = Vector2.Lerp(inn, outr, 0.55f); b[i] = outr; }
                }
                if (pass == 0) Strip(a, b, Fade(IronDark, alpha), solid, lay + 0.001f);
                else if (pass == 1) Strip(a, b, Fade(Steel, alpha), solid, lay + 0.003f);
                else Strip(a, b, Fade(SteelLit, alpha * 0.85f), solid, lay + 0.005f);
            }
        }

        // ------------------------------------------------------------------ floor and dust

        /// <summary>
        /// A drag mark on the floor from <paramref name="from"/> toward <paramref name="to"/>, grown to
        /// <paramref name="amount"/> of the way, that stays: two heel furrows either side of the line and
        /// a faint dusting between them.
        /// </summary>
        internal static void Scuff(Vector2 from, Vector2 to, float amount, float alpha = 0.4f)
        {
            if (amount <= 0f) return;
            Vector2 end = Vector2.Lerp(from, to, amount);
            float dx = to.x - from.x, dz = to.y - from.y, L = Mathf.Sqrt(dx * dx + dz * dz);
            if (L <= 0f) L = 1f;
            float nx = -dz / L, nz = dx / L;
            // The sketch passes the direction's angle to sprite() unchanged, and so does this.
            Sprite((from + end) / 2f, L * amount, 0.3f, Fade(Dust, alpha * 0.35f), soft, Floor + 0.011f, Mathf.Atan2(dz, dx) * Mathf.Rad2Deg);
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                for (int i = 0; i <= 8; i++)
                {
                    float u = i / 8f, wave = Mathf.Sin(u * 9f + side) * 0.02f;
                    Vector2 p = Vector2.Lerp(from, end, u);
                    T[i] = new Vector2(p.x + nx * (side * 0.11f + wave), p.y + nz * (side * 0.11f + wave));
                }
                Tube(T, 9, 0.035f, 0.055f, Fade(Body, alpha), Floor + 0.013f + k * 0.0001f);
            }
        }

        /// <summary>Dust kicked up at a dragged or straining pawn's feet: five puffs on a loop.</summary>
        internal static void Kick(Vector2 pos, float age, float strength, int seed = 0)
        {
            if (age < 0f) return;
            for (int i = 0; i < 5; i++)
            {
                float u = (age * 1.6f + Rand(i + seed + 30)) % 1f;
                float x = pos.x + (Rand(i + seed + 10) - 0.5f) * 0.5f, h = u * 0.35f;
                Sprite(new Vector2(x, pos.y - 0.05f + h * Lift + (Rand(i + seed + 20) - 0.5f) * 0.2f), 0.25f + u * 0.3f, 0.2f + u * 0.25f,
                    Fade(Dust, Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * 0.5f * strength), PowerPoleGraphics.puff, Y + 0.01f + i * 0.0001f);
            }
        }

        /// <summary>A faint floor ring at the rule's radius that fades over <paramref name="life"/> seconds.</summary>
        internal static void RangeRing(Vector2 pos, float radius, float age, float life = 0.8f, float alpha = 0.35f)
        {
            if (age < 0f || age > life) return;
            Circle(pos, radius, alpha * (1f - age / life), Floor + 0.01f, Cream);
        }

        /// <summary>
        /// A snagged pawn as the kit draws it between casts: the chain from the holder's hand to the
        /// first turn of the coil, the coil round the pawn, and the weight riding the coil's end. The
        /// same shapes as Snag's result.
        /// </summary>
        internal static void Snagged(Vector2 holder, Vector2 target, float size, Vector2 sun, float strength)
        {
            Vector2 run = target - holder;
            float aim = run.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;
            var f = new ChainSickleFrame(aim, sun);
            Vector2 hand = f.Ground(holder, 0.20f, 0.18f);
            float big = Mathf.Sqrt(size);
            int coil = CoilPath(LinkCoil, target, CoilTurns, CoilR * big, (ChestH + 0.1f) * big, 0.25f * big, aim + 180f);
            int path = ChainPath(LinkPath, At(hand, HandH), LinkCoil[0], 0.05f);
            Chain(LinkPath, path, sun, strength, Y + 0.02f);
            Coil(LinkCoil, coil, target.y + 0.02f, sun, strength);
            Weight(LinkCoil[coil - 1], sun, strength, Y + 0.03f);
        }

        private static readonly Vector3[] LinkPath = new Vector3[25], LinkCoil = new Vector3[37];
    }
}
