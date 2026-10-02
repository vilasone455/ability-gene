using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace RimArt.VfxLab
{
    /// <summary>
    /// The mod's def XML (1.6/Defs), for previews that read their balance numbers from the defs: the recorder has no
    /// DefDatabase, so it reads the same files. Plain values only: a def's own elements, no inheritance or patches.
    /// </summary>
    public static class ModDefs
    {
        private static Dictionary<string, XElement> defs;

        /// <summary>
        /// Sets every public float field of <paramref name="into"/> from the first element of that name inside the first
        /// of <paramref name="defNames"/> that has one (the field <c>range</c> finds a verb's range). A field no def has
        /// stops the recording: a preview drawn with a made-up number would compare as if it were right.
        /// </summary>
        public static T Fill<T>(T into, params string[] defNames)
        {
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType == typeof(float)))
            {
                XElement found = defNames.Select(Def).Select(d => d.Descendants(field.Name).FirstOrDefault()).FirstOrDefault(e => e != null)
                    ?? throw new InvalidDataException($"{typeof(T).Name}.{field.Name}: no <{field.Name}> in {string.Join(", ", defNames)}");
                field.SetValue(into, float.Parse(found.Value, CultureInfo.InvariantCulture));
            }
            return into;
        }

        private static XElement Def(string defName)
        {
            if (defs == null)
            {
                defs = new Dictionary<string, XElement>();
                foreach (string file in Directory.EnumerateFiles(DefsFolder(), "*.xml", SearchOption.AllDirectories))
                    foreach (XElement def in XDocument.Load(file).Root.Elements())
                    {
                        string name = def.Element("defName")?.Value;
                        if (name != null) defs[name] = def;
                    }
            }
            return defs.TryGetValue(defName, out XElement d) ? d : throw new InvalidDataException($"No def named {defName} in 1.6/Defs");
        }

        private static string DefsFolder()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "1.6", "Defs")))
                    return Path.Combine(dir.FullName, "1.6", "Defs");
            throw new DirectoryNotFoundException("1.6/Defs not found above the recorder: run it from inside the repository.");
        }
    }
}
