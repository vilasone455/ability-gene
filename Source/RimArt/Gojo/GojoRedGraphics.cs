using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using R = RimArt.GojoRed;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>A pawn inside the burst radius, pushed straight away from the burst point.</summary>
    public struct GojoRedPushed
    {
        /// <summary>Where it stood at the burst, and where it is now (the game's live point in an ability).</summary>
        public Vector2 From, Now;
    }

    /// <summary>
    /// One Red as the picture needs it. Points are ground points on the map: in the previews where the lab
    /// draws its stand-ins, in game a pawn's DrawPos (drawn between PawnFit.Begin and End so heights on a
    /// pawn are fitted to a real one).
    /// </summary>
    public struct GojoRedShot
    {
        /// <summary>Gojo's point, and Red's direction in degrees, 0 east, 90 north.</summary>
        public Vector2 Gojo;
        public float Aim;
        /// <summary>Cells from Gojo to where Red bursts: the first pawn or thing on its line, or the target cell.</summary>
        public float Dist;
        /// <summary>Seconds at the finger; Red's speed in cells/s (balance, from the ability's XML).</summary>
        public float Charge, Speed;
        /// <summary>The burst radius (the ring on the floor), the red wash's radius, cells the others are pushed.</summary>
        public float BurstRadius, WashRadius, PushCells;
        /// <summary>
        /// Something was hit and is thrown down Red's line. False: Red burst at an empty cell, which is outlined
        /// until the burst, and the shock front only runs the burst radius.
        /// </summary>
        public bool Thrown;
        /// <summary>
        /// The thrown body now: its ground point, its height in cells, and whether it lies (in the air and
        /// skidding, drawn with the lying red edge) or stands.
        /// </summary>
        public Vector2 Body;
        public float BodyHeight;
        public bool Lying;
        /// <summary>Seconds from the burst until the thrown body stops, and cells from the burst point to where it stops.</summary>
        public float StopAge, StopCells;
        /// <summary>A wall stopped it: its face is <see cref="WallFace"/> cells past the burst point; the body struck it <see cref="SlamHeight"/> cells up.</summary>
        public bool HitsWall;
        public float WallFace, SlamHeight;
        /// <summary>In the open: it first touched down <see cref="TouchAge"/> s after the burst, <see cref="TouchCells"/> past the burst point (drag marks from there, landing dust there and where it stops).</summary>
        public float TouchAge, TouchCells;
        /// <summary>Pawns inside the burst radius, the first <see cref="PushedCount"/> of <see cref="Pushed"/>.</summary>
        public GojoRedPushed[] Pushed;
        public int PushedCount;
        /// <summary>Gojo's sleeve and skin for the drawn arm.</summary>
        public Color Sleeve, Skin;
    }

    /// <summary>
    /// Draws Reversal: Red, the port of the lab's gojo-red-v2.js. The charge: Gojo's arm comes up and points,
    /// a red swirl of 4 arms turns at the fingertip, 2 ribbon arcs sweep round him, red specks are drawn in,
    /// the orb grows with lilac needles and thin red lines crossing it, a white ring for the last 0.2 s, the
    /// core going white-hot, the floor lit red under it. The fire: a flash and a red ring at the finger.
    /// The flight: a shaded red ball in a corona with 2 crescents turning round it, a dark-red and red tail,
    /// 8 dark-red ink streaks redrawn 24 times a second, 4 red speed lines, specks shed, the floor lit under
    /// it, and a wake of dust pushed out sideways from its path. The burst: the ground washed red, a white
    /// flash, the burst ring on the floor at the true radius, the shock front running down Red's line, 16
    /// sparks, dust, 6 rocks thrown forward that stay, a scorch that stays; the thrown body's red edge and
    /// red and black speed lines; drag marks and landing dust in the open; the pushed pawns' red edges and
    /// dust trails. On a wall: a red flash on the face, a dent and 5 cracks that stay, a dust cloud, 12
    /// chips thrown back that stay.
    ///
    /// Flat shapes and level circles only, so it turns with the aim and there is no per-facing method.
    ///
    /// Not drawn (the sketch's stand-ins): Gojo's body and his red tint, the raiders' bodies, shadows and red
    /// tints, the thrown raider lying and his squash on the wall face (a real pawn cannot be squashed), the
    /// wall cells and their shake.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GojoRedGraphics
    {
        internal static readonly Color Red = new Color(1f, 0.1f, 0.14f), RedDeep = new Color(0.42f, 0f, 0.05f),
            HotPink = new Color(1f, 0.78f, 0.82f), Lilac = new Color(0.98f, 0.74f, 1f);
        private static readonly Color Ink2 = new Color(0.3f, 0f, 0.04f), WakeDust = new Color(0.7f, 0.64f, 0.56f),
            Scorch = new Color(0.22f, 0.03f, 0.04f), WallDust = new Color(0.74f, 0.72f, 0.69f), White = new Color(1f, 1f, 1f);
        private static readonly float BuildingLayer = AltitudeLayer.Building.AltitudeFor();
        private const float Tau = 6.2831855f;
        private static readonly Vector2[] Corners = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };

        // The frame's shot as locals for Place.
        private static Vector2 gojo, dir, side;
        private static float chestUp;

        /// <summary>A point <paramref name="along"/> cells down Red's line from Gojo, <paramref name="across"/> to its left, <paramref name="up"/> high.</summary>
        private static Vector2 Place(float along, float across = 0f, float up = 0f) =>
            gojo + dir * along + side * across + new Vector2(0f, up * GokuGraphics.Lift);

        /// <summary>Everything from the arm coming up to the tail. <paramref name="s"/> is the picture's clock, 0 when the arm starts up.</summary>
        public static void Draw(in GojoRedShot shot, float s, Map map)
        {
            float fire = R.Fire(shot.Charge), arrive = R.Arrive(shot.Charge, shot.Dist, shot.Speed);
            float fly = shot.Thrown ? shot.StopAge : 0f;
            if (s < 0f || s >= R.End(arrive, fly) || !Shown(shot.Gojo, map)) return;
            Begin(shot.Gojo);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            gojo = shot.Gojo;
            dir = Turn(shot.Aim);
            side = new Vector2(-dir.y, dir.x);
            float chest = GokuGraphics.ChestOn;
            chestUp = chest / GokuGraphics.Lift;
            float a = shot.Aim * Mathf.Deg2Rad, dist = shot.Dist, age = s - arrive;
            bool burst = age >= 0f, open = shot.Thrown && !shot.HitsWall;
            Vector2 B = Place(dist, 0f, chestUp), Bf = Place(dist), F = Place(R.Tip, 0f, chestUp);
            float charge = R.Charged(s, shot.Charge);
            bool charging = s >= R.Start && s < fire, flying = s >= fire && s < arrive;
            float d = flying ? R.Tip + (s - fire) * shot.Speed : R.Tip;
            // The thrown body: cells travelled from the burst point and its height; the drawing follows its
            // side offset from Red's line too, so a real pawn a little off the line keeps its lines and marks.
            float gone = shot.Thrown ? Vector2.Dot(shot.Body - Bf, dir) : 0f, lateral = shot.Thrown ? Vector2.Dot(shot.Body - Bf, side) : 0f;
            float h = shot.Thrown ? shot.BodyHeight : 0f;
            float slamAge = shot.Thrown && shot.HitsWall && burst && age >= fly ? age - fly : -1f, face = dist + shot.WallFace, slamUp = shot.SlamHeight;
            // The shock front runs to the wall face, the full throw, or only the burst radius when nothing was hit.
            float frontLen = !shot.Thrown ? shot.BurstRadius : shot.HitsWall ? Mathf.Max(0.5f, face - 0.1f - dist - 0.2f) : shot.StopCells;
            float frontTime = frontLen / R.FrontSpeed;

            // --- floor: red light under the orb, the target cell, the scorch and drag marks that stay ---
            if (charging) Sprite(Place(R.Tip), 1.5f * charge, 1.1f * charge, Fade(Red, 0.3f * charge), glow, Floor + 0.03f);
            if (flying)
            {
                Vector2 under = Place(d);
                Sprite(under, 1.5f, 1.1f, Fade(RedDeep, 0.25f), soft, Floor + 0.029f);
                Sprite(under, 1.7f, 1.2f, Fade(Red, 0.3f), glow, Floor + 0.03f);
                Vector2[] path = GokuGraphics.Points(2);
                path[0] = under;
                path[1] = Place(Mathf.Max(R.Tip, d - 3.5f));
                GokuGraphics.Line(path, 0.5f, Fade(Red, 0.14f), whiteGlow, Floor + 0.025f);
            }
            if (!shot.Thrown && s < arrive + 0.1f)
            {
                float k = Smooth(s / 0.2f) * (1f - Smooth((s - arrive) / 0.1f));
                var c = new Vector2(Mathf.Floor(Bf.x) + 0.5f, Mathf.Floor(Bf.y) + 0.5f);
                for (int i = 0; i < 4; i++)
                {
                    Vector2[] edge = GokuGraphics.Points(2);
                    edge[0] = c + Corners[i] * 0.5f;
                    edge[1] = c + Corners[(i + 1) % 4] * 0.5f;
                    GokuGraphics.Line(edge, 0.04f, Fade(Red, 0.6f * k), whiteGlow, Floor + 0.02f + i * 0.0001f, Taper.None);
                }
            }
            if (burst)
            {
                float g = Smooth(age / 0.1f);
                Sprite(Bf, 1.5f * g, 1.1f * g, Fade(Scorch, 0.4f), soft, Floor + 0.01f);
                Sprite(Bf, 0.7f * g, 0.5f * g, Fade(Scorch, 0.55f), soft, Floor + 0.011f);
                // Drag marks from the first touchdown to where he stops skidding.
                if (open && gone > shot.TouchCells)
                    for (int i = 0; i < 2; i++)
                    {
                        float x = lateral + (i == 0 ? -0.12f : 0.12f);
                        Vector2[] mark = GokuGraphics.Points(2);
                        mark[0] = Place(dist + shot.TouchCells, x);
                        mark[1] = Place(dist + gone, x);
                        GokuGraphics.Line(mark, 0.05f, Fade(Ink, 0.3f), solid, Floor + 0.012f + i * 0.0001f, Taper.None);
                    }
            }

            // --- the wall: a dent and cracks that stay on its face where the body struck ---
            if (slamAge >= 0f)
            {
                float grow = Smooth(slamAge / 0.06f), up = slamUp + chestUp * 0.6f;
                Vector2 dent = Place(face + 0.14f, lateral, up);
                Sprite(dent, 0.45f, 0.65f, Fade(Ink, 0.5f * grow), soft, BuildingLayer + 0.004f, -shot.Aim);
                Sprite(dent, 0.2f, 0.32f, Fade(Ink, 0.6f * grow), soft, BuildingLayer + 0.0045f, -shot.Aim);
                for (int i = 0; i < R.Cracks; i++)
                {
                    float th = (-75f + i * 37.5f + (Rand(i + 3) - 0.5f) * 20f) * Mathf.Deg2Rad, len = (0.4f + 0.3f * Rand(i + 8)) * Smooth(slamAge / 0.08f);
                    float c = Mathf.Cos(th), n = Mathf.Sin(th);
                    Vector2[] pts = GokuGraphics.Points(8);
                    for (int k = 0; k <= 7; k++)
                    {
                        float dd = len * k / 7f, off = k > 0 ? (Rand(i * 11 + k) - 0.5f) * 0.14f : 0f;
                        pts[k] = Place(face + 0.02f + c * dd - n * off, lateral + n * dd + c * off, up);
                    }
                    GokuGraphics.Line(pts, 0.045f, Fade(Ink, 0.8f), solid, BuildingLayer + 0.005f + i * 0.0001f);
                }
            }

            // --- rocks thrown forward by the burst: they land and stay ---
            if (burst)
                for (int i = 0; i < R.Rocks; i++)
                {
                    float q = a + (Rand(i + 400) - 0.5f) * 80f * Mathf.Deg2Rad, reach = 2f + 2f * Rand(i + 410), T = 0.35f + 0.15f * Rand(i + 420), u = Mathf.Clamp01(age / T);
                    float r0 = 0.2f + reach * (1f - (1f - u) * (1f - u)), hh = 0.6f * (0.6f + 0.4f * Rand(i + 430)) * Mathf.Max(0f, Mathf.Sin(Mathf.PI * u));
                    Vector2 ground = Bf + new Vector2(Mathf.Cos(q), Mathf.Sin(q)) * r0;
                    if (u < 1f) Sprite(ground + sun * hh, 0.18f, 0.12f, Fade(Ink, 0.35f), soft, Floor + 0.05f + i * 0.0001f);
                    PaperBombGraphics.Rock(new Vector2(ground.x, ground.y + hh * GokuGraphics.Lift), 0.14f + 0.08f * Rand(i + 440), Rand(i + 450) * 360f + age * 600f * (1f - u),
                        1f, 2 * (i % 3) + 1, (u < 1f ? Overhead + 0.05f : Floor + 0.06f) + i * 0.001f);
                }

            // --- the arm, and the red edges round the thrown and pushed bodies ---
            GojoGraphics.PointingArm(shot.Gojo, shot.Aim, R.ArmOut(s, arrive), chest, shot.Sleeve, shot.Skin);
            if (shot.Thrown && burst)
            {
                float edge = 1f - Mathf.Clamp01(age / 0.4f);
                var lifted = new Vector2(shot.Body.x, shot.Body.y + h * GokuGraphics.Lift);
                if (shot.Lying) FlungEdge(lifted, shot.Aim, edge, 18f * Mathf.Sin(age * 22f) * (h > 0.05f ? 1f : 0f));
                // Nothing while he is squashed on the wall face; standing again as he slides down it.
                else if (slamAge < 0f || slamAge >= R.Squash) RedEdge(lifted, edge);
            }
            if (burst)
                for (int n = 0; n < shot.PushedCount; n++) RedEdge(shot.Pushed[n].Now, 1f - Mathf.Clamp01(age / 0.2f));

            // --- the charge: ribbon arcs round Gojo, the swirl at the finger, the orb ---
            float arcFade = s < fire ? Smooth(charge / 0.25f) : 1f - Mathf.Clamp01((s - fire) / 0.15f);
            if (s >= R.Start && arcFade > 0f)
            {
                Vector2 C = Place(0f, 0f, chestUp);
                float sweep = R.ArcSweep * Mathf.Deg2Rad * Smooth(Mathf.Min(1f, charge));
                for (int i = 0; i < R.Arcs.GetLength(0); i++)
                    Ribbon(C, R.Arcs[i, 0], R.Arcs[i, 1] * Mathf.Deg2Rad - sweep, 2.1f, arcFade * (0.75f + 0.25f * Mathf.Sin(s * 17f + i)), i);
            }
            if (charging)
            {
                float u = charge, env = Smooth(u / 0.25f);
                Swirl(F, R.SwirlFrom + (R.SwirlTo - R.SwirlFrom) * Smooth(u), -s * R.SwirlSpin * Mathf.Deg2Rad, env);
                // Red specks drawn in along short spirals.
                for (int i = 0; i < 10; i++)
                {
                    float ph = s / 0.32f + Rand(i + 200);
                    ph -= Mathf.Floor(ph);
                    float rr = 0.15f + (1f - ph) * 1.2f, q = Rand(i + 210) * Tau + ph * 1.4f, q2 = q - 0.25f, rr2 = rr + 0.16f;
                    Streak(F + new Vector2(Mathf.Cos(q), Mathf.Sin(q)) * rr, F + new Vector2(Mathf.Cos(q2), Mathf.Sin(q2)) * rr2, 0.035f,
                        Fade(i % 2 == 1 ? HotPink : Red, env * Mathf.Max(0f, Mathf.Sin(ph * Mathf.PI))), whiteGlow, Overhead + 0.098f + i * 0.00005f, 3);
                }
                float r = R.OrbRadius * Smooth(Mathf.Clamp01(u / 0.6f)), hot = Mathf.Clamp01((u - 0.75f) / 0.25f), ringK = Mathf.Clamp01((s - (fire - 0.2f)) / 0.08f);
                CrossLines(F, 4, 0.9f + 0.9f * u, 0.025f, 0.75f * Mathf.Clamp01((u - 0.3f) / 0.3f), s * 0.5f, 300);
                if (ringK > 0f)
                {
                    Sprite(F, r * 5.4f, r * 5.4f, Fade(Red, 0.3f * ringK), glow, Overhead + 0.103f);
                    PaperBombGraphics.RingAt(F, r * 2.2f, Fade(White, 0.6f * ringK), Overhead + 0.104f, false, whiteGlow);
                }
                RedOrb(F, r, s, hot, env);
            }

            // --- fire: the flash at the finger ---
            if (s >= fire && s < fire + R.Flash)
            {
                float f = (s - fire) / R.Flash, size = 0.4f + 1.1f * (1f - f);
                Sprite(F, size, size, Fade(HotPink, 0.9f * (1f - f)), glow, Overhead + 0.2f);
                PaperBombGraphics.RingAt(F, 0.2f + 0.9f * Smooth(f), Fade(Red, 0.8f * (1f - f)), Overhead + 0.19f, true, whiteGlow);
            }

            // --- flight: the ball, its tail, ink streaks, speed lines ---
            if (flying)
            {
                Vector2 at = Place(d, 0f, chestUp);
                Tail(at, d, 2.4f, 0.6f, Fade(RedDeep, 0.45f), solid, Overhead + 0.092f);
                Tail(at, d, 2.2f, 0.7f, Fade(Red, 0.25f), whiteGlow, Overhead + 0.095f);
                Tail(at, d, 1.6f, 0.3f, Fade(Red, 0.6f), whiteGlow, Overhead + 0.096f);
                Tail(at, d, 1f, 0.08f, Fade(HotPink, 0.9f), whiteGlow, Overhead + 0.097f);
                // Dark-red ink streaks (the anime's smear), redrawn at new places 24 times a second.
                int step = Mathf.FloorToInt(s * 24f);
                for (int i = 0; i < R.InkStreaks; i++)
                {
                    int k = step * 13 + i;
                    float across = (Rand(k + 500) - 0.5f) * 1.1f, len = 0.9f + 1.8f * Rand(k + 510), lag = 0.15f + 0.5f * Rand(k + 520);
                    float head = d - lag, back = Mathf.Max(R.Tip, head - len);
                    if (head > back + 0.15f)
                        Streak(Place(back, across, chestUp), Place(head, across, chestUp), 0.03f + 0.03f * Rand(k + 530), Fade(Ink2, 0.75f), solid, Overhead + 0.093f + i * 0.00004f, 3);
                }
                for (int i = 0; i < 4; i++)
                {
                    float across = (i % 2 == 1 ? 1f : -1f) * (0.16f + 0.3f * Rand(i + 230)), len = 0.8f + 1.4f * Rand(i + 240), lag = 0.2f + 0.6f * Rand(i + 250);
                    float flick = 0.6f + 0.4f * Mathf.Sin(s * 31f + i * 2.3f), head = d - lag, back = Mathf.Max(R.Tip, head - len);
                    if (head > back + 0.1f)
                        Streak(Place(back, across, chestUp), Place(head, across, chestUp), 0.035f, Fade(Red, 0.7f * flick), whiteGlow, Overhead + 0.094f + i * 0.00005f, 3);
                }
                FlightOrb(at, R.FlightRadius, s, sun);
            }
            // The wake: the air Red pushes aside throws dust out from its path, one pair of puffs and two flicked
            // specks every WakeStep cells, each moving out WakeOut cells over WakeLife.
            for (int i = 0; ; i++)
            {
                float along = 1.3f + i * R.WakeStep;
                if (along > dist - 0.2f) break;
                float born = fire + (along - R.Tip) / shot.Speed, u = (s - born) / R.WakeLife;
                if (u < 0f || u >= 1f) continue;
                float grow = 1f - (1f - u) * (1f - u), rise = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                for (int j = 0; j < 2; j++)
                {
                    int sign = j == 0 ? -1 : 1;
                    float outward = sign * (0.2f + R.WakeOut * grow), back = along - 0.25f * u, size = 0.25f + 0.35f * u;
                    Sprite(Place(back, outward, 0.05f * u), size, size * 0.8f, Fade(WakeDust, 0.55f * rise * (0.7f + 0.3f * Rand(i * 2 + sign + 600))), soft,
                        Floor + 0.04f + (i * 2 + j) * 0.00001f);
                    float fl = 0.15f + R.WakeOut * 1.3f * grow, hh = 0.25f * rise;
                    Streak(Place(back + 0.05f, sign * (fl - 0.12f), hh), Place(back - 0.05f, sign * fl, hh), 0.03f, Fade(Ink, 0.6f * (1f - u)), solid,
                        Floor + 0.045f + (i * 2 + j) * 0.00001f, 3);
                }
            }
            // Red specks shed behind the ball.
            for (int i = 0; i < 12; i++)
            {
                float born = fire + (i + 0.5f) / 12f * (arrive - fire), sAge = s - born;
                if (sAge < 0f || sAge > 0.3f) continue;
                float u = sAge / 0.3f, along = R.Tip + (born - fire) * shot.Speed - sAge * 2.5f;
                float across = (Rand(i + 260) - 0.5f) * (0.3f + 2.2f * u), up = chestUp + (Rand(i + 270) - 0.5f) * 0.5f * u;
                Streak(Place(along - 0.18f, across * 1.05f, up), Place(along, across, up), 0.04f, Fade(i % 3 != 0 ? Red : HotPink, 1f - u), whiteGlow,
                    Overhead + 0.0935f + i * 0.00002f, 3);
            }

            if (burst) Burst(shot, age, B, Bf, a, gone, lateral, h, fly, face, frontLen, frontTime);
            if (slamAge >= 0f) Slam(slamAge, face, lateral, slamUp, sun);
        }

        /// <summary>The burst: wash, flash, ring, shock front, sparks, dust, the thrown body's speed lines, trails and landing dust.</summary>
        private static void Burst(in GojoRedShot shot, float age, Vector2 B, Vector2 Bf, float a, float gone, float lateral, float h, float fly, float face,
            float frontLen, float frontTime)
        {
            float dist = shot.Dist;
            // The ground washed red (the anime's red frames).
            if (age < R.WashTime)
            {
                float w = (1f - Smooth(age / R.WashTime)) * Mathf.Clamp01(0.5f + age / 0.04f), size = shot.WashRadius * 2f;
                Sprite(B, size * 1.3f, size * 1.3f, Fade(RedDeep, 0.55f * w), soft, Overhead + 0.001f);
                Sprite(B, size * 0.8f, size * 0.8f, Fade(RedDeep, 0.35f * w), soft, Overhead + 0.0015f);
                Sprite(B, size * 1.4f, size * 1.4f, Fade(Red, 0.6f * w), glow, Overhead + 0.002f);
                Sprite(B, size * 0.7f, size * 0.7f, Fade(Red, 0.45f * w), glow, Overhead + 0.0025f);
            }
            if (age < 0.16f)
            {
                float f = age / 0.16f, a1 = 0.35f + 0.9f * (1f - f), a2 = 0.8f + 2.2f * (1f - f);
                Sprite(B, a2, a2, Fade(HotPink, 0.6f * (1f - f)), glow, Overhead + 0.199f);
                Sprite(B, a1, a1, Fade(White, 0.85f * (1f - f)), glow, Overhead + 0.2f);
            }
            // The burst radius on the floor, faint: the ring opens to it and fades there.
            float opening = 1f - Mathf.Clamp01(age / R.RingTime), rr = shot.BurstRadius * (1f - opening * opening);
            float ra = age < R.RingTime ? 1f : 1f - Smooth((age - R.RingTime) / R.RingHold);
            if (ra > 0f) PaperBombGraphics.RingAt(Bf, rr, Fade(Red, 0.45f * ra), Floor + 0.03f, false, whiteGlow);
            // The shock front: a red crescent across Red's line running ahead of the body, and a fainter echo behind it.
            if (age < frontTime + R.FrontFade)
            {
                float u = Mathf.Clamp01(age / frontTime), k = age < frontTime ? 1f - 0.35f * u : 0.65f * (1f - (age - frontTime) / R.FrontFade);
                float radius = R.FrontWide / 2f / Mathf.Sin(R.FrontHalf * Mathf.Deg2Rad) * (0.6f + 0.4f * u), lead = dist + 0.2f + frontLen * u;
                for (int j = 0; j < 2; j++)
                {
                    float back = j == 0 ? 0f : 0.45f, share = j == 0 ? 1f : 0.45f;
                    Vector2 C = Place(lead - back - radius, 0f, 0.35f);
                    Vector2[] pts = GokuGraphics.Points(15);
                    for (int m = 0; m <= 14; m++)
                    {
                        float q = a + (m / 14f * 2f - 1f) * R.FrontHalf * Mathf.Deg2Rad;
                        pts[m] = C + new Vector2(Mathf.Cos(q), Mathf.Sin(q)) * radius;
                    }
                    GokuGraphics.Line(pts, 0.5f, Fade(Red, 0.3f * k * share), whiteGlow, Overhead + 0.14f + j * 0.0002f, Taper.Both);
                    GokuGraphics.Line(pts, 0.2f, Fade(Red, 0.7f * k * share), whiteGlow, Overhead + 0.141f + j * 0.0002f, Taper.Both);
                    GokuGraphics.Line(pts, 0.06f, Fade(HotPink, 0.9f * k * share), whiteGlow, Overhead + 0.142f + j * 0.0002f, Taper.Both);
                }
            }
            // The front hitting the wall: a red flash on the face.
            if (shot.Thrown && shot.HitsWall && age >= frontTime && age < frontTime + 0.15f)
            {
                float f = (age - frontTime) / 0.15f;
                Sprite(Place(face - 0.1f, 0f, 0.35f), 1.1f + 0.6f * f, 1.8f + 0.6f * f, Fade(Red, 0.7f * (1f - f)), glow, Overhead + 0.143f, -shot.Aim);
            }
            // Red sparks thrown forward along Red's line.
            if (age < 0.35f)
                for (int i = 0; i < R.Sparks; i++)
                {
                    float q = a + (Rand(i + 280) - 0.5f) * 100f * Mathf.Deg2Rad, u = Smooth(age / 0.35f);
                    float r0 = 0.25f + (2.2f + 1.8f * Rand(i + 290)) * u, len = (0.3f + 0.5f * Rand(i + 300)) * (1f - 0.5f * u);
                    var way = new Vector2(Mathf.Cos(q), Mathf.Sin(q));
                    Vector2 tip = B + way * r0;
                    Streak(tip - way * len, tip, 0.045f, Fade(i % 3 != 0 ? Red : HotPink, 1f - age / 0.35f), whiteGlow, Overhead + 0.15f + i * 0.00002f, 3);
                }
            // Dust: a small ring out to the burst radius, and a fan thrown forward.
            if (age < 0.7f)
            {
                for (int i = 0; i < 8; i++)
                {
                    float u = Mathf.Clamp01((age - Rand(i + 310) * 0.05f) / 0.55f), q = i * Tau / 8f + Rand(i + 320), r0 = 0.3f + shot.BurstRadius * 0.9f * Smooth(u);
                    Sprite(new Vector2(Bf.x + Mathf.Cos(q) * r0, Bf.y + Mathf.Sin(q) * r0 * 0.8f + u * 0.1f), 0.3f + 0.45f * u, 0.25f + 0.35f * u,
                        Fade(GokuGraphics.Dust, 0.4f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), soft, Floor + 0.04f);
                }
                for (int i = 0; i < 12; i++)
                {
                    float u = Mathf.Clamp01((age - Rand(i + 460) * 0.08f) / 0.62f), q = a + (Rand(i + 470) - 0.5f) * 80f * Mathf.Deg2Rad;
                    float r0 = 0.3f + (1.5f + 1.5f * Rand(i + 480)) * (1f - (1f - u) * (1f - u));
                    Sprite(new Vector2(Bf.x + Mathf.Cos(q) * r0, Bf.y + Mathf.Sin(q) * r0 + u * 0.15f), 0.3f + 0.6f * u, 0.25f + 0.5f * u,
                        Fade(GokuGraphics.Dust, 0.45f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), soft, Floor + 0.041f);
                }
            }
            // Speed lines behind the thrown body, red and black, at its height.
            if (shot.Thrown && age < fly + 0.05f && gone > 0.3f && (h > 0.02f || shot.HitsWall))
                for (int i = 0; i < 6; i++)
                {
                    float across = lateral + (i - 2.5f) * 0.12f + (Rand(i + 330) - 0.5f) * 0.08f, back = 0.8f + Rand(i + 340) * 1.4f, up = h + 0.2f;
                    bool black = i % 2 == 0;
                    Streak(Place(dist + Mathf.Max(0f, gone - back), across, up), Place(dist + gone - 0.3f, across, up), black ? 0.05f : 0.04f,
                        Fade(black ? Ink : Red, 0.75f), black ? solid : whiteGlow, Overhead + 0.09f + i * 0.0001f, 3);
                }
            // Pushed pawns: a dust trail from their feet along the push.
            for (int n = 0; n < shot.PushedCount; n++)
            {
                GojoRedPushed pushed = shot.Pushed[n];
                Vector2 away = pushed.From - Bf;
                away = away.sqrMagnitude > 1e-8f ? away.normalized : Turn(shot.Aim);
                float done = shot.PushCells > 0f ? Vector2.Distance(pushed.Now, pushed.From) / shot.PushCells : 0f;
                for (int k = 0; k < 6; k++)
                {
                    float at = k / 5f, u = Mathf.Clamp01((age - R.PushedAt(at)) / 0.5f);
                    if (done + 1e-4f < at || u >= 1f) continue;
                    Vector2 p = pushed.From + away * (shot.PushCells * at);
                    Sprite(new Vector2(p.x, p.y + u * 0.1f), 0.25f + 0.3f * u, 0.2f + 0.25f * u, Fade(GokuGraphics.Dust, 0.45f * (1f - u)), soft,
                        Floor + 0.042f + (n * 6 + k) * 0.00001f);
                }
            }
            // Landing in the open: dust where he first touches down and where he stops skidding.
            if (shot.Thrown && !shot.HitsWall)
                for (int j = 0; j < 2; j++)
                {
                    float when = j == 0 ? shot.TouchAge : shot.StopAge, at = j == 0 ? shot.TouchCells : shot.StopCells, u = Mathf.Clamp01((age - when) / 0.6f);
                    if (u <= 0f || u >= 1f) continue;
                    Vector2 c = Place(dist + at, lateral);
                    for (int k = 0; k < 6; k++)
                    {
                        float q = k * Tau / 6f + Rand(k + 360 + j * 10), r0 = 0.2f + 0.5f * u;
                        Sprite(new Vector2(c.x + Mathf.Cos(q) * r0, c.y + Mathf.Sin(q) * r0 * 0.7f), 0.3f + 0.35f * u, 0.25f + 0.3f * u,
                            Fade(GokuGraphics.Dust, 0.4f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), soft, Floor + 0.04f + 0.0002f + j * 0.0001f);
                    }
                }
        }

        /// <summary>The wall slam: a big dust cloud along the face, chips thrown back that land and stay.</summary>
        private static void Slam(float slamAge, float face, float lateral, float slamUp, Vector2 sun)
        {
            for (int i = 0; i < 18; i++)
            {
                float sign = i % 2 == 1 ? 1f : -1f, u = Mathf.Clamp01((slamAge - Rand(i + 30) * 0.06f) / 0.9f);
                if (u <= 0f || u >= 1f) continue;
                float across = lateral + sign * (0.15f + 1.9f * Rand(i + 40) * Smooth(u)), along = face - 0.25f - 0.5f * Rand(i + 50) - 0.3f * u, size = 0.45f + 1.05f * u;
                Sprite(Place(along, across, 0.25f * u), size, size * 0.8f, Fade(WallDust, 0.65f * Mathf.Sin(u * Mathf.PI)), soft, Overhead + 0.01f + i * 0.00002f);
            }
            for (int i = 0; i < 4; i++)
            {
                float u = Mathf.Clamp01((slamAge - 0.05f * i) / 1.4f), size = (1.3f + 0.8f * Rand(i + 370)) * (0.5f + 0.7f * Smooth(u));
                if (u <= 0f || u >= 1f) continue;
                Sprite(Place(face - 0.5f - 0.3f * Rand(i + 380), lateral + (Rand(i + 390) - 0.5f) * 1.4f, 0.3f + 0.5f * u), size, size * 0.85f,
                    Fade(WallDust, 0.6f * Mathf.Sin(u * Mathf.PI)), soft, Overhead + 0.011f + i * 0.00002f);
            }
            for (int i = 0; i < R.Chips; i++)
            {
                float T = 0.3f + 0.15f * Rand(i + 60), u = Mathf.Clamp01(slamAge / T), la = face - 0.6f - 1.3f * Rand(i + 80), lx = (Rand(i + 90) - 0.5f) * 2.2f;
                float along = (face - 0.1f) + (la - face + 0.1f) * u, across = lateral + lx * u;
                float hh = (slamUp + chestUp) * (1f - u) + 0.35f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                Vector2 ground = Place(along, across);
                if (u < 1f) Sprite(ground + sun * hh, 0.18f, 0.12f, Fade(Ink, 0.35f), soft, Floor + 0.051f + i * 0.00005f);
                PaperBombGraphics.Rock(Place(along, across, hh), 0.16f + 0.12f * Rand(i + 100), Rand(i + 110) * 360f + slamAge * 700f * (1f - u), 1f, 2 * (i % 3),
                    (u < 1f ? Overhead + 0.056f : Floor + 0.066f) + i * 0.001f);
            }
        }

        /// <summary>
        /// The orb at the finger: a red halo and a pale pink one, a darker rim, the red body, a core that goes
        /// white-hot (<paramref name="hot"/> 0 to 1), and lilac needle flares that flicker (<paramref name="flare"/> 0 to 1).
        /// </summary>
        private static void RedOrb(Vector2 at, float r, float s, float hot, float flare)
        {
            if (r <= 0.005f) return;
            float beat = 1f + 0.1f * Mathf.Sin(s * 40f);
            Sprite(at, r * 9f, r * 9f, Fade(Red, 0.35f), glow, Overhead + 0.1f);
            Sprite(at, r * 4.5f, r * 4.5f, Fade(HotPink, 0.45f), glow, Overhead + 0.101f);
            if (flare > 0f)
                for (int i = 0; i < 7; i++)
                {
                    float q = (i * 51.4f + Rand(i + 180) * 24f + s * 40f) * Mathf.Deg2Rad, flick = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(s * 19f + i * 2.7f));
                    float len = r * (1.4f + 2.6f * Rand(i + 190)) * flick;
                    var way = new Vector2(Mathf.Cos(q), Mathf.Sin(q));
                    Streak(at + way * (r * 0.9f), at + way * (r + len), r * 0.38f, Fade(Lilac, 0.8f * flare * flick), whiteGlow, Overhead + 0.106f + i * 0.00002f, 4);
                }
            DrawMesh(disc, at, Overhead + 0.11f, r * 1.18f, r * 1.18f, 0f, RedDeep, solid);
            DrawMesh(disc, at, Overhead + 0.111f, r, r, 0f, Red, solid);
            float core = r * (0.35f + 0.45f * hot) * beat;
            DrawMesh(disc, at, Overhead + 0.112f, core, core, 0f, Color.Lerp(HotPink, White, hot), solid);
            Sprite(at, core * 3.2f, core * 3.2f, Fade(White, 0.25f + 0.6f * hot), glow, Overhead + 0.113f);
        }

        /// <summary>
        /// Red in flight: a solid red ball shaded from the sun side (lit toward the light, dark rim away) in a red
        /// corona over a dark-red shade, a thin pale pressure ring, and white-pink crescents turning round it.
        /// </summary>
        private static void FlightOrb(Vector2 at, float r, float s, Vector2 sun)
        {
            float len = sun.magnitude;
            if (len == 0f) len = 1f;
            Vector2 light = -sun / len;
            float beat = 1f + 0.06f * Mathf.Sin(s * 45f);
            Sprite(at, r * 6f, r * 6f, Fade(RedDeep, 0.35f), soft, Overhead + 0.099f);
            Sprite(at, r * 8f * beat, r * 8f * beat, Fade(Red, 0.45f), glow, Overhead + 0.1f);
            PaperBombGraphics.RingAt(at, r * 2.4f + 0.03f * Mathf.Sin(s * 50f), Fade(HotPink, 0.18f), Overhead + 0.101f, false, whiteGlow);
            DrawMesh(disc, at, Overhead + 0.11f, r * 1.1f, r * 1.1f, 0f, Ink2, solid);
            DrawMesh(disc, at - light * (r * 0.1f), Overhead + 0.111f, r * 0.98f, r * 0.98f, 0f, RedDeep, solid);
            DrawMesh(disc, at + light * (r * 0.1f), Overhead + 0.112f, r * 0.82f, r * 0.82f, 0f, Red, solid);
            DrawMesh(disc, at + light * (r * 0.38f), Overhead + 0.113f, r * 0.34f, r * 0.3f, 0f, Fade(HotPink, 0.85f), solid);
            DrawMesh(disc, at + light * (r * 0.48f), Overhead + 0.114f, r * 0.13f, r * 0.12f, 0f, Fade(White, 0.9f), solid);
            Sprite(at, r * 1.6f, r * 1.6f, Fade(HotPink, 0.35f), glow, Overhead + 0.115f);
            for (int i = 0; i < R.Crescents; i++)
            {
                float a0 = -s * R.CrescentSpin * Mathf.Deg2Rad + i * Tau / R.Crescents, radius = r * 1.9f;
                Vector2[] pts = GokuGraphics.Points(9);
                for (int k = 0; k <= 8; k++)
                {
                    float q = a0 + k / 8f * 0.8f;
                    pts[k] = at + new Vector2(Mathf.Cos(q), Mathf.Sin(q)) * radius;
                }
                GokuGraphics.Line(pts, 0.1f, Fade(Red, 0.6f), whiteGlow, Overhead + 0.116f + i * 0.0002f, Taper.Both);
                GokuGraphics.Line(pts, 0.035f, Fade(White, 0.9f), whiteGlow, Overhead + 0.117f + i * 0.0002f, Taper.Both);
            }
        }

        /// <summary>The game's hold: a flat swirl of 4 arms curling out from the centre to <paramref name="radius"/>, turned by <paramref name="spin"/> radians.</summary>
        private static void Swirl(Vector2 at, float radius, float spin, float alpha)
        {
            if (radius <= 0.02f || alpha <= 0f) return;
            Sprite(at, radius * 3f, radius * 3f, Fade(Red, 0.35f * alpha), glow, Overhead + 0.085f);
            for (int i = 0; i < 4; i++)
            {
                float a0 = spin + i * Tau / 4f;
                Vector2[] pts = GokuGraphics.Points(13);
                for (int k = 0; k <= 12; k++)
                {
                    float v = k / 12f, rr = radius * (0.15f + 0.85f * v), q = a0 + v * 2.6f;
                    pts[k] = at + new Vector2(Mathf.Cos(q), Mathf.Sin(q)) * rr;
                }
                GokuGraphics.Line(pts, radius * 0.28f, Fade(Red, 0.55f * alpha), whiteGlow, Overhead + 0.086f + i * 0.0001f, Taper.Both);
                GokuGraphics.Line(pts, radius * 0.09f, Fade(HotPink, 0.85f * alpha), whiteGlow, Overhead + 0.087f + i * 0.0001f, Taper.Both);
            }
        }

        /// <summary>A ribbon arc round <paramref name="centre"/>: its head at angle <paramref name="head"/>, its tail <paramref name="span"/> radians behind.</summary>
        private static void Ribbon(Vector2 centre, float radius, float head, float span, float alpha, int n)
        {
            if (alpha <= 0f) return;
            Vector2[] pts = GokuGraphics.Points(17);
            for (int k = 0; k <= 16; k++)
            {
                float q = head + span * (1f - k / 16f);
                pts[k] = centre + new Vector2(Mathf.Cos(q), Mathf.Sin(q)) * radius;
            }
            GokuGraphics.Line(pts, 0.18f, Fade(Red, 0.45f * alpha), whiteGlow, Overhead + 0.08f + n * 0.0002f, Taper.Both);
            GokuGraphics.Line(pts, 0.055f, Fade(HotPink, 0.85f * alpha), whiteGlow, Overhead + 0.081f + n * 0.0002f, Taper.Both);
        }

        /// <summary>Thin straight red lines crossing through a point (the Toji frame); <paramref name="half"/> is each line's half length.</summary>
        private static void CrossLines(Vector2 at, int count, float half, float width, float alpha, float turn, int seed)
        {
            if (alpha <= 0f || half <= 0.01f) return;
            for (int i = 0; i < count; i++)
            {
                float q = (i * 180f / count + (Rand(i + seed) - 0.5f) * 30f) * Mathf.Deg2Rad + turn, l = half * (0.7f + 0.3f * Rand(i + seed + 7));
                Vector2 d = new Vector2(Mathf.Cos(q), Mathf.Sin(q)) * l;
                Streak(at - d, at + d, width, Fade(Red, alpha), whiteGlow, Overhead + 0.102f + i * 0.00002f, 8);
            }
        }

        /// <summary>A tail from the ball back <paramref name="length"/> cells down Red's line (not behind the finger), through its middle.</summary>
        private static void Tail(Vector2 at, float d, float length, float width, Color colour, Material material, float altitude)
        {
            float back = Mathf.Max(R.Tip, d - length);
            Vector2[] pts = GokuGraphics.Points(3);
            pts[0] = at;
            pts[1] = Place((d + back) / 2f, 0f, chestUp);
            pts[2] = Place(back, 0f, chestUp);
            GokuGraphics.Line(pts, width, colour, material, altitude);
        }

        /// <summary>
        /// A red edge round a standing pawn at <paramref name="pos"/> (its DrawPos, lifted if it is in the air),
        /// drawn just under the pawn layer: two ovals the size of the lab's stand-in body and head, fitted to a
        /// real pawn in game (PawnFit), and a glow. <paramref name="k"/> 0 to 1.
        /// </summary>
        private static void RedEdge(Vector2 pos, float k)
        {
            if (k <= 0f) return;
            float body = PawnFit.Body;
            DrawMesh(disc, new Vector2(pos.x, pos.y + PawnFit.Y(0.18f)), GokuGraphics.PawnLayer - 0.003f, 0.28f * body, 0.38f * body, 0f, Fade(Red, 0.85f * k), solid);
            DrawMesh(disc, new Vector2(pos.x, pos.y + PawnFit.Y(0.58f)), GokuGraphics.PawnLayer - 0.0031f, 0.22f * body, 0.23f * body, 0f, Fade(Red, 0.85f * k), solid);
            Sprite(new Vector2(pos.x, pos.y + PawnFit.Y(0.35f)), 1.1f * body, 1.3f * body, Fade(Red, 0.35f * k), glow, GokuGraphics.PawnLayer - 0.004f);
        }

        /// <summary>
        /// The red edge round the thrown body lying along the throw, head leading, at <paramref name="pos"/> (already
        /// lifted). <paramref name="wob"/> turns it a little (degrees) so it does not fly rigid. The lab's lying
        /// stand-in's shape: there is no measurement of a real lying pawn to fit it to.
        /// </summary>
        private static void FlungEdge(Vector2 pos, float degrees, float edge, float wob)
        {
            if (edge <= 0f) return;
            float rot = -degrees + wob;
            Vector2 head = pos + Turn(degrees) * 0.42f, body = new Vector2(pos.x, pos.y + 0.12f);
            DrawMesh(disc, body, GokuGraphics.PawnLayer - 0.003f, 0.38f, 0.26f, rot, Fade(Red, 0.85f * edge), solid);
            DrawMesh(disc, new Vector2(head.x, head.y + 0.02f), GokuGraphics.PawnLayer - 0.0031f, 0.22f, 0.23f, rot, Fade(Red, 0.85f * edge), solid);
            Sprite(body, 1.4f, 0.9f, Fade(Red, 0.35f * edge), glow, GokuGraphics.PawnLayer - 0.004f, rot);
        }
    }
}
