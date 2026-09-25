using UnityEngine;
using Verse;
using static RimArt.BubblePipeGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.BubblePipeDriftingBurstTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Drifting Burst: the pipe raised to the mouth, six bubbles forming on the tip one after
    /// another and drifting along the aim in a fan, bobbing and wobbling; a bubble a pawn touches pops
    /// with a floor ring at the blast radius; the ones nobody touched pop on their own when their life
    /// runs out. The caster and the walking pawn of the sketch are not drawn.
    /// </summary>
    public static class BubblePipeDriftingBurstGraphics
    {
        /// <summary>The preview. <paramref name="centre"/> is the chosen cell, as in the lab's sketch; the caster stands 2.5 cells behind it.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, bool pawnWalksIn, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.ScriptDuration(T.ScriptLife)) return;
            Vector2 feet = new Vector2(centre.x, centre.z) - toward * T.ScriptCasterBack;
            if (!Shown(feet, map)) return;
            DrawCaster(feet, toward, seconds, T.ScriptLowerAt(T.ScriptLife), T.ScriptBlows, T.ScriptCost, T.ScriptCount, BubblePipeGraphics.JarCap, map);

            DriftingPop[] pops = pawnWalksIn ? T.ScriptPops(seconds) : null;
            DrawCloud(feet, toward, seconds, T.ScriptCount, T.ScriptSpread, T.ScriptSpeed, T.ScriptLife, T.ScriptBlast, pops, true, map);
        }

        /// <summary>
        /// The caster's part: the pipe, raised from <see cref="BubblePipeGraphics.Lead"/> and lowered from
        /// <paramref name="lowerAt"/>, the jar's soap dropping by <paramref name="cost"/> over the blowing
        /// from <paramref name="blowsBefore"/>, and whichever bubble is forming on the tip.
        /// </summary>
        public static void DrawCaster(Vector2 feet, Vector2 toward, float seconds, float lowerAt, float blowsBefore, int cost, int count, float cap, Map map)
        {
            if (!Shown(feet, map)) return;
            Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            var f = new BubbleFrame(toward, sun);
            DrawPipe(feet, f, Raised(seconds, lowerAt), T.Blows(blowsBefore, cost, count, seconds), cap, T.Forming(count, seconds), shadow);
        }

        /// <summary>
        /// The bubbles of one cast at <paramref name="seconds"/> on the cast's clock. <paramref name="pops"/>
        /// holds, per bubble, whether a pawn popped it (or a wall did), when, and where it was; a bubble
        /// popped before <paramref name="seconds"/> draws its pop, and <paramref name="rings"/> adds the floor
        /// ring at <paramref name="blast"/> for a pop that hurt. Unpopped bubbles pop on their own at the
        /// end of <paramref name="life"/>. Aim-frame positions are from <paramref name="feet"/>.
        /// </summary>
        public static void DrawCloud(Vector2 feet, Vector2 toward, float seconds, int count, float spread, float speed, float life, float blast,
            DriftingPop[] pops, bool rings, Map map, bool[] harmless = null)
        {
            if (!Shown(feet, map)) return;
            Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            var f = new BubbleFrame(toward, sun);
            float expireAt = T.ExpireAt(life);
            for (int i = 0; i < count; i++)
            {
                if (!T.At(i, count, spread, speed, seconds, out DriftingBubble b) || b.Forming) continue;
                if (pops != null && i < pops.Length && pops[i].Popped && pops[i].At <= seconds)
                {
                    Vector2 g = f.Place(feet, pops[i].Along, pops[i].Across);
                    float age = seconds - pops[i].At;
                    Pop(g, pops[i].Height, T.Radius, age, i, Overhead + 0.05f + i * 0.01f);
                    bool ring = rings && (harmless == null || i >= harmless.Length || !harmless[i]);
                    if (ring && age < 0.6f) Circle(g, blast * Smooth(age / 0.15f), (1f - age / 0.6f) * 0.7f, Floor + 0.05f + i * 0.01f, WaterLit);
                    continue;
                }
                Vector2 at = f.Place(feet, b.Along, b.Across);
                if (seconds >= expireAt)
                {
                    Pop(at, b.Height, T.Radius, seconds - expireAt, i + 20, Overhead + 0.05f + i * 0.01f);
                    continue;
                }
                Bubble(at, b.Height, b.Radius, seconds, sun, shadow, 0.07f, i * 1.3f, 1f, Overhead + 0.05f + i * 0.01f);
            }
        }
    }
}
