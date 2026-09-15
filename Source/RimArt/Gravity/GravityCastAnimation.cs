using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>Gravity Well's dedicated one-hand gather and clench clip. Melee Animation
    /// is resolved by reflection so the rest of RimArts still loads without it.</summary>
    public static class GravityCastAnimation
    {
        private static bool resolved, present;
        private static ConstructorInfo constructor;
        private static MethodInfo trigger, animatorFor, seek, destroy;
        private static FieldInfo timeScale, settings, globalSpeed;
        private static PropertyInfo currentTime, duration, destroyed;
        private static Def clip;

        public static bool Present { get { Resolve(); return present; } }

        public static float Speed
        {
            get { Resolve(); return present ? (float)globalSpeed.GetValue(settings.GetValue(null)) : 1f; }
        }

        public static bool TryRestore(Pawn pawn, out Handle handle)
        {
            Resolve();
            handle = null;
            if (!present) return false;
            object existing = animatorFor.Invoke(null, new object[] { pawn });
            if (existing != null)
            {
                // Only reclaim our saved clip; never seize an unrelated animation.
                object def = AccessTools.Field(existing.GetType(), "Def")?.GetValue(existing)
                    ?? AccessTools.Property(existing.GetType(), "Def")?.GetValue(existing);
                if (def != clip) return false;
                handle = new Handle(existing);
                timeScale.SetValue(existing, 0f);
                return true;
            }
            return TryStart(pawn, out handle);
        }

        public sealed class Handle
        {
            private readonly object renderer;
            internal Handle(object renderer) { this.renderer = renderer; }

            public void Stop() { destroy.Invoke(renderer, null); }
            public bool Seek(float time)
            {
                try
                {
                    if ((bool)destroyed.GetValue(renderer)) return false;
                    timeScale.SetValue(renderer, 0f);
                    seek.Invoke(renderer, new object[] { (float?)time, 0f, null, false });
                    return true;
                }
                catch (Exception e) { Disable(e); return false; }
            }

            public bool Read(out float time, out bool finished)
            {
                time = 0f;
                finished = false;
                try
                {
                    time = Math.Max(0f, (float)currentTime.GetValue(renderer));
                    float length = (float)duration.GetValue(renderer);
                    finished = time >= length;
                    return finished || !(bool)destroyed.GetValue(renderer);
                }
                catch (Exception e) { Disable(e); return false; }
            }
        }

        public static bool CanAnimate(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike
                || pawn.ParentHolder is not Map || !Present) return false;
            try { return animatorFor.Invoke(null, new object[] { pawn }) == null; }
            catch (Exception e) { Disable(e); return false; }
        }

        public static bool TryStart(Pawn pawn, out Handle handle)
        {
            handle = null;
            if (!CanAnimate(pawn)) return false;
            try
            {
                // The authored south-facing hand stays visible through the gathering pose.
                object start = constructor.Invoke(new object[] { clip, pawn, null });
                object[] args = { null };
                if (!(bool)trigger.Invoke(start, args) || args[0] == null) return false;
                timeScale.SetValue(args[0], 0f);
                handle = new Handle(args[0]);
                return true;
            }
            catch (Exception e) { Disable(e); return false; }
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
            animatorFor = AccessTools.Method(renderer, "TryGetAnimator", new[] { typeof(Pawn) });
            seek = AccessTools.Method(renderer, "Seek");
            destroy = AccessTools.Method(renderer, "Destroy", Type.EmptyTypes);
            timeScale = AccessTools.Field(renderer, "TimeScale");
            settings = AccessTools.Field(AccessTools.TypeByName("AM.Core"), "Settings");
            globalSpeed = settings == null ? null : AccessTools.Field(settings.FieldType, "GlobalAnimationSpeed");
            currentTime = AccessTools.Property(renderer, "CurrentTime");
            duration = AccessTools.Property(renderer, "Duration");
            destroyed = AccessTools.Property(renderer, "IsDestroyed");
            clip = GenDefDatabase.GetDefSilentFail(def, "AG_GravityChannel", false) as Def;
            present = constructor != null && trigger != null && animatorFor != null
                && seek != null && destroy != null && timeScale != null && globalSpeed != null && currentTime != null && duration != null && destroyed != null && clip != null;
        }

        private static void Disable(Exception e)
        {
            present = false;
            Log.Error("[RimArt] Gravity Well's animation bridge failed and was disabled: " + e);
        }
    }
}
