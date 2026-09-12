using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>One pawn, empty hands, no animation events. Optional Melee Animation bridge.</summary>
    public static class ShinraCastAnimation
    {
        private static bool resolved, present;
        private static ConstructorInfo constructor;
        private static FieldInfo flipX;
        private static MethodInfo trigger, animatorFor;
        private static PropertyInfo currentTime, duration, destroyed;
        private static Def east, north, south;

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
                int facing = pawn.Rotation.AsInt;
                Def clip = facing == 0 ? north : facing == 2 ? south : east;
                object start = constructor.Invoke(new object[] { clip, pawn, null });
                flipX.SetValue(start, facing == 3);
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
            flipX = AccessTools.Field(start, "FlipX");
            trigger = AccessTools.Method(start, "TryTrigger", new[] { renderer.MakeByRefType() });
            animatorFor = AccessTools.Method(renderer, "TryGetAnimator", new[] { typeof(Pawn) });
            currentTime = AccessTools.Property(renderer, "CurrentTime");
            duration = AccessTools.Property(renderer, "Duration");
            destroyed = AccessTools.Property(renderer, "IsDestroyed");
            east = GenDefDatabase.GetDefSilentFail(def, "AG_ShinraPush", false) as Def;
            north = GenDefDatabase.GetDefSilentFail(def, "AG_ShinraPushNorth", false) as Def;
            south = GenDefDatabase.GetDefSilentFail(def, "AG_ShinraPushSouth", false) as Def;
            present = constructor != null && flipX != null && trigger != null && animatorFor != null
                && currentTime != null && duration != null && destroyed != null
                && east != null && north != null && south != null;
        }

        private static void Disable(Exception e)
        {
            present = false;
            Log.Error("[RimArt] Shinra Tensei's animation bridge failed and was disabled: " + e);
        }
    }
}
