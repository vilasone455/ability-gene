using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Judgement Cut's timing and geometry: when each phase runs, where each cut crosses the ball, and
    /// the pieces the ball breaks into when it closes. Seconds in, geometry out, no drawing and no map.
    /// The port of Tools/VfxLab/web/sketches/vergil-judgement-cut.js; the constants are that sketch's
    /// default values.
    ///
    /// The ability behind it is proposed and not agreed, and every number is a placeholder that will
    /// become an XML field: a cell within 18 with line of sight, a radius 1.9 sphere opens on it, and
    /// every pawn inside, allies included, takes 5 hits of 7 Cut at 50 % armour penetration over
    /// 0.5 s. Cooldown 12 s. Nothing flies from the caster to the cell; the cut happens there.
    /// </summary>
    public static class JudgementCutTiming
    {
        /// <summary>Decided values, the sketch's constants.</summary>
        public const float Lead = 0.4f, Tail = 1.2f, Open = 0.08f, Close = 0.16f, Sweep = 0.04f, Hold = 0.07f, Gone = 0.06f;
        public const float DrawArc = 0.12f, RingFade = 0.4f;
        public const int Hits = 5, Motes = 12, Breaks = 5, Puffs = 10;
        /// <summary>How far a cut pokes out of the ball, least and most.</summary>
        public const float OverLeast = 0.12f, OverMost = 0.3f;

        /// <summary>The sketch's panel defaults, the ones the preview plays.</summary>
        public const float Aim = 20f, Distance = 8f, Radius = 1.9f, Warm = 0.6f, Burst = 0.5f;
        public const int Cuts = 24;

        /// <summary>The draw arc at the caster: its radius, how far round it sweeps, and its points.</summary>
        public const float ArcRadius = 0.95f, ArcSpread = 110f;
        public const int ArcPoints = 11;
        /// <summary>A pawn stands a little north of its cell, and so does the ball over its target.</summary>
        public const float Chest = 0.3f;

        public static float CastAt => Lead;
        public static float OpenAt(float warm) => Lead + warm;
        public static float CloseAt(float warm, float burst) => OpenAt(warm) + burst;
        public static float Duration(float warm, float burst) => CloseAt(warm, burst) + Close + Tail;

        /// <summary>One damage tick's share of the burst.</summary>
        public static float Tick(float burst) => burst / Hits;

        /// <summary>
        /// Cut <paramref name="k"/>, relative to the centre of the ball: a straight chord right across
        /// it that pokes out of the rim at both ends.
        /// </summary>
        public static VergilChord CutLine(int k, float radius)
        {
            float turn = (k * 137f + VergilTiming.Rand(k + 5) * 50f) * Mathf.Deg2Rad;
            float off = Mathf.Sqrt(VergilTiming.Rand(k * 3 + 1)) * radius * 0.6f;
            var q = new Vector2(Mathf.Cos(turn) * off, Mathf.Sin(turn) * off);
            float angle = (k * 67f + VergilTiming.Rand(k * 7 + 2) * 60f) * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float over = radius + OverLeast + (OverMost - OverLeast) * VergilTiming.Rand(k * 11 + 3);
            VergilTiming.Chord(q, d, over, out Vector2 a, out Vector2 b);
            return new VergilChord { Q = q, D = d, A = k % 2 == 0 ? a : b, B = k % 2 == 0 ? b : a };
        }

        // The pieces the ball breaks into: the disc split along its last few cuts. Worked out, with the
        // meshes, only when the radius or the cut count changes, so the meshes are static and may be
        // drawn many times in a frame.
        private static string builtKey = string.Empty;
        private static List<VergilPiece> built = new List<VergilPiece>();

        /// <summary>The pieces a ball of <paramref name="radius"/> with <paramref name="count"/> cuts breaks into.</summary>
        public static List<VergilPiece> Pieces(float radius, int count)
        {
            string key = radius + "|" + count;
            if (builtKey == key) return built;
            var lines = new List<VergilChord>();
            for (int i = 0; i < Mathf.Min(Breaks, count); i++) lines.Add(CutLine(count - 1 - i, radius));
            built = VergilTiming.Shatter(radius, lines, 32);
            builtKey = key;
            return built;
        }
    }
}
