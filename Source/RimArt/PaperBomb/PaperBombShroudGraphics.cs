using UnityEngine;
using Verse;
using static RimArt.PaperBombGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.PaperBombShroudTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Paper Shroud: six tags leaving the roll one after another, each swinging out to its own
    /// side, flipping in the air and blending onto a fixed slot on the target's body; the slap as each
    /// lands; the seals lighting one after another; the floor ring at the true radius; the caster's
    /// hand seal; and the burst. A tag is a flat quad, so every aim is one method. Neither pawn is
    /// drawn. The slots are offsets for a standing human-sized pawn.
    /// </summary>
    public static class PaperBombShroudGraphics
    {
        /// <summary>The points of one flying tag's trail. Filled and handed over within one call, so one buffer serves all six.</summary>
        private static readonly Vector2[] tail = new Vector2[5];

        /// <summary>The preview. <paramref name="centre"/> is the target cell, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, float seconds, Map map)
        {
            var target = new Vector2(centre.x, centre.z);
            // The sketch's target walks the last 1.5 cells and stops as the first tag lands.
            float walk = 1.5f * (1f - Smooth(seconds / T.FirstLand));
            Draw(target - toward * T.ScriptDistance, target + toward * walk, T.ScriptHeld, T.ScriptRadius, seconds, map);
        }

        /// <summary>
        /// <paramref name="feet"/> is where the caster stands and <paramref name="victim"/> the target's
        /// ground point now; after the burst it is where the burst happened.
        /// </summary>
        public static void Draw(Vector2 feet, Vector2 victim, float held, float radius, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration(held) || !Shown(victim, map)) return;
            Begin(victim);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            float burstAt = T.BurstAt(held), curlAt = burstAt - Burn, age = seconds - burstAt;
            // 0..1: how far the seals have lit, tag by tag.
            float lit = Mathf.Clamp01((seconds - T.LastLand - 0.15f) / Mathf.Max(0.2f, curlAt - T.LastLand - 0.4f));

            if (age < 0f && seconds >= T.FirstLand)
                RingAt(victim, radius, Fade(Red, 0.25f + 0.45f * lit * (0.6f + 0.4f * Mathf.Sin(seconds * 9f))), Floor + 0.02f);
            Burst(victim, age, radius, sun, shadow, 911, T.Power);

            float sealAt = T.SealAt(held);
            PaperBombTagLineGraphics.SealFlash(feet, Smooth((seconds - sealAt) / 0.12f) * (1f - Smooth((seconds - burstAt - 0.3f) / 0.3f)));
            if (age >= 0f) return;

            Vector2 run = victim - feet;
            float distance = run.magnitude;
            Vector2 toward = distance < 0.01f ? Vector2.right : run / distance, across = new Vector2(-toward.y, toward.x);
            float pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
            // A held target shakes a little.
            var body = new Vector2(victim.x + (seconds >= T.FirstLand ? Mathf.Sin(seconds * 43f) * 0.018f : 0f), victim.y);

            for (int k = 0; k < T.Sheets; k++)
            {
                float leave = T.LeaveAt(k);
                if (seconds < leave) continue;
                float u = Mathf.Clamp01((seconds - leave) / T.Fly);
                Vector3 place = T.Slots[k];
                var slot = new Vector2(body.x + place.x, body.y + place.y);
                if (u < 1f)
                {
                    float join = Smooth((u - 0.7f) / 0.3f);
                    Vector2 at = Path(feet, toward, across, distance, slot, k, u, out float h, out Vector2 ground);
                    Vector2 way = Path(feet, toward, across, distance, slot, k, Mathf.Min(1f, u + 0.04f), out _, out _) - at;
                    float degrees = Mathf.Lerp(Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg, place.z, join);
                    float flip = 1f - 0.78f * Mathf.Abs(Mathf.Sin(seconds * 17f + k * 1.3f)) * (1f - join);
                    Sprite(ground + sun * (h * (1f - join)), 0.3f, 0.14f, Fade(Shade, shadow * 0.8f), soft, AltitudeLayer.Shadows.AltitudeFor());
                    for (int j = 0; j < 5; j++) tail[j] = Path(feet, toward, across, distance, slot, k, Mathf.Max(0f, u - 0.22f + j * 0.055f), out _, out _);
                    PowerPoleGraphics.Tapered(tail, 0.1f, Fade(Paper, 0.3f * (1f - join)), Overhead + 0.04f + k * 0.0002f);
                    Tag(at, degrees, Overhead + 0.05f + k * 0.005f, Overhead + 0.09f, Mathf.Lerp(0.62f, T.OnBody, join), 0.62f * flip);
                    continue;
                }
                // Stuck. A slap: the tag lands 25 % large and settles in 0.08 s. Seals light one after another.
                float since = seconds - leave - T.Fly, slap = 1f + 0.25f * (1f - Mathf.Clamp01(since / 0.08f));
                float heat = Mathf.Clamp01(lit * T.Sheets - k), curl = Mathf.Clamp01((seconds - curlAt) / Burn);
                if (since < 0.12f) RingAt(slot, 0.08f + since * 1.6f, Fade(Paper, 0.7f * (1f - since / 0.12f)), pawnLayer + 0.05f);
                Tag(slot, place.z, pawnLayer + 0.012f + k * 0.005f, Overhead + 0.02f + k * 0.0002f, T.OnBody * slap, 0.55f * slap, heat, curl,
                    heat > 0f ? 0f : 0.5f + 0.5f * Mathf.Sin(seconds * 5f + k));
            }
        }

        /// <summary>A tag's drawn point <paramref name="v"/> of the way through its flight: out to one side and up, then blended onto its slot over the last 30 %.</summary>
        private static Vector2 Path(Vector2 feet, Vector2 toward, Vector2 across, float distance, Vector2 slot, int sheet, float v, out float height, out Vector2 ground)
        {
            float e = 1f - (1f - v) * (1f - v), join = Smooth((v - 0.7f) / 0.3f), swing = Mathf.Max(0f, Mathf.Sin(Mathf.PI * e));
            ground = feet + toward * Mathf.Lerp(T.HandOut, distance, e) + across * (T.Bulge[sheet] * swing * (distance / 5f));
            height = Mathf.Lerp(T.HandHeight, 0.3f, e) + T.Arc * swing;
            return Vector2.Lerp(Up(ground, height), slot, join);
        }
    }
}
