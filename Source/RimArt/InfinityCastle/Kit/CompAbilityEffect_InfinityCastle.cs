using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>Infinity Castle's balance numbers, on the AG_Nakime_InfinityCastle AbilityDef. Range, warm-up and cooldown are the def's own.</summary>
    public class CompProperties_AbilityInfinityCastle : CompProperties_AbilityEffect
    {
        /// <summary>Hostile pawns within this many cells of the target cell are taken.</summary>
        public float radius = 5.9f;

        /// <summary>At most this many pawns, nearest first.</summary>
        public int maxPawns = 8;

        /// <summary>At most this much body size in all (a human is 1, a thrumbo 4).</summary>
        public float maxBodySize = 8f;

        /// <summary>The castle stands this long, in game seconds, from the moment everyone is in.</summary>
        public float castleSeconds = 60f;

        public CompProperties_AbilityInfinityCastle()
        {
            compClass = typeof(CompAbilityEffect_InfinityCastle);
        }
    }

    /// <summary>
    /// Infinity Castle, the ability: the strum at the end of the warm-up begins an
    /// <see cref="InfinityCastleCast"/>, which does everything after that. The ability comes from the Nakime
    /// Echo, which takes its charge before this runs (EchoCastPayment); if nobody is left to take or the
    /// castle cannot be made, the cooldown and the charge come back. Its commands while the castle stands
    /// are added to its gizmo by <see cref="Patch_InfinityCastleCommands"/>.
    /// </summary>
    public class CompAbilityEffect_InfinityCastle : CompAbilityEffect
    {
        public new CompProperties_AbilityInfinityCastle Props => (CompProperties_AbilityInfinityCastle)props;

        // The last answer of CandidatesAt: the targeting cursor asks Valid, the preview and the label each frame.
        private List<Pawn> candidates;
        private int candidatesFrame = -1, candidatesTick = -1;
        private IntVec3 candidatesCell;
        private Map candidatesMap;

        /// <summary>
        /// <see cref="InfinityCastleCast.Candidates"/> for the caster at <paramref name="cell"/>, worked out once per
        /// frame and game tick. The list is shared: read it, do not keep or change it.
        /// </summary>
        public List<Pawn> CandidatesAt(IntVec3 cell)
        {
            Pawn pawn = parent.pawn;
            int frame = Time.frameCount, tick = Find.TickManager.TicksGame;
            if (candidates == null || frame != candidatesFrame || tick != candidatesTick || cell != candidatesCell || pawn.Map != candidatesMap)
            {
                candidates = InfinityCastleCast.Candidates(pawn, pawn.Map, cell, Props);
                candidatesFrame = frame;
                candidatesTick = tick;
                candidatesCell = cell;
                candidatesMap = pawn.Map;
            }
            return candidates;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            float paid = EchoUtility.ManifestedWith(pawn, parent.def)?.def.CastCost(parent.def) ?? 0f;
            float warmup = parent.verb?.verbProps?.warmupTime ?? 1f;
            InfinityCastleCast cast = InfinityCastleCast.Begin(pawn, target.Cell, warmup, paid, Props, out string why);
            if (cast != null)
            {
                GameComponent_InfinityCastle.Instance?.Add(cast);
                return;
            }
            parent.ResetCooldown();
            GameComponent_Echoes.Get?.Refund(paid);
            Messages.Message(why, pawn, MessageTypeDefOf.RejectInput, false);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = parent.pawn;
            if (pawn.Map == null || pawn.Map.IsPocketMap)
            {
                if (throwMessages) Messages.Message("Infinity Castle cannot be opened inside a pocket map.", pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (target.IsValid && CandidatesAt(target.Cell).Count == 0)
            {
                if (throwMessages) Messages.Message("No hostile within " + Props.radius.ToString("0.#") + " cells of that cell.", pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn pawn = parent.pawn;
            InfinityCastleCast cast = GameComponent_InfinityCastle.Instance?.For(pawn);
            if (cast != null)
            {
                reason = cast.Standing ? "The castle stands." : "Opening the castle.";
                return true;
            }
            if (pawn.MapHeld != null && pawn.MapHeld.IsPocketMap)
            {
                reason = "Inside a pocket map.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (!target.IsValid || pawn.Map == null) return;
            GenDraw.DrawRadiusRing(target.Cell, Props.radius);
            foreach (Pawn p in CandidatesAt(target.Cell)) GenDraw.DrawTargetHighlight(p);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (!target.IsValid || pawn.Map == null) return null;
            int n = CandidatesAt(target.Cell).Count;
            return n == 0 ? "Nobody to take" : n + " taken";
        }
    }

    /// <summary>
    /// The castle's commands, after the ability's own button while its castle stands and the carrier is in
    /// it: Shift, Drop, Seal / Open, Crush, Summon and Release. Each is one strum of the biwa; the castle
    /// keeps them 1.5 s apart (<see cref="MapComponent_InfinityCastle"/> has the rules, and says why when
    /// one is refused).
    /// </summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class Patch_InfinityCastleCommands
    {
        public static void Postfix(Ability __instance, ref IEnumerable<Command> __result)
        {
            if (__instance.def == InfinityCastleDefOf.AG_Nakime_InfinityCastle) __result = With(__result, __instance);
        }

        private static IEnumerable<Command> With(IEnumerable<Command> own, Ability ability)
        {
            foreach (Command c in own) yield return c;
            InfinityCastleCast cast = GameComponent_InfinityCastle.Instance?.For(ability.pawn);
            if (cast == null || !cast.Standing || cast.releasing || ability.pawn.Map != cast.castle) yield break;
            MapComponent_InfinityCastle castle = cast.Component;
            if (castle == null || !castle.Open) yield break;
            foreach (Command c in InfinityCastleCommands.For(cast, castle, ability.pawn)) yield return c;
        }
    }

    /// <summary>The command buttons and their targeting.</summary>
    [StaticConstructorOnStartup]
    internal static class InfinityCastleCommands
    {
        private static readonly Texture2D IconShift = ContentFinder<Texture2D>.Get("RimArt/InfinityCastle/IconShift");
        private static readonly Texture2D IconDrop = ContentFinder<Texture2D>.Get("RimArt/InfinityCastle/IconDrop");
        private static readonly Texture2D IconSeal = ContentFinder<Texture2D>.Get("RimArt/InfinityCastle/IconSeal");
        private static readonly Texture2D IconCrush = ContentFinder<Texture2D>.Get("RimArt/InfinityCastle/IconCrush");
        private static readonly Texture2D IconSummon = ContentFinder<Texture2D>.Get("RimArt/InfinityCastle/IconSummon");
        private static readonly Texture2D IconRelease = ContentFinder<Texture2D>.Get("RimArt/InfinityCastle/IconRelease");

        public static IEnumerable<Command> For(InfinityCastleCast cast, MapComponent_InfinityCastle castle, Pawn nakime)
        {
            string strum = castle.StrumWait > 0f ? "The last strum is still sounding." : null;

            yield return Button("Shift", "Pick a room, then a direction. It slides straight until its wall touches another room, at most " +
                InfinityCastleRules.Of.shiftMaxCells + " cells, with everyone and everything in it. Its doorways close as the rooms part; a new one opens where it now touches a room. Anyone standing in a doorway that breaks falls and comes up in a random room.",
                IconShift, strum, () => TargetRoom(nakime, castle, IconShift, cell => ShiftMenu(castle, cell)));

            yield return Button("Drop", "Pick a pawn in the castle, then a room. A door opens in the floor under the pawn; it comes up through a door in that room's floor. No damage.",
                IconDrop, strum, () => TargetPawn(nakime, castle, pawn => TargetRoom(nakime, castle, IconDrop, cell => Say(castle.TryDrop(pawn, cell, out string why), why))));

            yield return Button("Seal / Open", "Pick a doorway (either of its two door cells). An open doorway is barred shut and nobody passes; a sealed one opens. The biwa room's doorways cannot be sealed.",
                IconSeal, strum, () => TargetRoom(nakime, castle, IconSeal, cell => SealOrOpen(castle, cell)));

            InfinityCastleRules rules = InfinityCastleRules.Of;
            string crushWait = castle.CrushWait > 0f ? "Crush is not ready: " + Mathf.CeilToInt(castle.CrushWait) + " s." : strum;
            yield return Button("Crush", "Pick a room of " + rules.crushMinSize + " x " + rules.crushMinSize + " or more (not the biwa room). Its walls slam " + rules.crushBand +
                " cells in and draw back: " + rules.crushDamage.ToString("0") + " blunt and a " + rules.crushStunSeconds.ToString("0.#") + " s stun to everyone near the walls, own pawns included. Cooldown " +
                rules.crushCooldownSeconds.ToString("0") + " s.",
                IconCrush, crushWait, () => TargetRoom(nakime, castle, IconCrush, cell => Say(castle.TryCrush(cell, out string why), why)));

            yield return Button("Summon", "Pick a room, then a colonist on the home map. The colonist comes up through a door in that room's floor and goes home with everyone else.",
                IconSummon, strum ?? (cast.SummonChoices().Any() ? null : "No colonist on the home map can come."),
                () => TargetRoom(nakime, castle, IconSummon, cell => SummonMenu(cast, cell)));

            yield return Button("Release (" + cast.SecondsLeft(Find.TickManager.TicksGame).ToString("0") + " s)",
                "End the castle now. Everyone goes back to where they came from; corpses and items come up round the target cell.",
                IconRelease, null, () => cast.releaseOrdered = true);
        }

        private static Command_Action Button(string label, string desc, Texture2D icon, string disabled, System.Action action)
        {
            var command = new Command_Action { defaultLabel = label, defaultDesc = desc, icon = icon, groupable = false, action = action };
            if (disabled != null) command.Disable(disabled);
            return command;
        }

        private static void Say(bool done, string why)
        {
            if (!done && why != null) Messages.Message(why, MessageTypeDefOf.RejectInput, false);
        }

        /// <summary>Click a cell in the castle; the room under the mouse is outlined while choosing.</summary>
        private static void TargetRoom(Pawn nakime, MapComponent_InfinityCastle castle, Texture2D icon, System.Action<IntVec3> then)
        {
            var parms = new TargetingParameters { canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false, mapObjectTargetsMustBeAutoAttackable = false };
            Find.Targeter.BeginTargeting(parms, t => then(t.Cell), null, t => t.IsValid && t.Cell.InBounds(castle.map), nakime, null, icon, true, null,
                t => Outline(castle, t));
        }

        private static void TargetPawn(Pawn nakime, MapComponent_InfinityCastle castle, System.Action<Pawn> then)
        {
            var parms = new TargetingParameters
            {
                canTargetPawns = true, canTargetLocations = false, canTargetBuildings = false, mapObjectTargetsMustBeAutoAttackable = false,
                validator = t => t.Thing is Pawn p && p != nakime && p.Map == castle.map,
            };
            Find.Targeter.BeginTargeting(parms, t => { if (t.Thing is Pawn p) then(p); }, nakime, null, IconDrop);
        }

        /// <summary>The room under the mouse, outlined in the strum's pale colour.</summary>
        private static void Outline(MapComponent_InfinityCastle castle, LocalTargetInfo target)
        {
            if (!target.IsValid || Find.CurrentMap != castle.map) return;
            CastleRoom room = castle.RoomAt(target.Cell);
            if (room == null) return;
            Vector2 centre = CastleRoomGraphics.CentreOf(Vector2.zero, room);
            VfxDraw.Begin(centre);
            CastleEffectGraphics.Outline(room, centre, 0.5f, CastleLayers.Pocket.Wall);
        }

        private static void ShiftMenu(MapComponent_InfinityCastle castle, IntVec3 cell)
        {
            CastleRoom room = castle.RoomAt(cell);
            if (room == null) { Say(false, "No room there."); return; }
            if (room.Kind == CastleKind.Biwa) { Say(false, "The biwa room does not move."); return; }
            var options = new List<FloatMenuOption>();
            foreach (var (name, dx, dz) in new[] { ("north", 0, 1), ("east", 1, 0), ("south", 0, -1), ("west", -1, 0) })
            {
                int d = castle.ShiftDistance(cell, dx, dz);
                options.Add(d > 0
                    ? new FloatMenuOption("Shift " + name + " (" + d + " cells)", () => Say(castle.TryShift(cell, dx, dz, out string why), why))
                    : new FloatMenuOption("Shift " + name + ": blocked", null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void SealOrOpen(MapComponent_InfinityCastle castle, IntVec3 cell)
        {
            bool sealedNow = castle.SealedAt(cell, out bool doorway);
            if (!doorway) { Say(false, "No doorway there: click one of its two door cells."); return; }
            string why;
            Say(sealedNow ? castle.TryOpen(cell, out why) : castle.TrySeal(cell, out why), why);
        }

        private static void SummonMenu(InfinityCastleCast cast, IntVec3 cell)
        {
            if (cast.Component?.RoomAt(cell) == null) { Say(false, "No room there."); return; }
            List<FloatMenuOption> options = cast.SummonChoices()
                .Select(p => new FloatMenuOption("Summon " + p.LabelShortCap, () => Say(cast.Summon(p, cell, out string why), why)))
                .ToList();
            if (options.Count == 0) { Say(false, "No colonist on the home map can come."); return; }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    /// <summary>
    /// The carrier on the dais, playing the biwa while her castle stands: she does not move or attack, and
    /// orders cannot interrupt her (<see cref="PlayerInterruptable"/>). The cast starts it at the move in and
    /// again if something else ended it; it ends by itself when the castle is released.
    /// </summary>
    public class JobDriver_CastlePlay : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override bool PlayerInterruptable => false;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil play = ToilMaker.MakeToil("CastlePlay");
            play.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.Rotation = Rot4.South;
            };
            play.tickIntervalAction = delta =>
            {
                if (GameComponent_InfinityCastle.Instance?.PlayingFor(pawn) != true) EndJobWith(JobCondition.Succeeded);
            };
            play.handlingFacing = true;
            play.defaultCompleteMode = ToilCompleteMode.Never;
            yield return play;
        }
    }
}
