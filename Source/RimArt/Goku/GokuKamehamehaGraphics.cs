using UnityEngine;
using Verse;
using static RimArt.GokuGraphics;
using static RimArt.ThunderGodGraphics;
using G = RimArt.GokuTiming;
using T = RimArt.GokuKamehamehaTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Kamehameha. The channel: the lane and the end circle on the floor at their true size, a
    /// bar of light down the lane on each of the four beats, the lit floor stepping out, rings, dust,
    /// pebbles hanging in the air, threads of light running into the hands, the aura, and the ki ball
    /// at the rear hip growing beat by beat. "HA": the muzzle burst, a ring along the floor, dust blown
    /// out behind. The beam: four nested additive layers, a pale layer and a white core, soft glow,
    /// flow lines and rings along it, a bulb and bow arc at its head, sparks where it presses at its
    /// end, a burst on each pawn it reaches, dust and rocks thrown to both sides. Then the rear runs
    /// down the lane and the end blast: a flash, a low dome of level circles, cracks of light, rocks,
    /// a dust ring, soot on a wall that stopped the beam; a scorched trench that cools and smokes. The
    /// beam is a flat shape at chest height and the dome is level circles, so the whole turns with
    /// the aim and there is no per-facing method. The warp's vanish and arrival are Instant
    /// Transmission's, from <see cref="GokuGraphics"/>.
    ///
    /// Not drawn (the sketch's stand-ins): the pawns and their shadows, the ki tint on the caster and
    /// the white on the pawns that are hit, the downed pawns, and the wall itself.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GokuKamehamehaGraphics
    {
        private static readonly Color Grey = new Color(0.42f, 0.42f, 0.44f);
        private static readonly Mesh beamRing = SixPathsBurstGraphics.Band(0.9f, "Kamehameha ring");
        private const int MostEnemies = 8;
        private static readonly Vector2[] struck = new Vector2[MostEnemies];
        private static readonly float[] struckAt = new float[MostEnemies];

        /// <summary>
        /// The preview. <paramref name="centre"/> is the middle of the lane, as in the lab's sketch: the
        /// firing cell is half the length behind it.
        /// </summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, bool warp, bool wall, float seconds, Map map)
        {
            KamehamehaPlan plan = T.Plan(warp);
            var o = new Vector2(centre.x, centre.z);
            Vector2 from = o - toward * (T.ScriptLength / 2f);
            float stop = T.Stop(T.ScriptLength, wall);
            int n = 0;
            for (int i = 0; i < T.EnemyAlong.Length; i++)
            {
                if (T.EnemyAlong[i] >= T.ScriptLength) continue;
                Vector2 local = T.Enemy(i, seconds, plan, T.ScriptLength, T.ScriptWidth, stop, out bool inLane, out float hitAt);
                if (!inLane) continue;
                struck[n] = G.Place(from, toward, local.x, local.y);
                struckAt[n++] = hitAt;
            }
            Draw(new KamehamehaShot
            {
                From = from, Toward = toward, Home = warp ? G.Place(from, toward, T.WarpFrom.x, T.WarpFrom.y) : from, Seconds = seconds, Plan = plan,
                Length = T.ScriptLength, Width = T.ScriptWidth, Blast = T.ScriptBlast, BallSize = T.ScriptBallSize,
                Stop = stop, Walled = wall && T.WallAt < T.ScriptLength, WallAt = T.WallAt,
                Struck = struck, StruckAt = struckAt, StruckCount = n,
            }, map);
        }

        public static void Draw(in KamehamehaShot shot, Map map)
        {
            KamehamehaPlan t = shot.Plan;
            float s = shot.Seconds;
            if (s < 0f || s >= t.End || !Shown(shot.From, map)) return;
            Begin(shot.From);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            Vector2 from = shot.From, toward = shot.Toward, home = shot.Home;
            float aim = Mathf.Atan2(toward.y, toward.x), aimDegrees = aim * Mathf.Rad2Deg, ca = toward.x, sa = toward.y;
            float len = shot.Length, W = shot.Width, R = shot.Blast, stop = shot.Stop;
            Vector2 Place(float along, float across = 0f, float up = 0f) => G.Place(from, toward, along, across) + new Vector2(0f, up);
            Vector2 end = Place(stop);
            float fired = s - t.Fire, blasted = s - t.Blast;
            bool firing = fired >= 0f && s < t.Blast;
            float reach = stop * Mathf.Clamp01(fired / (T.HeadTime * stop / len)), rear = stop * Mathf.Clamp01((s - t.Release) / T.Depart);
            bool arrived = fired >= 0f && reach >= stop;

            // --- the floor: the lane and the end circle, the light under the beam, the trench after it ---
            float charge = Mathf.Clamp01((s - t.Cast) / t.Channel);
            int beat = Mathf.Min(T.Beats - 1, Mathf.FloorToInt(charge * T.Beats));
            float inBeat = charge * T.Beats - beat, beatAge = inBeat * t.Channel / T.Beats;
            bool last = beat == T.Beats - 1 && s < t.Fire;
            if (s >= t.Cast && s < t.Blast)
            {
                float show = Smooth((s - t.Cast) / 0.3f), pulse = s < t.Fire ? 0.35f + 0.4f * (1f - Mathf.Clamp01(inBeat / 0.5f)) : 0.3f + 0.3f * Mathf.Sin(s * 40f);
                if (s < t.Fire)
                {
                    for (int side = -1; side <= 1; side += 2)
                        Streak(Place(0.5f, side * W / 2f), Place(stop, side * W / 2f), 0.07f, Fade(KiSky, pulse * show), solid, Floor + 0.02f, 2);
                    Sprite(Place(stop / 2f), stop, W, Fade(KiDeep, 0.1f * show), glow, Floor + 0.006f, -aimDegrees);
                    // A bar of light runs down the lane on each beat.
                    float run = beatAge / T.LanePulse;
                    if (run < 1f)
                    {
                        float d = stop * run, fade = Mathf.Sin(Mathf.Min(1f, run * 1.1f) * Mathf.PI);
                        Streak(Place(d, -W / 2f), Place(d, W / 2f), 0.3f, Fade(KiIce, 0.9f * fade), whiteGlow, Floor + 0.025f, 4);
                        Sprite(Place(d), 2.4f, W * 1.3f, Fade(Ki, 0.45f * fade), glow, Floor + 0.024f, -aimDegrees);
                    }
                }
                PaperBombGraphics.RingAt(end, R, Fade(KiSky, pulse * show), Floor + 0.02f);                                   // the end blast, at its true radius
                DrawMesh(disc, end, Floor + 0.006f, R, R, 0f, Fade(KiDeep, (arrived ? 0.3f : 0.1f) * show), whiteGlow);
            }
            if (fired >= 0f)
            {
                float made = Mathf.Max(0f, reach), settle = 1f - 0.3f * Smooth(blasted / T.Tail), heat = 1f - Smooth((s - t.Release) / T.Cool);
                Sprite(Place(made / 2f), made + 1f, W * 0.95f, Fade(Ink, 0.6f * settle * Mathf.Clamp01(fired / 0.4f)), soft, Floor + 0.01f, -aimDegrees);
                // The trench's edges glow and cool.
                if (made > 1f)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2[] pts = Points(Mathf.FloorToInt(made - 0.5f) + 1);
                        for (int k = 0; k < pts.Length; k++)
                        {
                            float d = 0.5f + k;
                            pts[k] = Place(d, side * (W * 0.33f + 0.05f * Mathf.Sin(d * 1.3f + side)));
                        }
                        Line(pts, 0.1f, Fade(Color.Lerp(KiDeep, KiSky, heat), 0.15f + 0.4f * heat), whiteGlow, Floor + 0.03f, Taper.Both);
                    }
                if (firing) Sprite(Place((rear + reach) / 2f), reach - rear + 3f, W * 2.6f, Fade(KiDeep, 0.45f), glow, Floor + 0.012f, -aimDegrees);
            }
            if (blasted >= 0f)
            {
                float burnt = Smooth(blasted / 0.5f) * (1f - 0.3f * Smooth(blasted / T.Tail)), heat = 1f - Smooth(blasted / T.Cool);
                Sprite(end, R * 2.3f, R * 2.3f, Fade(Ink, 0.5f * burnt), soft, Floor + 0.011f);
                PaperBombGraphics.RingAt(end, R * 0.97f, Fade(Ink, 0.5f * burnt), Floor + 0.012f, true);
                for (int i = 0; i < T.BlastCracks; i++)
                {
                    float ang = i * Mathf.PI * 2f / T.BlastCracks + Rand(i + 3) * 0.4f, reachOut = R * (0.55f + 0.4f * Rand(i + 8));
                    Vector2 way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)), normal = new Vector2(-way.y, way.x);
                    Vector2[] pts = Points(7);
                    for (int k = 0; k <= 6; k++) pts[k] = end + way * (reachOut * k / 6f) + normal * (k > 0 ? (Rand(i * 13 + k) - 0.5f) * 0.4f : 0f);
                    Line(pts, 0.12f, Fade(Color.Lerp(KiDeep, White, heat), 0.3f + 0.65f * heat), whiteGlow, Floor + 0.031f);
                }
            }

            // --- soot on the wall cells the blast reached ---
            if (shot.Walled && blasted >= 0f)
                for (int k = -1; k <= 1; k++)
                    Sprite(Place(shot.WallAt, k), 1.3f, 1.3f, Fade(Ink, 0.55f * Smooth(blasted / 0.3f)), soft, Overhead + 0.001f);

            // --- the caster: the aura, and the warp's slices ---
            bool warp = t.Warp;
            float gone = warp ? Mathf.Clamp01((s - t.Go) / T.Vanish) : 0f;
            bool there = !warp || s >= t.Go + T.Vanish + T.Gap;
            float back = warp && there ? 1f - Mathf.Clamp01((s - t.Go - T.Vanish - T.Gap) / T.Vanish) : 0f;
            float slide = fired >= 0f ? T.Recoil * Smooth(fired / t.Hold) : 0f, lunge = 0.12f * Smooth(fired / 0.05f) * (1f - Smooth((fired - 0.05f) / 0.2f));
            Vector2 stand = there ? Place(-slide + lunge) : home;
            bool shown = !warp || s < t.Go + T.Vanish || there;
            float power = s < t.Fire ? Smooth(charge * 1.5f) * (last ? 1f + 0.6f * Smooth(inBeat / 0.3f) : 1f) : 1f - Smooth((s - t.Release) / 0.4f);
            if (shown)
            {
                if (s >= t.Cast && s < t.Blast) Aura(stand, s, power * (1f - Mathf.Max(gone, back)));
                Sliced(stand, there ? back : gone, hair: true);
            }
            if (warp)
            {
                Blink(home, s - t.Go - T.Vanish * 0.5f);
                Blink(from, s - t.Go - T.Vanish - T.Gap);
            }

            // --- the channel: the ball at the rear hip and everything round it ---
            float seen = 1f - Mathf.Max(gone, back);
            if (s >= t.Cast && s < t.Fire)
            {
                float grown = (beat + Smooth(inBeat / 0.25f)) / T.Beats, flare = last ? 1f + T.FinalFlare * Smooth(inBeat / 0.3f) : 1f;
                float size = shot.BallSize * grown * flare * (1f + 0.05f * Mathf.Sin(s * 47f));
                var hands = new Vector2(stand.x - ca * 0.24f + sa * 0.13f, stand.y - sa * 0.24f - ca * 0.13f + Chest - 0.06f);
                // The lit floor steps out one cell a beat, and a ring flashes on it as it does.
                float lit = beat + Smooth(inBeat / 0.2f);
                Sprite(stand, lit * 2.6f + 1f, (lit * 2.6f + 1f) * 0.85f, Fade(Ki, (0.22f + 0.06f * beat) * seen * (0.85f + 0.15f * Mathf.Sin(s * 31f))), glow, Floor + 0.007f);
                PaperBombGraphics.RingAt(stand, beat + 1f, Fade(KiSky, 0.8f * (1f - Mathf.Clamp01(beatAge / 0.35f)) * seen), Floor + 0.021f, false, whiteGlow);
                for (int n = 0; n < T.WindRings; n++)
                {
                    float v = ((s - t.Cast) / 0.5f + n / (float)T.WindRings) % 1f;
                    PaperBombGraphics.RingAt(stand, 0.3f + v * (1f + lit * 0.7f), Fade(KiIce, 0.5f * (1f - v) * grown * seen), Floor + 0.02f);
                }
                // Each beat sends one ring out from the ball.
                PaperBombGraphics.RingAt(hands, size / 2f + inBeat * 1.6f, Fade(KiIce, 0.9f * (1f - Mathf.Clamp01(inBeat / 0.45f)) * seen), Overhead + 0.09f, false, whiteGlow);
                for (int i = 0; i < 8; i++)
                {
                    float v = ((s - t.Cast) * 1.1f + Rand(i + 30)) % 1f, rad = 0.4f + v * (1f + lit * 0.6f), ang = (i * 45f + Rand(i + 60) * 30f + v * 40f) * Mathf.Deg2Rad;
                    Sprite(new Vector2(stand.x + Mathf.Cos(ang) * rad, stand.y + Mathf.Sin(ang) * rad * 0.85f + v * 0.1f), 0.35f + v * 0.4f, 0.28f + v * 0.3f,
                        Fade(Dust, 0.4f * grown * seen * Mathf.Sin(v * Mathf.PI)), soft, Floor + 0.04f);
                }
                // Pebbles lift off the ground round the caster and hang there; higher on the last beat.
                for (int i = 0; i < T.Pebbles; i++)
                {
                    float v = ((s - t.Cast) * (0.35f + 0.25f * Rand(i + 2)) + Rand(i)) % 1f, ang = i * 2.399f, rad = 0.55f + Rand(i + 7) * 1.4f, size0 = 0.05f + 0.05f * Rand(i + 9);
                    var ground = new Vector2(stand.x + Mathf.Cos(ang) * rad, stand.y + Mathf.Sin(ang) * rad * 0.8f);
                    float h = v * (0.7f + 0.8f * Rand(i + 4)) * grown * (last ? 1.6f : 1f), show = Mathf.Sin(v * Mathf.PI) * seen * Mathf.Clamp01(grown * 3f);
                    Sprite(ground + sun * h, size0 * 2.4f, size0 * 1.4f, Fade(Ink, 0.35f * show), soft, Floor + 0.05f);
                    PaperBombGraphics.Rock(new Vector2(ground.x, ground.y + h * Lift), size0 * 2.4f, i * 50f + s * 50f, show, i, Overhead + 0.005f);
                }
                for (int i = 0; i < T.Threads; i++)
                {
                    float v = ((s - t.Cast) * 1.5f + Rand(i)) % 1f, r0 = (1.2f + lit * 0.4f) * (1f - v) + size / 2f, r1 = r0 + 0.35f * (1f - v) + 0.05f;
                    Vector2 way = Turn(i * 30f + Rand(i + 9) * 40f);
                    Streak(hands + way * r0, hands + way * r1, 0.045f, Fade(KiIce, 0.85f * Mathf.Sin(v * Mathf.PI) * seen), whiteGlow, Overhead + 0.09f, 2);
                }
                KiBall(hands, size, s, seen, (0.5f + 0.5f * grown) * flare);
            }

            // --- HA: the muzzle burst, the ring along the floor, the dust blown out behind ---
            Vector2 muzzle = Place(0.45f - slide, 0f, Chest);
            if (fired >= 0f && fired < 0.16f)
            {
                float v = fired / 0.16f, f = 1f - v;
                Sprite(muzzle, 5.5f, 5.5f, Fade(Ki, 0.85f * f * f), glow, Overhead + 0.13f);
                Sprite(muzzle, 2.6f, 2.6f, Fade(White, f), glow, Overhead + 0.131f);
                PaperBombGraphics.RingAt(muzzle, 0.5f + v * 2.2f, Fade(KiSky, f), Overhead + 0.132f, false, whiteGlow);
                for (int i = 0; i < T.BurstLines; i++)
                {
                    Vector2 way = Turn(i * 360f / T.BurstLines + Rand(i + 70) * 14f);
                    float r0 = 0.5f + v * 1.4f, r1 = r0 + 0.8f + Rand(i + 90) * 0.8f;
                    Streak(muzzle + way * r0, muzzle + way * r1, 0.08f, Fade(White, f), whiteGlow, Overhead + 0.133f, 4);
                }
            }
            if (fired >= 0f && fired < 0.7f)
            {
                float v = fired / 0.7f;
                if (fired < 0.3f) PaperBombGraphics.RingAt(from, 0.4f + 2.6f * Smooth(fired / 0.3f), Fade(KiSky, 0.9f * (1f - fired / 0.3f)), Floor + 0.022f, false, whiteGlow);
                for (int i = 0; i < T.BackDust; i++)
                {
                    float spread = (Rand(i + 41) - 0.5f) * 1.2f, d = 0.4f + v * (1.5f + 2.5f * Rand(i + 42));
                    Vector2 at = Place(-Mathf.Cos(spread) * d, Mathf.Sin(spread) * d);
                    Sprite(new Vector2(at.x, at.y + v * 0.35f), 0.6f + v * 1.1f, 0.45f + v * 0.85f, Fade(Dust, 0.55f * Mathf.Sin(v * Mathf.PI)), soft, Overhead + 0.005f);
                }
            }

            // --- the beam: light, not an object ---
            float start = Mathf.Max(0.45f - slide, rear);
            if (firing && reach - start > 0.4f)
            {
                int count = Mathf.Min(T.MostSteps, Mathf.Max(2, Mathf.CeilToInt((reach - start) / T.Step)));
                bool leaving = rear > 0f;
                // Half width d cells down the lane: narrow at the hands and full by 1.8 cells (or rounded off at the rear once it
                // has let go), with a slow ripple that runs toward the far end.
                float Half(float d, float share) => W / 2f * share * Mathf.Sqrt(Mathf.Clamp01((reach - d) / 1.3f) * 0.85f + 0.15f)
                    * (leaving ? Mathf.Sqrt(Mathf.Clamp01((d - rear) / 1.4f)) : 0.22f + 0.78f * Smooth((d - start) / 1.8f))
                    * (1f + 0.06f * Mathf.Sin(d * 0.9f - s * 30f) + 0.025f * Mathf.Sin(d * 2.3f - s * 47f));
                void Layer(float share, Color colour, float altitude)
                {
                    Sides(count + 1, out Vector2[] left, out Vector2[] right);
                    for (int i = 0; i <= count; i++)
                    {
                        float d = start + (reach - start) * i / count, h = Half(d, share);
                        left[i] = Place(d, h, Chest);
                        right[i] = Place(d, -h, Chest);
                    }
                    Strip(left, right, colour, whiteGlow, altitude);
                }
                // Four nested additive layers, each narrower and brighter: the edge falls off in steps and none of it is opaque.
                Layer(1.55f, Fade(Ki, 0.14f), Overhead + 0.1f);
                Layer(1.25f, Fade(Ki, 0.2f), Overhead + 0.1005f);
                Layer(1f, Fade(Ki, 0.26f), Overhead + 0.101f);
                Layer(0.74f, Fade(KiSky, 0.3f), Overhead + 0.1015f);
                for (float d = start + 1f; d < reach - 0.5f; d += 2f)
                    Sprite(Place(d, 0f, Chest), 3.6f, W * 2f * (Half(d, 1f) / (W / 2f)), Fade(Ki, 0.14f), glow, Overhead + 0.0995f, -aimDegrees);
                Layer(0.46f, Fade(KiSky, 0.45f), Overhead + 0.103f);
                Layer(0.24f, Fade(White, 0.85f), Overhead + 0.104f);
                for (int i = 0; i < T.FlowLines; i++)
                {
                    float d = start + 1f + (Rand(i) * reach + fired * 46f) % Mathf.Max(1f, reach - start - 2f), across = (Rand(i + 15) - 0.5f) * W * 0.75f, l = 1.2f + Rand(i + 33) * 1.8f;
                    if (d + 0.2f >= reach) continue;
                    Streak(Place(d, across, Chest), Place(Mathf.Min(reach, d + l), across, Chest), 0.08f, Fade(Mathf.Abs(across) < W * 0.17f ? KiSky : White, 0.6f), whiteGlow, Overhead + 0.105f, 3);
                }
                for (int n = 0; n < T.BeamRings; n++)
                {
                    float d = 1.5f + (fired * 24f + n * stop / T.BeamRings) % Mathf.Max(1f, stop - 2f);
                    if (d > reach - 1f || d < start + 0.5f) continue;
                    DrawMesh(beamRing, Place(d, 0f, Chest), Overhead + 0.106f, 0.1f, W * 0.62f * (1f + 0.25f * Mathf.Sin(d)), -aimDegrees, Fade(KiIce, 0.55f), whiteGlow);
                }
                // The head: a bright soft bulb with a bow arc in front. Where the lane ends it presses instead: a pulsing light
                // and sparks thrown back.
                Vector2 tip = Place(reach, 0f, Chest);
                float bulb = W / 2f * (arrived ? 1.1f + 0.15f * Mathf.Sin(s * 52f) : 1.25f);
                Sprite(tip, bulb * 4.6f, bulb * 4.6f, Fade(Ki, 0.6f), glow, Overhead + 0.107f);
                Sprite(tip, bulb * 2.8f, bulb * 2.8f, Fade(KiIce, 0.8f), glow, Overhead + 0.108f);
                Sprite(tip, bulb * 1.6f, bulb * 1.6f, Fade(White, 0.95f), glow, Overhead + 0.109f);
                if (!arrived)   // only while it travels: at a wall it would sit past the wall
                {
                    Vector2[] bow = Points(15);
                    for (int j = 0; j <= 14; j++)
                    {
                        float ang = aim + (j / 14f - 0.5f) * 2.5f;
                        bow[j] = tip + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (bulb * 1.2f);
                    }
                    Line(bow, 0.14f + bulb * 0.08f, Fade(White, 0.85f), whiteGlow, Overhead + 0.11f, Taper.Both);
                }
                else
                    for (int i = 0; i < T.PressSparks; i++)
                    {
                        float v = (s * 4f + Rand(i + 12)) % 1f, ang = aim + Mathf.PI + (Rand(i + 13) - 0.5f) * 2.6f, r0 = bulb * 0.8f + v * 2f, r1 = r0 + 0.5f + Rand(i + 14) * 0.7f;
                        var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        Streak(tip + way * r0, tip + way * r1, 0.08f, Fade(White, 1f - v), whiteGlow, Overhead + 0.111f, 3);
                    }
                if (!leaving)
                {
                    Vector2 source = Place(start, 0f, Chest);
                    float pulse = 2.8f + 0.3f * Mathf.Sin(s * 44f);
                    Sprite(source, pulse, pulse, Fade(KiIce, 0.75f), glow, Overhead + 0.112f);
                    Sprite(source, 1.1f, 1.1f, Fade(White, 0.95f), glow, Overhead + 0.113f);
                }
            }

            // --- the hit on each pawn in the lane as the head reaches it ---
            for (int p = 0; p < shot.StruckCount; p++)
            {
                float since = s - shot.StruckAt[p];
                if (since < 0f || since >= 0.22f) continue;
                float v = since / 0.22f;
                var c = new Vector2(shot.Struck[p].x, shot.Struck[p].y + Chest);
                Sprite(c, 1.2f + v * 1.4f, 1.2f + v * 1.4f, Fade(White, 1f - v), glow, Overhead + 0.114f);
                PaperBombGraphics.RingAt(c, 0.3f + v * 1.1f, Fade(White, 0.9f * (1f - v)), Overhead + 0.115f, true, whiteGlow);
                for (int k = 0; k < T.HitSparks; k++)
                {
                    float ang = aim + (k / (float)(T.HitSparks - 1) - 0.5f) * 2.4f + (k % 2 == 1 ? 0.5f : -0.5f) * Mathf.PI, r0 = 0.3f + v * 1.2f;
                    var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Streak(c + way * r0, c + way * (r0 + 0.6f), 0.07f, Fade(White, 1f - v), whiteGlow, Overhead + 0.116f, 3);
                }
            }

            // --- thrown to both sides as the head passes: dust, and rocks that stay ---
            if (fired >= 0f)
            {
                for (int i = 0; i < T.SideDust; i++)
                {
                    float d = 1f + (i + 0.5f) / T.SideDust * (stop - 1f), age = s - T.Passes(t, d, len), life = 0.7f + Rand(i) * 0.4f;
                    if (age < 0f || age > life) continue;
                    float v = age / life, side = i % 2 == 1 ? 1f : -1f;
                    Vector2 at = Place(d, side * (W / 2f + 0.1f + v * (0.8f + Rand(i + 5))));
                    Sprite(new Vector2(at.x, at.y + v * 0.4f), 0.6f + v * 0.9f, 0.45f + v * 0.7f, Fade(Dust, 0.5f * Mathf.Sin(v * Mathf.PI)), soft, Overhead + 0.005f);
                }
                for (int i = 0; i < T.SideRocks; i++)
                {
                    float d = 1.5f + Rand(i + 80) * (stop - 2.5f), age = s - T.Passes(t, d, len);
                    if (age < 0f) continue;
                    float air = 0.5f + 0.4f * Rand(i + 81), u = Mathf.Min(1f, age / air), side = i % 2 == 1 ? 1f : -1f, size = 0.14f + 0.3f * Rand(i + 82) * Rand(i + 83);
                    Vector2 foot = Place(d + u * (Rand(i + 84) - 0.3f) * 1.5f, side * (W / 2f + u * (0.6f + 1.8f * Rand(i + 85))));
                    float h = (0.6f + 1.2f * Rand(i + 86)) * 4f * u * (1f - u);
                    Sprite(foot + sun * h, size * 2.2f, size * 1.1f, Fade(Ink, 0.3f), soft, Floor + 0.05f);
                    PaperBombGraphics.Rock(new Vector2(foot.x, foot.y + h * Lift), size, i * 47f + u * 400f, 1f, i, u < 1f ? Overhead + 0.006f : Floor + 0.06f);
                }
            }

            // --- the end blast ---
            if (blasted >= 0f)
            {
                float opened = 1f - Mathf.Pow(1f - Mathf.Clamp01(blasted / T.BlastOpen), 3f), r = R * opened;
                float fading = Smooth((blasted - T.BlastOpen - T.BlastHold) / T.BlastFade), alive = 1f - fading;
                if (alive > 0f)
                {
                    float first = Mathf.Clamp01(1f - blasted / 0.3f);
                    Sprite(end, R * 5f, R * 5f, Fade(KiIce, 0.8f * first * first), glow, Overhead + 0.12f);
                    Sprite(end, R * 2.4f, R * 2.4f, Fade(White, first), glow, Overhead + 0.121f);
                    for (int k = 0; k < T.BlastLevels; k++)
                    {
                        float tilt = k / (float)T.BlastLevels * 80f * Mathf.Deg2Rad, rad = r * Mathf.Cos(tilt);
                        var c = new Vector2(end.x, end.y + r * 0.38f * Mathf.Sin(tilt) * (1f + 0.5f * fading) * Lift / 0.6f);
                        DrawMesh(disc, c, Overhead + 0.122f + k * 0.001f, rad, rad, 0f, Fade(Color.Lerp(KiDeep, KiSky, k / (float)(T.BlastLevels - 1)), 0.28f * alive), whiteGlow);
                        Vector2[] arc = Points(13);
                        float turn = s * (k % 2 == 1 ? 110f : -90f) + k * 80f;
                        for (int j = 0; j <= 12; j++) arc[j] = c + Turn(turn + 120f * j / 12f) * (rad * 0.96f);
                        Line(arc, 0.1f, Fade(White, 0.6f * alive), whiteGlow, Overhead + 0.127f, Taper.Both);
                    }
                    PaperBombGraphics.RingAt(end, r, Fade(KiIce, 0.9f * alive), Overhead + 0.128f, true, whiteGlow);                  // the front, on the true radius
                }
                for (int i = 0; i < T.BlastRocks; i++)
                {
                    float air = 0.6f + 0.6f * Rand(i + 51), u = Mathf.Min(1f, blasted / air), ang = Rand(i + 52) * Mathf.PI * 2f, big = Rand(i + 55), size = 0.18f + 0.45f * big * big;
                    Vector2 foot = end + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (R * (0.2f + 0.4f * Rand(i + 53)) + R * 0.9f * u);
                    float h = (1.5f + 2.5f * Rand(i + 54)) * (1.1f - 0.5f * big) * 4f * u * (1f - u);
                    Sprite(foot + sun * h, size * 2.2f, size * 1.1f, Fade(Ink, 0.3f), soft, Floor + 0.05f);
                    PaperBombGraphics.Rock(new Vector2(foot.x, foot.y + h * Lift), size, i * 53f + u * air * 500f, 1f, i + 2, u < 1f ? Overhead + 0.129f : Floor + 0.06f);
                }
                float dv = blasted / 1f;
                if (dv <= 1f)
                    for (int i = 0; i < 20; i++)
                    {
                        float ang = i * Mathf.PI * 2f / 20f + Rand(i + 60) * 0.2f, d = R * (0.9f + 0.8f * Smooth(dv));
                        Sprite(new Vector2(end.x + Mathf.Cos(ang) * d, end.y + Mathf.Sin(ang) * d + dv * 0.4f), 0.9f + dv * 1.3f, 0.7f + dv, Fade(Dust, 0.5f * Mathf.Sin(dv * Mathf.PI)), soft, Overhead + 0.005f);
                    }
            }

            // --- smoke off the trench afterwards ---
            if (s >= t.Release)
                for (int i = 0; i < T.Smoke; i++)
                {
                    float born = t.Release + Rand(i + 3) * 1.2f, life = 1.2f + Rand(i + 12) * 0.9f, v = (s - born) / life;
                    if (v < 0f || v > 1f) continue;
                    Vector2 at = Place(1f + Rand(i) * (stop - 1f), (Rand(i + 21) - 0.5f) * W * 0.6f, v * 1.1f);
                    Sprite(new Vector2(at.x + v * 0.3f, at.y), 0.8f + v * 1.4f, 0.7f + v * 1.1f, Fade(Grey, 0.32f * Mathf.Sin(v * Mathf.PI)), soft, Overhead + 0.004f);
                }
        }
    }
}
