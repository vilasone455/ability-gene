using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.SamehadaGraphics;
using CS = RimArt.ChainSickleGraphics;
using T = RimArt.SamehadaFeedTiming;

namespace RimArt
{
    /// <summary>One landed hit as the in-game picture needs it.</summary>
    public struct SamehadaFeedHit
    {
        /// <summary>The holder's and the target's feet now.</summary>
        public Vector2 Holder, Target;
        /// <summary>The held blade's tip now, where the drain ends.</summary>
        public Vector2 Tip;
        /// <summary>Holder to target, degrees.</summary>
        public float Aim;
        /// <summary>The blade took a charge: the drain runs. Full: it had no room, a grey puff at the tip instead.</summary>
        public bool Gained, Full;
        /// <summary>The target's own altitude (DrawPos.y), for the scratches over it.</summary>
        public float TargetAltitude;
    }

    /// <summary>
    /// Draws Feed. The preview plays samehada-feed.js's script: three swings from rest (wind-up 70
    /// degrees behind the aim, the cut 12 past it, a blur of blade ghosts), each landing on a target 1.15
    /// cells ahead with a pale flash and three scratches; chakra parcels run from its chest into the tip
    /// while the tip grows one charge and the bandage slides back one row; the holder glows green; the
    /// target's Drained haze gains a ring per stack; the tally under the holder fills. The holder and
    /// target stand-ins are not drawn.
    ///
    /// In game the swing is Core's; <see cref="DrawHit"/> draws what follows a landed hit.
    /// </summary>
    public static class SamehadaFeedGraphics
    {
        /// <summary>The preview. <paramref name="centre"/> is halfway between holder and target, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, float aim, float s, Map map)
        {
            if (s < 0f || s >= T.End) return;
            var o = new Vector2(centre.x, centre.z);
            if (!Shown(o, map)) return;
            Begin(o);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 caster = Ground(o, aim, -T.Reach / 2f, 0f), target0 = Ground(o, aim, T.Reach / 2f, 0f);

            SamehadaFeedState st = T.Replay(s);
            int maxHits = T.MaxHits();
            bool inCycle = st.Current >= 0 && st.Age < T.Per;
            float hitAge = inCycle ? st.Age - T.Windup - T.Swing : -1f;
            float sign = Mirror(aim);
            float rel = sign * (inCycle ? T.SwingAngle(st.Age) : T.Rest);
            float deg = aim + rel;

            // The target rocks back at the bite.
            float rock = hitAge >= 0f && hitAge < 0.35f ? T.Rock * CS.Bump(hitAge / 0.35f) : 0f;
            Vector2 target = Ground(target0, aim, rock, 0f);
            var chest = new Vector2(target.x, target.y + ChestH * Lift);

            // Floor first.
            Tally(caster, st.Charges, aim);
            Drained(target, st.Stacks, s);

            Vector2 hand = Hand(caster, deg);

            // Swing blur: a fan of faint blade ghosts behind the blade during the swing.
            if (inCycle && st.Age >= T.Windup && st.Age < T.Windup + T.Swing + 0.06f)
            {
                float u = Mathf.Clamp01((st.Age - T.Windup) / T.Swing), len = BladeLength(Mathf.Floor(st.Charges));
                Vector2 from = CS.Screen(CS.At(hand, HandH));
                for (int i = 1; i <= 6; i++)
                {
                    float a2 = aim + Mathf.Lerp(-T.Back * sign, rel, 1f - i * 0.12f), r2 = a2 * Mathf.Deg2Rad;
                    var c = new Vector2(from.x + Mathf.Cos(r2) * (GripLength + len * 0.55f), from.y + Mathf.Sin(r2) * (GripLength + len * 0.55f));
                    Sprite(c, len * 0.9f, 0.32f, Fade(Wisp, 0.22f * (1f - i / 7f) * Mathf.Min(1f, u * 3f)), soft, Y + 0.04f + i * 0.0001f, -a2);
                }
            }

            bool drinking = hitAge >= 0f && hitAge < T.Drain && st.Current < maxHits;
            float hot = drinking ? Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(hitAge / T.Drain) * Mathf.PI)) : 0f;
            SamehadaBlade sword = Blade(hand, deg, st.Charges, sun, strength, hot: hot);

            if (hitAge >= 0f) Bite(chest, aim, hitAge);
            if (drinking)
            {
                Drain(chest, sword.Tip, hitAge, T.Drain);
                Healing(caster, hitAge - 0.1f, T.Drain);
            }
            // Full: nothing more to drink into the blade. A short grey puff at the tip says so.
            if (st.Current >= maxHits && hitAge >= 0f && hitAge < T.FullPuff)
                Sprite(sword.Tip, 0.3f, 0.3f, Fade(Wisp, 0.5f * (1f - hitAge / T.FullPuff)), soft, Y + 0.09f);
        }

        /// <summary>
        /// A landed hit in game, <paramref name="age"/> seconds after it: the bite on the target's chest,
        /// the drain into the held blade's tip and the holder's green glow, or the grey puff of a full blade.
        /// The blade itself is the held weapon's drawing (its hot light and the growing tip are there).
        /// </summary>
        public static void DrawHit(SamehadaFeedHit hit, float age, Map map)
        {
            if (age < 0f || age >= T.BiteLife || !Shown(hit.Target, map)) return;
            Begin(hit.Target);
            var chest = new Vector2(hit.Target.x, hit.Target.y + ChestH * Lift);
            Bite(chest, hit.Aim, age, 1f, hit.TargetAltitude);
            // The holder heals on every fed hit, full or not.
            Healing(hit.Holder, age - 0.1f, T.Drain);
            if (hit.Gained) Drain(chest, hit.Tip, age, T.Drain);
            else if (hit.Full && age < T.FullPuff) Sprite(hit.Tip, 0.3f, 0.3f, Fade(Wisp, 0.5f * (1f - age / T.FullPuff)), soft, Y + 0.09f);
        }
    }
}
