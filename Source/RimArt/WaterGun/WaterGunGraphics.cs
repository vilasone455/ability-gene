using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;

namespace RimArt
{
    /// <summary>
    /// The aim frame of a Water Gun picture: cells along the cast direction, across it, and h cells
    /// up. <see cref="Place"/> is where a point is drawn (height shifted north), <see cref="Cast"/>
    /// where its shadow falls.
    /// </summary>
    public readonly struct WaterGunFrame
    {
        public readonly float ca, sa;
        public readonly Vector2 sun;

        public WaterGunFrame(float aimDegrees, Vector2 sun)
        {
            float a = aimDegrees * Mathf.Deg2Rad;
            ca = Mathf.Cos(a);
            sa = Mathf.Sin(a);
            this.sun = sun;
        }

        public Vector2 Place(Vector2 at, float along, float across, float h = 0f) =>
            new Vector2(at.x + along * ca - across * sa, at.y + along * sa + across * ca + h * SixPathsHeight.Lift);

        public Vector2 Cast(Vector2 at, float along, float across, float h = 0f) =>
            new Vector2(at.x + along * ca - across * sa + sun.x * h, at.y + along * sa + across * ca + sun.y * h);
    }

    /// <summary>How the weapon is posed in one frame: see <see cref="WaterGunGraphics.Weapon"/>.</summary>
    public struct WaterGunPose
    {
        /// <summary>0 at rest, hanging across the body; 1 up at hand height along the aim.</summary>
        public float Raise;
        /// <summary>Water in the bag now, 0 to <see cref="WaterGunGraphics.BagCap"/>.</summary>
        public float Units;
        public float Slosh, Squeeze, Recoil;
    }

    /// <summary>
    /// The drawing pieces shared by the Water Gun's Stream Shot and Hydro Pump: the gun, the water bag
    /// on the back with its fill level, the hose, the jet, its torn wake, the contact splash, puddles,
    /// drips and steam. The port of Tools/VfxLab/web/sketches/lib/water-gun.js; its numbers are that
    /// file's. Water is alpha-blended blue with a dark underside and gapped white reflections, no
    /// additive glow. The bag is a level cylinder and the gun lies along the aim, so nothing here has
    /// a per-facing method. Every routine takes ages and amounts and keeps no state. Strips, sprites,
    /// discs and rings come from ThunderGodGraphics; call its Begin with the effect's ground point first.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class WaterGunGraphics
    {
        internal static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        internal static readonly Color Water = new Color(0.24f, 0.55f, 0.88f), WaterLit = new Color(0.62f, 0.86f, 1f),
            WaterDark = new Color(0.10f, 0.28f, 0.56f), Foam = new Color(0.92f, 0.97f, 1f), Steam = new Color(0.95f, 0.97f, 1f);
        internal static readonly Color BagSkin = new Color(0.40f, 0.62f, 0.80f, 0.45f), BagRim = new Color(0.55f, 0.78f, 0.95f),
            BagDark = new Color(0.16f, 0.30f, 0.46f, 0.55f);
        internal static readonly Color Gun = new Color(0.15f, 0.17f, 0.21f), GunLit = new Color(0.30f, 0.33f, 0.40f),
            GunDark = new Color(0.05f, 0.05f, 0.07f), Accent = new Color(0.95f, 0.60f, 0.16f), Steel = new Color(0.60f, 0.63f, 0.68f);

        /// <summary>Gun height when aimed, cells.</summary>
        internal const float HandH = 0.5f;
        /// <summary>The picture's full bag. The rule's capacity is an XML field; the drawing shows units / BagCap.</summary>
        internal const float BagCap = 30f;
        internal const float BagR = 0.20f, BagH = 0.55f, BagBase = 0.28f;
        /// <summary>Where the bag hangs from the caster's feet, behind the aim.</summary>
        internal const float BagAlong = -0.30f, BagAcross = 0.04f;
        internal const float GripAlong = 0.12f, MuzzleAlong = 0.72f;
        internal const float HoseW = 0.045f;
        /// <summary>Rest, raising the gun, lowering it, rest again, seconds.</summary>
        internal const float Lead = 0.2f, Raise = 0.25f, Lower = 0.3f, Tail = 0.2f;
        internal const float Gravity = 9f;

        internal static readonly float Y = AltitudeLayer.MoteOverhead.AltitudeFor();
        internal static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();

        /// <summary>Most points one strip takes: ThunderGodGraphics' pool size.</summary>
        private const int MostPoints = ThunderGodGraphics.MostPoints;
        // One line of points and its half-widths, filled by a routine and handed to Tube. Only ever
        // used for one tube at a time; Tube copies them into a strip from the pool.
        private static readonly Vector2[] P = new Vector2[MostPoints];
        private static readonly float[] W = new float[MostPoints];
        private static readonly Vector2[] A = new Vector2[MostPoints], C = new Vector2[MostPoints];

        internal static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;
        private static float Sin01(float x) => Mathf.Max(0f, Mathf.Sin(x * Mathf.PI));

        internal static void Disc(Vector2 at, float altitude, float width, float depth, float angle, Color colour) =>
            DrawMesh(disc, at, altitude, width, depth, angle, colour, solid);

        /// <summary>A quad whose long side points along <paramref name="degrees"/> (0 east, 90 north).</summary>
        internal static void Rect(Vector2 at, float length, float width, float degrees, Color colour, float altitude) =>
            Sprite(at, length, width, colour, solid, altitude, -degrees);

        /// <summary>
        /// A ribbon through the first <paramref name="count"/> points of P, W[i] cells to each side
        /// scaled by <paramref name="lo"/> and <paramref name="hi"/> (-1 and 1 is the full width).
        /// Widths are measured across the line's own direction on screen.
        /// </summary>
        private static void Tube(int count, Color colour, float altitude, float lo = -1f, float hi = 1f)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            int n = count - 1;
            Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                Vector2 pr = P[Mathf.Max(0, i - 1)], nx = P[Mathf.Min(n, i + 1)];
                float dx = nx.x - pr.x, dz = nx.y - pr.y, L = Mathf.Sqrt(dx * dx + dz * dz);
                if (L <= 0f) L = 1f;
                dx /= L; dz /= L;
                float w = W[i];
                Vector2 q = P[i];
                a[i] = new Vector2(q.x - dz * w * lo, q.y + dx * w * lo);
                b[i] = new Vector2(q.x - dz * w * hi, q.y + dx * w * hi);
            }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A band between the first <paramref name="count"/> points of A and C.</summary>
        private static void Band(int count, Color colour, float altitude)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < count; i++) { a[i] = A[i]; b[i] = C[i]; }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A puddle that grows over 0.3 s and stays. <paramref name="size"/> in cells.</summary>
        internal static void Puddle(Vector2 at, float age, float size = 1f, float alpha = 0.5f)
        {
            if (age < 0f) return;
            float g = Smooth(age / 0.3f);
            Sprite(at, 1.2f * size * g, 0.8f * size * g, Fade(WaterDark, alpha * 0.5f), soft, Floor + 0.02f);
            Sprite(at, 0.9f * size * g, 0.55f * size * g, Fade(Water, alpha), soft, Floor + 0.03f);
            Sprite(new Vector2(at.x - 0.1f * size, at.y + 0.08f * size), 0.4f * size * g, 0.18f * size * g, Fade(WaterLit, alpha * 0.5f), soft, Floor + 0.04f);
        }

        /// <summary>Drips falling from a soaked pawn: three drops on a loop from body height to the floor.</summary>
        internal static void Drips(Vector2 at, float age, float fade, int seed = 0)
        {
            if (age < 0f || fade <= 0f) return;
            for (int i = 0; i < 3; i++)
            {
                float u = (age * 1.8f + Rand(i + seed + 800)) % 1f, x = at.x + (Rand(i + seed + 810) - 0.5f) * 0.4f, h = 0.45f * (1f - u * u);
                Sprite(new Vector2(x, at.y + 0.1f + h * SixPathsHeight.Lift), 0.07f, 0.11f, Fade(WaterLit, 0.9f * fade * (1f - u * 0.5f)), soft, Y + 0.03f + i * 0.0001f);
            }
        }

        /// <summary>Steam rising where fire was put out: puffs that climb and fade over <paramref name="life"/> seconds.</summary>
        internal static void SteamPuffs(Vector2 at, float age, float life = 0.9f, int seed = 0)
        {
            if (age < 0f || age > life) return;
            for (int i = 0; i < 6; i++)
            {
                float u = Mathf.Clamp01((age - i * 0.06f) / (life * 0.8f));
                if (u <= 0f || u >= 1f) continue;
                float x = at.x + (Rand(i + seed + 700) - 0.5f) * 0.5f, z = at.y + 0.3f + (0.2f + u * 1.1f) * SixPathsHeight.Lift;
                Sprite(new Vector2(x, z), 0.3f + u * 0.5f, 0.25f + u * 0.45f, Fade(Steam, Sin01(u) * 0.6f), PowerPoleGraphics.puff, Y + 0.05f + i * 0.0001f);
            }
        }

        /// <summary>
        /// Contact of a jet: a short sideways fan at the contact height, ballistic drops, then wet
        /// ground and a puddle. <paramref name="contactH"/> is the contact's height in cells; the
        /// default is a standing pawn's chest.
        /// </summary>
        internal static void ShotImpact(Vector2 pos, float age, in WaterGunFrame f, float strength, float contactH = 0.38f / SixPathsHeight.Lift)
        {
            if (age < 0f) return;
            float height = contactH;
            // Runoff reaches the floor after 0.18 s; the central pool grows during the landings.
            Puddle(new Vector2(pos.x, pos.y - 0.08f), age - 0.18f, 1.05f, 0.5f);
            float burst = Smooth(age / 0.025f) * (1f - Smooth((age - 0.065f) / 0.12f));
            if (burst > 0f)
            {
                float reach = 0.2f + 0.8f * Smooth(age / 0.10f);
                for (int i = 0; i < 6; i++)
                {
                    float side = i % 2 == 1 ? -1f : 1f;
                    int branch = i / 2;
                    float across = side * (0.32f + branch * 0.14f + Rand(i + 1150) * 0.16f) * reach;
                    float along = (0.20f - branch * 0.17f + (Rand(i + 1160) - 0.5f) * 0.12f) * reach;
                    for (int j = 0; j <= 12; j++)
                    {
                        float v = j / 12f, arc = Sin01(v);
                        P[j] = f.Place(pos, along * v - 0.10f * arc * reach, across * v, height + 0.08f * arc * reach);
                        W[j] = (0.07f + branch * 0.014f) * Mathf.Pow(arc, 0.65f) * (1f - 0.3f * v);
                    }
                    Tube(13, Fade(Water, burst * 0.8f), Y + 0.04f + i * 0.0001f);
                    Tube(13, Fade(WaterLit, burst * 0.85f), Y + 0.042f + i * 0.0001f, 0.05f, 0.65f);
                }
                Disc(f.Place(pos, 0f, 0f, height), Y + 0.044f, 0.11f, 0.13f, 0f, Fade(WaterLit, burst * 0.7f));
            }
            for (int i = 0; i < 18; i++)
            {
                float life = 0.20f + Rand(i + 1100) * 0.32f;
                float side = i % 2 == 1 ? -1f : 1f;
                float across = side * (0.18f + Rand(i + 1110) * 0.63f);
                float along = -0.34f + Rand(i + 1120) * 0.56f;
                float size = 0.025f + Rand(i + 1130) * 0.034f;
                if (age >= life)
                {
                    // The same drop leaves a small wet patch where it landed.
                    Vector2 land = f.Place(pos, along, across);
                    float spread = Smooth((age - life) / 0.08f);
                    Sprite(land, size * (3f + spread * 2f), size * (2f + spread), Fade(WaterDark, 0.18f * spread), soft, Floor + 0.025f);
                    Sprite(land, size * (2f + spread * 2f), size * (1.5f + spread), Fade(Water, 0.33f * spread), soft, Floor + 0.035f);
                    continue;
                }
                float u = age / life, launch = (Gravity * life * life / 2f - height) / life;
                float h = Mathf.Max(0f, height + launch * age - Gravity * age * age / 2f);
                Vector2 q = f.Place(pos, along * u, across * u, h), sh = f.Cast(pos, along * u, across * u, h);
                float alpha = Smooth(age / 0.025f);
                Sprite(sh, size * 3f, size * 2f, Fade(Body, strength * 0.3f * alpha), soft, ShadowLayer);
                // Stretched along the drawn velocity, with a small reflected cap.
                float vx = (f.ca * along - f.sa * across) / life;
                float vz = (f.sa * along + f.ca * across) / life + (launch - Gravity * age) * SixPathsHeight.Lift;
                float angle = -Mathf.Atan2(vz, vx) * Mathf.Rad2Deg;
                Disc(q, Y + 0.046f, size * (1.3f + 0.5f * u), size, angle, Fade(Water, alpha * 0.9f));
                Disc(new Vector2(q.x - size * 0.15f, q.y + size * 0.25f), Y + 0.048f, size * 0.7f, size * 0.38f, angle, Fade(WaterLit, alpha * 0.95f));
            }
        }

        /// <summary>
        /// A translucent jet from drawn point <paramref name="a"/> to <paramref name="b"/>: its tail is at
        /// share <paramref name="u0"/> of the way and its front at <paramref name="u1"/>. A rounded front,
        /// a darker underside, a lit surface, sliding reflections with gaps and drops peeling off.
        /// <paramref name="tailBreak"/> 0 to 1 tears the rear third into parcels (Stream Shot after the
        /// nozzle closes); Hydro Pump's sustained flow keeps it 0 until its spray ends.
        /// </summary>
        internal static void Stream(Vector2 a, Vector2 b, float u0, float u1, float width, float s, float layer, float sag = 0.05f,
            int seed = 0, float tailBreak = 0f)
        {
            if (u1 <= u0) return;
            float dx = b.x - a.x, dz = b.y - a.y, L = Mathf.Sqrt(dx * dx + dz * dz);
            if (L <= 0f) L = 1f;
            float nx = -dz / L, nz = dx / L, length = L * (u1 - u0);
            float flow = s * 18f + seed * 2.7f;
            float cut = 0.32f * tailBreak, bodyU = Mathf.Lerp(u0, u1, cut);

            int n = Mathf.Min(MostPoints - 1, Mathf.Max(12, Mathf.CeilToInt(length * (1f - cut) / 0.055f)));
            for (int i = 0; i <= n; i++)
            {
                float v = i / (float)n;
                P[i] = At(a, b, L, nx, nz, width, flow, sag, Mathf.Lerp(bodyU, u1, v));
                W[i] = BodyRadius(v, u0, u1, L, length, width, flow, cut);
            }
            Tube(n + 1, Fade(Water, 0.88f), layer);
            Tube(n + 1, Fade(WaterDark, 0.44f), layer + 0.002f, -0.95f, -0.38f);
            Tube(n + 1, Fade(WaterLit, 0.63f), layer + 0.004f, -0.12f, 0.75f);

            // Broken curved reflections slide along the water; the gaps keep it from reading as a laser core.
            int patches = Mathf.CeilToInt(L / 0.5f);
            for (int i = 0; i < patches; i++)
            {
                float u = (s * 2.1f + i / (float)patches + Rand(i + seed + 940) * 0.04f) % 1f;
                float end = Mathf.Min(u1, u + (0.14f + Rand(i + seed + 941) * 0.2f) / L);
                if (u < bodyU || u >= u1 || end - u < 0.012f / L) continue;
                for (int j = 0; j <= 6; j++)
                {
                    float t = j / 6f, qU = Mathf.Lerp(u, end, t);
                    Vector2 q = At(a, b, L, nx, nz, width, flow, sag, qU);
                    float off = BodyRadius((qU - bodyU) / (u1 - bodyU), u0, u1, L, length, width, flow, cut) * (0.38f + 0.18f * Sin01(t));
                    P[j] = new Vector2(q.x + nx * off, q.y + nz * off);
                    W[j] = width * 0.16f * Sin01(t);
                }
                Tube(7, Fade(Foam, 0.88f), layer + 0.006f + i * 0.00005f);
            }

            // Drops peel away from the rear and sides, all behind the true front.
            float angle = -Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            for (int i = 0; i < 14; i++)
            {
                float v = 0.05f + Rand(i + seed + 960) * 0.83f;
                if (v < cut) continue;
                Vector2 q = At(a, b, L, nx, nz, width, flow, sag, Mathf.Lerp(u0, u1, v));
                float cycle = (s * (2.5f + Rand(i + seed + 961)) + Rand(i + seed + 962)) % 1f;
                float side = i % 2 == 1 ? -1f : 1f;
                float off = side * (Radius(v, u0, u1, L, length, width, flow) + width * (1f + cycle * 3f));
                float alpha = Sin01(cycle) * Smooth(length / 0.35f);
                float r = width * (0.2f + Rand(i + seed + 963) * 0.27f);
                float x = q.x + nx * off, z = q.y + nz * off - cycle * cycle * 0.07f;
                Disc(new Vector2(x, z), layer + 0.008f, r * (1.4f + cycle), r, angle, Fade(Water, alpha * 0.85f));
                Disc(new Vector2(x - r * 0.2f, z + r * 0.3f), layer + 0.010f, r * 0.75f, r * 0.38f, angle, Fade(WaterLit, alpha));
            }
        }

        private static Vector2 At(Vector2 a, Vector2 b, float L, float nx, float nz, float width, float flow, float sag, float u)
        {
            float ripple = width * (0.22f * Mathf.Sin(u * L * 5f - flow) + 0.12f * Mathf.Sin(u * L * 11f - flow * 1.3f));
            return new Vector2(Mathf.Lerp(a.x, b.x, u) + nx * ripple,
                Mathf.Lerp(a.y, b.y, u) + nz * ripple - Mathf.Sin(u * Mathf.PI) * sag * SixPathsHeight.Lift);
        }

        private static float Radius(float v, float u0, float u1, float L, float length, float width, float flow)
        {
            float u = Mathf.Lerp(u0, u1, v);
            float tail = u0 > 0f ? Mathf.Pow(Mathf.Clamp01(v / 0.48f), 0.65f) : 0.72f + 0.28f * Smooth(v / 0.25f);
            float front = u1 < 1f ? Mathf.Sqrt(Mathf.Clamp01((1f - v) * length / (width * 2.8f))) : 1f;
            float k = (1f - v) * length / (width * 4f) - 0.8f;
            float swell = 1f + 0.5f * Mathf.Exp(-k * k);
            float ripple = 1f + 0.12f * Mathf.Sin(u * L * 9f - flow) + 0.06f * Mathf.Sin(u * L * 19f - flow * 0.8f);
            return width * (1.1f + 0.35f * u) * tail * front * swell * ripple;
        }

        private static float BodyRadius(float v, float u0, float u1, float L, float length, float width, float flow, float cut) =>
            Radius(Mathf.Lerp(cut, 1f, v), u0, u1, L, length, width, flow) * (cut > 0f ? Smooth(v / 0.09f) : 1f);

        /// <summary>
        /// Stream Shot's torn wake: water from the last third of the nozzle pulse. Each parcel has a
        /// fixed emission time, travels slower than the jet, spreads and falls as it ages, and is gone
        /// at its own contact. <paramref name="age"/> is seconds since the nozzle opened.
        /// </summary>
        internal static void StreamTrail(Vector2 a, Vector2 b, float width, float age, float flight, float jet, float layer)
        {
            float released = age - jet, breakup = Smooth(released / 0.06f);
            if (breakup <= 0f || age > flight + jet + 0.14f) return;
            float dx = b.x - a.x, dz = b.y - a.y, L = Mathf.Sqrt(dx * dx + dz * dz);
            if (L <= 0f) L = 1f;
            float tx = dx / L, tz = dz / L, nx = -tz, nz = tx;
            float angle = -Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            for (int i = 0; i < 13; i++)
            {
                bool big = i < 5;
                float emission = big ? 0.72f + i * 0.055f + (Rand(i + 1010) - 0.5f) * 0.012f : 0.73f + Rand(i + 1010) * 0.27f;
                float drag = (big ? 0.09f + i * 0.012f : 0.07f + Rand(i + 1020) * 0.08f) * released * breakup;
                float u = (age - jet * emission - drag) / flight;
                if (u <= 0f || u >= 1f) continue;
                float spread = width * (big ? 0.45f : 1.2f) + released * (big ? 0.22f : 0.65f);
                float side = (Rand(i + 1030) - 0.5f) * 2f * spread * breakup;
                float x = Mathf.Lerp(a.x, b.x, u) + nx * side;
                float z = Mathf.Lerp(a.y, b.y, u) + nz * side - Mathf.Sin(u * Mathf.PI) * 0.05f * SixPathsHeight.Lift
                    - released * released * (big ? 0.45f : 0.8f);
                float alpha = breakup * (1f - Smooth((u - 0.96f) / 0.04f));
                float size = width * (big ? 0.48f + Rand(i + 1040) * 0.3f : 0.18f + Rand(i + 1040) * 0.18f);
                if (big)
                {
                    // Uneven stretched parcels, shorter toward the rear; they round out as they lose speed.
                    float halfLength = Mathf.Min(width * (1.25f - i * 0.13f), L * jet / flight * 0.025f) * (1f - 0.35f * Mathf.Clamp01(released / flight));
                    for (int j = 0; j <= 12; j++)
                    {
                        float v = j / 12f, along = (v * 2f - 1f) * halfLength;
                        float bend = Sin01(v) * size * 0.2f * Mathf.Sin(i * 2.7f + released * 12f);
                        P[j] = new Vector2(x + tx * along + nx * bend, z + tz * along + nz * bend);
                        W[j] = size * Mathf.Pow(Sin01(v), 0.7f) * (0.7f + 0.3f * v);
                    }
                    float at = layer + i * 0.00005f;
                    Tube(13, Fade(Water, alpha * 0.88f), at);
                    Tube(13, Fade(WaterDark, alpha * 0.35f), at + 0.002f, -0.9f, -0.35f);
                    Tube(13, Fade(WaterLit, alpha * 0.8f), at + 0.004f, 0.05f, 0.68f);
                }
                else
                {
                    Disc(new Vector2(x, z), layer + 0.006f, size * 1.6f, size, angle, Fade(Water, alpha * 0.8f));
                    Disc(new Vector2(x - size * 0.2f, z + size * 0.25f), layer + 0.008f, size * 0.7f, size * 0.35f, angle, Fade(WaterLit, alpha * 0.9f));
                }
            }
        }

        /// <summary>
        /// The water bag on the back: a level cylinder with its water drawn up to the fill level, a lit
        /// surface disc, the bag skin, rims, cap and two shoulder straps. When the back is on the far
        /// (north) side of the caster it is drawn under the pawn layer so the pawn stands in front.
        /// Returns the bag's height, which the hose starts from.
        /// </summary>
        internal static float Bag(in WaterGunFrame f, Vector2 feet, float s, float units, float slosh, float squeeze, float strength)
        {
            float level = Mathf.Clamp01(units / BagCap);
            float R = BagR * (1f - 0.12f * squeeze), H = BagH * (1f + 0.06f * squeeze);
            Vector2 B = f.Place(feet, BagAlong, BagAcross, BagBase);
            bool behind = B.y > feet.y + 0.06f;
            float bagLayer = behind ? PawnLayer - 0.03f : Y + 0.001f;
            Sprite(f.Cast(feet, BagAlong, BagAcross, BagBase + H * 0.5f), R * 2.6f, R * 1.5f, Fade(Body, strength * 0.6f), soft, ShadowLayer);
            float wl = level * H, bob = slosh * 0.02f * Mathf.Sin(s * 11f);
            if (level > 0f)
            {
                BagSide(B, R * 0.93f, 0.02f, wl + bob, 20);
                Band(21, Water, bagLayer);
                // Lit stripe on the south face of the water.
                for (int i = 0; i <= 8; i++)
                {
                    float th = Mathf.PI * 1.5f + (i / 8f - 0.5f) * 0.8f, x = B.x + Mathf.Cos(th) * R * 0.93f, z = B.y + Mathf.Sin(th) * R * 0.93f;
                    A[i] = new Vector2(x, z + 0.04f * SixPathsHeight.Lift);
                    C[i] = new Vector2(x, z + (wl + bob) * SixPathsHeight.Lift * 0.96f);
                }
                Band(9, Fade(WaterLit, 0.55f), bagLayer + 0.002f);
                Disc(new Vector2(B.x, B.y + (wl + bob) * SixPathsHeight.Lift), bagLayer + 0.004f, R * 0.93f, R * 0.93f, 0f, WaterLit);
            }
            BagSide(B, R, 0f, H, 20);
            Band(21, BagDark, bagLayer + 0.006f);
            Vector2 top = new Vector2(B.x, B.y + H * SixPathsHeight.Lift);
            Disc(top, bagLayer + 0.008f, R, R, 0f, BagSkin);
            Circle(top, R, 0.9f, bagLayer + 0.010f, BagRim);
            Circle(B, R, 0.5f, bagLayer + 0.0101f, BagRim);
            // Two straps over the shoulders to the chest.
            for (int k = 0; k < 2; k++)
            {
                float sx = k == 0 ? -1f : 1f;
                P[0] = f.Place(feet, BagAlong + 0.05f, BagAcross + sx * R * 0.6f, BagBase + H * 0.9f);
                P[1] = f.Place(feet, -0.05f, sx * 0.21f, 0.62f);
                P[2] = f.Place(feet, 0.10f, sx * 0.17f, 0.40f);
                W[0] = W[1] = W[2] = 0.03f;
                Tube(3, GunDark, (behind ? Y + 0.001f : bagLayer + 0.012f) + k * 0.0001f);
            }
            // Cap on top: a small dark disc.
            Disc(new Vector2(B.x + R * 0.35f, B.y + H * SixPathsHeight.Lift + 0.02f), bagLayer + 0.011f, 0.05f, 0.05f, 0f, GunDark);
            return H;
        }

        /// <summary>The south half of a level circle round <paramref name="B"/>, at heights h0 (A) and h1 (C).</summary>
        private static void BagSide(Vector2 B, float r, float h0, float h1, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float th = Mathf.PI + i / (float)n * Mathf.PI, x = B.x + Mathf.Cos(th) * r, z = B.y + Mathf.Sin(th) * r;
                A[i] = new Vector2(x, z + h0 * SixPathsHeight.Lift);
                C[i] = new Vector2(x, z + h1 * SixPathsHeight.Lift);
            }
        }

        /// <summary>
        /// The whole weapon for one frame: bag, gun and hose. At rest the gun hangs low across the body;
        /// raised it points along the aim at hand height. With <paramref name="draw"/> false nothing is
        /// drawn and only the muzzle is worked out. Returns the muzzle's drawn point.
        /// </summary>
        internal static Vector2 Weapon(in WaterGunFrame f, Vector2 feet, float s, in WaterGunPose pose, float strength, bool draw = true)
        {
            float lift = Smooth(pose.Raise);
            float ga0 = Mathf.Lerp(0.02f, GripAlong, lift) - pose.Recoil, ga1 = Mathf.Lerp(-0.22f, 0f, lift), ga2 = Mathf.Lerp(0.38f, HandH, lift);
            float gb0 = Mathf.Lerp(0.42f, MuzzleAlong, lift) - pose.Recoil, gb1 = Mathf.Lerp(-0.34f, 0f, lift), gb2 = Mathf.Lerp(0.16f, HandH, lift);
            Vector2 ga = f.Place(feet, ga0, ga1, ga2), gb = f.Place(feet, gb0, gb1, gb2);
            if (!draw) return gb;

            float H = Bag(f, feet, s, pose.Units, pose.Slosh, pose.Squeeze, strength);

            float gunDeg = Mathf.Atan2(gb.y - ga.y, gb.x - ga.x) * Mathf.Rad2Deg;
            float len = (gb - ga).magnitude;
            float gunLayer = Y + 0.014f;
            P[0] = f.Cast(feet, ga0, ga1, ga2);
            P[1] = f.Cast(feet, gb0, gb1, gb2);
            W[0] = W[1] = 0.09f;
            Tube(2, Fade(Body, strength * 0.7f), ShadowLayer + 0.0001f);
            // Body (rear 60 %), pump grip below it, nozzle (front 40 %).
            Vector2 body = Vector2.Lerp(ga, gb, 0.32f);
            Rect(body, len * 0.62f + 0.04f, 0.17f, gunDeg, GunDark, gunLayer);
            Rect(body, len * 0.62f, 0.13f, gunDeg, Gun, gunLayer + 0.002f);
            Rect(new Vector2(body.x, body.y + 0.035f), len * 0.56f, 0.035f, gunDeg, GunLit, gunLayer + 0.004f);
            Rect(Vector2.Lerp(ga, gb, 0.30f), len * 0.30f, 0.05f, gunDeg, Accent, gunLayer + 0.005f);
            Vector2 pump = Vector2.Lerp(ga, gb, 0.52f);
            pump.y -= 0.07f;
            Rect(pump, 0.16f, 0.09f, gunDeg, GunDark, gunLayer - 0.002f);
            Rect(pump, 0.13f, 0.06f, gunDeg, Accent, gunLayer - 0.001f);
            Vector2 nozzle = Vector2.Lerp(ga, gb, 0.80f);
            Rect(nozzle, len * 0.40f + 0.02f, 0.09f, gunDeg, GunDark, gunLayer + 0.0005f);
            Rect(nozzle, len * 0.40f, 0.06f, gunDeg, Steel, gunLayer + 0.0025f);
            Disc(gb, gunLayer + 0.006f, 0.04f, 0.04f, 0f, WaterDark);   // the bore

            // The hose from the bag's top over the shoulder to the grip: a quadratic curve of 19 points.
            float p00 = BagAlong, p01 = BagAcross, p02 = BagBase + H;
            float p10 = (BagAlong + ga0) / 2f - 0.05f, p11 = BagAcross * 0.5f + 0.12f, p12 = HandH + 0.3f;
            for (int i = 0; i <= 18; i++)
            {
                float u = i / 18f, w0 = (1f - u) * (1f - u), w1 = 2f * (1f - u) * u, w2 = u * u;
                P[i] = f.Place(feet, w0 * p00 + w1 * p10 + w2 * ga0, w0 * p01 + w1 * p11 + w2 * ga1, w0 * p02 + w1 * p12 + w2 * ga2);
            }
            for (int i = 0; i <= 18; i++) W[i] = HoseW + 0.02f;
            Tube(19, GunDark, Y + 0.008f);
            for (int i = 0; i <= 18; i++) W[i] = HoseW;
            Tube(19, Gun, Y + 0.010f);
            Tube(19, GunLit, Y + 0.012f, 0.1f, 0.55f);
            return gb;
        }

        /// <summary>The idle bag on a pawn holding the gun, facing <paramref name="aimDegrees"/>. No gun and no hose: Core draws the held gun.</summary>
        internal static void IdleBag(Vector2 feet, float aimDegrees, float units, Map map)
        {
            if (!Shown(feet, map)) return;
            Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            Bag(new WaterGunFrame(aimDegrees, sun), feet, 0f, units, 0f, 0f, shadow);
        }
    }
}
