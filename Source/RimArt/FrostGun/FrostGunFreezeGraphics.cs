using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.FrostGunGraphics;
using T = RimArt.FrostGunFreezeTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Flash Freeze, <c>s</c> seconds after the warmup started:
    /// - warmup: frost gathers at the held gun's muzzle (the gun itself is Core's, aimed at the target);
    /// - fire: a white-blue beam crosses to the target's chest at <see cref="T.BeamSpeed"/>;
    /// - hit: frost spreads on the floor round the feet, and the ice block rises out of it round the
    ///   pawn over <see cref="T.Grow"/>, spikes following;
    /// - frozen: the block stands, glinting;
    /// - then either the shatter (flash, mist, shards that land and melt, stumps) or the thaw (the
    ///   block shrinks and drips into a puddle). The frost on the floor stays 1.5 s after the ice.
    /// </summary>
    public static class FrostGunFreezeGraphics
    {
        /// <summary>
        /// The preview. <paramref name="centre"/> is the frozen pawn's cell; the caster stands
        /// <see cref="T.ScriptDistance"/> cells back along <paramref name="aimDegrees"/>. The shatter entry
        /// breaks the ice <see cref="T.ScriptShatterAfter"/> s after it formed; the thaw entry lets it run out.
        /// </summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, bool shatter, float seconds, Map map)
        {
            var target = new Vector2(centre.x, centre.z);
            Vector2 aim = Turn(aimDegrees);
            var shot = new FrostFreezeShot
            {
                Target = target, Muzzle = Muzzle(target - aim * T.ScriptDistance, aim), Warmup = T.ScriptWarmup, Freeze = T.ScriptFreeze,
                ShatterAt = -1f, Seed = 7,
            };
            if (shatter) shot.ShatterAt = T.Hit(shot) + T.Grow + T.ScriptShatterAfter;
            Draw(shot, seconds, map);
        }

        /// <summary>The preview's length: the shatter or the thaw played out.</summary>
        public static float PreviewEnd(bool shatter)
        {
            var shot = new FrostFreezeShot
            {
                Target = new Vector2(T.ScriptDistance, 0f), Muzzle = new Vector2(MuzzleAlong, 0f), Warmup = T.ScriptWarmup, Freeze = T.ScriptFreeze, ShatterAt = -1f,
            };
            if (shatter) shot.ShatterAt = T.Hit(shot) + T.Grow + T.ScriptShatterAfter;
            return T.End(shot);
        }

        /// <param name="landed">False while the warmup has not ended in game: only the charge is drawn.</param>
        public static void Draw(in FrostFreezeShot shot, float s, Map map, bool landed = true)
        {
            Vector2 pawn = shot.LiveTarget ?? shot.Target;
            if (s < 0f || !Shown(pawn, map)) return;
            if (landed && s >= T.End(shot)) return;
            Begin(pawn);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);

            float fire = shot.Warmup, hit = T.Hit(shot), flight = hit - fire;
            if (s < fire) Charge(shot.Muzzle, s / Mathf.Max(0.01f, shot.Warmup), s);
            if (!landed) return;

            // The muzzle's flash as the beam leaves, and the beam.
            float mf = (s - fire) / 0.14f;
            if (mf >= 0f && mf < 1f) Sprite(shot.Muzzle, 0.9f * (1f - mf) + 0.2f, 0.9f * (1f - mf) + 0.2f, Fade(Cold, 0.8f * (1f - mf)), glow, Y + 0.024f);
            Vector2 chest = pawn + new Vector2(0f, 0.12f);
            Beam(shot.Muzzle, chest, Mathf.Clamp01((s - fire) / flight), 1f - Smooth((s - hit) / T.BeamLinger), s);

            float iceEnd = T.IceEnd(shot), age = s - hit;
            if (age < 0f) return;
            Vector2 feet = pawn + new Vector2(0f, -FeetBack - 0.05f);
            Stain(feet, age / T.StainGrow, 1f - Smooth((s - iceEnd - T.StainHold) / T.StainFade), shot.Seed, s);
            if (shot.Missed) return;

            if (shot.ShatterAt >= 0f && s >= shot.ShatterAt)
            {
                Shatter(pawn, s - shot.ShatterAt, shot.Seed, s, sun);
                return;
            }
            float thawAt = T.ThawAt(shot);
            float thaw = Mathf.Clamp01((s - thawAt) / T.Thaw);
            if (s >= thawAt)
                Thaw(pawn, s - thawAt, 1f - Smooth((s - thawAt - T.Thaw - T.PuddleHold) / T.PuddleFade), shot.Seed);
            if (thaw < 1f)
                IceBlock(pawn, Mathf.Clamp01(age / T.Grow), thaw, 1f - Smooth((thaw - 0.55f) / 0.45f), shot.Seed, s);
            // The flash of the freeze landing.
            float lf = age / 0.25f;
            if (lf < 1f) Sprite(pawn + new Vector2(0f, 0.1f), 1.8f, 2.2f, Fade(IceWhite, 0.45f * (1f - lf)), glow, Y + 0.058f);
        }
    }

    /// <summary>
    /// Draws the normal shot's preview: three bolts from a caster <see cref="FrostGunShotTiming.ScriptDistance"/>
    /// cells back along the aim, each ending in a frost puff on the target, and the target's Chilled
    /// haze gaining a mark per hit. In game the bolt is the gun's projectile (Projectile_FrostGun draws
    /// it with <see cref="FrostGunGraphics.Bolt"/>), the puff is MapComponent_FrostGun's, and the haze is
    /// drawn for every Chilled pawn.
    /// </summary>
    public static class FrostGunShotGraphics
    {
        public static void DrawPreview(Vector3 centre, float aimDegrees, float seconds, Map map)
        {
            var target = new Vector2(centre.x, centre.z);
            if (seconds < 0f || seconds >= FrostGunShotTiming.ScriptEnd || !Shown(target, map)) return;
            Begin(target);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            Vector2 aim = Turn(aimDegrees);
            float d = FrostGunShotTiming.ScriptDistance;
            Vector2 origin = target - aim * d;
            int stacks = 0;
            for (int k = 0; k < FrostGunShotTiming.ScriptShots; k++)
            {
                float fire = FrostGunShotTiming.Fire(k), hit = FrostGunShotTiming.Hit(k, d);
                if (seconds >= fire && seconds < hit)
                    Bolt(origin, Vector2.Lerp(origin, target, (seconds - fire) / (hit - fire)), FrostGunShotTiming.BoltSpeed, Y + 0.03f, seconds);
                if (seconds >= hit)
                {
                    stacks++;
                    Puff(target, seconds - hit, 11 + k, true, aim, sun);
                }
            }
            ChillHaze(target, stacks, seconds, 3);
        }
    }
}
