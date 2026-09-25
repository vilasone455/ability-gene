using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.WaterGunGraphics;
using T = RimArt.WaterGunPumpTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Hydro Pump: the gun comes up, the bag is squeezed while the true cone shows on the floor,
    /// then five jets fan from the muzzle to the cone's end and land there with the Stream Shot
    /// contact; each pawn hit gets that contact where it stood, a puddle where it slid to, drips while
    /// it is down, and steam if it was burning. The jets' rear thirds tear into parcels when the spray
    /// ends. The caster and pawn stand-ins, their falls, hands and flames of the sketch are not drawn.
    /// </summary>
    public static class WaterGunPumpGraphics
    {
        private static readonly Vector2[] Edge = new Vector2[13];

        /// <summary>The preview. <paramref name="centre"/> is the middle of the cone's length, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, float seconds, Map map)
        {
            WaterPumpShot shot = T.Script();
            Draw(new Vector2(centre.x, centre.z) - Turn(aimDegrees) * (shot.Length / 2f), aimDegrees, shot, seconds, map);
        }

        /// <param name="gun">Draw the weapon. In game it is left off once the caster's job is over.</param>
        public static void Draw(Vector2 feet, float aim, in WaterPumpShot shot, float s, Map map, bool gun = true)
        {
            if (s < 0f || s >= T.End(shot.Down, shot.GunHold) || !Shown(feet, map)) return;
            Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            var f = new WaterGunFrame(aim, sun);
            float length = shot.Length, width = shot.Width, lower0 = T.Lower0(shot.GunHold), blast = T.Blast, sprayEnd = T.SprayEnd;

            float raise = s < T.Raise0 ? 0f : s < lower0 ? Smooth((s - T.Raise0) / Raise) : 1f - Smooth((s - lower0) / Lower);
            float pumping = s >= T.Pump0 && s < blast ? Smooth((s - T.Pump0) / T.Windup) : 0f;
            var pose = new WaterGunPose
            {
                Raise = raise,
                Squeeze = s < blast ? pumping : Mathf.Max(0f, 1f - (s - blast) / 0.15f),
                Units = shot.Units - T.Cost * Mathf.Clamp01((s - blast) / T.Spray),
                Slosh = Bump((s - blast) / (T.Spray + 0.4f)) + 0.4f * pumping,
                Recoil = -0.06f * pumping + 0.07f * Bump((s - blast) / 0.25f) + 0.03f * (s >= blast && s < sprayEnd ? Mathf.Sin(s * 60f) : 0f),
            };

            // The cone on the floor: the true hit area, from the wind-up until the spray ends.
            float coneA = s < T.Pump0 ? 0f : s < sprayEnd + 0.3f ? Smooth((s - T.Pump0) / 0.15f) * (1f - Smooth((s - sprayEnd) / 0.3f)) : 0f;
            if (coneA > 0f) Cone(f, feet, length, width, coneA);

            Vector2 muzzle = Weapon(f, feet, s, pose, strength, gun);

            // The pawns hit: the front reaches each at its distance, it slides the push and lies in a
            // puddle, drips until it is up, steams if it was burning.
            float up = T.Up(shot.Down);
            WaterPumpVictim[] victims = shot.Victims;
            for (int i = 0; victims != null && i < victims.Length; i++)
            {
                WaterPumpVictim q = victims[i];
                float hitAge = s - T.ReachAt(q.Along, length);
                if (hitAge < 0f) continue;
                float slide = q.Pushed * Smooth(hitAge / T.Slide);
                Vector2 pos = q.LiveAt ?? f.Place(feet, q.Along + slide, q.Across);
                Puddle(f.Place(feet, q.Along + q.Pushed, q.Across, -0.3f), hitAge - T.Slide, 1.1f, 0.5f);
                if (q.Burning) SteamPuffs(pos, hitAge, 1f, i * 7);
                ShotImpact(f.Place(feet, q.Along, q.Across), hitAge, f, strength);
                Drips(pos, hitAge, 1f - Smooth((s - up) / 0.5f), i * 13);
            }

            // Five jets fan from the muzzle to the cone's end, fronts racing out over Front, tails
            // leaving when the spray ends and tearing into parcels; each lands on the ground there.
            float sprayAge = s - blast, halfEnd = T.HalfWidth(length, length, width);
            for (int j = 0; j < T.Jets; j++)
                ShotImpact(f.Place(feet, length, JetEnd(j, halfEnd)), sprayAge - T.Front, f, strength, 0.15f);
            if (sprayAge >= 0f && sprayAge < T.Spray + T.Front + 0.2f)
            {
                float u1 = Mathf.Clamp01(sprayAge / T.Front), u0 = Mathf.Clamp01((sprayAge - T.Spray) / T.Front);
                float tailBreak = Smooth((sprayAge - T.Spray) / 0.06f);
                for (int j = 0; j < T.Jets; j++)
                {
                    Vector2 end = f.Place(feet, length, JetEnd(j, halfEnd), 0.15f);
                    float sway = 0.02f * Mathf.Sin(s * 23f + j * 1.9f);
                    var b = new Vector2(end.x + sway * f.sa, end.y - sway * f.ca);
                    if (u0 < 1f) Stream(muzzle, b, u0, u1, T.JetWidth, s + j * 0.17f, Y + 0.02f + j * 0.001f, 0.12f, j + 1, tailBreak);
                    StreamTrail(muzzle, b, T.JetWidth, sprayAge, T.Front, T.Spray, Y + 0.02f + j * 0.001f);
                }
                float mz = sprayAge < T.Spray ? 1f : Mathf.Max(0f, 1f - (sprayAge - T.Spray) / 0.12f);
                Sprite(muzzle, 0.30f, 0.26f, Fade(Water, mz * 0.6f), soft, Y + 0.03f);   // wet muzzle
            }
        }

        /// <summary>Where jet <paramref name="j"/> lands across the cone's end.</summary>
        private static float JetEnd(int j, float halfEnd) => Mathf.Lerp(-1f, 1f, j / (float)(T.Jets - 1)) * halfEnd * 0.8f;

        /// <summary>The wedge on the floor from the muzzle to the cone's end, its two edges and its end line.</summary>
        private static void Cone(in WaterGunFrame f, Vector2 feet, float length, float width, float alpha)
        {
            Sides(13, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= 12; i++)
            {
                float along = Mathf.Lerp(T.Apex, length, i / 12f), w = T.HalfWidth(along, length, width);
                a[i] = f.Place(feet, along, -w);
                b[i] = f.Place(feet, along, w);
            }
            Strip(a, b, Fade(Water, 0.14f * alpha), solid, Floor + 0.06f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i <= 12; i++)
                {
                    float along = Mathf.Lerp(T.Apex, length, i / 12f);
                    Edge[i] = f.Place(feet, along, side * T.HalfWidth(along, length, width));
                }
                Sides(13, out a, out b);
                for (int i = 0; i <= 12; i++) { a[i] = Edge[i]; b[i] = new Vector2(Edge[i].x, Edge[i].y + 0.03f); }
                Strip(a, b, Fade(WaterLit, 0.7f * alpha), solid, Floor + 0.07f + (side + 1) * 0.0001f);
            }
            float half = T.HalfWidth(length, length, width);
            Sides(11, out a, out b);
            for (int i = 0; i <= 10; i++)
            {
                float w = Mathf.Lerp(-half, half, i / 10f);
                a[i] = f.Place(feet, length, w);
                b[i] = f.Place(feet, length + 0.03f, w);
            }
            Strip(a, b, Fade(WaterLit, 0.7f * alpha), solid, Floor + 0.0703f);
        }
    }
}
