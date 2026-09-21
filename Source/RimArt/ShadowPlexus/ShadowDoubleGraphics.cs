using UnityEngine;
using Verse;
using static RimArt.ShadowPlexusGraphics;
using static RimArt.ThunderGodGraphics;
using P = RimArt.ShadowPlexusTiming;
using T = RimArt.ShadowDoubleTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Shadow double: the carrier's shadow sliding over the ground as a flat figure with a thin
    /// line behind it, standing up out of the floor at the lit cell as a black figure with an indigo
    /// edge and wisps, an Imitation cast from its feet with the range ring round it, and the end: it
    /// sinks and slides home, or bursts into shreds when its cell goes dark and the thin line snaps
    /// back. The standing figure is the sketch's two-disc shape in shadow colour, so it has no
    /// per-facing method; in game it would be the carrier's own silhouette. No pawn is drawn, and the
    /// carrier's own shadow is not hidden while the double is out, as the sketch does to its stand-in.
    /// </summary>
    public static class ShadowDoubleGraphics
    {
        private static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();
        private static readonly Vector2[] wisp = new Vector2[3];

        /// <summary>The preview. <paramref name="centre"/> is the lit cell the double stands on, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, DoubleEnd end, float seconds, Map map)
        {
            DoublePlan plan = T.Plan(end);
            if (seconds < 0f || seconds >= plan.Duration) return;
            var o = new Vector2(centre.x, centre.z);
            var left = new Vector2(-toward.y, toward.x);
            Vector2 Place(Vector2 local) => o + toward * local.x + left * local.y;

            float moved = plan.Moved(seconds);
            var spot = new Vector2(0f, moved);
            Draw(new DoubleShot
            {
                Carrier = Place(new Vector2(-T.ScriptDistance, moved)), Spot = Place(spot), Enemy = Place(plan.Enemy),
                Aim = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg, Seconds = seconds, Cast = T.ScriptCast, CastStart = plan.CastStart,
                HeldAt = plan.HeldAt, Release = plan.Release, Gone = plan.Gone, End = end,
                Range = P.Range(T.ImitationRange, plan.Level(spot, seconds)),
            }, map);
        }

        public static void Draw(in DoubleShot shot, Map map)
        {
            float s = shot.Seconds, after = s - shot.Gone;
            if (s < 0f || !Shown(shot.Spot, map)) return;
            Begin(shot.Spot);
            bool sinks = shot.End == DoubleEnd.TimeRunsOut;
            Vector2 carrier = shot.Carrier, spot = shot.Spot;

            // Where the shadow is: sliding out, standing, or (time runs out) sliding home.
            float homeward = sinks ? P.Smooth((after - T.Rise) / T.Home) : 0f, outward = s < shot.Cast ? P.Smooth(s / shot.Cast) : 1f - homeward;
            float up = P.Smooth((s - shot.Cast) / T.Rise) * (sinks ? 1f - P.Smooth(after / T.Rise) : 1f), seen = sinks ? 1f : 1f - P.Smooth(after / T.Burst);
            bool away = sinks ? homeward < 1f : after < T.Burst + T.LineBack;

            // The thin line back to the carrier. It is the only line allowed over dark cells.
            float tie = !sinks && after > T.Burst ? 1f - P.Smooth((after - T.Burst) / T.LineBack) : outward;
            if (away) Line(carrier, spot, 0f, tie, s, 0.06f, T.TieWidth, flare: false, point: false);
            FlatFigure(Vector2.Lerp(carrier, spot, outward), shot.Aim, 1f - up, away ? seen : 0f);
            Pool(spot, T.PoolRadius * up * seen, 1f, s);

            // Imitation cast from the double.
            float since = s - shot.Release, grab = P.Smooth((s - shot.HeldAt) / 0.25f) * (1f - P.Smooth(since / 0.25f));
            if (s >= shot.CastStart && since < T.LineBack)
                Line(spot, shot.Enemy, 0f, since < 0f ? P.EaseOut((s - shot.CastStart) / T.LineOut) : 1f - P.Smooth(since / T.LineBack), s, 0.1f, T.LineWidth);
            if (shot.Range > 0f) RangeRing(spot, shot.Range, P.Smooth((s - shot.CastStart + 0.2f) / 0.2f) * (1f - P.Smooth(since / 0.4f)));
            Pool(shot.Enemy, T.PoolRadius * grab, 1f, s);
            Grip(shot.Enemy, grab, s, 4, 0.35f);
            Shreds(shot.Enemy, since, 7);

            Silhouette(spot, up, seen, s);
            if (!sinks) Shreds(new Vector2(spot.x, spot.y + 0.3f), after, 16, 0.55f, 1.1f);
        }

        /// <summary>The flat shadow that slides over the floor: a body strip and a head, pointing along <paramref name="degrees"/>.</summary>
        private static void FlatFigure(Vector2 at, float degrees, float scale, float alpha)
        {
            if (scale <= 0f || alpha <= 0f) return;
            Vector2 u = Turn(degrees);
            Sides(6, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= 5; i++)
            {
                float share = i / 5f, w = Mathf.Lerp(0.19f, 0.13f, share) * scale;
                Vector2 p = at + u * (share * 0.9f * scale);
                a[i] = new Vector2(p.x - u.y * w, p.y + u.x * w);
                b[i] = new Vector2(p.x + u.y * w, p.y - u.x * w);
            }
            Color colour = Fade(Shade, 0.94f * alpha);
            Strip(a, b, colour, solid, LineLayer + 0.006f);
            DrawMesh(disc, at + u * (1.02f * scale), LineLayer + 0.006f, 0.16f * scale, 0.16f * scale, 0f, colour, solid);
        }

        /// <summary>The standing double. <paramref name="up"/> 0 to 1 is how far it has risen out of the floor.</summary>
        private static void Silhouette(Vector2 at, float up, float alpha, float seconds)
        {
            if (up <= 0f || alpha <= 0f) return;
            for (int pass = 0; pass < 2; pass++)
            {
                float grow = pass == 0 ? 0.03f : 0f, layer = PawnLayer + (pass == 0 ? 0f : 0.002f);
                Color colour = Fade(pass == 0 ? Fringe : Shade, 0.95f * alpha);
                DrawMesh(disc, new Vector2(at.x, at.y + 0.18f * up), layer, 0.22f + grow, (0.32f + grow) * up, 0f, colour, solid);
                DrawMesh(disc, new Vector2(at.x, at.y + 0.58f * up), layer + 0.001f, (0.16f + grow) * Mathf.Lerp(0.6f, 1f, up), (0.17f + grow) * up, 0f, colour, solid);
            }
            // Wisps: they leave the shoulders, lift 0.5 cells and thin out.
            float lift = SixPathsHeight.Lift;
            for (int i = 0; i < 4; i++)
            {
                float u = (seconds * 0.7f + i / 4f) % 1f, x = at.x + (i % 2 == 1 ? 0.2f : -0.2f) + Mathf.Sin(u * 5f + i) * 0.08f, h = (0.55f + u * 0.5f) * up;
                wisp[0] = new Vector2(x, at.y + h * lift);
                wisp[1] = new Vector2(x + 0.04f, at.y + (h + 0.12f) * lift);
                wisp[2] = new Vector2(x - 0.02f, at.y + (h + 0.26f) * lift);
                PowerPoleGraphics.Tapered(wisp, 0.07f * (1f - u), Fade(Shade, 0.8f * (1f - u) * alpha * up), Overhead + 0.01f);
            }
        }
    }
}
