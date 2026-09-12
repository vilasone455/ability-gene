using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Hand a pawn any kit in the mod, in one click, with its acquisition route bypassed.
    ///
    /// The routes are the balance - a gene has to be found and implanted, an implant researched,
    /// crafted and fitted by a surgeon, a weapon trait rolled on a unique weapon, and Origin:
    /// Blade earned over five studies and two skill thresholds. Every one of those is correct
    /// for play and useless as a test loop, which is the same argument
    /// <see cref="DebugActions_Dispersal"/> makes about waiting for a real hit.
    ///
    /// So each entry here does two things: it satisfies whatever the kit needs, and then it
    /// grants the kit by the same call the real route would have made at the end. Nothing here
    /// is a second implementation of a kit - the gene is added to the gene tracker, the implant
    /// is the hediff the surgery installs, the belt is worn, the weapon trait goes on a real
    /// unique weapon, and the origin goes through OriginBladeUtility.Awaken. If a grant would
    /// drift from what the game does, it is wrong here rather than in the kit.
    ///
    /// Where a requirement cannot be forced - no gene tracker on a mechanoid, a skill the
    /// backstory disabled outright, a mod that is not loaded - the entry says so and does
    /// nothing, rather than half-granting a kit and leaving the player to work out which half.
    /// </summary>
    public static class DebugActions_Kits
    {
        private const string Category = "RimArts";

        private class Kit
        {
            public string Label;

            /// <summary>Grants it, or returns why it could not. Null means it worked.</summary>
            public Func<Pawn, string> Grant;
        }

        // ----------------------------------------------------------------- the menu

        [DebugAction(Category, "Grant kit...", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GrantKit(Pawn pawn)
        {
            if (pawn == null) return;

            List<Kit> kits = Kits();
            List<DebugMenuOption> options = new List<DebugMenuOption>();

            foreach (Kit kit in kits)
            {
                Kit captured = kit;
                options.Add(new DebugMenuOption(captured.Label, DebugMenuOptionMode.Action,
                    () => Apply(pawn, captured)));
            }

            options.Add(new DebugMenuOption("ALL of the above", DebugMenuOptionMode.Action,
                () => ApplyAll(pawn, kits)));

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        private static void Apply(Pawn pawn, Kit kit)
        {
            string refused = Run(pawn, kit);

            Messages.Message(
                refused == null
                    ? pawn.LabelShortCap + " was given " + kit.Label + "."
                    : kit.Label + ": " + refused,
                pawn, refused == null ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput,
                false);
        }

        /// <summary>
        /// Every kit at once, reported as one line. Three of them want the weapon slot - the
        /// frost bomb and the two weapon traits - and the stasis belt wants the belt slot, so
        /// the last one to run holds each and the earlier weapons end up in the pawn's
        /// inventory. Worth knowing before reading the result as a bug.
        /// </summary>
        private static void ApplyAll(Pawn pawn, List<Kit> kits)
        {
            List<string> refusals = new List<string>();
            int granted = 0;

            foreach (Kit kit in kits)
            {
                string refused = Run(pawn, kit);
                if (refused == null) granted++;
                else refusals.Add(kit.Label + ": " + refused);
            }

            string report = "Gave " + pawn.LabelShortCap + " " + granted + " of " + kits.Count + " kits.";
            if (refusals.Count > 0) report += " Skipped - " + string.Join("; ", refusals);

            Messages.Message(report, pawn,
                refusals.Count == 0 ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.CautionInput,
                false);
        }

        /// <summary>
        /// One grant, with the exception guard that keeps a half-loaded game from taking the
        /// whole menu down. A dev tool that throws is a dev tool nobody presses twice.
        /// </summary>
        private static string Run(Pawn pawn, Kit kit)
        {
            try
            {
                return kit.Grant(pawn);
            }
            catch (Exception exception)
            {
                Log.Error("[RimArt] granting " + kit.Label + " failed: " + exception);
                return "threw " + exception.GetType().Name + ", see the log";
            }
        }

        // ----------------------------------------------------------------- the roster

        private static List<Kit> Kits()
        {
            return new List<Kit>
            {
                Gene("Corrosive glands", "AG_CorrosiveGlands"),
                Gene("Hypermetabolic glands", "AG_HypermetabolicGlands"),
                Gene("Anchor organ", "AG_AnchorOrgan"),
                Gene("Fold organ", "AG_InvoluteOrgan"),
                Gene("Dispersal plexus", "AG_DispersalPlexus"),

                Trait("Combat presence", "AG_CombatPresence"),
                Trait("Pain debt", "AG_PainDebt"),
                Trait("Commanding voice", "AG_CommandingVoice"),

                Implant("Neural accelerator", "AG_NeuralAccelerator", "Brain", "Bionics"),
                Implant("Reflex booster", "AG_ReflexBooster", "Spine", "Prosthetics"),
                // No research: the phase barrier is deliberately uncraftable.
                Implant("Phase barrier", "AG_PhaseBarrier", "Brain", null),

                new Kit { Label = "Stasis belt", Grant = GrantStasisBelt },
                new Kit { Label = "Frost bomb", Grant = GrantFrostBomb },

                WeaponTrait("Resonant weapon", "AG_WeaponResonance"),
                WeaponTrait("Arcing weapon", "AG_WeaponArc"),

                new Kit { Label = "Origin: Blade", Grant = GrantOriginBlade },
            };
        }

        // ----------------------------------------------------------------- genes

        private static Kit Gene(string label, string geneDefName)
        {
            return new Kit
            {
                Label = label,
                Grant = pawn =>
                {
                    GeneDef def = DefDatabase<GeneDef>.GetNamedSilentFail(geneDefName);
                    if (def == null) return "no GeneDef " + geneDefName + " - is Biotech active?";
                    if (pawn.genes == null) return pawn.LabelShortCap + " has no genes to add to";
                    if (pawn.genes.HasActiveGene(def)) return "already has it";

                    // Xenogene rather than endogene: this is something done to the pawn, which
                    // is what the acquisition route is, and it keeps it out of their children.
                    pawn.genes.AddGene(def, true);
                    return null;
                }
            };
        }

        // ----------------------------------------------------------------- traits

        private static Kit Trait(string label, string traitDefName)
        {
            return new Kit
            {
                Label = label,
                Grant = pawn =>
                {
                    TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
                    if (def == null) return "no TraitDef " + traitDefName;
                    if (pawn.story == null || pawn.story.traits == null) return "has no traits";
                    if (pawn.story.traits.HasTrait(def)) return "already has it";

                    // suppressConflicts, because forcing the condition is the point: a pawn who
                    // rolled a conflicting trait should still get the kit under test.
                    pawn.story.traits.GainTrait(new Trait(def, 0, true), true);
                    return null;
                }
            };
        }

        // ----------------------------------------------------------------- implants

        private static Kit Implant(string label, string hediffDefName, string partDefName,
            string researchDefName)
        {
            return new Kit
            {
                Label = label,
                Grant = pawn =>
                {
                    HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
                    if (def == null) return "no HediffDef " + hediffDefName;
                    if (pawn.health == null) return "has no health tracker";
                    if (pawn.health.hediffSet.GetFirstHediffOfDef(def) != null) return "already fitted";

                    BodyPartRecord part = pawn.health.hediffSet
                        .GetNotMissingParts()
                        .FirstOrDefault(candidate => candidate.def != null
                                                     && candidate.def.defName == partDefName);
                    if (part == null) return "has no " + partDefName.ToLower() + " to fit it to";

                    pawn.health.AddHediff(def, part);

                    // The device stays craftable afterwards, so the next one can go through the
                    // real route without the research being the thing in the way.
                    FinishResearch(researchDefName);
                    return null;
                }
            };
        }

        // ----------------------------------------------------------------- stasis belt

        private static string GrantStasisBelt(Pawn pawn)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("AG_StasisBelt");
            if (def == null) return "no ThingDef AG_StasisBelt";
            if (pawn.apparel == null) return "cannot wear apparel";
            if (pawn.apparel.WornApparel.Any(worn => worn.def == def)) return "already wearing one";

            FinishResearch("AG_StasisFields");

            Apparel belt = (Apparel)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
            pawn.apparel.Wear(belt, true, false);
            return null;
        }

        // ----------------------------------------------------------------- frost bomb

        /// <summary>
        /// Puts a frost bomb in the pawn's hands, ready to throw.
        ///
        /// Equipped rather than dropped at their feet, because the thing being tested is the
        /// throw and an item on the floor is two more clicks before any of it happens. The
        /// pawn's existing weapon goes to their inventory, so a test pawn does not silently lose
        /// the rifle they were carrying.
        ///
        /// The weapon slot has to be genuinely empty before the bomb goes in, and the clearing
        /// is checked rather than assumed. AddEquipment does not refuse a second primary - it
        /// logs a red error and returns, leaving the pawn holding the old weapon - so an
        /// unchecked transfer produces a dev action that reports success and grants nothing. The
        /// transfer really can fail: an inventory can refuse the weapon, and under Combat
        /// Extended a pawn near their bulk limit will. Dropping it is the fallback, and being
        /// unable to do either is reported instead of being papered over.
        /// </summary>
        private static string GrantFrostBomb(Pawn pawn)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("AG_FrostBomb");
            if (def == null) return "no ThingDef AG_FrostBomb";
            if (pawn.equipment == null) return "cannot carry equipment";
            if (pawn.equipment.Primary?.def == def) return "already holding one";

            FinishResearch("AG_CryogenicMunitions");

            ThingWithComps held = pawn.equipment.Primary;
            if (held != null)
            {
                bool stowed = pawn.inventory != null
                    && pawn.equipment.TryTransferEquipmentToContainer(held, pawn.inventory.innerContainer);

                if (!stowed && !pawn.equipment.TryDropEquipment(held, out _, pawn.Position, false))
                    return "could not put down " + held.LabelShortCap;
            }

            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def)));
            return null;
        }

        // ----------------------------------------------------------------- weapon traits

        /// <summary>
        /// Puts a real unique melee weapon carrying the trait into the pawn's hands.
        ///
        /// The trait is the kit, and a trait with no weapon under it is nothing - the ability
        /// comes off the weapon's own equippable-ability comp, which only exists once
        /// CompUniqueWeapon has been told the trait is there. So this makes the weapon, adds the
        /// trait, runs the same Setup the game runs when one is generated, and equips it.
        /// </summary>
        private static Kit WeaponTrait(string label, string traitDefName)
        {
            return new Kit
            {
                Label = label,
                Grant = pawn =>
                {
                    WeaponTraitDef trait = DefDatabase<WeaponTraitDef>.GetNamedSilentFail(traitDefName);
                    if (trait == null)
                    {
                        return "no WeaponTraitDef " + traitDefName
                               + " - needs Odyssey and Unique Melee Weapons";
                    }
                    if (pawn.equipment == null) return "cannot hold a weapon";

                    ThingDef weaponDef = UniqueMeleeDefFor(trait);
                    if (weaponDef == null)
                    {
                        return "no unique melee weapon accepts " + trait.weaponCategory?.defName;
                    }

                    ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(
                        weaponDef, GenStuff.DefaultStuffFor(weaponDef));

                    CompUniqueWeapon unique = weapon.TryGetComp<CompUniqueWeapon>();
                    if (unique == null) return weaponDef.defName + " lost its unique weapon comp";

                    unique.AddTrait(trait);
                    unique.Setup(false);

                    pawn.equipment.MakeRoomFor(weapon);
                    pawn.equipment.AddEquipment(weapon);
                    return null;
                }
            };
        }

        /// <summary>
        /// The first melee weapon whose unique-weapon comp declares this trait's category.
        /// Read off the defs rather than named, so it keeps working whatever Unique Melee
        /// Weapons ships.
        /// </summary>
        private static ThingDef UniqueMeleeDefFor(WeaponTraitDef trait)
        {
            if (trait.weaponCategory == null) return null;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.IsMeleeWeapon || def.comps == null) continue;

                foreach (CompProperties props in def.comps)
                {
                    CompProperties_UniqueWeapon uniqueProps = props as CompProperties_UniqueWeapon;
                    if (uniqueProps == null || uniqueProps.weaponCategories == null) continue;
                    if (uniqueProps.weaponCategories.Contains(trait.weaponCategory)) return def;
                }
            }

            return null;
        }

        // ----------------------------------------------------------------- origin: blade

        /// <summary>
        /// The one kit with a real checklist, so this fills the checklist in rather than
        /// granting the trait behind it. Awakening still goes through OriginBladeUtility, which
        /// means the psycast and ranged-weapon restrictions are applied by the same code the
        /// letter would have used.
        /// </summary>
        private static string GrantOriginBlade(Pawn pawn)
        {
            if (OriginBladeUtility.HasOrigin(pawn)) return "already awakened";
            if (pawn.skills == null || pawn.story == null) return "has no skills to raise";
            if (!pawn.IsColonistPlayerControlled) return "must be a player-controlled colonist";

            SkillRecord melee = pawn.skills.GetSkill(SkillDefOf.Melee);
            SkillRecord crafting = pawn.skills.GetSkill(SkillDefOf.Crafting);
            if (melee.TotallyDisabled) return "melee is disabled by backstory or traits";
            if (crafting.TotallyDisabled) return "crafting is disabled by backstory or traits";

            if (melee.Level < OriginBladeUtility.MeleeRequired)
            {
                melee.Level = OriginBladeUtility.MeleeRequired;
            }
            if (crafting.Level < OriginBladeUtility.CraftingRequired)
            {
                crafting.Level = OriginBladeUtility.CraftingRequired;
            }

            GameComponent_BladeStudy study = Current.Game.GetComponent<GameComponent_BladeStudy>();
            if (study == null) return "no blade study component";

            List<ThingDef> blades = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(OriginBladeUtility.IsBlade)
                .Take(OriginBladeUtility.BladesRequired)
                .ToList();
            if (blades.Count < OriginBladeUtility.BladesRequired)
            {
                return "only " + blades.Count + " studyable blade types exist";
            }

            // Written straight into the record rather than through CompleteStudy: that path
            // announces each blade and then offers the awakening by letter, and the point of
            // the dev tool is to skip the offer, not to replay it five times.
            BladeStudyRecord record = study.RecordFor(pawn);
            record.bladeTypes.Clear();
            foreach (ThingDef blade in blades) record.bladeTypes.Add(blade.defName);
            record.offered = true;

            OriginBladeUtility.Awaken(pawn);
            return OriginBladeUtility.HasOrigin(pawn) ? null : "the awakening was refused";
        }

        // ----------------------------------------------------------------- research

        /// <summary>
        /// Finishes a project and everything it stands on. Prerequisites matter because a
        /// finished project behind an unfinished one is still not craftable.
        /// </summary>
        private static void FinishResearch(string researchDefName)
        {
            if (researchDefName == null) return;

            ResearchProjectDef project = DefDatabase<ResearchProjectDef>
                .GetNamedSilentFail(researchDefName);
            if (project == null || project.IsFinished) return;

            if (project.prerequisites != null)
            {
                foreach (ResearchProjectDef prerequisite in project.prerequisites)
                {
                    if (prerequisite != null) FinishResearch(prerequisite.defName);
                }
            }

            Find.ResearchManager.FinishProject(project, false, null, false);
        }
    }
}
