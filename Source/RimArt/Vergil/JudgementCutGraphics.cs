using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.VergilGraphics;
using T = RimArt.JudgementCutTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Judgement Cut: the floor ring at the true radius, lights rising out of the floor inside it
    /// during the warm-up, a thin aura round the caster, the draw arc and a hilt glint at the caster, the ball of cut space opening
    /// on the target cell, the straight chords sweeping across it, the ball breaking into pieces that
    /// fall to its centre, the scars left along the cuts, and the dust off the floor as it closes.
    ///
    /// The port of Tools/VfxLab/web/sketches/vergil-judgement-cut.js. A level disc, level rings and
    /// lines inside one area, so no part needs a per-facing method; the draw arc at the caster lies
    /// flat and turns with the aim. No distortion shader: the dark inside stands in for the warp the
    /// source shows.
    ///
    /// The sketch's stand-in pawns are not ported, and with them go the five damage ticks, the cut
    /// across each chest and the red slits that stay. Those are the ability's, not the drawing's.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class JudgementCutGraphics
    {
        private static readonly Vector2[] arc = new Vector2[T.ArcPoints];

        // One mesh per piece. The pieces only change when the radius or the cut count does, so these
        // are static meshes and may be drawn twice in a frame (a dark face and a lit copy).
        private static List<VergilPiece> meshedFor;
        private static Mesh[] pieceMeshes = new Mesh[0];

        /// <summary>The preview. <paramref name="centre"/> is the chosen cell, which is where the caster stands in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, float seconds, Map map) =>
            Draw(new Vector2(centre.x, centre.z), aimDegrees, T.Distance, T.Radius, T.Cuts, T.Warm, T.Burst, seconds, map);

        /// <summary>
        /// <paramref name="caster"/> is the caster's ground point and <paramref name="aimDegrees"/> the
        /// direction to the target, which lies <paramref name="distance"/> cells along it.
        /// </summary>
        public static void Draw(Vector2 caster, float aimDegrees, float distance, float radius, int count, float warm, float burst,
            float seconds, Map map)
        {
            float castAt = T.CastAt, openAt = T.OpenAt(warm), closeAt = T.CloseAt(warm, burst);
            if (seconds < 0f || seconds >= T.Duration(warm, burst)) return;

            Vector2 aim = Turn(aimDegrees);
            Vector2 target = caster + aim * distance;
            if (!Shown(target, map)) return;
            Begin(target);

            var ballCentre = new Vector2(target.x, target.y + T.Chest);
            float since = seconds - openAt, sinceClose = seconds - closeAt;
            float size = since < 0f ? 0f
                : sinceClose < 0f ? Smooth(since / T.Open) * (1f + 0.08f * Mathf.Sin(Mathf.Clamp01(since / (T.Open * 2f)) * Mathf.PI))
                : 1f - Smooth(sinceClose / T.Close);
            float covered = since < 0f ? 0f : sinceClose < 0f ? Smooth(since / T.Open) : 1f - Smooth(sinceClose / T.Close);

            // --- the floor: the true radius, and the ball's own shadow while it hides it ----------------
            if (seconds >= castAt)
            {
                float grown = Smooth((seconds - castAt) / (warm * 0.7f));
                float left = 1f - Smooth((sinceClose - T.Close) / T.RingFade);
                PaperBombGraphics.RingAt(target, radius * grown, Fade(Blue, 0.7f * left * (1f - covered)), Floor + 0.02f);
                Sprite(target, radius * 2.5f, radius * 1.7f, Fade(Void, 0.4f * covered), soft, Floor + 0.03f);
            }

            // --- the scars: every second cut leaves the middle half of its chord on the floor ------------
            if (sinceClose >= 0f)
                for (int k = 0; k < count; k += 2)
                {
                    VergilChord c = T.CutLine(k, radius);
                    Vector2 a = target + c.A + (c.B - c.A) * 0.25f, b = target + c.A + (c.B - c.A) * 0.75f;
                    Streak(a, b, 0.05f, Fade(Void, 0.45f - 0.2f * Smooth(sinceClose / T.Tail)), solid, Floor + 0.01f, 4);
                }

            // --- the warm-up: short lights rise out of the floor inside the ring --------------------------
            if (seconds >= castAt && since < 0f)
            {
                float w = (seconds - castAt) / warm;
                for (int i = 0; i < T.Motes; i++)
                {
                    float v = (w * 1.8f + Rand(i)) % 1f;
                    float angle = Rand(i + 40) * Mathf.PI * 2f, far = Mathf.Sqrt(Rand(i + 80)) * radius * 0.9f;
                    var foot = new Vector2(target.x + Mathf.Cos(angle) * far, target.y + Mathf.Sin(angle) * far + v * 0.6f);
                    Streak(foot, new Vector2(foot.x, foot.y + 0.28f), 0.05f,
                        Fade(Ice, 0.85f * w * Mathf.Max(0f, Mathf.Sin(v * Mathf.PI))), whiteGlow, Overhead + 0.01f, 3);
                }
                Sprite(ballCentre, radius * 1.2f * w, radius * 1.2f * w, Fade(Deep, 0.35f * w), glow, Overhead + 0.005f);
            }

            // --- the warm-up at the caster: a thin aura stands up, and drops as the ball opens -----------
            if (seconds >= castAt && sinceClose < 0f)
                GokuGraphics.Aura(caster, seconds, 0.5f * Mathf.Clamp01((seconds - castAt) / warm) * (1f - Mathf.Clamp01(since / 0.3f)), Blue);

            // --- the draw, at the caster: one flat light arc toward the target and a glint at the hilt ----
            if (since >= 0f && since < T.DrawArc)
            {
                float u = since / T.DrawArc;
                for (int j = 0; j < T.ArcPoints; j++)
                {
                    float a = (aimDegrees + (j / (float)(T.ArcPoints - 1) - 0.5f) * T.ArcSpread) * Mathf.Deg2Rad;
                    arc[j] = new Vector2(caster.x + Mathf.Cos(a) * T.ArcRadius, caster.y + T.Chest + Mathf.Sin(a) * T.ArcRadius);
                }
                ArcCut(arc, Mathf.Clamp01(u * 2.5f), 1f - u * u, 0.05f, hot: 1f);   // hot: light only, no dark slit
                Glint(Hilt(caster), 0.35f * (1f - u) + 0.1f, 1f - u, Snow, 20f);
            }

            // --- the ball ----------------------------------------------------------------------------------
            if (size > 0f) Sphere(ballCentre, radius * size, seconds, sinceClose < 0f ? 1f : size, sinceClose < 0f ? 0.45f : 0f);

            // --- it closes: the ball breaks along its last cuts and the pieces fall to its centre ----------
            if (sinceClose >= 0f && sinceClose < T.Close)
            {
                List<VergilPiece> pieces = T.Pieces(radius, count);
                Meshes(pieces);
                float u = sinceClose / T.Close, pull = Smooth(u), scale = 0.92f * (1f - 0.8f * u);
                for (int i = 0; i < pieces.Count; i++)
                {
                    Vector2 at = ballCentre + pieces[i].Centre * (1f - pull);
                    float turn = (Rand(i + 7) - 0.5f) * 70f * u;
                    DrawMesh(pieceMeshes[i], at, Overhead + 0.016f, scale, scale, -turn, Fade(Void, 0.55f * (1f - 0.4f * u)), solid);
                    DrawMesh(pieceMeshes[i], at, Overhead + 0.0165f, scale, scale, -turn, Fade(Blue, 0.3f * (1f - u)), whiteGlow);
                }
            }

            // --- the cuts: thin straight chords across the ball, a few on screen at a time -----------------
            if (since >= 0f && sinceClose < T.Close)
                for (int k = 0; k < count; k++)
                {
                    float age = since - k / (float)count * Mathf.Max(0.05f, burst - T.Hold - T.Sweep);
                    if (age < 0f || age >= T.Sweep + T.Hold + T.Gone) continue;
                    VergilChord c = T.CutLine(k, radius);
                    float grown = Mathf.Clamp01(age / T.Sweep);
                    // Thin while it is still short, or it shows as a leaf rather than a cut.
                    Cut(ballCentre + c.A, ballCentre + c.B, grown, 1f - Mathf.Clamp01((age - T.Sweep - T.Hold) / T.Gone),
                        0.03f * (0.35f + 0.65f * grown), Mathf.Clamp01(1f - age / 0.06f));
                }

            // --- it closes: a glint, a thin ring front, and the guard meeting the scabbard at the caster ---
            if (sinceClose >= 0f && sinceClose < 0.35f)
            {
                float u = sinceClose / 0.35f;
                Glint(ballCentre, 0.25f + 0.6f * Mathf.Clamp01(sinceClose / T.Close) * (1f - u), 1f - u, Snow, 45f);
                if (sinceClose >= T.Close)
                    PaperBombGraphics.RingAt(ballCentre, radius * (0.2f + 1.1f * Smooth((sinceClose - T.Close) / 0.2f)), Fade(Ice, 0.6f * (1f - u)), Overhead + 0.03f, false, whiteGlow);
                float home = Mathf.Clamp01(sinceClose / 0.15f);
                Glint(Hilt(caster), 0.3f * (1f - home), 1f - home, Snow, 20f);
            }

            // --- dust off the floor round the true radius as it closes -------------------------------------
            float dusty = (sinceClose - T.Close * 0.5f) / 0.6f;
            if (dusty >= 0f && dusty < 1f)
                for (int i = 0; i < T.Puffs; i++)
                {
                    float angle = (i / (float)T.Puffs + Rand(i + 60) * 0.08f) * Mathf.PI * 2f;
                    float far = radius * 0.85f + (0.3f + 0.5f * Rand(i + 70)) * dusty;
                    var at = new Vector2(target.x + Mathf.Cos(angle) * far, target.y + Mathf.Sin(angle) * far * 0.9f + dusty * 0.25f);
                    Sprite(at, 0.45f + 0.6f * dusty, 0.38f + 0.5f * dusty,
                        Fade(Grit, 0.4f * Mathf.Max(0f, Mathf.Sin(dusty * Mathf.PI))), soft, Overhead + 0.004f);
                }
        }

        /// <summary>Where the caster's scabbard hilt sits: the draw and the sheathe both glint here.</summary>
        private static Vector2 Hilt(Vector2 caster) => new Vector2(caster.x - 0.02f, caster.y + 0.46f);

        /// <summary>One triangle-fan mesh per piece, rebuilt only when the piece list itself changes.</summary>
        private static void Meshes(List<VergilPiece> pieces)
        {
            if (ReferenceEquals(meshedFor, pieces)) return;
            pieceMeshes = new Mesh[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                Vector2[] points = pieces[i].Points;
                int n = points.Length;
                var vertices = new Vector3[n + 1];
                var indices = new int[n * 3];
                vertices[0] = Vector3.zero;
                for (int j = 0; j < n; j++) vertices[j + 1] = new Vector3(points[j].x, 0f, points[j].y);
                for (int j = 0; j < n; j++)
                {
                    // The points run counter-clockwise in map coordinates, which is clockwise on screen.
                    indices[j * 3] = 0;
                    indices[j * 3 + 1] = 1 + j;
                    indices[j * 3 + 2] = 1 + (j + 1) % n;
                }
                var mesh = new Mesh { name = "Vergil judgement cut piece " + i, vertices = vertices, triangles = indices };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                pieceMeshes[i] = mesh;
            }
            meshedFor = pieces;
        }
    }
}
