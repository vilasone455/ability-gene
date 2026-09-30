// The Trace library as the study code sees it (Source/RimArt/Trace/Kit/TraceLibrary.cs needs the real game for
// materials, quality and labels; the game test "Trace: library 1" covers it). Here an entry is one per weapon def.
using System.Collections.Generic;
using Verse;

namespace RimArt
{
    public sealed class TraceLibraryEntry : IExposable
    {
        public string blade, stuff;
        public void ExposeData() { }
    }

    public static class TraceLibrary
    {
        public static bool Adds(List<TraceLibraryEntry> library, Thing blade) => !library.Exists(e => e.blade == blade.def.defName);

        public static TraceLibraryEntry Learn(List<TraceLibraryEntry> library, Thing blade)
        {
            if (!Adds(library, blade)) return null;
            var entry = new TraceLibraryEntry { blade = blade.def.defName };
            library.Add(entry);
            return entry;
        }

        public static string Label(TraceLibraryEntry entry, int below) => entry.blade;
    }

    public class CompProperties_TraceOn
    {
        public static int QualityBelow => 1;
    }
}
