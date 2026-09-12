using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>One pawn, empty hands, no animation events, one facing-free clip.
    /// Optional Melee Animation bridge.</summary>
    public static class ShinraCastAnimation
    {
        private static bool resolved, present;
        private static ConstructorInfo constructor;
        private static MethodInfo trigger, animatorFor;
        private static PropertyInfo currentTime, duration, destroyed;
        private static Def clip;

        public static bool Present { get { Resolve(); return present; } }

        public sealed class Handle
        {
            private readonly object renderer;
            internal Handle(object renderer) { this.renderer = renderer; }

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
                // The clip carries its own facing. A centred wave has no direction to mirror,
                // so the caster's rotation is not read here at all.
                object start = constructor.Invoke(new object[] { clip, pawn, null });
                object[] args = { null };
                if (!(bool)trigger.Invoke(start, args) || args[0] == null) return false;
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
            currentTime = AccessTools.Property(renderer, "CurrentTime");
            duration = AccessTools.Property(renderer, "Duration");
            destroyed = AccessTools.Property(renderer, "IsDestroyed");
            clip = GenDefDatabase.GetDefSilentFail(def, "AG_ShinraPush", false) as Def;
            present = constructor != null && trigger != null && animatorFor != null
                && currentTime != null && duration != null && destroyed != null && clip != null;
        }

        private static void Disable(Exception e)
        {
            present = false;
            Log.Error("[RimArt] Shinra Tensei's animation bridge failed and was disabled: " + e);
        }
    }
}
