using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>What a piece of torn-up floor is: a slab (a stone lump), a plank (a two-tone board) or a chip (a small lump).</summary>
    public enum BluePieceKind { Slab, Plank, Chip }

    /// <summary>Where a piece is: still on the floor, in the air, or packed into the bubble.</summary>
    public enum BluePieceState { Ground, Flying, Packed }

    /// <summary>
    /// One piece of floor Blue tears up. It lies at <see cref="R0"/> cells from the centre in direction
    /// <see cref="A0"/> (radians, 0 east) until <see cref="Born"/>, spirals up and in over
    /// <see cref="Rise"/> s to an orbit of <see cref="OrbitR"/> cells at height <see cref="OrbitH"/>,
    /// orbits for <see cref="Orbit"/> s and is packed into the bubble at <see cref="PackedAt"/>.
    /// Heights are in cells up, drawn x <see cref="SixPathsHeight.Lift"/> north.
    /// </summary>
    public struct BluePiece
    {
        public int Index;
        public BluePieceKind Kind;
        public float Born, Rise, Orbit, PackedAt, R0, A0, OrbitR, OrbitH, Size;
    }

    /// <summary>
    /// Times and sizes of Lapse: Blue, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/gojo-blue-v2.js). Seconds in, numbers out, no drawing and no map.
    ///
    /// The picture's clock starts when Gojo starts to raise his arm (0). The Blue opens at
    /// <see cref="OpenAt"/>, is full at <see cref="FullAt"/>, holds for the hold, everything rushes into
    /// the centre for <see cref="Rush"/> s and it implodes at <see cref="BurstAt"/>; the dust and the
    /// crater run <see cref="Tail"/> s after that. The balance numbers here are defaults: the ability
    /// will read them from XML (CompProperties_AbilityGravityWell) and pass them in the
    /// <see cref="BlueCast"/>.
    /// </summary>
    public static class GojoBlue
    {
        // Balance, defaults until the ability reads them from XML: how long it holds (s), the pull radius,
        // the core radius, the pull at the core edge (cells/s) and the implosion radius.
        public const float Hold = 3f, PullRadius = 4f, CoreRadius = 1f, PullSpeed = 6f, BurstRadius = 2f;
        /// <summary>How dark the ground inside the pull goes, 0 to 1.</summary>
        public const float Dark = 0.6f;

        /// <summary>The arm goes up, the ball grows, the rush-in, and the picture after the implosion (s).</summary>
        public const float Raise = 0.15f, Grow = 0.3f, Rush = 0.12f, Tail = 2.2f;
        /// <summary>
        /// The ball's height: the sketch's chest (0.3 cells drawn) in cells up. It is not on a pawn, so
        /// it is not fitted to one (PawnFit).
        /// </summary>
        public const float BallUp = 0.3f / SixPathsHeight.Lift;
        /// <summary>Held pawns are lifted this far (cells drawn) in the sketch.</summary>
        public const float HeldLift = 0.25f;

        // The bubble's swirl bands and wisps; the streaks curling in and how often each comes round (s).
        public const int Bands = 12, Wisps = 6, Specks = 26, Streaks = 14;
        public const float StreakPeriod = 0.6f;
        // Fog puffs on FogArms spiral arms, rays from the ball, cracks, the dust cloud, pieces thrown out.
        public const int Fog = 30, FogArms = 3, Rays = 8, Cracks = 10, Clouds = 20, Thrown = 16;
        /// <summary>The crater's radius; its dark middle is twice this across.</summary>
        public const float CraterRadius = 0.8f;
        /// <summary>Slabs, planks and chips torn up one after another over the first TearShare of the hold.</summary>
        public const int Slabs = 14, Planks = 8, Chips = 26, Pieces = Slabs + Planks + Chips;
        public const float TearShare = 0.75f;
        /// <summary>Packed pieces drawn inside the bubble: the latest this many.</summary>
        public const int MostPacked = 20;
        /// <summary>The two cyan arcs round Gojo: radius (cells) and where each starts (degrees); both sweep ArcSweep degrees.</summary>
        public static readonly float[] ArcRadius = { 1.1f, 1.4f }, ArcFrom = { 30f, 210f };
        public const float ArcSweep = 200f;

        /// <summary>Camera shakes: as it opens, two rumbles 1 s and 2 s into the hold, and the implosion.</summary>
        public const float OpenShake = 0.03f, RumbleShake = 0.02f, SecondRumbleShake = 0.025f, BurstShake = 0.15f;

        // The preview's script, the sketch's "group": Gojo stands ScriptDistance cells from the Blue and aims
        // ScriptAim degrees at it; the pawns stand at (degrees from the aim, cells from the centre). The
        // last one stands outside the pull and is not moved. The sketch's pulled rock and rifle are not scripted.
        public const float ScriptAim = 0f, ScriptDistance = 8f;
        public static readonly float[] ScriptPawnDegrees = { 60f, 200f, -70f, 150f }, ScriptPawnFrom = { 2.5f, 3.6f, 3.2f, 5f };

        public static float OpenAt => Raise;
        public static float FullAt => Raise + Grow;
        public static float ImplodeAt(float hold) => FullAt + hold;
        public static float BurstAt(float hold) => ImplodeAt(hold) + Rush;
        public static float Duration(float hold) => BurstAt(hold) + Tail;
        public static float FirstRumbleAt => FullAt + 1f;
        public static float SecondRumbleAt => FullAt + 2f;

        /// <summary>The ball growing, 0 to 1 over Grow s from the open.</summary>
        public static float Grown(float s) => Smooth((s - OpenAt) / Grow);

        /// <summary>1 until the implosion's rush-in, then down to 0 over Rush s.</summary>
        public static float Shrink(float s, float hold) => s >= ImplodeAt(hold) ? 1f - Smooth((s - ImplodeAt(hold)) / Rush) : 1f;

        /// <summary>0 until 0.6 s before the rush-in, 1 at it: the last moments speed up.</summary>
        public static float Late(float s, float hold) => Smooth((s - (ImplodeAt(hold) - 0.6f)) / 0.6f);

        /// <summary>The bubble's turn in radians (negative is clockwise), faster late and much faster in the rush-in.</summary>
        public static float Swirl(float s, float hold)
        {
            float late = Late(s, hold), implode = ImplodeAt(hold);
            return -(s * 3.2f + late * late * 4f) - (s >= implode ? (s - implode) * 30f : 0f);
        }

        /// <summary>Seconds the pull has run: from the full ball to the rush-in.</summary>
        public static float PullSeconds(float s, float hold) => Mathf.Max(0f, Mathf.Min(s, ImplodeAt(hold)) - FullAt);

        /// <summary>
        /// The sketch's pull, the preview's script: a pawn <paramref name="r0"/> cells from the centre after
        /// <paramref name="t"/> s of pull. The speed is <paramref name="speed"/> at the core edge and falls
        /// linearly to 0 at the pull radius (Gravity Well's rule for body size 1), so the distance closes
        /// exponentially; it stops at the core.
        /// </summary>
        public static float Pulled(float r0, float t, float pullR, float coreR, float speed)
        {
            if (r0 >= pullR || t <= 0f || r0 <= coreR) return r0;
            float k = speed / (pullR - coreR);
            return Mathf.Max(coreR, pullR - (pullR - r0) * Mathf.Exp(k * t));
        }

        /// <summary>Seconds of pull before a pawn that started <paramref name="r0"/> cells out reaches the core; infinity outside the pull.</summary>
        public static float ReachCore(float r0, float pullR, float coreR, float speed)
        {
            if (r0 >= pullR) return float.PositiveInfinity;
            if (r0 <= coreR) return 0f;
            return Mathf.Log((pullR - coreR) / (pullR - r0)) / (speed / (pullR - coreR));
        }

        /// <summary>How far Gojo's arm is out, 0 to 1: up over Raise s, down 0.4 s after the ball is full.</summary>
        public static float ArmOut(float s) => Smooth(s / Raise) * (1f - Smooth((s - FullAt - 0.4f) / 0.3f));

        /// <summary>The blue light at the hand, 0 to 1: up with the arm, gone 0.3 s after the ball is full.</summary>
        public static float HandLight(float s) => Smooth(s / Raise) * (1f - Smooth((s - FullAt) / 0.3f));

        /// <summary>The arcs round Gojo, 0 to 1: in from 0.1 s, out over 0.25 s once the ball is full.</summary>
        public static float Arcs(float s)
        {
            if (s < 0.1f) return 0f;
            return s < FullAt ? Smooth((s - 0.1f) / 0.15f) : 1f - Mathf.Clamp01((s - FullAt) / 0.25f);
        }

        /// <summary>
        /// The blue tint of Gojo's body in the sketch, 0 to 0.45 (with the hand light). The game's to draw on
        /// the real pawn, if at all; the picture does not.
        /// </summary>
        public static float CasterLight(float s) => 0.45f * Smooth(s / Raise) * (1f - Smooth((s - FullAt) / 0.5f));

        /// <summary>
        /// The blue tint of a pulled pawn in the sketch, 0 to 0.55: stronger nearer the centre, while the
        /// Blue is open. The game's to draw on the real pawn, if at all; the picture does not.
        /// </summary>
        public static float PawnLight(float s, float hold, float distance, float pullR)
        {
            if (s < OpenAt || s >= BurstAt(hold)) return 0f;
            return 0.55f * (1f - Mathf.Clamp01(distance / (pullR + 1.5f))) * Grown(s) * Shrink(s, hold);
        }

        /// <summary>
        /// How far a pawn held in the core since <paramref name="heldAt"/> is lifted in the sketch (cells
        /// drawn): up over 0.25 s, dropped to its feet over 0.15 s at the implosion. The game's to apply
        /// to the real pawn's draw position, if at all; the picture does not.
        /// </summary>
        public static float HeldLiftAt(float s, float heldAt, float hold)
        {
            if (s < heldAt) return 0f;
            float age = s - BurstAt(hold);
            return age < 0f ? HeldLift * Smooth((s - heldAt) / 0.25f) : HeldLift * (1f - Smooth(age / 0.15f));
        }

        /// <summary>
        /// The white flash of a pawn held in the core at the implosion in the sketch, 0 to 0.7, gone in
        /// 0.25 s. The game's to draw on the real pawn, if at all; the picture does not.
        /// </summary>
        public static float HeldFlash(float s, float hold)
        {
            float age = s - BurstAt(hold);
            return age < 0f ? 0f : 0.7f * (1f - Mathf.Clamp01(age / 0.25f));
        }

        /// <summary>Piece <paramref name="i"/> of <see cref="Pieces"/>: slabs first, then planks, then chips.</summary>
        public static BluePiece Piece(int i, float hold, float pullR, float coreR)
        {
            var kind = i < Slabs ? BluePieceKind.Slab : i < Slabs + Planks ? BluePieceKind.Plank : BluePieceKind.Chip;
            float born = FullAt + 0.1f + hold * TearShare * (i * 7 % Pieces) / Pieces, rise = 0.6f + 0.4f * Rand(i + 1100), orbit = 0.6f + 0.8f * Rand(i + 1110);
            float size = kind == BluePieceKind.Slab ? 0.45f + 0.4f * Rand(i + 1160)
                : kind == BluePieceKind.Plank ? 0.65f + 0.3f * Rand(i + 1160) : 0.14f + 0.1f * Rand(i + 1160);
            return new BluePiece
            {
                Index = i, Kind = kind, Born = born, Rise = rise, Orbit = orbit, PackedAt = born + rise + orbit,
                R0 = coreR + 0.5f + (pullR - coreR - 0.7f) * Rand(i + 1120), A0 = Rand(i + 1130) * Mathf.PI * 2f,
                OrbitR = coreR * (1.1f + 0.5f * Rand(i + 1140)), OrbitH = BallUp + (Rand(i + 1150) - 0.5f) * 0.9f, Size = size,
            };
        }

        /// <summary>
        /// Where piece <paramref name="g"/> is at <paramref name="s"/>: <paramref name="r"/> cells from the
        /// centre in direction <paramref name="q"/> (radians), <paramref name="h"/> cells up. On the floor
        /// until born, then spiralling up and in to its orbit, orbiting until packed. At the rush-in
        /// everything still flying goes to the centre.
        /// </summary>
        public static BluePieceState PieceAt(in BluePiece g, float s, float hold, out float r, out float q, out float h)
        {
            r = g.R0; q = g.A0; h = 0f;
            if (s < g.Born) return BluePieceState.Ground;
            float implode = ImplodeAt(hold), m = Mathf.Min(s, implode);
            float u = Mathf.Clamp01((m - g.Born) / g.Rise), e = Smooth(u);
            r = g.R0 + (g.OrbitR - g.R0) * e; q = g.A0 - 2.6f * u * u; h = g.OrbitH * Mathf.Pow(e, 1.5f);
            if (u >= 1f)
            {
                float v = m - g.Born - g.Rise, w = 3.2f * 1.3f / g.OrbitR;
                q = g.A0 - 2.6f - w * v; r = g.OrbitR * (1f - 0.25f * Mathf.Clamp01(v / g.Orbit)); h = g.OrbitH + 0.08f * Mathf.Sin(v * 7f + g.Index);
            }
            if (m >= g.PackedAt) { r = 0f; h = BallUp; return BluePieceState.Packed; }
            if (s >= implode)
            {
                float k = 1f - Smooth((s - implode) / Rush);
                if (k <= 0f) { r = 0f; h = BallUp; return BluePieceState.Packed; }
                r *= k; q -= (1f - k) * 3f; h = BallUp + (h - BallUp) * k;
            }
            return BluePieceState.Flying;
        }
    }
}
