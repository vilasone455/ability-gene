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
    /// Kamehameha channels, Cancel alone while its Warp locks onto the landing cell; Throw and Cancel
    /// while a Spirit Bomb channels. Unlimited Blade Works adds its Release the same way.
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
            if (!(GameComponent_Goku.Instance?.For(ability.pawn) is KamehamehaCast cast)) yield break;
            int now = Find.TickManager.TicksGame;
            if (cast.Locking(now))
            {
                yield return Cancel(cast, "Let the ki go before the jump. No charge or cooldown is spent, Instant Transmission's included.");
                yield break;
            }
            if (!cast.Channelling) yield break;
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
                defaultDesc = "Instant Transmission with the ball charged: pick a cell to appear on, then a direction. Goku holds the ball while he locks onto the cell (Instant Transmission's channel time), then jumps and fires. Spends Instant Transmission's charge (" + cast.WarpCost.ToString("0") + ") and cooldown too.",
                icon = GokuDefOf.AG_GokuInstantTransmission.uiIcon,
                groupable = false,
                action = () => BeginWarp(cast),
            };
            string why = cast.WarpDisabled(now);
            if (why != null) warp.Disable(why);
            else if (cast.fireOrdered) warp.Disable("Firing at full charge.");
            yield return warp;

            yield return Cancel(cast, "Let the ki go. No charge or cooldown is spent.");
        }

        private static Command_Action Cancel(GokuCast cast, string desc) => new Command_Action
        {
            defaultLabel = "Cancel",
            defaultDesc = desc,
            icon = TexCommand.ClearPrioritizedWork,
            groupable = false,
            action = () => cast.Cancel(false),
        };

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
            },
            target => { if (target.IsValid) GokuTransmissionLock.DrawLock(pawn, target.Cell, null); },
            target => target.IsValid && cast.ValidWarpCell(target.Cell, out _),
            pawn, null, GokuDefOf.AG_GokuInstantTransmission.uiIcon, true,
            target => { if (target.IsValid && target.Cell.InBounds(pawn.Map)) Widgets.MouseAttachedLabel(GokuTransmissionLock.Label(pawn, cast.ChannelCell, target.Cell, null)); });
        }

        private static IEnumerable<Command> SpiritBomb(IEnumerable<Command> own, Ability ability)
        {
            foreach (Command c in own) yield return c;
            if (!(GameComponent_Goku.Instance?.For(ability.pawn) is SpiritBombCast cast) || !cast.Channelling) yield break;
            int now = Find.TickManager.TicksGame;
            CompProperties_SpiritBomb props = GokuBusy.Props<CompProperties_SpiritBomb>(ability.def);

            var throwIt = new Command_Action
            {
                defaultLabel = "Throw (power " + cast.PowerNow(now).ToString("0") + "/" + props.maxPower.ToString("0") + ", radius " + cast.RadiusNow(now).ToString("0.0") + ")",
                defaultDesc = "Throw the bomb at the target area now. It lands in " + props.flySeconds.ToString("0.0") + " s and deals "
                              + props.hits + " hits of " + cast.HitDamageNow(now).ToString("0") + " (" + (props.hits * cast.HitDamageNow(now)).ToString("0")
                              + " in all) to every hostile pawn within " + cast.RadiusNow(now).ToString("0.0") + " cells.\n\n"
                              + (cast.Full ? "The ball is full." : "Growing " + cast.rateNow.ToString("0.00") + " power per second; " + cast.LenderCount + " lending.")
                              + "\nGoku gives " + SpiritBombCast.RateOf(cast.caster, true).ToString("0.00") + " per second (health x Rest, Rest "
                              + GokuLifeEnergy.Rest(cast.caster).ToStringPercent() + "; nothing below " + props.stopRest.ToStringPercent() + ").",
                icon = ability.def.uiIcon,
                groupable = false,
                action = () => cast.throwOrdered = true,
            };
            if (!cast.CanThrow(now)) throwIt.Disable("Needs " + props.minChannelSeconds.ToString("0") + " s of channelling (" + cast.Channelled(now).ToString("0.0") + " s).");
            else if (cast.throwOrdered) throwIt.Disable("Throwing.");
            yield return throwIt;

            yield return Cancel(cast, "Let the energy go. No charge or cooldown is spent.");
        }
    }

    /// <summary>
    /// While Instant Transmission's destination is picked: the lock radius round the cell under the
    /// mouse and a highlight on the pawn Goku would lock onto. The destination comp's DrawHighlight
    /// is not virtual.
    /// </summary>
    [HarmonyPatch(typeof(CompAbilityEffect_WithDest), nameof(CompAbilityEffect_WithDest.DrawHighlight))]
    public static class Patch_CompAbilityEffect_WithDest_DrawHighlight_GokuLock
    {
        public static void Postfix(CompAbilityEffect_WithDest __instance, LocalTargetInfo target)
        {
            if (!(__instance is CompAbilityEffect_InstantTransmission it) || !target.IsValid) return;
            Pawn caster = it.parent.pawn;
            if (caster?.Map == null || !target.Cell.InBounds(caster.Map)) return;
            GokuTransmissionLock.DrawLock(caster, target.Cell, it.Passenger);
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
            CompProperties_SpiritBomb props = GokuBusy.Props<CompProperties_SpiritBomb>(GokuDefOf.AG_GokuSpiritBomb);
            var lend = new Command_Action
            {
                defaultLabel = "Lend energy (+" + SpiritBombCast.RateOf(pawn, false).ToString("0.00") + "/s)",
                defaultDesc = "Stand still with a hand up and give " + caster.LabelShortCap + "'s Spirit Bomb your ki: "
                              + SpiritBombCast.RateOf(pawn, false).ToString("0.00") + " power per second now (health "
                              + pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent() + " x Rest " + GokuLifeEnergy.Rest(pawn).ToStringPercent() + ").\n\n"
                              + "Each second costs " + props.restPerSecond.ToStringPercent() + " of the Rest bar, so the rate falls as you tire. "
                              + "Lending stops at " + props.stopRest.ToStringPercent() + " Rest, when the ball is full, at the throw, or on Stop lending.",
                icon = icon,
                groupable = true,
                action = () =>
                {
                    Job job = JobMaker.MakeJob(GokuDefOf.AG_GokuLend, caster);
                    job.playerForced = true;
                    pawn.jobs.TryTakeOrderedJob(job, pawn.Drafted ? JobTag.DraftedOrder : JobTag.Misc);
                },
            };
            if (bomb.Full) lend.Disable("The Spirit Bomb is full.");
            else if (SpiritBombCast.TooTired(pawn)) lend.Disable("Too tired: Rest " + GokuLifeEnergy.Rest(pawn).ToStringPercent() + ", lending needs " + props.stopRest.ToStringPercent() + ".");
            yield return lend;
        }
    }
}
