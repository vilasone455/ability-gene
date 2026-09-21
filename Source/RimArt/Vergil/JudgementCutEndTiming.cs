using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>One of Judgement Cut End's cuts, relative to the caster: a chord across the whole ring.</summary>
    public struct CutEndCut
    {
        public VergilChord Line;
        /// <summary>Which of the 14 is drawn when, 0 first.</summary>
        public int Order;
    }

    /// <summary>
    /// Judgement Cut End's timing and geometry: when each phase runs, where each cut crosses the ring
    /// and in which order, and the pieces the ring breaks into along them. Seconds in, geometry out, no
    /// drawing and no map. The port of Tools/VfxLab/web/sketches/vergil-judgement-cut-end.js; the
    /// constants are that sketch's default values.
    ///
    /// The ability behind it is proposed and not agreed, and every number is a placeholder that will
    /// become an XML field: no target, warm-up 1.0 s, every hostile within 10 cells with line of sight
    /// to the caster is marked and stunned, the caster is gone for 1.5 s and cannot be targeted, comes
    /// back kneeling and sheathes for 0.8 s, and on the click each marked pawn takes 4 hits of 10 Cut.
    /// Cooldown 1 day.
    ///
    /// The preview's script: the first cuts each pass through one of the sketch's marked raiders, whose
    /// places are fixed points round the caster here. They are points the cuts aim at, not pawns.
    /// </summary>
    public static class JudgementCutEndTiming
    {
        /// <summary>Decided values, the sketch's constants.</summary>
        public const float Lead = 0.5f, Tail = 1.8f, Sweep = 0.07f, FirstCut = 0.1f, LastCut = 0.15f, Dark = 0.55f, Front = 0.15f, CutsGone = 0.25f;
        /// <summary>Pane edges: their width, how square to the light an edge must be to show, how far a pane sits off its place before the click, its size when gone.</summary>
        public const float EdgeWidth = 0.07f, EdgeFacing = 0.3f, AjarLeast = 0.03f, AjarMost = 0.08f, FallTo = 0.7f;
        public const int Motes = 40;
        /// <summary>A pawn's chest is drawn this far north of its feet.</summary>
        public const float Chest = 0.3f;

        /// <summary>The sketch's panel defaults, the ones the preview plays.</summary>
        public const float Radius = 10f, Push = 0.3f, Warm = 1f, Gone = 1.5f, Sheathe = 0.8f, Fade = 0.6f;
        public const int Cuts = 14;

        /// <summary>The sketch's raiders as (degrees from the caster, cells) where they stand when the caster vanishes.</summary>
        private static readonly Vector2[] Raiders =
        {
            new Vector2(25f, 3.2f), new Vector2(80f, 5.6f), new Vector2(-35f, 6.4f),
            new Vector2(140f, 4.3f), new Vector2(-110f, 7.4f), new Vector2(172f, 8.4f),
        };

        public static float CastAt => Lead;
        public static float VanishAt => Lead + Warm;
        public static float BackAt => VanishAt + Gone;
        public static float ClickAt => BackAt + Sheathe;
        public static float Duration => ClickAt + Tail;

        public static Vector2 Polar(float degrees, float cells) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad) * cells, Mathf.Sin(degrees * Mathf.Deg2Rad) * cells);

        /// <summary>When cut <paramref name="order"/> of <paramref name="count"/> starts to be drawn.</summary>
        public static float StartOf(int order, int count) =>
            VanishAt + FirstCut + order / (float)Mathf.Max(1, count - 1) * (Gone - FirstCut - LastCut - Sweep);

        private static string laidFor = string.Empty;
        private static List<CutEndCut> laid = new List<CutEndCut>();

        /// <summary>
        /// The cuts, relative to the caster. Each runs across the line from the caster to its point, within
        /// 40 degrees, so none crosses the caster's cell; the first ones pass through a marked raider's chest.
        /// </summary>
        public static List<CutEndCut> Layout(float radius, int count)
        {
            string key = radius + "|" + count;
            if (laidFor == key) return laid;
            var marked = new List<Vector2>();
            foreach (Vector2 r in Raiders) if (r.y <= radius) marked.Add(Polar(r.x, r.y));

            var cuts = new List<CutEndCut>(count);
            var sort = new List<(float key, int k)>(count);
            for (int k = 0; k < count; k++)
            {
                float far = (0.2f + 0.5f * VergilTiming.Rand(k * 5 + 3)) * radius, turn = (k * 137f + VergilTiming.Rand(k * 3 + 9) * 60f) * Mathf.Deg2Rad;
                Vector2 q = k < marked.Count
                    ? new Vector2(marked[k].x + (VergilTiming.Rand(k * 13 + 1) - 0.5f) * 0.2f, marked[k].y + Chest)
                    : new Vector2(Mathf.Cos(turn) * far, Mathf.Sin(turn) * far);
                float angle = Mathf.Atan2(q.y, q.x) + Mathf.PI / 2f + (VergilTiming.Rand(k * 11 + 4) - 0.5f) * 80f * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                VergilTiming.Chord(q, d, radius, out Vector2 a, out Vector2 b);
                cuts.Add(new CutEndCut { Line = new VergilChord { Q = q, D = d, A = k % 2 == 0 ? a : b, B = k % 2 == 0 ? b : a } });
                sort.Add((VergilTiming.Rand(k + 50), k));
            }
            sort.Sort((m, n) => m.key != n.key ? m.key.CompareTo(n.key) : m.k.CompareTo(n.k));
            for (int i = 0; i < sort.Count; i++)
            {
                CutEndCut c = cuts[sort[i].k];
                c.Order = i;
                cuts[sort[i].k] = c;
            }
            laid = cuts;
            laidFor = key;
            return laid;
        }

        /// <summary>The pieces the ring breaks into along every cut.</summary>
        public static List<VergilPiece> Pieces(float radius, int count)
        {
            var lines = new List<VergilChord>(count);
            foreach (CutEndCut c in Layout(radius, count)) lines.Add(c.Line);
            return VergilTiming.Shatter(radius, lines);
        }
    }
}
