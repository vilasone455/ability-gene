using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using Taper = RimArt.GokuGraphics.Taper;
using V = RimArt.VectorShove;

namespace RimArt
{
    /// <summary>A pawn standing in the path of a thrown body.</summary>
    public struct VectorShoveLiner
    {
        /// <summary>Cells down the line from the target's start; cells across it (left of the throw is positive); the side it is knocked to, 1 or -1.</summary>
        public float Along, Across, Side;
        /// <summary>True: the stun stars follow <see cref="At"/>, the game's live point for the pawn. False: the sketch's slide, Knock cells over KnockTime.</summary>
        public bool Live;
        public Vector2 At;
    }

    /// <summary>
    /// One shove as the picture needs it. Points are where the lab draws its stand-ins; in game a pawn's
    /// DrawPos, drawn between PawnFit.Begin and End so heights on pawns are fitted to a real pawn.
    /// </summary>
    public struct VectorShoveShot
    {
        /// <summary>Accelerator's point; the target's point at the touch; where the thrown pawn lies after the throw (its stun stars and landing dust).</summary>
        public Vector2 Caster, Start, Lies;
        /// <summary>The throw's direction, 0 east, 90 north; cells travelled from <see cref="Start"/>.</summary>
        public float Degrees, Stop;
        /// <summary>A wall ends the throw; its face is <see cref="WallFace"/> cells along from <see cref="Start"/>.</summary>
        public bool HitsWall;
        public float WallFace;
        /// <summary>Force returned: the first thing struck gets the heavier look (cracks, 12 chips, more dust, 8 longer impact lines).</summary>
        public bool Bonus;
        /// <summary>
        /// A loose thing thrown instead of a pawn: an arc and the chunk hit instead of the pawn throw. <see cref="Hit"/>
        /// is true when it struck a pawn at <see cref="HitAt"/> (or a wall face there, with <see cref="HitsWall"/>).
        /// <see cref="DrawRock"/> draws it as a stone chunk with its shadow; false leaves it to the game.
        /// </summary>
        public bool Thing, Hit, DrawRock;
        public Vector2 HitAt;
        /// <summary>Pawns bowled aside by a thrown pawn, the first <see cref="LinerCount"/> of <see cref="Liners"/>.</summary>
        public VectorShoveLiner[] Liners;
        public int LinerCount;
        /// <summary>Accelerator's sleeve and skin for the arm.</summary>
        public Color Sleeve, Skin;
    }

    /// <summary>
    /// Draws Vector shove, the port of the lab's accelerator-vector-shove.js: the chosen line and arrow
    /// on the floor, the touch (flash, black ring, 5 streaks) and the arm, black and white speed lines
    /// behind the flying body, dust where it starts; the slam on a wall (a dent, 5 cracks when the force
    /// is returned, grey dust along the face, stone chips that fly back and stay); on each pawn bowled
    /// aside, black impact lines, a dust puff and stun stars; a thrown chunk's arc with its shadow, and
    /// its hit (impact lines, dust, 4 chips). <see cref="HitFlash"/> and <see cref="WindowRing"/> are the
    /// melee hit on Accelerator and the force-returned window, drawn outside a shove.
    ///
    /// Palette: white light with a black edge only on Accelerator's part (flashes, rings, the touch, the
    /// line, the speed lines); the impacts are plain physics with no glow.
    ///
    /// Not drawn (the sketch's stand-ins): the pawns, their white tints and outline, the raider's squash
    /// on the wall face (a real pawn cannot be squashed), the mace, the wall cells and their shake. Flat
    /// shapes and level circles only, so it turns with the aim and there is no per-facing method.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VectorShoveGraphics
    {
        private static readonly float BuildingLayer = AltitudeLayer.Building.AltitudeFor();

        // The frame's shot as locals for Place.
        private static Vector2 start, dir, side;

        /// <summary>A point <paramref name="along"/> cells down the throw from the target's start, <paramref name="across"/> to its left, <paramref name="up"/> high.</summary>
        private static Vector2 Place(float along, float across = 0f, float up = 0f) =>
            start + dir * along + side * across + new Vector2(0f, up * GokuGraphics.Lift);

        /// <summary>
        /// Everything from the line to the tail. <paramref name="s"/> is seconds since the touch; the line and
        /// the arm start before it (from -<see cref="VectorShove.LineLead"/>), so a caller that starts at 0
        /// shows them already out.
        /// </summary>
        public static void Draw(in VectorShoveShot shot, float s, Map map)
        {
            float stop = shot.Stop, arrive = V.Arrive(stop);
            if (s < -V.LineLead || s >= V.End(stop) || !Shown(shot.Caster, map)) return;
            Begin(shot.Start);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            start = shot.Start;
            dir = Turn(shot.Degrees);
            side = new Vector2(-dir.y, dir.x);
            float chest = GokuGraphics.ChestOn, chestUp = chest / GokuGraphics.Lift, a = shot.Degrees * Mathf.Deg2Rad;
            float flown = V.Flown(s, stop), since = s - arrive, linger = V.Linger(s, stop);
            bool arrived = s >= arrive, thing = shot.Thing;
            // Force returned goes to the first thing struck: the first pawn in the path, else the wall.
            int first = -1;
            for (int i = 0; i < shot.LinerCount; i++)
                if (first < 0 || shot.Liners[i].Along < shot.Liners[first].Along) first = i;
            bool wallBonus = shot.Bonus && first < 0;

            // --- the floor: the chosen line with its arrow ---
            float show = V.LineShow(s);
            if (show > 0f)
            {
                Vector2 tip = Place(stop);
                Vector2[] pts = GokuGraphics.Points(2);
                pts[0] = Place(0.3f);
                pts[1] = tip;
                GokuGraphics.Line(pts, 0.09f, Fade(AcceleratorGraphics.Edge, 0.6f * show), solid, Floor + 0.02f, Taper.None);
                GokuGraphics.Line(pts, 0.05f, Fade(AcceleratorGraphics.White, 0.9f * show), solid, Floor + 0.021f, Taper.None);
                for (int k = -1; k <= 1; k += 2)
                    Streak(Place(stop - 0.45f, k * 0.35f), tip, 0.06f, Fade(AcceleratorGraphics.White, 0.9f * show), solid, Floor + 0.0212f + k * 0.0001f, 2);
            }

            // --- the wall: a dent that stays on the face where the chest struck, cracks when the force is returned ---
            float slamAge = shot.HitsWall && !thing && arrived ? since : -1f, face = shot.WallFace;
            if (slamAge >= 0f)
            {
                float grow = Smooth(slamAge / 0.06f);
                Vector2 dent = Place(face + 0.14f) + new Vector2(0f, chest);
                Sprite(dent, 0.34f, 0.55f, Fade(Ink, 0.5f * grow * linger), soft, BuildingLayer + 0.004f, -shot.Degrees);
                Sprite(dent, 0.16f, 0.28f, Fade(Ink, 0.6f * grow * linger), soft, BuildingLayer + 0.0045f, -shot.Degrees);
                if (wallBonus)
                    for (int i = 0; i < V.Cracks; i++)
                    {
                        float th = (-75f + i * 37.5f + (Rand(i + 3) - 0.5f) * 20f) * Mathf.Deg2Rad, len = (0.4f + 0.3f * Rand(i + 8)) * Smooth(slamAge / 0.08f);
                        float c = Mathf.Cos(th), n = Mathf.Sin(th);
                        Vector2[] pts = GokuGraphics.Points(8);
                        for (int k = 0; k <= 7; k++)
                        {
                            float d = len * k / 7f, off = k > 0 ? (Rand(i * 11 + k) - 0.5f) * 0.14f : 0f;
                            pts[k] = Place(face + 0.02f + c * d - n * off, n * d + c * off, chestUp);
                        }
                        GokuGraphics.Line(pts, 0.04f, Fade(AcceleratorGraphics.Edge, 0.75f * linger), solid, BuildingLayer + 0.005f + i * 0.0001f);
                    }
            }
            // Dust under a pawn that landed in the open.
            if (arrived && !thing && !shot.HitsWall)
                Sprite(shot.Lies, 1.2f, 0.8f, Fade(Ink, 0.35f * Smooth(since / 0.3f) * linger), soft, Floor + 0.01f);

            // --- the arm and the touch, toward the target ---
            Vector2 reach = shot.Start - shot.Caster;
            Vector2 toTarget = reach.sqrMagnitude > 1e-6f ? reach.normalized : dir;
            AcceleratorGraphics.Arm(shot.Caster, Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg, V.ArmOut(s), chest, shot.Sleeve, shot.Skin);
            if (s >= 0f && s < V.TouchLife)
            {
                float u = s / V.TouchLife, size = 0.5f * (1f - u) + 0.3f;
                Vector2 touch = shot.Caster + toTarget * V.Contact + new Vector2(0f, chest);
                Sprite(touch, size, size, Fade(AcceleratorGraphics.White, 0.95f * (1f - u)), glow, Overhead + 0.2f);
                PaperBombGraphics.RingAt(touch, 0.2f + u * 0.8f, Fade(AcceleratorGraphics.Edge, 0.8f * (1f - u)), Overhead + 0.19f);
                for (int i = 0; i < 5; i++)
                {
                    Vector2 way = Turn((a + Mathf.PI + (i - 2) * 0.35f) * Mathf.Rad2Deg);
                    Streak(touch + way * 0.2f, touch + way * (0.3f + u * 0.6f), 0.05f, Fade(AcceleratorGraphics.White, 0.9f * (1f - u)), whiteGlow, Overhead + 0.2f + (i + 1) * 0.0002f, 3);
                }
            }

            // --- the flight: speed lines behind the body at chest height, dust where it started ---
            if (s >= V.Touch && !arrived)
                for (int i = 0; i < V.SpeedLines; i++)
                {
                    float across = (i - 2) * 0.22f + (Rand(i + 9) - 0.5f) * 0.1f, back = 1f + Rand(i + 3) * 1.5f;
                    bool black = i % 2 == 0;
                    Streak(Place(Mathf.Max(0.2f, flown - back), across, chestUp), Place(Mathf.Max(0.2f, flown - 0.3f), across, chestUp), black ? 0.06f : 0.04f,
                        Fade(black ? Ink : AcceleratorGraphics.White, 0.7f), black ? solid : whiteGlow, Overhead + 0.09f + i * 0.0002f, 3);
                }
            if (s >= V.Touch && s < V.Touch + 0.6f)
                for (int i = 0; i < V.StartDust; i++)
                {
                    float u = Mathf.Clamp01((s - V.Touch - Rand(i) * 0.1f) / 0.5f), d = 0.2f + u * (0.8f + Rand(i + 4)), across = (Rand(i + 8) - 0.5f) * 1.2f;
                    Sprite(Place(-d * 0.3f + 0.1f, across), 0.3f + u * 0.4f, 0.25f + u * 0.3f, Fade(GokuGraphics.Dust, 0.45f * Mathf.Sin(u * Mathf.PI)), soft, Floor + 0.04f + i * 0.0001f);
                }

            // --- a thrown chunk: an arc with its shadow, then it lies beside what it hit ---
            if (thing && shot.DrawRock)
            {
                if (!arrived)
                {
                    float u = stop > 0f ? Mathf.Clamp01(flown / stop) : 1f, h = V.ChunkArc * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                    Vector2 ground = Place(flown);
                    Sprite(ground + sun * h, 0.7f, 0.4f, Fade(Ink, 0.35f), soft, Floor + 0.05f);
                    PaperBombGraphics.Rock(Place(flown, 0f, h), V.ChunkSize, s < V.Touch ? 20f : 20f + flown * 90f, 1f, 2, Overhead + 0.01f);
                }
                else
                {
                    Vector2 rest = shot.Hit && !shot.HitsWall ? shot.HitAt + side * V.ChunkAside : shot.Hit ? shot.HitAt - dir * 0.3f : Place(stop);
                    PaperBombGraphics.Rock(rest, V.ChunkSize, 140f, 1f, 2, Floor + 0.06f);
                }
            }

            // --- the strikes: plain physics ---
            if (slamAge >= 0f)
            {
                // Grey dust out along the wall face both ways, chips back toward Accelerator.
                int dusts = wallBonus ? V.BonusWallDust : V.WallDust;
                float spread = wallBonus ? 1.5f : 1.2f, big = wallBonus ? 1.3f : 1f;
                for (int i = 0; i < dusts; i++)
                {
                    float way = i % 2 == 1 ? 1f : -1f, u = Mathf.Clamp01((slamAge - Rand(i + 30) * 0.06f) / 0.6f);
                    if (u <= 0f || u >= 1f) continue;
                    float across = way * (0.15f + spread * Rand(i + 40) * Smooth(u)), along = face - 0.25f - 0.35f * Rand(i + 50) - 0.15f * u, size = (0.3f + 0.55f * u) * big;
                    Sprite(Place(along, across, 0.15f * u), size, size * 0.8f, Fade(V.WallDustColour, 0.55f * Mathf.Sin(u * Mathf.PI)), soft, Overhead + 0.01f + i * 0.0002f);
                }
                Chips(Place(face - 0.1f), wallBonus ? V.BonusChips : V.Chips, slamAge, -1f, 0.5f, 0.9f, 80, 1.8f, 90, chestUp, sun, linger);
            }
            for (int i = 0; i < shot.LinerCount; i++)
            {
                VectorShoveLiner liner = shot.Liners[i];
                if (thing || s < V.Touch || flown < liner.Along) continue;
                float hitAt = V.PassAt(liner.Along), age = s - hitAt;
                bool big = i == first && shot.Bonus;
                ImpactLines(Place(liner.Along, liner.Across, chestUp), age, big ? V.BonusLines : V.Lines, big, i);
                Puff(Place(liner.Along, liner.Across), age, big ? V.BonusPuffs : V.Puffs, big ? 0.6f : 0.4f, i);
                if (age > 0.2f)
                {
                    Vector2 now = liner.Live ? liner.At : Place(liner.Along, liner.Across + liner.Side * V.Knock * V.Knocked(s, liner.Along));
                    Stars(now, s, 0.8f * (1f - Smooth((age - 0.3f) / 0.5f)));
                }
            }
            if (thing && arrived && shot.Hit)
            {
                // Chips go on past a pawn, back off a wall.
                ImpactLines(shot.HitAt + new Vector2(0f, chest), since, V.Lines, false, 7);
                Puff(shot.HitAt, since, V.ChunkPuffs, 0.5f, 7);
                Chips(shot.HitAt, V.ChunkChips, since, shot.HitsWall ? -1f : 1f, 0.4f, 0.7f, 120, 1.4f, 130, chestUp, sun, linger);
            }

            // Stun stars on the thrown pawn: at a wall once it has slid down the face, in the open from the landing.
            if (arrived && !thing && (!shot.HitsWall || since > V.Squash + V.Drop))
                Stars(shot.Lies, s, 0.9f * (1f - Smooth((since - 0.3f) / V.Tail)));
        }

        /// <summary>
        /// The melee hit landing on Accelerator: a white flash with a black ring at his chest, on the side
        /// toward the attacker. <paramref name="caster"/> is his point, <paramref name="toward"/> a unit
        /// vector to the attacker, <paramref name="age"/> seconds since the hit.
        /// </summary>
        public static void HitFlash(Vector2 caster, Vector2 toward, float age, Map map)
        {
            if (age < 0f || age >= V.HitFlashLife || !Shown(caster, map)) return;
            float u = age / V.HitFlashLife, size = 0.6f * (1f - u) + 0.2f;
            Vector2 at = caster + toward * V.HitFlashAt + new Vector2(0f, GokuGraphics.ChestOn);
            Sprite(at, size, size, Fade(AcceleratorGraphics.White, 0.9f * (1f - u)), glow, Overhead + 0.2f);
            PaperBombGraphics.RingAt(at, 0.15f + u * 0.5f, Fade(AcceleratorGraphics.Edge, 0.8f * (1f - u)), Overhead + 0.19f);
        }

        /// <summary>
        /// The force-returned window: a thin white ring with a black edge closing round the pawn that hit
        /// Accelerator in melee, over <see cref="VectorShove.Window"/> s. <paramref name="attacker"/> is its
        /// point, <paramref name="age"/> seconds since its hit. The caller stops drawing it when the throw starts.
        /// </summary>
        public static void WindowRing(Vector2 attacker, float age, Map map)
        {
            if (age < 0f || age >= V.Window || !Shown(attacker, map)) return;
            float w = age / V.Window, r = (V.WindowRadius - V.WindowClose * w) * PawnFit.Body;
            var at = new Vector2(attacker.x, attacker.y + PawnFit.Y(V.WindowUp));
            PaperBombGraphics.RingAt(at, r, Fade(AcceleratorGraphics.White, 0.85f * (1f - w * 0.5f)), Overhead + 0.05f);
            PaperBombGraphics.RingAt(at, r + 0.04f, Fade(AcceleratorGraphics.Edge, 0.5f * (1f - w * 0.5f)), Overhead + 0.049f);
        }

        /// <summary>Short black impact lines round a contact point, gone in ImpactLife s.</summary>
        private static void ImpactLines(Vector2 at, float age, int count, bool big, int seed)
        {
            if (age < 0f || age > V.ImpactLife) return;
            float u = age / V.ImpactLife;
            for (int i = 0; i < count; i++)
            {
                float ang = i * Mathf.PI * 2f / count + (Rand(i + 5) - 0.5f) * 0.5f, inner = 0.2f + u * 0.25f;
                float outer = inner + (0.25f + 0.2f * Rand(i + 15)) * (big ? 1.5f : 1f) * (1f - u * 0.5f);
                var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Streak(at + way * inner, at + way * outer, big ? 0.075f : 0.055f, Fade(AcceleratorGraphics.Edge, 0.9f * (1f - u)), solid,
                    Overhead + 0.2f + seed * 0.002f + i * 0.0001f, 3);
            }
        }

        /// <summary>Dust thrown up round a point on the floor.</summary>
        private static void Puff(Vector2 at, float age, int count, float reach, int seed)
        {
            if (age < 0f || age > V.PuffLife) return;
            for (int i = 0; i < count; i++)
            {
                float u = Mathf.Clamp01((age - Rand(i + 7) * 0.05f) / 0.55f), ang = i * Mathf.PI * 2f / count + Rand(i + 3), d = 0.1f + u * reach;
                Sprite(new Vector2(at.x + Mathf.Cos(ang) * d, at.y + Mathf.Sin(ang) * d * 0.7f + u * 0.1f), 0.25f + u * 0.35f, 0.2f + u * 0.28f,
                    Fade(GokuGraphics.Dust, 0.45f * Mathf.Sin(u * Mathf.PI)), soft, Floor + 0.04f + seed * 0.001f + i * 0.0001f);
            }
        }

        /// <summary>
        /// Stone chips from chest height at <paramref name="from"/>, thrown <paramref name="way"/> (1 on down the
        /// throw, -1 back) <paramref name="near"/> + <paramref name="far"/> x rand cells and up to half
        /// <paramref name="spread"/> to either side, in low arcs with shadows; they land and stay.
        /// </summary>
        private static void Chips(Vector2 from, int count, float age, float way, float near, float far, int alongSeed, float spread, int acrossSeed,
            float chestUp, Vector2 sun, float linger)
        {
            if (age < 0f) return;
            for (int i = 0; i < count; i++)
            {
                float time = 0.3f + 0.15f * Rand(i + 60), u = Mathf.Clamp01(age / time);
                float along = way * (near + far * Rand(i + alongSeed)) * u, across = (Rand(i + acrossSeed) - 0.5f) * spread * u;
                float h = chestUp * (1f - u) + 0.35f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                Vector2 ground = from + dir * along + side * across;
                if (u < 1f) Sprite(ground + sun * h, 0.14f, 0.09f, Fade(Ink, 0.35f), soft, Floor + 0.05f);
                PaperBombGraphics.Rock(ground + new Vector2(0f, h * GokuGraphics.Lift), 0.12f + 0.07f * Rand(i + 100), Rand(i + 110) * 360f + age * 700f * (1f - u),
                    linger, 2 * (i % 3), (u < 1f ? Overhead + 0.05f : Floor + 0.06f) + i * 0.001f);
            }
        }

        /// <summary>Three small white stars circling a pawn's head (<paramref name="pos"/> is its point): it is stunned.</summary>
        private static void Stars(Vector2 pos, float seconds, float alpha)
        {
            if (alpha <= 0f) return;
            for (int i = 0; i < 3; i++)
            {
                float ang = seconds * 5f + i * 2.094f;
                GokuGraphics.Glint(new Vector2(pos.x + Mathf.Cos(ang) * 0.27f, pos.y + PawnFit.Y(0.84f) + Mathf.Sin(ang) * 0.1f), 0.1f,
                    alpha * (0.6f + 0.4f * Mathf.Sin(ang)), AcceleratorGraphics.White, 45f);
            }
        }
    }
}
