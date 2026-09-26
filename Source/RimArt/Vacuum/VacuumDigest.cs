using UnityEngine;

namespace RimArt
{
    /// <summary>One Digest as the picture needs it.</summary>
    public sealed class VacuumDigestShot
    {
        public Vector2 Caster;
        /// <summary>The caster's facing, degrees (0 east, 90 north): the wand points this way.</summary>
        public float Aim;
        /// <summary>Kg inside when the chewing starts, in the picture's 100 kg canister, and the seconds it takes to chew them.</summary>
        public float Kg = VacuumDigestTiming.ScriptKg;
        public float Chew = VacuumDigestTiming.ScriptKg * VacuumDigestTiming.ScriptPerKg;
        public float Hold = VacuumDigestTiming.ScriptHold;
        public float Slack = VacuumSuckTiming.Slack;
        /// <summary>The chewing was stopped at this time (the job was interrupted): no burp, the canister sinks from here.</summary>
        public float StopAt = float.MaxValue;
    }

    /// <summary>
    /// Timing of Digest: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/vacuum-digest.js; the constants are that sketch's defaults. In game
    /// the ability fires at <see cref="Chew0"/> (its warmupTime is Rise + 0.05 s) and the stomach drains
    /// over the chew.
    /// </summary>
    public static class VacuumDigestTiming
    {
        public const float ChompHz = 4f, Burp = 0.35f;
        public const int Crumbs = 14;
        public const float BurpShake = 0.04f;

        // The preview's script: the sketch's defaults, 60 kg at 0.05 s per kg.
        public const float ScriptKg = 60f, ScriptPerKg = 0.05f, ScriptHold = 0.8f;
        /// <summary>In game the result shows this long before the canister sinks.</summary>
        public const float GameHold = 0.3f;

        public static float Rise0 => VacuumGraphics.Lead;
        public static float Chew0 => VacuumGraphics.Lead + VacuumGraphics.Rise + 0.05f;
        public static float Done(VacuumDigestShot shot) => Chew0 + shot.Chew;
        public static bool Stopped(VacuumDigestShot shot) => shot.StopAt < Done(shot);
        public static float Sink0(VacuumDigestShot shot) => Stopped(shot) ? Mathf.Max(shot.StopAt, Chew0) : Done(shot) + Burp + shot.Hold;
        /// <summary>The canister is back in the floor: the caster may go.</summary>
        public static float Home(VacuumDigestShot shot) => Sink0(shot) + VacuumGraphics.Sink;
        public static float End(VacuumDigestShot shot) => Home(shot) + VacuumGraphics.Tail;

        /// <summary>The share of the kg chewed by <paramref name="s"/>.</summary>
        public static float Digested(VacuumDigestShot shot, float s) =>
            shot.Chew <= 0f ? (s >= Chew0 ? 1f : 0f) : Mathf.Clamp01((Mathf.Min(s, shot.StopAt) - Chew0) / shot.Chew);

        public static VacuumDigestShot Script(Vector2 centre, float aimDegrees) => new VacuumDigestShot { Caster = centre, Aim = aimDegrees };

        public static float ScriptEnd => End(new VacuumDigestShot());
    }
}
