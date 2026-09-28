using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Kamui: Warp (docs/hero-echo.md "Obito"): 1 s warm-up (the verb's), then Obito is inside the Kamui
    /// dimension. From inside, the same button picks any revealed cell of the map he left; a warning mark shows
    /// there for <see cref="exitMarkSeconds"/>, then he comes out. The cooldown starts at the exit. Cast cost
    /// 2 charge (the EchoDef), paid going in; coming out is free.
    /// </summary>
    public class CompProperties_AbilityKamuiWarp : CompProperties_AbilityEffect
    {
        public float exitMarkSeconds = 0.5f;
        public float cooldownAfterExitSeconds = 15f;

        public CompProperties_AbilityKamuiWarp()
        {
            compClass = typeof(CompAbilityEffect_KamuiWarp);
        }
    }

    public class CompAbilityEffect_KamuiWarp : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Gene_Involute gene = InvoluteUtility.GeneOf(parent.pawn);
            if (gene == null || !gene.Enter()) return;
            // The cooldown runs from the exit, not the entry.
            parent.ResetCooldown();
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = null;
            Gene_Involute gene = InvoluteUtility.GeneOf(parent.pawn);
            if (gene == null)
            {
                reason = "AG_KamuiNoGene".Translate(parent.pawn.LabelShortCap);
                return true;
            }
            return base.GizmoDisabled(out reason);
        }
    }

    /// <summary>Kamui: Warp's ability: outside, the usual cast; inside the dimension, the way out.</summary>
    public class Ability_KamuiWarp : Ability
    {
        public Ability_KamuiWarp() { }
        public Ability_KamuiWarp(Pawn pawn) : base(pawn) { }
        public Ability_KamuiWarp(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept) { }
        public Ability_KamuiWarp(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public Ability_KamuiWarp(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def) { }

        public override IEnumerable<Command> GetGizmos()
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene != null && gene.Inside)
            {
                yield return ExitCommand(gene);
                yield break;
            }
            foreach (Command command in base.GetGizmos()) yield return command;
        }

        private Command ExitCommand(Gene_Involute gene)
        {
            var command = new Command_Action
            {
                defaultLabel = "AG_KamuiWarpOutLabel".Translate(),
                defaultDesc = "AG_KamuiWarpOutDesc".Translate(pawn.LabelShortCap),
                icon = def.uiIcon,
                Order = 5.5f,
                action = () => TargetExit(gene, def.uiIcon),
            };
            if (gene.ExitPending) command.Disable("AG_KamuiWarpExiting".Translate(pawn.LabelShortCap));
            else if (gene.Intangible) command.Disable("AG_KamuiPhasedCannot".Translate(pawn.LabelShortCap));
            else if (pawn.Downed) command.Disable("CommandDisabledUnconscious".TranslateWithBackup("CommandCallRoyalAidUnconscious").Formatted(pawn));
            else if (gene.OutsideMap == null) command.Disable("AG_KamuiWarpNoWayOut".Translate());
            return command;
        }

        /// <summary>
        /// The camera goes to the map he left and the player picks a revealed, standable cell there. The
        /// targeter has no caster: Obito stays on the dimension's map while the player looks at the other one.
        /// </summary>
        public static void TargetExit(Gene_Involute gene, Texture2D icon)
        {
            Map map = gene.OutsideMap;
            if (map == null) return;
            IntVec3 look = map == gene.FromMap && gene.FromCell.IsValid ? gene.FromCell : map.Center;
            CameraJumper.TryJump(new GlobalTargetInfo(look, map));
            var parms = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
            };
            Find.Targeter.BeginTargeting(parms,
                target => gene.BeginExit(map, target.Cell),
                target => { if (ValidExit(map, target.Cell)) GenDraw.DrawTargetHighlight(target); },
                target => ValidExit(map, target.Cell),
                null, null, icon);
        }

        public static bool ValidExit(Map map, IntVec3 cell) =>
            Find.CurrentMap == map && cell.InBounds(map) && !cell.Fogged(map) && cell.Standable(map);
    }

    /// <summary>
    /// The map Obito left stays while he is in his dimension: a temporary map (a quest site, an ambush) with
    /// nobody of the player's left on it would otherwise be removed, taking his way back with it.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(MapParent), nameof(MapParent.CheckRemoveMapNow))]
    public static class Patch_MapParent_KamuiWarp
    {
        public static bool Prefix(MapParent __instance)
        {
            if (__instance == null || !__instance.HasMap) return true;
            Map map = __instance.Map;
            foreach (Map other in Find.Maps)
            {
                Pawn owner = other.GetComponent<MapComponent_KamuiDimension>()?.Owner;
                if (owner == null || owner.Dead || owner.MapHeld != other) continue;
                Gene_Involute gene = InvoluteUtility.GeneOf(owner);
                if (gene != null && gene.FromMap == map) return false;
            }
            return true;
        }
    }
}
