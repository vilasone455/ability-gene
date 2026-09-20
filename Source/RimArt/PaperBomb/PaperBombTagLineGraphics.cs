using UnityEngine;
using Verse;
using static RimArt.PaperBombGraphics;
using static RimArt.ThunderGodGraphics;
using T = RimArt.PaperBombTagLineTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Tag Line: the paper strip running out of the roll along the floor with a wave, a lift and
    /// a twist that die out behind its leading edge, the tags printed on it, the fuse burning along it
    /// from where it was lit, and one burst per tag. Everything lies flat or is a level circle, so it
    /// turns with the direction it is given and there is no per-facing method. No pawn, roll or wall
    /// is drawn.
    /// </summary>
    public static class PaperBombTagLineGraphics
    {
        /// <summary>Most points a strip mesh takes (ThunderGodGraphics' pool), less a margin.</summary>
        private const int MostPoints = 38;

        /// <summary>The preview. <paramref name="centre"/> is halfway between the caster and the last tag, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, float seconds, Map map)
        {
            float casterAlong = -(T.ScriptTags - 1) / 2f - (T.Leader + 1f) / 2f;
            Draw(new Vector2(centre.x, centre.z) + toward * casterAlong, toward, T.Script(seconds), map);
        }

        public static void Draw(Vector2 feet, Vector2 toward, in TagLineShot shot, Map map)
        {
            float s = shot.Seconds;
            int n = shot.Tags;
            if (s < 0f || n <= 0 || !Shown(feet, map)) return;
            Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            var across = new Vector2(-toward.y, toward.x);
            float a0 = T.Start, aEnd = T.End(n), front = T.Front(s, n), torn = Smooth((s - T.Lay - 0.1f) / 0.2f);
            bool fusing = shot.FuseSeconds >= 0f;
            float burned = fusing ? shot.FuseSeconds / shot.PerTag : -1f;

            // The strip, in up to two pieces: what the fuse has not reached on either side of where it was lit.
            if (s >= T.Flick)
            {
                float low = a0 + 0.25f * torn, high = Mathf.Min(front, aEnd);
                if (!fusing) Piece(feet, toward, across, sun, shadow, low, high, s, n, torn, front, 0);
                else
                {
                    Piece(feet, toward, across, sun, shadow, low, Mathf.Min(high, shot.FuseFrom - burned), s, n, torn, front, 0);
                    Piece(feet, toward, across, sun, shadow, Mathf.Max(low, shot.FuseFrom + burned), high, s, n, torn, front, 1);
                }
            }

            // The tags printed on the strip, one per cell.
            for (int i = 0; i < n; i++)
            {
                float c = T.TagAt(i);
                if (front < c + 0.38f) continue;
                float heat = 0f, curl = 0f;
                if (fusing)
                {
                    float reach = T.ReachAt(i, shot.FuseFrom, shot.PerTag);
                    if (shot.FuseSeconds >= reach + Burn) continue;
                    heat = Mathf.Clamp01((shot.FuseSeconds - (reach - 0.15f)) / 0.15f);
                    curl = Mathf.Clamp01((shot.FuseSeconds - reach) / Burn);
                }
                float armed = !fusing && s >= T.Lay ? 0.5f + 0.5f * Mathf.Sin(s * 4f - i * 0.5f) : 0f;
                Vector2 pa = Point(feet, toward, across, c - 0.3f, s, n, torn, out _), pb = Point(feet, toward, across, c + 0.3f, s, n, torn, out _);
                Vector2 mid = Point(feet, toward, across, c, s, n, torn, out float twist), run = pb - pa;
                Tag(mid, Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg, Floor + 0.014f + i * 0.0001f, Overhead + 0.02f + i * 0.0001f, run.magnitude / 0.6f, twist, heat, curl, armed);
            }

            if (!fusing)
            {
                return;
            }

            // The fuse: an ember on each burning end of the strip.
            for (int side = -1; side <= 1; side += 2)
            {
                float at = shot.FuseFrom + side * burned;
                if (at < a0 || at > aEnd) continue;
                Vector2 e = feet + toward * at;
                Sprite(e, 0.7f, 0.55f, Fade(Ember, 0.9f), glow, Overhead + 0.03f);
                Sprite(e, 0.25f, 0.2f, Hot, glow, Overhead + 0.031f);
                int flicker = Mathf.FloorToInt(s * 45f);
                for (int k = 0; k < 4; k++)
                {
                    float turn = Rand(flicker * 7 + k + side) * 6.283f, reach = 0.15f + 0.3f * Rand(flicker + k + 3);
                    Streak(e, new Vector2(e.x + Mathf.Cos(turn) * reach, e.y + Mathf.Abs(Mathf.Sin(turn)) * reach), 0.04f, Fade(Hot, 0.9f), whiteGlow, Overhead + 0.032f + k * 0.0002f, 3);
                }
            }

            // The caster's hand seal, for the 0.35 s before the fuse starts and a little after.
            if (shot.Sealed)
            {
                float fuseAt = s - shot.FuseSeconds, sealing = Smooth((s - (fuseAt - Seal)) / 0.12f) * (1f - Smooth((shot.FuseSeconds - 0.3f) / 0.3f));
                SealFlash(feet, sealing);
            }

            // Bursts, far north first so nearer ones overlap them.
            for (int j = 0; j < n; j++)
            {
                int i = toward.y > 0f ? n - 1 - j : j;
                Burst(feet + toward * T.TagAt(i), shot.FuseSeconds - T.BurstAt(i, shot.FuseFrom, shot.PerTag), shot.Radius, sun, shadow, i * 200 + 7);
            }
        }

        /// <summary>The flash of a hand seal on the caster: a four-point glint at the chest and a floor ring. Also used before the fuse is lit.</summary>
        public static void SealFlash(Vector2 feet, float amount)
        {
            if (amount <= 0f) return;
            Vector2 chest = new Vector2(feet.x, feet.y + 0.5f);
            Color flare = new Color(1f, 0.96f, 0.72f);
            float size = 0.3f * amount;
            Sprite(chest, size * 0.9f, size * 0.9f, Fade(flare, amount), glow, Overhead + 0.06f);
            Streak(chest - Turn(45f) * size, chest + Turn(45f) * size, size * 0.16f, Fade(flare, amount), whiteGlow, Overhead + 0.061f, 4);
            Streak(chest - Turn(135f) * (size * 0.7f), chest + Turn(135f) * (size * 0.7f), size * 0.16f, Fade(flare, amount), whiteGlow, Overhead + 0.0612f, 4);
            RingAt(feet, 0.3f + 0.5f * amount, Fade(flare, 0.6f * amount), Floor + 0.03f);
        }

        /// <summary>How loose the strip still is at <paramref name="along"/>: 1 at the leading edge, 0 once it has settled, and 0 at the hand.</summary>
        private static float Loose(float along, float s, int n) =>
            (1f - Smooth((s - T.Passed(along, n)) / T.Settle)) * Smooth((along - T.Start) / 0.8f);

        /// <summary>The drawn point of the strip's middle line at <paramref name="along"/>, with its wave and lift, and how much its twist narrows it.</summary>
        private static Vector2 Point(Vector2 feet, Vector2 toward, Vector2 across, float along, float s, int n, float torn, out float twist)
        {
            Shape(along, s, n, torn, out float side, out float h, out twist);
            return Up(feet + toward * along + across * side, h);
        }

        private static void Shape(float along, float s, int n, float torn, out float side, out float h, out float twist)
        {
            float e = Loose(along, s, n);
            side = T.Wave * e * Mathf.Sin(5f * along - 16f * s);
            h = T.Rise * e * (0.6f + 0.4f * Mathf.Sin(7f * along - 20f * s + 1f)) + T.HandHeight * (1f - Smooth((along - T.Start) / 0.9f)) * (1f - torn);
            twist = 1f - 0.7f * e * Mathf.Abs(Mathf.Sin(3f * along - 11f * s));
        }

        /// <summary>One piece of strip from <paramref name="low"/> to <paramref name="high"/>: shadow, dark edge, paper.</summary>
        private static void Piece(Vector2 feet, Vector2 toward, Vector2 across, Vector2 sun, float shadow, float low, float high, float s, int n, float torn, float front, int piece)
        {
            if (high - low < 0.02f) return;
            int points = Mathf.Clamp(Mathf.CeilToInt((high - low) / 0.2f) + 1, 2, MostPoints);
            // Three strips of the same length in one frame take three meshes from the pool, which hands them out in turn.
            for (int pass = 0; pass < 3; pass++)
            {
                Sides(points, out Vector2[] left, out Vector2[] right);
                for (int i = 0; i < points; i++)
                {
                    float along = Mathf.Lerp(low, high, i / (float)(points - 1));
                    Shape(along, s, n, torn, out float side, out float h, out float twist);
                    float half = T.Width / 2f * twist + (pass == 1 ? 0.03f : 0f);
                    Vector2 ground = feet + toward * along + across * side;
                    Vector2 mid = pass == 0 ? ground + sun * h : Up(ground, h);
                    left[i] = mid + across * half;
                    right[i] = mid - across * half;
                }
                if (pass == 0) Strip(left, right, Fade(Shade, shadow * 0.8f), solid, AltitudeLayer.Shadows.AltitudeFor() + piece * 0.0001f);
                else if (pass == 1) Strip(left, right, PaperEdge, solid, Floor + 0.01f + piece * 0.0001f);
                else Strip(left, right, Paper, solid, Floor + 0.012f + piece * 0.0001f);
            }
        }
    }
}
