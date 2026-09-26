using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public static class EchoGizmos
    {
        public static Command ManifestToggle(EchoRecord record)
        {
            var toggle = new Command_Toggle
            {
                defaultLabel = "AG_EchoManifestLabel".Translate(record.def.LabelCap),
                defaultDesc = "AG_EchoManifestDesc".Translate(record.def.LabelCap, record.def.upkeepPerHour.ToString("0.#")),
                icon = EchoTex.Manifest,
                isActive = () => record.manifested,
                toggleAction = () =>
                {
                    if (record.manifested) EchoUtility.Revert(record, collapse: false);
                    else EchoUtility.Manifest(record);
                },
                Order = -99f,
            };
            if (!record.manifested && !EchoUtility.CanManifest(record, out string reason)) toggle.Disable(reason);
            return toggle;
        }

        public static Command Trials(EchoRecord record)
        {
            Pawn pawn = record.candidate;
            bool ready = EchoUtility.TrialsMet(record);
            return new Command_Action
            {
                defaultLabel = ready ? "AG_EchoAwakenGizmo".Translate(record.def.LabelCap)
                    : "AG_EchoTrialsGizmo".Translate(record.def.LabelCap,
                        record.def.trials.Count(t => t.Met(pawn)), record.def.trials.Count),
                defaultDesc = TrialsText(record),
                icon = EchoTex.Trials,
                Order = -99f,
                action = () =>
                {
                    if (!ready)
                    {
                        Find.WindowStack.Add(new Dialog_MessageBox(TrialsText(record)));
                        return;
                    }
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        EchoUtility.AwakenText(record.def, pawn), () => EchoUtility.Awaken(record), true));
                }
            };
        }

        public static string TrialsText(EchoRecord record)
        {
            Pawn pawn = record.candidate;
            var sb = new StringBuilder();
            sb.AppendLine("AG_EchoTrialsHeader".Translate(record.def.LabelCap, pawn.LabelShortCap));
            foreach (EchoTrial trial in record.def.trials)
                sb.AppendLine((trial.Met(pawn) ? "  ✓ " : "  · ") + trial.Label + ": " + trial.ProgressText(pawn));
            string costs = EchoUtility.CostLines(record.def, pawn).ToLineList("  - ");
            if (!costs.NullOrEmpty()) sb.AppendLine().AppendLine("AG_EchoCostHeader".Translate()).Append(costs);
            return sb.ToString().TrimEndNewlines();
        }
    }

    /// <summary>The shared pool on every Host: charge, refill and the drain of everyone manifested.</summary>
    public sealed class Gizmo_EchoCharge : Gizmo
    {
        private readonly EchoRecord record;

        public Gizmo_EchoCharge(EchoRecord record)
        {
            this.record = record;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            float share = echoes.MaxCharge <= 0f ? 0f : Mathf.Clamp01(echoes.charge / echoes.MaxCharge);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "AG_EchoCharge".Translate());
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f),
                Mathf.FloorToInt(echoes.charge) + " / " + echoes.MaxCharge.ToString("0"));
            Text.Anchor = TextAnchor.UpperLeft;
            var bar = new Rect(inner.x, inner.y + 26f, inner.width, 16f);
            Widgets.FillableBar(bar, share, share < 0.2f ? EchoTex.ChargeLow : EchoTex.Charge);
            Text.Font = GameFont.Tiny;
            float net = (echoes.DeviceWorking ? echoes.RefillPerHour : 0f) - echoes.DrainPerHour;
            Widgets.Label(new Rect(inner.x, inner.y + 44f, inner.width, 20f),
                "AG_EchoNetPerHour".Translate((net >= 0f ? "+" : "") + net.ToString("0.#")));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(rect, "AG_EchoChargeTip".Translate(echoes.charge.ToString("0"),
                echoes.MaxCharge.ToString("0"), echoes.DeviceWorking ? echoes.RefillPerHour.ToString("0.#") : "0",
                echoes.DrainPerHour.ToString("0.#"), record.def.LabelCap, record.def.upkeepPerHour.ToString("0.#")));
            return new GizmoResult(GizmoState.Clear);
        }
    }

    /// <summary>
    /// On the manifest hediff. If something other than EchoUtility.Revert takes the hediff off (a
    /// healer serum, the dev tools), the Echo reverts too, so its abilities and look never outlive it.
    /// </summary>
    public class HediffComp_EchoManifest : HediffComp
    {
        public override void CompPostPostRemoved()
        {
            EchoRecord record = GameComponent_Echoes.Get?.HostRecord(Pawn);
            if (record != null && record.manifested && record.def.manifestHediff == parent.def)
                EchoUtility.Revert(record, collapse: false);
        }
    }

    public class HediffCompProperties_EchoManifest : HediffCompProperties
    {
        /// <summary>
        /// Hero form fights and moves; these are the colony work it does not do. Written once here
        /// and copied into every stage of the hediff at startup (EchoStartup), because vanilla reads
        /// disabled work types from hediff stages only.
        /// </summary>
        public WorkTags disabledWorkTags;

        public HediffCompProperties_EchoManifest() => compClass = typeof(HediffComp_EchoManifest);
    }

}
