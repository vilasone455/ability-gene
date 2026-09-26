using UnityEngine;
using Verse;
using static RimArt.PaperBombGraphics;
using static RimArt.VfxDraw;
using T = RimArt.PaperBombTagThrowTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Tag Throw: the pin with the tag trailing and fluttering behind it, the stuck tag lying
    /// back toward the thrower with its seal pulsing faster as the fuse runs down, the floor ring at
    /// the true radius, and the burst. The pin and tag are flat quads on the throw's tangent, so
    /// every aim is one method. Neither pawn nor the wall is drawn; a wall's broken block is the
    /// game's business, and the rubble here is only the picture of it.
    /// </summary>
    public static class PaperBombTagThrowGraphics
    {
        /// <summary>The preview. <paramref name="centre"/> is the target cell, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, TagThrowTarget target, float seconds, Map map)
        {
            var hit = new Vector2(centre.x, centre.z);
            Vector2 feet = hit - toward * T.ScriptDistance;
            float along = target == TagThrowTarget.Pawn ? T.ScriptCarrierAlong(Mathf.Min(seconds, T.BurstAt(T.ScriptFuse))) : 0f;
            Draw(feet, toward, T.ScriptDistance, target, hit + toward * along, T.ScriptFuse, T.ScriptRadius, seconds, map);
        }

        /// <summary>
        /// <paramref name="feet"/> is where the thrower stands, <paramref name="toward"/> the unit direction
        /// of the throw and <paramref name="distance"/> how far the tag flies. <paramref name="anchor"/> is the
        /// ground point of what the tag is stuck in, now: the cell, or the carrier's position. After the
        /// burst it is where the burst happened.
        /// </summary>
        public static void Draw(Vector2 feet, Vector2 toward, float distance, TagThrowTarget target, Vector2 anchor, float fuse, float radius,
            float seconds, Map map)
        {
            float burstAt = T.BurstAt(fuse);
            if (seconds < 0f || seconds >= T.Duration(fuse) || !Shown(anchor, map)) return;
            Begin(anchor);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            bool carried = target == TagThrowTarget.Pawn, wall = target == TagThrowTarget.Wall;
            float age = seconds - burstAt, burning = seconds - T.LandAt, aim = ThunderGodTiming.Degrees(toward);

            if (age < 0f && burning >= 0f)
            {
                float f = burning / fuse;
                RingAt(anchor, radius, Fade(Red, 0.2f + 0.5f * f * (0.6f + 0.4f * Mathf.Sin(seconds * 9f))), Floor + 0.02f);
            }
            Burst(anchor, age, radius, sun, shadow, 77, T.Power);
            if (wall && age >= 0f) Rubble(anchor, toward, age);
            if (age >= 0f || seconds < T.Release) return;

            float stuckHeight = T.StuckHeight(target), hitAlong = wall ? -T.WallShort : 0f;
            Vector2 target0 = feet + toward * distance;

            // Flight: the pin leads and the tag trails behind it, fluttering.
            if (seconds < T.LandAt)
            {
                float u = (seconds - T.Release) / T.Fly;
                Vector2 at = Flight(feet, toward, distance + hitAlong, stuckHeight, u, out float h, out Vector2 ground);
                Vector2 next = Flight(feet, toward, distance + hitAlong, stuckHeight, Mathf.Min(1f, u + 0.03f), out _, out _);
                Vector2 way = next - at;
                float length = way.magnitude;
                way = length < 1e-5f ? toward : way / length;
                float degrees = Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg, back = 0.13f + TagLong * T.FlyLong / 2f;
                Sprite(ground + sun * h, 0.4f, 0.16f, Fade(Shade, shadow * 0.8f), soft, AltitudeLayer.Shadows.AltitudeFor());
                Tag(new Vector2(at.x - way.x * back, at.y - way.y * back + Mathf.Sin(seconds * 40f) * 0.02f), degrees + Mathf.Sin(seconds * 31f) * 9f,
                    Overhead + 0.05f, Overhead + 0.058f, T.FlyLong, 0.7f * (0.45f + 0.55f * Mathf.Abs(Mathf.Cos(seconds * 23f))));
                Quad(at, 0.28f, 0.04f, degrees, Steel, Overhead + 0.06f);
                return;
            }

            // Stuck and burning. The seal pulses faster as the fuse runs down, heats over the last 0.5 s, then curls.
            float share = burning / fuse, pulse = 0.5f + 0.5f * Mathf.Sin(burning * (5f + 16f * share));
            float heat = Mathf.Clamp01((burning - (fuse - 0.5f - Burn)) / 0.5f), curl = Mathf.Clamp01((seconds - (burstAt - Burn)) / Burn);
            float slap = 1f + 0.25f * (1f - Mathf.Clamp01(burning / 0.08f));
            Vector2 stick = Up((carried ? anchor : target0 + toward * hitAlong), stuckHeight);
            // Over the pawn, over a wall's top, or on the floor.
            float layer = carried ? AltitudeLayer.Pawn.AltitudeFor() + 0.012f : wall ? AltitudeLayer.Pawn.AltitudeFor() + 0.05f : Floor + 0.014f;
            if (burning < 0.12f) RingAt(stick, 0.1f + burning * 2f, Fade(Hot, 0.7f * (1f - burning / 0.12f)), layer + 0.02f);
            if (carried)
            {
                Tag(stick, -35f, layer, Overhead + 0.02f, T.CarriedLong * slap, 0.6f * slap, heat, curl, pulse);
                Quad(stick, 0.14f, 0.035f, aim, Steel, layer + 0.006f);
                return;
            }
            float tagLength = 0.8f * slap, lies = 0.1f + TagLong * tagLength / 2f * (1f - 0.6f * curl), sway = Mathf.Sin(burning * 7f) * 5f * (1f - curl);
            Tag(stick + Turn(aim + 180f + sway) * lies, aim + sway, layer, Overhead + 0.02f, tagLength, 0.85f + 0.15f * Mathf.Sin(burning * 9f), heat, curl, pulse);
            Quad(stick, 0.2f, 0.04f, aim, Steel, layer + 0.006f);
        }

        /// <summary>The pin's drawn point <paramref name="u"/> of the way through its flight, its height, and the ground point under it.</summary>
        private static Vector2 Flight(Vector2 feet, Vector2 toward, float reach, float endHeight, float u, out float height, out Vector2 ground)
        {
            ground = feet + toward * Mathf.Lerp(T.HandOut, reach, u);
            height = Mathf.Lerp(T.HandHeight, endHeight, u) + T.Arc * Mathf.Max(0f, Mathf.Sin(Mathf.PI * u));
            return Up(ground, height);
        }

        /// <summary>The wall's block breaks into pieces thrown to both sides of the wall line, where the neighbouring tops do not hide them.</summary>
        private static void Rubble(Vector2 centre, Vector2 toward, float age)
        {
            var across = new Vector2(-toward.y, toward.x);
            for (int k = 0; k < 7; k++)
            {
                double n = System.Math.Sin(k * 91.7 + 3) * 43758.5;
                float r = (float)(n - System.Math.Floor(n)), u = Mathf.Clamp01(age / 0.3f), h = 0.5f * 4f * u * (1f - u);
                Vector2 ground = centre + toward * ((k % 2 == 1 ? 1f : -1f) * (0.55f + 0.6f * r) * u) + across * ((r - 0.5f) * 0.9f * u);
                Rock(Up(ground, h), 0.22f + 0.18f * r, u < 1f ? age * 400f : r * 360f, 1f, k * 2, (u < 1f ? Overhead + 0.05f : Floor + 0.025f) + k * 0.001f);
            }
        }
    }
}
