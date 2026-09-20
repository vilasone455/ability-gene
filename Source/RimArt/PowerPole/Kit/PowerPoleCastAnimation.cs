using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Optional Melee Animation bridge for the pole's casts: one pawn, two hands on a pole that
    /// RimArt draws itself. Without that mod nothing here does anything and the pawn stands still
    /// while the pole moves.
    ///
    /// Like ClapCastAnimation, the clip runs inside the pawn's own cast job, so it is started with
    /// that job as its CustomJobDef, and the cast job owns the clock: the clip's time scale is zero
    /// and every tick is a Seek. Like the throws, each clip is authored for one direction and
    /// ThrowAimWorker turns the hands by what is left to the real aim (<see cref="ThrowAim"/>).
    /// There are four clips per ability and none is mirrored: a mirrored Sweep would swing right to
    /// left while the pole swings left to right.
    /// </summary>
    public static class PowerPoleCastAnimation
    {
        private static readonly string[] Facings = { "East", "North", "South", "West" };
        private static readonly float[] Turns = { 0f, 90f, -90f, 180f };
        private static readonly string[] Prefixes = { "AG_PoleThrust", "AG_PoleSweep", "AG_PolePlant" };

        private static bool resolved, present;
        /// <summary>Why the bridge is not available, for the dev-mode log line.</summary>
        private static string missing;
        private static ConstructorInfo constructor;
        private static MethodInfo trigger, animatorFor, seek, destroy;
        private static FieldInfo startJob, rendererJob, timeScale;
        private static PropertyInfo destroyed;
        private static readonly Def[,] clips = new Def[3, 4];

        public static bool Present { get { Resolve(); return present; } }

        /// <summary><paramref name="toward"/> is the unit direction of the cast. False when nothing plays; the cast goes on without a gesture.</summary>
        public static bool TryStart(Pawn pawn, PowerPoleCastKind kind, Vector2 toward, JobDef job) => Start(pawn, kind, toward, job, out _);

        /// <summary>As <see cref="TryStart"/>, and says which clip started or why none did. For finding out why a cast has no gesture.</summary>
        public static bool Start(Pawn pawn, PowerPoleCastKind kind, Vector2 toward, JobDef job, out string why)
        {
            Resolve();
            if (!present) { why = missing ?? "Melee Animation is not loaded"; return false; }
            if (!CanAnimate(pawn)) { why = "the pawn cannot be animated now (not humanlike, downed, or already in an animation)"; return false; }
            try
            {
                // The dominant axis picks the clip, ties going to east and west, as RimWorld picks a pawn's facing.
                int facing = Mathf.Abs(toward.x) >= Mathf.Abs(toward.y) ? (toward.x >= 0f ? 0 : 3) : (toward.y > 0f ? 1 : 2);
                object start = constructor.Invoke(new object[] { clips[(int)kind, facing], pawn, null });
                startJob.SetValue(start, job);
                object[] args = { null };
                if (!(bool)trigger.Invoke(start, args) || args[0] == null) { why = "Melee Animation refused " + clips[(int)kind, facing].defName; return false; }
                timeScale.SetValue(args[0], 0f);
                ThrowAim.Set(args[0], Mathf.DeltaAngle(Turns[facing], Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg));
                why = clips[(int)kind, facing].defName;
                return true;
            }
            catch (Exception e) { Disable(e); why = "the bridge threw"; return false; }
        }

        /// <summary>False when the pawn has no clip of this job, which is also how a clip that was never started reads.</summary>
        public static bool Seek(Pawn pawn, JobDef job, float seconds)
        {
            object renderer = Ours(pawn, job);
            if (renderer == null) return false;
            try
            {
                timeScale.SetValue(renderer, 0f);
                seek.Invoke(renderer, new object[] { (float?)seconds, 0f, null, false });
                return true;
            }
            catch (Exception e) { Disable(e); return false; }
        }

        public static void Stop(Pawn pawn, JobDef job)
        {
            object renderer = Ours(pawn, job);
            if (renderer == null) return;
            try { destroy.Invoke(renderer, null); }
            catch (Exception e) { Disable(e); }
        }

        private static bool CanAnimate(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike
                || pawn.ParentHolder is not Map || !Present) return false;
            try { return animatorFor.Invoke(null, new object[] { pawn }) == null; }
            catch (Exception e) { Disable(e); return false; }
        }

        // Only ever touch a clip this job started; never seek or stop an unrelated animation.
        private static object Ours(Pawn pawn, JobDef job)
        {
            if (pawn == null || !Present) return null;
            try
            {
                object renderer = animatorFor.Invoke(null, new object[] { pawn });
                if (renderer == null || rendererJob.GetValue(renderer) != job || (bool)destroyed.GetValue(renderer)) return null;
                return renderer;
            }
            catch (Exception e) { Disable(e); return null; }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            Type def = AccessTools.TypeByName("AM.AnimDef");
            Type renderer = AccessTools.TypeByName("AM.AnimRenderer");
            Type start = AccessTools.TypeByName("AM.AnimationStartParameters");
            if (def == null || renderer == null || start == null) { missing = "Melee Animation's types were not found"; return; }
            constructor = start.GetConstructor(new[] { def, typeof(Pawn), typeof(Pawn) });
            trigger = AccessTools.Method(start, "TryTrigger", new[] { renderer.MakeByRefType() });
            startJob = AccessTools.Field(start, "CustomJobDef");
            animatorFor = AccessTools.Method(renderer, "TryGetAnimator", new[] { typeof(Pawn) });
            seek = AccessTools.Method(renderer, "Seek");
            destroy = AccessTools.Method(renderer, "Destroy", Type.EmptyTypes);
            rendererJob = AccessTools.Field(renderer, "CustomJobDef");
            timeScale = AccessTools.Field(renderer, "TimeScale");
            destroyed = AccessTools.Property(renderer, "IsDestroyed");
            bool all = true;
            for (int k = 0; k < Prefixes.Length; k++)
                for (int f = 0; f < Facings.Length; f++)
                {
                    clips[k, f] = GenDefDatabase.GetDefSilentFail(def, Prefixes[k] + Facings[f], false) as Def;
                    if (clips[k, f] != null) continue;
                    all = false;
                    missing = "AnimDef " + Prefixes[k] + Facings[f] + " is not loaded";
                }
            if (constructor == null || trigger == null || startJob == null || animatorFor == null || seek == null || destroy == null
                || rendererJob == null || timeScale == null || destroyed == null)
                missing = "a Melee Animation member was not found: " + (constructor == null ? "constructor " : "") + (trigger == null ? "TryTrigger " : "")
                    + (startJob == null ? "CustomJobDef(start) " : "") + (animatorFor == null ? "TryGetAnimator " : "") + (seek == null ? "Seek " : "")
                    + (destroy == null ? "Destroy " : "") + (rendererJob == null ? "CustomJobDef(renderer) " : "") + (timeScale == null ? "TimeScale " : "")
                    + (destroyed == null ? "IsDestroyed" : "");
            present = all && constructor != null && trigger != null && startJob != null && animatorFor != null && seek != null
                && destroy != null && rendererJob != null && timeScale != null && destroyed != null;
        }

        private static void Disable(Exception e)
        {
            present = false;
            Log.Error("[RimArt] The Power Pole's animation bridge failed and was disabled: " + e);
        }
    }
}
