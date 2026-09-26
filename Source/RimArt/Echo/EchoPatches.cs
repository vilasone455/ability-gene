using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimArt
{
    public static class EchoDeeds
    {
        public static int KillsWith(Pawn pawn, Trial_KillsWith trial)
        {
            PawnDeeds deeds = GameComponent_Echoes.Get?.DeedsFor(pawn, false);
            if (deeds == null) return 0;
            int total = 0;
            foreach (KeyValuePair<ThingDef, int> entry in deeds.killsByWeapon)
                if (trial.Counts(entry.Key)) total += entry.Value;
            return total;
        }
    }

    /// <summary>Counts the weapon behind each kill by a colonist, which vanilla's records do not keep.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    static class Patch_Pawn_Kill_EchoDeeds
    {
        static void Prefix(Pawn __instance, DamageInfo? dinfo, out bool __state) => __state = __instance.Dead;

        static void Postfix(Pawn __instance, DamageInfo? dinfo, bool __state)
        {
            if (__state || !__instance.Dead || dinfo == null) return;
            if (!(dinfo.Value.Instigator is Pawn killer) || !killer.IsColonist) return;
            ThingDef weapon = dinfo.Value.Weapon;
            if (weapon == null || weapon == ThingDefOf.Human) return;
            PawnDeeds deeds = GameComponent_Echoes.Get?.DeedsFor(killer, true);
            if (deeds == null) return;
            deeds.killsByWeapon.TryGetValue(weapon, out int count);
            deeds.killsByWeapon[weapon] = count + 1;
        }
    }

    /// <summary>An Echo ability with a cast cost is disabled while the pool cannot pay it.</summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.GizmoDisabled))]
    static class Patch_Ability_GizmoDisabled_Echo
    {
        static void Postfix(Ability __instance, ref bool __result, ref string reason)
        {
            if (__result) return;
            EchoRecord record = EchoUtility.ManifestedWith(__instance.pawn, __instance.def);
            if (record == null) return;
            float cost = record.def.CastCost(__instance.def);
            if (cost <= 0f || GameComponent_Echoes.Get.charge >= cost) return;
            __result = true;
            reason = "AG_EchoCastNoCharge".Translate(cost.ToString("0"), GameComponent_Echoes.Get.charge.ToString("0"));
        }
    }

    /// <summary>
    /// Takes the cast cost when the ability fires, not when it is ordered: the charge might have
    /// been spent by another Host in between, and a cast that is cancelled costs nothing.
    /// </summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.Activate), typeof(LocalTargetInfo), typeof(LocalTargetInfo))]
    static class Patch_Ability_Activate_Echo
    {
        static bool Prefix(Ability __instance, ref bool __result) => EchoCastPayment.Pay(__instance, ref __result);
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.Activate), typeof(GlobalTargetInfo))]
    static class Patch_Ability_ActivateGlobal_Echo
    {
        static bool Prefix(Ability __instance, ref bool __result) => EchoCastPayment.Pay(__instance, ref __result);
    }

    static class EchoCastPayment
    {
        /// <returns>False to skip the cast because the pool cannot pay.</returns>
        public static bool Pay(Ability ability, ref bool result)
        {
            EchoRecord record = EchoUtility.ManifestedWith(ability.pawn, ability.def);
            if (record == null) return true;
            float cost = record.def.CastCost(ability.def);
            if (GameComponent_Echoes.Get.TrySpend(cost)) return true;
            Messages.Message("AG_EchoCastNoCharge".Translate(cost.ToString("0"), GameComponent_Echoes.Get.charge.ToString("0")),
                ability.pawn, MessageTypeDefOf.RejectInput, false);
            result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    static class Patch_Pawn_EchoGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result) yield return gizmo;
            if (!__instance.IsColonistPlayerControlled) yield break;
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            if (echoes == null) yield break;

            EchoRecord host = echoes.HostRecord(__instance);
            if (host != null)
            {
                yield return new Gizmo_EchoCharge(host);
                yield return EchoGizmos.ManifestToggle(host);
                if (DebugSettings.ShowDevGizmos)
                    yield return new Command_Action { defaultLabel = "DEV: Fill charge", action = DebugActions_Echo.Fill };
                yield break;
            }
            EchoRecord candidate = echoes.CandidateRecord(__instance);
            if (candidate == null) yield break;
            yield return EchoGizmos.Trials(candidate);
            // Shown only in god mode, like vanilla's own dev commands.
            if (DebugSettings.ShowDevGizmos && !EchoUtility.TrialsMet(candidate))
            {
                Pawn pawn = __instance;
                yield return new Command_Action { defaultLabel = "DEV: Meet trials", action = () => DebugActions_Echo.MeetTrials(pawn) };
            }
        }
    }

    /// <summary>Host wealth: the Echo's value is added to the Host's market value, so raids grow with it.</summary>
    public class StatPart_EchoHost : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn && GameComponent_Echoes.Get?.HostRecord(pawn) is EchoRecord record)
                val += record.def.wealth;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.Thing is Pawn pawn && GameComponent_Echoes.Get?.HostRecord(pawn) is EchoRecord record)
                return "AG_EchoWealthPart".Translate(record.def.LabelCap, record.def.wealth.ToStringMoney());
            return null;
        }
    }

    [StaticConstructorOnStartup]
    static class EchoStartup
    {
        static EchoStartup()
        {
            StatDef stat = StatDefOf.MarketValue;
            if (stat.parts == null) stat.parts = new List<StatPart>();
            stat.parts.Add(new StatPart_EchoHost { parentStat = stat });

            // The hero-form work block is written once on the manifest comp; vanilla reads disabled
            // work types from hediff stages, so it is copied into every stage of those hediffs.
            foreach (HediffDef hediff in DefDatabase<HediffDef>.AllDefsListForReading)
            {
                HediffCompProperties_EchoManifest props = hediff.CompProps<HediffCompProperties_EchoManifest>();
                if (props == null) continue;
                if (hediff.stages.NullOrEmpty()) hediff.stages = new List<HediffStage> { new HediffStage() };
                foreach (HediffStage stage in hediff.stages) stage.disabledWorkTags |= props.disabledWorkTags;
            }
        }
    }
}
