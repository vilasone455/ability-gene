using System;
using System.Collections.Generic;
using System.IO;
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
        string combatExtended = CheckCombatExtended();
        Console.WriteLine($"Passed {count} Harmony target/signature checks against installed RimWorld, "
            + $"plus trait and job definition checks. {combatExtended}");
    }

    /// <summary>
    /// The Combat Extended contract, stated independently of the bridge that uses it.
    ///
    /// CombatExtendedRounds reaches twenty-five members of one class of theirs by name, and a
    /// name that has moved fails at runtime as a single Log.Warning in a game nobody is watching
    /// the log of. Stating the expected members and types here means a CE update that breaks the
    /// bridge breaks this instead.
    ///
    /// Skipped, not failed, when CE is not installed: it is an optional dependency, and a check
    /// that cannot run is not a check that failed.
    /// </summary>
    static string CheckCombatExtended()
    {
        string dll = Environment.GetEnvironmentVariable("CombatExtendedDll") ?? FindCombatExtended();
        if (dll == null) return "Combat Extended not installed, bridge contract not checked.";

        Type projectile = Assembly.LoadFrom(dll).GetType("CombatExtended.ProjectileCE");
        if (projectile == null) throw new Exception("CE is installed but has no CombatExtended.ProjectileCE");

        var expected = new Dictionary<string, string>
        {
            { "exactPosition", "UnityEngine.Vector3" },
            { "LastPos", "UnityEngine.Vector3" },
            { "velocity", "UnityEngine.Vector3" },
            { "origin", "UnityEngine.Vector2" },
            { "Destination", "UnityEngine.Vector2" },
            { "OriginIV3", "Verse.IntVec3" },
            { "shotSpeed", "System.Single" },
            { "initialSpeed", "System.Single" },
            { "shotAngle", "System.Single" },
            { "shotRotation", "System.Single" },
            { "shotHeight", "System.Single" },
            { "startingTicksToImpact", "System.Single" },
            { "intTicksToImpact", "System.Int32" },
            { "FlightTicks", "System.Int32" },
            { "ticksToTruePosition", "System.Int32" },
            { "GravityPerWidth", "System.Single" },
            { "gravity", "System.Double" },
            { "landed", "System.Boolean" },
            { "lerpPosition", "System.Boolean" },
            { "launcher", "Verse.Thing" },
            { "intendedTarget", "Verse.LocalTargetInfo" },
            { "ambientSustainer", "Verse.Sound.Sustainer" },
            { "forcedTrajectoryWorker", "CombatExtended.BaseTrajectoryWorker" },
            { "cachedPredictedPositions", "System.Collections.Generic.List`1[UnityEngine.Vector3]" },
            { "damageAmount", "System.Nullable`1[System.Single]" },
        };

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var member in expected)
        {
            FieldInfo field = projectile.GetField(member.Key, Any);
            if (field == null)
                throw new Exception($"CE bridge: ProjectileCE.{member.Key} is gone");
            if (field.FieldType.ToString() != member.Value)
                throw new Exception($"CE bridge: ProjectileCE.{member.Key} is now {field.FieldType}, "
                    + $"expected {member.Value}");
        }

        // The redirect writes position through the property rather than the field, because CE's
        // setter also moves the thing's cell; it clears and re-reads DamageAmount to keep force
        // absolute; and a held round is stopped by a prefix on Tick.
        if (projectile.GetProperty("ExactPosition")?.GetSetMethod() == null)
            throw new Exception("CE bridge: ProjectileCE.ExactPosition has no public setter");
        PropertyInfo damage = projectile.GetProperty("DamageAmount");
        if (damage == null || damage.PropertyType != typeof(float) || damage.GetSetMethod() == null)
            throw new Exception("CE bridge: ProjectileCE.DamageAmount is not a settable float");
        if (projectile.GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) == null)
            throw new Exception("CE bridge: ProjectileCE no longer declares Tick");
        if (projectile.Assembly.GetType("CombatExtended.LerpedTrajectoryWorker") == null)
            throw new Exception("CE bridge: CombatExtended.LerpedTrajectoryWorker is gone");

        return $"Checked {expected.Count + 4} members of the Combat Extended bridge contract.";
    }

    /// <summary>The workshop copy, whichever folder Steam gave it. Null when CE is not there.</summary>
    static string FindCombatExtended()
    {
        const string Workshop = "/mnt/c/Program Files (x86)/Steam/steamapps/workshop/content/294100";
        if (!Directory.Exists(Workshop)) return null;

        return Directory.EnumerateDirectories(Workshop)
            .Select(folder => Path.Combine(folder, "Assemblies", "CombatExtended.dll"))
            .FirstOrDefault(File.Exists);
    }
}
