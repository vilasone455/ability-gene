using UnityEngine;
using Verse;
using static RimArt.ShadowPlexusGraphics;
using static RimArt.VfxDraw;
using P = RimArt.ShadowPlexusTiming;
using T = RimArt.ShadowImitationTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Shadow imitation: the carrier's shadow running out over the ground as one black line to
    /// the target, the pool that opens under the target and the threads that climb to its knees, the
    /// dust of each drag step, and the end: the line running back, snapping where it was cut, or
    /// dying from the dark end. Everything but the threads is flat on the floor, so there is no
    /// per-facing method. No pawn is drawn, and the target is not darkened: the sketch tints its
    /// stand-in, which a real pawn's drawing would have to do itself.
    /// </summary>
    public static class ShadowImitationGraphics
    {
        /// <summary>The preview. <paramref name="centre"/> is halfway between carrier and target, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, ImitationEnd end, float seconds, Map map)
        {
            ImitationPlan plan = T.Plan(end);
            if (seconds < 0f || seconds >= plan.Duration) return;
            var o = new Vector2(centre.x, centre.z);
            var left = new Vector2(-toward.y, toward.x);
            Vector2 Place(Vector2 local) => o + toward * local.x + left * local.y;

            plan.Places(seconds, out Vector2 carrier, out Vector2 target);
            Draw(new ImitationShot
            {
                Carrier = Place(carrier), Target = Place(target), Seconds = seconds, Cast = T.ScriptCast,
                Release = plan.Release, End = end, CutShare = (1.2f + plan.Half) / T.ScriptDistance,
                Range = P.Range(T.FullRange, plan.Level(carrier)), Width = T.Width, Sway = T.Sway,
            }, map);

            // Dust at the target for each step it is dragged.
            for (int k = 0; k < plan.Steps; k++)
            {
                float from = plan.WalkStart + k * (T.ScriptStep + T.StepPause) + T.DragLag;
                if (from < plan.BreakAt) Scuff(Place(new Vector2(plan.Half, k + 0.5f)), seconds - from);
            }
        }

        public static void Draw(in ImitationShot shot, Map map)
        {
            float s = shot.Seconds, since = s - shot.Release;
            if (s < 0f || !Shown(shot.Carrier, map)) return;
            Begin(shot.Carrier);
            Vector2 carrier = shot.Carrier, target = shot.Target;
            if (shot.Range > 0f) RangeRing(carrier, shot.Range, 1f - VfxMath.Smooth(since / 0.5f));

            // The line.
            if (s < shot.Release) Line(carrier, target, 0f, P.EaseOut(s / shot.Cast), s, shot.Sway, shot.Width);
            else if (shot.End == ImitationEnd.Released) Line(carrier, target, 0f, 1f - VfxMath.Smooth(since / T.Retract), s, shot.Sway, shot.Width);
            else if (shot.End == ImitationEnd.Cut) BrokenLine(carrier, target, shot.CutShare, since, shot.Width, s, shot.Sway);
            else Line(carrier, target, 0f, 1f - VfxMath.Smooth(since / P.SnapTime), s, shot.Sway, shot.Width);

            // The hold on the target: pool and knee threads. All of it lets go at the release.
            float grab = VfxMath.Smooth((s - shot.Cast) / 0.25f) * (1f - VfxMath.Smooth(since / 0.25f));
            Pool(target, T.PoolRadius * grab, 1f, s);
            Pool(carrier, 0.3f * (1f - VfxMath.Smooth(since / T.After(shot.End))), 1f, s);
            Grip(target, grab, s, 4, T.KneeThreads);
            Shreds(target, since, 7);
        }
    }
}
