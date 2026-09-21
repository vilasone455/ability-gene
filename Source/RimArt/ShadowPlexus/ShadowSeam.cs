using UnityEngine;

namespace RimArt
{
    /// <summary>The two scenes of the Shadow seam preview.</summary>
    public enum SeamScene { Rusher, Rescue }

    /// <summary>What a Shadow seam looks like now. Points are ground points on the map.</summary>
    public struct SeamShot
    {
        /// <summary>The carrier, and the two sewn targets.</summary>
        public Vector2 Carrier, A, B;
        /// <summary>Seconds since the cast began; the cast, which is also when the two are sewn; when the seam first went taut; when it comes undone.</summary>
        public float Seconds, Cast, Taut, Undo;
        /// <summary>How much bigger B's pool and stitches are than A's, for a bigger body.</summary>
        public float PoolB, StitchB;
        /// <summary>The range ring's radius now; 0 draws none.</summary>
        public float Range;
        public float Width, Sway;
    }

    /// <summary>
    /// Timing of Shadow seam: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/shadow-plexus-seam.js; the constants are that sketch's defaults.
    /// There is no ability behind it yet. The rule (user's draft, placeholders): two targets within
    /// 15.9 cells times the light level, 0.8 s cast, for 20 s they cannot be more than 4 cells apart.
    /// </summary>
    public static class ShadowSeamTiming
    {
        public const float FullRange = 15.9f, MaxApart = 4f, RushSpeed = 4f, CrawlSpeed = 0.5f, RunSpeed = 4f, RunFrom = 0.25f, RunTo = -8f;
        public const float Fork = 0.55f, Straighten = 0.4f, FeederBack = 0.45f, StitchPitch = 0.45f, StitchLength = 0.34f, StitchSlant = 25f;
        public const float Undo = 0.5f, Tail = 0.7f, Twang = 0.18f, Recoil = 0.6f, PoolRadius = 0.36f;
        /// <summary>Camera shake when the rusher is yanked back.</summary>
        public const float TautShake = 0.04f;

        // The preview's script: the sketch's sliders at their defaults.
        public const float ScriptDistance = 5.5f, ScriptCast = 0.8f, Width = 0.15f, Sway = 0.35f;

        public static SeamPlan Plan(SeamScene scene)
        {
            var plan = new SeamPlan { Scene = scene, Sewn = ScriptCast };
            float taut = plan.Sewn;
            while (taut < plan.Sewn + 8f && plan.Apart(taut) < MaxApart) taut += 0.01f;
            plan.Taut = taut;
            plan.A0 = plan.FreeA(taut);
            plan.B0 = plan.FreeB(taut);
            plan.Undo = scene == SeamScene.Rusher ? taut + 2.2f : plan.Sewn + RunFrom + -RunTo / RunSpeed + 0.5f;
            plan.Duration = plan.Undo + Undo + Tail;
            return plan;
        }
    }

    /// <summary>The preview's script. Points are (along the aim, to its left) from the chosen cell.</summary>
    public struct SeamPlan
    {
        public SeamScene Scene;
        public float Sewn, Taut, Undo, Duration;
        /// <summary>Where the two were when the seam went taut.</summary>
        public Vector2 A0, B0;

        public Vector2 Carrier => new Vector2(-ShadowSeamTiming.ScriptDistance, 0f);

        /// <summary>
        /// Where A would be if nothing held it. Rusher: an enemy rushing the carrier at 4 cells a second.
        /// Rescue: an ally running for cover once sewn.
        /// </summary>
        public Vector2 FreeA(float t) => Scene == SeamScene.Rusher
            ? new Vector2(3.4f - ShadowSeamTiming.RushSpeed * t, 1.2f)
            : new Vector2(Mathf.Max(ShadowSeamTiming.RunTo, -ShadowSeamTiming.RunSpeed * Mathf.Max(0f, t - Sewn - ShadowSeamTiming.RunFrom)), 1.5f);

        /// <summary>Where B would be if nothing held it: a centipede crawling, or a downed ally lying still.</summary>
        public Vector2 FreeB(float t) => Scene == SeamScene.Rusher ? new Vector2(2.5f - ShadowSeamTiming.CrawlSpeed * t, -1f) : new Vector2(1.5f, -0.6f);

        public float Apart(float t) => Vector2.Distance(FreeA(t), FreeB(t));

        /// <summary>Where the two are at <paramref name="t"/>.</summary>
        public void Both(float t, out Vector2 a, out Vector2 b)
        {
            if (t < Taut) { a = FreeA(t); b = FreeB(t); return; }
            float age = t - Taut;
            if (Scene == SeamScene.Rusher)
            {
                // The centipede wins: the rusher is held at 4 cells, and recoils once.
                b = FreeB(t);
                float back = ShadowSeamTiming.Recoil * Mathf.Sin(age * 9f) * Mathf.Exp(-age * 5f);
                b.x -= 0.08f * Mathf.Exp(-age * 6f);
                a = new Vector2(b.x + (A0.x - B0.x) + Mathf.Max(0f, back), b.y + (A0.y - B0.y));
                return;
            }
            // The body swings in behind the runner.
            a = FreeA(t);
            float side = (B0.y - A0.y) * Mathf.Exp(-(A0.x - a.x) / ShadowSeamTiming.MaxApart);
            b = new Vector2(a.x + Mathf.Sqrt(ShadowSeamTiming.MaxApart * ShadowSeamTiming.MaxApart - side * side), a.y + side);
        }
    }
}
