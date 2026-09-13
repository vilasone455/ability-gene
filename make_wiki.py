#!/usr/bin/env python3
"""
Builds a player-facing wiki page from the mod's defs: every kit, what it is, how a player
gets it, and each ability in it with its description, cooldown, charges, range and cast time.

The page is written for players, so it prints in-game labels only ("bionics", "fabrication
bench", "spine") and never a defName. Labels for vanilla things - research, benches, body
parts, materials - are read from the game's own Data folder; without it the script falls back
to splitting the defName into words, which reads worse but is still not an ID.

Kits are found the same way validate.py check 8 finds acquisition sources: a gene, trait,
implant, apparel or weapon trait that lists abilities. Throwable weapons with a command of
their own (frost bomb, mimic beacon) are added as weapon kits.

Usage: python3 make_wiki.py [path/to/RimWorld/Data] [-o docs/wiki.md]
"""
import argparse, copy, glob, os, re
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("data", nargs="?",
                    default="/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Data")
parser.add_argument("-o", "--out", default="docs/wiki.md")
args = parser.parse_args()

# Things the defs cannot say. Keyed by defName; the text is what the player reads.
EARNED = {
    "AG_OriginBlade": [
        "Cannot appear on generated pawns. It has to be earned.",
        "Reach Melee 14 and Crafting 12.",
        "Study five different bladed melee weapon types: select an undrafted colonist, "
        "right-click a blade on the ground and choose **Study blade** (one in-game hour each, "
        "the weapon is not used up). Material and quality variants of one weapon count once.",
        "When all requirements are met a letter offers **Awaken** or **Not yet**. Declining is "
        "free; the Origin: Blade button can awaken the pawn later.",
        "Awakening removes all psylink levels and psycasts, and the pawn can never use ranged "
        "weapons again.",
    ],
}
MOD_NAMES = {
    "Ludeon.RimWorld.Biotech": "Biotech DLC",
    "Ludeon.RimWorld.Royalty": "Royalty DLC",
    "Ludeon.RimWorld.Ideology": "Ideology DLC",
    "Ludeon.RimWorld.Anomaly": "Anomaly DLC",
    "Ludeon.RimWorld.Odyssey": "Odyssey DLC",
    "co.uk.epicguru.meleeanimation": "Melee Animation",
    "shunter.uniquemeleeweapons": "Unique Melee Weapons",
    "ceteam.combatextended": "Combat Extended",
}
# Weapon categories that live in other mods, so their labels are not in the game's Data folder.
CATEGORY_NAMES = {"UMW_Melee": "unique melee weapons", "UMW_Bladed": "unique bladed weapons"}
TYPE_ORDER = ["Gene", "Archite gene", "Trait", "Implant", "Wearable", "Weapon trait", "Weapon"]


# ---------------------------------------------------------------- loading and inheritance

def parse_all(files):
    out = []
    for f in files:
        try:
            out.extend((el, f) for el in ET.parse(f).getroot())
        except ET.ParseError:
            pass
    return out

vanilla_files = []
for dlc in ("Core", "Royalty", "Biotech", "Odyssey"):
    vanilla_files += glob.glob(os.path.join(args.data, dlc, "Defs", "**", "*.xml"), recursive=True)
mod_files = sorted(glob.glob("1.6/Defs/**/*.xml", recursive=True)
                   + glob.glob("Patch_*/**/Defs/**/*.xml", recursive=True))
vanilla = parse_all(vanilla_files)
mod = parse_all(mod_files)

named = {}          # Name= attribute -> element, for ParentName lookups
for el, _ in vanilla + mod:
    if el.get("Name"):
        named[el.get("Name")] = el

def merge(parent, child):
    """RimWorld's XML inheritance: child values win, <li> lists append unless Inherit=False."""
    out = copy.deepcopy(parent)
    out.tag, out.attrib = child.tag, dict(child.attrib)
    if len(child) == 0:
        out.text = child.text
        for sub in list(out):
            out.remove(sub)
        return out
    for sub in child:
        existing = out.find(sub.tag) if sub.tag != "li" else None
        if sub.tag == "li":
            out.append(copy.deepcopy(sub))
        elif existing is None or (sub.get("Inherit") or "").lower() == "false":
            if existing is not None:
                out.remove(existing)
            out.append(copy.deepcopy(sub))
        else:
            out.remove(existing)
            out.append(merge(existing, sub))
    return out

resolved_cache = {}
def resolve(el):
    if id(el) in resolved_cache:
        return resolved_cache[id(el)]
    parent = named.get(el.get("ParentName") or "")
    result = merge(resolve(parent), el) if parent is not None else el
    resolved_cache[id(el)] = result
    return result

defs = {}           # (tag, defName) -> resolved element; mod defs override vanilla
for el, _ in vanilla + mod:
    name = (el.findtext("defName") or "").strip()
    if name and (el.get("Abstract") or "").lower() != "true":
        defs[(el.tag, name)] = el
mod_keys = [(el.tag, (el.findtext("defName") or "").strip()) for el, _ in mod
            if el.findtext("defName") and (el.get("Abstract") or "").lower() != "true"]

def get(tag, name):
    el = defs.get((tag, name))
    return resolve(el) if el is not None else None


# ---------------------------------------------------------------- wording helpers

def words(def_name):
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", re.sub(r"^AG_", "", def_name)).replace("_", " ").lower()

def label(tag, name):
    el = get(tag, name)
    if el is not None:
        text = el.findtext("label") or el.findtext("degreeDatas/li/label")
        if text:
            return text.strip()
    return words(name)

def text_of(el, path):
    return (el.findtext(path) or "").strip()

def paragraphs(desc):
    return [p.strip() for p in re.split(r"(?:\\n|\n)\s*(?:\\n|\n)", desc.strip()) if p.strip()]

def number(value):
    return ("%g" % value) if value != int(value) else "{:,}".format(int(value))

def ticks(n):
    if n >= 60000:
        v, unit = n / 60000, "day"
    elif n >= 2500:
        v, unit = n / 2500, "hour"
    else:
        v, unit = n / 60, "second"
    v = round(v, 1)
    return number(v) + " " + unit + ("" if v == 1 else "s") + ("" if unit == "second" else " (in-game)")

def tick_range(text):
    parts = [float(p) for p in text.split("~")]
    return " to ".join(ticks(p) for p in parts) if len(set(parts)) > 1 else ticks(parts[0])

def join(items, word="and"):
    items = list(items)
    return items[0] if len(items) == 1 else ", ".join(items[:-1]) + " " + word + " " + items[-1]

def cost_list(el):
    return join(label("ThingDef", c.tag) + " ×" + number(float(c.text)) for c in el.find("costList")) \
        if el.find("costList") is not None and len(el.find("costList")) else ""

def mods_needed(el):
    ids = [m.strip() for m in (el.get("MayRequire") or "").split(",") if m.strip()]
    return [MOD_NAMES.get(m, m) for m in ids]

def research(name):
    r = get("ResearchProjectDef", name)
    line = "researching **" + label("ResearchProjectDef", name) + "**"
    if r is not None:
        prereqs = [li.text.strip() for li in r.findall("prerequisites/li") if li.text]
        if prereqs:
            line += " (which needs " + join(label("ResearchProjectDef", p) for p in prereqs) + ")"
    return line

def ammo_lines(thing):
    """Reloadable apparel: what it holds and where the ammunition is made."""
    reload = thing.find("comps/li[@Class='CompProperties_ApparelReloadable']")
    ammo = (reload.findtext("ammoDef") or "").strip() if reload is not None else ""
    if not ammo:
        return []
    per = reload.findtext("ammoCountPerCharge") or "1"
    line = ("Holds " + reload.findtext("maxCharges").strip() + " charges and starts full. Reload with "
            + label("ThingDef", ammo) + " (" + per.strip() + " per charge).")
    for tag, name in mod_keys:
        recipe = get(tag, name) if tag == "RecipeDef" else None
        count = recipe.findtext("products/" + ammo) if recipe is not None else None
        if not count:
            continue
        benches = [li.text.strip() for li in recipe.findall("recipeUsers/li") if li.text]
        inputs = [number(float(li.findtext("count"))) + " " + label("ThingDef", li.findtext("filter/thingDefs/li").strip())
                  for li in recipe.findall("ingredients/li") if li.findtext("filter/thingDefs/li")]
        line += (" Make " + count.strip() + " from " + join(inputs) + " at a "
                 + join((label("ThingDef", b) for b in benches), "or"))
        if recipe.findtext("researchPrerequisite"):
            line += " after " + research(recipe.findtext("researchPrerequisite").strip())
        line += "."
    return [line]

def craft_lines(thing):
    """How an item is made, found and bought."""
    lines = []
    maker = thing.find("recipeMaker")
    if maker is not None:
        benches = [li.text.strip() for li in maker.findall("recipeUsers/li") if li.text]
        line = "Craft it"
        if benches:
            line += " at a " + join((label("ThingDef", b) for b in benches), "or")
        if maker.findtext("researchPrerequisite"):
            line += " after " + research(maker.findtext("researchPrerequisite").strip())
        line += "."
        skills = [s.tag + " skill " + s.text.strip() for s in maker.findall("skillRequirements/*")]
        if skills:
            line += " Needs " + join(skills) + "."
        cost = cost_list(thing)
        if cost:
            line += " Costs " + cost + "."
        lines.append(line)
    else:
        lines.append("Cannot be crafted.")
    lines.extend(ammo_lines(thing))
    if any(li.text and li.text.startswith("Reward") for li in thing.findall("thingSetMakerTags/li")):
        lines.append("Can be given as a quest reward.")
    if thing.findall("tradeTags/li"):
        lines.append("Can be bought from traders.")
    return lines


# ---------------------------------------------------------------- kits

kits = []

def add_kit(kind, name, desc, how, ability_names, source_el):
    kits.append({"type": kind, "name": name, "desc": desc, "how": how,
                 "abilities": [a for a in ability_names], "mods": mods_needed(source_el)})

for tag, name in mod_keys:
    el = get(tag, name)
    if tag == "GeneDef" and el.findall("abilities/li"):
        arc = int(text_of(el, "biostatArc") or 0)
        stats = ["Complexity " + text_of(el, "biostatCpx") if text_of(el, "biostatCpx") else "",
                 "Metabolism " + text_of(el, "biostatMet") if text_of(el, "biostatMet") else "",
                 "Archites " + str(arc) if arc else ""]
        how = ["Found in genepacks. Put it in a xenogerm at a gene assembler and implant it."]
        if arc:
            how.append("This is an archite gene: the xenogerm needs an archite capsule.")
        how.append("Gene stats: " + ", ".join(s for s in stats if s) + ".")
        add_kit("Archite gene" if arc else "Gene", text_of(el, "label"), text_of(el, "description"),
                how, [li.text.strip() for li in el.findall("abilities/li")], el)

    elif tag == "TraitDef" and el.findall("modExtensions/li/abilities/li"):
        how = list(EARNED.get(name, []))
        if not how:
            if float(text_of(el, "commonality") or 1) > 0:
                how.append("A trait that can appear on new colonists and other generated pawns.")
            else:
                how.append("Cannot appear on generated pawns.")
        conflicts = [li.text.strip() for li in el.findall("conflictingTraits/li") if li.text]
        if conflicts:
            how.append("Cannot be on the same pawn as: " + join(label("TraitDef", c) for c in conflicts) + ".")
        if any(li.text == "Violent" for li in el.findall("requiredWorkTags/li")):
            how.append("The pawn must be capable of violence.")
        add_kit("Trait", text_of(el, "degreeDatas/li/label"), text_of(el, "degreeDatas/li/description"),
                how, [li.text.strip() for li in el.findall("modExtensions/li/abilities/li")], el)

    elif tag == "HediffDef" and el.findall("abilities/li"):
        recipes = [get(*k) for k in mod_keys if k[0] == "RecipeDef"]
        recipes = [r for r in recipes if text_of(r, "addsHediff") == name]
        if not recipes:
            continue        # a hediff granted some other way, not an installable kit
        recipe = recipes[0]
        item = get("ThingDef", name)
        if item is None:
            item = get("ThingDef", text_of(el, "spawnThingOnRemoved"))
        how = craft_lines(item) if item is not None else []
        parts = join(label("BodyPartDef", li.text.strip()) for li in recipe.findall("appliedOnFixedBodyParts/li"))
        line = "Install it by surgery in the " + parts + "."
        med = text_of(recipe, "skillRequirements/Medicine")
        if med:
            line += " Needs Medicine skill " + med + "."
        medicine = [li.findtext("count") for li in recipe.findall("ingredients/li")
                    if li.find("filter/categories/li") is not None]
        if medicine:
            line += " Uses the item and " + medicine[0].strip() + " medicine."
        how.append(line)
        if text_of(el, "spawnThingOnRemoved"):
            how.append("Can be removed by surgery and given to another colonist.")
        add_kit("Implant", text_of(el, "label"), text_of(item if item is not None else el, "description"),
                how, [li.text.strip() for li in el.findall("abilities/li")], el)

    elif tag == "ThingDef" and el.findall("comps/li/abilities/li"):
        how = craft_lines(el) + ["Wear it to use the ability. Taking it off removes the ability."]
        add_kit("Wearable", text_of(el, "label"), text_of(el, "description"), how,
                [li.text.strip() for li in el.findall("comps/li/abilities/li")], el)

    elif tag == "WeaponTraitDef" and el.find("abilityProps/abilityDef") is not None:
        category = text_of(el, "weaponCategory")
        cat = CATEGORY_NAMES.get(category) or label("WeaponCategoryDef", category)
        how = ["A weapon trait that can roll on " + cat + ".",
               "Equip a weapon with this trait to get the ability."]
        add_kit("Weapon trait", text_of(el, "label"), text_of(el, "description"), how,
                [text_of(el, "abilityProps/abilityDef")], el)

    elif tag == "ThingDef" and el.find("verbs/li/label") is not None and el.find("costList") is not None:
        how = craft_lines(el) + ["Equip it as a weapon; the throw is a button on the pawn."]
        verb = el.find("verbs/li")
        kits.append({"type": "Weapon", "name": text_of(el, "label"), "desc": text_of(el, "description"),
                     "how": how, "mods": mods_needed(el), "abilities": [],
                     "verb": {"name": text_of(verb, "label"),
                              "cooldown": text_of(el, "statBases/RangedWeapon_Cooldown"),
                              "range": text_of(verb, "range"), "warmup": text_of(verb, "warmupTime")}})

kits.sort(key=lambda k: (TYPE_ORDER.index(k["type"]), k["name"].lower()))


# ---------------------------------------------------------------- abilities

def ability_facts(el):
    facts = []
    if text_of(el, "cooldownTicksRange"):
        cd = tick_range(text_of(el, "cooldownTicksRange"))
        per = text_of(el, "cooldownPerCharge").lower() == "true"
        facts.append(("Cooldown", cd + (" per charge" if per else "")))
    if text_of(el, "charges"):
        facts.append(("Charges", text_of(el, "charges")))
    rng = text_of(el, "verbProperties/range")
    if rng and float(rng) >= 999:
        facts.append(("Range", "unlimited"))
    elif rng and float(rng) > 0:
        facts.append(("Range", number(float(rng)) + " cells"))
    elif text_of(el, "targetRequired").lower() == "false" or rng:
        facts.append(("Range", "self"))
    warm = text_of(el, "verbProperties/warmupTime")
    if warm and float(warm) > 0:
        facts.append(("Cast time", ticks(float(warm) * 60)))
    dur = text_of(el, "statBases/Ability_Duration")
    if dur:
        facts.append(("Duration", ticks(float(dur) * 60)))
    return facts

def cap(s):
    return s[:1].upper() + s[1:]

def anchor(s):
    return re.sub(r"[^a-z0-9 -]", "", s.lower()).replace(" ", "-")

out = ["<!-- Generated by make_wiki.py from the mod files. Edit the mod, then re-run it. -->", "",
       "# RimArts: Combat Abilities - Wiki", "",
       "Every kit in the mod, how to get it, and what each ability does.", "",
       "**Required:** " + join(d.findtext("displayName").strip()
                               for d in ET.parse("About/About.xml").getroot().findall("modDependencies/li")), "",
       "| Kit | Type | Abilities | Needs |", "|---|---|---|---|"]
for k in kits:
    names = [label("AbilityDef", a) for a in k["abilities"]] or [k["verb"]["name"]]
    out.append("| [" + cap(k["name"]) + "](#" + anchor(k["name"]) + ") | " + k["type"] + " | "
               + ", ".join(cap(n) for n in names) + " | " + (", ".join(k["mods"]) or "-") + " |")

current_type = None
for k in kits:
    if k["type"] != current_type:
        current_type = k["type"]
        out += ["", "## " + current_type + ("s" if not current_type.endswith("s") else "")]
    out += ["", "### " + cap(k["name"]), ""]
    out += [p + "\n" for p in paragraphs(k["desc"])]
    if k["mods"]:
        out += ["**Needs:** " + ", ".join(k["mods"]), ""]
    out += ["**How to get it**", ""] + ["- " + line for line in k["how"]] + [""]

    if k.get("verb"):
        v = k["verb"]
        facts = [("Cooldown", ticks(float(v["cooldown"]) * 60))] if v["cooldown"] else []
        if v["range"]:
            facts.append(("Range", number(float(v["range"])) + " cells"))
        if v["warmup"]:
            facts.append(("Cast time", ticks(float(v["warmup"]) * 60)))
        out += ["#### " + cap(v["name"]), "", " | ".join("**" + a + ":** " + b for a, b in facts), ""]
        continue

    for a in k["abilities"]:
        el = get("AbilityDef", a)
        if el is None:
            continue
        out += ["#### " + cap(text_of(el, "label")), ""]
        out += [p + "\n" for p in paragraphs(text_of(el, "description"))]
        facts = ability_facts(el)
        if facts:
            out += [" | ".join("**" + x + ":** " + y for x, y in facts), ""]
        needs = [m for m in mods_needed(el) if m not in k["mods"]]
        if needs:
            out += ["**Needs:** " + ", ".join(needs), ""]

os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
with open(args.out, "w") as f:
    f.write("\n".join(out).rstrip() + "\n")
print("wrote " + args.out + ": " + str(len(kits)) + " kits, "
      + str(sum(len(k["abilities"]) or 1 for k in kits)) + " abilities")
