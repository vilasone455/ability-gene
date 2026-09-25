using UnityEngine;
using Verse;
using static RimArt.ShadowPlexusGraphics;
using static RimArt.VfxDraw;
using P = RimArt.ShadowPlexusTiming;
using T = RimArt.ShadowNeckBindTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Shadow neck bind over an Imitation line that is already there: two small hands coming
    /// out of the carrier's pool and crawling along the line, one each side, fingers working; climbing
    /// the body on thin arms; turning in at the shoulders and closing on the neck; and coming apart in
    /// shreds when the target drops or the line is cut. On the floor the hands turn with the line. On
    /// the body they are flat shapes pointing up the screen, which is up the body for every facing, so
    /// there is no per-facing method. The neck height is a standing human's; in game it would come from
    /// the pawn's draw size. No pawn is drawn, and neither is the sketch's suffocation meter (the game
    /// shows a hediff).
    /// </summary>
    public static class ShadowNeckBindGraphics
    {
        private static readonly Vector2[] arm = new Vector2[11];

        /// <summary>The preview. <paramref name="centre"/> is halfway between carrier and target, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, bool cut, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration(cut)) return;
            var o = new Vector2(centre.x, centre.z);
            float half = T.ScriptDistance / 2f;
            Draw(new NeckBindShot
            {
                Carrier = o - toward * half, Target = o + toward * half, Aim = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg,
                Seconds = seconds, Climb = T.ScriptClimb, Release = T.Release(cut), Choke = T.ScriptChoke,
                Cut = cut, CutAt = (1.2f + half) / T.ScriptDistance, Width = T.Width, Sway = T.Sway,
            }, map);
        }

        public static void Draw(in NeckBindShot shot, Map map)
        {
            float s = shot.Seconds, since = s - shot.Release, level = shot.Severity;
            if (s < 0f || !Shown(shot.Carrier, map)) return;
            Begin(shot.Carrier);
            Vector2 carrier = shot.Carrier, toward = Turn(shot.Aim), left = new Vector2(-toward.y, toward.x);
            // The target shudders harder as the suffocation fills.
            Vector2 target = shot.Target + toward * (since < 0f ? 0.025f * level * Mathf.Sin(s * 40f) : 0f);

            // The Imitation line that is already there, and how it ends.
            if (since < 0f) Line(carrier, target, 0f, 1f, s, shot.Sway, shot.Width);
            else if (shot.Cut) BrokenLine(carrier, target, shot.CutAt, since, shot.Width, s, shot.Sway);
            else Line(carrier, target, 0f, 1f - VfxMath.Smooth(since / T.LineBack), s, shot.Sway, shot.Width);
            float held = 1f - VfxMath.Smooth(since / 0.25f);
            Pool(carrier, (0.3f + 0.04f * Mathf.Sin(s * 5f)) * (1f - VfxMath.Smooth(since / T.LineBack)), 1f, s);
            Pool(target, T.PoolRadius * held, 1f, s);
            Grip(target, held, s, 4, 0.35f);

            // The two hands. k is -1 for the one on the left of the screen, 1 for the right.
            float gone = VfxMath.Smooth(since / 0.2f);
            for (int k = -1; k <= 1; k += 2)
            {
                float work = 0.2f + 0.25f * (0.5f + 0.5f * Mathf.Sin(s * 22f + k * 1.5f));    // fingers working while it crawls
                if (s < T.Crawl)
                {
                    Vector2 c = P.PointOn(carrier, target, VfxMath.Smooth(s / T.Crawl));
                    Hand(c + left * (k * T.Beside), shot.Aim, VfxMath.Smooth(s / 0.15f), work, T.HandSize, k > 0, LineLayer + 0.006f);
                    continue;
                }
                if (gone >= 1f) continue;
                float u = VfxMath.Smooth((s - T.Crawl) / shot.Climb);
                for (int i = 0; i < arm.Length; i++) arm[i] = OnBody(target, k, u * i / 10f);
                ShadowLine(arm, T.ArmWidth, 1f - gone, s, flare: false, point: false, layer: Overhead + 0.018f);
                float close = VfxMath.Smooth((u - 0.85f) / 0.15f), squeeze = s >= shot.Closed ? 0.88f + 0.08f * Mathf.Sin(s * 6f) : Mathf.Lerp(work, 0.88f, close);
                Hand(OnBody(target, k, u), 90f + k * T.TurnIn * VfxMath.Smooth((u - 0.75f) / 0.25f), 1f - gone, squeeze, T.HandSize, k > 0, Overhead + 0.02f);
            }
            Shreds(new Vector2(target.x, target.y + T.NeckHeight), since, 10, 0.45f, 0.6f);
        }

        /// <summary>A point up the body from the feet (<paramref name="v"/> 0) to the neck (1), on side <paramref name="k"/>.</summary>
        private static Vector2 OnBody(Vector2 target, int k, float v) =>
            new Vector2(target.x + k * Mathf.Lerp(0.2f, T.NeckApart, v) + Mathf.Sin(v * 9f + k) * 0.03f, target.y + 0.02f + v * T.NeckHeight);
    }
}
