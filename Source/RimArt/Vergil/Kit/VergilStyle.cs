using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>One letter rank of the Style meter and the Style it starts at.</summary>
    public sealed class VergilStyleRank
    {
        public string label;
        public float from;
    }

    /// <summary>
    /// The Style numbers, on the Vergil Echo (AG_Echoes.xml). How much each ability adds is on the ability's
    /// own comp (styleGainPerPawn and the rest); what the meter itself does is here.
    /// </summary>
    public sealed class VergilStyleExtension : DefModExtension
    {
        public float max = 100f;
        public List<VergilStyleRank> ranks = new List<VergilStyleRank>();
        /// <summary>Judgement Cut End needs this much Style (rank S) and spends all of it.</summary>
        public float cutEndNeeds = 60f;
        /// <summary>Added per landed Yamato melee hit on a hostile pawn.</summary>
        public float meleeHit = 2f;
        /// <summary>Lost each time Vergil takes damage.</summary>
        public float damageTaken = 20f;
        /// <summary>Lost per second once <see cref="drainAfterSeconds"/> have passed without landing a hit.</summary>
        public float drainPerSecond = 5f;
        public float drainAfterSeconds = 10f;

        public string RankOf(float style)
        {
            string rank = ranks.Count > 0 ? ranks[0].label : "D";
            for (int i = 0; i < ranks.Count; i++)
                if (style >= ranks[i].from) rank = ranks[i].label;
            return rank;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (ranks.NullOrEmpty()) yield return "VergilStyleExtension has no ranks";
            for (int i = 1; i < ranks.Count; i++)
                if (ranks[i].from < ranks[i - 1].from) yield return "VergilStyleExtension ranks are not in rising order";
        }
    }

    /// <summary>One Host's Style: 0 to max while manifested as Vergil. Emptied when he reverts.</summary>
    public sealed class VergilStyleMeter : IExposable
    {
        public Pawn host;
        public float style;
        /// <summary>The last tick he landed a hit; the drain waits for drainAfterSeconds after it.</summary>
        public int lastHitTick;

        /// <summary>Every <see cref="VergilStyle.TickInterval"/> ticks: the drain. False when the meter can go.</summary>
        public bool Tick(int now)
        {
            if (host == null || host.Destroyed) return false;
            VergilStyleExtension rules = VergilStyle.Rules;
            if (rules == null) return true;
            if (VergilKit.Manifested(host) == null)
            {
                style = 0f;
                return true;
            }
            if (style > 0f && now - lastHitTick > rules.drainAfterSeconds * 60f)
                style = Mathf.Max(0f, style - rules.drainPerSecond * VergilStyle.TickInterval / 60f);
            return true;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref host, "host");
            Scribe_Values.Look(ref style, "style");
            Scribe_Values.Look(ref lastHitTick, "lastHitTick");
        }
    }

    /// <summary>
    /// Style, DMC's style meter (docs/hero-echo.md, the Judgement Cut End sketch header): a meter 0-100 on Vergil
    /// while he is manifested, shown on its gizmo as a letter rank. Hits on hostile pawns fill it, taking damage
    /// empties some of it, and it drains after a while without a hit. Judgement Cut End needs rank S and spends
    /// all of it.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VergilStyle
    {
        public const int TickInterval = 15;

        internal static readonly Texture2D Bar = SolidColorMaterials.NewSolidColorTexture(new Color(0.3f, 0.55f, 1f));
        internal static readonly Texture2D BarReady = SolidColorMaterials.NewSolidColorTexture(new Color(0.8f, 0.9f, 1f));

        public static VergilStyleExtension Rules => VergilDefOf.AG_Echo_Vergil?.GetModExtension<VergilStyleExtension>();

        public static float Of(Pawn host) => GameComponent_Vergil.Instance?.Meter(host, false)?.style ?? 0f;

        /// <summary>
        /// Adds Style for a landed hit on <paramref name="victim"/>, if it is a hostile pawn and
        /// <paramref name="host"/> is manifested as Vergil. Counts as landing a hit for the drain.
        /// </summary>
        public static void Hit(Pawn host, Pawn victim, float amount)
        {
            if (amount <= 0f || victim == null || !victim.HostileTo(host) || VergilKit.Manifested(host) == null) return;
            VergilStyleExtension rules = Rules;
            VergilStyleMeter meter = GameComponent_Vergil.Instance?.Meter(host, true);
            if (rules == null || meter == null) return;
            meter.style = Mathf.Min(rules.max, meter.style + amount);
            meter.lastHitTick = Find.TickManager.TicksGame;
        }

        public static void Lose(Pawn host, float amount)
        {
            VergilStyleMeter meter = GameComponent_Vergil.Instance?.Meter(host, false);
            if (meter == null || amount <= 0f) return;
            meter.style = Mathf.Max(0f, meter.style - amount);
        }

        /// <summary>Spends all of it: what was there, for Judgement Cut End's cut count.</summary>
        public static float SpendAll(Pawn host)
        {
            VergilStyleMeter meter = GameComponent_Vergil.Instance?.Meter(host, false);
            if (meter == null) return 0f;
            float had = meter.style;
            meter.style = 0f;
            return had;
        }

        /// <summary>For tests and god mode: sets the meter.</summary>
        public static void Set(Pawn host, float style)
        {
            VergilStyleMeter meter = GameComponent_Vergil.Instance?.Meter(host, true);
            if (meter == null) return;
            meter.style = Mathf.Clamp(style, 0f, Rules?.max ?? 100f);
            meter.lastHitTick = Find.TickManager.TicksGame;
        }
    }

    /// <summary>The Style gizmo on a manifested Vergil: the letter rank, the bar, and how far to rank S.</summary>
    public sealed class Gizmo_VergilStyle : Gizmo
    {
        private readonly Pawn host;

        public Gizmo_VergilStyle(Pawn host)
        {
            this.host = host;
            Order = -99f;
        }

        public override float GetWidth(float maxWidth) => 120f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            VergilStyleExtension rules = VergilStyle.Rules;
            float style = VergilStyle.Of(host), max = rules?.max ?? 100f, needs = rules?.cutEndNeeds ?? 60f;
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "Style");
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y - 3f, inner.width, 30f), rules?.RankOf(style) ?? "D");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            var bar = new Rect(inner.x, inner.y + 28f, inner.width, 14f);
            Widgets.FillableBar(bar, max <= 0f ? 0f : Mathf.Clamp01(style / max), style >= needs ? VergilStyle.BarReady : VergilStyle.Bar);
            // A tick where rank S starts: Judgement Cut End is ready past it.
            float mark = bar.x + bar.width * Mathf.Clamp01(needs / max);
            Widgets.DrawLineVertical(mark, bar.y - 2f, bar.height + 4f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, inner.y + 44f, inner.width, 20f), Mathf.FloorToInt(style) + " / " + max.ToString("0"));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(rect, "Style: " + style.ToString("0") + " / " + max.ToString("0") + ", rank " + (rules?.RankOf(style) ?? "D")
                + ".\n\nHits on hostile pawns fill it: Judgement Cut +4 per pawn, Yamato Dash +3 per mark, Summoned Swords +1 per hit, Yamato +"
                + (rules?.meleeHit ?? 2f).ToString("0") + " per melee hit. Taking damage costs " + (rules?.damageTaken ?? 20f).ToString("0")
                + ". After " + (rules?.drainAfterSeconds ?? 10f).ToString("0") + " seconds without a hit it drains "
                + (rules?.drainPerSecond ?? 5f).ToString("0") + " a second.\n\nJudgement Cut End needs rank S (" + needs.ToString("0")
                + ") and spends all of it.");
            return new GizmoResult(GizmoState.Clear);
        }
    }

    /// <summary>
    /// The two hits Style hears about from outside the abilities: a landed Yamato melee hit on a hostile pawn
    /// (the weapon on the DamageInfo is Yamato; the abilities' own cuts name no weapon and add their Style
    /// themselves), and damage Vergil takes.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    static class Patch_Pawn_VergilStyle
    {
        static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (totalDamageDealt <= 0f || GameComponent_Vergil.Instance == null) return;
            if (dinfo.Weapon == VergilDefOf.AG_Yamato && dinfo.Instigator is Pawn host && host != __instance)
                VergilStyle.Hit(host, __instance, VergilStyle.Rules?.meleeHit ?? 0f);
            if (dinfo.Instigator != __instance && VergilKit.Manifested(__instance) != null)
                VergilStyle.Lose(__instance, VergilStyle.Rules?.damageTaken ?? 0f);
        }
    }

    /// <summary>The Style gizmo and, while the swords are out, the Summoned Swords mode button.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    static class Patch_Pawn_VergilGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result) yield return gizmo;
            if (!__instance.IsColonistPlayerControlled || VergilKit.Manifested(__instance) == null) yield break;
            yield return new Gizmo_VergilStyle(__instance);
            SummonedSwordsCast swords = GameComponent_Vergil.Instance?.Latest<SummonedSwordsCast>(__instance);
            if (swords != null && swords.Out(Find.TickManager.TicksGame)) yield return swords.ModeCommand();
            if (DebugSettings.ShowDevGizmos)
            {
                Pawn host = __instance;
                yield return new Command_Action { defaultLabel = "DEV: Style to SSS", action = () => VergilStyle.Set(host, VergilStyle.Rules?.max ?? 100f) };
            }
        }
    }
}
