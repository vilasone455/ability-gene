using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.ObitoGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// One Wood Release: Cutting Technique, from the moment it fires (the lab's obito-wood-release.js from its
    /// 0.5 s on). Three strands twisting round each other race out of his right hand at the XML speed; side
    /// twigs split off behind the front; at every pawn on the line two spikes fan out and pass through it, and
    /// the pawn takes its hit as the front reaches it. 0.25 s after the front is out the branches dry to grey
    /// and break from the hand outward; the pieces drop and lie as splinters for a few seconds.
    ///
    /// The warm-up half (his hand coming up, turning to bark, twigs splitting off it) is
    /// <see cref="DrawWarmup"/>, drawn from the cast's warm-up stance.
    /// </summary>
    internal sealed class WoodStrike
    {
        // The sketch's rule numbers that are look (the length, speed and damage are the XML's).
        private const float H = 0.55f, Step = 0.2f, Chunk = 0.45f, Gravity = 6f, Break = 30f, Hold = 0.25f;
        private const float HandAlong = 0.48f, HandAcross = -0.13f;
        /// <summary>Splinters lie this long after they land, then fade over a second.</summary>
        private const float SplintersStay = 8f;
        private static readonly float[] StrandW = { 0.2f, 0.09f, 0.09f }, StrandLag = { 0f, 0.12f, 0.2f },
            StrandShort = { 0f, 0.3f, 0.5f }, StrandFrom = { 0f, 0.12f, 0.2f };

        private sealed class Victim { public Pawn pawn; public float along; public int hitTick; public bool hit; public Vector2 at; }

        private Pawn caster;
        private Map map;
        private CompProperties_AbilityWoodRelease props;
        private Vector2 origin, aim;
        private float len, speed;
        private int fireTick;
        private readonly List<Victim> victims = new List<Victim>();

        /// <summary>How many hits have landed from Wood Release since the game started, for the tests.</summary>
        internal static int Landed { get; private set; }

        internal static WoodStrike Make(Pawn caster, Vector2 aim, float reach, List<IntVec3> cells, CompProperties_AbilityWoodRelease props, Map map)
        {
            var strike = new WoodStrike
            {
                caster = caster, map = map, props = props, aim = aim, origin = Ground(caster.DrawPos),
                len = reach + 0.5f, speed = Mathf.Max(1f, props.speed), fireTick = Find.TickManager.TicksGame,
            };
            Vector3 from = caster.Position.ToVector3Shifted();
            var seen = new HashSet<Pawn>();
            foreach (IntVec3 cell in cells)
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (!(thing is Pawn pawn) || !seen.Add(pawn) || !CompAbilityEffect_WoodRelease.Hits(caster, pawn, props)) continue;
                    Vector3 at = pawn.DrawPos;
                    float along = Mathf.Max(0.5f, (at.x - from.x) * aim.x + (at.z - from.z) * aim.y);
                    strike.victims.Add(new Victim
                    {
                        pawn = pawn, along = along,
                        hitTick = strike.fireTick + Mathf.Max(0, Mathf.RoundToInt((along - HandAlong) / strike.speed * 60f)),
                    });
                }
            strike.Tick(strike.fireTick);
            return strike;
        }

        internal void Tick(int now)
        {
            foreach (Victim v in victims)
            {
                if (v.hit || now < v.hitTick) continue;
                v.hit = true;
                if (v.pawn.Spawned && v.pawn.Map == map)
                {
                    Vector3 at = v.pawn.DrawPos;
                    v.at = new Vector2(at.x, at.z);
                    CompAbilityEffect_WoodRelease.Hit(caster, v.pawn, aim, props);
                    Landed++;
                }
            }
        }

        private float Full => (len - HandAlong) / speed;
        private float Crumble => Full + Hold;
        private float Settled => Crumble + 0.12f + (len - HandAlong) / Break + Mathf.Sqrt(H / Gravity);

        internal bool Done(int now)
        {
            float s = (now - fireTick) / 60f;
            foreach (Victim v in victims) if (!v.hit) return false;
            return s > Settled + SplintersStay + 1f;
        }

        // ---- the aim frame ----

        private static Vector2 Place(Vector2 o, Vector2 aim, float along, float across, float h) =>
            new Vector2(o.x + along * aim.x - across * aim.y, o.y + along * aim.y + across * aim.x + h * Lift);

        private static float Settle(float x) => Mathf.Exp(-(x - HandAlong) / 0.55f);
        private static float Main(float x) => HandAcross * Settle(x) + 0.04f * Mathf.Sin(x * 1.9f + 1.3f) * (1f - Settle(x));

        /// <summary>
        /// The warm-up: his right hand comes up to point down the line (pawns have no arms: the hand alone moves, as
        /// Melee Animation draws hands), turns to bark, and small twigs split off it. <paramref name="s"/> is seconds
        /// into the warm-up.
        /// </summary>
        internal static void DrawWarmup(Pawn caster, Vector2 ground, Vector2 aim, float s, float total)
        {
            float raise = Smooth(s / 0.15f), bark = Smooth((s - 0.05f) / 0.35f);
            DrawHand(ground, aim, raise, bark, SkinOf(caster));
            PowerPoleGraphics.Sun(Find.CurrentMap, out Vector2 sun, out float strength);
            float layer = aim.y > 0.5f ? LPawn - 0.03f : LFx;
            for (int j = 0; j < 3; j++) HandTwig(ground, aim, j, s, sun, strength, layer);
        }

        /// <summary>His right hand: from beside his body up to the point the branches grow from, <paramref name="bark"/> of it wood.</summary>
        private static void DrawHand(Vector2 ground, Vector2 aim, float raise, float bark, Color skin)
        {
            Vector2 hand = Place(ground, aim, Mathf.Lerp(0.15f, HandAlong, raise), Mathf.Lerp(-0.2f, HandAcross, raise), H);
            Hand(hand, skin, aim.y > 0.5f ? LPawn - 0.01f : LPawn + 0.04f, bark, Mathf.Clamp01(raise * 3f));
        }

        /// <summary>The twigs splitting off the hand: how far back along the aim each starts, which side it goes, its length.</summary>
        private static readonly float[,] Twigs = { { 0f, 1f, 0.14f }, { 0.03f, -1f, 0.12f }, { 0.06f, 1f, 0.17f } };
        private static readonly Vector2[] two = new Vector2[2];

        private static void HandTwig(Vector2 ground, Vector2 aim, int j, float s, Vector2 sun, float strength, float layer, float dry = 0f, float alpha = 1f)
        {
            float g = Smooth((s - 0.22f - j * 0.07f) / 0.15f);
            if (g <= 0f) return;
            float back = Twigs[j, 0], side = Twigs[j, 1], length = Twigs[j, 2], ang = 55f * Mathf.Deg2Rad;
            float bx = HandAlong - back, by = HandAcross;
            two[0] = Place(ground, aim, bx, by, H);
            two[1] = Place(ground, aim, bx + Mathf.Cos(ang) * length * g, by + side * Mathf.Sin(ang) * length * g, H);
            Branch(two, 2, 0.04f, H, sun, strength, alpha, layer, 0.08f, dry);
        }

        // ---- after the fire ----

        private readonly List<Vector2> spine = new List<Vector2>();
        private static Vector2[] points = new Vector2[64];

        private delegate float Across(float x);

        /// <summary>The sketch's line(): points every Step along the aim from <paramref name="from"/> to the front or <paramref name="to"/>.</summary>
        private void Line(float from, float to, Across across, float grow)
        {
            spine.Clear();
            float end = Mathf.Min(to, grow);
            if (end <= from + 0.02f) return;
            for (float x = from; x < end; x += Step) spine.Add(new Vector2(x, across(x)));
            spine.Add(new Vector2(end, across(end)));
        }

        internal void Draw(int now)
        {
            float s = (now - fireTick) / 60f;
            if (!Shown(origin, map) && !Shown(origin + aim * len, map)) return;
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            float layer = aim.y > 0.5f ? LPawn - 0.03f : LFx;
            float front = HandAlong + Mathf.Max(0f, s) * speed;
            float fade = 1f - Smooth((s - Settled - SplintersStay) / 1f);

            // The hand: up and barked from the warm-up; the bark leaves as the branches crumble, then it goes back down.
            float raise = 1f - Smooth((s - Settled) / 0.3f), bark = 1f - Smooth((s - Crumble - 0.1f) / 0.3f);
            if (caster.Spawned && caster.Map == map) DrawHand(Ground(caster.DrawPos), aim, raise, bark, SkinOf(caster));

            // Blood thrown out behind every pawn the spikes passed through.
            foreach (Victim v in victims)
                if (v.hit) Bleed(v.at, (now - v.hitTick) / 60f, Mathf.Atan2(aim.y, aim.x), victims.IndexOf(v));

            float dry = Smooth((s - Crumble) / 0.15f);
            bool broken = s >= Crumble + 0.12f;
            int piece = 0;
            for (int k = 0; k < 3; k++)
            {
                int strand = k;
                Line(HandAlong + StrandFrom[k], len - StrandShort[k], x => Main(x) + (strand == 0 ? 0f
                    : (strand == 1 ? 1f : -1f) * 0.085f * Mathf.Sin(x * 2.6f + strand * 1.7f) * (1f - Settle(x) * 0.7f)), front - StrandLag[k]);
                Piece(piece++, StrandW[k], 0.35f, true, s, dry, broken, sun, strength, layer, fade);
            }
            if (!broken)
                for (int j = 0; j < 3; j++) HandTwig(origin, aim, j, 1f, sun, strength, layer, dry);
            for (int j = 0; ; j++)
            {
                float b = 1f + j * 1.05f + (Rand(j * 3 + 1) - 0.5f) * 0.35f;
                if (b > len - 0.6f) break;
                float side = Rand(j * 3 + 2) < 0.5f ? 1f : -1f, ang = (22f + Rand(j * 3 + 3) * 20f) * Mathf.Deg2Rad, l0 = 0.45f + Rand(j * 3 + 4) * 0.85f;
                float g = Smooth((s - (b - HandAlong) / speed) / 0.1f);
                if (g <= 0f) continue;
                var bas = new Vector2(b, Main(b));
                var dir = new Vector2(Mathf.Cos(ang), side * Mathf.Sin(ang));
                float l = l0 * g;
                spine.Clear();
                spine.Add(bas);
                spine.Add(new Vector2(bas.x + dir.x * l * 0.5f, bas.y + dir.y * l * 0.5f - side * 0.03f));
                spine.Add(bas + dir * l);
                Piece(piece++, 0.07f, 0.2f, false, s, dry, broken, sun, strength, layer, fade);
            }
            foreach (Victim v in victims)
                for (int side = 1; side >= -1; side -= 2)
                {
                    float start = v.along - 0.75f, sd = side;
                    Line(start, v.along + 0.45f, x => Main(x) + sd * Mathf.Lerp(0f, 0.2f, Clamp01((x - start) / 1.2f)), front);
                    if (spine.Count > 0) Piece(piece++, 0.075f, 0.25f, false, s, dry, broken, sun, strength, layer, fade);
                }
        }

        /// <summary>
        /// One piece of wood whose spine (aim-frame points) is in <see cref="spine"/>: intact until the crumble;
        /// then dry, broken from the hand outward, dropping, and lying as splinters.
        /// </summary>
        private void Piece(int id, float w, float tip, bool strand, float s, float dry, bool broken, Vector2 sun, float strength, float layer, float fade)
        {
            int n = spine.Count;
            if (n < 2 || fade <= 0.01f) return;
            if (points.Length < n) points = new Vector2[n * 2];
            if (!broken)
            {
                for (int i = 0; i < n; i++) points[i] = Place(origin, aim, spine[i].x, spine[i].y, H);
                Branch(points, n, w, H, sun, strength, 1f, layer, tip, dry);
                return;
            }
            // A strand breaks into pieces about Chunk cells long; twigs and spikes fall whole.
            int start = 0, chunk = 0;
            while (start < n - 1)
            {
                int end = start + 1;
                if (strand) while (end < n - 1 && spine[end].x - spine[start].x < Chunk) end++;
                else end = n - 1;
                DrawChunk(id, chunk++, start, end, end == n - 1 ? tip : 0.03f, w, s, sun, strength, layer, fade);
                start = end;
            }
        }

        private void DrawChunk(int id, int ci, int from, int to, float tip, float w, float s, Vector2 sun, float strength, float layer, float fade)
        {
            int count = to - from + 1;
            Vector2 mid = spine[from + count / 2];
            float age = s - Crumble - 0.12f - (mid.x - HandAlong) / Break;
            float fall = Mathf.Max(0f, age), h = Mathf.Max(0f, H - Gravity * fall * fall);
            bool down = h <= 0f;
            float r = Rand(ci * 7 + id * 13), turn = (r - 0.5f) * 0.7f * Clamp01(fall / 0.3f), drift = (Rand(ci * 5 + 2 + id) - 0.5f) * 0.25f * Clamp01(fall / 0.3f);
            float ct = Mathf.Cos(turn), st = Mathf.Sin(turn);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = spine[from + i];
                float dx = p.x - mid.x, dy = p.y - mid.y;
                points[i] = Place(origin, aim, mid.x + dx * ct - dy * st, mid.y + drift + dx * st + dy * ct, h);
            }
            Branch(points, count, w * (down ? 0.8f : 1f), h, sun, down ? 0f : strength, (down ? 0.85f : 1f) * fade,
                down ? LFloor + 0.002f : layer, tip, 1f);
        }

        /// <summary>Blood thrown out behind the pawn along the aim (the drops that land are the game's own blood).</summary>
        private static void Bleed(Vector2 at, float age, float aimAngle, int seed)
        {
            if (age < 0f || age > 0.45f) return;
            var chest = new Vector2(at.x, at.y);
            for (int i = 0; i < 7; i++)
            {
                float ang = aimAngle + (Rand(seed * 29 + i * 3 + 40) - 0.5f) * 1.2f, sp = 1.6f + Rand(seed * 29 + i * 3 + 41) * 1.8f,
                    life = 0.28f + Rand(seed * 29 + i * 3 + 42) * 0.1f;
                if (age >= life) continue;
                float d = sp * age, h = 0.45f + 1.1f * age - 5f * age * age;
                Sprite(new Vector2(chest.x + Mathf.Cos(ang) * d, chest.y - FeetBelowDrawPos + Mathf.Sin(ang) * d + Mathf.Max(0f, h) * Lift),
                    0.07f, 0.07f, Blood, soft, LFx + 0.02f);
            }
        }
    }
}
