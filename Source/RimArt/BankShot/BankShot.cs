using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// One charged Bank Shot: the flight, where it is on the map and the numbers that time it. A
    /// real cast fills it from the map and the ability's XML; the preview fills it from a layout.
    /// </summary>
    public struct BankShotShot
    {
        /// <summary>The flight, in the rule's cell space (a cell's centre is a whole number).</summary>
        public BankShotPath Path;
        /// <summary>Added to a path point to get the map point: the scene cell's centre in the preview, 0.5 in game.</summary>
        public Vector2 Offset;
        /// <summary>The caster's feet on the map, and the aim in degrees (0 east, 90 north).</summary>
        public Vector2 Caster;
        public float Aim;
        /// <summary>Seconds of charge (the ability's warmup), and the bullet's speed in cells per second.</summary>
        public float Charge, Speed;
        /// <summary>The embed's contact counts as this many bounces for its sparks: the rule's bounce limit.</summary>
        public int MaxBounces;

        public float FireAt => BankShotTiming.Lead + Charge;
        public float EndAt => FireAt + (float)Path.Length / Speed;
        public float Duration => EndAt + BankShotTiming.Hold + BankShotTiming.Tail;
        /// <summary>When the bullet reaches bounce <paramref name="index"/> (from 0).</summary>
        public float BounceAt(int index) => FireAt + (float)Path.Bounces[index].D / Speed;
        public Vector2 Map(double x, double z) => new Vector2((float)x + Offset.x, (float)z + Offset.y);
    }

    /// <summary>
    /// Bank Shot's charged shot: when each part happens and the picture's fixed sizes. Seconds in,
    /// numbers out, no drawing and no map. The port of Tools/VfxLab/web/sketches/bank-shot.js and
    /// lib/bank-shot.js; the constants are their defaults. Damage, bounces, range, speed and the
    /// charge time are XML fields on the ability (CompProperties_BankShotCharge); the Script
    /// numbers here are only the preview's.
    ///
    /// Order, from the cast's clock: 0 aim (the path is shown), <see cref="Lead"/> the charge
    /// begins, FireAt the shot, then one bounce per wall contact at its distance / speed, then the
    /// hit or the embed at EndAt, the result held for <see cref="Hold"/>, then <see cref="Tail"/>.
    /// </summary>
    public static class BankShotTiming
    {
        /// <summary>Rest before the charge, rest after the result, and how long the aim line and the charge glow take to fade after the shot.</summary>
        public const float Lead = 0.2f, Tail = 0.3f, Fade = 0.1f;
        /// <summary>The trace fades over this; the result is held this long.</summary>
        public const float TrailLife = 0.5f, Hold = 1.5f;
        /// <summary>The gun and the bullet fly this many cells up. The grip and the muzzle are this far from the caster's feet along the aim.</summary>
        public const float HandHeight = 0.5f, GripAlong = 0.12f, MuzzleAlong = 0.5f;
        /// <summary>Where the bullet meets a standing pawn, cells up.</summary>
        public const float ChestHeight = 0.45f;
        /// <summary>Camera shakes: the shot, each bounce, the hit or embed.</summary>
        public const float FireShake = 0.015f, BounceShake = 0.01f, EndShake = 0.02f;
        /// <summary>The muzzle recoil: how far the gun slides back, over how long.</summary>
        public const float Kick = 0.09f, KickTime = 0.14f;

        // The preview's script: the sketch's default sliders.
        public const float ScriptCharge = 1.5f, ScriptSpeed = 28f, ScriptRange = 30f;
        public const int ScriptMaxBounces = 3;

        public static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;

        /// <summary>The tracer is wider and hotter with every bounce taken, so the damage climb shows.</summary>
        public static float Heat(int bounces, int maxBounces) => Mathf.Clamp01(bounces / (float)Mathf.Max(1, maxBounces));
        public static float TracerWidth(int bounces) => 0.07f + 0.025f * bounces;

        /// <summary>The preview's shot for one of the sketch's layouts, with the scene cell's centre at <paramref name="origin"/>.</summary>
        public static BankShotShot Script(BankShotPath.Scene scene, Vector2 origin)
        {
            BankShotPath.Layout layout = BankShotPath.For(scene);
            return new BankShotShot
            {
                Path = layout.Shot(0, MuzzleAlong, ScriptMaxBounces, ScriptRange),
                Offset = origin,
                Caster = origin + new Vector2(layout.CasterX, layout.CasterZ),
                Aim = (float)layout.Aim,
                Charge = ScriptCharge,
                Speed = ScriptSpeed,
                MaxBounces = ScriptMaxBounces,
            };
        }
    }
}
