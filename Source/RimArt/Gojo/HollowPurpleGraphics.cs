using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.HollowPurple;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>What a thing the sphere touched is, for how it breaks up.</summary>
    public enum HollowPurpleTouchKind
    {
        /// <summary>A plant or an item: specks of its colour drawn into the sphere.</summary>
        Thing,
        /// <summary>One cell of a wall or building: specks from a little lower, a cell wide.</summary>
        Building,
        /// <summary>A pawn on the centre row, erased: a white flash as the sphere touches it, then specks.</summary>
        Erased,
        /// <summary>A pawn on a side row, struck: a white flash, fewer specks, and a cut glowing on the edge facing the lane.</summary>
        Struck,
    }

    /// <summary>A thing the sphere touched: its ground point, cells off the path (left positive), when it was touched, what it is and its colour.</summary>
    public struct HollowPurpleTouch
    {
        public Vector2 Ground;
        public float Across, At;
        public HollowPurpleTouchKind Kind;
        public Color Colour;
    }

    /// <summary>A cut face of a building cell left standing next to an erased one: the face's two ends on the ground and when it was cut.</summary>
    public struct HollowPurpleCut
    {
        public Vector2 From, To;
        public float At;
    }

    /// <summary>What one Hollow Purple looks like now. Points are ground points on the map.</summary>
    public struct HollowPurpleShot
    {
        /// <summary>Gojo's position (the pawn's DrawPos in game) and the aim in degrees, 0 east, 90 north: Red's line and Purple's path.</summary>
        public Vector2 Feet;
        public float Aim;
        /// <summary>Blue's centre from Gojo and Purple's run (cells), Red's charge and the merge (s), Purple's speed (cells/s) and radius (cells), the world dim (0 to 1).</summary>
        public float BlueDist, Travel, Charge, Merge, Speed, Radius, Dim;
        /// <summary>Draw the trench shading (scoured strip, north wall, lips, cut edges, dust) while it passes.</summary>
        public bool Trench;
        /// <summary>The picture's clock, 0 with Blue already open.</summary>
        public float Seconds;
        /// <summary>What it touched, or null. Each is drawn from its touch on.</summary>
        public HollowPurpleTouch[] Touched;
        /// <summary>Cut faces of buildings left standing, or null.</summary>
        public HollowPurpleCut[] Cuts;
        /// <summary>Gojo's sleeve and skin colours, for the drawn pointing arm.</summary>
        public Color Sleeve, Skin;
        /// <summary>
        /// Where the pointing arm is drawn when Gojo does not stand on the path's line (in game Red can pass up to
        /// Hollow Purple's blueRadius from Blue's centre, and <see cref="Feet"/> is then Blue's centre moved back
        /// along the aim). Left at zero it is <see cref="Feet"/>, as in the previews.
        /// </summary>
        public Vector2 ArmAt;
    }

    /// <summary>
    /// Draws Hollow Purple, the port of the lab's gojo-purple.js. Blue is already open in front of Gojo
    /// (its core sphere, fog and streaks, a compact copy of Blue v2); Gojo points and Red charges at the
    /// finger (a red swirl and orb, a compact Red v2), flies into Blue and the two spiral in: red and blue
    /// arcs, the world split red on one side and blue on the other, a red and a cyan half ring, and the
    /// darkening over the last 0.15 s. The ignition: a white point and flash, a purple wash, four ripple
    /// rings and 12 rays. Purple grows and travels: a plum body lit from its edge, violet bands, a white
    /// core with rays, lightning, ripple rings, light rays along the path, the ground under it glowing
    /// lilac with a white cut line and specks lifting into it, a haze band behind it, the trench with its
    /// cut edges cooling, its north wall, lips and dust, the view dimmed toward violet-black with violet
    /// light round the sphere. At the end it breaks into specks with a last ring and the dim lifts.
    /// What it touched breaks into specks drawn into it; a touched pawn flashes white; a struck pawn
    /// keeps a glowing cut on the edge facing the lane; the cut faces of a wall glow and fade.
    ///
    /// Everything is a level circle, a quad or a strip laid along the path, so the picture turns with the
    /// aim and there is no per-facing method.
    ///
    /// Not drawn (the sketch's stand-ins, or the game's): Gojo's body and his red and violet tint, the
    /// raiders, the ally lying down, the tree, the wall cells and the crate, and the violet light on
    /// them. The Erased ground lane is the game's terrain (the preview draws a stand-in for it), and the
    /// sketch's rule lines are not in game.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HollowPurpleGraphics
    {
        private const float Tau = 6.2831855f;

        internal static readonly Color Red = new Color(1f, 0.1f, 0.14f), RedDeep = new Color(0.42f, 0f, 0.05f), HotPink = new Color(1f, 0.78f, 0.82f);
        internal static readonly Color Royal = new Color(0.12f, 0.32f, 0.95f), Abyss = new Color(0.01f, 0.03f, 0.16f), Cyan = new Color(0.45f, 0.85f, 1f);
        internal static readonly Color Violet = new Color(0.58f, 0.22f, 1f), Lilac = new Color(0.86f, 0.72f, 1f), Plum = new Color(0.2f, 0.03f, 0.36f),
            Night = new Color(0.06f, 0f, 0.12f), Magenta = new Color(0.95f, 0.35f, 1f);
        internal static readonly Color Scoured = new Color(0.3f, 0.25f, 0.23f), Earth = new Color(0.38f, 0.3f, 0.24f), Lip = new Color(0.62f, 0.56f, 0.5f),
            Haze = new Color(0.72f, 0.67f, 0.6f);
        private static readonly Color White = GokuGraphics.White, Blue = VergilGraphics.Blue, Deep = VergilGraphics.Deep, Ice = VergilGraphics.Ice;

        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly float PawnLayer = GokuGraphics.PawnLayer, BuildingLayer = AltitudeLayer.Building.AltitudeFor();
        private static readonly float Lift = GokuGraphics.Lift;

        // Purple's rings and bands: (share of the radius, alpha).
        private static readonly float[,] PurpleRings = { { 0.985f, 0.5f }, { 0.95f, 0.36f }, { 0.91f, 0.24f }, { 0.87f, 0.15f }, { 0.83f, 0.09f } };
        private static readonly float[,] BlueRings = { { 0.985f, 0.38f }, { 0.95f, 0.28f }, { 0.91f, 0.19f }, { 0.87f, 0.12f } };
        // The stand-in pawn's body and head as ellipses (centre x, centre z, radius x, radius z), standing and lying: the white flash's shape.
        private static readonly float[,] Standing = { { 0f, 0.18f, 0.22f, 0.32f }, { 0f, 0.58f, 0.16f, 0.17f } };
        private static readonly float[,] Lying = { { 0f, 0.12f, 0.32f, 0.2f }, { 0.42f, 0.14f, 0.16f, 0.17f } };

        private static readonly Capsule trench = new Capsule("Hollow Purple trench"), trenchFloor = new Capsule("Hollow Purple trench floor");
        private static readonly Vector2[] two = new Vector2[2], foot = new Vector2[2];

        public static void Draw(in HollowPurpleShot shot, Map map)
        {
            float s = shot.Seconds;
            HollowPurpleTimes t = T.TimesFor(shot.BlueDist, shot.Travel, shot.Charge, shot.Merge, shot.Speed);
            if (s < 0f || s >= t.End || !Shown(shot.Feet, map)) return;
            Begin(shot.Feet);
            Vector2 feet = shot.Feet, toward = Turn(shot.Aim);
            float aim = shot.Aim, a = aim * Mathf.Deg2Rad, ca = toward.x, sa = toward.y, chest = GokuGraphics.ChestOn;
            float B = shot.BlueDist, R = shot.Radius, speed = shot.Speed;
            Vector2 Place(float along, float across = 0f) => GokuTiming.Place(feet, toward, along, across);
            Vector2 Up(Vector2 ground, float h) => new Vector2(ground.x, ground.y + h);

            // Purple's centre, cells past Gojo, and its radius now.
            float grown = T.Grown(t, s), fading = T.Fading(t, s);
            float at = T.Centre(t, s, B, shot.Travel, speed), size = T.Size(t, s, R);
            bool purpleOn = s >= t.Ignite && s < t.Gone;
            Vector2 P0 = Up(Place(at), chest);

            // --- floor: the trench, a cut with a wall you can see; the cut edges cooling; the ground being erased ---------
            float mv = s >= t.Move && s < t.Gone ? Smooth((s - t.Move) / 0.3f) * fading : 0f;
            // The trench shading holds while it passes, then fades into the flat lane: terrain has no depth to keep.
            float tr = T.TrenchNow(t, s);
            if (shot.Trench && s >= t.Move && tr > 0f && at > B + 0.05f)
            {
                Vector2 from = Place(B), to = Place(at);
                trench.Build(from, to, R, a);
                DrawMesh(trench.mesh, from, Floor + 0.012f, 1f, 1f, 0f, Fade(Scoured, 0.5f * tr), solid);
                trenchFloor.Build(from, to, R * 0.9f, a);
                DrawMesh(trenchFloor.mesh, from, Floor + 0.0122f, 1f, 1f, 0f, Fade(Night, 0.12f * tr), solid);
                for (int k = -1; k <= 1; k += 2)
                {
                    two[0] = Place(B, k * R); two[1] = Place(at, k * R);
                    if (k * ca > 0.05f)
                    {
                        // The wall below the north lip is seen from above: a band TrenchDepth x 0.6 tall, earth, darker at its foot.
                        float drop = T.TrenchDepth * Lift;
                        foot[0] = new Vector2(two[0].x, two[0].y - drop); foot[1] = new Vector2(two[1].x, two[1].y - drop);
                        Sides(2, out Vector2[] top, out Vector2[] bottom);
                        top[0] = two[0]; top[1] = two[1]; bottom[0] = foot[0]; bottom[1] = foot[1];
                        Strip(top, bottom, Fade(Earth, 0.9f * tr), solid, Floor + 0.0126f);
                        GokuGraphics.Line(foot, 0.07f, Fade(Night, 0.5f * tr), solid, Floor + 0.0127f, Taper.None);
                        GokuGraphics.Line(two, 0.05f, Fade(Lip, 0.6f * tr), solid, Floor + 0.0128f, Taper.None);
                    }
                    else GokuGraphics.Line(two, 0.08f, Fade(Night, 0.6f * tr), solid, Floor + 0.0128f, Taper.None);
                    // The cut edges glow where the sphere has just passed: white, then lilac, then violet, gone in 1.5 s.
                    for (float x0 = Mathf.Max(B, at - T.EdgeCool * speed - T.EdgeStep); x0 < at; x0 += T.EdgeStep)
                    {
                        float x1 = Mathf.Min(at, x0 + T.EdgeStep), age = s - T.PassedAt(t, B, speed, (x0 + x1) / 2f), u = Mathf.Clamp01(age / T.EdgeCool);
                        if (u >= 1f) continue;
                        Color colour = age < 0.3f ? Color.Lerp(White, Lilac, age / 0.3f) : Color.Lerp(Lilac, Violet, Mathf.Clamp01((age - 0.3f) / 0.6f));
                        Vector2[] edge = GokuGraphics.Points(2);
                        edge[0] = Up(Place(x0, k * R * 0.99f), 0.02f * Lift); edge[1] = Up(Place(x1, k * R * 0.99f), 0.02f * Lift);
                        GokuGraphics.Line(edge, 0.15f - 0.07f * u, Fade(colour, 0.9f * (1f - u)), whiteGlow, Floor + 0.014f, Taper.None);
                    }
                }
            }
            if (purpleOn)
            {
                Sprite(Place(at), 11f * grown, 8.5f * grown, Fade(Plum, 0.3f * fading), soft, Floor + 0.029f);
                Sprite(Place(at), 12f * grown, 9f * grown, Fade(Violet, 0.45f * fading), glow, Floor + 0.03f);
            }
            // The ground under the sphere being erased: a glow, a bright cut line round the front of its footprint,
            // and specks of earth lifting off and drawn into it.
            if (mv > 0f)
            {
                Vector2 under = Place(at);
                Sprite(under, R * 2.2f, R * 1.6f, Fade(Lilac, 0.5f * mv), glow, Floor + 0.034f);
                Sprite(Place(at + R * 0.45f), R * 1.2f, R * 0.6f, Fade(White, 0.45f * mv), glow, Floor + 0.035f, -aim);
                Vector2[] cut = GokuGraphics.Points(13);
                for (int m = 0; m <= 12; m++)
                {
                    float q = a + (m / 12f * 2f - 1f) * 80f * Mathf.Deg2Rad;
                    cut[m] = new Vector2(under.x + Mathf.Cos(q) * R, under.y + Mathf.Sin(q) * R);
                }
                GokuGraphics.Line(cut, 0.3f, Fade(Lilac, 0.35f * mv), whiteGlow, Floor + 0.036f, Taper.Both);
                GokuGraphics.Line(cut, 0.08f, Fade(White, 0.9f * mv), whiteGlow, Floor + 0.037f, Taper.Both);
                for (int i = 0; i < T.Lifted; i++)
                {
                    float ph = (s / 0.35f + Rand(i + 3200)) % 1f, along = (Rand(i + 3210) - 0.2f) * R, across = (Rand(i + 3220) - 0.5f) * R * 1.8f;
                    Vector2 g0 = Place(at + along, across);
                    float k = Smooth(ph);
                    var q = new Vector2(g0.x + (P0.x - g0.x) * k * 0.7f, g0.y + (P0.y - g0.y) * k * 0.7f + k * 0.2f);
                    Sprite(q, 0.09f, 0.09f, Fade(Color.Lerp(Haze, Lilac, k), Mathf.Sin(Mathf.PI * ph) * mv), glow, Overhead + 0.14f + i * 0.0001f);
                }
            }
            // The purple band left behind (S1 #47-48): haze rising along the cut and fading over 1.1 s after the sphere passes.
            if (s >= t.Move)
                for (int n = 0; ; n++)
                {
                    float x = B + (n + 0.5f) * T.BandStep;
                    if (x >= at) break;
                    float u = (s - T.PassedAt(t, B, speed, x)) / T.BandLife;
                    if (u < 0f || u >= 1f) continue;
                    for (int j = 0; j < 3; j++)
                    {
                        int id = n * 3 + j;
                        float c = (j - 1) * 0.55f, up = 0.2f + 1.2f * u, sz = R * 1.7f * (0.7f + 0.5f * u), layer = (n % 30) * 0.0002f;
                        Vector2 q = Up(Place(x + (Rand(id + 3330) - 0.5f) * 0.3f, c * R + (Rand(id + 3300) - 0.5f) * 0.3f), up * Lift);
                        Sprite(q, sz, sz * 0.8f, Fade(Plum, 0.28f * (1f - u)), puff, Overhead + 0.02f + layer, Rand(id + 3310) * 360f + u * 70f);
                        Sprite(q, sz * 0.85f, sz * 0.7f, Fade(Violet, 0.5f * Mathf.Pow(1f - u, 1.5f)), GojoGraphics.PuffGlow, Overhead + 0.1f + layer, Rand(id + 3320) * 360f - u * 70f);
                    }
                }
            // Dust rising off both lips of the trench once the band has thinned (S1 #61), fading over 2 s.
            if (shot.Trench && s >= t.Move)
                for (int k = -1; k <= 1; k += 2)
                    for (int n = 0; ; n++)
                    {
                        float x = B + (n + 0.5f) * T.DustStep;
                        if (x >= at) break;
                        int id = n * 2 + (k > 0 ? 1 : 0);
                        float u = (s - T.PassedAt(t, B, speed, x) - 0.25f - 0.3f * Rand(id + 3410)) / (T.DustLife * (0.7f + 0.5f * Rand(id + 3420)));
                        if (u <= 0f || u >= 1f) continue;
                        Vector2 q = Up(Place(x + (Rand(id + 3430) - 0.5f) * 0.5f, k * R * (1f + 0.1f * Rand(id + 3440) + 0.35f * u)), (0.1f + (0.5f + 0.8f * Rand(id + 3450)) * u) * Lift);
                        float sz = (0.4f + 1.3f * u) * (0.7f + 0.6f * Rand(id + 3460));
                        Sprite(q, sz, sz * 0.85f, Fade(Haze, 0.28f * Mathf.Sin(Mathf.PI * u)), puff, Overhead + 0.015f + (n % 30) * 0.0002f, Rand(id + 3400) * 360f + u * 50f);
                    }

            // --- the cut faces of a wall: the face toward the path glows for 1.5 s after the cut --------------------------
            if (shot.Cuts != null)
                foreach (HollowPurpleCut face in shot.Cuts)
                {
                    float g = s >= face.At ? 1f - Mathf.Clamp01((s - face.At) / T.WallCutGlow) : 0f;
                    if (g <= 0f) continue;
                    two[0] = Up(face.From, 0.02f * Lift); two[1] = Up(face.To, 0.02f * Lift);
                    GokuGraphics.Line(two, 0.12f, Fade(Lilac, 0.8f * g), whiteGlow, BuildingLayer + 0.01f, Taper.None);
                }

            // --- Blue (already open) and Red coming in, then the merge ------------------------------------------------------
            Vector2 blueGround = Place(B), C = Up(blueGround, chest);
            if (s < t.Ignite)
            {
                float k = s < t.Contact ? 1f : 1f - Smooth((s - t.Contact) / shot.Merge);
                // Fog and streaks of the open Blue, lighter than Blue v2's.
                for (int i = 0; i < 12; i++)
                {
                    float r0 = 1.2f + 2.6f * Rand(i + 2400), q = Rand(i + 2410) * Tau - s * 3.2f / Mathf.Max(1f, r0) - (s >= t.Contact ? (s - t.Contact) * 12f : 0f);
                    Sprite(new Vector2(blueGround.x + Mathf.Cos(q) * r0 * k, blueGround.y + Mathf.Sin(q) * r0 * k + 0.12f), 1.8f, 1.4f, Fade(Deep, 0.3f * k), soft, Floor + 0.05f + i * 0.0003f);
                }
                for (int i = 0; i < 10; i++)
                {
                    float ph = (s / 0.6f + Rand(i + 2420)) % 1f, a0 = Rand(i + 2430) * Tau;
                    Vector2[] pts = GokuGraphics.Points(7);
                    for (int m = 0; m <= 6; m++)
                    {
                        float v = Mathf.Max(0f, ph - 0.22f + 0.22f * m / 6f), rr = 1f + 3f * Mathf.Pow(1f - v, 1.4f), q = a0 - 2f * v;
                        pts[m] = new Vector2(blueGround.x + Mathf.Cos(q) * rr * k, blueGround.y + Mathf.Sin(q) * rr * k + chest * v * v);
                    }
                    GokuGraphics.Line(pts, 0.045f, Fade(Ice, 0.5f * k * Mathf.Sin(Mathf.PI * ph)), whiteGlow, Overhead + 0.09f, Taper.Both);
                }
            }
            if (s < t.Contact) BlueSphere(C, T.BlueR, s, 1f);
            // Red at the finger, then in flight.
            if (s >= T.Start && s < t.Fire)
            {
                float u = Mathf.Clamp01((s - T.Start) / shot.Charge), Rr = 0.3f + 0.35f * u;
                Vector2 F = Up(Place(T.Tip), chest);
                for (int i = 0; i < 3; i++)
                {
                    float a0 = -s * 9.4f + i * Tau / 3f;
                    Vector2[] pts = GokuGraphics.Points(11);
                    for (int m = 0; m <= 10; m++)
                    {
                        float v = m / 10f, rr = Rr * (0.15f + 0.85f * v), q = a0 + v * 2.6f;
                        pts[m] = new Vector2(F.x + Mathf.Cos(q) * rr, F.y + Mathf.Sin(q) * rr);
                    }
                    GokuGraphics.Line(pts, Rr * 0.25f, Fade(Red, 0.6f * u), whiteGlow, Overhead + 0.118f, Taper.Both);
                }
                RedBall(F, T.OrbR * Smooth(Mathf.Clamp01(u / 0.6f)), 1f);
            }
            if (s >= t.Fire && s < t.Contact)
            {
                float d = T.Tip + (s - t.Fire) * T.RedSpeed;
                Vector2 P = Up(Place(d), chest);
                two[0] = P; two[1] = Up(Place(Mathf.Max(T.Tip, d - 1.8f)), chest);
                GokuGraphics.Line(two, 0.5f, Fade(RedDeep, 0.45f), solid, Overhead + 0.115f, Taper.End);
                GokuGraphics.Line(two, 0.28f, Fade(Red, 0.6f), whiteGlow, Overhead + 0.116f, Taper.End);
                RedBall(P, T.OrbR, 1f);
            }
            // Merge: red and blue spiral round each other and in; red and blue arcs swirl; the ground split red / blue.
            if (s >= t.Contact && s < t.Ignite)
            {
                float u = (s - t.Contact) / shot.Merge, e = Smooth(u), rho = 0.8f * (1f - e), th = a + Mathf.PI + e * 4f * Tau / 2f;
                var spin = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
                Vector2 rP = C + spin * rho, bP = C - spin * (rho * 0.6f), side = new Vector2(-sa, ca);
                // The world split red and blue (S2 ep 4 #30-32): a red half on one side, a blue half on the other,
                // and a red and a cyan half ring 2.2 cells out spinning opposite ways.
                float env = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.1f)));
                Sprite(blueGround + side * 3f, 8f, 7f, Fade(Red, 0.45f * env), glow, Floor + 0.03f);
                Sprite(blueGround - side * 3f, 8f, 7f, Fade(Blue, 0.5f * env), glow, Floor + 0.031f);
                Sprite(C + side * 2.5f, 6f, 5f, Fade(RedDeep, 0.25f * env), soft, Overhead + 0.0012f);
                Sprite(C - side * 2.5f, 6f, 5f, Fade(Abyss, 0.25f * env), soft, Overhead + 0.0013f);
                for (int j = 0; j < 2; j++)
                {
                    float dirn = j == 0 ? 1f : -1f, q0 = a + Mathf.PI / 2f * dirn + dirn * (s - t.Contact) * 7f, rr = 2.2f * (1f - 0.4f * e);
                    Vector2[] pts = GokuGraphics.Points(17);
                    for (int m = 0; m <= 16; m++)
                    {
                        float q = q0 + Mathf.PI * m / 16f;
                        pts[m] = new Vector2(C.x + Mathf.Cos(q) * rr, C.y + Mathf.Sin(q) * rr);
                    }
                    GokuGraphics.Line(pts, 0.3f, Fade(j == 0 ? Red : Cyan, 0.6f * env), whiteGlow, Overhead + 0.124f + j * 0.0002f, Taper.Both);
                    GokuGraphics.Line(pts, 0.08f, Fade(White, 0.7f * env), whiteGlow, Overhead + 0.1242f + j * 0.0002f, Taper.Both);
                }
                // The world holds its breath: it darkens over the last 0.15 s before the flash.
                float hush = Smooth((s - (t.Ignite - 0.15f)) / 0.15f);
                if (hush > 0f) Sprite(C, 20f, 18f, Fade(Night, 0.45f * hush), soft, Overhead + 0.0015f);
                float arcs = 0.7f * Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.2f)));
                for (int i = 0; i < 6; i++)
                {
                    float rr = (0.6f + 0.2f * i) * (1f - 0.5f * e), q0 = th * (1f + 0.15f * i) + i * 1.3f;
                    Vector2[] pts = GokuGraphics.Points(11);
                    for (int m = 0; m <= 10; m++)
                    {
                        float q = q0 + 1.6f * m / 10f;
                        pts[m] = new Vector2(C.x + Mathf.Cos(q) * rr, C.y + Mathf.Sin(q) * rr);
                    }
                    GokuGraphics.Line(pts, 0.1f, Fade(i % 2 == 1 ? Red : Cyan, arcs), whiteGlow, Overhead + 0.125f + i * 0.0002f, Taper.Both);
                }
                BlueSphere(bP, T.BlueR * (1f - 0.5f * e), s, 1f);
                RedBall(rP, T.OrbR * (1f + 0.6f * e), 1f);
                Sprite(C, 1.6f * e, 1.6f * e, Fade(Magenta, 0.6f * e), glow, Overhead + 0.13f);
            }

            // --- ignition: white point, flash, ripple rings, rays ---------------------------------------------------------
            if (s >= t.Ignite && s < t.Ignite + 0.7f)
            {
                float age = s - t.Ignite;
                if (age < 0.5f)
                {
                    float f = age / 0.5f;
                    Sprite(C, 18f, 16f, Fade(Violet, 0.5f * (1f - f)), glow, Overhead + 0.0016f);
                    Sprite(C, 16f, 14f, Fade(Plum, 0.3f * (1f - f)), soft, Overhead + 0.0015f);
                }
                if (age < 0.25f)
                {
                    float f = age / 0.25f;
                    Sprite(C, 3.5f * (1f - f) + 0.5f, 3.5f * (1f - f) + 0.5f, Fade(White, 0.95f * (1f - f)), glow, Overhead + 0.2f);
                    GokuGraphics.Glint(C, 1.6f * (1f - f) + 0.3f, 1f - f, Lilac, 15f);
                }
                for (int i = 0; i < 4; i++)
                {
                    float u = Mathf.Clamp01((age - i * 0.08f) / 0.6f);
                    if (u > 0f && u < 1f)
                        PaperBombGraphics.RingAt(C, 0.2f + 3f * Smooth(u), Fade(Violet, 0.7f * (1f - u)), Overhead + 0.19f + i * 0.0002f, i % 2 == 0, whiteGlow);
                }
                if (age < 0.35f)
                    for (int i = 0; i < 12; i++)
                    {
                        float q = i * Tau / 12f + Rand(i + 2500) * 0.3f, len = (2f + 2f * Rand(i + 2510)) * Smooth(age / 0.1f);
                        two[0] = C; two[1] = new Vector2(C.x + Mathf.Cos(q) * len, C.y + Mathf.Sin(q) * len);
                        GokuGraphics.Line(two, 0.16f, Fade(Lilac, 0.7f * (1f - age / 0.35f)), whiteGlow, Overhead + 0.195f, Taper.End);
                    }
            }

            // --- the world lit purple while it travels (S1 ep 20 46-53 s): the view dims toward violet-black over pawns and
            // walls, violet light round the sphere above the dim; the dim lifts as the sphere breaks up ------------------------
            float dim = T.DimNow(t, s, shot.Dim);
            if (dim > 0f)
            {
                // One quad over everything the camera sees, padded by two cells so a camera shake does not show an edge.
                CellRect view = Find.CameraDriver.CurrentViewRect;
                Vector3 centre = view.CenterVector3;
                DrawMesh(MeshPool.plane10, new Vector2(centre.x, centre.z), Overhead + 0.0014f, view.Width + 4f, view.Height + 4f, 0f, Fade(Night, dim), solid);
                if (purpleOn) Sprite(Place(at), 10f * grown, 8f * grown, Fade(Violet, 0.5f * dim * fading), glow, Overhead + 0.0017f);
            }

            // --- Gojo's pointing arm, and what it touched: flash, cut, specks drawn into the sphere -----------------------------
            GojoGraphics.PointingArm(shot.ArmAt == Vector2.zero ? feet : shot.ArmAt, aim, T.ArmOut(t, s), chest, shot.Sleeve, shot.Skin);
            if (shot.Touched != null)
                foreach (HollowPurpleTouch touch in shot.Touched)
                {
                    float age = s - touch.At;
                    if (age < 0f) continue;
                    switch (touch.Kind)
                    {
                        case HollowPurpleTouchKind.Thing:
                            Dissolve(Up(touch.Ground, 0.5f * Lift), 0.9f, age, touch.Colour, P0, 16);
                            break;
                        case HollowPurpleTouchKind.Building:
                            Dissolve(Up(touch.Ground, 0.4f * Lift), 1f, age, touch.Colour, P0, 14);
                            break;
                        case HollowPurpleTouchKind.Erased:
                            // Centre row: a white flash as the sphere touches him, then nothing but his specks going into the sphere.
                            if (age < 0.1f) Flash(touch.Ground, 0.85f * (1f - age / 0.1f), false);
                            Dissolve(Up(touch.Ground, chest), 0.7f, age, touch.Colour, P0, 16);
                            break;
                        case HollowPurpleTouchKind.Struck:
                            // Side row: a white flash, down, a cut glowing on the edge facing the lane (+1 when the lane is north
                            // of him; on a path near north-south the lower edge stands in), specks off the body.
                            Flash(touch.Ground, 0.8f * (1f - Mathf.Clamp01(age / 0.2f)), age > 0.15f);
                            float away = touch.Across > 0f ? -1f : touch.Across < 0f ? 1f : 0f;
                            if (age > 0.15f) CutGlow(touch.Ground, away * ca > 0.3f ? 1f : -1f, age - 0.15f);
                            Dissolve(Up(touch.Ground, chest), 0.6f, age, touch.Colour, P0, 10);
                            break;
                    }
                }

            // --- Purple: the sphere, ripple rings, rays along the path, specks drawn in ------------------------------------------
            Vector2 Pt = P0;
            if (purpleOn)
            {
                if (s >= t.Move)
                {
                    float since = s - t.Move;
                    int last = Mathf.FloorToInt(since / T.RingEvery);
                    for (int n = Mathf.Max(0, last - 1); n <= last; n++)
                    {
                        float u = (since - n * T.RingEvery) / 0.6f;
                        if (u > 0f && u < 1f)
                            PaperBombGraphics.RingAt(Pt, size + 1.6f * Smooth(u), Fade(Violet, 0.45f * (1f - u) * fading), Overhead + 0.148f, false, whiteGlow);
                    }
                    for (int i = 0; i < T.Rays; i++)
                    {
                        float q = a + (i % 2 == 1 ? 0f : Mathf.PI) + (Rand(i + 2600) - 0.5f) * 30f * Mathf.Deg2Rad;
                        float len = (3f + 4f * Rand(i + 2610)) * (0.7f + 0.3f * Mathf.Sin(s * 11f + i)), off = (Rand(i + 2620) - 0.5f) * size;
                        two[0] = new Vector2(Pt.x - Mathf.Sin(q) * off, Pt.y + Mathf.Cos(q) * off);
                        two[1] = new Vector2(two[0].x + Mathf.Cos(q) * len, two[0].y + Mathf.Sin(q) * len);
                        GokuGraphics.Line(two, 0.12f, Fade(Lilac, 0.3f * fading * grown), whiteGlow, Overhead + 0.145f, Taper.End);
                    }
                    // Specks swept in from the front, the air it erases.
                    for (int i = 0; i < 16; i++)
                    {
                        float ph = (s / 0.5f + Rand(i + 2700)) % 1f, q = a + (Rand(i + 2710) - 0.5f) * 2.4f, r0 = size * (1.2f + 1.5f * Rand(i + 2720)), rr = r0 * (1f - ph) + size * 0.3f * ph;
                        Sprite(new Vector2(Pt.x + Mathf.Cos(q) * rr, Pt.y + Mathf.Sin(q) * rr), 0.1f, 0.1f, Fade(i % 2 == 1 ? Lilac : White, Mathf.Sin(Mathf.PI * ph) * fading), glow, Overhead + 0.149f);
                    }
                }
                PurpleSphere(Pt, size, s, s >= t.Stop ? 0.5f + 0.5f * fading : 1f);
            }
            // Break-up at the end: specks flying out, a last ring.
            if (s >= t.Stop && s < t.Gone + 0.3f)
            {
                float u = (s - t.Stop) / (T.Fade + 0.3f);
                for (int i = 0; i < 30; i++)
                {
                    float q = Rand(i + 2800) * Tau, rr = R * (0.3f + 1.8f * Smooth(u) * (0.5f + 0.5f * Rand(i + 2810)));
                    Sprite(new Vector2(Pt.x + Mathf.Cos(q) * rr, Pt.y + Mathf.Sin(q) * rr + u * 0.4f), 0.12f, 0.12f, Fade(i % 3 != 0 ? Lilac : Violet, 1f - u), glow, Overhead + 0.16f);
                }
                PaperBombGraphics.RingAt(Pt, R * (1f + 1.5f * Smooth(u)), Fade(Lilac, 0.5f * (1f - u)), Overhead + 0.161f, false, whiteGlow);
            }
        }

        /// <summary>Red: a shaded red ball in a red corona with a white-hot glint (Red v2's flight look, compact).</summary>
        private static void RedBall(Vector2 at, float r, float alpha)
        {
            if (r <= 0.01f || alpha <= 0f) return;
            Sprite(at, r * 7f, r * 7f, Fade(Red, 0.45f * alpha), glow, Overhead + 0.12f);
            DrawMesh(disc, at, Overhead + 0.121f, r * 1.1f, r * 1.1f, 0f, Fade(RedDeep, alpha), solid);
            DrawMesh(disc, new Vector2(at.x + r * 0.08f, at.y + r * 0.08f), Overhead + 0.122f, r * 0.85f, r * 0.85f, 0f, Fade(Red, alpha), solid);
            DrawMesh(disc, new Vector2(at.x + r * 0.35f, at.y + r * 0.38f), Overhead + 0.123f, r * 0.3f, r * 0.26f, 0f, Fade(HotPink, 0.85f * alpha), solid);
            Sprite(at, r * 1.4f, r * 1.4f, Fade(HotPink, 0.35f * alpha), glow, Overhead + 0.124f);
        }

        /// <summary>Blue's core sphere, compact (Blue v2): royal body darkening inward, lit edge, whirlpool bands, the eye.</summary>
        private static void BlueSphere(Vector2 at, float R, float s, float alpha)
        {
            if (R <= 0.01f || alpha <= 0f) return;
            Sprite(at, R * 4.4f, R * 4.4f, Fade(Blue, 0.4f * alpha), glow, Overhead + 0.1f);
            DrawMesh(disc, at, Overhead + 0.101f, R, R, 0f, Fade(Royal, 0.92f * alpha), solid);
            Sprite(at, R * 1.9f, R * 1.9f, Fade(Abyss, 0.9f * alpha), soft, Overhead + 0.1015f);
            for (int j = 0; j < BlueRings.GetLength(0); j++)
                PaperBombGraphics.RingAt(at, R * BlueRings[j, 0], Fade(Cyan, BlueRings[j, 1] * alpha), Overhead + 0.102f + j * 0.0001f, false, whiteGlow);
            for (int k = 0; k < 8; k++)
            {
                float v = k / 7f, rr = R * (0.3f + 0.62f * v), a0 = Rand(k + 1250) * Tau - s * 3.2f * Mathf.Pow(R / rr, 1.1f);
                Vector2[] pts = GokuGraphics.Points(9);
                for (int m = 0; m <= 8; m++)
                {
                    float q = a0 + 1.2f * m / 8f;
                    pts[m] = new Vector2(at.x + Mathf.Cos(q) * rr, at.y + Mathf.Sin(q) * rr);
                }
                GokuGraphics.Line(pts, R * 0.07f, Fade(Color.Lerp(Cyan, Ice, v), 0.42f * alpha), whiteGlow, Overhead + 0.103f + k * 0.0002f, Taper.Both);
            }
            Sprite(at, R * 0.22f, R * 0.22f, Fade(White, alpha), glow, Overhead + 0.105f);
        }

        /// <summary>
        /// Purple: a deep plum body lit from its edge in lilac, violet bands turning, a white core with rays,
        /// lightning crackling off it, specks drawn in. <paramref name="R"/> is the radius; <paramref name="s"/> drives the motion.
        /// </summary>
        private static void PurpleSphere(Vector2 at, float R, float s, float alpha)
        {
            if (R <= 0.01f || alpha <= 0f) return;
            float beat = 1f + 0.06f * Mathf.Sin(s * 13f);
            Sprite(at, R * 4.2f * beat, R * 4.2f * beat, Fade(Violet, 0.45f * alpha), glow, Overhead + 0.15f);
            Sprite(at, R * 2.6f, R * 2.6f, Fade(Magenta, 0.25f * alpha), glow, Overhead + 0.1505f);
            DrawMesh(disc, at, Overhead + 0.151f, R, R, 0f, Fade(Plum, 0.95f * alpha), solid);
            Sprite(at, R * 1.8f, R * 1.8f, Fade(Night, 0.8f * alpha), soft, Overhead + 0.1515f);
            for (int j = 0; j < PurpleRings.GetLength(0); j++)
                PaperBombGraphics.RingAt(at, R * PurpleRings[j, 0], Fade(Lilac, PurpleRings[j, 1] * alpha), Overhead + 0.152f + j * 0.0001f, false, whiteGlow);
            PaperBombGraphics.RingAt(at, R * 1.02f, Fade(Violet, 0.4f * alpha), Overhead + 0.1526f, true, whiteGlow);
            for (int k = 0; k < T.Bands; k++)
            {
                float v = k / (float)(T.Bands - 1), rr = R * (0.28f + 0.64f * v), a0 = Rand(k + 2050) * Tau - s * 2.6f * Mathf.Pow(R / rr, 1.1f), span = 0.9f + 0.8f * Rand(k + 2060);
                Vector2[] pts = GokuGraphics.Points(9);
                for (int m = 0; m <= 8; m++)
                {
                    float q = a0 + span * m / 8f, wob = 1f + 0.04f * Mathf.Sin(m * 1.7f + s * 6f + k);
                    pts[m] = new Vector2(at.x + Mathf.Cos(q) * rr * wob, at.y + Mathf.Sin(q) * rr * wob);
                }
                GokuGraphics.Line(pts, R * (0.04f + 0.04f * Rand(k + 2070)), Fade(Color.Lerp(Magenta, Lilac, v), 0.5f * alpha), whiteGlow, Overhead + 0.153f + k * 0.0002f, Taper.Both);
            }
            for (int i = 0; i < T.Specks; i++)
            {
                float d = R * (0.4f + 0.6f * Rand(i + 2100)), q = Rand(i + 2110) * Tau - s * 2.6f * Mathf.Pow(R / d, 1.1f), tw = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(s * 13f + i * 1.7f));
                Sprite(new Vector2(at.x + Mathf.Cos(q) * d, at.y + Mathf.Sin(q) * d), 0.08f, 0.08f, Fade(i % 3 != 0 ? White : Lilac, tw * alpha), glow, Overhead + 0.156f);
            }
            // The core: a white point with short rays, beating.
            Sprite(at, R * 0.9f * beat, R * 0.9f * beat, Fade(Lilac, 0.6f * alpha), glow, Overhead + 0.157f);
            Sprite(at, R * 0.38f * beat, R * 0.38f * beat, Fade(White, alpha), glow, Overhead + 0.1572f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = Turn(i * 45f + 20f * Mathf.Sin(s * 3f)) * (R * (0.3f + 0.25f * Mathf.Abs(Mathf.Sin(s * 17f + i * 2.1f))));
                Vector2[] ray = GokuGraphics.Points(3);
                ray[0] = at - d; ray[1] = at; ray[2] = at + d;
                GokuGraphics.Line(ray, R * 0.04f, Fade(White, 0.6f * alpha), whiteGlow, Overhead + 0.1574f, Taper.Both);
            }
            // Lightning off the edge, redrawn 20 times a second.
            int step = Mathf.FloorToInt(s * 20f);
            for (int i = 0; i < T.Bolts; i++)
            {
                int kk = step * 7 + i;
                float q0 = Rand(kk + 2200) * Tau, len = R * (0.5f + 0.7f * Rand(kk + 2210)), cq = Mathf.Cos(q0), sq = Mathf.Sin(q0);
                Vector2[] pts = GokuGraphics.Points(7);
                for (int m = 0; m <= 6; m++)
                {
                    float v = m / 6f, d = R * 0.9f + len * v, off = m > 0 ? (Rand(kk * 11 + m + 2220) - 0.5f) * 0.35f * R * v : 0f;
                    pts[m] = new Vector2(at.x + cq * d - sq * off, at.y + sq * d + cq * off);
                }
                GokuGraphics.Line(pts, 0.1f, Fade(Violet, 0.7f * alpha), whiteGlow, Overhead + 0.158f, Taper.End);
                GokuGraphics.Line(pts, 0.035f, Fade(White, 0.9f * alpha), whiteGlow, Overhead + 0.1582f, Taper.End);
            }
        }

        /// <summary>A thing dissolving: specks of its colour turning purple, drawn toward <paramref name="pull"/> (the sphere's centre) and up.</summary>
        private static void Dissolve(Vector2 at, float size, float age, Color colour, Vector2 pull, int count)
        {
            if (age < 0f || age > T.Dissolve) return;
            float u = age / T.Dissolve;
            for (int i = 0; i < count; i++)
            {
                float ox = (Rand(i + 2300) - 0.5f) * size, oz = (Rand(i + 2310) - 0.5f) * size, k = Smooth(Mathf.Clamp01(u * (1.2f + Rand(i + 2320))));
                var q = new Vector2(at.x + ox + (pull.x - at.x - ox) * k * 0.8f, at.y + oz + (pull.y - at.y - oz) * k * 0.8f + k * 0.3f);
                Sprite(q, 0.12f * (1f - 0.5f * u), 0.12f * (1f - 0.5f * u), Fade(Color.Lerp(colour, Lilac, k), 1f - u), glow, Overhead + 0.14f + i * 0.0001f);
            }
        }

        /// <summary>
        /// The white flash on a pawn the sphere touches: its body and head as two white ovals over it, the lab stand-in's
        /// shape (fitted to a real standing pawn in game, PawnFit). <paramref name="lie"/> for a pawn going down.
        /// </summary>
        private static void Flash(Vector2 pos, float alpha, bool lie)
        {
            if (alpha <= 0f) return;
            float[,] parts = lie ? Lying : Standing;
            for (int k = 0; k < 2; k++)
            {
                Vector2 c = lie ? new Vector2(pos.x + parts[k, 0], pos.y + parts[k, 1]) : PawnFit.At(pos, parts[k, 0], parts[k, 1]);
                float body = lie ? 1f : PawnFit.Body;
                DrawMesh(disc, c, PawnLayer + 0.005f + k * 0.0005f, parts[k, 2] * body, parts[k, 3] * body, 0f, Fade(White, alpha), solid);
            }
        }

        /// <summary>
        /// A side-row pawn lying after the hit: the cut where the part facing the lane was erased, on that edge of the
        /// body. White for 0.8 s, lilac by 1.6 s, out by <see cref="HollowPurple.CutGlow"/>. <paramref name="side"/> is +1
        /// when the lane is north of him.
        /// </summary>
        private static void CutGlow(Vector2 pos, float side, float age)
        {
            float u = Mathf.Clamp01(age / T.CutGlow);
            if (u >= 1f) return;
            Color hot = age < 0.8f ? White : Color.Lerp(Lilac, Violet, Mathf.Clamp01((age - 1.6f) / 0.9f));
            Color lit = age < 0.8f ? White : Color.Lerp(White, Lilac, Mathf.Clamp01((age - 0.8f) / 0.8f));
            float z = pos.y + 0.12f + side * 0.2f;
            Sprite(new Vector2(pos.x - 0.01f, z), 0.5f, 0.26f, Fade(Lilac, 0.7f * (1f - u)), glow, PawnLayer + 0.009f);
            Vector2[] cut = GokuGraphics.Points(2);
            cut[0] = new Vector2(pos.x - 0.17f, z); cut[1] = new Vector2(pos.x + 0.15f, z);
            GokuGraphics.Line(cut, 0.11f - 0.04f * u, Fade(age < 1.6f ? lit : hot, 0.95f * (1f - u * u)), whiteGlow, PawnLayer + 0.01f, Taper.Both);
        }

        /// <summary>
        /// The trench mark: one mesh, a half circle of radius R behind the start and a straight band to the end, so the
        /// rounded start is not drawn twice. Written relative to the start, drawn there at scale 1, rebuilt each frame.
        /// </summary>
        private sealed class Capsule
        {
            private const int Arc = 12;
            public readonly Mesh mesh;
            private readonly Vector3[] vertices = new Vector3[Arc + 4];
            private readonly int[] triangles = new int[(Arc + 3) * 3];

            public Capsule(string name) => mesh = new Mesh { name = name };

            /// <summary><paramref name="dir"/> is the path's angle in radians.</summary>
            public void Build(Vector2 from, Vector2 to, float radius, float dir)
            {
                vertices[0] = Vector3.zero;
                for (int i = 0; i <= Arc; i++)
                {
                    float q = dir + Mathf.PI / 2f + Mathf.PI * i / Arc;
                    vertices[1 + i] = new Vector3(Mathf.Cos(q) * radius, 0f, Mathf.Sin(q) * radius);
                }
                Vector2 d = to - from, n = new Vector2(-Mathf.Sin(dir), Mathf.Cos(dir)) * radius;
                vertices[Arc + 2] = new Vector3(d.x - n.x, 0f, d.y - n.y);
                vertices[Arc + 3] = new Vector3(d.x + n.x, 0f, d.y + n.y);
                int w = 0;
                for (int i = 1; i <= Arc; i++) Triangle(ref w, 0, i, i + 1);
                Triangle(ref w, 0, Arc + 1, Arc + 2);
                Triangle(ref w, 0, Arc + 2, Arc + 3);
                Triangle(ref w, 0, Arc + 3, 1);
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
            }

            // Clockwise on screen with north up, so the face survives backface culling.
            private void Triangle(ref int w, int p, int q, int r)
            {
                Vector3 a = vertices[p], b = vertices[q], c = vertices[r];
                bool back = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x) > 0f;
                triangles[w++] = p;
                triangles[w++] = back ? r : q;
                triangles[w++] = back ? q : r;
            }
        }
    }
}
