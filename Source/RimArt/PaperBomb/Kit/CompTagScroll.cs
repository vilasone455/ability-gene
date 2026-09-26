using System.Collections.Generic;
using RimWorld;
using RimWorld.Utility;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    public class CompProperties_TagScroll : CompProperties
    {
        public List<AbilityDef> abilities;
        public int maxTags = 20;
        public ThingDef ammoDef;
        public int ammoCountPerTag = 1;
        public int baseReloadTicks = 60;
        public SoundDef soundReload;

        public CompProperties_TagScroll()
        {
            compClass = typeof(CompTagScroll);
        }
    }

    /// <summary>
    /// The tag scroll: grants the kit's abilities to whoever holds it, and holds the tags they spend.
    ///
    /// It is the weapon's CompEquippable, which is why the def lists its comps with Inherit="False":
    /// Core's reload jobs find a weapon's ammunition through Pawn_EquipmentTracker.PrimaryEq being an
    /// IReloadableComp. Core's own CompEquippableAbilityReloadable is not used because it keeps its
    /// charges on one Ability, and Ability.StartCooldown refills them; tags are spent by three
    /// abilities with a cooldown each, so they are counted here.
    ///
    /// <see cref="ItemAbilityGrant"/> keeps the cooldowns on the scroll.
    /// </summary>
    public class CompTagScroll : CompEquippable, IReloadableComp
    {
        private int tags = -1;
        /// <summary>A line laid by the holder is set off by the first hostile to step on a tag, not by Detonate.</summary>
        public bool tripwire;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_TagScroll Props => (CompProperties_TagScroll)props;

        public static CompTagScroll HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompTagScroll>();

        public int Tags => tags < 0 ? Props.maxTags : tags;

        public void Spend(int count) => tags = Mathf.Max(0, Tags - count);

        // IReloadableComp and ICompWithCharges
        public Thing ReloadableThing => parent;
        public ThingDef AmmoDef => Props.ammoDef;
        public int BaseReloadTicks => Props.baseReloadTicks;
        public int MaxCharges => Props.maxTags;
        public int RemainingCharges => Tags;
        public string LabelRemaining => Tags + " / " + Props.maxTags;

        public bool NeedsReload(bool allowForceReload) => Props.ammoDef != null && (allowForceReload ? Tags < Props.maxTags : Tags == 0);

        public int MinAmmoNeeded(bool allowForcedReload) => NeedsReload(allowForcedReload) ? Props.ammoCountPerTag : 0;

        public int MaxAmmoNeeded(bool allowForcedReload) => NeedsReload(allowForcedReload) ? Props.ammoCountPerTag * (Props.maxTags - Tags) : 0;

        public int MaxAmmoAmount() => Props.ammoDef == null ? 0 : Props.ammoCountPerTag * Props.maxTags;

        public void ReloadFrom(Thing ammo)
        {
            if (!NeedsReload(true) || ammo.stackCount < Props.ammoCountPerTag) return;
            int loaded = Mathf.Clamp(ammo.stackCount / Props.ammoCountPerTag, 0, Props.maxTags - Tags);
            ammo.SplitOff(loaded * Props.ammoCountPerTag).Destroy();
            tags = Tags + loaded;
            Props.soundReload?.PlayOneShot(new TargetInfo(parent.PositionHeld, parent.MapHeld));
        }

        public bool CanBeUsed(out string reason)
        {
            reason = Tags > 0 ? null : DisabledReason(MinAmmoNeeded(false), MaxAmmoNeeded(false));
            return Tags > 0;
        }

        public string DisabledReason(int minNeeded, int maxNeeded) =>
            "No tags left. Reload the scroll with " + (Props.ammoDef?.label ?? "tags") + ".";

        public override void PostPostMake()
        {
            base.PostPostMake();
            tags = Props.maxTags;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            grant.Give(pawn, Props.abilities);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            grant.Take(pawn, Props.abilities, this);
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn holder = Holder;
            if (holder == null || !holder.IsColonistPlayerControlled) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "Tag line: tripwire",
                defaultDesc = "On: a laid tag line goes off when the first hostile steps on a tag, starting at that tag. Off: it waits for Detonate. Changing this changes the lines already laid.\n\nTags: " + LabelRemaining,
                icon = ContentFinder<Texture2D>.Get("RimArt/PaperBomb/IconTagLine"),
                isActive = () => tripwire,
                toggleAction = delegate
                {
                    tripwire = !tripwire;
                    MapComponent_PaperBomb.SetTripwire(holder, tripwire);
                },
            };
        }

        public override string CompInspectStringExtra() => "Tags: " + LabelRemaining;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tags, "tags", -1);
            Scribe_Values.Look(ref tripwire, "tripwire");
            grant.ExposeData();
        }
    }
}
