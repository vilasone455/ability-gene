using UnityEngine;
using Verse;
using static RimArt.CoilGunGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.CoilGunArcTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Chain Arc. During the warmup the coil charges at the muzzle of Core's gun (the gun itself
    /// is Core's, aimed at target 1 by the warmup stance). When it fires: a muzzle flash, then a bolt
    /// from the muzzle to target 1's chest, and from each pawn hit to the next, each leaving
    /// <see cref="CoilGunArcTiming.Hop"/> after the one before and reaching its end in
    /// <see cref="CoilGunArcTiming.Strike"/>. Each arrival is a flash, sparks, arcs crawling over the
    /// body and a scorch on the floor under it; a mechanoid adds the EMP ring, a Soaked pawn steam,
    /// water glints and spray. When a bolt leaves a Soaked pawn, a faint ring on the floor shows the true
    /// reach of that jump (6 cells, double the usual 3). Bolts stay lit, re-jagging, then fade; the scorch
    /// marks stay. The target pawns of the preview are not drawn.
    ///
    /// Every piece lies level or at chest height, so nothing here has a per-facing method.
    /// </summary>
    public static class CoilGunArcGraphics
    {
        public static void DrawPreview(Vector3 centre, float aimDegrees, float seconds, Map map) =>
            Draw(T.Script(new Vector2(centre.x, centre.z), aimDegrees), seconds, map);

        public static void Draw(in CoilArcShot shot, float s, Map map)
        {
            CoilArcHop[] hops = shot.Hops;
            int n = hops?.Length ?? 0;
            if (s < 0f || (n > 0 && s >= T.End(shot.Fire, n)) || !Shown(shot.Muzzle, map)) return;
            Begin(shot.Muzzle);

            if (s < shot.Fire)
            {
                Charge(shot.Muzzle, Mathf.Clamp01((s - T.Lead) / Mathf.Max(0.05f, shot.Fire - T.Lead)), s, shot.Seed);
                return;
            }
            MuzzleFlash(shot.Muzzle, s - shot.Fire);
            // The charge dies out over 0.1 s after the shot.
            float after = s - shot.Fire;
            if (after < 0.1f) Charge(shot.Muzzle, 1f - after / 0.1f, s, shot.Seed);

            for (int i = 0; i < n; i++)
            {
                float start = T.Start(shot.Fire, i), age = s - start;
                if (age < 0f) break;
                Vector2 to = Chest(Feet(hops[i]));
                Vector2 from = i == 0 ? shot.Muzzle : Chest(Feet(hops[i - 1]));
                if (i > 0 && hops[i - 1].Soaked) ReachRing(Feet(hops[i - 1]), hops[i - 1].Reach, age);

                float grow = Mathf.Clamp01(age / T.Strike);
                float lit = age < T.Strike + T.Lit ? 1f : 1f - (age - T.Strike - T.Lit) / T.Fade;
                if (lit > 0f)
                {
                    // A jump out of a Soaked pawn runs wider and brighter.
                    bool wet = i > 0 && hops[i - 1].Soaked;
                    Bolt(from, to, grow, lit, s, shot.Seed * 31 + i * 101, wet ? 0.075f : 0.06f, 0.16f, Y + 0.03f + i * 0.004f, true, wet ? 0.95f : 0.75f);
                }

                float hitAge = age - T.Strike;
                if (hitAge < 0f) continue;
                int seed = shot.Seed * 53 + i * 211;
                Scorch(Feet(hops[i]), hitAge, 1f, T.ScorchStay, seed);
                HitFlash(to, hitAge, s, seed, hops[i].Mech ? 1.2f : 1f);
                Sparks(Feet(hops[i]), ChestH, hitAge, hops[i].Mech ? 12 : 9, 3.2f, seed + 7);
                if (hops[i].Mech) EmpRing(to, hitAge);
                if (hops[i].Soaked) SoakedHit(Feet(hops[i]), hitAge, seed + 13);
            }
        }

        private static Vector2 Feet(in CoilArcHop hop) => hop.LiveAt ?? hop.At;
    }
}
