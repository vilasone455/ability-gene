using System.Collections.Generic;
using RimWorld;
using UnityEngine;
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

        /// <summary>
        /// Three plain colonists (<see cref="Plain"/>) at <paramref name="low"/>, <paramref name="mid"/> and <paramref name="high"/>,
        /// drawn at a low, a middle and a high height. Core adds a seeded offset of up to +/-0.0366 to each pawn's DrawPos.y
        /// (Pawn_DrawTracker.SeededYOffset, from its thingIDNumber), so a picture placed from AltitudeLayer.Pawn instead of the
        /// pawn's own height looks right on some pawns and not others. Up to 40 colonists are made; the first with an offset
        /// at or below -0.025, within 0.006 of 0 and at or above +0.025 are kept, the rest destroyed. A slot no colonist
        /// filled is null. Each kept pawn's offset is logged.
        /// </summary>
        public static Pawn[] Spread(RimArtTestContext t, IntVec3 low, IntVec3 mid, IntVec3 high)
        {
            var kept = new Pawn[3];
            IntVec3[] cells = { low, mid, high };
            for (int n = 0; n < 40 && (kept[0] == null || kept[1] == null || kept[2] == null); n++)
            {
                Pawn pawn = t.Colonist(mid);
                float y = pawn.Drawer.SeededYOffset;
                int slot = y <= -0.025f ? 0 : Mathf.Abs(y) <= 0.006f ? 1 : y >= 0.025f ? 2 : -1;
                if (slot < 0 || kept[slot] != null)
                {
                    pawn.Destroy();
                    continue;
                }
                kept[slot] = pawn;
                pawn.Position = cells[slot];
                pawn.Notify_Teleported();
            }
            for (int i = 0; i < 3; i++)
                if (kept[i] != null)
                {
                    Plain(kept[i], Rot4.South);
                    t.Log((i == 0 ? "low" : i == 1 ? "middle" : "high") + ": " + kept[i].LabelShort + " (id " + kept[i].thingIDNumber + ") at "
                        + kept[i].Position + ", seeded offset " + kept[i].Drawer.SeededYOffset.ToString("+0.0000;-0.0000") + ", DrawPos.y "
                        + kept[i].DrawPos.y.ToString("0.0000") + " (pawn layer " + AltitudeLayer.Pawn.AltitudeFor().ToString("0.0000") + ")");
                }
            return kept;
        }

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
