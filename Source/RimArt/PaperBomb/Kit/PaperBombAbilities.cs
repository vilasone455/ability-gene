using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class PaperBombDefOf
    {
        public static AbilityDef AG_PaperBomb_TagThrow;
        public static AbilityDef AG_PaperBomb_TagLine;
        public static AbilityDef AG_PaperBomb_Shroud;
        public static AbilityDef AG_PaperBomb_Detonate;

        static PaperBombDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PaperBombDefOf));
        }
    }

    /// <summary>What the three tag abilities share: they need the scroll in hand and enough tags on it.</summary>
    public abstract class CompAbilityEffect_PaperBomb : CompAbilityEffect
    {
        protected CompTagScroll Scroll => CompTagScroll.HeldBy(parent.pawn);

        /// <summary>The fewest tags a cast can spend.</summary>
        protected abstract int TagsNeeded { get; }

        public override bool CanCast => base.CanCast && Unavailable() == null;

        private string Unavailable()
        {
            CompTagScroll scroll = Scroll;
            if (scroll == null) return "Requires a tag scroll in hand.";
            if (!parent.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return parent.pawn.LabelShortCap + " cannot manipulate.";
            if (scroll.Tags < TagsNeeded) return "Needs " + TagsNeeded + " tags. The scroll has " + scroll.Tags + ".";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override string ExtraTooltipPart()
        {
            CompTagScroll scroll = Scroll;
            return scroll == null ? null : "Tags: " + scroll.LabelRemaining;
        }

        protected static Vector2 Feet(Pawn pawn)
        {
            Vector3 stands = pawn.DrawPos;
            return new Vector2(stands.x, stands.z);
        }
    }

    public class CompProperties_TagThrow : CompProperties_AbilityEffect
    {
        public float fuseSeconds = 2f;
        public float radius = 1.5f;
        public int damage = 35;
        /// <summary>A building the tag is stuck in takes this many times the damage.</summary>
        public float stuckBuildingFactor = 4f;

        public CompProperties_TagThrow()
        {
            compClass = typeof(CompAbilityEffect_TagThrow);
        }
    }

    /// <summary>
    /// Tag Throw. The hit roll is the kunai's (KunaiAccuracy: Melee skill, distance, cover, weather),
    /// made at cast. A miss sends the tag to a scattered cell or into the cover; it still sticks and
    /// bursts there. The flight, the fuse and the burst are MapComponent_PaperBomb's.
    /// </summary>
    public class CompAbilityEffect_TagThrow : CompAbilityEffect_PaperBomb
    {
        public new CompProperties_TagThrow Props => (CompProperties_TagThrow)props;

        protected override int TagsNeeded => 1;

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (!target.IsValid || parent.pawn.Map == null) return null;
            return "Hit chance: " + KunaiAccuracy.For(parent.pawn, parent.verb, target).TotalEstimatedHitChance.ToStringPercent("F0");
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompTagScroll scroll = Scroll;
            if (caster?.Map == null || scroll == null || scroll.Tags < 1 || !target.IsValid) return;
            scroll.Spend(1);
            KunaiAccuracy.Learn(caster, parent, target);
            caster.Map.GetComponent<MapComponent_PaperBomb>().Throw(caster, Feet(caster), Aim(caster, target), Props);
        }

        /// <summary>Where the tag goes. The order is Verb_LaunchProjectile.TryCastShot's: wild miss, then cover, then hit.</summary>
        private LocalTargetInfo Aim(Pawn caster, LocalTargetInfo target)
        {
            Verb verb = parent.verb;
            ShotReport report = KunaiAccuracy.For(caster, verb, target);
            if (!Rand.Chance(report.AimOnTargetChance_IgnoringPosture))
            {
                if (!verb.TryFindShootLineFromTo(caster.Position, target, out ShootLine line))
                    line = new ShootLine(caster.Position, target.Cell);
                line.ChangeDestToMissWild(report.AimOnTargetChance_StandardTarget, false, caster.Map);
                return line.Dest;
            }
            Thing cover = report.GetRandomCoverToMissInto();
            if (cover != null && target.Thing != null && target.Thing.def.CanBenefitFromCover && !Rand.Chance(report.PassCoverChance))
                return cover;
            return target;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (target.IsValid) GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }
    }

    public class CompProperties_TagLine : CompProperties_AbilityEffect
    {
        public int maxTags = 8;
        public float perTagSeconds = 0.1f;
        public float radius = 1.1f;
        public int damage = 30;
        public int lifetimeTicks = 60000;

        public CompProperties_TagLine()
        {
            compClass = typeof(CompAbilityEffect_TagLine);
        }
    }

    /// <summary>
    /// Tag Line. The strip runs from the caster toward the chosen tile: 2.5 tiles of plain strip, then
    /// one tag per tile up to the tile, the props' most, or the tags left on the scroll. A wall or
    /// other solid thing on the line stops it short.
    /// </summary>
    public class CompAbilityEffect_TagLine : CompAbilityEffect_PaperBomb
    {
        public new CompProperties_TagLine Props => (CompProperties_TagLine)props;

        protected override int TagsNeeded => 1;

        /// <summary>How many tags a line toward <paramref name="target"/> takes, and its direction.</summary>
        private int TagsToward(LocalTargetInfo target, out Vector2 toward)
        {
            Pawn caster = parent.pawn;
            Vector2 feet = Feet(caster), run = new Vector2(target.Cell.x + 0.5f, target.Cell.z + 0.5f) - feet;
            float distance = run.magnitude;
            toward = distance < 0.01f ? Vector2.right : run / distance;
            int wanted = Mathf.Min(Props.maxTags, Scroll?.Tags ?? 0, Mathf.FloorToInt(distance - PaperBombTagLineTiming.Leader + 0.5f) + 1);
            int clear = 0;
            for (; clear < wanted; clear++)
            {
                Vector2 at = feet + toward * PaperBombTagLineTiming.TagAt(clear);
                IntVec3 cell = new Vector3(at.x, 0f, at.y).ToIntVec3();
                if (!cell.InBounds(caster.Map) || cell.Filled(caster.Map)) break;
            }
            return clear;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;
            if (TagsToward(target, out _) >= 1) return true;
            if (throwMessages) Messages.Message("Too close, or blocked: the first tag lies 2.5 tiles from the caster.", parent.pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompTagScroll scroll = Scroll;
            if (caster?.Map == null || scroll == null || !target.IsValid) return;
            int tags = TagsToward(target, out Vector2 toward);
            if (tags < 1) return;
            scroll.Spend(tags);
            caster.Map.GetComponent<MapComponent_PaperBomb>().Lay(caster, Feet(caster), toward, tags, scroll.tripwire, parent.def.verbProperties.warmupTime, Props);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid) return;
            int tags = TagsToward(target, out Vector2 toward);
            var cells = new List<IntVec3>();
            Vector2 feet = Feet(caster);
            for (int i = 0; i < tags; i++)
            {
                Vector2 at = feet + toward * PaperBombTagLineTiming.TagAt(i);
                cells.Add(new Vector3(at.x, 0f, at.y).ToIntVec3());
            }
            if (cells.Count > 0) GenDraw.DrawFieldEdges(cells);
        }
    }

    public class CompProperties_Shroud : CompProperties_AbilityEffect
    {
        public int tags = 6;
        public float heldSeconds = 2f;
        public float radius = 0.9f;
        /// <summary>Bomb damage to everything in the radius, and the total the target takes.</summary>
        public int damage = 20;
        public int targetDamage = 60;

        public CompProperties_Shroud()
        {
            compClass = typeof(CompAbilityEffect_Shroud);
        }
    }

    /// <summary>Paper Shroud. Six tags fly to one pawn, hold it, and go off together; the flight, the hold and the burst are MapComponent_PaperBomb's.</summary>
    public class CompAbilityEffect_Shroud : CompAbilityEffect_PaperBomb
    {
        public new CompProperties_Shroud Props => (CompProperties_Shroud)props;

        protected override int TagsNeeded => Props.tags;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompTagScroll scroll = Scroll;
            if (caster?.Map == null || scroll == null || scroll.Tags < Props.tags || !(target.Thing is Pawn victim)) return;
            scroll.Spend(Props.tags);
            caster.Map.GetComponent<MapComponent_PaperBomb>().Shroud(caster, Feet(caster), victim, Props);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (target.IsValid) GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }
    }

    public class CompProperties_DetonateTagLine : CompProperties_AbilityEffect
    {
        public CompProperties_DetonateTagLine()
        {
            compClass = typeof(CompAbilityEffect_DetonateTagLine);
        }
    }

    /// <summary>The hand seal: lights every tag line the caster has laid on this map that is down and not already burning.</summary>
    public class CompAbilityEffect_DetonateTagLine : CompAbilityEffect
    {
        private bool HasLine => parent.pawn?.Map?.GetComponent<MapComponent_PaperBomb>()?.HasArmedLine(parent.pawn) ?? false;

        public override bool CanCast => base.CanCast && HasLine;

        public override bool ShouldHideGizmo => !HasLine;

        public override bool GizmoDisabled(out string reason)
        {
            reason = HasLine ? null : "No tag line laid.";
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            parent.pawn?.Map?.GetComponent<MapComponent_PaperBomb>()?.Detonate(parent.pawn);
        }
    }
}
