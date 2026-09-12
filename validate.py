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
  7. Translate keys used in C# but not defined in Languages/
  8. Concrete abilities with zero or multiple acquisition sources

Usage: python3 validate.py [path/to/RimWorld/Data]
"""
import os, re, sys, glob, subprocess
import xml.etree.ElementTree as ET

DATA = sys.argv[1] if len(sys.argv) > 1 else \
    "/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Data"
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
}
LIST_TAGS = {"abilities", "descriptionHyperlinks", "exceptions",
             "recipeUsers", "thingDefs", "prerequisites", "thingCategories",
             "categories", "appliedOnFixedBodyParts"}

problems = []
def fail(kind, where, detail):
    problems.append((kind, where, detail))

# Patch_* folders are defs that only load alongside some other mod (see loadFolders.xml).
# They are checked the same as any other def: the game will not read them without that mod
# installed, which means a typo in one is invisible until the one player who has both finds it.
my_files = sorted(glob.glob("1.6/Defs/**/*.xml", recursive=True)
                  + glob.glob("Patch_*/**/Defs/**/*.xml", recursive=True))

# 1. well-formedness
for f in my_files + sorted(glob.glob("Languages/**/*.xml", recursive=True)) + ["About/About.xml"]:
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
            elif el.tag in REF_TAGS and el.text and el.text.strip():
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
            if any(os.path.exists(os.path.join("Textures", tex + ext))
                   for ext in (".png", ".jpg")):
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
    for f in my_files:
        for m in re.finditer(r'Class="' + NAMESPACE + r'\.([A-Za-z_0-9]+)"', open(f).read()):
            if m.group(1) not in blob:
                fail("class not in assembly", f, NAMESPACE + "." + m.group(1))
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
        for ability in refs:
            grants.setdefault(ability, set()).add(source)

for ability, f in sorted(ability_defs.items()):
    sources = grants.get(ability, set())
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
      " abstract names, " + str(len(mine)) + " defNames, no problems")
