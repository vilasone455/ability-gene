using UnityEngine;
using Verse;
using static RimArt.PowerPoleGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
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
        /// The preview. <paramref name="centre"/> is halfway between the start cell and the target cell,
        /// as in the lab's sketch, and <paramref name="toward"/> is the unit direction of the cast.
        /// </summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, float seconds, Map map)
        {
            var middle = new Vector2(centre.x, centre.z);
            Draw(middle - toward * (T.ScriptDistance / 2f), toward, T.Script, T.ScriptStaggerRadius, T.ScriptRange, seconds, map, middle, true);
        }

        /// <summary>
        /// <paramref name="start"/> is the cell the caster leaves and <paramref name="toward"/> the unit
        /// direction from it to the landing cell. <paramref name="range"/> draws the range ring round
        /// the start; 0 leaves it out, because a real cast has the targeter's ring. A real cast stops
        /// drawing the pole once it is the held staff again; the preview keeps it (<paramref name="keepPole"/>).
        /// </summary>
        public static void Draw(Vector2 start, Vector2 toward, in PowerPoleStrikeShot shot, float staggerRadius, float range, float seconds,
            Map map, Vector2? anchor = null, bool keepPole = false)
        {
            if (seconds < 0f || seconds >= shot.Duration) return;
            Vector2 left = new Vector2(-toward.y, toward.x);
            Vector2 target = start + toward * shot.TargetAlong + left * shot.TargetAcross, landing = start + toward * shot.Landing;
            if (!Shown(start, map) || !Shown(target, map)) return;
            Begin(anchor ?? start);
            Sun(map, out Vector2 sun, out float shadow);
            float bow = T.Bow * Mathf.Abs(toward.y), struck = seconds - shot.StrikeHitAt, offLine = shot.TargetAcross;
            // A place (along, height), a share of the target's sideways offset, as it is drawn; and its shadow on the ground.
            Vector2 Place(Vector2 place, float across = 0f) => new Vector2(
                start.x + toward.x * place.x + left.x * (offLine * across) + place.y * bow,
                start.y + toward.y * place.x + left.y * (offLine * across) + place.y * SixPathsHeight.Lift);
            Vector2 Cast(Vector2 place, float across = 0f) => start + toward * place.x + left * (offLine * across) + sun * place.y;

            // Floor rings at the rule's true places: range round the start, stagger radius on the target, the landing cell.
            float markers = 1f - Smooth(struck / 0.4f);
            if (range > 0f) Circle(start, range, 0.22f * markers, Floor, Cream);
            Circle(target, staggerRadius, 0.6f * markers, Floor + 0.0002f, Cream);
            Circle(landing, 0.45f, 0.45f * markers, Floor + 0.0004f, Cream);

            // Cracks in the ground from the strike, out to the stagger radius. They stay.
            if (struck >= 0f)
                for (int i = 0; i < T.Cracks; i++)
                {
                    float turn = i / (float)T.Cracks * Mathf.PI * 2f + Rand(i + 300) * 0.5f;
                    float reach = staggerRadius * (0.55f + Rand(i + 310) * 0.45f) * Mathf.Clamp01(struck / 0.08f);
                    crackLine[0] = target - new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)) * 0.05f;
                    for (int j = 0; j < CrackShare.Length; j++)
                    {
                        float bend = (Rand(i * 7 + j * 10 + 320) - 0.5f) * 0.35f * CrackShare[j];
                        crackLine[j + 1] = target + new Vector2(Mathf.Cos(turn + bend), Mathf.Sin(turn + bend)) * (reach * CrackShare[j]);
                    }
                    Tapered(crackLine, 0.075f, Fade(Crack, 0.65f), Floor + 0.004f + i * 0.0002f);
                }

            // The swept fan behind the pole's tip during the whip and the strike: three nested slices, newest brightest.
            bool striking = seconds >= shot.StrikeStartAt;
            float fanFrom = striking ? shot.StrikeStartAt : shot.PeakAt, fanTo = striking ? shot.StrikeHitAt : shot.WhipEndAt;
            if (seconds >= shot.PeakAt && seconds < fanTo + 0.1f && !(seconds >= shot.WhipEndAt + 0.1f && seconds < shot.StrikeStartAt))
                for (int k = 0; k < T.FanSpans.Length; k++)
                {
                    float newest = Mathf.Min(seconds, fanTo), oldest = Mathf.Max(fanFrom, seconds - T.FanSpans[k]);
                    if (newest <= oldest) continue;
                    float fade = seconds < fanTo ? 1f : 1f - (seconds - fanTo) / 0.1f;
                    Sides(T.FanSteps + 1, out Vector2[] inner, out Vector2[] outer);
                    for (int i = 0; i <= T.FanSteps; i++)
                    {
                        float time = Mathf.Lerp(oldest, newest, i / (float)T.FanSteps);
                        T.PoleAt(time, shot, out Vector2 end, out _, out float endAcross);
                        inner[i] = Place(Vector2.Lerp(T.HandsAt(time, shot), end, 0.25f), endAcross * 0.25f);
                        outer[i] = Place(end, endAcross);
                    }
                    Strip(inner, outer, Fade(Cream, 0.16f * fade), solid, Overhead - 0.01f + k * 0.0002f);
                }

            T.PoleAt(seconds, shot, out Vector2 tip, out Vector2 grip, out float tipAcross);
            if (keepPole || seconds < shot.HomeAt)
                Pole(Place(tip, tipAcross), Place(grip), Cast(tip, tipAcross), Cast(grip), (grip - tip).magnitude, Width, shadow, Overhead + 0.04f);

            // Dust: a kick at the planted tip, a stream while the pole pushes, a small puff where the pawn lands.
            Vector2 foot = start + toward * T.Foot;
            Burst(100, foot, seconds - shot.LaunchAt, 9, 0.9f, 0.7f, Overhead - 0.02f);
            if (seconds > shot.LaunchAt && seconds < shot.PeakAt)
                for (int i = 0; i < 4; i++)
                {
                    float u = ((seconds - shot.LaunchAt) * 3f + i / 4f) % 1f;
                    Puff(new Vector2(foot.x + (Rand(i + 30) - 0.5f) * 0.5f, foot.y + u * 0.3f), 0.2f + u * 0.3f, 0.16f + u * 0.24f, u, 0.45f,
                        Overhead - 0.015f + i * 0.0002f);
                }
            Burst(200, landing, seconds - shot.LandAt, 7, 0.7f, 0.55f, Overhead - 0.012f);

            // The strike: flash, a ring out to the stagger radius, a big dust burst.
            if (struck >= 0f && struck < 0.7f)
            {
                float flash = Mathf.Max(0f, 1f - struck / 0.16f);
                var spot = new Vector2(target.x, target.y + 0.15f);
                Sprite(spot, 3.4f, 2.4f, Fade(Cream, flash * 0.7f), glow, Overhead + 0.06f);
                Sprite(spot, 1.5f, 1.1f, Fade(Cream, flash), glow, Overhead + 0.062f);
                Circle(target, staggerRadius * Mathf.Clamp01(0.2f + struck / 0.18f), (1f - Mathf.Clamp01(struck / 0.5f)) * 0.8f, Floor + 0.006f, Cream);
                Burst(400, target, struck, 16, staggerRadius, 0.85f, Overhead - 0.008f, 1.4f);
            }
        }
    }
}
