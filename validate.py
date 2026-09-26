#!/usr/bin/env python3
"""
Static checks for the mod. Catches the things RimWorld only complains about at
runtime, in the order they have actually bitten this project:

  1. XML well-formedness
  2. Duplicate Name= declarations -- RimWorld's Name attribute is ONE namespace
     shared by every def type in a mod, so an abstract HediffDef and an abstract
     AbilityDef cannot share a name. The second is silently dropped and its
     children inherit the wrong base.
  3. Unresolved ParentName, and ranges written in vector syntax ("(60, 75)" instead
     of "60~75"), which drops the whole def at load with no useful error
  4. Def and texture references that resolve neither in this mod's own Textures/
     folder nor in Core or Biotech (anything Royalty-only would break for players
     without that DLC)
  5. Custom Class= values that do not exist in the built assembly
  6. Comp classes a def ends up with twice once inheritance is applied
  6b. Three ThingDef.ConfigErrors rules the game enforces at load: an explosive projectile
     verb must declare a forcedMissRadius (and a non-explosive one must not), and a def
     carrying CompProperties_Explosive must tick Normal, and a smeltable def must have a cost
  6c. SoundDef.ConfigErrors: a sustainer must not use priorityMode PrioritizeNewest,
     which is the default when priorityMode is left out
  6d. TerrainDef tags that are not fields of the 1.6 TerrainDef (holdSnow vs holdSnowOrSand)
  7. Translate keys used in C# but not defined in Languages/
  8. Concrete abilities with zero or multiple acquisition sources (retired ones may have zero)

Usage: python3 validate.py [path/to/RimWorld/Data]
"""
import os, re, sys, glob, subprocess
import xml.etree.ElementTree as ET
import rimworld_paths

DATA = sys.argv[1] if len(sys.argv) > 1 else rimworld_paths.DATA
DLCS = ("Core", "Royalty", "Biotech")
REF_TAGS = {
    "hediffDef", "stateDef", "fleckDef", "jobDef", "capacity", "soundCast",
    "warmupStartSound", "clamorType", "category", "prerequisite", "damageDef",
    "thingDef", "effecterDef", "projectileDef", "strainHediff", "injuryDef",
    # Item, recipe and research plumbing. Added when the implant devices landed: an
    # install recipe that names a researchPrerequisite or an unfinishedThingDef that
    # does not exist drops the recipe at load, which looks exactly like the surgery
    # simply not being offered.
    "researchPrerequisite", "requiredResearchBuilding", "unfinishedThingDef", "addsHediff",
    # Reloadable apparel ammunition (the kunai belt). A wrong name leaves the belt unreloadable.
    "ammoDef", "soundReload", "soundInteract", "soundDrop",
    # Echo defs: trials, costs, cast costs, the manifest hediff and the device tiers.
    "manifestHediff", "skill", "record", "trait", "ability", "research", "bodyType",
}
LIST_TAGS = {"abilities", "descriptionHyperlinks", "exceptions",
             "recipeUsers", "thingDefs", "prerequisites", "thingCategories",
             "categories", "appliedOnFixedBodyParts", "weapons", "researchPrerequisites"}

problems = []
def fail(kind, where, detail):
    problems.append((kind, where, detail))

# Patch_* folders are defs that only load alongside some other mod (see loadFolders.xml).
# They are checked the same as any other def: the game will not read them without that mod
# installed, which means a typo in one is invisible until the one player who has both finds it.
my_files = sorted(glob.glob("1.6/Defs/**/*.xml", recursive=True)
                  + glob.glob("Patch_*/**/Defs/**/*.xml", recursive=True))

# PatchOperation files, which are not defs and so are checked for well-formedness only. Worth
# listing separately rather than skipping: a Patch_* folder is read by the game only when some
# other mod is present, so a broken one is invisible here and loud for the player who has both.
patch_files = sorted(f for f in glob.glob("Patch_*/**/Patches/**/*.xml", recursive=True)
                     if f not in my_files)

# 1. well-formedness
for f in (my_files + patch_files + sorted(glob.glob("Languages/**/*.xml", recursive=True))
          + ["About/About.xml"]):
    try:
        ET.parse(f)
    except Exception as e:
        fail("malformed XML", f, str(e))

# index vanilla
vanilla = {}
for dlc in DLCS:
    root = os.path.join(DATA, dlc)
    if not os.path.isdir(root):
        print("warning: no vanilla data at " + root + " -- skipping reference checks")
        DATA = None
        break
    for dp, _, fn in os.walk(root):
        for f in fn:
            if not f.endswith(".xml"): continue
            s = open(os.path.join(dp, f), encoding="utf-8", errors="ignore").read()
            for m in re.finditer(r"<defName>([^<]+)</defName>", s):
                vanilla.setdefault(m.group(1).strip(), set()).add(dlc)
            for m in re.finditer(r'Name\s*=\s*"([^"]+)"', s):
                vanilla.setdefault(m.group(1).strip(), set()).add(dlc)
            for m in re.finditer(r"<(?:iconPath|texPath)>([^<]+)<", s):
                vanilla.setdefault("TEX:" + m.group(1).strip(), set()).add(dlc)

# 2. duplicate Name declarations, and collect mine
declared, mine = {}, set()
for f in my_files:
    s = open(f).read()
    for m in re.finditer(r"<defName>([^<]+)</defName>", s):
        mine.add(m.group(1).strip())
    # a declaration is Name=, never ParentName=
    for m in re.finditer(r'(?<!Parent)Name\s*=\s*"([^"]+)"', s):
        name = m.group(1).strip()
        if name in declared:
            fail("duplicate Name", f, name + " already declared in " + declared[name]
                 + " -- Name is one namespace across ALL def types in a mod")
        declared[name] = f
        mine.add(name)

# 3. ParentName resolution
for f in my_files:
    for m in re.finditer(r'ParentName\s*=\s*"([^"]+)"', open(f).read()):
        v = m.group(1).strip()
        if v not in mine and (DATA is None or v not in vanilla):
            fail("unresolved ParentName", f, v)

# 3b. range syntax
# RimWorld parses IntRange and FloatRange from "a~b" (or a bare number) and Vector2/Vector3
# from "(x, y)". Writing a range in vector syntax throws a FormatException at load, the def
# is dropped whole, and the only symptom is a later "Failed to find ThingDef named ..." -
# which is how AG_PanoplyBladeFalling went missing with the rest of the file loading fine.
for f in my_files:
    for el in ET.parse(f).getroot().iter():
        if not el.tag.endswith("Range") or not el.text: continue
        text = el.text.strip()
        if text.startswith("("):
            fail("range in vector syntax", f,
                 "<" + el.tag + ">" + text + " -- ranges are written a~b, not (a, b)")

# 4. references
if DATA is not None:
    stats = set()
    for f in my_files:
        root = ET.parse(f).getroot()
        # <category> is an AbilityCategoryDef reference on an AbilityDef and a plain
        # ThingCategory enum on a ThingDef. Only the first is a def name, so the enum ones
        # are collected here and skipped below - checking them asks the game for a def that
        # was never supposed to exist.
        enum_categories = {el for d in root.findall("ThingDef") for el in d.findall("category")}
        for el in root.iter():
            vals = []
            if el in enum_categories:
                pass
            # A number is a comp's own field that shares a tag name with a def reference, such as
            # the water gun's <capacity>30</capacity> beside a capMod's <capacity>Moving</capacity>.
            elif el.tag in REF_TAGS and el.text and el.text.strip() \
                    and not re.fullmatch(r"-?\d+(\.\d+)?", el.text.strip()):
                vals = [el.text.strip()]
            elif el.tag in LIST_TAGS:
                vals = [c.text.strip() for c in el if c.text]
            elif el.tag in ("statBases", "statOffsets", "statFactors"):
                stats.update(c.tag for c in el)
            # <costList> names its ingredients as child TAGS, not as text or <li>.
            elif el.tag == "costList":
                vals = [c.tag for c in el]
            for v in vals:
                if v in mine: continue
                w = vanilla.get(v)
                if not w: fail("unresolved ref <" + el.tag + ">", f, v)
                elif w == {"Royalty"}: fail("Royalty-only ref <" + el.tag + ">", f, v)
        for m in re.finditer(r"<(?:iconPath|texPath)>([^<]+)<", open(f).read()):
            tex = m.group(1).strip()
            # This mod's own art comes first: Origin: Blade ships two sprites because a
            # top-down weapon texture cannot be turned into a blade standing in the ground.
            # Graphic_Multi textures (render nodes) are split by facing, so the bare path has no
            # file of its own: accept the path when all three facings exist.
            if any(os.path.exists(os.path.join("Textures", tex + ext))
                   for ext in (".png", ".jpg")):
                continue
            if all(os.path.exists(os.path.join("Textures", tex + "_" + facing + ".png"))
                   for facing in ("north", "east", "south")):
                continue
            # Graphic_Random textures (makibishi spikes) are a folder of variants.
            folder = os.path.join("Textures", tex)
            if os.path.isdir(folder) and any(n.endswith(".png") for n in os.listdir(folder)):
                continue
            w = vanilla.get("TEX:" + tex)
            if not w: fail("unresolved texture", f, tex)
            elif w == {"Royalty"}: fail("Royalty-only texture", f, tex)
    for s in sorted(stats):
        w = vanilla.get(s)
        if not w: fail("unknown stat", "statBases/Offsets/Factors", s)
        elif w == {"Royalty"}: fail("Royalty-only stat", "statBases/Offsets/Factors", s)

# 5. custom Class= exists in the assembly
NAMESPACE = "RimArt"
dll = "1.6/Assemblies/" + NAMESPACE + ".dll"
if os.path.exists(dll):
    blob = open(dll, "rb").read().decode("latin-1")
    # Two spellings, because RimWorld names a class two different ways and only one of them
    # is an attribute. Class="RimArt.X" picks the implementation of a <li> or a def; the
    # element forms below name a class in a field. A typo in either produces a def that loads
    # and then throws when the game first needs the type - at spawn, at cast, at damage - so
    # both are checked against the built assembly here.
    ELEMENT_CLASS_TAGS = ("thingClass", "workerClass", "driverClass", "gizmoClass",
                          "verbClass", "compClass", "hediffClass", "abilityClass")
    element_pattern = re.compile(
        r'<(?:' + "|".join(ELEMENT_CLASS_TAGS) + r')>\s*' + NAMESPACE + r'\.([A-Za-z_0-9]+)\s*<')

    for f in my_files:
        text = open(f).read()
        found = [m.group(1) for m in
                 re.finditer(r'Class="' + NAMESPACE + r'\.([A-Za-z_0-9]+)"', text)]
        found += [m.group(1) for m in element_pattern.finditer(text)]
        for name in found:
            if name not in blob:
                fail("class not in assembly", f, NAMESPACE + "." + name)
else:
    print("warning: " + dll + " not built -- skipping class check")

# 6. duplicate comp classes, counting what a def inherits
#    A child's <comps> list is MERGED with its parent's rather than replacing it, so a comp
#    declared on both an abstract base and its child ends up on the hediff twice. RimWorld
#    reports it only at load, as "two comps with same compClass".
#    Inheritance resolves through Name=, which is one namespace across every def type, so
#    the parent lookup is keyed on that -- defName is not unique across types (an AbilityDef
#    and a HediffDef may share one) and keying on it would double-report.
parents, all_defs = {}, []
for f in my_files:
    for el in ET.parse(f).getroot():
        comps = el.find("comps")
        entry = {
            "file": f,
            "label": el.get("Name") or (el.findtext("defName") or "").strip() or el.tag,
            "parent": el.get("ParentName"),
            "inherit": comps is None or comps.get("Inherit", "True").lower() != "false",
            "classes": [c.get("Class") for c in comps if c.get("Class")] if comps is not None else [],
            "abstract": (el.get("Abstract") or "").lower() == "true",
        }
        if el.get("Name"): parents[el.get("Name")] = entry
        all_defs.append(entry)

for entry in all_defs:
    if entry["abstract"]: continue
    chain, node = list(entry["classes"]), entry
    while node["inherit"] and node["parent"] in parents:
        node = parents[node["parent"]]
        chain += node["classes"]
    seen = set()
    for c in chain:
        if c in seen:
            fail("duplicate comp class", entry["file"],
                 entry["label"] + " ends up with two " + c
                 + " -- a child's <comps> merges with its parent's, it does not replace it")
        seen.add(c)

# 6b. Two rules the game enforces in ThingDef.ConfigErrors that nothing above would catch.
#    Both were found the hard way - by a red error at load, after a build, a validator run and
#    an API check pass had all come back clean - and both are the same shape: a def that is
#    well-formed, resolves every name it uses, and is still refused by the game.
#
#    Only defs whose pieces are all visible from here are judged. A projectile inherited from a
#    vanilla abstract, or a thingClass this file cannot see, is skipped rather than guessed at.
EXPLOSIVE_PROJECTILE_CLASSES = ("Projectile_Explosive", "Projectile_DoomsdayRocket")

things_by_name, things_by_defname = {}, {}
for f in my_files:
    for el in ET.parse(f).getroot():
        if el.tag != "ThingDef": continue
        rec = {"el": el, "file": f, "parent": el.get("ParentName"),
               "label": el.get("Name") or (el.findtext("defName") or "").strip()}
        if el.get("Name"): things_by_name[el.get("Name")] = rec
        defname = (el.findtext("defName") or "").strip()
        if defname: things_by_defname[defname] = rec

def _chain(rec):
    """The def and every ancestor of it declared in this mod, nearest first."""
    seen = set()
    while rec is not None:
        yield rec
        parent = rec["parent"]
        if parent is None or parent in seen: return
        seen.add(parent)
        rec = things_by_name.get(parent)

def _inherited(rec, tag):
    for node in _chain(rec):
        value = node["el"].findtext(tag)
        if value is not None and value.strip(): return value.strip()
    return None

def _comp_classes(rec):
    found = []
    for node in _chain(rec):
        comps = node["el"].find("comps")
        if comps is not None:
            found += [c.get("Class") for c in comps if c.get("Class")]
    return found

def _explosive(rec):
    if any(c and "CompProperties_Explosive" in c for c in _comp_classes(rec)): return True
    thing_class = _inherited(rec, "thingClass")
    if thing_class is None: return None
    return any(k in thing_class for k in EXPLOSIVE_PROJECTILE_CLASSES)

for defname, rec in sorted(things_by_defname.items()):
    # "CompExplosive requires Normal ticker type" - a comp that counts down a wick cannot be
    # ticked rarely, and tickerType defaults to Never.
    if any(c and "CompProperties_Explosive" in c for c in _comp_classes(rec)):
        if _inherited(rec, "tickerType") != "Normal":
            fail("config error", rec["file"], defname
                 + " has CompProperties_Explosive but does not declare <tickerType>Normal</tickerType>"
                 + " -- the game refuses any other ticker for it")

    # "explosive projectiles and only explosive projectiles should have forced miss enabled",
    # which is an equality rather than a minimum: a forcedMissRadius on a non-explosive verb is
    # refused just as loudly as its absence on an explosive one.
    verbs = rec["el"].find("verbs")
    if verbs is None: continue
    for index, li in enumerate(verbs):
        projectile = (li.findtext("defaultProjectile") or "").strip()
        if not projectile: continue
        target = things_by_defname.get(projectile)
        if target is None: continue
        explodes = _explosive(target)
        if explodes is None: continue
        try:
            radius = float((li.findtext("forcedMissRadius") or "0").strip())
        except ValueError:
            continue
        if (radius > 0) != explodes:
            fail("config error", rec["file"], defname + " verb " + str(index)
                 + (" launches explosive " + projectile + " but has no forcedMissRadius"
                    if explodes else
                    " has a forcedMissRadius but " + projectile + " is not explosive"))


# "is smeltable but does not give anything for smelting" (ThingDef.ConfigErrors): smeltable
# needs something to smelt into, a costList, costStuffCount or smeltProducts. Found in game for
# AG_WaterGun, which has no cost. Judged through this mod's own parents only; a vanilla parent
# is taken to add no cost.
def _chain(rec):
    seen = []
    while rec is not None and rec not in seen:
        seen.append(rec)
        rec = things_by_name.get(rec["parent"]) if rec["parent"] else None
    return seen

for defname, rec in things_by_defname.items():
    chain = _chain(rec)
    smeltable = next(((r["el"].findtext("smeltable") or "").strip().lower() for r in chain
                      if r["el"].find("smeltable") is not None), "")
    if smeltable != "true": continue
    if any(r["el"].find(tag) is not None for r in chain for tag in ("costList", "costStuffCount", "smeltProducts")):
        continue
    fail("config error", rec["file"], defname + " is smeltable but has no costList, costStuffCount or"
         + " smeltProducts -- the game says it does not give anything for smelting")

# 6c. "PrioritizeNewest is not supported with sustainers." SoundDef.priorityMode defaults to
#    PrioritizeNewest, so a sustainer that says nothing about priority is refused at load. Found
#    in the game log for AG_GravityHum after build, validator and API checks were all clean.
#    Mod SoundDefs do not use ParentName, so only a def's own tags are read; one that inherits
#    is skipped rather than guessed at.
for f in my_files:
    for el in ET.parse(f).getroot():
        if el.tag != "SoundDef" or el.get("ParentName"): continue
        if (el.findtext("sustain") or "").strip().lower() != "true": continue
        mode = (el.findtext("priorityMode") or "PrioritizeNewest").strip()
        if mode == "PrioritizeNewest":
            fail("config error", f, (el.findtext("defName") or "?").strip()
                 + " is a sustainer with priorityMode PrioritizeNewest (the default when it is not set)"
                 + " -- the game refuses that; use PrioritizeNearest")

# 6d. "<holdSnow> doesn't correspond to any field in type TerrainDef." A tag the game cannot
#    match to a field is a red error at load and the value is dropped. Found for AG_CastleVoid
#    and AG_CastleFloor after build, validator and API checks were all clean: the 1.6 field is
#    holdSnowOrSand. The list below is every public instance field of Verse.TerrainDef and its
#    bases in RimWorld 1.6, read from the decompiled Assembly-CSharp on 2026-09-24; regenerate
#    it with ilspycmd when the game updates. Only TerrainDef is checked this way because it is
#    the one def type this mod writes by hand from memory rather than by copying a vanilla def.
TERRAIN_DEF_FIELDS = set("""
affordances altitudeLayer artisticSkillPrerequisite autoRebuildable avoidWander blocksAltitudes
blueprintDef bridge bridgePropsLoopGraphic bridgePropsPath bridgePropsRightGraphic
buildingPrerequisites burnDamage burnIntervalTicks burnedDef canBePolluted canEverTerraform
canFreeze canGenerateDefaultDesignator categoryType changeable clearBuildingArea color colorDef
colorPerStuff constructEffect constructionSkillPrerequisite costList costListForDifficulty
costStuffCount cropIcon customShader customShaderParameters dangerous defName
defaultPlacingRot description descriptionHyperlinks designationCategory designationHotKey
designatorDropdown destroyBuildingsOnDestroyed destroyEffect destroyEffectWater
destroyOnBombDamageThreshold discoveryPrerequisites dominantStyleCategory dontRender
drawStyleCategory driesTo edgeType exposesToVacuum extinguishesFire extraDeteriorationFactor
extraDraftedPerceivedPathCost extraNonDraftedPerceivedPathCost fertility filthAcceptanceMask
fleckData floodTerrain forceMoveItemsBeforeConstruction forcePassableByFlyingPawns frameDef
generated generatedFilth glowColor glowRadius graphic graphicPolluted gravshipReplacementTerrain
heatPerTick holdSnowOrSand ideoBuilding ignitePawnsIntervalTicks igniteRadius ignoreConfigErrors
ignoreIllegalLabelCharacterConfigError installBlueprintDef isAltar isFoundation isPaintable label
layerable maxTechLevelToBuild meltSnowRadius minMonolithLevel minTechLevelToBuild modExtensions
natural passability pathCost pathCostIgnoreRepeat placeWorkers pollutedTexturePath
pollutionCloudColor pollutionColor pollutionOverlayScale pollutionOverlayScrollSpeed
pollutionOverlayTexturePath pollutionShaderType pollutionTintColor preventCraters renderPrecedence
repairEffect requireInspectedGravEngine researchPrerequisites resourcesFractionWhenDeconstructed
scatterType smoothedTerrain spaceBridgePropsLoopGraphic spaceBridgePropsPath
spaceBridgePropsRightGraphic spaceEdgeGraphicData specialDisplayRadius statBases stuffCategories
supportsRock tags takeFootprints takeSplashes tempTerrain temporary terrainAffordanceNeeded
texturePath throwFleckChance tools toxicBuildupFactor traversedThought uiIcon uiIconAngle
uiIconColor uiIconColorTwo uiIconForStackCount uiIconOffset uiIconPath uiIconPathsStuff uiOrder
useStuffTerrainAffordance waterBodyType waterDepthMaterial waterDepthShader
waterDepthShaderParameters
""".split())
for f in my_files:
    for el in ET.parse(f).getroot():
        if el.tag != "TerrainDef": continue
        label = el.get("Name") or (el.findtext("defName") or "").strip()
        for child in el:
            if child.tag not in TERRAIN_DEF_FIELDS:
                fail("unknown field", f, label + " uses <" + child.tag
                     + ">, which is not a field of TerrainDef in RimWorld 1.6"
                     + (" (the 1.6 name is holdSnowOrSand)" if child.tag == "holdSnow" else ""))

# 7. translate keys
used = set()
for f in glob.glob("Source/**/*.cs", recursive=True):
    used.update(re.findall(r'"(AG_[A-Za-z0-9_]+)"\s*\.Translate', open(f).read()))
defined = set()
for f in glob.glob("Languages/**/Keyed/*.xml", recursive=True):
    defined.update(re.findall(r"<(AG_[A-Za-z0-9_]+)>", open(f).read()))
for k in sorted(used - defined):
    fail("missing translate key", "Languages/English/Keyed/", k)

# 8. every concrete ability has exactly one acquisition source
ability_defs, grants = {}, {}
for f in my_files:
    for el in ET.parse(f).getroot():
        def_name = (el.findtext("defName") or "").strip()
        if el.tag == "AbilityDef" and def_name and (el.get("Abstract") or "").lower() != "true":
            ability_defs[def_name] = f

        source = el.tag + ":" + def_name
        refs = []
        if el.tag in ("GeneDef", "HediffDef"):
            refs = [n.text.strip() for n in el.findall("./abilities/li") if n.text]
        elif el.tag == "TraitDef":
            refs = [n.text.strip() for n in el.findall("./modExtensions/li/abilities/li") if n.text]
        elif el.tag == "ThingDef":
            refs = [n.text.strip() for n in el.findall("./comps/li/abilities/li") if n.text]
        elif el.tag == "WeaponTraitDef":
            refs = [n.text.strip() for n in el.findall("./abilityProps/abilityDef") if n.text]
        elif el.tag == "RimArt.EchoDef":
            refs = [n.text.strip() for n in el.findall("./abilities/li") if n.text]
        for ability in refs:
            grants.setdefault(ability, set()).add(source)

# Abilities no source grants any more, kept so a save whose pawns have them still loads. Origin: Blade
# granted Rain, Loose and Grasp until 2026-09-25, when it took Unlimited Blade Works instead.
RETIRED = {"AG_Panoply_Rain", "AG_Panoply_Loose", "AG_Panoply_Grasp"}

# Hero abilities that still have their pre-hero source (an implant, a gene or a trait) while the
# Echo that uses them is being built. Each is to lose one source once it is decided whether the
# old item stays in the game; until then two sources are expected, and only these two.
SHARED_WITH_ECHO = {
    "AG_VectorReflection", "AG_VectorSurge", "AG_VectorShove", "AG_ShinraTensei",
    "AG_Imperative_Stop", "AG_Imperative_Drop", "AG_Imperative_Kneel", "AG_Imperative_Come",
    "AG_Imperative_Run",
}

for ability, f in sorted(ability_defs.items()):
    sources = grants.get(ability, set())
    if ability in RETIRED and not sources:
        continue
    if ability in SHARED_WITH_ECHO and len(sources) == 2 \
            and sum(1 for src in sources if src.startswith("RimArt.EchoDef:")) == 1:
        continue
    if len(sources) != 1:
        fail("ability source count", f,
             ability + " has " + str(len(sources)) + " acquisition sources: "
             + (", ".join(sorted(sources)) or "none"))

if problems:
    print("FAILED -- " + str(len(problems)) + " problem(s)\n")
    for kind, where, detail in problems:
        print("  [" + kind + "] " + where + "\n      " + detail)
    sys.exit(1)

print("OK -- " + str(len(my_files)) + " def files, " + str(len(declared)) +
      " abstract names, " + str(len(mine)) + " defNames, " + str(len(patch_files)) +
      " patch files, no problems")
