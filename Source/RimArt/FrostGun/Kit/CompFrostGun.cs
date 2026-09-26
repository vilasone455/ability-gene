using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class FrostGunDefOf
    {
        public static ThingDef AG_FrostGun;
        public static ThingDef AG_FrostGun_Bolt;
        public static AbilityDef AG_FrostGun_FlashFreeze;
        public static HediffDef AG_FrostGunChilled;
        public static HediffDef AG_FrostGunFrozen;
        public static JobDef AG_CastFrostGun;

        static FrostGunDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FrostGunDefOf));
        }
    }

    /// <summary>The coolant tank's rules. Every balance number of the kit that is not the ability's or the bolt's is here, as an XML field on the weapon.</summary>
    public class CompProperties_FrostGun : CompProperties
    {
        public List<AbilityDef> abilities;

        /// <summary>Coolant the tank holds. A new gun comes full.</summary>
        public float capacity = 30f;
        /// <summary>Coolant a second while the gun is held, anywhere.</summary>
        public float refillPerSecond = 0.1f;
        /// <summary>Coolant a second instead while the holder's cell (its room, or outdoors) is below <see cref="coldBelowCelsius"/>.</summary>
        public float refillPerSecondCold = 2f;
        public float coldBelowCelsius = 0f;

        public CompProperties_FrostGun()
        {
            compClass = typeof(CompFrostGun);
        }
    }

    /// <summary>
    /// The frost rifle: grants Flash Freeze to whoever holds it and holds the coolant Flash Freeze
    /// spends. The rifle's normal shot is its own verb and costs nothing. The tank refills only while
    /// the gun is held (MapComponent_FrostGun, once a second): slowly anywhere, fast in the cold.
    ///
    /// It is the weapon's CompEquippable (the def lists its comps with Inherit="False"), as
    /// CompFlameGauntlet is, so the coolant can be an equipped gizmo and the rifle's verb lives on it.
    /// <see cref="ItemAbilityGrant"/> keeps the cooldown on the gun.
    /// </summary>
    public class CompFrostGun : CompEquippable
    {
        private float coolant = -1f;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_FrostGun Props => (CompProperties_FrostGun)props;

        public static CompFrostGun HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompFrostGun>();

        /// <summary>The pawn holding the gun, or null.</summary>
        public Pawn HolderPawn => Holder;

        /// <summary>Coolant in the tank, with the fraction a refill has built up.</summary>
        public float Coolant => coolant < 0f ? Props.capacity : coolant;
        /// <summary>Whole units, what a cast can spend.</summary>
        public int Units => Mathf.FloorToInt(Coolant + 0.0001f);
        public string LabelRemaining => Units + " / " + Props.capacity.ToString("0");

        public void Spend(float amount) => coolant = Mathf.Max(0f, Coolant - amount);

        public void Add(float amount) => coolant = Mathf.Min(Props.capacity, Coolant + amount);

        public void SetCoolant(float value) => coolant = Mathf.Clamp(value, 0f, Props.capacity);

        /// <summary>Coolant a second for a holder standing on <paramref name="cell"/> now.</summary>
        public float RefillRate(IntVec3 cell, Map map) =>
            map != null && cell.InBounds(map) && cell.GetTemperature(map) < Props.coldBelowCelsius ? Props.refillPerSecondCold : Props.refillPerSecond;

        public override void PostPostMake()
        {
            base.PostPostMake();
            coolant = Props.capacity;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            pawn?.MapHeld?.GetComponent<MapComponent_FrostGun>()?.Register(pawn);
            grant.Give(pawn, Props.abilities);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            grant.Take(pawn, Props.abilities, this);
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn holder = Holder;
            if (holder != null && holder.IsColonistPlayerControlled) yield return new Gizmo_FrostGunCoolant(this);
        }

        public override string CompInspectStringExtra() => "Coolant: " + LabelRemaining;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref coolant, "coolant", -1f);
            grant.ExposeData();
        }
    }

    /// <summary>The tank in the command bar: coolant out of the capacity, and the refill rate where the holder stands.</summary>
    [StaticConstructorOnStartup]
    public sealed class Gizmo_FrostGunCoolant : Gizmo
    {
        private static readonly Texture2D Fill = SolidColorMaterials.NewSolidColorTexture(new Color(0.45f, 0.78f, 1f));
        private static readonly Texture2D FillCold = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.93f, 1f));
        private static readonly Texture2D Line = SolidColorMaterials.NewSolidColorTexture(new Color(0.12f, 0.35f, 0.65f));
        private readonly CompFrostGun gun;

        public Gizmo_FrostGunCoolant(CompFrostGun gun)
        {
            this.gun = gun;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            CompProperties_FrostGun props = gun.Props;
            Pawn holder = gun.HolderPawn;
            float rate = holder != null ? gun.RefillRate(holder.Position, holder.Map) : props.refillPerSecond;
            bool cold = rate > props.refillPerSecond;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "Coolant");
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), gun.LabelRemaining);
            Text.Anchor = TextAnchor.UpperLeft;
            var bar = new Rect(inner.x, inner.y + 30f, inner.width, 22f);
            Widgets.FillableBar(bar, Mathf.Clamp01(gun.Coolant / props.capacity), cold ? FillCold : Fill);
            // Flash Freeze's cost, marked on the bar.
            CompProperties_FlashFreeze freeze = FlashFreeze.Props;
            if (freeze != null && freeze.cost < props.capacity)
            {
                float at = bar.x + bar.width * Mathf.Clamp01(freeze.cost / props.capacity);
                GUI.DrawTexture(new Rect(at - 1f, bar.y - 2f, 2f, bar.height + 4f), Line);
            }
            string here = holder != null && holder.Map != null ? " Here it is " + holder.Position.GetTemperature(holder.Map).ToString("0") + " °C: " + rate.ToString("0.##") + " a second." : "";
            TooltipHandler.TipRegion(rect, "Coolant " + gun.Coolant.ToString("0.#") + " / " + props.capacity.ToString("0") + ". Flash Freeze uses "
                + (freeze?.cost.ToString("0") ?? "?") + ". The tank refills " + props.refillPerSecond.ToString("0.##") + " a second while the gun is held, and "
                + props.refillPerSecondCold.ToString("0.##") + " a second below " + props.coldBelowCelsius.ToString("0") + " °C." + here);
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
