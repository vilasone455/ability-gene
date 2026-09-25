using UnityEngine;
using Verse;
using static RimArt.BubblePipeGraphics;
using static RimArt.ThunderGodGraphics;
using T = RimArt.BubblePipeEyePopTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Eye Pop: the pipe raised, one bubble forming on the tip, its flight to the target's face on
    /// a slight arc, the burst on the face, and the soapy film over the eyes with tiny bubbles popping
    /// off it until the debuff ends. The caster, the target, the target's head shake and rubbing hand
    /// and the shooter's missed shots in the sketch are stand-ins and are not drawn.
    /// </summary>
    public static class BubblePipeEyePopGraphics
    {
        /// <summary>The preview. <paramref name="centre"/> is the middle of the line, as in the lab's sketch: the caster 3 cells behind it, the target 3 ahead.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.ScriptDuration) return;
            var mid = new Vector2(centre.x, centre.z);
            Vector2 feet = mid - toward * (T.ScriptDistance / 2f), target = mid + toward * (T.ScriptDistance / 2f);
            if (!Shown(feet, map)) return;
            DrawCaster(feet, toward, seconds, T.ScriptLowerAt, T.ScriptBlows, T.ScriptCost, map);
            float hit = T.ScriptHitAt, clear = hit + T.ScriptDebuff;
            if (seconds >= hit && seconds < clear)
                DrawSoaped(target, new Vector2(target.x, target.y + T.HeadUp), seconds - hit, clear - seconds, T.ScriptDebuff, seconds, PawnAltitude + 0.004f, map);
            DrawShot(feet + toward * TipAlong, target, seconds, hit, map);
        }

        /// <summary>The caster's part: the pipe up from Lead and down from <paramref name="lowerAt"/>, the jar's level, the bubble forming on the tip.</summary>
        public static void DrawCaster(Vector2 feet, Vector2 toward, float seconds, float lowerAt, float blowsBefore, int cost, Map map, float cap = JarCap)
        {
            if (!Shown(feet, map)) return;
            Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            DrawPipe(feet, new BubbleFrame(toward, sun), Raised(seconds, lowerAt), T.Blows(blowsBefore, cost, seconds), cap, T.Forming(seconds), shadow);
        }

        /// <summary>
        /// The bubble in flight from the raised tip's ground point <paramref name="tip"/> to the ground point
        /// <paramref name="target"/> under the face, and its burst there. <paramref name="hitAt"/> is when it
        /// arrives on the cast's clock.
        /// </summary>
        public static void DrawShot(Vector2 tip, Vector2 target, float seconds, float hitAt, Map map)
        {
            if (seconds < T.LaunchAt || !Shown(target, map)) return;
            Begin(target);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            if (seconds < hitAt)
            {
                float u = Mathf.Clamp01((seconds - T.LaunchAt) / Mathf.Max(0.01f, hitAt - T.LaunchAt));
                Vector2 g = Vector2.Lerp(tip, target, u);
                float h = Mathf.Lerp(TipH, T.FaceH, u) + Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * T.Arc;
                Bubble(new Vector2(g.x, g.y + 0.01f * Mathf.Sin(seconds * 9f)), h, T.Radius, seconds, sun, shadow, 0.12f, 2f);
                return;
            }
            Pop(target, T.FaceH, T.Radius, seconds - hitAt, 7);
        }

        /// <summary>
        /// The film over the eyes of a pawn standing at <paramref name="ground"/> with its head drawn at
        /// <paramref name="head"/>, <paramref name="sinceHit"/> after the burst and <paramref name="untilClear"/>
        /// before the debuff ends; it fades over the last 0.4 s. Four tiny bubbles float up off the face and
        /// pop. <paramref name="clock"/> drives the shimmer and the wobble.
        /// </summary>
        public static void DrawSoaped(Vector2 ground, Vector2 head, float sinceHit, float untilClear, float debuff, float clock, float filmAltitude, Map map)
        {
            if (sinceHit < 0f || untilClear <= 0f || !Shown(ground, map)) return;
            Begin(ground);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            float fade = 1f - Smooth((0.4f - untilClear) / 0.4f);
            var film = new Vector2(head.x, head.y + 0.02f);
            DrawMesh(disc, film, filmAltitude, 0.15f, 0.12f, 0f, Fade(Film, 0.35f * fade), solid);
            Arc(film, 0.15f, 0.12f, 0f, 360f, 0.02f, Fade(Color.Lerp(Iris1, Iris2, 0.5f + 0.5f * Mathf.Sin(clock * 5f)), 0.6f * fade), filmAltitude + 0.001f);
            for (int k = 0; k < T.Tiny; k++)
            {
                float age = sinceHit - T.TinyBornAfterHit(k, debuff);
                if (age < 0f || age > T.TinyLife + 0.15f) continue;
                var g = new Vector2(ground.x + (k % 2 == 1 ? 0.12f : -0.10f), ground.y + 0.02f * k);
                float h = 0.75f + age * 0.35f, r = 0.045f + k * 0.008f, layer = Overhead + 0.06f + k * 0.01f;
                if (age < T.TinyLife) Bubble(g, h, r, clock, sun, shadow, 0.1f, k, fade, layer);
                else Pop(g, h, r, age - T.TinyLife, 60 + k, layer);
            }
        }
    }
}
