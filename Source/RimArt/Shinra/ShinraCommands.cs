using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class Patch_ShinraCommands
    {
        public static void Postfix(Ability __instance, ref IEnumerable<Command> __result)
        {
            if (__instance.def.defName == "AG_ShinraTensei") __result = Commands(__instance);
        }
        private static IEnumerable<Command> Commands(Ability ability)
        {
            Pawn pawn = ability.pawn;
            // Hediff abilities are separate instances. Only the first source supplies controls.
            if (pawn.abilities.AllAbilitiesForReading.FirstOrDefault(a => a.def == ability.def) != ability) yield break;
            yield return new Command_ShinraTensei(GameComponent_Shinra.Instance.For(pawn));
        }
    }

    /// <summary>
    /// Shinra Tensei's one button (<see cref="Command_TapHold"/>): a tap is the quick version; a hold charges, the
    /// arms rising at each size, and letting go releases the size reached. The bar fills over the full charge with a
    /// line at each size. Auto-release is on the right-click menu.
    /// </summary>
    public sealed class Command_ShinraTensei : Command_TapHold
    {
        private readonly ShinraPawnState s;

        public Command_ShinraTensei(ShinraPawnState state)
        {
            s = state;
            ShinraTuning t = ShinraTuning.Get;
            holder = state;
            icon = ContentFinder<Texture2D>.Get("RimArt/Shinra/IconPush");
            bool holding = s.active && !s.charge.releasing;
            defaultLabel = holding ? "Let go: " + SizeLabel(s.charge.SizeNow) : "Shinra Tensei";
            defaultDesc = Describe(t, s.pawn);
            // A tap on a hold that is already charging (one restored from a save) lets it go.
            tap = () => { if (s.active) s.Release(); else GameComponent_Shinra.Instance.Start(s.pawn, true); };
            charge = () => { if (!s.active) GameComponent_Shinra.Instance.Start(s.pawn, false); };
            release = s.Release;
            cancel = () => { if (s.active && !s.charge.releasing) s.Cancel(); };
            fill = () => s.active && !s.charge.tap && !s.charge.releasing ? Mathf.Max(0.02f, s.charge.Power) : 0f;
            marks = t.sizes.Select(size => size.chargeSeconds / t.fullChargeSeconds).Where(m => m < 1f).ToArray();
            if (!holding && s.CannotStart() is string why) Disable(why);
        }

        private static string SizeLabel(int size) =>
            (size < 0 ? ShinraTuning.Get.tapRadius : ShinraTuning.Get.sizes[size].radius).ToString("0.#") + " cells";

        private static string Describe(ShinraTuning t, Pawn pawn)
        {
            float echo = PainKit.Cost(pawn, PainDefOf.AG_ShinraTensei);
            string sizes = string.Join(", ", t.sizes.Select(z =>
                $"after {z.chargeSeconds:0.#} s {z.radius:0.#} cells ({z.cooldownSeconds:0} s cooldown)"));
            return $"Tap: a {t.tapRadius:0.#}-cell push. Pushes {t.tapPush:0.#} cells, {t.tapWallDamage:0} blunt against a wall, "
                + $"turns shots up to {t.tapShotLimit:0} damage for {t.tapDeflectSeconds:0.##} s. {t.tapCooldownSeconds:0} s cooldown"
                + (echo > 0f ? $", {t.tapEchoCost:0} Echo charge." : ".")
                + $"\n\nHold: charge for up to {t.fullChargeSeconds:0.#} s and let go: {sizes}. Let go sooner for the tap version. "
                + $"Pushes {t.pushLow:0.#}-{t.pushHigh:0.#} cells and {t.wallDamageLow:0}-{t.wallDamageHigh:0} blunt against a wall by charge, "
                + $"turns shots up to {t.shotLimitLow:0}-{t.shotLimitHigh:0} damage for {t.deflectSeconds:0.##} s, explosives only at full charge"
                + (echo > 0f ? $". {echo:0} Echo charge." : ".")
                + "\n\nRight-click or Esc while holding cancels, as does a move order. Pushed pawns (bigger bodies less far) include allies. "
                + "Shinra Tensei and Banshō Ten'in share a 5-second gap, and both wait while a Chibaku Tensei ball holds.";
        }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                yield return new FloatMenuOption((s.autoRelease ? "Turn off" : "Turn on") + " auto-release: at full charge, "
                    + "let go when a shot it can turn is about to hit", () => s.autoRelease = !s.autoRelease);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class Patch_ShinraOrderedJob
    {
        public static bool Prefix(Pawn ___pawn, Job job, ref bool __result)
        {
            var s = GameComponent_Shinra.Instance.States.FirstOrDefault(s => s.pawn == ___pawn && s.active);
            if (s == null) return true;
            if (job.def == JobDefOf.Goto) { s.Cancel(); return true; }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new[] { typeof(LocalTargetInfo),
        typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Patch_ShinraBlockAttacks
    {
        public static bool Prefix(Verb __instance, ref bool __result)
        {
            if (!GameComponent_Shinra.Instance.States.Any(s => s.active && s.pawn == __instance.CasterPawn)) return true;
            __result = false;
            return false;
        }
    }
}
