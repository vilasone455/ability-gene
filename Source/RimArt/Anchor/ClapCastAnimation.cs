using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Optional Melee Animation bridge for the clap and the double clap: one pawn, empty hands, one
    /// facing-free clip each. Without that mod nothing here does anything and the clap casts with
    /// no gesture.
    ///
    /// Two things separate this from the Shinra bridge. The clip runs inside the pawn's own cast
    /// job, so it is started with that job as its CustomJobDef - Melee Animation cancels a clip
    /// whose pawn is in any other job, and its default is a job of its own that would replace the
    /// cast. And the pawn changes cell in the middle of the clip, so the clip's root is moved
    /// after them; a clip otherwise stays drawn where it started.
    ///
    /// The cast job owns the clock: the clip's own time scale is zero and every tick is a Seek.
    /// </summary>
    public static class ClapCastAnimation
    {
        private static bool resolved, present;
        private static ConstructorInfo constructor;
        private static MethodInfo trigger, animatorFor, seek, destroy;
        private static FieldInfo startJob, rendererJob, timeScale, rootTransform;
        private static PropertyInfo destroyed;
        private static Def clap, clapTwice;

        public static bool Present { get { Resolve(); return present; } }

        public static bool TryStart(Pawn pawn, bool twice, JobDef job)
        {
            if (!CanAnimate(pawn)) return false;
            try
            {
                object start = constructor.Invoke(new object[] { twice ? clapTwice : clap, pawn, null });
                startJob.SetValue(start, job);
                object[] args = { null };
                if (!(bool)trigger.Invoke(start, args) || args[0] == null) return false;
                timeScale.SetValue(args[0], 0f);
                return true;
            }
            catch (Exception e) { Disable(e); return false; }
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

        /// <summary>Puts the clip where the pawn now stands. Call after the pawn's Position has changed.</summary>
        public static void MoveTo(Pawn pawn, JobDef job)
        {
            object renderer = Ours(pawn, job);
            if (renderer == null) return;
            try
            {
                // Melee Animation's own MakeAnimationMatrix: the pawn's cell at the pawn's altitude.
                rootTransform.SetValue(renderer, Matrix4x4.TRS(
                    pawn.Position.ToVector3ShiftedWithAltitude(pawn.DrawPos.y), Quaternion.identity, Vector3.one));
            }
            catch (Exception e) { Disable(e); }
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

        // Only ever touch a clip this job started; never seek, move or stop an unrelated animation.
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
            if (def == null || renderer == null || start == null) return;
            constructor = start.GetConstructor(new[] { def, typeof(Pawn), typeof(Pawn) });
            trigger = AccessTools.Method(start, "TryTrigger", new[] { renderer.MakeByRefType() });
            startJob = AccessTools.Field(start, "CustomJobDef");
            animatorFor = AccessTools.Method(renderer, "TryGetAnimator", new[] { typeof(Pawn) });
            seek = AccessTools.Method(renderer, "Seek");
            destroy = AccessTools.Method(renderer, "Destroy", Type.EmptyTypes);
            rendererJob = AccessTools.Field(renderer, "CustomJobDef");
            timeScale = AccessTools.Field(renderer, "TimeScale");
            rootTransform = AccessTools.Field(renderer, "RootTransform");
            destroyed = AccessTools.Property(renderer, "IsDestroyed");
            clap = GenDefDatabase.GetDefSilentFail(def, "AG_Clap", false) as Def;
            clapTwice = GenDefDatabase.GetDefSilentFail(def, "AG_ClapTwice", false) as Def;
            present = constructor != null && trigger != null && startJob != null && animatorFor != null && seek != null
                && destroy != null && rendererJob != null && timeScale != null && rootTransform != null && destroyed != null
                && clap != null && clapTwice != null;
        }

        private static void Disable(Exception e)
        {
            present = false;
            Log.Error("[RimArt] The clap's animation bridge failed and was disabled: " + e);
        }
    }
}
