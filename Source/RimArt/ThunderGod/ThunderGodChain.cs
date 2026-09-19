using UnityEngine;

namespace RimArt
{
    /// <summary>One jump of the chain. The jump back to the start has no target.</summary>
    public struct ChainHop
    {
        public Vector2 from, to, target, along;
        public bool hasTarget;
        /// <summary>Direction of the jump, and the direction the kunai was thrown from the start, in degrees.</summary>
        public float degrees, thrown;
    }

    /// <summary>
    /// Flying Thunder God: Chain. When each jump happens, and the route for the preview. The
    /// defaults of the lab's kunai-flying-thunder-god-chain.js. The target positions are the
    /// sketch's fixed spread for the demonstration, the preview's script, not a rule.
    /// </summary>
    public static class ThunderGodChainTiming
    {
        public const float Lead = 0.4f, StrikeDelay = 0.04f, RouteGlow = 0.6f;
        public const float Seal = 0.2f, Hop = 0.24f, Squeeze = 0.05f, Line = 0.08f, Flash = 0.22f, Linger = 1f;
        public const float FlashRadius = 1f, LineWidth = 0.12f, Distance = 5f, Shake = 0.03f;
        public const int MostTargets = 5;

        // Where the targets stand: further along the aim than the first, and across it.
        private static readonly float[] SpotAlong = { 0f, 1.8f, 3.6f, 5.2f, 6.8f }, SpotAcross = { 0f, 2.2f, -1.4f, 1.6f, -0.8f };

        public static float CastAt => Lead;
        public static int Hops(int targets, bool returns) => targets + (returns ? 1 : 0);
        public static float GoAt(int hop) => Lead + Seal + hop * Hop;
        public static float ArriveAt(int hop) => GoAt(hop) + Squeeze;
        public static float StrikeAt(int hop) => ArriveAt(hop) + StrikeDelay;
        public static float HitAt(int hop) => StrikeAt(hop) + ThunderGodTiming.SlashTime / 2f;
        public static float SettleAt(int targets, bool returns) => ArriveAt(Hops(targets, returns) - 1) + Flash;
        public static float Duration(int targets, bool returns) => SettleAt(targets, returns) + Linger;

        /// <summary>
        /// Fills <paramref name="hops"/> and returns how many. The chosen cell is the middle of
        /// everything, so the whole route is in view.
        /// </summary>
        public static int Route(Vector2 centre, Vector2 toward, int targets, bool returns, ChainHop[] hops)
        {
            float far = 0f, low = 0f, high = 0f;
            for (int i = 0; i < targets; i++)
            {
                far = Mathf.Max(far, Distance + SpotAlong[i]);
                low = Mathf.Min(low, SpotAcross[i]);
                high = Mathf.Max(high, SpotAcross[i]);
            }
            far += ThunderGodTiming.Behind;
            var across = new Vector2(-toward.y, toward.x);
            Vector2 home = centre - toward * (far / 2f) - across * ((low + high) / 2f), from = home;
            for (int i = 0; i < targets; i++)
            {
                Vector2 target = home + toward * (Distance + SpotAlong[i]) + across * SpotAcross[i], step = target - from;
                float length = step.magnitude;
                Vector2 along = step / (length > 1e-5f ? length : 1f);
                hops[i] = new ChainHop
                {
                    from = from, to = target + along * ThunderGodTiming.Behind, target = target, along = along, hasTarget = true,
                    degrees = ThunderGodTiming.Degrees(step), thrown = ThunderGodTiming.Degrees(target - home),
                };
                from = hops[i].to;
            }
            if (!returns) return targets;
            hops[targets] = new ChainHop { from = from, to = home, degrees = ThunderGodTiming.Degrees(home - from) };
            return targets + 1;
        }
    }
}
