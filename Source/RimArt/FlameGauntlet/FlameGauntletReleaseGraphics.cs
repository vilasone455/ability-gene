using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.FlameGauntletGraphics;
using T = RimArt.FlameReleaseTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Release: the fist comes forward and the heat glow slides into it, a red outline shows
    /// the cone, then a flash at the knuckles, five licks of flame dive onto the floor, and a front
    /// of fire runs out along the floor lighting each cone cell as it passes (a flash, embers, the
    /// cell's fire flaring 1.5x and settling, a low sheet of flame), with soot rising behind it and
    /// pale exhaust out of the elbow vents. Under the minimum heat: a dull red flash, a pale cough
    /// and three sparks, and nothing lights. The port of Tools/VfxLab/web/sketches/flame-gauntlet-release.js.
    ///
    /// In game (DrawFires false) vanilla fires appear in the cells as they light: each cell's picture
    /// fire is drawn with its flare and fades out over FlareFor, with no scorch, and no burning pawn
    /// is drawn. The camera shake at the release is the kit's. The stand-in pawns are not drawn; the
    /// tally is drawn by the preview (a gizmo in game).
    /// </summary>
    public static class FlameGauntletReleaseGraphics
    {
        /// <summary>The preview: wearer to the chosen cell along the aim, cells; the enemy's row.</summary>
        public const float ScriptBack = 3f;
        public const int ScriptEnemyRow = 4;
        private static readonly FlameReleaseShot preview = new FlameReleaseShot();
        /// <summary>The lit columns of each row, index = row.</summary>
        private static float[] rowLo = new float[16], rowHi = new float[16];

        /// <summary>
        /// The sketch's cone for a preview: the wearer <see cref="ScriptBack"/> cells behind
        /// <paramref name="centre"/>, the cells of <see cref="FlameReleaseTiming.Pattern"/> at their
        /// aim-frame points, one lit per heat point from the minimum up, and a pawn in the row-4
        /// centre cell when <paramref name="enemy"/>.
        /// </summary>
        internal static FlameReleaseShot Script(Vector2 centre, float aimDegrees, float startHeat, bool enemy, FlameReleaseShot into = null)
        {
            FlameReleaseShot shot = into ?? new FlameReleaseShot();
            var f = new ChainSickleFrame(aimDegrees, Vector2.zero);
            shot.Caster = f.Ground(centre, -ScriptBack, 0f);
            shot.Aim = aimDegrees;
            shot.StartHeat = startHeat;
            shot.DrawFires = true;
            shot.CasterAt = null;
            shot.Cells.Clear();
            shot.PawnCell = -1;
            var pattern = T.Pattern(shot.Length, shot.Width);
            for (int i = 0; i < pattern.Count; i++)
            {
                Vector2Int c = pattern[i];
                shot.Cells.Add(new FlameConeCell { At = f.Ground(shot.Caster, c.x, c.y), Row = c.x, Across = c.y });
                if (enemy && c.x == ScriptEnemyRow && c.y == 0) shot.PawnCell = i;
            }
            shot.Lit = startHeat >= T.ScriptMinHeat ? Mathf.Min(shot.Cells.Count, Mathf.FloorToInt(startHeat)) : 0;
            return shot;
        }

        /// <summary>The preview: the sketch's cone round the chosen cell, and the heat tally under the wearer's feet.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, float startHeat, bool enemy, float seconds, Map map)
        {
            FlameReleaseShot shot = Script(new Vector2(centre.x, centre.z), aimDegrees, startHeat, enemy, preview);
            if (seconds < 0f || seconds >= T.End(shot)) return;
            Draw(shot, seconds, map);
            Tally(shot.Caster, T.HeatAt(shot, seconds), shot.MaxHeat, shot.OverheatAt, shot.Aim, 1f);
        }

        /// <param name="weapon">Draw the gauntlet in the hand. In game it is left off once the wearer's cast job is over.</param>
        public static void Draw(FlameReleaseShot shot, float s, Map map, bool weapon = true)
        {
            float end = T.End(shot);
            if (s < 0f || s >= end || !Shown(shot.Caster, map)) return;
            Begin(shot.Caster);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            var f = new ChainSickleFrame(shot.Aim, sun);
            Vector2 caster = shot.CasterAt ?? shot.Caster, side = new Vector2(-f.sa, f.ca);
            float lead = FlameGauntletTiming.Lead, go = T.Go, speed = T.Speed(shot), stop = T.Stop(shot), result = T.Result(shot);
            int lit = shot.Lit;
            float reach = T.Reach(shot);
            int last = Rows(shot);

            float heat = T.HeatAt(shot, s);
            float over = heat >= shot.OverheatAt ? 1f : 0f;

            // The hand: forward from the wind-up, a recoil at the release, back at the result.
            float raise = Smooth((s - lead) / T.Windup), closing = Smooth((s - result) / T.Close);
            float fwd = raise * (1f - closing), recoil = lit > 0 ? 0.08f * Bump((s - go) / 0.25f) : 0f;
            Vector2 hand = f.Ground(caster, Mathf.Lerp(0.12f, 0.34f, fwd) - recoil, Mathf.Lerp(0.18f, 0.08f, fwd));
            float lean = lit > 0 ? fwd * (1f - Smooth((s - go - T.Spread) / 0.3f)) : fwd * (1f - Smooth((s - go) / 0.2f));

            // Floor first: the cone outline while it matters.
            float live = Smooth((s - lead) / 0.2f) * (1f - Smooth((s - result) / 0.3f));
            if (live > 0f) Outline(f, caster, shot.Length, shot.Width, Fade(Ember, 0.45f * live));

            // Every lit cell: a flash, a burst of embers, its fire flaring tall and settling, and the
            // low sheet of flame that joins it to its neighbours for a moment. The pawn's cell burns
            // under the pawn layer so the pawn stays in front of its own fire.
            for (int i = 0; i < lit; i++)
            {
                float age = s - T.EruptAt(shot, i);
                if (age < 0f) continue;
                Vector2 c = shot.Cells[i].At;
                bool under = i == shot.PawnCell;
                float flare = T.Flare * (1f - Smooth(age / T.FlareFor));
                if (shot.DrawFires) FireCell(c, s, Clamp01(age / 0.1f), i + 1, under ? PawnLayer - 0.02f : Y + 0.03f, flare);
                else FireCell(c, s, Clamp01(age / 0.1f), i + 1, Y + 0.03f, flare, false, 1f - Smooth(age / T.FlareFor));
                if (age < T.Sheet) Sheet(c, s, 1f - Smooth(age / T.Sheet), i + 1, under ? PawnLayer - 0.025f : Y + 0.025f);
                if (age < 0.25f) Sprite(c, 1.3f, 1.1f, Fade(Core, 0.45f * (1f - age / 0.25f)), glow, Floor + 0.025f);
                if (age < 0.5f)
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * 0.785f + Rand(i * 9 + k) * 0.5f, d = age * (1.2f + Rand(i * 9 + k + 50)), h = age * 2.4f - age * age * 4.5f;
                        var pt = new Vector2(c.x + Mathf.Cos(a) * d * 0.5f, c.y + Mathf.Sin(a) * d * 0.35f + Mathf.Max(0f, h) * Lift);
                        float fade = 1f - age / 0.5f;
                        Sprite(pt, 0.16f, 0.14f, Fade(Flame, 0.3f * fade), glow, Y + 0.119f);
                        Sprite(pt, 0.08f, 0.08f, Fade(Core, 0.95f * fade), glow, Y + 0.12f);
                    }
            }
            if (lit > 0) WaveSmoke(f, caster, go + T.JetLand, speed, T.Start, reach, rowLo, rowHi, last, s);

            // The pawn in the cone (previews): on fire once its cell lights.
            if (shot.DrawFires && shot.PawnCell >= 0 && shot.PawnCell < lit)
                BurningPawn(shot.Cells[shot.PawnCell].At, s, Clamp01((s - T.EruptAt(shot, shot.PawnCell)) / 0.2f), 9);

            Overheating(caster, s, over);

            FlameGauntletPose g = weapon
                ? Gauntlet(hand, shot.Aim, heat, shot.MaxHeat, shot.OverheatAt, s, sun, strength, 0f, lean)
                : Pose(Raised(hand, HandH), shot.Aim);

            // The wind-up: the glow at the knuckles, and the light it throws on the floor under the fist.
            float charge = raise * (1f - Smooth((s - go) / (lit > 0 ? 0.08f : 0.2f))) * (1f + 0.12f * Mathf.Sin(s * 41f));
            if (charge > 0f)
            {
                Sprite(g.Fist, 0.5f * charge + 0.2f, 0.42f * charge + 0.18f, Fade(Flame, 0.55f * charge), glow, Y + 0.15f);
                Sprite(g.Fist, 0.22f * charge, 0.2f * charge, Fade(Core, 0.9f * charge), glow, Y + 0.151f);
                Sprite(hand, 1.1f * charge + 0.2f, 0.95f * charge + 0.2f, Fade(Flame, 0.25f * charge), glow, Floor + 0.02f);
            }

            float since = s - go;
            if (lit > 0)
            {
                // The release: the licks from the fist, the vents dumping heat, the knuckles smoking after.
                Jet(hand, f.Ground(caster, T.Start, 0f), side, since, s);
                Exhaust(g, since);
                Wisp(g.Fist, since - 0.3f);
                // The wave: from where the licks land to the end of the lit cells, then it sinks.
                float D = Mathf.Min(reach, T.Start + (s - go - T.JetLand) * speed);
                float amount = s < go + T.JetLand ? 0f : 1f - Smooth((s - stop) / T.Sink);
                if (amount > 0f) Wave(f, caster, D, SpanAt(rowLo, rowHi, last, D, T.Start), s, amount, T.Start, T.Bow);
            }
            else
            {
                // Too cold: the knuckles flash dull red and go dark, a pale puff coughs out of the fist,
                // and three sparks drop off them to the floor and go out. Nothing reaches the cone.
                if (since >= 0f && since < 0.2f) Sprite(g.Fist, 0.4f, 0.35f, Fade(Ember, 0.6f * (1f - since / 0.2f)), glow, Y + 0.15f);
                Cough(Raised(f.Ground(caster, 0.45f, 0f), HandH), g.D, since);
                for (int k = 0; k < 3; k++)
                {
                    float u = (since - k * 0.05f) / 0.35f;
                    if (u <= 0f || u >= 1f) continue;
                    Vector2 q = f.Ground(caster, 0.4f + 0.15f * u + k * 0.04f, 0.06f * (k - 1));
                    var pt = new Vector2(q.x, q.y + HandH * (1f - u * u) * Lift);
                    Sprite(pt, 0.16f, 0.14f, Fade(Flame, 0.35f * (1f - u)), glow, Y + 0.119f);
                    Sprite(pt, 0.08f, 0.08f, Fade(Core, 0.9f * (1f - u)), glow, Y + 0.12f);
                }
            }
        }

        /// <summary>Fills rowLo/rowHi with the lit columns of each row; returns the last lit row (0 when none).</summary>
        private static int Rows(FlameReleaseShot shot)
        {
            int need = shot.Length + 2;
            if (rowLo.Length < need) { rowLo = new float[need]; rowHi = new float[need]; }
            int last = 0;
            for (int i = 0; i < shot.Lit; i++)
            {
                FlameConeCell c = shot.Cells[i];
                if (c.Row < 0 || c.Row >= rowLo.Length) continue;
                if (c.Row > last)
                {
                    for (int r = last + 1; r <= c.Row; r++) { rowLo[r] = c.Across; rowHi[r] = c.Across; }
                    last = c.Row;
                }
                rowLo[c.Row] = Mathf.Min(rowLo[c.Row], c.Across);
                rowHi[c.Row] = Mathf.Max(rowHi[c.Row], c.Across);
            }
            return last;
        }

        /// <summary>
        /// The true footprint on the floor: the single first cell, then the full-width block from row 2
        /// to the last row, as eight thin quads.
        /// </summary>
        private static void Outline(in ChainSickleFrame f, Vector2 caster, int length, int width, Color colour)
        {
            float half = (width - 1) / 2f + 0.5f, L = Floor + 0.03f;
            Corner[0] = f.Ground(caster, 0.5f, -0.5f);
            Corner[1] = f.Ground(caster, 1.5f, -0.5f);
            Corner[2] = f.Ground(caster, 1.5f, -half);
            Corner[3] = f.Ground(caster, length + 0.5f, -half);
            Corner[4] = f.Ground(caster, length + 0.5f, half);
            Corner[5] = f.Ground(caster, 1.5f, half);
            Corner[6] = f.Ground(caster, 1.5f, 0.5f);
            Corner[7] = f.Ground(caster, 0.5f, 0.5f);
            Corner[8] = Corner[0];
            for (int i = 0; i + 1 < Corner.Length; i++)
            {
                Vector2 a = Corner[i], b = Corner[i + 1], run = b - a;
                float len = run.magnitude;
                if (len < 1e-4f) continue;
                Rect((a + b) / 2f, len, 0.04f, Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg, colour, L);
            }
        }

        private static readonly Vector2[] Corner = new Vector2[9];
    }
}
