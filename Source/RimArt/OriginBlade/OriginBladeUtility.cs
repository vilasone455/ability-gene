using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class OriginBladeDefOf
    {
        public static TraitDef AG_OriginBlade;
        public static JobDef AG_StudyBlade;
        public static LetterDef AG_OriginBladeAwakening;
        public static ToolCapacityDef Cut;
        public static ToolCapacityDef Stab;

        static OriginBladeDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(OriginBladeDefOf));
    }

    public static class OriginBladeUtility
    {
        public const int MeleeRequired = 14;
        public const int CraftingRequired = 12;
        public const int BladesRequired = 5;
        public const int StudyTicks = 2500;

        public static bool HasOrigin(Pawn pawn) =>
            pawn?.story?.traits?.HasTrait(OriginBladeDefOf.AG_OriginBlade) == true;

        public static bool CanStudy(Pawn pawn) => pawn?.IsColonistPlayerControlled == true
            && pawn.skills != null && pawn.story?.traits != null
            && !pawn.WorkTagIsDisabled(WorkTags.Violent)
            && !pawn.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled;

        // Sharp tools on a gun do not make it a studyable blade.
        public static bool IsBlade(ThingDef def) => def != null && def.IsMeleeWeapon
            && def.tools != null && def.tools.Any(tool => tool.capacities != null
                && tool.capacities.Any(capacity => capacity == OriginBladeDefOf.Cut
                    || capacity == OriginBladeDefOf.Stab));

        public static bool SkillsReady(Pawn pawn) => pawn?.skills != null
            && !pawn.skills.GetSkill(SkillDefOf.Melee).TotallyDisabled
            && !pawn.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled
            && pawn.skills.GetSkill(SkillDefOf.Melee).Level >= MeleeRequired
            && pawn.skills.GetSkill(SkillDefOf.Crafting).Level >= CraftingRequired;

        /// <summary>Every requirement met and the trait not yet taken.</summary>
        public static bool ReadyToAwaken(Pawn pawn)
        {
            if (pawn == null || HasOrigin(pawn) || !CanStudy(pawn) || !SkillsReady(pawn)) return false;
            BladeStudyRecord record = Current.Game?.GetComponent<GameComponent_BladeStudy>()?.RecordFor(pawn);
            return record != null && record.bladeTypes.Count >= BladesRequired;
        }

        /// <summary>
        /// Takes the origin. The single place the trait is granted, so the letter and the
        /// gizmo cannot drift apart on what awakening means.
        /// </summary>
        public static void Awaken(Pawn pawn)
        {
            if (!ReadyToAwaken(pawn)) return;

            pawn.story.traits.GainTrait(new Trait(OriginBladeDefOf.AG_OriginBlade));
            if (!HasOrigin(pawn)) return;

            EnforceRestrictions(pawn);
            Find.LetterStack.ReceiveLetter("AG_OriginBladeAwakenedLabel".Translate(),
                "AG_OriginBladeAwakened".Translate(pawn.LabelShortCap), LetterDefOf.NeutralEvent, pawn);
        }

        public static void EnforceRestrictions(Pawn pawn)
        {
            if (!HasOrigin(pawn)) return;
            if (pawn.abilities != null)
                foreach (Ability ability in pawn.abilities.abilities.ToList())
                    if (ability.def.IsPsycast) pawn.abilities.RemoveAbility(ability.def);

            if (pawn.health != null)
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
                    if (hediff is Hediff_Psylink) pawn.health.RemoveHediff(hediff);

            if (pawn.equipment == null) return;
            foreach (ThingWithComps weapon in pawn.equipment.AllEquipmentListForReading.ToList())
            {
                if (!weapon.def.IsRangedWeapon) continue;
                // Preserve the item even when the pawn is in a caravan or cannot drop here.
                if (pawn.Spawned && pawn.equipment.TryDropEquipment(weapon, out _, pawn.Position, false))
                    continue;
                if (pawn.inventory != null)
                    pawn.equipment.TryTransferEquipmentToContainer(weapon, pawn.inventory.innerContainer);
            }
        }

        public static string ProgressText(Pawn pawn)
        {
            BladeStudyRecord record = Current.Game.GetComponent<GameComponent_BladeStudy>().RecordFor(pawn);
            string understood = string.Join(", ", record.bladeTypes.Select(name =>
                DefDatabase<ThingDef>.GetNamedSilentFail(name)?.LabelCap.ToString() ?? name));
            return "AG_OriginBladeProgress".Translate(pawn.skills.GetSkill(SkillDefOf.Melee).Level,
                MeleeRequired, pawn.skills.GetSkill(SkillDefOf.Crafting).Level, CraftingRequired,
                record.bladeTypes.Count, BladesRequired)
                + "\n\n" + "AG_OriginBladeStudyHelp".Translate()
                + (understood.Length == 0 ? "" : "\n\n" + understood)
                + "\n\n" + "AG_OriginBladeWarning".Translate();
        }
    }
}
