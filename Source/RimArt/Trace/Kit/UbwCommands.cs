using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The buttons of Unlimited Blade Works' commands: on the caster's ability while the world stands (Full Open, or its
    /// Release and Cancel while it charges; Pin; Draw; Intercept), added after Close by <see cref="Patch_UbwCommands"/>,
    /// and Arm on every other player colonist inside (<see cref="Patch_Pawn_GetGizmos_UbwArm"/>). They exist only while
    /// <see cref="UbwCast.CommandsOpen"/>: the world stands, its close has not begun, and the caster is in it. None costs
    /// charge; each sword taken out of the ground takes swordCostSeconds off the world, shown on Close.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class UbwCommands
    {
        private static readonly Texture2D TraceIcon = ContentFinder<Texture2D>.Get("RimArt/Trace/IconTraceOn");

        static UbwCommands()
        {
            // The previews play with the def's numbers.
            UbwCommandNumbers.Provider = () =>
            {
                UbwRules r = UbwRules.Of;
                return new UbwCommandNumbers
                {
                    fullOpenSwordSeconds = r.fullOpenSwordSeconds, fullOpenVolleySeconds = r.fullOpenVolleySeconds, fullOpenSpeed = r.fullOpenSpeed,
                    pinSpeed = r.pinSpeed, pinSwords = r.pinSwords, drawSpeed = r.drawSpeed, interceptRiseSeconds = r.interceptRiseSeconds,
                    interceptSpeed = r.interceptSpeed, interceptClearOfGun = r.interceptClearOfGun, interceptShortOfTarget = r.interceptShortOfTarget,
                };
            };
        }

        private static string Cost(UbwRules rules, int swords) => (swords * rules.swordCostSeconds).ToString("0.#") + " s of the world";

        internal static IEnumerable<Command> Buttons(UbwCast cast, Ability ability)
        {
            if (!cast.CommandsOpen) yield break;
            UbwRules rules = UbwRules.Of;
            Texture2D icon = ability.def.uiIcon;
            Pawn caster = cast.caster;

            if (cast.fullOpen.Charging)
            {
                int n = cast.fullOpen.hovering.Count;
                yield return new Command_Action
                {
                    defaultLabel = "Release (" + n + (n == 1 ? " sword)" : " swords)"),
                    defaultDesc = "Fire every hovering sword at " + cast.fullOpen.target.LabelShort + " within " + rules.fullOpenVolleySeconds.ToString("0.##")
                                  + " s. Each hits as its own weapon and sticks in the ground past the target, where it stands again.",
                    icon = icon,
                    groupable = false,
                    action = () => cast.fullOpen.Release(cast, Find.TickManager.TicksGame),
                };
                yield return new Command_Action
                {
                    defaultLabel = "Cancel",
                    defaultDesc = "Drop the hovering swords back into their holes. The time they cost stays spent.",
                    icon = TexCommand.ClearPrioritizedWork,
                    groupable = false,
                    action = () => cast.fullOpen.Cancel(cast, Find.TickManager.TicksGame),
                };
            }
            else
            {
                yield return new Command_Target
                {
                    defaultLabel = "Full Open",
                    defaultDesc = "Pick a pawn. " + caster.LabelShort + " stands still while one sword every " + rules.fullOpenSwordSeconds.ToString("0.##")
                                  + " s rises from the ground nearest it and hovers aimed at it, up to " + rules.fullOpenSwords + ". Release fires them all; Cancel or a move order drops them back. Each sword costs "
                                  + Cost(rules, 1) + ". No cooldown.",
                    icon = icon,
                    groupable = false,
                    targetingParams = PawnIn(cast),
                    action = t => { if (t.Pawn != null) cast.fullOpen.Begin(cast, t.Pawn, Find.TickManager.TicksGame); },
                };
            }

            yield return new Command_Target
            {
                defaultLabel = "Pin",
                defaultDesc = "Pick a pawn. The " + rules.pinSwords + " swords nearest it pin it to the ground for " + rules.pinSeconds.ToString("0.#") + " s, "
                              + rules.pinDamage.ToString("0.#") + " Cut each. It counts as downed and can be captured once everyone is home. Costs " + Cost(rules, rules.pinSwords) + ".",
                icon = TexCommand.Attack,
                groupable = false,
                targetingParams = PawnIn(cast),
                action = t => { if (t.Pawn != null) cast.pins.Begin(cast, t.Pawn, Find.TickManager.TicksGame); },
            };

            yield return new Command_Target
            {
                defaultLabel = "Draw",
                defaultDesc = "Pick a sword (the nearest within " + rules.drawPickRadius.ToString("0.#") + " cells of the click). It flies to " + caster.LabelShort + " at "
                              + rules.drawSpeed.ToString("0") + " cells a second, hits every enemy it passes once as its own weapon, and is caught as a traced copy. Costs " + Cost(rules, 1) + ".",
                icon = TraceIcon,
                groupable = false,
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false, canTargetItems = false, mapObjectTargetsMustBeAutoAttackable = false,
                    validator = t => t.IsValid && UbwDraw.PickAt(cast, t.Cell) != null,
                },
                action = t =>
                {
                    UbwSword sw = UbwDraw.PickAt(cast, t.Cell);
                    if (sw != null) cast.draws.Begin(cast, sw, Find.TickManager.TicksGame);
                },
                onUpdate = t => UbwDraw.Highlight(cast, t),
            };

            yield return new Command_Toggle
            {
                defaultLabel = "Intercept",
                defaultDesc = "While on, every enemy shot inside the world that a sword can reach first is met by the sword nearest its path and stopped; an explosive bursts where it is met. Each stopped shot costs "
                              + Cost(rules, 1) + ". A shot no sword can reach in time is not stopped and costs nothing.",
                icon = icon,
                groupable = false,
                isActive = () => cast.intercept,
                toggleAction = () => cast.intercept = !cast.intercept,
            };
        }

        /// <summary>Any pawn standing in the cast's world other than its caster.</summary>
        private static TargetingParameters PawnIn(UbwCast cast) => new TargetingParameters
        {
            canTargetPawns = true, canTargetLocations = false, canTargetBuildings = false, canTargetItems = false, mapObjectTargetsMustBeAutoAttackable = false,
            validator = t => t.Thing is Pawn p && p != cast.caster && cast.InWorld(p),
        };

        /// <summary>The Arm button for <paramref name="pawn"/>, or null when it has none: a player colonist inside a standing world that is not its caster's own.</summary>
        internal static Command ArmButton(Pawn pawn)
        {
            if (!pawn.IsColonistPlayerControlled || !pawn.Spawned) return null;
            if (pawn.Map.GetComponent<MapComponent_UnlimitedBladeWorks>()?.IsWorld != true) return null;
            UbwCast cast = GameComponent_UnlimitedBladeWorks.Instance?.ForWorld(pawn.Map);
            if (cast == null || cast.caster == pawn || !cast.CommandsOpen) return null;
            var command = new Command_Action
            {
                defaultLabel = "Arm",
                defaultDesc = "The sword nearest " + pawn.LabelShort + " flies to their hand as a traced copy of its weapon. Their own weapon goes to their inventory. The copy breaks when the world closes. Costs "
                              + Cost(UbwRules.Of, 1) + ".",
                icon = TraceIcon,
                groupable = false,
                action = () => cast.arms.Begin(cast, pawn, Find.TickManager.TicksGame),
            };
            string why = cast.arms.Refusal(cast, pawn);
            if (why != null) command.Disable(why);
            return command;
        }
    }
}
