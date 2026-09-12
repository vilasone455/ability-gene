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
        string distortion = CheckShinraDistortion();
        Console.WriteLine($"Passed {count} Harmony target/signature checks against installed RimWorld, "
            + $"plus trait and job definition checks. {combatExtended} {meleeAnimation} {mimic} {distortion}");
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
        foreach (string clip in new[] { "RimArt_ThrowGrenade", "RimArt_ThrowGrenadeNorth", "RimArt_ThrowGrenadeSouth",
                                        "RimArt_ShinraPush" })
        {
            curves += CheckThrowAnimationJson(dataModel, partModel, clip);
            clips++;
        }
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
}
