#!/usr/bin/env python3
"""
Static checks for the mod. Catches the things RimWorld only complains about at
runtime, in the order they have actually bitten this project:

  1. XML well-formedness
  2. Duplicate Name= declarations -- RimWorld's Name attribute is ONE namespace
     shared by every def type in a mod, so an abstract HediffDef and an abstract
     AbilityDef cannot share a name. The second is silently dropped and its
     children inherit the wrong base.
  3. Unresolved ParentName
  4. Def and texture references that do not resolve in Core or Biotech (anything
     Royalty-only would break for players without that DLC)
  5. Custom Class= values that do not exist in the built assembly
  6. Comp classes a def ends up with twice once inheritance is applied
  7. Translate keys used in C# but not defined in Languages/

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
}
LIST_TAGS = {"abilities", "descriptionHyperlinks", "exceptions"}

problems = []
def fail(kind, where, detail):
    problems.append((kind, where, detail))

my_files = sorted(glob.glob("1.6/Defs/**/*.xml", recursive=True))

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

# 4. references
if DATA is not None:
    stats = set()
    for f in my_files:
        for el in ET.parse(f).getroot().iter():
            vals = []
            if el.tag in REF_TAGS and el.text and el.text.strip():
                vals = [el.text.strip()]
            elif el.tag in LIST_TAGS:
                vals = [c.text.strip() for c in el if c.text]
            elif el.tag in ("statBases", "statOffsets", "statFactors"):
                stats.update(c.tag for c in el)
            for v in vals:
                if v in mine: continue
                w = vanilla.get(v)
                if not w: fail("unresolved ref <" + el.tag + ">", f, v)
                elif w == {"Royalty"}: fail("Royalty-only ref <" + el.tag + ">", f, v)
        for m in re.finditer(r"<(?:iconPath|texPath)>([^<]+)<", open(f).read()):
            w = vanilla.get("TEX:" + m.group(1).strip())
            if not w: fail("unresolved texture", f, m.group(1))
            elif w == {"Royalty"}: fail("Royalty-only texture", f, m.group(1))
    for s in sorted(stats):
        w = vanilla.get(s)
        if not w: fail("unknown stat", "statBases/Offsets/Factors", s)
        elif w == {"Royalty"}: fail("Royalty-only stat", "statBases/Offsets/Factors", s)

# 5. custom Class= exists in the assembly
dll = "1.6/Assemblies/AbilityGenes.dll"
if os.path.exists(dll):
    blob = open(dll, "rb").read().decode("latin-1")
    for f in my_files:
        for m in re.finditer(r'Class="AbilityGenes\.([A-Za-z_0-9]+)"', open(f).read()):
            if m.group(1) not in blob:
                fail("class not in assembly", f, "AbilityGenes." + m.group(1))
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

if problems:
    print("FAILED -- " + str(len(problems)) + " problem(s)\n")
    for kind, where, detail in problems:
        print("  [" + kind + "] " + where + "\n      " + detail)
    sys.exit(1)

print("OK -- " + str(len(my_files)) + " def files, " + str(len(declared)) +
      " abstract names, " + str(len(mine)) + " defNames, no problems")
