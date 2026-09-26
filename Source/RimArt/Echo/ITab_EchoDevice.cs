using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The resonance device tab: the shared pool at the top, then one card per Echo. A card shows
    /// its state (not tuned, tracking a candidate, awakened, manifested, closed), the Trials with the
    /// candidate's progress, the cost as it would apply to that candidate, upkeep and cast costs.
    /// </summary>
    public class ITab_EchoDevice : ITab
    {
        private const float HeaderHeight = 116f, CardWidth = 238f, CardHeight = 300f, Gap = 8f, Margin = 10f, ScrollBar = 16f;
        private static readonly Color Muted = new Color(0.65f, 0.65f, 0.65f);
        private Vector2 scroll;

        public ITab_EchoDevice()
        {
            size = new Vector2(3 * CardWidth + 2 * Gap + ScrollBar + 2 * Margin + 4f, 620f);
            labelKey = "AG_EchoTab";
        }

        protected override void FillTab()
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            Rect all = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
            DrawHeader(new Rect(all.x, all.y, all.width, HeaderHeight), echoes);

            List<EchoDef> defs = EchoUtility.AllEchoes
                .OrderBy(d => SortKey(echoes.RecordFor(d))).ThenBy(d => d.order).ToList();
            Rect outer = new Rect(all.x, all.y + HeaderHeight + Gap, all.width, all.height - HeaderHeight - Gap);
            int columns = Mathf.Max(1, Mathf.FloorToInt((outer.width - ScrollBar + Gap) / (CardWidth + Gap)));
            int rows = Mathf.CeilToInt(defs.Count / (float)columns);
            Rect view = new Rect(0f, 0f, outer.width - ScrollBar, rows * (CardHeight + Gap));
            Widgets.BeginScrollView(outer, ref scroll, view);
            for (int i = 0; i < defs.Count; i++)
            {
                int col = i % columns, row = i / columns;
                DrawCard(new Rect(col * (CardWidth + Gap), row * (CardHeight + Gap), CardWidth, CardHeight),
                    echoes.RecordFor(defs[i]));
            }
            Widgets.EndScrollView();
        }

        private static int SortKey(EchoRecord record)
        {
            if (record.manifested) return 0;
            switch (record.state)
            {
                case EchoState.Awakened: return 1;
                case EchoState.Tracking: return 2;
                case EchoState.Untuned: return 3;
                default: return 4;
            }
        }

        private static void DrawHeader(Rect rect, GameComponent_Echoes echoes)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, 200f, 24f), "AG_EchoCharge".Translate());
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f),
                echoes.charge.ToString("0") + " / " + echoes.MaxCharge.ToString("0"));
            Text.Anchor = TextAnchor.UpperLeft;
            float share = echoes.MaxCharge <= 0f ? 0f : Mathf.Clamp01(echoes.charge / echoes.MaxCharge);
            Widgets.FillableBar(new Rect(inner.x, inner.y + 26f, inner.width, 14f), share, EchoTex.Charge);

            float w = inner.width / 4f, y = inner.y + 48f;
            Stat(new Rect(inner.x, y, w, 52f), "AG_EchoRefill".Translate(),
                echoes.DeviceWorking ? "+" + echoes.RefillPerHour.ToString("0.#") + " / h" : "AG_EchoNoPower".Translate().ToString());
            Stat(new Rect(inner.x + w, y, w, 52f), "AG_EchoDrainNow".Translate(),
                "-" + echoes.DrainPerHour.ToString("0.#") + " / h");
            Stat(new Rect(inner.x + 2 * w, y, w, 52f), "AG_EchoHeroes".Translate(),
                echoes.HostCount + " / " + echoes.HeroCap);
            EchoDeviceTier next = EchoDevice.NextTier;
            Stat(new Rect(inner.x + 3 * w, y, w, 52f), "AG_EchoNextUpgrade".Translate(), next == null
                ? "AG_EchoMaxTier".Translate().ToString()
                : "AG_EchoUpgradeLine".Translate(next.research.LabelCap, next.heroCap, next.maxCharge.ToString("0"),
                    next.refillPerHour.ToString("0.#")).ToString());
        }

        private static void Stat(Rect rect, string label, string value)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = Muted;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 16f), label);
            GUI.color = Color.white;
            Widgets.Label(new Rect(rect.x, rect.y + 15f, rect.width - 4f, rect.height - 15f), value);
            Text.Font = GameFont.Small;
        }

        private static void DrawCard(Rect rect, EchoRecord record)
        {
            EchoDef def = record.def;
            Widgets.DrawMenuSection(rect);
            if (record.manifested) Widgets.DrawBoxSolidWithOutline(rect, Color.clear, new Color(0.35f, 0.75f, 0.35f), 2);
            else if (record.state == EchoState.Tracking) Widgets.DrawBoxSolidWithOutline(rect, Color.clear, new Color(0.35f, 0.62f, 0.95f), 2);
            Rect inner = rect.ContractedBy(8f);
            float y = inner.y;

            Pawn pawn = record.state == EchoState.Awakened ? record.host
                : record.state == EchoState.Tracking ? record.candidate : null;
            Rect portrait = new Rect(inner.x, y, 44f, 44f);
            Widgets.DrawBoxSolid(portrait, new Color(0.12f, 0.12f, 0.12f));
            if (pawn != null) GUI.DrawTexture(portrait, PortraitsCache.Get(pawn, new Vector2(44f, 44f), Rot4.South));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x + 50f, y, inner.width - 50f, 22f), def.LabelCap);
            Text.Font = GameFont.Tiny;
            GUI.color = Muted;
            Widgets.Label(new Rect(inner.x + 50f, y + 20f, inner.width - 50f, 34f), StateLine(record));
            GUI.color = Color.white;
            y += 58f;

            if (!def.subtitle.NullOrEmpty())
            {
                GUI.color = Muted;
                Widgets.Label(new Rect(inner.x, y, inner.width, 16f), def.subtitle);
                GUI.color = Color.white;
                y += 16f;
            }

            if (record.state == EchoState.Untuned || record.state == EchoState.Tracking)
            {
                foreach (EchoTrial trial in def.trials) y = DrawTrial(inner, y, trial, record.candidate);
                y += 4f;
                foreach (string line in EchoUtility.CostLines(def, record.candidate))
                {
                    float h = Text.CalcHeight(line, inner.width);
                    Widgets.Label(new Rect(inner.x, y, inner.width, h), line);
                    y += h;
                }
            }
            else
            {
                string abilities = def.abilities.Select<AbilityDef, string>(a =>
                {
                    float cost = def.CastCost(a);
                    return cost > 0f ? a.LabelCap + " (" + cost.ToString("0") + ")" : a.LabelCap.ToString();
                }).ToLineList("  ");
                float h = Text.CalcHeight(abilities, inner.width);
                Widgets.Label(new Rect(inner.x, y, inner.width, h), abilities);
                y += h;
            }

            Widgets.Label(new Rect(inner.x, inner.yMax - 50f, inner.width, 18f),
                "AG_EchoUpkeepLine".Translate(def.upkeepPerHour.ToString("0.#")));
            Text.Font = GameFont.Small;
            DrawButton(new Rect(inner.x, inner.yMax - 28f, inner.width, 28f), record);
        }

        private static string StateLine(EchoRecord record)
        {
            switch (record.state)
            {
                case EchoState.Tracking:
                    return "AG_EchoStateTracking".Translate(record.candidate.LabelShortCap,
                        record.def.trials.Count(t => t.Met(record.candidate)), record.def.trials.Count);
                case EchoState.Awakened:
                    return (record.manifested ? "AG_EchoStateManifested" : "AG_EchoStateAwakened")
                        .Translate(record.host.LabelShortCap);
                case EchoState.Closed: return "AG_EchoStateClosed".Translate();
                default: return "AG_EchoStateUntuned".Translate();
            }
        }

        private static float DrawTrial(Rect inner, float y, EchoTrial trial, Pawn pawn)
        {
            Text.Font = GameFont.Tiny;
            bool met = pawn != null && trial.Met(pawn);
            Widgets.Label(new Rect(inner.x, y, inner.width, 16f), (met ? "✓ " : "") + trial.Label);
            if (pawn == null) return y + 16f;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, y, inner.width, 16f), trial.ProgressText(pawn));
            Text.Anchor = TextAnchor.UpperLeft;
            float share = trial.IsExclusion ? (met ? 1f : 0f) : trial.Target <= 0f ? 1f : Mathf.Clamp01(trial.Current(pawn) / trial.Target);
            Widgets.FillableBar(new Rect(inner.x, y + 16f, inner.width, 6f), share, met ? EchoTex.TrialDone : EchoTex.TrialOpen);
            return y + 26f;
        }

        private static void DrawButton(Rect rect, EchoRecord record)
        {
            switch (record.state)
            {
                case EchoState.Untuned:
                    if (Widgets.ButtonText(rect, "AG_EchoTune".Translate())) Find.WindowStack.Add(new FloatMenu(TuneOptions(record.def)));
                    break;
                case EchoState.Tracking:
                    if (EchoUtility.TrialsMet(record))
                    {
                        if (Widgets.ButtonText(rect, "AG_EchoAwakenAccept".Translate()))
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                                EchoUtility.AwakenText(record.def, record.candidate), () => EchoUtility.Awaken(record), true));
                    }
                    else if (Widgets.ButtonText(rect, "AG_EchoStopTuning".Translate()))
                        EchoUtility.StopTuning(record, lost: false);
                    break;
            }
        }

        private static List<FloatMenuOption> TuneOptions(EchoDef def)
        {
            var options = new List<FloatMenuOption>();
            EchoRecord tuned = GameComponent_Echoes.Get.Tuned;
            foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists.OrderBy(p => p.LabelShort))
            {
                string label = pawn.LabelShortCap;
                if (tuned != null) label += " (" + "AG_EchoRetune".Translate(tuned.def.LabelCap) + ")";
                if (EchoUtility.CanBeCandidate(pawn, out string reason))
                    options.Add(new FloatMenuOption(label, () => EchoUtility.Tune(def, pawn)));
                else
                    options.Add(new FloatMenuOption(pawn.LabelShortCap + ": " + reason, null));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("AG_EchoNoColonists".Translate(), null));
            return options;
        }
    }
}
