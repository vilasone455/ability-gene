using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.WaterGunGraphics;
using T = RimArt.WaterGunStreamTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Stream Shot: the gun comes up, one jet crosses to the target with a rounded front and
    /// a tail that tears into parcels, the contact fans out sideways and drops fall into a puddle
    /// that stays; a burning target gives off steam; the bag's level drops one unit. The caster and
    /// target stand-ins, their hands and the flames of the sketch are not drawn.
    /// </summary>
    public static class WaterGunStreamGraphics
    {
        /// <summary>The preview. <paramref name="centre"/> is halfway between the caster and the target, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, bool burning, float seconds, Map map)
        {
            var c = new Vector2(centre.x, centre.z);
            Vector2 toward = Turn(aimDegrees);
            var shot = new WaterStreamShot
            {
                Target = c + toward * (T.ScriptDistance / 2f), Units = T.ScriptUnits, GunHold = T.ScriptHold, Hold = T.ScriptHold,
                OnPawn = true, Burning = burning,
            };
            shot.DripAt = shot.Target;
            Draw(c - toward * (T.ScriptDistance / 2f), shot, seconds, map);
        }

        /// <param name="gun">Draw the weapon. In game it is left off once the caster's job is over.</param>
        public static void Draw(Vector2 feet, in WaterStreamShot shot, float s, Map map, bool gun = true)
        {
            Vector2 run = shot.Target - feet;
            float distance = run.magnitude;
            float aim = distance > 0.001f ? Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg : 0f;
            float fire = T.Fire, hit = T.Hit(distance), lower0 = T.Lower0(distance, shot.GunHold), flight = T.Flight(distance);
            if (s < 0f || s >= T.End(distance, shot.GunHold, shot.Hold) || !Shown(feet, map)) return;
            Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            var f = new WaterGunFrame(aim, sun);

            float raise = s < T.Raise0 ? 0f : s < lower0 ? Smooth((s - T.Raise0) / Raise) : 1f - Smooth((s - lower0) / Lower);
            bool fired = s >= fire;
            var pose = new WaterGunPose
            {
                Raise = raise,
                Units = fired ? shot.Units - T.Cost * Smooth((s - fire) / 0.3f) : shot.Units,
                Slosh = Bump((s - fire) / 0.6f) + 0.3f * Bump((s - T.Raise0) / Raise),
                Recoil = 0.04f * Bump((s - fire) / 0.2f),
            };
            Vector2 muzzle = Weapon(f, feet, s, pose, strength, gun);

            // What happens to the target: steam if it was burning, drips while it is soaked.
            float hitAge = s - hit;
            if (shot.Burning) SteamPuffs(shot.Target, hitAge, 1f, 3);
            if (shot.DripAt.HasValue) Drips(shot.DripAt.Value, hitAge, 1f - Smooth((hitAge - shot.Hold + 0.4f) / 0.4f), 5);

            // The jet: its head flies from the muzzle to the target over the flight, its tail leaves the
            // muzzle Jet seconds later, so a short mass of water crosses the gap and ends in the splash.
            float contactH = shot.OnPawn ? 0.38f / SixPathsHeight.Lift : 0.15f;
            Vector2 target = f.Place(shot.Target, 0f, 0f, contactH);
            float u0 = Mathf.Clamp01((s - fire - T.Jet) / flight), u1 = Mathf.Clamp01((s - fire) / flight);
            if (fired && u0 < 1f)
            {
                Stream(muzzle, target, u0, u1, T.Width, s, Y + 0.02f, 0.05f, 0, Smooth((s - fire - T.Jet) / 0.06f));
                float mz = 1f - Mathf.Clamp01((s - fire) / 0.12f);
                Sprite(muzzle, 0.26f, 0.22f, Fade(Water, mz * 0.6f), soft, Y + 0.03f);   // wet muzzle spray
            }
            StreamTrail(muzzle, target, T.Width, s - fire, flight, T.Jet, Y + 0.02f);
            ShotImpact(shot.Target, hitAge, f, strength, contactH);
        }
    }
}
