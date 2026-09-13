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
        string meleeAnimation = CheckMeleeAnimation();
        string mimic = CheckMimicContract();
        CheckShinraAcquisition();
        string distortion = CheckShinraDistortion();
        string sounds = CheckShinraSounds(assembly);
        string retrieval = CheckRetrievalHookContract();
        string kunai = CheckKunaiContract();
        string makibishi = CheckMakibishiContract();
        Console.WriteLine(CheckFumaContract());
        Console.WriteLine($"Passed {count} Harmony target/signature checks against installed RimWorld, "
            + $"plus trait and job definition checks. {combatExtended} {meleeAnimation} {mimic} {distortion} {sounds} {retrieval} {kunai} {makibishi}");
    }

    static void CheckShinraAcquisition()
    {
        var item = XDocument.Load("1.6/Defs/ThingDefs/AG_Shinra_Things.xml").Root.Element("ThingDef");
        var recipe = XDocument.Load("1.6/Defs/RecipeDefs/AG_Shinra_Recipes.xml").Root.Element("RecipeDef");
        var eye = XDocument.Load("1.6/Defs/HediffDefs/AG_Shinra_Kit.xml").Root.Elements("HediffDef")
            .Single(e => (string)e.Element("defName") == "AG_RepulsionEye");
        if ((string)item.Attribute("ParentName") != "BodyPartArchotechBase"
            || (float)item.Element("statBases").Element("MarketValue") != 3200f
            || (string)item.Element("thingSetMakerTags").Element("li") != "RewardStandardCore"
            || item.Element("recipeMaker") != null || item.Element("costList") != null)
            throw new Exception("Repulsion eye must be an uncraftable 3200-silver archotech trade/reward item");
        if ((string)eye.Attribute("ParentName") != "AddedBodyPartBase"
            || (float)eye.Element("addedPartProps").Element("partEfficiency") != 1f
            || (string)eye.Element("spawnThingOnRemoved") != "AG_RepulsionEye"
            || (string)eye.Element("abilities").Element("li") != "AG_ShinraTensei")
            throw new Exception("Repulsion eye must supply normal sight, recovery and the existing ability ID");
        var ingredients = recipe.Element("ingredients");
        if ((string)recipe.Attribute("ParentName") != "SurgeryInstallBodyPartArtificialBase"
            || (int)recipe.Element("skillRequirements").Element("Medicine") != 8
            || (string)recipe.Element("appliedOnFixedBodyParts").Element("li") != "Eye"
            || (string)ingredients.Attribute("Inherit") != "False"
            || ingredients.Elements("li").Count() != 2
            || !ingredients.Elements("li").Any(e => (int)e.Element("count") == 1
                && (string)e.Element("filter").Element("thingDefs")?.Element("li") == "AG_RepulsionEye")
            || !ingredients.Elements("li").Any(e => (int)e.Element("count") == 2
                && (string)e.Element("filter").Element("categories")?.Element("li") == "Medicine"))
            throw new Exception("Repulsion surgery must replace one eye with Medicine 8, one device and two medicine");
    }

    /// <summary>
    /// Shinra Tensei's screen warp borrows RimWorld's own distortion shader and the two maps it
    /// reads. All three are addressed by string at runtime and all three fail silently, so this
    /// pins them to Core: a shader or texture that only ships with a DLC would leave the warp
    /// missing for anyone without that DLC, and nothing in the log would say so.
    ///
    /// The renderer already degrades to no warp when they are absent, so a missing install is
    /// skipped rather than failed. What must not happen quietly is the names moving.
    /// </summary>
    static string CheckShinraDistortion()
    {
        const string Core = "/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Data/Core";
        string mask = Path.Combine(Directory.GetCurrentDirectory(), "Textures/RimArt/Shinra/Distort.png");
        if (!File.Exists(mask))
            throw new Exception("Missing Textures/RimArt/Shinra/Distort.png; run make_shinra_textures.py");
        if (!Directory.Exists(Core)) return "Skipped the distortion contract: RimWorld's Core data is not installed.";

        bool declared = Directory.EnumerateFiles(Core, "*.xml", SearchOption.AllDirectories)
            .Where(file => file.Contains("ShaderTypeDef"))
            .SelectMany(file => XDocument.Load(file).Descendants("ShaderTypeDef"))
            .Any(def => (string)def.Element("defName") == "MoteLargeDistortionWave");
        if (!declared)
            throw new Exception("Core no longer declares the MoteLargeDistortionWave shader type");

        foreach (string texture in new[] { "PsychicDistortionCurrents", "PsycastNoise" })
        {
            bool referenced = Directory.EnumerateFiles(Core, "*.xml", SearchOption.AllDirectories)
                .Any(file => File.ReadAllText(file).Contains(texture));
            if (!referenced)
                throw new Exception($"Core no longer ships {texture}; the distortion maps moved to a DLC");
        }
        return "Checked the distortion shader type and both core distortion maps.";
    }

    /// <summary>
    /// Shinra Tensei's two sounds are this mod's own SoundDefs pointed at Core's audio, so that
    /// the release plays at a sane volume instead of Explosion_Thump's 80. Two things can rot
    /// silently: a DefOf field whose def is not declared, which fails at startup rather than at
    /// the cast; and a Core clip folder that moves, which leaves a SoundDef that resolves and
    /// plays nothing at all.
    /// </summary>
    static string CheckShinraSounds(Assembly assembly)
    {
        var declared = XDocument.Load("1.6/Defs/SoundDefs/AG_Shinra_Sounds.xml").Root
            .Elements("SoundDef").ToArray();
        var names = declared.Select(def => (string)def.Element("defName")).ToArray();

        Type defOf = assembly.GetType("RimArt.ShinraSoundDefOf")
            ?? throw new Exception("RimArt.ShinraSoundDefOf is gone; the sounds have no DefOf");
        foreach (FieldInfo field in defOf.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!names.Contains(field.Name))
                throw new Exception($"ShinraSoundDefOf.{field.Name} names no SoundDef in "
                    + "AG_Shinra_Sounds.xml, which fails at startup rather than at the cast");
        }

        var folders = declared.Descendants("clipFolderPath").Select(e => e.Value).Distinct().ToArray();
        if (folders.Length == 0) throw new Exception("The Shinra sounds reference no audio at all");

        const string Core = "/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Data/Core";
        if (!Directory.Exists(Core))
            return $"Skipped the {folders.Length} Shinra audio paths: RimWorld's Core data is not installed.";
        foreach (string folder in folders)
        {
            bool referenced = Directory.EnumerateFiles(Path.Combine(Core, "Defs", "SoundDefs"), "*.xml")
                .Any(file => File.ReadAllText(file).Contains(folder));
            if (!referenced)
                throw new Exception($"No Core sound still uses '{folder}'; the Shinra sound would "
                    + "resolve and play nothing");
        }
        return $"Checked {names.Length} Shinra sound defs against their DefOf and {folders.Length} core audio paths.";
    }

    /// <summary>
    /// The Melee Animation contract, stated independently of the two bridges that use it.
    ///
    /// Three separate things depend on their API and every one of them fails quietly. The throw
    /// bridge reaches members by name and switches itself off when a name has moved; the AnimDef
    /// in Patch_MeleeAnimation sets XML fields that are silently dropped if renamed; and the json
    /// written by make_throw_anim.py is deserialised straight onto their model classes, so a
    /// renamed property there means an animation that loads with that curve simply missing.
    ///
    /// Skipped, not failed, when Melee Animation is not installed - it is an optional dependency,
    /// and a check that cannot run is not a check that failed.
    /// </summary>
    static string CheckMeleeAnimation()
    {
        string dll = Environment.GetEnvironmentVariable("MeleeAnimationDll") ?? FindMeleeAnimation();
        if (dll == null) return "Melee Animation not installed, bridge contract not checked.";

        Assembly am = Assembly.LoadFrom(dll);
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static;

        Type Need(string name)
            => am.GetType(name) ?? throw new Exception($"Melee Animation bridge: type {name} is gone");

        Type invisibilityPatch = Need("AM.Patches.Patch_InvisibilityUtility_IsPsychologicallyInvisible");
        MethodInfo invisibilityPrefix = invisibilityPatch.GetMethod("Prefix", Any, null,
            new[] { typeof(Verse.Pawn), typeof(bool).MakeByRefType() }, null);
        if (invisibilityPrefix?.ReturnType != typeof(bool))
            throw new Exception("Shinra targeting exception requires Melee Animation's invisibility prefix");

        Type animDef = Need("AM.AnimDef");
        Type renderer = Need("AM.AnimRenderer");
        Type startParams = Need("AM.AnimationStartParameters");
        // These two carry no namespace: their AnimData.cs declares none, unlike every file
        // around it. Stated here rather than guessed at, because the throw bridge's texture
        // override binds by this exact name and skips itself silently when it misses.
        Type partData = Need("AnimPartData");
        Type overrideData = Need("AnimPartOverrideData");
        Type partModel = Need("AM.Data.Model.AnimPartModel");
        Type dataModel = Need("AM.Data.Model.AnimDataModel");
        Type handsVisibility = animDef.GetNestedType("HandsVisibilityData", Any)
            ?? throw new Exception("Melee Animation bridge: AnimDef.HandsVisibilityData is gone");

        // What ThrowAnimation reaches for. Every one of these is resolved by name at runtime and
        // a miss there is a warning in a log nobody reads.
        if (startParams.GetConstructor(new[] { animDef, typeof(Verse.Pawn), typeof(Verse.Pawn) }) == null)
            throw new Exception("Melee Animation bridge: AnimationStartParameters(AnimDef, Pawn, Pawn) is gone");
        // Both mirrors, because the throw needs both: FlipX turns the east clip into the west
        // one, FlipY turns the north clip into the south one.
        foreach (string flip in new[] { "FlipX", "FlipY" })
        {
            if (startParams.GetField(flip, Any) == null)
                throw new Exception($"Melee Animation bridge: AnimationStartParameters.{flip} is gone");
        }
        if (!startParams.GetMethods(Any).Any(m => m.Name == "TryTrigger"
                && m.GetParameters().Length == 1 && m.GetParameters()[0].IsOut))
            throw new Exception("Melee Animation bridge: AnimationStartParameters.TryTrigger(out) is gone");
        if (renderer.GetMethod("TryGetAnimator", new[] { typeof(Verse.Pawn) }) == null)
            throw new Exception("Melee Animation bridge: AnimRenderer.TryGetAnimator(Pawn) is gone");
        if (startParams.GetField("CustomJobDef", Any)?.FieldType != typeof(Verse.JobDef)
            || renderer.GetField("CustomJobDef", Any)?.FieldType != typeof(Verse.JobDef))
            throw new Exception("Fuma needs the custom-job animation bridge to preserve its throw job");
        if (renderer.GetProperty("DurationTicks") == null)
            throw new Exception("Melee Animation bridge: AnimRenderer.DurationTicks is gone");
        if (renderer.GetMethod("GetPart", new[] { typeof(string) }) == null)
            throw new Exception("Melee Animation bridge: AnimRenderer.GetPart(string) is gone");
        if (renderer.GetMethod("GetOverride", new[] { partData }) == null)
            throw new Exception("Melee Animation bridge: AnimRenderer.GetOverride(AnimPartData) is gone");
        foreach (var member in new[] { ("CurrentTime", typeof(float)), ("Duration", typeof(float)),
                                       ("IsDestroyed", typeof(bool)) })
            if (renderer.GetProperty(member.Item1)?.PropertyType != member.Item2)
                throw new Exception($"Shinra animation clock contract changed: {member.Item1}");
        if (overrideData.GetField("Texture", Any) == null)
            throw new Exception("Melee Animation bridge: AnimPartOverrideData.Texture is gone");

        if (renderer.GetField("TimeScale", Any)?.FieldType != typeof(float)
            || renderer.GetMethod("Destroy", Any, null, Type.EmptyTypes, null) == null
            || renderer.GetMethod("Seek", Any, null, new[] { typeof(float?), typeof(float),
                typeof(Action<>).MakeGenericType(am.GetType("AM.Events.EventBase")
                    ?? am.GetTypes().Single(t => t.Name == "EventBase")), typeof(bool) }, null) == null)
            throw new Exception("Shinra requires AnimRenderer.TimeScale, Seek and Destroy");
        Type settingsType = am.GetType("AM.Core").GetField("Settings", Any)?.FieldType;
        if (settingsType?.GetField("GlobalAnimationSpeed", Any)?.FieldType != typeof(float))
            throw new Exception("Shinra animation speed setting contract changed");

        // What Patch_MeleeAnimation/1.6/Defs/AG_Throw_Anims.xml sets. DirectXml drops an unknown
        // node with a warning, so a rename here costs the animation its hands or its pawn count.
        foreach (string field in new[] { "type", "pawnCount", "cellData", "handsVisibility",
                                         "canEditProbability", "jobString", "data" })
        {
            if (animDef.GetField(field, Any) == null)
                throw new Exception($"Melee Animation bridge: AnimDef.{field} is gone");
        }
        foreach (string field in new[] { "pawnIndex", "showMainHand", "showAltHand" })
        {
            if (handsVisibility.GetField(field, Any) == null)
                throw new Exception($"Melee Animation bridge: HandsVisibilityData.{field} is gone");
        }

        int curves = 0, clips = 0;
        // The throw is directional and needs one clip per facing; Shinra Tensei is centred and
        // has exactly one, so a second Shinra clip reappearing here is a mistake worth catching.
        foreach (string clip in ThrowAnimation.Grenade.All.Concat(ThrowAnimation.Kunai.All).Concat(ThrowAnimation.Scatter.All).Concat(ThrowAnimation.Fuma.All)
                     .Select(name => name.Replace("AG_", "RimArt_")).Append("RimArt_ShinraPush"))
        {
            curves += CheckThrowAnimationJson(dataModel, partModel, clip);
            clips++;
        }
        // The C# launches the thrown object at ReleaseFraction of the clip. The json hides the held
        // part at its release time, so the two must agree for every facing of every throw style.
        foreach (var style in new[] { ThrowAnimation.Grenade, ThrowAnimation.Kunai, ThrowAnimation.Scatter, ThrowAnimation.Fuma })
        foreach (string clip in style.All)
        {
            string file = "Animations/" + clip.Replace("AG_", "RimArt_") + ".json";
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            float length = doc.RootElement.GetProperty("Length").GetSingle();
            var held = doc.RootElement.GetProperty("Parts").EnumerateArray()
                .Single(p => p.GetProperty("CustomName").ValueKind == System.Text.Json.JsonValueKind.String
                             && p.GetProperty("CustomName").GetString() == "Grenade");
            float release = held.GetProperty("Curves").GetProperty("GameObject.m_IsActive").GetProperty("Keyframes")
                .EnumerateArray().First(k => k.GetProperty("value").GetSingle() == 0f).GetProperty("time").GetSingle();
            if (Math.Abs(release / length - style.ReleaseFraction) > 0.001f)
                throw new Exception($"{file}: release at {release}s of {length}s is {release / length:0.0000}, "
                    + $"but ThrowAnimation says {style.ReleaseFraction}");
            var patchDefs = XDocument.Load(style == ThrowAnimation.Fuma
                ? "Patch_MeleeAnimation/1.6/Defs/AG_Fuma_Anims.xml"
                : "Patch_MeleeAnimation/1.6/Defs/AG_Throw_Anims.xml").Root.Elements()
                .Where(e => e.Name.LocalName == "AM.AnimDef").ToArray();
            if (!patchDefs.Any(e => (string)e.Element("defName") == clip
                                    && (string)e.Element("data") == Path.GetFileName(file)))
                throw new Exception($"No AM.AnimDef {clip} pointing at {Path.GetFileName(file)}");
        }
        // Three clips plus an aim correction. (dx, dz) -> clip, mirrored, degrees counter-clockwise
        // from the clip's direction. The correction never exceeds 45 degrees either way.
        var E = ThrowAnimation.Facing.East; var N = ThrowAnimation.Facing.North; var S = ThrowAnimation.Facing.South;
        foreach (var (dx, dz, facing, flip, degrees) in new[] {
                     (5, 0, E, false, 0f), (0, 5, N, false, 0f), (0, -5, S, false, 0f), (-5, 0, E, true, 0f),
                     (4, 4, E, false, 45f), (-4, 4, E, true, -45f), (-4, -4, E, true, 45f), (4, -4, E, false, -45f),
                     (4, -5, S, false, 38.66f), (3, 10, N, false, -16.70f), (-10, 3, E, true, -16.70f), (0, 0, E, false, 0f) })
        {
            var got = ThrowAnimation.Aim(dx, dz, out bool gotFlip, out float gotDegrees);
            if (got != facing || gotFlip != flip || Math.Abs(gotDegrees - degrees) > 0.05f)
                throw new Exception($"ThrowAnimation.Aim({dx}, {dz}) = {got} flip {gotFlip} {gotDegrees:0.00}, "
                    + $"expected {facing} flip {flip} {degrees:0.00}");
        }
        foreach (string stale in new[] { "NorthEast", "SouthEast" })
        foreach (var style in new[] { ThrowAnimation.Grenade, ThrowAnimation.Kunai, ThrowAnimation.Scatter, ThrowAnimation.Fuma })
            if (File.Exists($"Animations/{style.East.Replace("AG_", "RimArt_")}{stale}.json"))
                throw new Exception($"{style.East}{stale} clip is back; diagonals are rotated at draw time, not authored");

        // The aim worker. It subclasses their worker, so a changed PreRenderPart signature would
        // stop the type loading and every throw AnimDef naming it would fail at startup.
        Type worker = Need("AM.RendererWorkers.AnimationRendererWorker");
        Type byRef(Type type) => type.MakeByRefType();
        Type unity(string name) => typeof(UnityEngine.Matrix4x4).Assembly.GetType("UnityEngine." + name);
        if (worker.GetMethod("PreRenderPart", Any, null, new[] { byRef(am.GetType("AnimPartSnapshot") ?? Need("AM.AnimPartSnapshot")), byRef(overrideData),
                byRef(typeof(UnityEngine.Mesh)), byRef(typeof(UnityEngine.Matrix4x4)), byRef(typeof(UnityEngine.Material)),
                byRef(unity("MaterialPropertyBlock")) }, null) == null
            || worker.GetMethod("SetupRenderer", Any, null, new[] { renderer }, null) == null)
            throw new Exception("Throw aim: AnimationRendererWorker.PreRenderPart / SetupRenderer changed");
        if (animDef.GetField("rendererWorker", Any)?.FieldType != typeof(Type))
            throw new Exception("Throw aim: AnimDef.rendererWorker (Type) is gone");
        if (renderer.GetField("RootTransform", Any)?.FieldType != typeof(UnityEngine.Matrix4x4)
            || renderer.GetMethod("GetSnapshot", new[] { partData }) == null
            || partData.GetField("Parent", Any)?.FieldType != partData)
            throw new Exception("Throw aim: AnimRenderer.RootTransform / GetSnapshot or AnimPartData.Parent changed");
        const string BridgeDll = "Patch_MeleeAnimation/1.6/Assemblies/RimArt.MeleeAnimation.dll";
        if (!File.Exists(BridgeDll))
            throw new Exception($"{BridgeDll} is missing - build Source/RimArt.MeleeAnimation");
        Type aimWorker = Assembly.LoadFrom(Path.GetFullPath(BridgeDll)).GetType("RimArt.MeleeAnimation.ThrowAimWorker");
        if (aimWorker?.BaseType?.FullName != worker.FullName)
            throw new Exception("Throw aim: RimArt.MeleeAnimation.ThrowAimWorker is missing or no longer an AnimationRendererWorker");
        foreach (var def in XDocument.Load("Patch_MeleeAnimation/1.6/Defs/AG_Throw_Anims.xml").Root.Elements()
                     .Where(e => e.Name.LocalName == "AM.AnimDef"))
            if ((string)def.Element("rendererWorker") != aimWorker.FullName)
                throw new Exception($"{def.Element("defName")?.Value} does not use {aimWorker.FullName}");

        foreach (string stale in new[] { "RimArt_ShinraPushNorth", "RimArt_ShinraPushSouth" })
        {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Animations", stale + ".json")))
                throw new Exception($"{stale}.json is back; the centred wave uses one facing-free clip");
        }
        return $"Checked the Melee Animation bridge contract and {curves} animation curves "
             + $"across {clips} clips.";
    }

    /// <summary>
    /// The generated animation, checked against the classes it is deserialised onto.
    ///
    /// make_throw_anim.py writes this file without Unity, which means nothing but this validates
    /// it. Newtonsoft ignores a property it does not recognise, so a key that has been renamed on
    /// their side - or mistyped on ours - produces an animation that loads happily and plays with
    /// the arm missing. Both the object keys and the curve names are checked, the latter against
    /// the string literals AnimPartData's constructor looks up.
    /// </summary>
    static int CheckThrowAnimationJson(Type dataModel, Type partModel, string clip)
    {
        string Path = $"Animations/{clip}.json";
        if (!File.Exists(Path))
            throw new Exception($"Melee Animation bridge: {Path} is missing - run make_throw_anim.py");

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path));
        var root = doc.RootElement;

        void CheckKeys(System.Text.Json.JsonElement element, Type model, string where)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (model.GetProperty(property.Name) == null)
                    throw new Exception($"Melee Animation bridge: {where} key '{property.Name}' "
                        + $"is not a property of {model.Name}");
            }
        }

        CheckKeys(root, dataModel, "animation");

        // The curve names their AnimPartData constructor asks for. A curve under any other name is
        // read by nobody: the part falls back to its default value and stops moving.
        var known = new HashSet<string>
        {
            "GameObject.m_IsActive", "PawnBody.Direction",
            "Transform.m_LocalPosition.x", "Transform.m_LocalPosition.y", "Transform.m_LocalPosition.z",
            "Transform.localEulerAnglesRaw.x", "Transform.localEulerAnglesRaw.y", "Transform.localEulerAnglesRaw.z",
            "Transform.m_LocalScale.x", "Transform.m_LocalScale.y", "Transform.m_LocalScale.z",
            "AnimatedPart.DataA", "AnimatedPart.DataB", "AnimatedPart.DataC",
            "AnimatedPart.Tint.r", "AnimatedPart.Tint.g", "AnimatedPart.Tint.b", "AnimatedPart.Tint.a",
            "AnimatedPart.FlipX", "AnimatedPart.FlipY",
            "AnimatedPart.SplitDrawMode", "AnimatedPart.FrameIndex",
        };

        int curveCount = 0;
        var names = new List<string>();
        foreach (var part in root.GetProperty("Parts").EnumerateArray())
        {
            CheckKeys(part, partModel, "part");

            string name = part.GetProperty("CustomName").ValueKind == System.Text.Json.JsonValueKind.Null
                ? part.GetProperty("Path").GetString()
                : part.GetProperty("CustomName").GetString();
            names.Add(name);

            foreach (var source in new[] { "Curves", "DefaultValues" })
            foreach (var property in part.GetProperty(source).EnumerateObject())
            {
                if (!known.Contains(property.Name))
                    throw new Exception($"Melee Animation bridge: part '{name}' has {source} entry "
                        + $"'{property.Name}', which their AnimPartData reads under no name");
                if (source == "Curves") curveCount++;
            }
        }

        // Their AddPawn looks these up by name. Both hands must exist because their off-hand
        // lookup is guarded by the main hand's null check and would throw inside their code.
        bool shinra = clip.StartsWith("RimArt_ShinraPush");
        foreach (string required in shinra ? new[] { "BodyA", "HeadA", "HandA", "HandB" }
                                           : new[] { "BodyA", "HandA", "HandB", "Grenade" })
        {
            if (!names.Contains(required))
                throw new Exception($"Melee Animation bridge: animation has no '{required}' part");
        }

        // A part called ItemA would have its texture replaced by whatever melee weapon the thrower
        // is carrying, putting a sword in the hand instead of the bomb.
        if (names.Contains("ItemA"))
            throw new Exception($"Melee Animation bridge: {clip} must not have an ItemA part");
        // The aim worker rotates everything under PawnALift: the throwing hand and the item must be
        // there, the body and the off hand must not.
        if (!shinra)
        {
            var parts = root.GetProperty("Parts").EnumerateArray().ToDictionary(
                p => p.GetProperty("ID").GetInt32(),
                p => (Name: p.GetProperty("CustomName").ValueKind == System.Text.Json.JsonValueKind.Null
                         ? p.GetProperty("Path").GetString() : p.GetProperty("CustomName").GetString(),
                      Parent: p.GetProperty("ParentID").GetInt32()));
            bool Under(string name)
            {
                int id = parts.First(p => p.Value.Name == name).Value.Parent;
                for (; id != 0; id = parts[id].Parent)
                    if (parts[id].Name == "PawnALift") return true;
                return false;
            }
            if (!names.Contains("PawnALift") || !Under("HandA") || !Under("Grenade") || Under("HandB") || Under("BodyA"))
                throw new Exception($"{clip}: HandA and Grenade must be under PawnALift, HandB and BodyA must not");
        }
        if (shinra && (names.Contains("Grenade") || root.GetProperty("Events").GetArrayLength() != 0))
            throw new Exception($"{clip} must have empty hands and no gameplay events");

        // The facing is baked into the clip, so it has to be there and it has to be the one this
        // clip is named for. A north clip that says east is a pawn throwing over its own shoulder.
        var body = root.GetProperty("Parts").EnumerateArray()
            .First(p => p.GetProperty("CustomName").ValueKind != System.Text.Json.JsonValueKind.Null
                     && p.GetProperty("CustomName").GetString() == "BodyA");
        if (!body.GetProperty("DefaultValues").TryGetProperty("PawnBody.Direction", out var facing))
            throw new Exception($"Melee Animation bridge: {clip} does not set PawnBody.Direction, "
                + "so the pawn would face whatever the previous animation left it facing");
        // Shinra Tensei's single clip faces south: that is the one facing which shows both arms
        // at full extension rather than hiding one behind the torso.
        int expected = shinra ? 2 : clip.EndsWith("North") ? 0 : clip.EndsWith("South") ? 2 : 1;
        if ((int)facing.GetDouble() != expected)
            throw new Exception($"Melee Animation bridge: {clip} has PawnBody.Direction "
                + $"{facing.GetDouble()}, expected Rot4 {expected}");

        return curveCount;
    }

    /// <summary>The workshop copy, whichever folder Steam gave it. Null when it is not there.</summary>
    static string FindMeleeAnimation()
    {
        const string Workshop = "/mnt/c/Program Files (x86)/Steam/steamapps/workshop/content/294100";
        if (!Directory.Exists(Workshop)) return null;

        return Directory.EnumerateDirectories(Workshop)
            .Select(folder => Path.Combine(folder, "1.6", "Assemblies", "zAnimationMod.dll"))
            .FirstOrDefault(File.Exists);
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
            { "equipmentDef", "Verse.ThingDef" },
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

        if (projectile.GetMethod("ExposeData", Any) == null || projectile.GetMethod("MoveForward", Any) == null)
            throw new Exception("CE repulsion persistence requires ProjectileCE.ExposeData and MoveForward");

        int patchTypes = CheckCombatExtendedPatchTypes(projectile.Assembly);

        return $"Checked {expected.Count + 4} members of the Combat Extended bridge contract "
             + $"and {patchTypes} types named by the CE patch folder.";
    }

    /// <summary>
    /// The CE types that Patch_CombatExtended names in XML rather than in code.
    ///
    /// A Class attribute that does not resolve is a red error at load for the player who has both
    /// mods and silence for everybody else, which is exactly the failure this file exists to move
    /// forward in time. The frost bomb patch is small on purpose - it gives the weapon CE's stats
    /// and CE's melee tool and keeps this mod's own verb, because CE's verb casts what it spawns
    /// to ProjectileCE and taking it would cost the throw animation - so there is exactly one
    /// type to check here today. Add to the list when the patch names another.
    /// </summary>
    static int CheckCombatExtendedPatchTypes(Assembly ce)
    {
        string[] named = { "CombatExtended.ToolCE" };
        foreach (string name in named)
        {
            if (ce.GetType(name) == null)
                throw new Exception($"CE patch: {name} is gone, named by "
                    + "Patch_CombatExtended/1.6/Patches/AG_Frost_CE.xml");
        }
        return named.Length;
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

    /// <summary>
    /// The mimic beacon's two contracts with the engine, neither of which is a Harmony patch and
    /// both of which fail silently.
    ///
    /// The decoy is a Thing that raiders shoot because it implements IAttackTarget, and it is
    /// drawn by running the source pawn's renderer at a second position. Neither is patched, so
    /// neither is covered by the loop above; and neither throws when it breaks. A renamed member
    /// on IAttackTarget is a compile error, but a *removed* one is not - the decoy would simply
    /// stop being registered in the attack target cache and quietly become scenery. A changed
    /// PawnRenderer signature is the same story: the decoy stands there invisible.
    ///
    /// The taunt strength is derived from AttackTargetFinder.GetShootingTargetScore, which is
    /// private and cannot be re-derived here. Its existence is checked, and Tests/Mimic holds a
    /// transcription of the arithmetic.
    /// </summary>
    static string CheckMimicContract()
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static;

        Type attackTarget = typeof(Verse.Thing).Assembly.GetType("Verse.AI.IAttackTarget")
            ?? throw new Exception("Mimic: Verse.AI.IAttackTarget is gone");

        foreach (string member in new[] { "Thing", "TargetCurrentlyAimingAt", "TargetPriorityFactor" })
        {
            if (attackTarget.GetProperty(member, Any) == null)
                throw new Exception($"Mimic: IAttackTarget.{member} is gone");
        }
        if (attackTarget.GetMethod("ThreatDisabled", Any) == null)
            throw new Exception("Mimic: IAttackTarget.ThreatDisabled is gone");

        if (!attackTarget.IsAssignableFrom(typeof(MimicDecoy)))
            throw new Exception("Mimic: MimicDecoy no longer implements IAttackTarget");

        // Registration is automatic and entirely implicit - this is the call that puts a spawned
        // decoy in front of enemy AI, and nothing in this mod invokes it.
        Type cache = typeof(Verse.Thing).Assembly.GetType("Verse.AI.AttackTargetsCache")
            ?? throw new Exception("Mimic: Verse.AI.AttackTargetsCache is gone");
        if (cache.GetMethod("Notify_ThingSpawned", Any) == null)
            throw new Exception("Mimic: AttackTargetsCache.Notify_ThingSpawned is gone - "
                + "a spawned decoy would never be registered as a target");

        Type finder = typeof(Verse.Thing).Assembly.GetType("Verse.AI.AttackTargetFinder")
            ?? throw new Exception("Mimic: Verse.AI.AttackTargetFinder is gone");
        if (finder.GetMethod("GetShootingTargetScore", Any) == null)
            throw new Exception("Mimic: AttackTargetFinder.GetShootingTargetScore is gone - "
                + "MimicDefaults.PriorityFactor is derived from its arithmetic, see Tests/Mimic");
        // Raiders scan with NeedAutoTargetable, and this is the predicate that flag runs. It
        // rejects dormant and uninitiated things; the decoy carries neither comp, which is the
        // reason it passes.
        if (finder.GetMethod("IsAutoTargetable", Any) == null)
            throw new Exception("Mimic: AttackTargetFinder.IsAutoTargetable is gone");

        // The decoy has no appearance of its own; it is the source pawn's renderer run again at
        // another position, with the rotation pinned. The Rot4? parameter is what pins it.
        MethodInfo drawPhase = typeof(Verse.PawnRenderer).GetMethod("DynamicDrawPhaseAt", Any, null,
            new[] { typeof(Verse.DrawPhase), typeof(UnityEngine.Vector3), typeof(Verse.Rot4?), typeof(bool) },
            null);
        if (drawPhase == null)
            throw new Exception("Mimic: PawnRenderer.DynamicDrawPhaseAt(DrawPhase, Vector3, Rot4?, bool) "
                + "is gone - the decoy would draw nothing at all");

        foreach (string phase in new[] { "EnsureInitialized", "ParallelPreDraw", "Draw" })
        {
            if (!Enum.IsDefined(typeof(Verse.DrawPhase), phase))
                throw new Exception($"Mimic: DrawPhase.{phase} is gone");
        }

        return "Checked the mimic beacon's IAttackTarget and PawnRenderer contracts.";
    }

    /// <summary>
    /// The retrieval hook belt's contracts with the health system, and its def numbers.
    ///
    /// The wound penalty reaches three things that are not Harmony patches and so are not covered
    /// by the loop above: the private severityInt field (written directly so probing a lethal
    /// amount does not notify the health tracker), the two public tend fields it clears, and the
    /// health checks it asks before committing. A rename of any of them compiles against an older
    /// assembly and then throws or silently does nothing in game.
    /// </summary>
    static string CheckRetrievalHookContract()
    {
        const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        FieldInfo severity = typeof(Verse.Hediff).GetField("severityInt", Instance);
        if (severity == null || severity.FieldType != typeof(float))
            throw new Exception("Retrieval hook: Hediff.severityInt (float) is gone");

        Type tend = typeof(Verse.HediffComp_TendDuration);
        FieldInfo ticks = tend.GetField("tendTicksLeft", BindingFlags.Public | BindingFlags.Instance);
        FieldInfo quality = tend.GetField("tendQuality", BindingFlags.Public | BindingFlags.Instance);
        if (ticks == null || ticks.FieldType != typeof(int) || quality == null || quality.FieldType != typeof(float))
            throw new Exception("Retrieval hook: HediffComp_TendDuration.tendTicksLeft (int) / tendQuality (float) changed");
        if (tend.GetProperty("IsTended")?.PropertyType != typeof(bool))
            throw new Exception("Retrieval hook: HediffComp_TendDuration.IsTended is gone");

        Type health = typeof(Verse.Pawn_HealthTracker);
        if (health.GetMethod("ShouldBeDead", Type.EmptyTypes)?.ReturnType != typeof(bool))
            throw new Exception("Retrieval hook: Pawn_HealthTracker.ShouldBeDead() is gone");
        if (health.GetMethod("Notify_HediffChanged", new[] { typeof(Verse.Hediff) }) == null)
            throw new Exception("Retrieval hook: Pawn_HealthTracker.Notify_HediffChanged(Hediff) is gone");
        if (health.GetProperty("CanBleed")?.PropertyType != typeof(bool))
            throw new Exception("Retrieval hook: Pawn_HealthTracker.CanBleed is gone");

        Type set = typeof(Verse.HediffSet);
        if (set.GetMethod("GetPartHealth", new[] { typeof(Verse.BodyPartRecord) })?.ReturnType != typeof(float))
            throw new Exception("Retrieval hook: HediffSet.GetPartHealth(BodyPartRecord) changed");
        if (set.GetMethod("DirtyCache", Type.EmptyTypes) == null)
            throw new Exception("Retrieval hook: HediffSet.DirtyCache() is gone");

        if (typeof(Verse.AI.Toil).GetField("tickIntervalAction")?.FieldType != typeof(Action<int>))
            throw new Exception("Retrieval hook: Toil.tickIntervalAction (Action<int>) changed - reel-in would do no work");
        if (typeof(Verse.ThingComp).GetMethod("CompGetWornGizmosExtra") == null)
            throw new Exception("Retrieval hook: ThingComp.CompGetWornGizmosExtra is gone - no Reel in tether button");

        var belt = XDocument.Load("1.6/Defs/ThingDefs/AG_RetrievalHook_Things.xml").Root.Element("ThingDef");
        var stats = belt.Element("statBases");
        var cost = belt.Element("costList");
        var recipe = belt.Element("recipeMaker");
        if ((float)stats.Element("Mass") != 2f || (float)stats.Element("WorkToMake") != 12000f
            || (float)stats.Element("EquipDelay") != 2f || stats.Elements().Any(e => e.Name.LocalName.StartsWith("Armor"))
            || (int)cost.Element("Steel") != 60 || (int)cost.Element("ComponentIndustrial") != 2 || (int)cost.Element("Cloth") != 20
            || cost.Elements().Count() != 3
            || (string)recipe.Element("researchPrerequisite") != "Machining"
            || (string)recipe.Element("recipeUsers").Element("li") != "TableMachining"
            || (int)recipe.Element("skillRequirements").Element("Crafting") != 5
            || (string)belt.Element("apparel").Element("layers").Element("li") != "Belt"
            || belt.Element("apparel").Element("tags") != null)
            throw new Exception("Retrieval hook belt: def no longer matches 2 kg, 12000 work, 2 s equip, 60 steel / 2 components / 20 cloth, Machining, Crafting 5, belt layer, no armor, no generation tags");

        var ability = XDocument.Load("1.6/Defs/AbilityDefs/AG_RetrievalHook_Abilities.xml").Root.Element("AbilityDef");
        var verb = ability.Element("verbProperties");
        if ((float)verb.Element("range") != RetrievalHookDefaults.Range || (bool)ability.Element("aiCanUse")
            || (bool)ability.Element("casterMustBeCapableOfViolence") || !(bool)ability.Element("displayGizmoWhileUndrafted")
            || ability.Element("cooldownTicksRange") != null)
            throw new Exception("Retrieval hook ability: expected range 15, no AI use, usable by non-violent and undrafted pawns, no cooldown");
        if (RetrievalHookDefaults.ReloadTicks != 600 || RetrievalHookDefaults.ItemCapacityKg != 20f)
            throw new Exception("Retrieval hook: reload must be 600 ticks and item capacity 20 kg");

        return "Checked the retrieval hook's health and tend contracts and def numbers.";
    }
    /// <summary>
    /// The kunai belt's contracts. The throw re-implements Verb_LaunchProjectile's hit roll from
    /// public ShotReport and ShootLine members, and spends charges on vanilla
    /// CompApparelReloadable; neither is a Harmony patch, so the loop above does not cover them.
    /// </summary>
    static string CheckKunaiContract()
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        Type report = typeof(Verse.ShotReport);
        if (report.GetMethod("HitReportFor", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Verse.Thing), typeof(Verse.Verb), typeof(Verse.LocalTargetInfo) }, null) == null)
            throw new Exception("Kunai: ShotReport.HitReportFor(Thing, Verb, LocalTargetInfo) changed");
        foreach (string name in new[] { "AimOnTargetChance_IgnoringPosture", "AimOnTargetChance_StandardTarget", "PassCoverChance", "TotalEstimatedHitChance" })
            if (report.GetProperty(name, Public)?.PropertyType != typeof(float))
                throw new Exception("Kunai: ShotReport." + name + " (float) is gone");
        if (report.GetMethod("GetRandomCoverToMissInto", Public)?.ReturnType != typeof(Verse.Thing))
            throw new Exception("Kunai: ShotReport.GetRandomCoverToMissInto() is gone");
        if (typeof(Verse.ShootLine).GetMethod("ChangeDestToMissWild", new[] { typeof(float), typeof(bool), typeof(Verse.Map) }) == null)
            throw new Exception("Kunai: ShootLine.ChangeDestToMissWild(float, bool, Map) changed");
        // KunaiAccuracy swaps Shooting for Melee by overwriting this private field and rebuilding
        // ShootingAccuracyPawn's value from its def. A rename would throw on the first throw.
        if (report.GetField("factorFromShooterAndDist", BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType != typeof(float))
            throw new Exception("Kunai: ShotReport.factorFromShooterAndDist (float) is gone - Melee accuracy cannot replace Shooting");
        Type statDef = typeof(RimWorld.StatDef);
        if (statDef.GetField("noSkillOffset", Public)?.FieldType != typeof(float)
            || statDef.GetField("capacityOffsets", Public) == null
            || statDef.GetField("postProcessCurve", Public)?.FieldType != typeof(Verse.SimpleCurve)
            || statDef.GetField("postProcessStatFactors", Public) == null
            || typeof(RimWorld.PawnCapacityOffset).GetMethod("GetOffset", new[] { typeof(float) })?.ReturnType != typeof(float))
            throw new Exception("Kunai: StatDef noSkillOffset / capacityOffsets / postProcessCurve / postProcessStatFactors or PawnCapacityOffset.GetOffset changed");
        if (typeof(RimWorld.Pawn_SkillTracker).GetMethod("Learn", new[] { typeof(RimWorld.SkillDef), typeof(float), typeof(bool), typeof(bool) }) == null)
            throw new Exception("Kunai: Pawn_SkillTracker.Learn(SkillDef, float, bool, bool) changed - no Melee XP");

        foreach (string name in new[] { "accuracyTouch", "accuracyShort", "accuracyMedium", "accuracyLong" })
            if (typeof(Verse.VerbProperties).GetField(name, Public)?.FieldType != typeof(float))
                throw new Exception("Kunai: VerbProperties." + name + " is gone - hit chance would ignore the def");

        Type reloadable = typeof(RimWorld.CompApparelReloadable);
        if (reloadable.GetProperty("RemainingCharges", Public)?.PropertyType != typeof(int)
            || reloadable.GetProperty("LabelRemaining", Public)?.PropertyType != typeof(string)
            || reloadable.GetMethod("UsedOnce", Type.EmptyTypes) == null)
            throw new Exception("Kunai: CompApparelReloadable.RemainingCharges / LabelRemaining / UsedOnce changed");
        if (typeof(RimWorld.Command_Ability).GetProperty("Ability", Public) == null)
            throw new Exception("Kunai: Command_Ability.Ability is gone - no kunai count on the gizmo");

        var things = XDocument.Load("1.6/Defs/ThingDefs/AG_Kunai_Things.xml").Root.Elements("ThingDef").ToArray();
        var belt = things.Single(e => (string)e.Element("defName") == "AG_KunaiBelt");
        var reload = belt.Element("comps").Elements("li").Single(e => (string)e.Attribute("Class") == "CompProperties_ApparelReloadable");
        if ((int)reload.Element("maxCharges") != 6 || (string)reload.Element("ammoDef") != "AG_Kunai"
            || (int)reload.Element("ammoCountPerCharge") != 1 || reload.Element("ammoCountToRefill") != null
            || (string)belt.Element("apparel").Element("layers").Element("li") != "Belt"
            || belt.Element("apparel").Element("tags") != null)
            throw new Exception("Kunai belt: expected 6 charges of AG_Kunai, 1 per charge, belt layer, no generation tags");
        var projectile = things.Single(e => (string)e.Element("defName") == "AG_KunaiProjectile").Element("projectile");
        if ((int)projectile.Element("damageAmountBase") != 12 || (float)projectile.Element("armorPenetrationBase") != 0.18f)
            throw new Exception("Kunai projectile: expected 12 damage and 0.18 armor penetration");

        var ability = XDocument.Load("1.6/Defs/AbilityDefs/AG_Kunai_Abilities.xml").Root.Element("AbilityDef");
        if ((float)ability.Element("verbProperties").Element("range") != 14.9f || (bool)ability.Element("aiCanUse")
            || (int)ability.Element("cooldownTicksRange") != 90
            || (float)ability.Element("verbProperties").Element("warmupTime") != 0.3f)
            throw new Exception("Throw kunai: expected range 14.9, no AI use, 0.3 s warmup, 90-tick cooldown");
        if (KunaiDefaults.BreakChanceOnHit != 0.2f)
            throw new Exception("Kunai: break chance on hit must be 0.2");

        // Stuck kunai. The hediff's hooks and the float menu provider are overrides, which the build
        // already checks; these are the pieces reached by name or by data.
        if (KunaiDefaults.MaxEmbeddedPerPawn != 3 || KunaiDefaults.EmbeddedBleedFactor != 0.5f
            || KunaiDefaults.PullCutSeverity != 6f || KunaiDefaults.PullTicksFighting != 30 || KunaiDefaults.PullTicksCalm != 120)
            throw new Exception("Stuck kunai: expected 3 per pawn, 0.5 bleed while stuck, severity 6 pull cut, 30/120 pull ticks");
        if (typeof(Verse.PawnRenderNode).GetField("hediff", Public)?.FieldType != typeof(Verse.Hediff)
            || typeof(Verse.PawnRenderNode).GetField("bodyPart", Public)?.FieldType != typeof(Verse.BodyPartRecord)
            || typeof(Verse.DrawData).GetField("useBodyPartAnchor", Public)?.FieldType != typeof(bool))
            throw new Exception("Stuck kunai: PawnRenderNode.hediff / bodyPart or DrawData.useBodyPartAnchor changed - kunai not drawn at the wound");
        if (typeof(Verse.DynamicPawnRenderNodeSetup_Hediffs) == null || typeof(RimWorld.Recipe_RemoveHediff) == null
            || typeof(RimWorld.FloatMenuMakerMap) == null)
            throw new Exception("Stuck kunai: hediff render nodes, Recipe_RemoveHediff or FloatMenuMakerMap is gone");
        // Without this override two kunai in the same part merge into one hediff and the second kunai
        // item is deleted.
        if (typeof(Hediff_EmbeddedKunai).GetMethod("TryMergeWith", new[] { typeof(Verse.Hediff) })?.DeclaringType != typeof(Hediff_EmbeddedKunai))
            throw new Exception("Stuck kunai: Hediff_EmbeddedKunai must override TryMergeWith to never merge");
        var hediff = XDocument.Load("1.6/Defs/HediffDefs/AG_Kunai_Hediffs.xml").Root.Element("HediffDef");
        var nodes = hediff.Element("renderNodeProperties").Elements("li").ToArray();
        if ((string)hediff.Element("hediffClass") != "RimArt.Hediff_EmbeddedKunai"
            || (float)hediff.Element("stages").Element("li").Element("painOffset") != 0.05f
            || (bool)hediff.Element("tendable") || !(bool)hediff.Element("forceRenderTreeRecache")
            || nodes.Length != 2
            || !nodes.Select(n => (string)n.Element("parentTagDef")).OrderBy(s => s).SequenceEqual(new[] { "Body", "Head" })
            || nodes.Any(n => !(bool)n.Element("drawData").Element("useBodyPartAnchor")))
            throw new Exception("Stuck kunai hediff: expected Hediff_EmbeddedKunai, 5% pain, not tendable, render recache, body and head anchor nodes");
        foreach (string facing in new[] { "north", "east", "south" })
            if (!File.Exists($"Textures/RimArt/Kunai/Embedded_{facing}.png"))
                throw new Exception($"Stuck kunai: Textures/RimArt/Kunai/Embedded_{facing}.png missing - Graphic_Multi needs every facing");
        var surgery = XDocument.Load("1.6/Defs/RecipeDefs/AG_Kunai_Recipes.xml").Root.Elements("RecipeDef")
            .Single(e => (string)e.Element("defName") == "AG_RemoveEmbeddedKunai");
        if ((string)surgery.Element("workerClass") != "Recipe_RemoveHediff" || (string)surgery.Element("removesHediff") != "AG_EmbeddedKunai"
            || !(bool)surgery.Element("targetsBodyPart") || (int)surgery.Element("skillRequirements").Element("Medicine") != 3)
            throw new Exception("Stuck kunai surgery: expected Recipe_RemoveHediff on AG_EmbeddedKunai per body part, Medicine 3");

        return "Checked the kunai hit roll, reloadable belt, stuck kunai and def numbers.";
    }

    /// <summary>
    /// The makibishi contracts. The spikes reach the path finder through a vanilla interface and
    /// ThingRequestGroup rather than a Harmony patch, and the slow reads HediffComp_Disappears
    /// fields, so the patch loop above covers none of it.
    /// </summary>
    static string CheckMakibishiContract()
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        // Path cost: PathFinder lists ThingRequestGroup.CostProvider, which is every thingClass
        // implementing IPathFindCostProvider, and PathGridDoorsBlockedJob asks each for its cells
        // and its cost for the pawn.
        Type provider = typeof(Verse.IPathFindCostProvider);
        if (provider.GetMethod("PathFindCostFor", new[] { typeof(Verse.Pawn) })?.ReturnType != typeof(ushort)
            || provider.GetMethod("GetOccupiedRect", Type.EmptyTypes)?.ReturnType != typeof(Verse.CellRect))
            throw new Exception("Makibishi: IPathFindCostProvider.PathFindCostFor(Pawn) / GetOccupiedRect() changed - spikes would not cost path");
        if (!Enum.IsDefined(typeof(Verse.ThingRequestGroup), "CostProvider"))
            throw new Exception("Makibishi: ThingRequestGroup.CostProvider is gone - the path finder no longer lists cost providers");
        if (!provider.IsAssignableFrom(typeof(Makibishi)) || !typeof(Verse.Building).IsAssignableFrom(typeof(Makibishi)))
            throw new Exception("Makibishi: the spikes must be a Building implementing IPathFindCostProvider");
        if (typeof(Verse.Pawn).GetProperty("Flying", Public)?.PropertyType != typeof(bool))
            throw new Exception("Makibishi: Pawn.Flying is gone - flying pawns would be hurt");
        if (typeof(Verse.HediffComp_Disappears).GetField("ticksToDisappear", Public)?.FieldType != typeof(int)
            || typeof(Verse.HediffComp_Disappears).GetField("disappearsAfterTicks", Public)?.FieldType != typeof(int))
            throw new Exception("Makibishi: HediffComp_Disappears.ticksToDisappear / disappearsAfterTicks changed - the slow cannot fade");
        if (typeof(RimWorld.BodyPartTagDefOf).GetField("MovingLimbSegment") == null
            || typeof(RimWorld.BodyPartTagDefOf).GetField("MovingLimbCore") == null)
            throw new Exception("Makibishi: BodyPartTagDefOf.MovingLimbSegment / MovingLimbCore is gone - no paw or leg to wound");

        if (MakibishiDefaults.LifetimeTicks != 1800 || MakibishiDefaults.PathFindCost != 400
            || MakibishiDefaults.TriggerChance != 0.35f || MakibishiDefaults.WoundDamage != 4f
            || MakibishiDefaults.WoundArmorPenetration != 0.10f || MakibishiDefaults.CornerChance != 0.5f)
            throw new Exception("Makibishi: expected 30 s, path cost 400, 35% per step, 4 damage, 10% AP, 50% corners");

        var things = XDocument.Load("1.6/Defs/ThingDefs/AG_Makibishi_Things.xml").Root.Elements("ThingDef").ToArray();
        var pouch = things.Single(e => (string)e.Element("defName") == "AG_MakibishiPouch");
        var reload = pouch.Element("comps").Elements("li").Single(e => (string)e.Attribute("Class") == "CompProperties_ApparelReloadable");
        if ((int)reload.Element("maxCharges") != 3 || (string)reload.Element("ammoDef") != "AG_Makibishi"
            || (int)reload.Element("ammoCountPerCharge") != 1
            || (string)pouch.Element("apparel").Element("layers").Element("li") != "Belt"
            || (string)pouch.Element("apparel").Element("bodyPartGroups").Element("li") != "Waist"
            || pouch.Element("apparel").Element("tags") != null)
            throw new Exception("Makibishi pouch: expected 3 charges of AG_Makibishi, 1 per charge, belt layer on the waist, no generation tags");
        var spikes = things.Single(e => (string)e.Element("defName") == "AG_MakibishiSpikes");
        if ((string)spikes.Element("thingClass") != "RimArt.Makibishi" || (bool)spikes.Element("building").Element("isEdifice")
            || (string)spikes.Element("passability") != "Standable" || (float)spikes.Element("fillPercent") != 0f
            || (string)spikes.Element("tickerType") != "Normal")
            throw new Exception("Makibishi spikes: expected RimArt.Makibishi, not an edifice, standable, fillPercent 0, Normal ticker - or it wipes doors or never ticks");
        if (!Directory.Exists("Textures/RimArt/Makibishi/Spikes") || Directory.GetFiles("Textures/RimArt/Makibishi/Spikes", "*.png").Length < 2)
            throw new Exception("Makibishi spikes: Graphic_Random needs a folder of variants at Textures/RimArt/Makibishi/Spikes");

        var ability = XDocument.Load("1.6/Defs/AbilityDefs/AG_Makibishi_Abilities.xml").Root.Element("AbilityDef");
        var verb = ability.Element("verbProperties");
        if ((float)verb.Element("range") != 9.9f || (bool)ability.Element("aiCanUse")
            || (float)verb.Element("warmupTime") != 0.5f || (int)ability.Element("cooldownTicksRange") != 120
            || !(bool)verb.Element("targetParams").Element("canTargetLocations"))
            throw new Exception("Scatter makibishi: expected range 9.9, no AI use, 0.5 s warmup, 120-tick cooldown, cell targets");

        var hediff = XDocument.Load("1.6/Defs/HediffDefs/AG_Makibishi_Hediffs.xml").Root.Element("HediffDef");
        var disappears = hediff.Element("comps").Elements("li").Single(e => (string)e.Attribute("Class") == "HediffCompProperties_Disappears");
        var offsets = hediff.Element("stages").Elements("li")
            .Select(li => (float)li.Element("capMods").Element("li").Element("offset")).ToArray();
        if ((string)disappears.Element("disappearsAfterTicks") != "900"
            || !offsets.SequenceEqual(new[] { -0.10f, -0.20f, -0.30f }))
            throw new Exception("Punctured foot: expected 900 ticks and Moving -10% / -20% / -30% stages");

        return "Checked the makibishi path cost provider, pouch, spikes and def numbers.";
    }
    static string CheckFumaContract()
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        if (!typeof(Verse.IThingHolder).IsAssignableFrom(typeof(Projectile_Fuma))
            || !typeof(Verse.Projectile).IsAssignableFrom(typeof(Projectile_Fuma))
            || !typeof(Verse.CompEquippable).IsAssignableFrom(typeof(CompFuma)))
            throw new Exception("Fuma must retain the physical weapon while in flight and standard melee equipment");
        if (typeof(Verse.Pawn_EquipmentTracker).GetMethod("TryTransferEquipmentToContainer",
                new[] { typeof(Verse.ThingWithComps), typeof(Verse.ThingOwner) })?.ReturnType != typeof(bool))
            throw new Exception("Fuma equipment transfer API changed");
        if (typeof(RimWorld.CompProjectileInterceptor).GetMethod("CheckIntercept", Any, null,
                new[] { typeof(Verse.Projectile), typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector3) }, null) == null)
            throw new Exception("Fuma shield interception API changed");
        foreach (string field in new[] { "origin", "destination", "ticksToImpact", "lifetime", "equipment", "launcher" })
            if (typeof(Verse.Projectile).GetField(field, Any) == null)
                throw new Exception("Fuma projectile field missing: " + field);
        var root = XDocument.Load("1.6/Defs/ThingDefs/AG_Fuma_Things.xml").Root;
        var weapon = root.Elements("ThingDef").Single(e => (string)e.Element("defName") == "AG_FumaShuriken");
        var projectile = root.Elements("ThingDef").Single(e => (string)e.Element("defName") == "AG_FumaProjectile");
        if ((string)weapon.Element("comps").Attribute("Inherit") != "False"
            || weapon.Element("comps").Elements("li").Count(e => (string)e.Attribute("Class") == "RimArt.CompProperties_Fuma") != 1
            || weapon.Element("weaponTags") != null || weapon.Element("verbs") != null)
            throw new Exception("Fuma must have exactly one equippable comp, normal melee, and no AI generation tags");
        if (!(bool)root.Element("DamageDef").Element("isRanged")
            || (string)projectile.Element("thingClass") != "RimArt.Projectile_Fuma")
            throw new Exception("Fuma needs ranged shield-aware damage and its explicit projectile class");
        foreach (string name in new[] { "Folded", "Unfolded", "Ring", "Blade", "IconFuma" })
            if (!File.Exists($"Textures/RimArt/Fuma/{name}.png")) throw new Exception("Missing Fuma texture: " + name);
        foreach (string clip in ThrowAnimation.Fuma.All)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText("Animations/" + clip.Replace("AG_", "RimArt_") + ".json"));
            var parts = doc.RootElement.GetProperty("Parts").EnumerateArray().Where(p =>
                p.GetProperty("CustomName").ValueKind == System.Text.Json.JsonValueKind.String
                && (p.GetProperty("CustomName").GetString().StartsWith("FumaBlade")
                    || p.GetProperty("CustomName").GetString() == "Grenade")).ToArray();
            if (parts.Length != 5) throw new Exception("Fuma needs four animated blades and a ring");
            foreach (var part in parts)
            {
                float release = part.GetProperty("Curves").GetProperty("GameObject.m_IsActive").GetProperty("Keyframes")
                    .EnumerateArray().First(k => k.GetProperty("value").GetSingle() == 0).GetProperty("time").GetSingle();
                if (Math.Abs(release * 60 - FumaRules.WarmupTicks) > .001)
                    throw new Exception("Fuma visual release must match the job, including all four blades");
            }
        }
        return "Checked Fuma equipment ownership interfaces, projectile interception, melee command, and folding/release assets.";
    }

}
