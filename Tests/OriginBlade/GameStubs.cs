// Minimal game boundary doubles. The production study/unlock/restriction code is linked by the project.
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
namespace Verse
{
    public class Def { public string defName; public string LabelCap => defName; }
    public class ThingDef : Def { public bool IsMeleeWeapon, IsRangedWeapon; public List<Tool> tools; }
    public class Tool { public List<ToolCapacityDef> capacities = new(); }
    public class ToolCapacityDef : Def { }
    public class ThingWithComps { public ThingDef def; }
    public class Pawn
    {
        public bool IsColonistPlayerControlled = true, Dead, Destroyed, Spawned = true, ViolentDisabled;
        public int Position;
        public string LabelShortCap => "Test pawn";
        public Story story = new();
        public Skills skills = new();
        public Abilities abilities = new();
        public Health health = new();
        public Equipment equipment = new();
        public Inventory inventory = new();
        public bool WorkTagIsDisabled(WorkTags tags) => ViolentDisabled;
    }
    public class Story { public Traits traits = new(); }
    public class Traits
    {
        public List<Trait> allTraits = new();
        public bool HasTrait(TraitDef def) => allTraits.Any(trait => trait.def == def);
        public void GainTrait(Trait trait) => allTraits.Add(trait);
    }
    public class Skills
    {
        private Dictionary<SkillDef, Skill> skills = new();
        public Skill GetSkill(SkillDef def) { if (!skills.ContainsKey(def)) skills[def] = new(); return skills[def]; }
    }
    public class Skill { public int Level; public bool TotallyDisabled; }
    public class Abilities
    {
        public List<Ability> abilities = new();
        public void RemoveAbility(AbilityDef def) => abilities.RemoveAll(ability => ability.def == def);
    }
    public class Hediff { }
    public class Hediff_Psylink : Hediff { }
    public class HediffSet { public List<Hediff> hediffs = new(); }
    public class Health
    {
        public HediffSet hediffSet = new();
        public void RemoveHediff(Hediff hediff) => hediffSet.hediffs.Remove(hediff);
    }
    public class Inventory { public List<ThingWithComps> innerContainer = new(); }
    public class Equipment
    {
        public bool CanDrop = true;
        public List<ThingWithComps> AllEquipmentListForReading = new(), Dropped = new();
        public bool TryDropEquipment(ThingWithComps thing, out ThingWithComps dropped, int pos, bool forbid)
        {
            dropped = null;
            if (!CanDrop) return false;
            AllEquipmentListForReading.Remove(thing); Dropped.Add(thing); dropped = thing; return true;
        }
        public bool TryTransferEquipmentToContainer(ThingWithComps thing, List<ThingWithComps> container)
        {
            AllEquipmentListForReading.Remove(thing); container.Add(thing); return true;
        }
    }
    public class DefOfAttribute : Attribute { }
    public static class DefOfHelper { public static void EnsureInitializedInCtor(Type t) { } }
    public static class DefDatabase<T> where T : Def { public static T GetNamedSilentFail(string name) => null; }
    public class Game { public RimArt.GameComponent_BladeStudy studies; public T GetComponent<T>() where T : GameComponent => studies as T; }
    public abstract class GameComponent { public virtual void GameComponentTick() { } public virtual void ExposeData() { } }
    public static class Current { public static Game Game; }
    public static class Find { public static Ticks TickManager = new(); public static Letters LetterStack = new(); }
    public class Ticks { public int TicksGame = 250; }
    public class Letters
    {
        public int Count;
        public List<object> Stack = new();
        public void ReceiveLetter(string title, string text, object def, Pawn pawn) => Count++;
        public void ReceiveLetter(object letter) { Count++; Stack.Add(letter); }
        public void RemoveLetter(object letter) => Stack.Remove(letter);
    }
    public class LetterDef : Def { }
    public class DiaOption
    {
        public string Text; public Action action; public bool resolveTree;
        public DiaOption() { } public DiaOption(string text) => Text = text;
    }
    public abstract class ChoiceLetter
    {
        public string title; public string text;
        public virtual bool CanShowInLetterStack => true;
        public virtual IEnumerable<DiaOption> Choices { get { yield break; } }
        public DiaOption Option_Close => new DiaOption("close");
        public virtual void ExposeData() { }
    }
    public static class LetterMaker
    {
        public static object MakeLetter(string title, string text, LetterDef def, Pawn pawn)
            => Activator.CreateInstance(typeof(RimArt.ChoiceLetter_OriginBladeAwakening));
    }
    public static class Messages { public static void Message(string text, Pawn pawn, object def, bool historical) { } }
    public static class TranslateExtension { public static string Translate(this string key, params object[] args) => key; }
    public interface IExposable { void ExposeData(); }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public enum LookMode { Value, Reference, Deep }
    public static class Scribe { public static LoadSaveMode mode; public static Dictionary<string, object> Data = new(); }
    public static class Scribe_References
    {
        public static void Look<T>(ref T value, string key) where T : class
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = Scribe.Data.GetValueOrDefault(key) as T;
        }
    }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T fallback = default)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.Data.TryGetValue(key, out object v) ? (T)v : fallback;
        }
    }
    public static class Scribe_Collections
    {
        public static void Look<T>(ref List<T> value, string key, LookMode mode)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value?.ToList();
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = (Scribe.Data.GetValueOrDefault(key) as List<T>)?.ToList();
        }
    }
}
namespace RimWorld
{
    public enum WorkTags { Violent }
    public class TraitDef : Verse.Def { }
    public class JobDef : Verse.Def { }
    public class Trait { public TraitDef def; public Trait(TraitDef def) => this.def = def; }
    public class SkillDef : Verse.Def { }
    public static class SkillDefOf { public static SkillDef Melee = new(), Crafting = new(); }
    public class AbilityDef : Verse.Def { public bool IsPsycast; }
    public class Ability { public AbilityDef def; }
    public static class MessageTypeDefOf { public static object PositiveEvent = new(); }
    public static class LetterDefOf { public static object NeutralEvent = new(); }
}
