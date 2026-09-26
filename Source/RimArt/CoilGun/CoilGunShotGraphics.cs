using UnityEngine;
using Verse;
using static RimArt.CoilGunGraphics;
using static RimArt.VfxDraw;
using T = RimArt.CoilGunShotTiming;

namespace RimArt
{
    /// <summary>
    /// Draws the Coil Gun's normal shot. In flight the round is a short bolt: a jagged white-blue line
    /// <see cref="CoilGunShotTiming.Trail"/> cells long behind a bright head, re-jagged every
    /// <see cref="CoilGunGraphics.Boil"/>, drawn by Projectile_CoilBolt in place of a texture. It leaves
    /// the muzzle of Core's gun, not the shooter's centre where Core launches it, and climbs to chest
    /// height by the time it arrives. A small blue flash at the muzzle replaces Core's yellow one. On
    /// a hit: a flash, sparks, crawling arcs and a scorch that stays 0.8 s; on a mechanoid also the EMP
    /// ring (it takes the extra damage). A miss leaves the flash, sparks and scorch where it lands.
    /// </summary>
    public static class CoilGunShotGraphics
    {
        /// <summary>The round in flight. <paramref name="feet"/> is where it was launched (the shooter's centre), <paramref name="end"/> where it will land (a chest, or the ground), <paramref name="fraction"/> how far it has gone, 0 to 1.</summary>
        public static void InFlight(Vector2 feet, Vector2 end, float fraction, float s, int seed, Map map)
        {
            Vector2 run = end - feet;
            float total = run.magnitude;
            if (total < CoilGunGraphics.MuzzleAlong + 0.05f || !Shown(feet, map)) return;
            Vector2 dir = run / total, muzzle = feet + dir * MuzzleAlong;
            float gone = fraction * total - MuzzleAlong;
            if (gone <= 0f) return;
            Vector2 head = Vector2.Lerp(muzzle, end, Mathf.Clamp01(gone / (total - MuzzleAlong)));
            float back = Mathf.Min(T.Trail, (head - muzzle).magnitude);
            Vector2 tail = head - (head - muzzle).normalized * back;
            Begin(feet);
            Bolt(tail, head, 1f, 1f, s, seed, 0.05f, 0.1f, Y + 0.03f, false, 0.5f);
            Sprite(head, 0.7f, 0.7f, Fade(Halo, 0.65f), glow, Y + 0.035f);
            Sprite(head, 0.24f, 0.24f, Fade(White, 1f), glow, Y + 0.036f);
        }

        /// <summary>The muzzle flash, <paramref name="age"/> seconds after the round left.</summary>
        public static void Flash(Vector2 feet, float aimDegrees, float age, Map map)
        {
            if (age < 0f || age >= 0.12f || !Shown(feet, map)) return;
            Begin(feet);
            MuzzleFlash(feet + Dir(aimDegrees) * MuzzleAlong, age, 0.85f);
        }

        /// <summary>Where a round landed. <paramref name="ground"/> is the floor under the hit; <paramref name="onPawn"/> puts the flash at chest height.</summary>
        public static void Impact(Vector2 ground, bool onPawn, bool mech, float age, float s, int seed, Map map)
        {
            if (age < 0f || age >= T.ImpactEnd || !Shown(ground, map)) return;
            Begin(ground);
            float h = onPawn ? ChestH : 0.1f;
            Vector2 at = new Vector2(ground.x, ground.y + Lifted(h));
            Scorch(ground, age, 0.7f, T.ScorchStay, seed);
            HitFlash(at, age, s, seed, onPawn ? 0.75f : 0.6f);
            Sparks(ground, h, age, 7, 2.6f, seed + 5);
            if (mech) EmpRing(at, age);
        }

        /// <summary>The preview: a burst of two at a pawn <see cref="CoilGunShotTiming.ScriptDistance"/> away along the aim; <paramref name="centre"/> is the middle of that line.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, bool mech, float s, Map map)
        {
            Vector2 dir = Dir(aimDegrees), mid = new Vector2(centre.x, centre.z);
            Vector2 feet = mid - dir * T.ScriptCentreAlong, target = feet + dir * T.ScriptDistance;
            Vector2 chest = Chest(target);
            for (int r = 0; r < T.ScriptRounds; r++)
            {
                float age = s - T.Fired(r);
                if (age < 0f) continue;
                Flash(feet, aimDegrees, age, map);
                float flight = T.Flight(T.ScriptDistance);
                if (age < flight) InFlight(feet, chest, age / flight, s, 40 + r * 7, map);
                else Impact(target, true, mech, age - flight, s, 60 + r * 7, map);
            }
        }
    }
}
