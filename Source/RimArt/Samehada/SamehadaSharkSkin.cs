using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>One Shark Skin cast as the in-game picture needs it.</summary>
    public sealed class SamehadaSharkCast
    {
        /// <summary>The holder's feet when it cast (the strips fly from here) and now.</summary>
        public Vector2 Caster, CasterAt;
        /// <summary>The holder's facing when it cast, degrees (0 east, 90 north).</summary>
        public float Aim;
        /// <summary>Charges before the cast, and the most the blade holds.</summary>
        public float StartCharges;
        public int Most = SamehadaGraphics.MaxCharges;
        /// <summary>How strongly the strips on the floor still show (they fade as Shark Skin ends).</summary>
        public float StripAlpha = 1f;
        /// <summary>
        /// The held pose the game draws the blade in (Core's carried-weapon anchor, SamehadaHeld.Pose): the
        /// ground point under the grip now and when it cast (the strips leave from there), the blade's
        /// direction, its altitude and the step between its parts.
        /// </summary>
        public Vector2 Hand, HandAtCast;
        public float BladeDeg, Layer, Step = 1f;
    }

    /// <summary>
    /// Timing of Shark Skin: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/samehada-shark-skin.js; the constants are that sketch's defaults. The
    /// preview plays its script: 3 charges, three targets in the arc, one sweep.
    ///
    /// In game the cast is the tear and the flare only: the warmup is the sketch's lead-in
    /// (<see cref="Tear0"/>), the ability fires there and the job holds until <see cref="Sweep0"/>. The
    /// sweep is every melee attack while Shark Skin lasts, drawn as the floor arc.
    /// </summary>
    public static class SamehadaSharkSkinTiming
    {
        /// <summary>Charges the sketch spends; the hit area's radius and half-angle, degrees.</summary>
        public const int Cost = 2;
        public const float ArcR = 1.5f, ArcHalf = 60f;
        /// <summary>Sweep start and end, degrees left (+) and right of the aim; the rest angle after; the recovery.</summary>
        public const float SweepFrom = 80f, SweepTo = -80f, Rest = -30f, Recover = 0.3f;
        public const int Strips = 12;
        public const float Rock = 0.12f;
        public const float Tear = 0.35f, Flare = 0.25f, Sweep = 0.32f, Drain = 0.45f, Hold = 1.0f;
        /// <summary>The camera shake in the middle of the sweep.</summary>
        public const float SweepShake = 0.02f;
        /// <summary>The preview's script: charges at the start and the three targets (along, across the aim).</summary>
        public const int ScriptStart = 3;
        public static readonly Vector2[] ScriptSpots = { new Vector2(1.15f, 0f), new Vector2(1.05f, 0.95f), new Vector2(1.05f, -0.95f) };
        /// <summary>In game: the arc of one Shark Skin attack shows this long; the strips fade over this long as it ends.</summary>
        public const float ArcLife = 0.8f, StripFade = 1f;

        public static float Tear0 => SamehadaGraphics.Lead;
        public static float Flare0 => Tear0 + Tear;
        public static float Sweep0 => Flare0 + Flare;
        public static float SweepEnd => Sweep0 + Sweep;
        public static float Result => SweepEnd + Drain;
        public static float End => Result + Hold + SamehadaGraphics.Tail;

        /// <summary>When the sweep passes a target at (along, across): the inverse of the sweep's easeOut.</summary>
        public static float HitTime(Vector2 spot, float sign)
        {
            float bearing = sign * Mathf.Atan2(spot.y, spot.x) * Mathf.Rad2Deg;
            float u = Mathf.Clamp01((SweepFrom - bearing) / (SweepFrom - SweepTo));
            return Sweep0 + Sweep * (1f - Cbrt(1f - u));
        }

        private static float Cbrt(float x) => x <= 0f ? 0f : Mathf.Pow(x, 1f / 3f);

        /// <summary>The blade's angle relative to the aim (before the mirror): lifting in the flare, the sweep, the recovery.</summary>
        public static float Angle(float s)
        {
            if (s < Sweep0) return Mathf.Lerp(Rest, SweepFrom, Smooth((s - Flare0) / Flare));
            if (s < SweepEnd) return Mathf.Lerp(SweepFrom, SweepTo, ChainSickleGraphics.EaseOut((s - Sweep0) / Sweep));
            return Mathf.Lerp(SweepTo, Rest, Smooth((s - SweepEnd) / Recover));
        }

        public static float TearAt(float s) => Smooth((s - Tear0) / Tear);
        public static float FlareAt(float s) => Smooth((s - Flare0) / Flare);
    }
}
