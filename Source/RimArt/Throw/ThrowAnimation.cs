using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Plays a throw on one pawn, through Melee Animation, facing the target. Which throw is a
    /// <see cref="Clips"/> set: <see cref="Grenade"/>, <see cref="Kunai"/> or <see cref="Scatter"/>.
    ///
    /// This is a second, separate bridge to the same mod that <see cref="MeleeAnimation"/> talks
    /// to, and the split is deliberate. That one asks for an execution: two pawns, a weapon
    /// filter, an outcome roll, a promotion pass, and it is switched off wholesale unless every
    /// one of those members resolves. A throw needs none of it - one pawn, one named clip, no
    /// damage - and hanging it off the execution bridge's stricter gate would mean a change to
    /// their execution API silently costing this mod its grenade animation too.
    ///
    /// Reflection rather than an assembly reference, for the reason given at length on the other
    /// bridge: this dll has to load and run with Melee Animation absent, and a compile-time
    /// reference resolves whenever the jitter feels like it rather than when the code runs.
    ///
    /// Their side of the contract is small and has been stable since 1.4:
    ///   AnimationStartParameters(AnimDef, Pawn, Pawn) - build a one-pawn start
    ///   TryTrigger(out AnimRenderer)                  - play it
    ///   AnimRenderer.DurationTicks                    - how long it runs
    ///   AnimRenderer.GetPart / GetOverride            - swap the thing in the pawn's hand
    ///
    /// Nothing here damages anybody or moves the grenade. The animation is only the picture; the
    /// caller spawns the projectile on <see cref="Throw.ReleaseTick"/>, so a throw still happens
    /// on its own schedule when this whole class does nothing.
    /// </summary>
    public static class ThrowAnimation
    {
        /// <summary>
        /// One throw style: three clips, defined in Patch_MeleeAnimation and loaded only alongside
        /// their mod, and the moment in them the hand opens.
        ///
        /// A pawn body is drawn at one of four facings, not at a free angle: their DrawPawns
        /// passes a Rot4 to the pawn renderer, and that Rot4 comes from the clip's own
        /// PawnBody.Direction. So the body's facing is baked into whichever clip is played: east
        /// (mirrored for west), north or south, picked by <see cref="Aim"/>.
        ///
        /// The exact direction is not baked in. <see cref="ThrowAim"/> records how far the target
        /// is from the clip's direction, and the renderer worker in Patch_MeleeAnimation turns the
        /// throwing hand and the held item by that much when they are drawn. This is how a 3/4
        /// top-down game aims with three body facings, and how RimWorld draws a gun: body in a
        /// fixed facing, weapon at the real angle.
        /// </summary>
        public sealed class Clips
        {
            public readonly string East;
            public readonly string North;
            public readonly string South;

            /// <summary>
            /// How far into the clip the hand opens, as a fraction of its length.
            ///
            /// Single-sourced with the animation: make_throw_anim.py prints this number for each
            /// style, and ApiChecks compares it with the json. If they differ the object leaves
            /// the hand at a moment when the hand is not open.
            /// </summary>
            public readonly float ReleaseFraction;

            internal Def eastDef;
            internal Def northDef;
            internal Def southDef;
            internal bool looked;

            /// <summary>Names the three clips as prefix, prefix + "North", prefix + "South".</summary>
            public Clips(string prefix, float releaseFraction)
            {
                East = prefix;
                North = prefix + "North";
                South = prefix + "South";
                ReleaseFraction = releaseFraction;
            }

            public IEnumerable<string> All => new[] { East, North, South };

            internal bool Loaded => eastDef != null && northDef != null && southDef != null;
        }

        /// <summary>Overhand lob, 72 ticks, release at tick 35. Frost bomb and mimic beacon.</summary>
        public static readonly Clips Grenade = new Clips("AG_ThrowGrenade", 0.4833f);

        /// <summary>Flat knife throw from beside the ear, 36 ticks, release at tick 18.</summary>
        public static readonly Clips Kunai = new Clips("AG_ThrowKunai", 0.5f);

        /// <summary>Underhand toss of a handful at the ground, 42 ticks, release at tick 18. Makibishi.</summary>
        public static readonly Clips Scatter = new Clips("AG_ThrowScatter", 0.4286f);
        public static readonly Clips Fuma = new Clips("AG_ThrowFuma", 2f / 3f);

        /// <summary>Which of the three clips a throw uses.</summary>
        public enum Facing
        {
            East,
            North,
            South,
        }

        /// <summary>
        /// The clip and aim correction for a throw at offset (dx, dz) in cells.
        ///
        /// The clip is the dominant axis, ties going to east/west, the way RimWorld picks a
        /// pawn's facing. <paramref name="flipX"/> is true for a westward throw. The returned angle
        /// is the target direction minus the played clip's direction, in degrees counter-clockwise
        /// seen from above, between -45 and 45. A zero offset throws east with no correction.
        /// </summary>
        public static Facing Aim(int dx, int dz, out bool flipX, out float offsetDegrees)
        {
            flipX = false;
            offsetDegrees = 0f;
            if (dx == 0 && dz == 0) return Facing.East;

            bool sideways = Math.Abs(dx) >= Math.Abs(dz);
            Facing facing = sideways ? Facing.East : dz < 0 ? Facing.South : Facing.North;
            flipX = sideways && dx < 0;

            double clipDegrees = facing == Facing.North ? 90.0 : facing == Facing.South ? -90.0 : flipX ? 180.0 : 0.0;
            double offset = Math.Atan2(dz, dx) * 180.0 / Math.PI - clipDegrees;
            while (offset > 180.0) offset -= 360.0;
            while (offset <= -180.0) offset += 360.0;
            offsetDegrees = (float)offset;
            return facing;
        }

        /// <summary>
        /// The custom part in the clip that holds the thrown object. Deliberately not "ItemA",
        /// which their AddPawn claims and fills with the pawn's melee weapon. Every clip set uses
        /// this name, including the kunai.
        /// </summary>
        private const string GrenadePartName = "Grenade";

        private static bool resolved;
        private static bool present;

        private static Type animDefType;
        private static ConstructorInfo startParamsConstructor;
        private static FieldInfo flipXField;
        private static FieldInfo flipYField;
        private static FieldInfo customJobDefField;
        private static FieldInfo rendererJobField, rendererTimeScale;
        private static MethodInfo rendererSeek, rendererDestroy;
        private static MethodInfo tryTrigger;
        private static MethodInfo tryGetAnimator;
        private static PropertyInfo durationTicksProperty;

        // Extras. Each is null-checked where it is used, because a throw whose grenade draws as
        // the default texture is still a throw, and losing the animation entirely over a renamed
        // helper would be a worse trade.
        private static MethodInfo getPart;
        private static MethodInfo getOverrideByPart;
        private static FieldInfo overrideTextureField;

        /// <summary>True when Melee Animation is loaded, its API looks right, and the grenade clips exist.</summary>
        public static bool Present
        {
            get
            {
                Resolve();
                return present;
            }
        }

        /// <summary>One running throw: what the caller needs to keep in step with the picture.</summary>
        public readonly struct Throw
        {
            /// <summary>Length of the whole clip. Zero when no animation is playing.</summary>
            public readonly int DurationTicks;

            /// <summary>Ticks from the start of the clip until the grenade leaves the hand.</summary>
            public readonly int ReleaseTick;

            public Throw(int durationTicks, float releaseFraction)
            {
                DurationTicks = durationTicks;
                ReleaseTick = Mathf.RoundToInt(durationTicks * releaseFraction);
            }

            public bool Started => DurationTicks > 0;
        }

        /// <summary>
        /// Whether this pawn could be animated right now.
        ///
        /// Their Register() writes an error to the log for a pawn that is dead, downed, unspawned
        /// or already inside somebody else's animation, so all four are answered here instead.
        /// </summary>
        public static bool CanAnimate(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned) return false;
            if (pawn.ParentHolder is not Map) return false;
            if (!pawn.RaceProps.Humanlike) return false; // Their rig has human hands and nothing else.
            if (!Present) return false;

            try
            {
                return tryGetAnimator.Invoke(null, new object[] { pawn }) == null;
            }
            catch (Exception e)
            {
                Disable(e);
                return false;
            }
        }

        /// <summary>
        /// Starts the <paramref name="clips"/> throw on <paramref name="thrower"/>, aimed at
        /// <paramref name="target"/>.
        ///
        /// <paramref name="texturePath"/> replaces whatever the clip draws in the hand, so one
        /// animation serves any number of thrown things; pass null to keep the clip's own.
        /// </summary>
        /// <returns>False if nothing is playing, in which case the caller should throw the
        /// ordinary way and on its own timing. Nothing has happened to the pawn.</returns>
        public static bool TryThrow(Pawn thrower, IntVec3 target, string texturePath, Clips clips, out Throw thrown)
            => TryThrow(thrower, target, texturePath, clips, out thrown, null);

        public static bool TryThrow(Pawn thrower, IntVec3 target, string texturePath, Clips clips, out Throw thrown,
                                    JobDef customJob)
        {
            thrown = default;
            if (clips == null || !Present || !CanAnimate(thrower)) return false;
            if (!LookUp(clips)) return false;
            if (customJob != null && (customJobDefField == null || rendererJobField == null
                || rendererTimeScale == null || rendererSeek == null || rendererDestroy == null)) return false;

            try
            {
                Facing facing = Aim(target.x - thrower.Position.x, target.z - thrower.Position.z,
                                    out bool flipX, out float offsetDegrees);
                object anim = facing == Facing.East ? clips.eastDef : facing == Facing.North ? clips.northDef : clips.southDef;
                if (anim == null) return false;

                object start = startParamsConstructor.Invoke(new object[] { anim, thrower, null });
                flipXField.SetValue(start, flipX);
                flipYField.SetValue(start, false);
                if (customJob != null) customJobDefField.SetValue(start, customJob);

                object[] args = { null };
                bool started = (bool)tryTrigger.Invoke(start, args);
                if (!started || args[0] == null) return false;

                object renderer = args[0];
                if (customJob != null) rendererTimeScale.SetValue(renderer, 0f);
                ApplyTexture(renderer, texturePath);
                ThrowAim.Set(renderer, offsetDegrees);

                int duration = (int)durationTicksProperty.GetValue(renderer);
                if (duration <= 0) return false;

                thrown = new Throw(duration, clips.ReleaseFraction);
                return true;
            }
            catch (Exception e)
            {
                Disable(e);
                return false;
            }
        }

        /// <summary>
        /// A custom throw job owns the clock, including after loading a saved animation.
        /// Never seek or stop an animation owned by another job.
        /// </summary>
        public static bool UpdateCustomThrow(Pawn pawn, JobDef job, float seconds, bool stop = false)
        {
            Resolve();
            if (!present || rendererJobField == null || rendererSeek == null || rendererDestroy == null) return false;
            try
            {
                object renderer = tryGetAnimator.Invoke(null, new object[] { pawn });
                if (renderer == null || rendererJobField.GetValue(renderer) != job) return false;
                if (stop) rendererDestroy.Invoke(renderer, null);
                else
                {
                    rendererTimeScale.SetValue(renderer, 0f);
                    rendererSeek.Invoke(renderer, new object[] { (float?)seconds, 0f, null, false });
                }
                return !stop;
            }
            catch (Exception e) { Disable(e); return false; }
        }

        /// <summary>Overrides the held object's texture; null keeps the authored texture.</summary>
        private static void ApplyTexture(object renderer, string texturePath)
        {
            if (texturePath == null) return;
            if (getPart == null || getOverrideByPart == null || overrideTextureField == null) return;

            object part = getPart.Invoke(renderer, new object[] { GrenadePartName });
            if (part == null) return;

            object partOverride = getOverrideByPart.Invoke(renderer, new[] { part });
            if (partOverride == null) return;

            Texture2D texture = ContentFinder<Texture2D>.Get(texturePath, false);
            if (texture != null) overrideTextureField.SetValue(partOverride, texture);
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            animDefType = AccessTools.TypeByName("AM.AnimDef");
            Type rendererType = AccessTools.TypeByName("AM.AnimRenderer");
            Type startParamsType = AccessTools.TypeByName("AM.AnimationStartParameters");
            if (animDefType == null || rendererType == null || startParamsType == null) return;

            // Picked by signature rather than by argument value: the second pawn passed is
            // legitimately null, and their params-array constructor would otherwise be an equally
            // good match for (def, pawn, null).
            startParamsConstructor = startParamsType.GetConstructor(
                new[] { animDefType, typeof(Pawn), typeof(Pawn) });

            flipXField = AccessTools.Field(startParamsType, "FlipX");
            flipYField = AccessTools.Field(startParamsType, "FlipY");
            customJobDefField = AccessTools.Field(startParamsType, "CustomJobDef");
            rendererJobField = AccessTools.Field(rendererType, "CustomJobDef");
            rendererTimeScale = AccessTools.Field(rendererType, "TimeScale");
            rendererSeek = AccessTools.Method(rendererType, "Seek");
            rendererDestroy = AccessTools.Method(rendererType, "Destroy", Type.EmptyTypes);
            tryTrigger = FindMethod(startParamsType, "TryTrigger", p => p.Length == 1 && p[0].IsOut);
            tryGetAnimator = AccessTools.Method(rendererType, "TryGetAnimator", new[] { typeof(Pawn) });
            durationTicksProperty = AccessTools.Property(rendererType, "DurationTicks");

            // No namespace on purpose: their AnimData.cs declares none, so AnimPartData and
            // AnimPartOverrideData sit in the global namespace while the file next to it is in
            // AM.Data. The namespaced names are tried second in case that is ever tidied up.
            Type overrideType = AccessTools.TypeByName("AnimPartOverrideData")
                                ?? AccessTools.TypeByName("AM.Data.AnimPartOverrideData");
            Type partType = AccessTools.TypeByName("AnimPartData")
                            ?? AccessTools.TypeByName("AM.Data.AnimPartData");

            getPart = AccessTools.Method(rendererType, "GetPart", new[] { typeof(string) });
            if (partType != null)
                getOverrideByPart = AccessTools.Method(rendererType, "GetOverride", new[] { partType });
            if (overrideType != null)
                overrideTextureField = AccessTools.Field(overrideType, "Texture");

            bool grenadeClips = LookUp(Grenade);

            present = startParamsConstructor != null && flipXField != null && flipYField != null
                      && tryTrigger != null && tryGetAnimator != null && durationTicksProperty != null
                      && grenadeClips;

            if (!present && Grenade.eastDef != null)
                Log.Warning("[RimArt] Melee Animation is loaded but its API did not look the way the throw animation expects. Grenades will be thrown without one.");
        }

        /// <summary>
        /// Finds a clip set's three AnimDefs once. False if any is missing, which is the ordinary
        /// state without Melee Animation: the clips live in a folder that only loads alongside
        /// their mod, so this is not logged.
        /// </summary>
        private static bool LookUp(Clips clips)
        {
            if (!clips.looked)
            {
                clips.looked = true;
                if (animDefType != null)
                {
                    clips.eastDef = GenDefDatabase.GetDefSilentFail(animDefType, clips.East, false) as Def;
                    clips.northDef = GenDefDatabase.GetDefSilentFail(animDefType, clips.North, false) as Def;
                    clips.southDef = GenDefDatabase.GetDefSilentFail(animDefType, clips.South, false) as Def;
                }
            }
            return clips.Loaded;
        }

        private static MethodInfo FindMethod(Type type, string name, Func<ParameterInfo[], bool> matches)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.Name == name && matches(method.GetParameters())) return method;
            }
            return null;
        }

        /// <summary>
        /// Anything thrown across the bridge takes the bridge down for the session rather than
        /// repeating once per cast. Throws then happen without an animation, which is exactly how
        /// they happen for everybody who does not run Melee Animation.
        /// </summary>
        private static void Disable(Exception e)
        {
            present = false;
            Log.Error("[RimArt] The throw animation's Melee Animation bridge threw and has been switched off for this session: " + e);
        }
    }
}
