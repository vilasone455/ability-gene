using System;
using System.Collections.Generic;
using RimArt;
using RimWorld;
using Verse;

static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static ThingDef Blade(string name) => new ThingDef
    {
        defName = name, IsMeleeWeapon = true,
        tools = new List<Tool> { new Tool { capacities = new List<ToolCapacityDef> { OriginBladeDefOf.Cut } } }
    };
    private static Pawn ReadyPawn()
    {
        Pawn pawn = new();
        pawn.skills.GetSkill(SkillDefOf.Melee).Level = 14;
        pawn.skills.GetSkill(SkillDefOf.Crafting).Level = 12;
        return pawn;
    }
    private static void Main()
    {
        OriginBladeDefOf.AG_OriginBlade = new TraitDef();
        OriginBladeDefOf.Cut = new ToolCapacityDef();
        OriginBladeDefOf.Stab = new ToolCapacityDef();
        Current.Game = new Game();
        var studies = Current.Game.studies = new GameComponent_BladeStudy(Current.Game);
        var pawn = ReadyPawn();
        var knife = Blade("Knife");
        Check(OriginBladeUtility.IsBlade(knife), "Cutting melee weapon is eligible");
        var spear = Blade("Spear");
        spear.tools[0].capacities[0] = OriginBladeDefOf.Stab;
        Check(OriginBladeUtility.IsBlade(spear), "Stabbing melee weapon is eligible");
        var gun = Blade("GunWithBayonet"); gun.IsMeleeWeapon = false; gun.IsRangedWeapon = true;
        Check(!OriginBladeUtility.IsBlade(gun), "Gun with sharp melee tools must not count");
        Check(!OriginBladeUtility.IsBlade(new ThingDef { IsMeleeWeapon = true }), "Blunt weapon must not count");
        Check(!OriginBladeUtility.IsBlade(null), "Missing study target is not eligible");
        studies.CompleteStudy(pawn, gun);
        Check(studies.RecordFor(pawn).bladeTypes.Count == 0, "Invalid studies give no progress");
        studies.CompleteStudy(pawn, knife);
        studies.CompleteStudy(pawn, Blade("Knife"));
        Check(studies.RecordFor(pawn).bladeTypes.Count == 1, "Distinct objects/material/quality variants share the same type credit");
        foreach (string name in new[] { "Ikwa", "Spear", "Gladius" }) studies.CompleteStudy(pawn, Blade(name));
        Check(!OriginBladeUtility.HasOrigin(pawn), "Four studies cannot unlock");
        var psychic = new Ability { def = new AbilityDef { IsPsycast = true } };
        var ordinary = new Ability { def = new AbilityDef() };
        pawn.abilities.abilities.AddRange(new[] { psychic, ordinary });
        var injury = new Hediff();
        pawn.health.hediffSet.hediffs.AddRange(new Hediff[] { new Hediff_Psylink(), injury });
        var ranged = new ThingWithComps { def = gun };
        var melee = new ThingWithComps { def = knife };
        pawn.equipment.AllEquipmentListForReading.AddRange(new[] { ranged, melee });
        studies.CompleteStudy(pawn, Blade("Longsword"));
        Check(OriginBladeUtility.HasOrigin(pawn), "Fifth type unlocks at exact skill thresholds");
        Check(pawn.abilities.abilities.Count == 1 && pawn.abilities.abilities.Contains(ordinary), "Only psycasts are removed");
        Check(pawn.health.hediffSet.hediffs.Count == 1 && pawn.health.hediffSet.hediffs.Contains(injury), "Only psylinks are removed");
        Check(pawn.equipment.Dropped.Contains(ranged) && pawn.equipment.AllEquipmentListForReading.Contains(melee), "Ranged weapon is dropped intact and melee retained");
        pawn.skills.GetSkill(SkillDefOf.Melee).Level = 0;
        pawn.skills.GetSkill(SkillDefOf.Crafting).Level = 0;
        studies.GameComponentTick();
        Check(OriginBladeUtility.HasOrigin(pawn) && pawn.story.traits.allTraits.Count == 1 && Find.LetterStack.Count == 1,
            "Skill decline preserves trait without duplicate unlock or notification");
        pawn.Spawned = false;
        pawn.equipment.AllEquipmentListForReading.Add(ranged);
        OriginBladeUtility.EnforceRestrictions(pawn);
        Check(pawn.inventory.innerContainer.Contains(ranged) && !pawn.equipment.AllEquipmentListForReading.Contains(ranged),
            "Off-map restriction transfers the weapon safely to inventory");
        var later = ReadyPawn(); later.skills.GetSkill(SkillDefOf.Crafting).Level = 11;
        foreach (string name in new[] { "Knife", "Ikwa", "Spear", "Gladius", "Longsword" }) studies.CompleteStudy(later, Blade(name));
        Check(!OriginBladeUtility.HasOrigin(later), "Five studies cannot bypass Crafting threshold");
        later.skills.GetSkill(SkillDefOf.Crafting).Level = 12;
        later.skills.GetSkill(SkillDefOf.Melee).Level = 13;
        studies.GameComponentTick();
        Check(!OriginBladeUtility.HasOrigin(later), "Melee threshold also required");
        later.skills.GetSkill(SkillDefOf.Melee).Level = 14;
        later.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled = true;
        studies.GameComponentTick();
        Check(!OriginBladeUtility.HasOrigin(later), "Disabled skill cannot qualify");
        later.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled = false;
        studies.GameComponentTick();
        Check(OriginBladeUtility.HasOrigin(later), "Skill training after study unlocks without another study");
        Check(studies.RecordFor(new Pawn()).bladeTypes.Count == 0, "Study progress is personal");
        var record = studies.RecordFor(later);
        Scribe.mode = LoadSaveMode.Saving; record.ExposeData();
        var loaded = new BladeStudyRecord();
        Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit; loaded.ExposeData();
        Check(loaded.pawn == later && loaded.bladeTypes.Count == 5, "Pawn identity and studied types survive serialization boundary");
        loaded.bladeTypes.Add("Knife"); loaded.bladeTypes.Add(null); loaded.ExposeData();
        Check(loaded.bladeTypes.Count == 5, "Loaded duplicate or missing type names cannot inflate progress");
        Scribe.mode = LoadSaveMode.Inactive;
        var unaffected = ReadyPawn(); unaffected.abilities.abilities.Add(psychic);
        unaffected.equipment.AllEquipmentListForReading.Add(ranged);
        OriginBladeUtility.EnforceRestrictions(unaffected);
        Check(unaffected.abilities.abilities.Contains(psychic) && unaffected.equipment.AllEquipmentListForReading.Contains(ranged),
            "Ordinary pawns retain psycasts and guns");
        Console.WriteLine($"Passed {checks} Origin: Blade checks.");
    }
}
