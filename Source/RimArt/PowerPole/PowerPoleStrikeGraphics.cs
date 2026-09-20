using UnityEngine;
using Verse;
using static RimArt.PowerPoleGraphics;
using static RimArt.ThunderGodGraphics;
using T = RimArt.PowerPoleStrikeTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Vault Strike: the pole planted and pushing up, whipped over the head, held high and
    /// struck down onto the target cell, the pale fan behind its tip, the dust at the planted tip and
    /// at the landing, and at the target the flash, the ring out to the stagger radius, the dust and
    /// the cracks, which stay. The floor rings are the range, the stagger radius and the landing cell.
    ///
    /// Per-facing method: the pole has height. Aimed east or west the arc shows on screen. Aimed
    /// north or south, travel and height share the screen's vertical axis and aimed south they
    /// cancel, so height also shifts the drawing east by Bow x height x |sin(aim)|. Shadows, cracks
    /// and floor rings do not shift; they are on the true cells. No pawn, hand or wall is drawn.
    /// </summary>
    public static class PowerPoleStrikeGraphics
    {
        private static readonly Color Crack = new Color(0.06f, 0.05f, 0.04f);
        private static readonly float[] CrackShare = { 0f, 0.33f, 0.66f, 1f };
        private static readonly Vector2[] crackLine = new Vector2[T.CrackPoints];

        /// <summary>
        /// <paramref name="centre"/> is halfway between the start cell and the target cell, as in the
        /// lab's sketch, and <paramref name="toward"/> is the unit direction of the cast.
        /// </summary>
        public static void Draw(Vector3 centre, Vector2 toward, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration) return;
            var middle = new Vector2(centre.x, centre.z);
            Vector2 start = middle - toward * (T.Distance / 2f), target = start + toward * T.Distance, landing = start + toward * T.Landing;
            if (!Shown(start, map) || !Shown(target, map)) return;
            Begin(middle);
            Sun(map, out Vector2 sun, out float shadow);
            float bow = T.Bow * Mathf.Abs(toward.y), struck = seconds - T.StrikeHitAt;
            // A place (along, height) as it is drawn, and its shadow on the ground.
            Vector2 Place(Vector2 place) => new Vector2(start.x + toward.x * place.x + place.y * bow, start.y + toward.y * place.x + place.y * SixPathsHeight.Lift);
            Vector2 Cast(Vector2 place) => start + toward * place.x + sun * place.y;

            // Floor rings at the rule's true places: range round the start, stagger radius on the target, the landing cell.
            float markers = 1f - Smooth(struck / 0.4f);
            Circle(start, T.Range, 0.22f * markers, Floor, Cream);
            Circle(target, T.StaggerRadius, 0.6f * markers, Floor + 0.0002f, Cream);
            Circle(landing, 0.45f, 0.45f * markers, Floor + 0.0004f, Cream);

            // Cracks in the ground from the strike, out to the stagger radius. They stay.
            if (struck >= 0f)
                for (int i = 0; i < T.Cracks; i++)
                {
                    float turn = i / (float)T.Cracks * Mathf.PI * 2f + Rand(i + 300) * 0.5f;
                    float reach = T.StaggerRadius * (0.55f + Rand(i + 310) * 0.45f) * Mathf.Clamp01(struck / 0.08f);
                    crackLine[0] = target - new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)) * 0.05f;
                    for (int j = 0; j < CrackShare.Length; j++)
                    {
                        float bend = (Rand(i * 7 + j * 10 + 320) - 0.5f) * 0.35f * CrackShare[j];
                        crackLine[j + 1] = target + new Vector2(Mathf.Cos(turn + bend), Mathf.Sin(turn + bend)) * (reach * CrackShare[j]);
                    }
                    Tapered(crackLine, 0.075f, Fade(Crack, 0.65f), Floor + 0.004f + i * 0.0002f);
                }

            // The swept fan behind the pole's tip during the whip and the strike: three nested slices, newest brightest.
            bool striking = seconds >= T.StrikeStartAt;
            float fanFrom = striking ? T.StrikeStartAt : T.PeakAt, fanTo = striking ? T.StrikeHitAt : T.WhipEndAt;
            if (seconds >= T.PeakAt && seconds < fanTo + 0.1f && !(seconds >= T.WhipEndAt + 0.1f && seconds < T.StrikeStartAt))
                for (int k = 0; k < T.FanSpans.Length; k++)
                {
                    float newest = Mathf.Min(seconds, fanTo), oldest = Mathf.Max(fanFrom, seconds - T.FanSpans[k]);
                    if (newest <= oldest) continue;
                    float fade = seconds < fanTo ? 1f : 1f - (seconds - fanTo) / 0.1f;
                    Sides(T.FanSteps + 1, out Vector2[] inner, out Vector2[] outer);
                    for (int i = 0; i <= T.FanSteps; i++)
                    {
                        float time = Mathf.Lerp(oldest, newest, i / (float)T.FanSteps);
                        T.PoleAt(time, out Vector2 end, out _);
                        inner[i] = Place(Vector2.Lerp(T.HandsAt(time), end, 0.25f));
                        outer[i] = Place(end);
                    }
                    Strip(inner, outer, Fade(Cream, 0.16f * fade), solid, Overhead - 0.01f + k * 0.0002f);
                }

            T.PoleAt(seconds, out Vector2 tip, out Vector2 grip);
            Pole(Place(tip), Place(grip), Cast(tip), Cast(grip), (grip - tip).magnitude, Width, shadow, Overhead + 0.04f);

            // Dust: a kick at the planted tip, a stream while the pole pushes, a small puff where the pawn lands.
            Vector2 foot = start + toward * T.Foot;
            Burst(100, foot, seconds - T.LaunchAt, 9, 0.9f, 0.7f, Overhead - 0.02f);
            if (seconds > T.LaunchAt && seconds < T.PeakAt)
                for (int i = 0; i < 4; i++)
                {
                    float u = ((seconds - T.LaunchAt) * 3f + i / 4f) % 1f;
                    Puff(new Vector2(foot.x + (Rand(i + 30) - 0.5f) * 0.5f, foot.y + u * 0.3f), 0.2f + u * 0.3f, 0.16f + u * 0.24f, u, 0.45f,
                        Overhead - 0.015f + i * 0.0002f);
                }
            Burst(200, landing, seconds - T.LandAt, 7, 0.7f, 0.55f, Overhead - 0.012f);

            // The strike: flash, a ring out to the stagger radius, a big dust burst.
            if (struck >= 0f && struck < 0.7f)
            {
                float flash = Mathf.Max(0f, 1f - struck / 0.16f);
                var spot = new Vector2(target.x, target.y + 0.15f);
                Sprite(spot, 3.4f, 2.4f, Fade(Cream, flash * 0.7f), glow, Overhead + 0.06f);
                Sprite(spot, 1.5f, 1.1f, Fade(Cream, flash), glow, Overhead + 0.062f);
                Circle(target, T.StaggerRadius * Mathf.Clamp01(0.2f + struck / 0.18f), (1f - Mathf.Clamp01(struck / 0.5f)) * 0.8f, Floor + 0.006f, Cream);
                Burst(400, target, struck, 16, T.StaggerRadius, 0.85f, Overhead - 0.008f, 1.4f);
            }
        }
    }
}
