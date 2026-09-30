using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One blade in a pawn's trace library: the weapon and its material, and the best quality of that pair the pawn
    /// has studied. Kept by def name, so a save still loads after a weapon mod is removed.
    /// </summary>
    public sealed class TraceLibraryEntry : IExposable
    {
        public string blade, stuff;
        public QualityCategory best = QualityCategory.Normal;

        public ThingDef Blade => blade == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(blade);
        public ThingDef Stuff => stuff == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(stuff);

        public bool Same(TraceLibraryEntry other) => other != null && other.blade == blade && other.stuff == stuff;

        public void ExposeData()
        {
            Scribe_Values.Look(ref blade, "blade");
            Scribe_Values.Look(ref stuff, "stuff");
            Scribe_Values.Look(ref best, "best", QualityCategory.Normal);
        }
    }

    /// <summary>
    /// Trace On's library: every blade the pawn has studied, by weapon and material, with the best quality studied
    /// (Origin: Blade's study job fills it, before and after awakening). A copy has the studied weapon's def and
    /// material and is <see cref="CompProperties_TraceOn.qualityBelow"/> levels below the best one studied.
    ///
    /// Saves from before the library keep only the five blade types of the awakening; they load as entries in the
    /// weapon's default material at normal quality (<see cref="BladeStudyRecord.ExposeData"/>).
    /// </summary>
    public static class TraceLibrary
    {
        public static List<TraceLibraryEntry> Of(Pawn pawn) =>
            Current.Game?.GetComponent<GameComponent_BladeStudy>()?.RecordFor(pawn).library ?? new List<TraceLibraryEntry>();

        private static TraceLibraryEntry EntryFor(Thing blade) => new TraceLibraryEntry
        {
            blade = blade.def.defName,
            stuff = blade.Stuff?.defName,
            best = blade.TryGetQuality(out QualityCategory q) ? q : QualityCategory.Normal,
        };

        /// <summary>Whether studying <paramref name="blade"/> adds to the library: a new weapon and material, or a better quality of one.</summary>
        public static bool Adds(List<TraceLibraryEntry> library, Thing blade)
        {
            TraceLibraryEntry entry = EntryFor(blade);
            TraceLibraryEntry known = library.Find(e => e.Same(entry));
            return known == null || entry.best > known.best;
        }

        /// <summary>Adds the blade or raises its quality; the entry it changed, or null if nothing changed.</summary>
        public static TraceLibraryEntry Learn(List<TraceLibraryEntry> library, Thing blade)
        {
            if (!Adds(library, blade)) return null;
            TraceLibraryEntry entry = EntryFor(blade);
            TraceLibraryEntry known = library.Find(e => e.Same(entry));
            if (known != null)
            {
                known.best = entry.best;
                return known;
            }
            library.Add(entry);
            return entry;
        }

        /// <summary>The quality of a copy: <paramref name="below"/> levels under the best studied, never under awful.</summary>
        public static QualityCategory CopyQuality(TraceLibraryEntry entry, int below) =>
            (QualityCategory)System.Math.Max((int)QualityCategory.Awful, (int)entry.best - below);

        /// <summary>The studied material if the weapon can still be made of it, else the weapon's default; null for a weapon without one.</summary>
        public static ThingDef StuffFor(TraceLibraryEntry entry, ThingDef def)
        {
            if (!def.MadeFromStuff) return null;
            ThingDef stuff = entry.Stuff;
            return stuff != null && GenStuff.AllowedStuffsFor(def).Contains(stuff) ? stuff : GenStuff.DefaultStuffFor(def);
        }

        /// <summary>A copy of the entry, not yet anywhere: the studied weapon in its material, at the copy's quality.</summary>
        public static ThingWithComps MakeCopy(TraceLibraryEntry entry, int below)
        {
            ThingDef def = entry?.Blade;
            if (def == null) return null;
            var copy = (ThingWithComps)ThingMaker.MakeThing(def, StuffFor(entry, def));
            copy.TryGetComp<CompQuality>()?.SetQuality(CopyQuality(entry, below), null);
            return copy;
        }

        private static readonly Dictionary<ThingDef, bool> plain = new Dictionary<ThingDef, bool>();

        /// <summary>
        /// Whether a copy of this weapon is just a weapon. Not a persona weapon (it would bond to the copy and grieve
        /// when it breaks), not a weapon that grants abilities (vanilla's equippable ability, or any of this mod's
        /// weapon comps: Samehada, Chain Sickle, Fūma Shuriken; such a weapon keeps its cooldowns on itself, so every
        /// fresh copy would come with them ready), and not a hero's own weapon (Yamato). Such blades still count as
        /// studied types for Origin: Blade.
        /// </summary>
        public static bool Plain(ThingDef def)
        {
            if (plain.TryGetValue(def, out bool result)) return result;
            result = !def.HasComp<CompBladelinkWeapon>()
                && (def.comps == null || !def.comps.Any(c => c.compClass != null
                    && (c.compClass.Namespace == typeof(TraceLibrary).Namespace || typeof(CompEquippableAbility).IsAssignableFrom(c.compClass))))
                && !DefDatabase<EchoDef>.AllDefsListForReading.Any(e => e.manifestWeapon == def);
            plain[def] = result;
            return result;
        }

        /// <summary>Whether the entry can be traced now: its weapon still exists and is <see cref="Plain"/>.</summary>
        public static bool CanTrace(TraceLibraryEntry entry)
        {
            ThingDef def = entry?.Blade;
            return def != null && Plain(def);
        }

        /// <summary>Why an entry cannot be traced, or null.</summary>
        public static string Refusal(TraceLibraryEntry entry)
        {
            ThingDef def = entry?.Blade;
            if (def == null) return "AG_TraceMissing".Translate(entry?.blade ?? "?");
            if (def.HasComp<CompBladelinkWeapon>()) return "AG_TracePersona".Translate(def.label);
            if (!Plain(def)) return "AG_TraceSpecial".Translate(def.label);
            return null;
        }

        /// <summary>"plasteel longsword (normal)": what a copy of the entry is.</summary>
        public static string Label(TraceLibraryEntry entry, int below)
        {
            ThingDef def = entry.Blade;
            if (def == null) return entry.blade;
            string label = GenLabel.ThingLabel(def, StuffFor(entry, def));
            return def.HasComp<CompQuality>() ? label + " (" + CopyQuality(entry, below).GetLabel() + ")" : label;
        }
    }
}
