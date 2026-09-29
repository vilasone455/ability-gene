using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.BlackGhostMatter;

namespace RimArt
{
    /// <summary>
    /// What the Black Ghost looks like this frame. The game fills it from the ghost pawn every frame (the pawn's own
    /// graphic is invisible). Start from <see cref="At"/>: a default struct would read Swipe 0 and TearSeconds 0 as a
    /// swing and a tear that just began. No map access in the drawing; everything it needs is here.
    /// </summary>
    public struct BlackGhostShot
    {
        /// <summary>The ghost pawn's DrawPos: its cell's centre, or between cells while it walks. The feet are drawn <see cref="BlackGhostGraphics.FeetDrop"/> below it, on the cell's bottom edge, where a pawn sprite's feet are.</summary>
        public Vector3 Pos;
        /// <summary>South: front (mouth, tongue). North: back. East: profile. West: east mirrored.</summary>
        public Rot4 Facing;
        /// <summary>Seconds since the summon began: the idle sway (2.6 s), the tongue flick (every 2.6 s from 1.6 s, 0.34 s long), the claws flexing, the flakes.</summary>
        public float Seconds;
        /// <summary>0 at the summon, 1 at the end of its life. It frays over the last 25 % (<see cref="BlackGhostGraphics.FrayFrom"/>).</summary>
        public float LifeFrac;
        /// <summary>Cells walked, a running total. One stride cycle per <see cref="BlackGhostGraphics.StrideCells"/>.</summary>
        public float WalkCells;
        /// <summary>True while it walks: the stride pose, the bob, more flakes, loose ends trailing.</summary>
        public bool Moving;
        /// <summary>The direction it walks, on the map (x, z). Zero: the facing's direction.</summary>
        public Vector3 WalkDir;
        /// <summary>Seconds since a melee swing began; negative = none. A swing lasts <see cref="BlackGhostGraphics.SwipeSeconds"/>.</summary>
        public float Swipe;
        /// <summary>Front and back views: false swings the claw on screen right (+x), true the left one. The profile always swings the near arm.</summary>
        public bool SwipeLeft;
        /// <summary>The swing target's DrawPos: the lunge (0.25 cells) goes toward it. Zero: along the facing.</summary>
        public Vector3 SwipeTarget;
        /// <summary>0..1 over the <see cref="BlackGhostGraphics.SummonSeconds"/> build-up from the feet; 1 = built.</summary>
        public float Build;
        /// <summary>0..1 over the <see cref="BlackGhostGraphics.DissolveSeconds"/> dissolve, flakes from the feet up; 0 = not dissolving.</summary>
        public float Dissolve;
        /// <summary>Seconds since the Tear grab began; negative = no tear. See <see cref="TearGraphics"/> for the beats.</summary>
        public float TearSeconds;
        /// <summary>The torn enemy's DrawPos, without the draw offset Tear gives it.</summary>
        public Vector3 TearTarget;
        /// <summary>True: Tear takes an arm. False: a leg.</summary>
        public bool TearArm;
        /// <summary>The sun's shadow per cell of height on the map (x, z): GenCelestial's shadow vector times SixPathsSlamGraphics.SunScale.</summary>
        public Vector2 Sun;
        /// <summary>0.32 times GenCelestial.CurShadowStrength, as the lab's scenes; 0 draws no shadow.</summary>
        public float ShadowStrength;

        /// <summary>A built ghost standing at <paramref name="pos"/>, no swing, no tear, no shadow.</summary>
        public static BlackGhostShot At(Vector3 pos, Rot4 facing, float seconds) => new BlackGhostShot
        {
            Pos = pos, Facing = facing, Seconds = seconds, Build = 1f, Swipe = -1f, TearSeconds = -1f,
        };
    }

    /// <summary>
    /// Satō's Black Ghost, v2 look: the port of Tools/VfxLab/web/sketches/ajin-black-ghost-v2.js and
    /// lib/ajin-ghost-v2.js. A reptile head with a hinged jaw and a forked tongue, a black skeleton in bandage wraps with
    /// loose ends, a hunched stance and a cobra sway; it frays over the last 25 % of its life. Beats: summon (black matter
    /// pours from Satō's shoulders, the ghost builds from the feet in 1.2 s), idle, walk, swipe, Tear (the pose; the rest in
    /// <see cref="TearGraphics"/>), dissolve (flakes from the feet up in 1.0 s). Constants are the sketch's defaults.
    /// The game draws the ghost pawn through <see cref="DrawGhost"/> every frame.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BlackGhostGraphics
    {
        /// <summary>The feet are drawn this far north of DrawPos (south, negative): the bottom edge of the cell, where a pawn's feet are.</summary>
        public const float FeetDrop = -0.45f;
        /// <summary>The build-up from the feet on summon; the stream from Satō's shoulders runs until StreamSeconds.</summary>
        public const float SummonSeconds = 1.2f, StreamSeconds = SummonSeconds * 1.2f;
        public const float DissolveSeconds = 1.0f;
        /// <summary>Share of its life after which it frays: limbs thin by up to 15 %, flakes triple, chunks peel off, loose ends grow from 3 to 5 and lengthen by up to 60 %.</summary>
        public const float FrayFrom = 0.75f;
        /// <summary>A swing: raise 0-50 %, strike 50-68 %, recover. The claw lands at SwipeHitAt; the camera shakes HitShake then.</summary>
        public const float SwipeSeconds = 0.5f, SwipeHitAt = 0.59f * SwipeSeconds, HitShake = 0.05f;
        /// <summary>The claw hit's streaks and blood show from HitFrom to HitUntil seconds around the hit (<see cref="DrawClawHit"/>).</summary>
        public const float HitFrom = -0.05f, HitUntil = 0.6f;
        /// <summary>Cells per stride cycle, and how far a swing lunges toward its target.</summary>
        public const float StrideCells = 0.8f, Lunge = 0.25f;
        private const float TonguePeriod = 2.6f, FlickLength = 0.34f, FlickStart = 1.6f;

        /// <summary>Tongue flick 0..1: out in 0.12 s, held 0.1 s, back in 0.12 s, every 2.6 s from 1.6 s.</summary>
        internal static float Flick(float t)
        {
            if (t < FlickStart) return 0f;
            float ph = Mathf.Repeat(t - FlickStart, TonguePeriod);
            if (ph > FlickLength) return 0f;
            return ph < 0.12f ? Smooth(ph / 0.12f) : ph < 0.22f ? 1f : 1f - Smooth((ph - 0.22f) / 0.12f);
        }

        /// <summary>A facing's direction on the map: 0 north, 1 east, 2 south, 3 west.</summary>
        internal static Vector2 FacingDir(int face) =>
            face == 0 ? Vector2.up : face == 1 ? Vector2.right : face == 2 ? Vector2.down : Vector2.left;

        private static float LungeShape(float w) =>
            w < 0.5f ? 0f : w < 0.68f ? Smooth((w - 0.5f) / 0.18f) : 1f - Smooth((w - 0.68f) / 0.32f);

        /// <summary>
        /// The shot in the figure's terms: where the feet are drawn (walk, lunge and Tear step included), the pose inputs,
        /// the walk on screen for the loose ends, and the lo / hi clamp of the build and the dissolve.
        /// </summary>
        private static void Resolve(in BlackGhostShot s, out Vector2 feet, out BlackGhostMotion m, out Vector2 travel, out float lo, out float hi)
        {
            int face = s.Facing.AsInt;
            var pos = new Vector2(s.Pos.x, s.Pos.z);
            float walk = s.Moving ? 1f : 0f;
            m = new BlackGhostMotion { Gait = s.WalkCells / StrideCells, Walk = walk, Seconds = s.Seconds, Swipe = -1f, Side = 1 };
            var heading = new Vector2(s.WalkDir.x, s.WalkDir.z);
            travel = (heading.sqrMagnitude > 1e-6f ? heading.normalized : FacingDir(face)) * walk;

            if (s.Swipe >= 0f && s.Swipe < SwipeSeconds)
            {
                float w = s.Swipe / SwipeSeconds;
                m.Swipe = w;
                m.Side = s.SwipeLeft ? -1 : 1;
                var toTarget = new Vector2(s.SwipeTarget.x - pos.x, s.SwipeTarget.z - pos.y);
                bool noTarget = (s.SwipeTarget.x == 0f && s.SwipeTarget.z == 0f) || toTarget.sqrMagnitude < 1e-6f;
                pos += (noTarget ? FacingDir(face) : toTarget.normalized) * (Lunge * LungeShape(w));
            }

            var enemy = new Vector2(s.TearTarget.x, s.TearTarget.z);
            bool tear = s.TearSeconds >= 0f;
            if (tear)
            {
                // Steps into the target's cell over the grab and back out after the throw (TearGraphics.Step).
                TearGraphics.Step(pos, enemy, s.TearSeconds, out Vector2 stepped, out float stepCells, out float stepWalk);
                pos = stepped;
                m.Gait += stepCells / StrideCells;
                m.Walk = stepWalk;
                travel = Vector2.zero;
            }
            else if (m.Walk <= 0f && m.Swipe < 0f)
            {
                // The tongue flicks while it stands still and does nothing else; the jaw parts a quarter for it.
                float f = Flick(s.Seconds);
                m.Jaw = 0.25f * f;
                m.Tongue = f;
            }
            feet = new Vector2(pos.x, pos.y + FeetDrop);
            if (tear) TearGraphics.Grip(new Vector2(s.Pos.x, s.Pos.z), enemy, s.TearSeconds, s.TearArm, feet, face == 3 ? -1f : 1f, ref m);

            hi = BlackGhostFigure.Top * Smooth((s.Build * SummonSeconds - 0.25f) / Mathf.Max(0.1f, SummonSeconds - 0.25f));
            lo = BlackGhostFigure.Top * Smooth(s.Dissolve);
        }

        /// <summary>
        /// The ghost this frame: its shadow, the figure, the flakes coming off it (60 % more while walking, up to 3x as it
        /// frays), the chunks peeling off as it frays, and the line of flakes on the edge while it builds or dissolves.
        /// </summary>
        public static void DrawGhost(BlackGhostShot s)
        {
            Reset();
            Resolve(s, out Vector2 feet, out BlackGhostMotion m, out Vector2 travel, out float lo, out float hi);
            Begin(feet);
            float top = BlackGhostFigure.Top, fray = Smooth((s.LifeFrac - FrayFrom) / (1f - FrayFrom)), mirror = s.Facing.AsInt == 3 ? -1f : 1f;
            if (BlackGhostFigure.Draw(feet, s.Facing.AsInt, m, lo, hi, s.Sun, s.ShadowStrength, fray, travel))
            {
                Flakes(feet, BlackGhostFigure.Segs, s.Seconds, (1f + m.Walk * 0.6f) * (1f + 2f * fray), lo, hi, mirror, Overhead + 0.02f);
                if (fray > 0f) BlackGhostFigure.Chunks(feet, s.Seconds, fray, lo, hi, mirror, Overhead + 0.025f);
            }
            if (hi < top && hi > 0f) EdgeFlakes(feet, hi, 0.8f, s.Seconds, 1.1f, 0.45f, Overhead + 0.03f);
            if (lo > 0f && lo < top) EdgeFlakes(feet, lo, 0.85f, s.Seconds, 1.5f, 0.7f, Overhead + 0.03f);
        }

        /// <summary>
        /// The ghost's wrists and elbows on screen this frame (into BlackGhostFigure.Wrists and Elbows), without drawing.
        /// False when nothing of it is shown. <paramref name="dissolving"/> is true once the dissolve has begun.
        /// </summary>
        internal static bool Joints(in BlackGhostShot s, out bool dissolving)
        {
            Resolve(s, out Vector2 feet, out BlackGhostMotion m, out _, out float lo, out float hi);
            dissolving = lo > 0f;
            if (BlackGhostFigure.Shown(lo, hi) <= 0f) return false;
            BlackGhostFigure.Joints(feet, s.Facing.AsInt, m, lo, hi);
            return true;
        }

        /// <summary>
        /// The black matter pouring out of Satō's shoulders to where the ghost forms: 6 strands bowed to alternate sides,
        /// reaching the spot by 0.72 s and fading out between 0.84 and <see cref="StreamSeconds"/>.
        /// <paramref name="from"/> is Satō's DrawPos, <paramref name="to"/> the ghost's; <paramref name="seconds"/> since the summon began.
        /// </summary>
        public static void DrawSummonStream(Vector3 from, Vector3 to, float seconds)
        {
            float pour = Smooth(seconds / (SummonSeconds * 0.6f)), alpha = 1f - Smooth((seconds - SummonSeconds * 0.7f) / (SummonSeconds * 0.5f));
            if (pour <= 0f || alpha <= 0f) return;
            Reset();
            Begin(new Vector2(from.x, from.z));
            Ooze(new Vector2(from.x, from.z + 0.12f), new Vector2(to.x, to.z + FeetDrop + 0.05f), seconds, 6, 0.45f, pour, alpha, Overhead + 0.01f);
        }

        /// <summary>
        /// A claw hit on <paramref name="target"/> (its DrawPos): six pale streaks raking down across it and 9 drops of
        /// blood thrown away from the ghost. <paramref name="age"/> is seconds since the hit (SwipeHitAt into a swing); it
        /// shows from <see cref="HitFrom"/> to <see cref="HitUntil"/>. The streaks run down to the right facing east, down
        /// to the left facing west, and away from the swinging claw facing north or south.
        /// </summary>
        public static void DrawClawHit(Vector3 target, Vector3 ghostToTarget, Rot4 facing, float age, bool swipeLeft)
        {
            if (age < HitFrom || age > HitUntil) return;
            Reset();
            var e0 = new Vector2(target.x, target.z);
            Vector2 d = new Vector2(ghostToTarget.x, ghostToTarget.z).normalized;
            Begin(e0);
            int face = facing.AsInt, j = swipeLeft ? 1 : 0;
            float side = swipeLeft ? -1f : 1f;
            Vector2 v = face == 1 ? new Vector2(0.75f, -0.66f) : face == 3 ? new Vector2(-0.75f, -0.66f) : new Vector2(-0.75f * side, -0.66f);
            var n = new Vector2(-v.y, v.x);
            var c = new Vector2(e0.x + d.x * 0.05f, e0.y - 0.1f);
            float reach = Mathf.Clamp01((age + 0.05f) / 0.1f), fade = 1f - Smooth((age - 0.15f) / 0.45f);
            for (int i = 0; i < 6; i++)
            {
                Vector2 a = c + n * ((i - 2.5f) * 0.05f) - v * 0.30f;
                Vector2[] pts = Buf(3);
                pts[0] = a; pts[1] = a + v * (0.3f * reach); pts[2] = a + v * (0.6f * reach);
                Trail(pts, 0.03f, Fade(Slash, 0.95f * fade), Overhead + 0.06f + i * 0.0004f);
            }
            for (int i = 0; i < 9; i++)
            {
                float life = 0.35f + R(i + j * 30) * 0.2f, u = age / life;
                if (u < 0f || u > 1f) continue;
                float th = Mathf.Atan2(d.y, d.x) + (R(i + j * 30 + 3) - 0.5f) * 1.6f, r = u * (0.25f + R(i + 5) * 0.35f);
                float hgt = 0.5f * Mathf.Sin(u * Mathf.PI) * 0.6f;
                Disc(new Vector2(c.x + Mathf.Cos(th) * r, c.y + 0.1f + Mathf.Sin(th) * r * 0.7f + hgt * 0.6f), Overhead + 0.05f + i * 0.0002f,
                    0.03f, 0.022f, 0f, Fade(Blood, 1f - u * 0.5f));
            }
        }
    }
}
