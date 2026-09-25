using System.Collections.Generic;
using RimWorld;
using RimWorld.Utility;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    [DefOf]
    public static class FlameGauntletDefOf
    {
        public static ThingDef AG_FlameGauntlet;
        public static HediffDef AG_FlameOverheating;
        public static AbilityDef AG_FlameGauntlet_Devour;
        public static AbilityDef AG_FlameGauntlet_Release;
        public static JobDef AG_CastFlameGauntlet;

        static FlameGauntletDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FlameGauntletDefOf));
        }
    }

    /// <summary>The meter and its rules. Every balance number of the kit that is not an ability's is here, as an XML field on the weapon.</summary>
    public class CompProperties_FlameGauntlet : CompProperties
    {
        public List<AbilityDef> abilities;

        /// <summary>The meter holds this much Heat. Devour refuses what would go over.</summary>
        public float maxHeat = 20f;
        /// <summary>At this much Heat or more the wearer is Overheating.</summary>
        public float overheatAt = 15f;
        /// <summary>Heat lost on its own: one whole Heat every this many seconds since it last changed.</summary>
        public float secondsPerHeatLost = 30f;

        /// <summary>Devour: Heat for each burning cell and each burning pawn it eats.</summary>
        public int heatPerCell = 1;
        public int heatPerPawn = 2;
        /// <summary>Heat for each fire hit the wearer's immunity blocks.</summary>
        public float heatPerBlockedHit = 1f;

        /// <summary>Release: Heat spent per cone cell, and the least it can start with.</summary>
        public int heatPerConeCell = 1;
        public int minReleaseHeat = 5;

        /// <summary>Overheating: a burn of this size on the gauntlet arm every this many seconds.</summary>
        public float overheatBurnSeconds = 5f;
        public float overheatBurnSeverity = 1f;

        /// <summary>
        /// Reload: each unit of <see cref="ammoDef"/> adds <see cref="heatPerAmmo"/>. A reload stops at
        /// <see cref="reloadUpTo"/>, under the overheat line, so filling it never burns the wearer. Only
        /// on the player's order (right-click the chemfuel); a pawn never goes to reload on its own.
        /// </summary>
        public ThingDef ammoDef;
        public float heatPerAmmo = 2f;
        public float reloadUpTo = 14f;
        public int baseReloadTicks = 60;
        public SoundDef soundReload;

        public CompProperties_FlameGauntlet()
        {
            compClass = typeof(CompFlameGauntlet);
        }
    }

    /// <summary>
    /// The flame gauntlet: grants Devour and Release to whoever holds it, and holds the Heat they
    /// fill and spend. Heat falls on its own by one every secondsPerHeatLost, worked out when it is
    /// read, so a gauntlet lying on the floor cools too.
    ///
    /// It is the weapon's CompEquippable (the def lists its comps with Inherit="False"), as
    /// CompTagScroll is, because Core's reload jobs find a weapon's ammunition through
    /// Pawn_EquipmentTracker.PrimaryEq being an IReloadableComp. The charges Core reads are whole
    /// Heat. <see cref="ItemAbilityGrant"/> keeps the cooldowns on the gauntlet.
    /// </summary>
    public class CompFlameGauntlet : CompEquippable, IReloadableComp
    {
        private float heat;
        /// <summary>The game tick <see cref="heat"/> was last written.</summary>
        private int heatTick;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_FlameGauntlet Props => (CompProperties_FlameGauntlet)props;

        public static CompFlameGauntlet HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompFlameGauntlet>();

        /// <summary>
        /// Heat now, after what it has lost since it was last written: one whole Heat at each
        /// secondsPerHeatLost mark, not a little every tick, so a meter set to 8 still pays for 8 cone
        /// cells a moment later.
        /// </summary>
        public float Heat
        {
            get
            {
                if (heat <= 0f) return 0f;
                int period = Mathf.Max(1, Mathf.RoundToInt(60f * Props.secondsPerHeatLost));
                int lost = Mathf.Max(0, Find.TickManager.TicksGame - heatTick) / period;
                return Mathf.Max(0f, heat - lost);
            }
        }

        public bool Overheating => Heat >= Props.overheatAt;

        public void SetHeat(float value)
        {
            heat = Mathf.Clamp(value, 0f, Props.maxHeat);
            heatTick = Find.TickManager.TicksGame;
        }

        public void Add(float amount) => SetHeat(Heat + amount);

        // IReloadableComp and ICompWithCharges
        public Thing ReloadableThing => parent;
        public ThingDef AmmoDef => Props.ammoDef;
        public int BaseReloadTicks => Props.baseReloadTicks;
        public int MaxCharges => Mathf.RoundToInt(Props.maxHeat);
        public int RemainingCharges => Mathf.FloorToInt(Heat + 0.0001f);
        public string LabelRemaining => Heat.ToString("0.#") + " / " + Props.maxHeat.ToString("0");

        private int AmmoRoom => Props.ammoDef == null ? 0 : Mathf.FloorToInt((Props.reloadUpTo - Heat) / Mathf.Max(0.01f, Props.heatPerAmmo) + 0.0001f);

        public bool NeedsReload(bool allowForceReload) => allowForceReload && AmmoRoom > 0;

        public int MinAmmoNeeded(bool allowForcedReload) => NeedsReload(allowForcedReload) ? 1 : 0;

        public int MaxAmmoNeeded(bool allowForcedReload) => NeedsReload(allowForcedReload) ? AmmoRoom : 0;

        public int MaxAmmoAmount() => Props.ammoDef == null ? 0 : Mathf.FloorToInt(Props.reloadUpTo / Mathf.Max(0.01f, Props.heatPerAmmo));

        public void ReloadFrom(Thing ammo)
        {
            int loaded = Mathf.Min(ammo.stackCount, AmmoRoom);
            if (loaded <= 0) return;
            ammo.SplitOff(loaded).Destroy();
            Add(loaded * Props.heatPerAmmo);
            Props.soundReload?.PlayOneShot(new TargetInfo(parent.PositionHeld, parent.MapHeld));
        }

        /// <summary>The gauntlet always works as a weapon; each ability checks the Heat it needs itself.</summary>
        public bool CanBeUsed(out string reason)
        {
            reason = null;
            return true;
        }

        public string DisabledReason(int minNeeded, int maxNeeded) =>
            "The gauntlet takes " + (Props.ammoDef?.label ?? "fuel") + " only up to " + Props.reloadUpTo.ToString("0") + " heat.";

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            pawn?.MapHeld?.GetComponent<MapComponent_FlameGauntlet>()?.Register(pawn);
            grant.Give(pawn, Props.abilities);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            grant.Take(pawn, Props.abilities, this);
            FlameGauntletHeat.EndOverheating(pawn);
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn holder = Holder;
            if (holder != null && holder.IsColonistPlayerControlled) yield return new Gizmo_FlameHeat(this);
        }

        public override string CompInspectStringExtra() => "Heat: " + LabelRemaining + (Overheating ? " (overheating)" : "");

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref heat, "heat");
            Scribe_Values.Look(ref heatTick, "heatTick");
            grant.ExposeData();
        }
    }

    /// <summary>The meter in the command bar: Heat out of the most, with the overheat line marked.</summary>
    [StaticConstructorOnStartup]
    public sealed class Gizmo_FlameHeat : Gizmo
    {
        private static readonly Texture2D Fill = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.55f, 0.10f));
        private static readonly Texture2D FillHot = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.30f, 0.12f));
        private static readonly Texture2D Line = SolidColorMaterials.NewSolidColorTexture(new Color(0.86f, 0.22f, 0.05f));
        private readonly CompFlameGauntlet gauntlet;

        public Gizmo_FlameHeat(CompFlameGauntlet gauntlet)
        {
            this.gauntlet = gauntlet;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            CompProperties_FlameGauntlet props = gauntlet.Props;
            float heat = gauntlet.Heat, share = Mathf.Clamp01(heat / props.maxHeat);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "Heat");
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), Mathf.FloorToInt(heat + 0.0001f) + " / " + props.maxHeat.ToString("0"));
            Text.Anchor = TextAnchor.UpperLeft;
            var bar = new Rect(inner.x, inner.y + 30f, inner.width, 22f);
            Widgets.FillableBar(bar, share, gauntlet.Overheating ? FillHot : Fill);
            float at = bar.x + bar.width * Mathf.Clamp01(props.overheatAt / props.maxHeat);
            GUI.DrawTexture(new Rect(at - 1f, bar.y - 2f, 2f, bar.height + 4f), Line);
            TooltipHandler.TipRegion(rect, "Heat " + heat.ToString("0.#") + " / " + props.maxHeat.ToString("0") + ". Devour adds "
                + props.heatPerCell + " per burning cell and " + props.heatPerPawn + " per burning pawn; Release spends "
                + props.heatPerConeCell + " per cone cell (at least " + props.minReleaseHeat + "). One heat is lost every "
                + props.secondsPerHeatLost.ToString("0") + " s. At " + props.overheatAt.ToString("0") + " or more the wearer overheats: slower, clumsier, and the gauntlet arm burns every "
                + props.overheatBurnSeconds.ToString("0") + " s.");
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
