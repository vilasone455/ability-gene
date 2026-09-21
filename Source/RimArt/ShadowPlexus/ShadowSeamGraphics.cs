using UnityEngine;
using Verse;
using static RimArt.ShadowPlexusGraphics;
using static RimArt.ThunderGodGraphics;
using P = RimArt.ShadowPlexusTiming;
using T = RimArt.ShadowSeamTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Shadow seam: one line leaving the carrier, forking to the two targets, the pools and the
    /// three stitches over each one's feet, the fork straightening into the seam between the two while
    /// the carrier's line runs back, the slanted stitch marks along the seam, the twang when it goes
    /// taut, and the stitches popping at the end. A slack seam waves; a taut one is straight. Flat on
    /// the floor except the stitches over the feet, which are level loops that look the same for every
    /// facing. No pawn is drawn.
    /// </summary>
    public static class ShadowSeamGraphics
    {
        private const int MostStitches = 12;
        private static readonly Vector2[] half = new Vector2[15], loop = new Vector2[9];
        private static readonly Vector2[][] stitchPaths = new Vector2[MostStitches + 1][];

        /// <summary>The preview. <paramref name="centre"/> is the chosen cell; the targets stand round it and the carrier behind, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, SeamScene scene, float seconds, Map map)
        {
            SeamPlan plan = T.Plan(scene);
            if (seconds < 0f || seconds >= plan.Duration) return;
            var o = new Vector2(centre.x, centre.z);
            var left = new Vector2(-toward.y, toward.x);
            Vector2 Place(Vector2 local) => o + toward * local.x + left * local.y;

            plan.Both(seconds, out Vector2 a, out Vector2 b);
            bool rusher = scene == SeamScene.Rusher;
            Draw(new SeamShot
            {
                Carrier = Place(plan.Carrier), A = Place(a), B = Place(b), Seconds = seconds, Cast = plan.Sewn, Taut = plan.Taut, Undo = plan.Undo,
                PoolB = rusher ? 1.9f : 1.2f, StitchB = rusher ? 1.8f : 1.2f,
                Range = P.Range(T.FullRange, P.Level(false)), Width = T.Width, Sway = T.Sway,
            }, map);

            // Dust: the rusher's feet at the yank, or the dragged body every 0.18 s.
            if (rusher) Scuff(Place(a), seconds - plan.Taut, 1.4f);
            else
                for (int k = 0; plan.Taut + k * 0.18f < Mathf.Min(seconds, plan.Undo - 0.5f); k++)
                {
                    float at = plan.Taut + k * 0.18f;
                    plan.Both(at, out _, out Vector2 dragged);
                    Scuff(Place(dragged), seconds - at);
                }
        }

        public static void Draw(in SeamShot shot, Map map)
        {
            float s = shot.Seconds, sewn = shot.Cast;
            if (s < 0f || !Shown(shot.Carrier, map)) return;
            Begin(shot.Carrier);
            Vector2 a = shot.A, b = shot.B, carrier = shot.Carrier;
            Vector2 mid = P.PointOn(a, b, 0.5f);
            float toCarrier = Vector2.Distance(mid, carrier);
            Vector2 forkAt = P.PointOn(mid, carrier, 0.8f / (toCarrier < 1e-5f ? 1f : toCarrier));
            if (shot.Range > 0f) RangeRing(carrier, shot.Range, 1f - P.Smooth((s - sewn - T.FeederBack) / 0.4f));

            // The carrier's line and the fork. After the sewing the fork point moves onto the straight
            // line between the two, and the carrier's line runs back.
            float gone = P.Smooth((s - shot.Undo) / T.Undo), apart = Vector2.Distance(a, b), slack = Mathf.Clamp01(1f - apart / T.MaxApart);
            Vector2 joint = P.PointOn(forkAt, mid, P.Smooth((s - sewn - 0.15f) / T.Straighten));
            float feeder = s < sewn + 0.15f ? P.EaseOut(s / (shot.Cast * T.Fork)) : 1f - P.Smooth((s - sewn - 0.15f) / T.FeederBack);
            Line(carrier, joint, 0f, feeder, s, 0.08f, shot.Width);
            Pool(carrier, 0.28f * Mathf.Clamp01(feeder * 4f), 1f, s);

            float branch = Mathf.Clamp01((s - shot.Cast * T.Fork) / (shot.Cast * (1f - T.Fork)));
            float twang = 1f + 0.7f * Mathf.Max(0f, 1f - Mathf.Abs(s - shot.Taut) / T.Twang);
            float sway = shot.Sway * slack * (s > sewn ? 1f : 0.3f), width = shot.Width * Mathf.Lerp(0.75f, 1f, slack) * twang;
            if (branch > 0f && gone < 1f)
            {
                P.Path(half, a, joint, 1f - branch, 1f, s, sway);
                ShadowLine(half, width, 1f - gone, s, flare: false, point: false);
                P.Path(half, b, joint, 1f - branch, 1f, s, sway);
                ShadowLine(half, width, 1f - gone, s, flare: false, point: false);

                // Stitch marks: from both ends toward the middle; they pop in the same order at the end.
                int count = Mathf.Clamp(Mathf.FloorToInt(apart / 2f / T.StitchPitch + 0.5f), 2, MostStitches);
                Vector2 run = b - a;
                float degrees = Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;
                Vector2[] path = stitchPaths[count] ?? (stitchPaths[count] = new Vector2[count * 2 + 1]);
                for (int k = 0; k < 2; k++)
                {
                    P.Path(path, k == 0 ? a : b, joint, 0f, 1f, s, sway);
                    for (int i = 0; i < count; i++)
                    {
                        float f = i / (float)count;
                        float shown = Mathf.Clamp01((s - sewn - 0.15f - T.Straighten * 0.6f - f * 0.35f) / 0.08f) * (1f - Mathf.Clamp01((s - shot.Undo - f * T.Undo * 0.7f) / 0.08f));
                        DrawMesh(MeshPool.plane10, path[i * 2 + 1], LineLayer + 0.004f, 0.05f, T.StitchLength * shown * Mathf.Lerp(1.25f, 1f, slack),
                            -(degrees + (k == 1 ? -T.StitchSlant : T.StitchSlant)), Fade(Shade, 0.95f), solid);
                    }
                }
            }

            // The two targets' pools and the loops over their feet.
            float held = P.Smooth((s - sewn) / 0.3f) * (1f - gone);
            Pool(a, T.PoolRadius * held, 1f, s);
            Pool(b, T.PoolRadius * shot.PoolB * held, 1f, s);
            StitchOver(a, held, 1f);
            StitchOver(b, held, shot.StitchB);
            Shreds(a, s - shot.Undo - T.Undo * 0.7f, 6);
            Shreds(b, s - shot.Undo - T.Undo * 0.7f, 6);
        }

        /// <summary>Three level loops over the feet, one after another: the needle going over and under.</summary>
        private static void StitchOver(Vector2 at, float amount, float big)
        {
            for (int i = 0; i < 3; i++)
            {
                float on = Mathf.Clamp01(amount * 3f - i);
                if (on <= 0f) continue;
                float z = at.y + (i - 1) * 0.11f;
                for (int j = 0; j < loop.Length; j++)
                {
                    float u = j / 8f * on;
                    loop[j] = new Vector2(at.x + Mathf.Lerp(-0.3f, 0.3f, u) * big, z + Mathf.Sin(u * Mathf.PI) * 0.3f * SixPathsHeight.Lift * big);
                }
                PowerPoleGraphics.Tapered(loop, 0.07f, Fade(Shade, 0.95f), Overhead + 0.012f);
            }
        }
    }
}
