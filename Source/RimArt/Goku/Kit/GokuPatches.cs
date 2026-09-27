using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The buttons of a cast in progress, after the ability's own: Fire, Warp and Cancel while a
    /// Kamehameha channels; Throw and Cancel while a Spirit Bomb does. Unlimited Blade Works adds its
    /// Release the same way.
    /// </summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class Patch_GokuCommands
    {
        public static void Postfix(Ability __instance, ref IEnumerable<Command> __result)
        {
            if (__instance.def == GokuDefOf.AG_GokuKamehameha) __result = Kamehameha(__result, __instance);
            else if (__instance.def == GokuDefOf.AG_GokuSpiritBomb) __result = SpiritBomb(__result, __instance);
        }

        private static IEnumerable<Command> Kamehameha(IEnumerable<Command> own, Ability ability)
        {
            foreach (Command c in own) yield return c;
            if (!(GameComponent_Goku.Instance?.For(ability.pawn) is KamehamehaCast cast) || !cast.Channelling) yield break;
            int now = Find.TickManager.TicksGame;
            Texture2D icon = ability.def.uiIcon;
            bool full = cast.FullCharge(now);

            var fire = new Command_Action
            {
                defaultLabel = full ? "Fire" : "Fire (" + cast.ChargeLeft(now).ToString("0.0") + " s)",
                defaultDesc = "Let the beam go along the aim. Pressed before full charge, it fires the moment the charge is full.",
                icon = icon,
                groupable = false,
                action = () => cast.fireOrdered = true,
            };
            if (cast.fireOrdered) fire.Disable("Firing at full charge.");
            yield return fire;

            var warp = new Command_Action
            {
                defaultLabel = "Warp Kamehameha",
                defaultDesc = "Instant Transmission with the ball charged: pick a cell to appear on, then a direction, and Goku jumps and fires at once. Spends Instant Transmission's charge (" + cast.WarpCost.ToString("0") + ") and cooldown too.",
                icon = GokuDefOf.AG_GokuInstantTransmission.uiIcon,
                groupable = false,
                action = () => BeginWarp(cast),
            };
            string why = cast.WarpDisabled(now);
            if (why != null) warp.Disable(why);
            else if (cast.fireOrdered) warp.Disable("Firing at full charge.");
            yield return warp;

            yield return new Command_Action
            {
                defaultLabel = "Cancel",
                defaultDesc = "Let the ki go. No charge or cooldown is spent.",
                icon = TexCommand.ClearPrioritizedWork,
                groupable = false,
                action = () => cast.Cancel(false),
            };
        }

        /// <summary>
        /// Two picks: the cell to appear on, then the direction. The targeter clears itself after a
        /// click's action runs, so the second pick is started on the next frame.
        /// </summary>
        private static void BeginWarp(KamehamehaCast cast)
        {
            Pawn pawn = cast.caster;
            CompProperties_Kamehameha props = GokuBusy.Props<CompProperties_Kamehameha>(GokuDefOf.AG_GokuKamehameha);
            var landing = new TargetingParameters
            {
                canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false, canTargetItems = false,
                validator = t => cast.ValidWarpCell(t.Cell, out _),
            };
            Find.Targeter.BeginTargeting(landing, cell =>
            {
                IntVec3 at = cell.Cell;
                var aim = new TargetingParameters
                {
                    canTargetLocations = true, canTargetPawns = true, canTargetAnimals = true, canTargetMechs = true, canTargetBuildings = true, canTargetItems = false,
                    validator = t => t.Cell != at && t.Cell.InBounds(pawn.Map),
                };
                LongEventHandler.ExecuteWhenFinished(() => Find.Targeter.BeginTargeting(aim,
                    target => cast.Warp(at, target.Cell),
                    target =>
                    {
                        if (!target.IsValid || target.Cell == at) return;
                        Vector2 toward = KamehamehaLane.Toward(at, target.Cell);
                        float stop = KamehamehaLane.Stop(at, toward, props.length, pawn.Map, out _, out _);
                        GenDraw.DrawFieldEdges(KamehamehaLane.Cells(at, toward, stop, props.width, pawn.Map));
                        GenDraw.DrawRadiusRing(KamehamehaLane.EndCell(at, toward, stop), props.blastRadius);
                    },
                    target => target.IsValid && target.Cell != at,
                    pawn, null, GokuDefOf.AG_GokuKamehameha.uiIcon, true, null,
                    target => GenDraw.DrawTargetHighlight(at)));
            }, pawn, null, GokuDefOf.AG_GokuInstantTransmission.uiIcon);
        }

        private static IEnumerable<Command> SpiritBomb(IEnumerable<Command> own, Ability ability)
        {
            foreach (Command c in own) yield return c;
            if (!(GameComponent_Goku.Instance?.For(ability.pawn) is SpiritBombCast cast) || !cast.Channelling) yield break;
            int now = Find.TickManager.TicksGame;
            CompProperties_SpiritBomb props = GokuBusy.Props<CompProperties_SpiritBomb>(ability.def);

            var throwIt = new Command_Action
            {
                defaultLabel = "Throw (power " + cast.PowerNow(now).ToString("0") + ", radius " + cast.RadiusNow(now).ToString("0.0") + ")",
                defaultDesc = "Throw the bomb at the target area now. It lands in " + props.flySeconds.ToString("0.0") + " s and deals "
                              + cast.DamageNow(now).ToString("0") + " to every hostile pawn within " + cast.RadiusNow(now).ToString("0.0") + " cells. "
                              + cast.LenderCount + " lending.",
                icon = ability.def.uiIcon,
                groupable = false,
                action = () => cast.throwOrdered = true,
            };
            if (!cast.CanThrow(now)) throwIt.Disable("Needs " + props.minChannelSeconds.ToString("0") + " s of channelling (" + cast.Channelled(now).ToString("0.0") + " s).");
            else if (cast.throwOrdered) throwIt.Disable("Throwing.");
            yield return throwIt;

            yield return new Command_Action
            {
                defaultLabel = "Cancel",
                defaultDesc = "Let the energy go. No charge or cooldown is spent.",
                icon = TexCommand.ClearPrioritizedWork,
                groupable = false,
                action = () => cast.Cancel(false),
            };
        }
    }

    /// <summary>
    /// Lend energy: every other colonist on the map gets the button while a Spirit Bomb is channelled
    /// there, and Stop lending while it lends.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GokuLendGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result) yield return gizmo;
            Pawn pawn = __instance;
            if (!pawn.IsColonistPlayerControlled || !pawn.Spawned || pawn.Downed || pawn.Dead || pawn.InMentalState) yield break;
            SpiritBombCast bomb = GameComponent_Goku.Instance?.SpiritBombOn(pawn.Map);
            if (bomb == null || bomb.caster == pawn) yield break;
            Pawn caster = bomb.caster;
            Texture2D icon = GokuDefOf.AG_GokuSpiritBomb.uiIcon;
            if (SpiritBombCast.IsLending(pawn, caster))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Stop lending",
                    defaultDesc = "Lower the hand and go back to what you were doing.",
                    icon = TexCommand.ClearPrioritizedWork,
                    groupable = true,
                    action = () => { if (pawn.CurJobDef == GokuDefOf.AG_GokuLend) pawn.jobs.EndCurrentJob(JobCondition.InterruptForced); },
                };
                yield break;
            }
            yield return new Command_Action
            {
                defaultLabel = "Lend energy",
                defaultDesc = "Stand still with a hand up and give " + caster.LabelShortCap + "'s Spirit Bomb 1 power per second until the throw, or until Stop lending.",
                icon = icon,
                groupable = true,
                action = () =>
                {
                    Job job = JobMaker.MakeJob(GokuDefOf.AG_GokuLend, caster);
                    job.playerForced = true;
                    pawn.jobs.TryTakeOrderedJob(job, pawn.Drafted ? JobTag.DraftedOrder : JobTag.Misc);
                },
            };
        }
    }
}
