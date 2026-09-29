using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Close screenshots for the pawn height fit (docs/pawn-height-handoff.md): the camera on a cell's
    /// centre at root size 3.5, 111 px per cell, so a crop round a pawn shows a picture against the
    /// real pawn's head, chest and feet. The kits' "height" tests use it.
    /// </summary>
    public static class HeightShots
    {
        public const float Size = 3.5f;

        /// <summary>
        /// Orders the cast and takes a shot at each of <paramref name="ticks"/> (ticks after the order,
        /// ascending), named "<paramref name="name"/> tick", with the camera on <paramref name="camera"/>.
        /// Each shot's line in the results gives where the caster and <paramref name="other"/> stand. The
        /// ability's cooldown is reset first.
        /// </summary>
        public static IEnumerable<int> Cast(RimArtTestContext t, Pawn caster, AbilityDef def, LocalTargetInfo target, IntVec3 camera,
            string name, Pawn other, params int[] ticks) => Cast(t, caster, def, target, LocalTargetInfo.Invalid, camera, name, other, ticks);

        /// <summary>The same, for an ability with a second target (<paramref name="dest"/>).</summary>
        public static IEnumerable<int> Cast(RimArtTestContext t, Pawn caster, AbilityDef def, LocalTargetInfo target, LocalTargetInfo dest,
            IntVec3 camera, string name, Pawn other, params int[] ticks)
        {
            Ability ability = caster.abilities.GetAbility(def);
            if (!t.Check(ability != null, "the caster has " + def.defName)) yield break;
            // These tests are for the pictures: a cooldown left by the last cast is not in the way.
            ability.ResetCooldown();
            bool applies = !def.targetRequired || ability.CanApplyOn(target);
            if (!t.Check(ability.CanCast && applies, def.defName + " can be cast on " + target + " (" + ability.CanCast.Reason + ")")) yield break;
            ability.QueueCastingJob(target, dest);
            int cast = t.Now;
            foreach (int at in ticks)
            {
                int wait = cast + at - t.Now;
                if (wait > 0) yield return wait;
                yield return Shoot(t, name + " " + at, camera, caster, other);
            }
        }

        /// <summary>
        /// Keeps a caster where it is between casts: drafted, so it does not flee the enemies the test
        /// puts near it, and not firing at will, so a gun in its hands stays quiet.
        /// </summary>
        public static Pawn Stay(Pawn pawn)
        {
            pawn.drafter.Drafted = true;
            pawn.drafter.FireAtWill = false;
            return Plain(pawn);
        }

        /// <summary>
        /// Makes a pawn look the same in every run, so before and after shots compare: an average male body
        /// (the fit's measured shape), nothing worn unless <paramref name="strip"/> is false (an Echo's host
        /// wears its costume), and with <paramref name="facing"/> a wait job facing it.
        /// </summary>
        public static Pawn Plain(Pawn pawn, Rot4? facing = null, bool strip = true)
        {
            if (strip) pawn.apparel?.DestroyAll();
            pawn.story.bodyType = BodyTypeDefOf.Male;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            if (facing.HasValue)
            {
                Job wait = JobMaker.MakeJob(JobDefOf.Wait, 6000);
                wait.overrideFacing = facing.Value;
                pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
                pawn.Rotation = facing.Value;
            }
            return pawn;
        }

        /// <summary>A plain hostile target (<see cref="Plain"/>) facing south, told to stand still.</summary>
        public static Pawn Target(RimArtTestContext t, IntVec3 at, bool armed = false) => Plain(t.Enemy(at, armed), Rot4.South);

        /// <summary>A shot now, with a line giving the camera cell and where the pawns stand.</summary>
        public static int Shoot(RimArtTestContext t, string name, IntVec3 camera, params Pawn[] pawns)
        {
            string line = name + ": camera " + camera;
            foreach (Pawn pawn in pawns)
                if (pawn != null) line += " | " + RimArtTestContext.Describe(pawn) + " facing " + pawn.Rotation.ToStringHuman();
            t.Log(line);
            return t.ShotAs(name, camera, Size);
        }
    }
}
