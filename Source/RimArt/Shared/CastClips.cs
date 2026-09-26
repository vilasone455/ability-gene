using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Optional Melee Animation bridge for one kit's cast clips: one pawn, no second pawn, no
    /// animation events. Melee Animation is found by reflection, so RimArt loads without it, and a
    /// cast without it has no gesture.
    ///
    /// The reflection is looked up once for every kit (<see cref="Api"/>). Each instance holds its
    /// own clips and switches itself off on its first exception, so one kit's failure does not stop
    /// another kit's gesture.
    ///
    /// A clip runs one of two ways:
    /// - Free (Gravity Well, Shinra Tensei): started with no job; the caller keeps a
    ///   <see cref="Handle"/> and seeks it.
    /// - Job-owned (clap, Power Pole): started with the cast job as its CustomJobDef. Melee Animation
    ///   cancels a clip whose pawn is in any other job, and its default is a job of its own that would
    ///   replace the cast. <see cref="Seek"/>, <see cref="MoveTo"/> and <see cref="Stop"/> only touch
    ///   a clip that job started.
    /// Either way the caller owns the clock: the clip's time scale is zero and every tick is a Seek.
    /// </summary>
    public sealed class CastClips
    {
        /// <summary>Melee Animation members a kit needs beyond starting, seeking and stopping a clip.</summary>
        [Flags]
        public enum Needs
        {
            None = 0,
            /// <summary>Reading a free clip's time and length, and the global speed setting.</summary>
            Clock = 1,
            /// <summary>Starting a clip inside the cast job and finding it again by that job.</summary>
            Job = 2,
            /// <summary>Moving the clip's root after a pawn that changed cell.</summary>
            Root = 4,
        }

        private readonly string label;
        private readonly Needs needs;
        private readonly string[] defNames;
        private readonly Def[] clips;
        private bool resolved, present;
        private string missing;

        /// <param name="label">Names the kit in the error log: "[RimArt] {label}'s animation bridge failed".</param>
        /// <param name="defNames">The AnimDefs, in the order the kit indexes them.</param>
        public CastClips(string label, Needs needs, params string[] defNames)
        {
            this.label = label;
            this.needs = needs;
            this.defNames = defNames;
            clips = new Def[defNames.Length];
        }

        public bool Present { get { Resolve(); return present; } }

        /// <summary>Why the bridge is not available, for the dev-mode log line.</summary>
        public string Missing { get { Resolve(); return present ? null : missing ?? "Melee Animation is not loaded"; } }

        public string DefName(int clip) => defNames[clip];

        /// <summary>Melee Animation's global speed setting, 1 without it.</summary>
        public float Speed
        {
            get { Resolve(); return present ? (float)Api.globalSpeed.GetValue(Api.settings.GetValue(null)) : 1f; }
        }

        public bool CanAnimate(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike
                || pawn.ParentHolder is not Map || !Present) return false;
            try { return Api.animatorFor.Invoke(null, new object[] { pawn }) == null; }
            catch (Exception e) { Disable(e); return false; }
        }

        /// <summary>
        /// Starts clip number <paramref name="clip"/> with its time scale at zero. With a
        /// <paramref name="job"/> the clip belongs to that job; with null it is free. False when
        /// nothing plays; the cast goes on without a gesture.
        /// </summary>
        public bool TryStart(Pawn pawn, int clip, JobDef job, out object renderer)
        {
            renderer = null;
            if (!CanAnimate(pawn)) return false;
            try
            {
                object start = Api.constructor.Invoke(new object[] { clips[clip], pawn, null });
                if (job != null) Api.startJob.SetValue(start, job);
                object[] args = { null };
                if (!(bool)Api.trigger.Invoke(start, args) || args[0] == null) return false;
                Api.timeScale.SetValue(args[0], 0f);
                renderer = args[0];
                return true;
            }
            catch (Exception e) { Disable(e); return false; }
        }

        // ---- Free clips ----

        public bool TryStart(Pawn pawn, out Handle handle)
        {
            handle = TryStart(pawn, 0, null, out object renderer) ? new Handle(this, renderer) : null;
            return handle != null;
        }

        /// <summary>After a load: takes back the pawn's running clip if it is one of ours, or starts a new one.</summary>
        public bool TryRestore(Pawn pawn, out Handle handle)
        {
            Resolve();
            handle = null;
            if (!present) return false;
            object existing = Api.animatorFor.Invoke(null, new object[] { pawn });
            if (existing != null)
            {
                // Only reclaim our saved clip; never seize an unrelated animation.
                object def = AccessTools.Field(existing.GetType(), "Def")?.GetValue(existing)
                    ?? AccessTools.Property(existing.GetType(), "Def")?.GetValue(existing);
                if (def == null || Array.IndexOf(clips, def) < 0) return false;
                handle = new Handle(this, existing);
                Api.timeScale.SetValue(existing, 0f);
                return true;
            }
            return TryStart(pawn, out handle);
        }

        public sealed class Handle
        {
            private readonly CastClips owner;
            private readonly object renderer;
            internal Handle(CastClips owner, object renderer) { this.owner = owner; this.renderer = renderer; }

            public void Stop() { Api.destroy.Invoke(renderer, null); }

            public bool Seek(float time)
            {
                try
                {
                    if ((bool)Api.destroyed.GetValue(renderer)) return false;
                    Api.timeScale.SetValue(renderer, 0f);
                    Api.seek.Invoke(renderer, new object[] { (float?)time, 0f, null, false });
                    return true;
                }
                catch (Exception e) { owner.Disable(e); return false; }
            }

            public bool Read(out float time, out bool finished)
            {
                time = 0f;
                finished = false;
                try
                {
                    time = Math.Max(0f, (float)Api.currentTime.GetValue(renderer));
                    float length = (float)Api.duration.GetValue(renderer);
                    finished = time >= length;
                    return finished || !(bool)Api.destroyed.GetValue(renderer);
                }
                catch (Exception e) { owner.Disable(e); return false; }
            }
        }

        // ---- Job-owned clips ----

        /// <summary>False when the pawn has no clip of this job, which is also how a clip that was never started reads.</summary>
        public bool Seek(Pawn pawn, JobDef job, float seconds)
        {
            object renderer = Ours(pawn, job);
            if (renderer == null) return false;
            try
            {
                Api.timeScale.SetValue(renderer, 0f);
                Api.seek.Invoke(renderer, new object[] { (float?)seconds, 0f, null, false });
                return true;
            }
            catch (Exception e) { Disable(e); return false; }
        }

        /// <summary>Puts the clip where the pawn now stands. Call after the pawn's Position has changed. Needs <see cref="Needs.Root"/>.</summary>
        public void MoveTo(Pawn pawn, JobDef job)
        {
            object renderer = Ours(pawn, job);
            if (renderer == null) return;
            try
            {
                // Melee Animation's own MakeAnimationMatrix: the pawn's cell at the pawn's altitude.
                Api.rootTransform.SetValue(renderer, Matrix4x4.TRS(
                    pawn.Position.ToVector3ShiftedWithAltitude(pawn.DrawPos.y), Quaternion.identity, Vector3.one));
            }
            catch (Exception e) { Disable(e); }
        }

        public void Stop(Pawn pawn, JobDef job)
        {
            object renderer = Ours(pawn, job);
            if (renderer == null) return;
            try { Api.destroy.Invoke(renderer, null); }
            catch (Exception e) { Disable(e); }
        }

        // Only ever touch a clip this job started; never seek, move or stop an unrelated animation.
        private object Ours(Pawn pawn, JobDef job)
        {
            if (pawn == null || !Present) return null;
            try
            {
                object renderer = Api.animatorFor.Invoke(null, new object[] { pawn });
                if (renderer == null || Api.rendererJob.GetValue(renderer) != job || (bool)Api.destroyed.GetValue(renderer)) return null;
                return renderer;
            }
            catch (Exception e) { Disable(e); return null; }
        }

        private void Resolve()
        {
            if (resolved) return;
            resolved = true;
            if (!Api.Resolve()) { missing = "Melee Animation's types were not found"; return; }
            bool all = true;
            for (int i = 0; i < defNames.Length; i++)
            {
                clips[i] = GenDefDatabase.GetDefSilentFail(Api.animDef, defNames[i], false) as Def;
                if (clips[i] != null) continue;
                all = false;
                missing = "AnimDef " + defNames[i] + " is not loaded";
            }
            string members = Api.MissingMembers(needs);
            if (members != null) missing = "a Melee Animation member was not found: " + members;
            present = all && members == null;
        }

        private void Disable(Exception e)
        {
            present = false;
            Log.Error("[RimArt] " + label + "'s animation bridge failed and was disabled: " + e);
        }

        /// <summary>The Melee Animation members every kit's clips use, looked up once.</summary>
        private static class Api
        {
            private static bool resolved, found;
            internal static Type animDef;
            internal static ConstructorInfo constructor;
            internal static MethodInfo trigger, animatorFor, seek, destroy;
            internal static FieldInfo startJob, rendererJob, timeScale, rootTransform, settings, globalSpeed;
            internal static PropertyInfo currentTime, duration, destroyed;

            /// <summary>False when Melee Animation's types are not loaded at all.</summary>
            internal static bool Resolve()
            {
                if (resolved) return found;
                resolved = true;
                animDef = AccessTools.TypeByName("AM.AnimDef");
                Type renderer = AccessTools.TypeByName("AM.AnimRenderer");
                Type start = AccessTools.TypeByName("AM.AnimationStartParameters");
                if (animDef == null || renderer == null || start == null) return false;
                found = true;
                constructor = start.GetConstructor(new[] { animDef, typeof(Pawn), typeof(Pawn) });
                trigger = AccessTools.Method(start, "TryTrigger", new[] { renderer.MakeByRefType() });
                startJob = AccessTools.Field(start, "CustomJobDef");
                animatorFor = AccessTools.Method(renderer, "TryGetAnimator", new[] { typeof(Pawn) });
                seek = AccessTools.Method(renderer, "Seek");
                destroy = AccessTools.Method(renderer, "Destroy", Type.EmptyTypes);
                rendererJob = AccessTools.Field(renderer, "CustomJobDef");
                timeScale = AccessTools.Field(renderer, "TimeScale");
                rootTransform = AccessTools.Field(renderer, "RootTransform");
                settings = AccessTools.Field(AccessTools.TypeByName("AM.Core"), "Settings");
                globalSpeed = settings == null ? null : AccessTools.Field(settings.FieldType, "GlobalAnimationSpeed");
                currentTime = AccessTools.Property(renderer, "CurrentTime");
                duration = AccessTools.Property(renderer, "Duration");
                destroyed = AccessTools.Property(renderer, "IsDestroyed");
                return true;
            }

            /// <summary>The names of the members <paramref name="needs"/> requires that were not found, or null.</summary>
            internal static string MissingMembers(Needs needs)
            {
                string names = (constructor == null ? "constructor " : "") + (trigger == null ? "TryTrigger " : "")
                    + (animatorFor == null ? "TryGetAnimator " : "") + (seek == null ? "Seek " : "")
                    + (destroy == null ? "Destroy " : "") + (timeScale == null ? "TimeScale " : "")
                    + (destroyed == null ? "IsDestroyed " : "");
                if ((needs & Needs.Clock) != 0)
                    names += (globalSpeed == null ? "GlobalAnimationSpeed " : "") + (currentTime == null ? "CurrentTime " : "")
                        + (duration == null ? "Duration " : "");
                if ((needs & Needs.Job) != 0)
                    names += (startJob == null ? "CustomJobDef(start) " : "") + (rendererJob == null ? "CustomJobDef(renderer) " : "");
                if ((needs & Needs.Root) != 0)
                    names += rootTransform == null ? "RootTransform " : "";
                return names.Length == 0 ? null : names.TrimEnd();
            }
        }
    }
}
