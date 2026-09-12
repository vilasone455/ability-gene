using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Plays the grenade throw on one pawn, through Melee Animation, facing the target.
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
        /// The three clips, defined in Patch_MeleeAnimation and loaded only alongside their mod.
        ///
        /// A pawn body is drawn at one of four facings, not at a free angle: their DrawPawns
        /// passes a Rot4 to the pawn renderer, and that Rot4 comes from the clip's own
        /// PawnBody.Direction. So the direction of a throw cannot be a runtime parameter the way
        /// a position can - it is baked into whichever clip is played.
        ///
        /// East is mirrored horizontally for west. North and south have dedicated clips,
        /// including the hand placement and draw depth for each facing.
        /// </summary>
        public const string ThrowDefNameEast = "AG_ThrowGrenade";
        public const string ThrowDefNameNorth = "AG_ThrowGrenadeNorth";
        public const string ThrowDefNameSouth = "AG_ThrowGrenadeSouth";

        /// <summary>
        /// The custom part in the clip that holds the thrown object. Deliberately not "ItemA",
        /// which their AddPawn claims and fills with the pawn's melee weapon.
        /// </summary>
        private const string GrenadePartName = "Grenade";

        /// <summary>
        /// How far into the clip the hand opens, as a fraction of its length.
        ///
        /// Single-sourced with the animation: make_throw_anim.py prints this number every time it
        /// writes the json, and the two are the same value or the grenade leaves the hand at a
        /// moment when the hand is not open. Read the script's output if the timing is changed.
        /// </summary>
        public const float ReleaseFraction = 0.4833f;

        private static bool resolved;
        private static bool present;

        private static Type animDefType;
        private static ConstructorInfo startParamsConstructor;
        private static FieldInfo flipXField;
        private static FieldInfo flipYField;
        private static MethodInfo tryTrigger;
        private static MethodInfo tryGetAnimator;
        private static PropertyInfo durationTicksProperty;

        // Extras. Each is null-checked where it is used, because a throw whose grenade draws as
        // the default texture is still a throw, and losing the animation entirely over a renamed
        // helper would be a worse trade.
        private static MethodInfo getPart;
        private static MethodInfo getOverrideByPart;
        private static FieldInfo overrideTextureField;

        private static Def throwDefEast;
        private static Def throwDefNorth;
        private static Def throwDefSouth;

        /// <summary>True when Melee Animation is loaded, its API looks right, and the clip exists.</summary>
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

            public Throw(int durationTicks)
            {
                DurationTicks = durationTicks;
                ReleaseTick = Mathf.RoundToInt(durationTicks * ReleaseFraction);
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
        /// Starts the throw on <paramref name="thrower"/>, aimed at <paramref name="target"/>.
        ///
        /// <paramref name="texturePath"/> replaces whatever the clip draws in the hand, so one
        /// animation serves any number of thrown things; pass null to keep the clip's own.
        /// </summary>
        /// <returns>False if nothing is playing, in which case the caller should throw the
        /// ordinary way and on its own timing. Nothing has happened to the pawn.</returns>
        public static bool TryThrow(Pawn thrower, IntVec3 target, string texturePath, out Throw thrown)
        {
            thrown = default;
            if (!Present || !CanAnimate(thrower)) return false;

            try
            {
                // Which of the four facings this throw is, decided the way RimWorld decides every
                // facing: the dominant axis wins, and a tie goes to east/west because that is the
                // pair with the pawn's widest sprite.
                int dx = target.x - thrower.Position.x;
                int dz = target.z - thrower.Position.z;
                bool sideways = Mathf.Abs(dx) >= Mathf.Abs(dz);

                object anim = sideways ? throwDefEast : dz < 0 ? throwDefSouth : throwDefNorth;
                if (anim == null) return false;

                object start = startParamsConstructor.Invoke(new object[] { anim, thrower, null });
                flipXField.SetValue(start, sideways && dx < 0);
                flipYField.SetValue(start, false);

                object[] args = { null };
                bool started = (bool)tryTrigger.Invoke(start, args);
                if (!started || args[0] == null) return false;

                object renderer = args[0];
                ApplyTexture(renderer, texturePath);

                int duration = (int)durationTicksProperty.GetValue(renderer);
                if (duration <= 0) return false;

                thrown = new Throw(duration);
                return true;
            }
            catch (Exception e)
            {
                Disable(e);
                return false;
            }
        }

        /// <summary>
        /// Puts the thrown thing's own texture in the pawn's hand.
        ///
        /// Their renderer checks the per-part override before the clip's texture path, so this
        /// costs nothing when it is skipped - the grenade in the json draws instead.
        /// </summary>
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

            // The clips live in a folder that only loads alongside their mod, so their absence
            // here is the ordinary way this mod runs, not a fault worth logging.
            throwDefEast = GenDefDatabase.GetDefSilentFail(animDefType, ThrowDefNameEast, false) as Def;
            throwDefNorth = GenDefDatabase.GetDefSilentFail(animDefType, ThrowDefNameNorth, false) as Def;

            throwDefSouth = GenDefDatabase.GetDefSilentFail(animDefType, ThrowDefNameSouth, false) as Def;

            present = startParamsConstructor != null && flipXField != null && flipYField != null
                      && tryTrigger != null && tryGetAnimator != null && durationTicksProperty != null
                      && throwDefEast != null && throwDefNorth != null && throwDefSouth != null;

            if (!present && throwDefEast != null)
                Log.Warning("[RimArt] Melee Animation is loaded but its API did not look the way the throw animation expects. Grenades will be thrown without one.");
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
