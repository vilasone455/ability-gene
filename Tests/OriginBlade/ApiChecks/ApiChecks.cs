using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimArt;

static class ApiChecks
{
    static void Main()
    {
        Assembly assembly = typeof(OriginBladeUtility).Assembly;
        int count = 0;
        foreach (Type type in assembly.GetTypes().Where(type => type.Name.StartsWith("Patch_")))
        {
            // Harmony allows three ways to name a target, and this mod uses all of them:
            // a class-level [HarmonyPatch], a static TargetMethod(), or one attribute per
            // patch method. Resolve whichever applies and check the injected parameters
            // against the method actually being patched.
            MethodInfo resolver = type.GetMethod("TargetMethod",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            HarmonyMethod classInfo = type.GetCustomAttribute<HarmonyPatch>()?.info;

            MethodBase classTarget = null;
            if (resolver != null) classTarget = (MethodBase)resolver.Invoke(null, null);
            else if (classInfo?.declaringType != null && classInfo.methodName != null)
                classTarget = classInfo.methodType == MethodType.Getter
                    ? AccessTools.PropertyGetter(classInfo.declaringType, classInfo.methodName)
                    : AccessTools.Method(classInfo.declaringType, classInfo.methodName, classInfo.argumentTypes);

            foreach (MethodInfo patch in type.GetMethods(
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                HarmonyMethod own = patch.GetCustomAttribute<HarmonyPatch>()?.info;
                bool named = patch.Name == "Prefix" || patch.Name == "Postfix"
                    || patch.GetCustomAttribute<HarmonyPrefix>() != null
                    || patch.GetCustomAttribute<HarmonyPostfix>() != null;
                if (!named) continue;

                MethodBase original = classTarget;
                if (own?.declaringType != null && own.methodName != null)
                    original = own.methodType == MethodType.Getter
                        ? AccessTools.PropertyGetter(own.declaringType, own.methodName)
                        : AccessTools.Method(own.declaringType, own.methodName, own.argumentTypes);

                if (original == null)
                    throw new Exception($"Missing Harmony target: {type.Name}.{patch.Name}");

                foreach (ParameterInfo param in patch.GetParameters())
                {
                    string name = param.Name;
                    if (name == "__instance" || name == "__result") continue;
                    if (name.StartsWith("___"))
                    {
                        if (AccessTools.Field(original.DeclaringType, name.Substring(3)) == null)
                            throw new Exception($"Missing injected field: {type.Name}.{name}");
                    }
                    else if (!original.GetParameters().Any(target => target.Name == name))
                        throw new Exception($"Missing original parameter: {type.Name}.{patch.Name}.{name}");
                }
            }
            count++;
        }
        var trait = XDocument.Load("1.6/Defs/TraitDefs/AG_OriginBlade.xml").Root.Element("TraitDef");
        if ((float)trait.Element("commonality") != 0 || (float)trait.Element("commonalityFemale") != 0
            || (bool)trait.Element("allowOnHostileSpawn") || (bool)trait.Element("canBeSuppressed"))
            throw new Exception("Earned trait must not generate randomly or be suppressed");
        var abilities = trait.Descendants("abilities").Single().Elements("li").Select(e => e.Value).ToArray();
        if (!abilities.SequenceEqual(new[] { "AG_Panoply_Rain", "AG_Panoply_Loose", "AG_Panoply_Grasp" }))
            throw new Exception("Trait must grant the complete kit");
        foreach (string file in new[] { "1.6/Defs/TraitDefs/AG_OriginBlade.xml", "1.6/Defs/JobDefs/AG_StudyBlade.xml" })
        foreach (var element in XDocument.Load(file).Descendants())
        {
            string className = (string)element.Attribute("Class")
                ?? (element.Name.LocalName == "driverClass" ? element.Value : null);
            if (className != null && assembly.GetType(className) == null)
                throw new Exception($"Missing class {className}");
        }
        Console.WriteLine($"Passed {count} Harmony target/signature checks against installed RimWorld, plus trait and job definition checks.");
    }
}
