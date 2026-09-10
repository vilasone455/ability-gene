using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The whole contact surface with Melee Animation, and the only place in this mod that
    /// knows the other mod exists.
    ///
    /// Everything here is reflection rather than an assembly reference, for one reason: this
    /// mod's dll has to load and run with Melee Animation absent. A compile-time reference
    /// would be resolved the moment any method touching one of their types is jitted, and the
    /// method that is jitted first is never the one you expected. Reflection makes the
    /// dependency a runtime question with an answer this class can hold: <see cref="Present"/>.
    ///
    /// The gene itself is gated in XML with MayRequire, so in practice Present is true wherever
    /// any of this is reached. It is checked anyway, because a mod list can change under a save
    /// and a null here would otherwise be an exception every tick of a run that cannot finish.
    ///
    /// Their side of the contract is three calls and it is stable across 1.4-1.6:
    ///   AnimDef.GetExecutionAnimationsForPawnAndWeapon  - what this carrier can throw
    ///   OutcomeUtility.GenerateRandomOutcome            - what it does to the person hit
    ///   AnimationStartParameters.TryTrigger             - play it
    /// The damage is theirs on purpose. An execution resolved against the carrier's own weapon,
    /// melee skill and lethality stat is already balanced against every other execution in the
    /// game, and a number invented here would only disagree with it.
    /// </summary>
    public static class MeleeAnimation
    {
        private static bool resolved;
        private static bool present;

        private static Type startParamsType;
        private static MethodInfo executionAnimsFor;
        private static MethodInfo randomOutcome;
        private static MethodInfo tryTrigger;
        private static MethodInfo tryGetAnimator;
        private static FieldInfo flipXField;
        private static FieldInfo outcomeField;
        private static PropertyInfo durationTicksProperty;
        private static FieldInfo fistsOfFuryActive;

        // Everything below is an extra rather than a requirement. Each is null-checked where it is
        // used and simply skipped if it went missing, because none of them is the difference
        // between an arc that works and one that does not - they are the difference between an arc
        // that is theirs and one that only borrows their animations.
        private static MethodInfo tryGetPromotion;
        private static MethodInfo makeOccupiedMask;
        private static Type promotionInputType;
        private static ConstructorInfo reqInputConstructor;
        private static PropertyInfo probabilityProperty;
        private static PropertyInfo gameCompCurrent;
        private static MethodInfo getOrCreateMeleeData;
        private static FieldInfo timeSinceExecuted;

        private static Type reqInputType;

        private static readonly List<object> animBuffer = new List<object>();

        /// <summary>True when Melee Animation is loaded and every member this needs was found.</summary>
        public static bool Present
        {
            get
            {
                Resolve();
                return present;
            }
        }

        /// <summary>
        /// Whether an execution animation exists for whatever this pawn is holding.
        ///
        /// Their shipped set is all weapon-filtered, so an empty-handed carrier gets nothing
        /// back unless Fists of Fury is also loaded and has added fist animations. Asked once
        /// at cast time rather than per hop: a carrier does not change hands mid-arc.
        /// </summary>
        public static bool CanStrikeWith(Pawn pawn)
        {
            if (pawn == null || !Present) return false;
            if (MeleeWeaponDefOf(pawn) != null) return true;

            try
            {
                return fistsOfFuryActive != null && (bool)fistsOfFuryActive.GetValue(null);
            }
            catch (Exception e)
            {
                Disable(e);
                return false;
            }
        }

        /// <summary>Whether this pawn is currently captured by an animation of theirs.</summary>
        public static bool IsAnimating(Pawn pawn)
        {
            if (pawn == null || !Present) return false;

            try
            {
                return tryGetAnimator.Invoke(null, new object[] { pawn }) != null;
            }
            catch (Exception e)
            {
                Disable(e);
                return false;
            }
        }

        /// <summary>
        /// Plays one execution of attacker on victim, here, now.
        ///
        /// The caller has already put the attacker in the right cell: their animations are laid
        /// out with the attacker at (0,0) and the victim at (1,0), so the attacker must be
        /// standing directly west of the victim, or directly east with <paramref name="flipX"/>.
        /// The root transform is read off the attacker's Position, which is why the teleport has
        /// to have happened before this is called and can happen in the same tick.
        /// </summary>
        /// <returns>False if no animation could be started, in which case nothing has happened
        /// to either pawn and the caller should swing the ordinary way.</returns>
        public static bool TryStrike(Pawn attacker, Pawn victim, bool flipX, out int durationTicks)
        {
            durationTicks = 0;
            if (!Present) return false;
            if (attacker == null || victim == null) return false;

            // Their Register() logs an error for a pawn that is dead, downed, unspawned or
            // already animating, so those are answered here rather than in their log.
            if (!CanBeInAnimation(attacker) || !CanBeInAnimation(victim)) return false;

            try
            {
                object anim = PickExecution(attacker);
                if (anim == null) return false;

                object outcome = randomOutcome.Invoke(null, new object[] { attacker, victim, false, null });
                anim = TryPromote(anim, attacker, victim, outcome, flipX) ?? anim;

                object start = Activator.CreateInstance(startParamsType, new object[] { anim, attacker, victim });
                flipXField.SetValue(start, flipX);
                outcomeField.SetValue(start, outcome);

                object[] args = { null };
                bool started = (bool)tryTrigger.Invoke(start, args);
                if (!started || args[0] == null) return false;

                durationTicks = (int)durationTicksProperty.GetValue(args[0]);
                NoteExecuted(attacker);
                return true;
            }
            catch (Exception e)
            {
                Disable(e);
                return false;
            }
        }

        /// <summary>
        /// Their own validity rule, asked before they can complain about it: not dead, not
        /// downed, spawned, and not already inside somebody else's animation.
        /// </summary>
        public static bool CanBeInAnimation(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned) return false;
            if (pawn.ParentHolder is not Map) return false;
            return !IsAnimating(pawn);
        }

        /// <summary>The melee weapon a strike would be thrown with, or null for bare hands.</summary>
        public static ThingDef MeleeWeaponDefOf(Pawn pawn)
        {
            ThingDef def = pawn?.equipment?.Primary?.def;
            return def != null && def.IsMeleeWeapon ? def : null;
        }

        /// <summary>
        /// One of the executions this carrier's weapon allows, weighted the way their own float
        /// menu weights it.
        ///
        /// The weight is their `Probability`, which is a def value multiplied by whatever the
        /// player has set in Melee Animation's own settings - somebody who has turned an animation
        /// off there has said they do not want to see it, and an arc is not the place to argue.
        /// </summary>
        private static object PickExecution(Pawn attacker)
        {
            object result = executionAnimsFor.Invoke(null, new object[] { attacker, MeleeWeaponDefOf(attacker), null });
            if (result is not IEnumerable animations) return null;

            animBuffer.Clear();
            float total = 0f;
            foreach (object anim in animations)
            {
                if (anim == null) continue;
                animBuffer.Add(anim);
                total += Weight(anim);
            }

            if (animBuffer.Count == 0) return null;

            object picked = animBuffer[animBuffer.Count - 1];
            if (total > 0f)
            {
                float roll = Rand.Range(0f, total);
                for (int i = 0; i < animBuffer.Count; i++)
                {
                    roll -= Weight(animBuffer[i]);
                    if (roll > 0f) continue;
                    picked = animBuffer[i];
                    break;
                }
            }

            animBuffer.Clear();
            return picked;
        }

        private static float Weight(object anim)
        {
            if (probabilityProperty == null) return 1f;
            float probability = (float)probabilityProperty.GetValue(anim);
            return probability > 0f ? probability : 0f;
        }

        /// <summary>
        /// Their own promotion pass: the roll that turns a plain execution into the one where the
        /// head comes off.
        ///
        /// This is the difference between an arc that looks like three melee hits and an arc worth
        /// having the gene for, and skipping it was leaving the best half of their animation set on
        /// the shelf. It is their roll, their chances and their space checks - all this does is ask
        /// the question the way their float menu asks it, including handing over a real occupied
        /// mask so a promoted animation cannot finish with somebody standing in a wall.
        /// </summary>
        private static object TryPromote(object anim, Pawn attacker, Pawn victim, object outcome, bool flipX)
        {
            if (tryGetPromotion == null || promotionInputType == null || makeOccupiedMask == null) return null;
            if (reqInputConstructor == null || attacker.Map == null) return null;

            // Nothing to promote a miss into. Their UI makes the same exclusion.
            string outcomeName = outcome?.ToString();
            if (outcomeName == "Nothing" || outcomeName == "Failure") return null;

            object[] maskArgs = { attacker.Map, attacker.Position, null };
            ulong occupied = (ulong)makeOccupiedMask.Invoke(null, maskArgs);

            object input = Activator.CreateInstance(promotionInputType);
            SetInputField(input, "Attacker", attacker);
            SetInputField(input, "Victim", victim);
            SetInputField(input, "ReqInput", reqInputConstructor.Invoke(new object[] { MeleeWeaponDefOf(attacker) }));
            SetInputField(input, "OriginalAnim", anim);
            SetInputField(input, "Outcome", outcome);
            SetInputField(input, "OccupiedMask", occupied);
            SetInputField(input, "FlipX", flipX);

            return tryGetPromotion.Invoke(anim, new object[] { input });
        }

        private static void SetInputField(object input, string name, object value)
        {
            AccessTools.Field(promotionInputType, name)?.SetValue(input, value);
        }

        /// <summary>
        /// Puts the carrier's ordinary execute button on its cooldown, because an arc spent three
        /// of them. Without this the gene would quietly hand out a fourth execution for free.
        /// </summary>
        private static void NoteExecuted(Pawn attacker)
        {
            if (gameCompCurrent == null || getOrCreateMeleeData == null || timeSinceExecuted == null) return;

            object comp = gameCompCurrent.GetValue(null);
            if (comp == null) return;

            object data = getOrCreateMeleeData.Invoke(comp, new object[] { attacker });
            if (data != null) timeSinceExecuted.SetValue(data, 0f);
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            Type animDefType = AccessTools.TypeByName("AM.AnimDef");
            Type rendererType = AccessTools.TypeByName("AM.AnimRenderer");
            Type outcomeUtilityType = AccessTools.TypeByName("AM.Outcome.OutcomeUtility");
            startParamsType = AccessTools.TypeByName("AM.AnimationStartParameters");

            if (animDefType == null || rendererType == null || outcomeUtilityType == null || startParamsType == null)
                return;

            executionAnimsFor = AccessTools.Method(animDefType, "GetExecutionAnimationsForPawnAndWeapon");
            tryGetAnimator = AccessTools.Method(rendererType, "TryGetAnimator", new[] { typeof(Pawn) });
            durationTicksProperty = AccessTools.Property(rendererType, "DurationTicks");
            flipXField = AccessTools.Field(startParamsType, "FlipX");
            outcomeField = AccessTools.Field(startParamsType, "ExecutionOutcome");
            fistsOfFuryActive = AccessTools.Field(AccessTools.TypeByName("AM.Core"), "IsFistsOfFuryActive");

            // Both of these are overloaded, and picking by parameter type keeps this working if
            // they add another: the outcome roll wanted is the one that takes two pawns, and the
            // trigger wanted is the one that hands back the renderer it made.
            randomOutcome = FindMethod(outcomeUtilityType, "GenerateRandomOutcome",
                p => p.Length == 4 && p[0].ParameterType == typeof(Pawn) && p[1].ParameterType == typeof(Pawn));
            tryTrigger = FindMethod(startParamsType, "TryTrigger",
                p => p.Length == 1 && p[0].IsOut);

            Type spaceCheckerType = AccessTools.TypeByName("AM.SpaceChecker");
            Type gameCompType = AccessTools.TypeByName("AM.GameComp");
            reqInputType = AccessTools.TypeByName("AM.Reqs.ReqInput");
            promotionInputType = AccessTools.Inner(animDefType, "PromotionInput");

            tryGetPromotion = AccessTools.Method(animDefType, "TryGetPromotionDef");
            makeOccupiedMask = AccessTools.Method(spaceCheckerType, "MakeOccupiedMask");
            probabilityProperty = AccessTools.Property(animDefType, "Probability");
            gameCompCurrent = AccessTools.Property(gameCompType, "Current");
            getOrCreateMeleeData = AccessTools.Method(gameCompType, "GetOrCreateData");
            timeSinceExecuted = AccessTools.Field(AccessTools.TypeByName("AM.PawnData.PawnMeleeData"), "TimeSinceExecuted");

            // ReqInput has two constructors and one of the arguments this passes is legitimately
            // null - a carrier fighting with their hands. Binding by argument value would leave
            // the overload ambiguous, so the constructor is picked by signature.
            if (reqInputType != null)
                reqInputConstructor = reqInputType.GetConstructor(new[] { typeof(ThingDef) });

            present = executionAnimsFor != null && tryGetAnimator != null && durationTicksProperty != null
                      && flipXField != null && outcomeField != null && randomOutcome != null && tryTrigger != null;

            if (!present)
                Log.Warning("[Ability Genes] Melee Animation is loaded but its API did not look the way the arc tendon expects. The gene will do nothing.");
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
        /// Anything thrown across the bridge takes the bridge down rather than repeating once a
        /// tick for the rest of the session. A silent gene is a bug report; a log full of the
        /// same exception is a bug report nobody can read.
        /// </summary>
        private static void Disable(Exception e)
        {
            present = false;
            Log.Error("[Ability Genes] The arc tendon's Melee Animation bridge threw and has been switched off for this session: " + e);
        }
    }
}
