using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.SamehadaGraphics;
using CS = RimArt.ChainSickleGraphics;
using T = RimArt.SamehadaSharkSkinTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Shark Skin. The preview plays samehada-shark-skin.js's script: the bandage unwinds from its
    /// loose end back to the grip while 12 strips fly off sideways and land; the scales stand off, the
    /// blade widens and purple flesh shows while it lifts to 80 degrees left of the aim; the 3-cell arc
    /// shows on the floor; the blade sweeps to 80 right, passing three targets, each bitten and drained,
    /// each drain growing the blade one charge. The holder and target stand-ins are not drawn.
    ///
    /// In game <see cref="DrawCast"/> draws the tear and the flare at rest; the sweep is each melee attack
    /// while Shark Skin lasts, drawn by <see cref="DrawAttackArc"/>.
    /// </summary>
    public static class SamehadaSharkSkinGraphics
    {
        private static readonly float[] HitAt = new float[3];

        /// <summary>The preview. <paramref name="centre"/> is the sketch's centre, 0.5 cells ahead of the holder.</summary>
        public static void DrawPreview(Vector3 centre, float aim, float s, Map map)
        {
            if (s < 0f || s >= T.End) return;
            var o = new Vector2(centre.x, centre.z);
            if (!Shown(o, map)) return;
            Begin(o);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 caster = Ground(o, aim, -0.5f, 0f);
            Vector2[] spots = T.ScriptSpots;

            float sign = Mirror(aim);
            float sweepU = s < T.Sweep0 ? 0f : CS.EaseOut((s - T.Sweep0) / T.Sweep);
            float rel = sign * T.Angle(s), deg = aim + rel;
            for (int i = 0; i < spots.Length; i++) HitAt[i] = T.HitTime(spots[i], sign);

            // Charges: spend 2 over the tear, gain one per drain.
            float charges = T.ScriptStart - T.Cost * T.TearAt(s);
            for (int i = 0; i < spots.Length; i++) if (s >= HitAt[i]) charges += Mathf.Clamp01((s - HitAt[i]) / T.Drain);
            charges = Mathf.Min(MaxCharges, charges);
            float tear = T.TearAt(s), flare = T.FlareAt(s);

            // Floor: the tally, the hit arc (from the flare on), the Drained hazes.
            Tally(caster, charges, aim);
            if (s >= T.Flare0) Arc(caster, aim, T.ArcR - 0.5f, T.ArcR, T.ArcHalf, 0.45f * Mathf.Min(1f, (s - T.Flare0) / 0.2f));

            Vector2 handRest = Hand(caster, aim), hand = Hand(caster, deg);
            float hot = 0f, healing = -1f;
            for (int i = 0; i < spots.Length; i++)
            {
                float hitAge = s - HitAt[i];
                float rock = hitAge >= 0f && hitAge < 0.35f ? T.Rock * CS.Bump(hitAge / 0.35f) : 0f;
                float dir = Mathf.Atan2(spots[i].y, spots[i].x);
                Vector2 pos = Ground(caster, aim, spots[i].x + Mathf.Cos(dir) * rock, spots[i].y + Mathf.Sin(dir) * rock);
                Drained(pos, hitAge >= 0f ? 1 : 0, s);
                if (hitAge >= 0f && hitAge < T.Drain) hot = Mathf.Max(hot, Mathf.Max(0f, Mathf.Sin(hitAge / T.Drain * Mathf.PI)));
            }

            // Torn strips: strip i leaves the bandage edge at tear0 + i/12 of the tear and flies off sideways.
            if (s >= T.Tear0) Strips(handRest, aim, T.ScriptStart, s - T.Tear0, 1f);

            // The flare: purple light along the blade as the scales stand up.
            float hr = deg * Mathf.Deg2Rad;
            if (s >= T.Flare0 && s < T.Sweep0 + 0.1f)
            {
                float u = Mathf.Clamp01((s - T.Flare0) / (T.Flare + 0.1f)), len = BladeLength(charges);
                Vector2 from = CS.Screen(CS.At(hand, HandH));
                var c = new Vector2(from.x + Mathf.Cos(hr) * (GripLength + len * 0.55f), from.y + Mathf.Sin(hr) * (GripLength + len * 0.55f));
                Sprite(c, len * 1.2f, 0.7f, Fade(FleshLit, 0.35f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), glow, Y + 0.04f, -deg);
            }

            // Sweep blur: ghosts of the blade behind it.
            if (s >= T.Sweep0 && s < T.SweepEnd + 0.08f)
            {
                Vector2 from = CS.Screen(CS.At(hand, HandH));
                float len = BladeLength(charges);
                for (int i = 1; i <= 8; i++)
                {
                    float a2 = aim + Mathf.Lerp(T.SweepFrom * sign, rel, 1f - i * 0.1f), r2 = a2 * Mathf.Deg2Rad;
                    var c = new Vector2(from.x + Mathf.Cos(r2) * (GripLength + len * 0.55f), from.y + Mathf.Sin(r2) * (GripLength + len * 0.55f));
                    Sprite(c, len * 0.95f, 0.4f, Fade(Color.Lerp(Wisp, FleshLit, 0.4f), 0.22f * (1f - i / 9f) * Mathf.Min(1f, sweepU * 4f)), soft,
                        Y + 0.04f + i * 0.0001f, -a2);
                }
            }

            SamehadaBlade sword = Blade(hand, deg, charges, sun, strength, flare, tear, hot);

            for (int i = 0; i < spots.Length; i++)
            {
                float hitAge = s - HitAt[i];
                if (hitAge < 0f) continue;
                float rock = hitAge < 0.35f ? T.Rock * CS.Bump(hitAge / 0.35f) : 0f;
                float dir = Mathf.Atan2(spots[i].y, spots[i].x);
                Vector2 pos = Ground(caster, aim, spots[i].x + Mathf.Cos(dir) * rock, spots[i].y + Mathf.Sin(dir) * rock);
                var chest = new Vector2(pos.x, pos.y + ChestH * Lift);
                Bite(chest, aim + dir * Mathf.Rad2Deg, hitAge);
                if (hitAge < T.Drain)
                {
                    Drain(chest, sword.Tip, hitAge, T.Drain);
                    healing = Mathf.Max(healing, hitAge);
                }
            }
            if (healing >= 0f) Healing(caster, healing - 0.1f, T.Drain);
        }

        /// <summary>
        /// The strips of the bandage torn off a blade held at rest on <paramref name="aim"/>: strip i
        /// leaves the wrap's edge i/12 of the tear after it starts, flies off sideways and lands.
        /// <paramref name="age"/> is seconds since the tear began.
        /// </summary>
        public static void Strips(Vector2 handRest, float aim, float startCharges, float age, float alpha)
        {
            if (alpha <= 0.001f) return;
            float hr0 = aim * Mathf.Deg2Rad, c0 = Mathf.Cos(hr0), s0 = Mathf.Sin(hr0);
            float len0 = BladeLength(startCharges), wrap0 = BandagedAt(startCharges);
            for (int i = 0; i < T.Strips; i++)
            {
                float stripAge = age - i / (float)T.Strips * T.Tear;
                if (stripAge < 0f) continue;
                float along = GripLength + len0 * wrap0 * (1f - i / (float)T.Strips), side = i % 2 == 1 ? 1f : -1f;
                var start = new Vector2(handRest.x + c0 * along - s0 * side * 0.1f, handRest.y + s0 * along + c0 * side * 0.1f);
                float out1 = 1.2f + Rand(i + 300) * 0.8f, spread = (Rand(i + 310) - 0.5f) * 0.8f;
                var v = new Vector2(-s0 * side * out1 + c0 * spread, c0 * side * out1 + s0 * spread);
                TornStrip(start, HandH, v, 0.8f + Rand(i + 320) * 0.6f, stripAge, aim + Rand(i + 330) * 180f, alpha, i);
            }
        }

        /// <summary>
        /// A Shark Skin cast in game, <paramref name="s"/> seconds after its warmup began: the blade, held
        /// where Core holds a weapon, tears and flares (the charges it spends run down on it), the strips fly
        /// off it and land, the purple light, and the arc from the flare on. <paramref name="weapon"/> false
        /// once the cast job is over: then only the strips on the floor are left, faded by StripAlpha.
        /// </summary>
        public static void DrawCast(SamehadaSharkCast cast, float s, Map map, bool weapon)
        {
            if (s < 0f || !Shown(cast.Caster, map)) return;
            Begin(cast.Caster);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            if (s >= T.Tear0) Strips(cast.HandAtCast, cast.BladeDeg, cast.StartCharges, s - T.Tear0, weapon ? 1f : cast.StripAlpha);
            if (!weapon) return;

            float charges = Mathf.Max(0f, cast.StartCharges - T.Cost * T.TearAt(s));
            SamehadaBlade sword = Blade(cast.Hand, cast.BladeDeg, charges, sun, strength, T.FlareAt(s), T.TearAt(s),
                layer: cast.Layer, step: cast.Step, most: cast.Most);
            if (s >= T.Flare0)
            {
                float u = Mathf.Clamp01((s - T.Flare0) / (T.Flare + 0.1f));
                Sprite(sword.Grip + sword.D * (GripLength + sword.Len * 0.55f), sword.Len * 1.2f, 0.7f,
                    Fade(FleshLit, 0.35f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), glow, Y + 0.04f, -cast.BladeDeg);
                Arc(cast.CasterAt, cast.Aim, T.ArcR - 0.5f, T.ArcR, T.ArcHalf, 0.45f * Mathf.Min(1f, (s - T.Flare0) / 0.2f));
            }
        }

        /// <summary>One melee attack under Shark Skin: the 3-cell arc on the floor toward the attacked cell, fading over ArcLife.</summary>
        public static void DrawAttackArc(Vector2 holder, float aim, float age, float radius, float half, Map map)
        {
            if (age < 0f || age >= T.ArcLife || !Shown(holder, map)) return;
            Begin(holder);
            float a = 0.45f * Mathf.Min(1f, age / 0.1f) * (1f - Smooth((age - 0.3f) / (T.ArcLife - 0.3f)));
            Arc(holder, aim, radius - 0.5f, radius, half, a);
        }
    }
}
