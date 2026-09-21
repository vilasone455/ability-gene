using UnityEngine;
using G = RimArt.GokuTiming;

namespace RimArt
{
    /// <summary>When the parts of one Solar Flare happen, in seconds from the start of the preview.</summary>
    public struct SolarFlarePlan
    {
        /// <summary>Hands go to the face; the flash; the stun ends; the preview ends.</summary>
        public float Cast, Flash, Wake, End;
        public float Warm, Stun;
    }

    /// <summary>What a Solar Flare looks like now. Points are ground points on the map.</summary>
    public struct SolarFlareShot
    {
        /// <summary>The caster's cell at the flash. The light comes from its head.</summary>
        public Vector2 Centre;
        public float Seconds;
        public SolarFlarePlan Plan;
        public float Radius, Fade;
        /// <summary>
        /// Pawns within sight of the flash, where they stand now: each throws a shadow away from it
        /// while it is bright. The first <see cref="Blinded"/> of them are stunned and get stars;
        /// <see cref="Seeds"/> sets where each one's stars are in their turn.
        /// </summary>
        public Vector2[] Pawns;
        public int[] Seeds;
        public int Count, Blinded;
        /// <summary>A wall across the light: its two outer ends. It throws a wedge of shadow out to the radius.</summary>
        public bool Walled;
        public Vector2 WallA, WallB;
    }

    /// <summary>
    /// Timing of Solar Flare: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/goku-solar-flare.js; the constants are that sketch's defaults. There
    /// is no ability behind it yet. The rule (user's draft, placeholders): a burst on the caster,
    /// 0.25 s warmup; every pawn within 6.9 cells with line of sight, outside the caster's faction,
    /// is stunned 3.5 s and flash-blinded. The script's melee enemies walk in, are blinded, recoil,
    /// sway through the stun and then walk on slowly; one more walks in from outside the radius and
    /// one stands behind a wall. The caster walking away is not scripted: nothing drawn follows it.
    /// </summary>
    public static class GokuSolarFlareTiming
    {
        public const float Lead = 0.3f, Tail = 1.2f, Front = 0.12f, Recoil = 0.2f, WalkIn = 1.2f, WalkOut = 1.7f, WalkBlind = 0.5f, ShadowReach = 2.6f;
        public const int Rays = 28, Gather = 10;
        /// <summary>The unaffected ones: this far outside the radius, and behind a wall at this direction and distance.</summary>
        public const float OutsideBy = 2.2f, OutsideDegrees = 158f, WallDegrees = -8f, WallDistance = 3.2f;
        /// <summary>The melee enemies: direction from the caster (degrees) and distance at the flash (cells).</summary>
        public static readonly float[] MeleeDegrees = { 20f, 75f, -40f, 130f, -95f, 48f }, MeleeDistance = { 1.3f, 2.2f, 1.8f, 3.4f, 4.6f, 5.6f };
        public const int OutsideSeed = 20, CoveredSeed = 21;

        // The preview's script: the sketch's sliders at their defaults.
        public const float ScriptRadius = 6.9f, ScriptWarm = 0.25f, ScriptFade = 0.6f, ScriptStun = 3.5f;

        public static SolarFlarePlan Plan(float warm = ScriptWarm, float stun = ScriptStun)
        {
            var plan = new SolarFlarePlan { Cast = Lead, Warm = warm, Stun = stun };
            plan.Flash = plan.Cast + warm;
            plan.Wake = plan.Flash + stun;
            plan.End = plan.Wake + Tail;
            return plan;
        }

        /// <summary>Where melee enemy <paramref name="i"/> stands at <paramref name="s"/>, from the caster.</summary>
        public static Vector2 Melee(int i, float s, in SolarFlarePlan plan)
        {
            float age = s - plan.Flash, before = Mathf.Max(0f, plan.Flash - s) * WalkIn, recoil = Recoil * G.Smooth(age / 0.1f), after = Mathf.Max(0f, s - plan.Wake) * WalkBlind;
            float sway = age >= 0f && s < plan.Wake ? Mathf.Sin(s * 6f + i * 2f) * 0.04f : 0f;
            return G.Polar(Vector2.zero, MeleeDegrees[i] + sway * 20f, Mathf.Max(1f, MeleeDistance[i] + before + recoil - after));
        }

        /// <summary>The enemy outside the radius, walking in all along.</summary>
        public static Vector2 Outside(float s, in SolarFlarePlan plan, float radius) =>
            G.Polar(Vector2.zero, OutsideDegrees, Mathf.Max(1.2f, radius + OutsideBy - (s - plan.Flash) * WalkIn));

        public static Vector2 Covered => G.Polar(Vector2.zero, WallDegrees, WallDistance + 1f);

        /// <summary>The wall's outer ends: 1.5 cells either side of its middle, across the line from the caster.</summary>
        public static void Wall(out Vector2 a, out Vector2 b)
        {
            Vector2 c = G.Polar(Vector2.zero, WallDegrees, WallDistance), across = G.Polar(Vector2.zero, WallDegrees + 90f, 1f);
            a = c - across * 1.5f;
            b = c + across * 1.5f;
        }
    }
}
