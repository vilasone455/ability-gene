using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.VergilGraphics;
using T = RimArt.JudgementCutEndTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Judgement Cut End: the floor ring growing to the true radius, lights rising out of the floor
    /// inside it and a thin aura round the caster during the warm-up; the dark inside the ring while the
    /// caster is gone; 14 chords drawn across the ring one after another, which stay in the air; the pieces
    /// between them showing as panes of glass while the blade goes home; and on the click a glint at the
    /// scabbard mouth, a ring front out to the radius, the cuts going white and every pane sliding out,
    /// turning, shrinking and fading. Scars stay on the floor along the cuts.
    ///
    /// The port of Tools/VfxLab/web/sketches/vergil-judgement-cut-end.js. Chords, level circles and flat
    /// polygons about one point, and no aim, so no part needs a per-facing method. A pane has a lit edge
    /// toward the sun and a dark edge away from it, so its meshes are rebuilt when the sun moves.
    ///
    /// The sketch's stand-ins are not ported: the caster (its hand, the line it thins to, the ghost at the
    /// end of each cut, the kneel), the raiders, the ally and the wall, and with them the glint on each
    /// marked chest, the stun tint and the four hits on the click. Those are pawn drawings or the
    /// ability's. The blue glow under the kneeling caster is light and is drawn.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class JudgementCutEndGraphics
    {
        /// <summary>The scabbard mouth on the caster's left hip, from its feet: the click glints here.</summary>
        private static readonly Vector2 Mouth = new Vector2(-0.3f, 0.46f);

        // A face mesh and a lit and a dark edge mesh per pane. Static meshes, rebuilt only when the radius,
        // the cut count or the light direction (to two decimals) changes.
        private static string builtKey = string.Empty;
        private static List<VergilPiece> pieces = new List<VergilPiece>();
        private static Mesh[] faces = new Mesh[0], lit = new Mesh[0], dim = new Mesh[0];

        /// <summary>The preview. <paramref name="centre"/> is the chosen cell, where the caster stands.</summary>
        public static void DrawPreview(Vector3 centre, float seconds, Map map) =>
            Draw(new Vector2(centre.x, centre.z), T.Radius, T.Cuts, seconds, map);

        public static void Draw(Vector2 o, float radius, int count, float s, Map map)
        {
            if (s < 0f || s >= T.Duration) return;
            if (!Shown(o, map)) return;
            Begin(o);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);

            List<CutEndCut> cuts = T.Layout(radius, count);
            Build(radius, count, sun);
            float sinceClick = s - T.ClickAt;
            float dark = Smooth((s - T.VanishAt) / 0.12f) * (1f - Smooth(sinceClick / 0.4f));
            float w = Mathf.Clamp01((s - T.CastAt) / T.Warm);

            // --- the floor: the true radius, scars left along the cuts ------------------------------------
            if (s >= T.CastAt)
            {
                float grown = Smooth((s - T.CastAt) / (T.Warm * 0.8f)), left = 1f - Smooth((sinceClick - 0.3f) / 0.6f);
                PaperBombGraphics.RingAt(o, radius * grown, Fade(Blue, 0.6f * left), Floor + 0.02f);
            }
            if (sinceClick >= 0f)
                foreach (CutEndCut c in cuts)
                    Streak(o + c.Line.A, o + c.Line.B, 0.05f, Fade(Void, 0.5f - 0.25f * Smooth(sinceClick / T.Tail)), solid, Floor + 0.01f, 4);

            // --- the warm-up: an aura at the caster, lights rising out of the floor inside the ring --------
            if (s >= T.CastAt && s < T.VanishAt)
            {
                GokuGraphics.Aura(o, s, w, Blue);
                for (int i = 0; i < T.Motes; i++)
                {
                    float v = (w * 2.2f + Rand(i)) % 1f;
                    Vector2 foot = o + T.Polar(Rand(i + 60) * 360f, Mathf.Sqrt(Rand(i + 120)) * radius * Smooth(w * 1.25f));
                    Streak(new Vector2(foot.x, foot.y + v * 0.7f), new Vector2(foot.x, foot.y + v * 0.7f + 0.3f), 0.05f,
                        Fade(Ice, 0.8f * w * Mathf.Max(0f, Mathf.Sin(v * Mathf.PI))), whiteGlow, Overhead + 0.01f, 3);
                }
            }

            // --- the blue glow where the caster kneels ----------------------------------------------------
            if (s >= T.BackAt) Sprite(new Vector2(o.x, o.y + 0.3f), 1.8f, 1.8f, Fade(Blue, 0.35f * dark), glow, Overhead - 0.04f);

            // --- the dark inside the ring, over the pawns and under the cuts --------------------------------
            DrawMesh(disc, o, Overhead - 0.05f, radius, radius, 0f, Fade(Void, T.Dark * dark), solid);

            // --- the panes: ajar while the blade goes home, breaking on the click ---------------------------
            if (s >= T.BackAt && sinceClick < T.Fade)
            {
                float u = Mathf.Clamp01(sinceClick / T.Fade), show = Smooth((s - T.BackAt) / (T.Sheathe * 0.6f));
                float spread = Smooth(Mathf.Clamp01(u * 1.6f)), gone = Mathf.Pow(1f - u, 1.5f), size = 1f - (1f - T.FallTo) * spread;
                bool before = sinceClick < 0f;
                for (int i = 0; i < pieces.Count; i++)
                {
                    Vector2 centre = pieces[i].Centre;
                    float far = centre.magnitude > 0f ? centre.magnitude : 1f;
                    float slide = before ? 0f : T.Push * (0.35f + 1.3f * Rand(i + 31)) * spread;
                    float ajar = (T.AjarLeast + (T.AjarMost - T.AjarLeast) * Rand(i + 90)) * show, lean = Rand(i + 91) * Mathf.PI * 2f;
                    Vector2 at = o + centre * (1f + slide / far) + new Vector2(Mathf.Cos(lean), Mathf.Sin(lean)) * ajar;
                    float facet = 0.05f + 0.2f * Rand(i + 3), turn = (Rand(i + 7) - 0.5f) * (3f * show + 22f * spread);
                    float face = before ? facet * 0.8f * show : (facet * 1.6f + 0.4f * Mathf.Clamp01(1f - u * 7f)) * gone, edge = before ? show : gone;
                    DrawMesh(faces[i], at, Overhead + 0.02f, size, size, turn, Fade(before ? Blue : Color.Lerp(Ice, Blue, Mathf.Clamp01(u * 3f)), face), whiteGlow);
                    if (dim[i] != null) DrawMesh(dim[i], at, Overhead + 0.021f, size, size, turn, Fade(Void, 0.75f * edge), solid);
                    if (lit[i] != null) DrawMesh(lit[i], at, Overhead + 0.022f, size, size, turn, Fade(Ice, 0.85f * edge), whiteGlow);
                }
            }

            // --- the cuts: drawn end to end one after another, white on the click, then gone ---------------
            if (s >= T.VanishAt && sinceClick < T.CutsGone)
                for (int k = 0; k < cuts.Count; k++)
                {
                    VergilChord c = cuts[k].Line;
                    float age = s - T.StartOf(cuts[k].Order, cuts.Count);
                    if (age < 0f) continue;
                    float hot = sinceClick >= 0f ? 1f : Mathf.Clamp01(1f - age / 0.18f) * 0.8f;
                    float alpha = sinceClick >= 0f ? 1f - sinceClick / T.CutsGone : 1f, grown = Mathf.Clamp01(age / T.Sweep);
                    Cut(o + c.A, o + c.B, grown, alpha, 0.05f, hot, 0.85f + 0.15f * Mathf.Sin(s * 40f + k * 1.7f));
                    if (age < T.Sweep) Glint(o + c.A + (c.B - c.A) * grown, 0.35f, 1f, Ice, 45f);
                }

            // --- the click: a glint at the scabbard mouth, a ring front, one flash over the ring --------------
            if (sinceClick >= 0f)
            {
                float f = Mathf.Clamp01(sinceClick / 0.3f);
                Glint(o + Mouth, 0.2f + 0.5f * (1f - f), 1f - f, Snow, 0f);
                if (sinceClick < T.Front * 2f)
                    PaperBombGraphics.RingAt(o, radius * Smooth(sinceClick / T.Front), Fade(Ice, 0.45f * (1f - sinceClick / (T.Front * 2f))), Overhead + 0.06f, true, whiteGlow);
                Sprite(o, radius * 2.4f, radius * 2.4f, Fade(Ice, 0.3f * (1f - Mathf.Clamp01(sinceClick / 0.2f))), glow, Overhead + 0.055f);
            }
        }

        /// <summary>
        /// The pane meshes. Each face is a fan; each edge is a strip <see cref="T.EdgeWidth"/> wide just inside
        /// an outline side that faces the light (lit) or away from it (dim). Sides nearly along the light
        /// get neither. Triangles are wound clockwise in map coordinates, as the shipped disc is.
        /// </summary>
        private static void Build(float radius, int count, Vector2 sun)
        {
            float length = sun.magnitude;
            Vector2 light = length > 0f ? -sun / length : new Vector2(-1f, 0f);
            string key = radius + "|" + count + "|" + light.x.ToString("F2") + "|" + light.y.ToString("F2");
            if (key == builtKey) return;
            builtKey = key;

            foreach (Mesh m in faces) if (m != null) Object.Destroy(m);
            foreach (Mesh m in lit) if (m != null) Object.Destroy(m);
            foreach (Mesh m in dim) if (m != null) Object.Destroy(m);

            pieces = T.Pieces(radius, count);
            faces = new Mesh[pieces.Count];
            lit = new Mesh[pieces.Count];
            dim = new Mesh[pieces.Count];
            var litVerts = new List<Vector3>();
            var litTris = new List<int>();
            var dimVerts = new List<Vector3>();
            var dimTris = new List<int>();
            for (int i = 0; i < pieces.Count; i++)
            {
                Vector2[] points = pieces[i].Points;
                int n = points.Length;
                var vertices = new Vector3[n + 1];
                var indices = new int[n * 3];
                for (int j = 0; j < n; j++) vertices[j + 1] = new Vector3(points[j].x, 0f, points[j].y);
                for (int j = 0; j < n; j++)
                {
                    indices[j * 3] = 0;
                    indices[j * 3 + 1] = 1 + (j + 1) % n;
                    indices[j * 3 + 2] = 1 + j;
                }
                faces[i] = Make("Vergil cut end pane " + i, vertices, indices);

                litVerts.Clear(); litTris.Clear(); dimVerts.Clear(); dimTris.Clear();
                for (int j = 0; j < n; j++)
                {
                    Vector2 v = points[j], wv = points[(j + 1) % n], side = wv - v;
                    float len = side.magnitude;
                    if (len < 0.05f) continue;
                    var normal = new Vector2(side.y / len, -side.x / len);   // outward, for a counter-clockwise outline
                    float facing = Vector2.Dot(normal, light);
                    if (Mathf.Abs(facing) < T.EdgeFacing) continue;
                    List<Vector3> verts = facing > 0f ? litVerts : dimVerts;
                    List<int> tris = facing > 0f ? litTris : dimTris;
                    int at = verts.Count;
                    Vector2 inset = normal * T.EdgeWidth;
                    verts.Add(new Vector3(v.x, 0f, v.y));
                    verts.Add(new Vector3(wv.x, 0f, wv.y));
                    verts.Add(new Vector3(wv.x - inset.x, 0f, wv.y - inset.y));
                    verts.Add(new Vector3(v.x - inset.x, 0f, v.y - inset.y));
                    tris.Add(at); tris.Add(at + 2); tris.Add(at + 1);
                    tris.Add(at); tris.Add(at + 3); tris.Add(at + 2);
                }
                lit[i] = litVerts.Count > 0 ? Make("Vergil cut end pane lit " + i, litVerts.ToArray(), litTris.ToArray()) : null;
                dim[i] = dimVerts.Count > 0 ? Make("Vergil cut end pane dim " + i, dimVerts.ToArray(), dimTris.ToArray()) : null;
            }
        }

        private static Mesh Make(string name, Vector3[] vertices, int[] indices)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
