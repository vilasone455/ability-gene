using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class CoilGunDefOf
    {
        public static ThingDef AG_CoilGun;
        public static ThingDef AG_CoilGun_Bolt;
        public static AbilityDef AG_CoilGun_ChainArc;
        public static JobDef AG_CastCoilGun;
        /// <summary>The bolts' and Chain Arc's damage: Core's Bullet (Sharp armour) that leaves Core's electrical burn.</summary>
        public static DamageDef AG_CoilBurn;

        static CoilGunDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CoilGunDefOf));
        }
    }

    /// <summary>The gun's battery and how it recharges. Every balance number of the battery is here, as an XML field on the weapon.</summary>
    public class CompProperties_CoilGun : CompProperties
    {
        public List<AbilityDef> abilities;

        /// <summary>Charge the gun's battery holds. A new gun comes full.</summary>
        public int capacity = 20;
        /// <summary>Charge a second while the holder stands next to a charged battery of its faction.</summary>
        public float chargePerSecond = 1f;
        /// <summary>Stored energy (watt-days, Wd) taken from that battery for each charge. A vanilla battery holds 600 Wd.</summary>
        public float wattDaysPerCharge = 10f;
        /// <summary>Cells from the holder to the nearest cell of the battery building.</summary>
        public float rechargeRadius = 1.5f;

        public CompProperties_CoilGun()
        {
            compClass = typeof(CompCoilGun);
        }
    }

    /// <summary>
    /// The coil gun: grants Chain Arc to whoever holds it and holds the battery Chain Arc spends. The
    /// battery does not refill on its own; MapComponent_CoilGun tops it up once a second while the
    /// holder stands within rechargeRadius of a battery building of its own faction that has stored
    /// energy, taking wattDaysPerCharge from it per charge. The gun's normal shots use no charge.
    ///
    /// It is the weapon's CompEquippable (the def lists its comps with Inherit="False"), as
    /// CompSamehada is, so the battery readout can be an equipped gizmo and Core's shooting verb stays on
    /// it. <see cref="ItemAbilityGrant"/> keeps Chain Arc's cooldown on the gun.
    /// </summary>
    public class CompCoilGun : CompEquippable
    {
        private float charge = -1f;
        /// <summary>The tick the holder last took charge from a battery, for the gizmo's "charging" line.</summary>
        private int chargedTick = -99999;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_CoilGun Props => (CompProperties_CoilGun)props;

        public static CompCoilGun HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompCoilGun>();

        /// <summary>Charge in the battery, with the fraction a recharge has built up.</summary>
        public float Charge => charge < 0f ? Props.capacity : charge;
        /// <summary>Whole charges, what a cast can spend.</summary>
        public int Units => Mathf.FloorToInt(Charge + 0.0001f);
        public bool Full => Charge >= Props.capacity - 0.0001f;
        public string LabelRemaining => Units + " / " + Props.capacity;
        /// <summary>Whether a battery topped the gun up in the last two seconds.</summary>
        public bool Charging => Find.TickManager.TicksGame - chargedTick <= 120;

        public void Spend(int units) => charge = Mathf.Max(0f, Charge - units);

        /// <summary>Adds charge up to the capacity and returns how much was added.</summary>
        public float Add(float units)
        {
            float before = Charge;
            charge = Mathf.Min(Props.capacity, before + Mathf.Max(0f, units));
            if (charge > before) chargedTick = Find.TickManager.TicksGame;
            return charge - before;
        }

        public void SetCharge(float value) => charge = Mathf.Clamp(value, 0f, Props.capacity);

        public override void PostPostMake()
        {
            base.PostPostMake();
            charge = Props.capacity;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            pawn?.MapHeld?.GetComponent<MapComponent_CoilGun>()?.Register(pawn);
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
            if (holder != null && holder.IsColonistPlayerControlled) yield return new Gizmo_CoilGunBattery(this);
        }

        public override string CompInspectStringExtra() => "Battery: " + LabelRemaining + (Charging ? " (charging)" : "");

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref charge, "charge", -1f);
            grant.ExposeData();
        }
    }

    /// <summary>The battery readout in the command bar: a bar of charge, and whether it is charging.</summary>
    [StaticConstructorOnStartup]
    public sealed class Gizmo_CoilGunBattery : Gizmo
    {
        private static readonly Texture2D Full = SolidColorMaterials.NewSolidColorTexture(new Color(0.40f, 0.72f, 1f));
        private static readonly Texture2D Empty = SolidColorMaterials.NewSolidColorTexture(new Color(0.10f, 0.13f, 0.20f));
        private readonly CompCoilGun gun;

        public Gizmo_CoilGunBattery(CompCoilGun gun)
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
            CompProperties_CoilGun props = gun.Props;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), gun.Charging ? "Charging" : "Battery");
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), gun.LabelRemaining);
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.FillableBar(new Rect(inner.x, inner.y + 30f, inner.width, 22f), gun.Charge / Mathf.Max(1, props.capacity), Full, Empty, true);
            int cost = CoilGunDefOf.AG_CoilGun_ChainArc?.comps?.Find(c => c is CompProperties_ChainArc) is CompProperties_ChainArc arc ? arc.cost : 0;
            TooltipHandler.TipRegion(rect, "Battery " + gun.LabelRemaining + ". Chain Arc uses " + cost + "; normal shots use none. It does not refill on its own: standing within "
                + props.rechargeRadius.ToString("0.#") + " tiles of a charged battery adds " + props.chargePerSecond.ToString("0.#") + " a second and takes "
                + props.wattDaysPerCharge.ToString("0.#") + " Wd of that battery's stored energy for each.");
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
