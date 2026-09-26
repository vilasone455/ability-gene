using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class SamehadaDefOf
    {
        public static ThingDef AG_Samehada;
        public static HediffDef AG_SamehadaDrained;
        public static HediffDef AG_SamehadaFused;
        public static AbilityDef AG_Samehada_SharkSkin;
        public static AbilityDef AG_Samehada_Fusion;
        public static JobDef AG_CastSamehada;

        static SamehadaDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SamehadaDefOf));
        }
    }

    /// <summary>Feed's rules and the blade's charges. Every balance number of the kit that is not an ability's is here, as an XML field on the weapon.</summary>
    public class CompProperties_Samehada : CompProperties
    {
        public List<AbilityDef> abilities;

        /// <summary>The most charges the blade holds.</summary>
        public int maxCharges = 5;
        /// <summary>Charges each fed hit adds.</summary>
        public int chargesPerHit = 1;
        /// <summary>Melee damage each charge adds to the blade's hits.</summary>
        public float damagePerCharge = 2f;
        /// <summary>One charge is lost at every this many seconds without a fed hit.</summary>
        public float secondsPerChargeLost = 60f;
        /// <summary>Hit points the holder heals on each fed hit.</summary>
        public float healPerHit = 4f;
        /// <summary>Each fed hit adds a stack of AG_SamehadaDrained to the target and sets it to last this long.</summary>
        public float drainedSeconds = 20f;
        /// <summary>The most Drained stacks a pawn can carry (the hediff's stages hold what each stack does).</summary>
        public int maxDrainedStacks = 3;

        public CompProperties_Samehada()
        {
            compClass = typeof(CompSamehada);
        }
    }

    /// <summary>
    /// Samehada: grants Shark Skin and Fusion to whoever holds it and keeps the charges Feed puts in it.
    /// Charges fall by one every secondsPerChargeLost since the last fed hit, worked out when they are read,
    /// so a blade lying on the floor loses them too. Shark Skin's time left is kept here as well, and the
    /// picture's state of the bandage (torn by Shark Skin, wrapped back one row per fed hit).
    ///
    /// It is the weapon's CompEquippable (the def lists its comps with Inherit="False"), as
    /// CompFlameGauntlet is, so the charge readout can be an equipped gizmo. <see cref="ItemAbilityGrant"/>
    /// keeps the cooldowns on the blade.
    /// </summary>
    public class CompSamehada : CompEquippable
    {
        private int charges;
        /// <summary>The tick the charge clock counts from: the last fed hit, moved on by each charge lost since.</summary>
        private int chargeTick;
        private int sharkSkinUntil = -1;
        /// <summary>Cells of bandage wrapped back since Shark Skin tore it; below 0 the wrap is whole.</summary>
        private float rewrap = -1f;
        /// <summary>The picture of the last charge gained: when, and how many.</summary>
        private int gainTick = -99999, gainAmount;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_Samehada Props => (CompProperties_Samehada)props;

        public static CompSamehada HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompSamehada>();

        private int Period => Mathf.Max(1, Mathf.RoundToInt(60f * Props.secondsPerChargeLost));

        /// <summary>Charges now, after those lost since the last fed hit.</summary>
        public int Charges
        {
            get
            {
                if (charges <= 0) return 0;
                int lost = Mathf.Max(0, Find.TickManager.TicksGame - chargeTick) / Period;
                return Mathf.Max(0, charges - lost);
            }
        }

        /// <summary>Writes the charges lost so far into the stored count, keeping the clock's phase.</summary>
        private void Settle()
        {
            int now = Find.TickManager.TicksGame, lost = Mathf.Max(0, now - chargeTick) / Period;
            if (lost <= 0) return;
            charges = Mathf.Max(0, charges - lost);
            chargeTick += lost * Period;
        }

        /// <summary>Sets the charges and starts the clock again (the debug shortcuts, the tests, Fusion's end).</summary>
        public void SetCharges(int value)
        {
            charges = Mathf.Clamp(value, 0, Props.maxCharges);
            chargeTick = Find.TickManager.TicksGame;
        }

        /// <summary>Tests only: moves the charge clock back, as if the last fed hit was <paramref name="ticks"/> earlier.</summary>
        internal void AgeChargeClock(int ticks) => chargeTick -= ticks;

        /// <summary>Takes <paramref name="amount"/> charges off, keeping the clock.</summary>
        public void Spend(int amount)
        {
            Settle();
            charges = Mathf.Max(0, charges - amount);
        }

        /// <summary>A fed hit: the charge clock starts again and chargesPerHit are added up to the most. Returns how many were added.</summary>
        public int Fed()
        {
            Settle();
            int before = charges;
            charges = Mathf.Min(Props.maxCharges, charges + Props.chargesPerHit);
            chargeTick = Find.TickManager.TicksGame;
            int gained = charges - before;
            if (gained > 0)
            {
                gainTick = chargeTick;
                gainAmount = gained;
            }
            // The picture: after Shark Skin, each drink wraps one row of scales back.
            if (rewrap >= 0f && !SharkSkinActive)
            {
                rewrap += SamehadaGraphics.RowStep;
                float natural = SamehadaGraphics.BandagedAt(charges, Props.maxCharges) * SamehadaGraphics.BladeLength(charges);
                if (rewrap >= natural) rewrap = -1f;
            }
            return gained;
        }

        public bool SharkSkinActive => Find.TickManager.TicksGame < sharkSkinUntil;

        public int SharkSkinTicksLeft => Mathf.Max(0, sharkSkinUntil - Find.TickManager.TicksGame);

        public void StartSharkSkin(int ticks)
        {
            sharkSkinUntil = Find.TickManager.TicksGame + ticks;
            rewrap = 0f;
        }

        public void EndSharkSkin() => sharkSkinUntil = -1;

        // ------------------------------------------------------------------ the picture's state

        /// <summary>Charges as the blade is drawn: a charge being drunk in grows over the drain.</summary>
        public float ShownCharges
        {
            get
            {
                float age = (Find.TickManager.TicksGame - gainTick) / 60f;
                if (age >= SamehadaFeedTiming.Drain) return Charges;
                return Mathf.Max(0f, Charges - gainAmount * (1f - Mathf.Clamp01(age / SamehadaFeedTiming.Drain)));
            }
        }

        /// <summary>The pale blue light while the blade drinks.</summary>
        public float Hot
        {
            get
            {
                float age = (Find.TickManager.TicksGame - gainTick) / 60f;
                return age < 0f || age >= SamehadaFeedTiming.Drain ? 0f : Mathf.Max(0f, Mathf.Sin(age / SamehadaFeedTiming.Drain * Mathf.PI));
            }
        }

        /// <summary>Shark Skin's spread scales: 1 while it lasts, falling flat over its last quarter second.</summary>
        public float Flare => SharkSkinActive ? Mathf.Clamp01(SharkSkinTicksLeft / (60f * SamehadaSharkSkinTiming.Flare)) : 0f;

        /// <summary>The picture's torn share of the wrap at <paramref name="shownCharges"/> (the sketch's tear).</summary>
        public float TearAt(float shownCharges)
        {
            if (rewrap < 0f) return 0f;
            if (SharkSkinActive) return 1f;
            float natural = SamehadaGraphics.BandagedAt(Mathf.Clamp(shownCharges, 0f, Props.maxCharges), Props.maxCharges) * SamehadaGraphics.BladeLength(shownCharges);
            return natural <= 0.001f ? 0f : 1f - Mathf.Clamp01(rewrap / natural);
        }

        // ------------------------------------------------------------------ equipping

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            pawn?.MapHeld?.GetComponent<MapComponent_Samehada>()?.Register(pawn);
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
            if (holder != null && holder.IsColonistPlayerControlled) yield return new Gizmo_SamehadaCharges(this);
        }

        public override string CompInspectStringExtra() =>
            "Charges: " + Charges + " / " + Props.maxCharges + (SharkSkinActive ? " (Shark Skin " + SharkSkinTicksLeft.ToStringSecondsFromTicks("F0") + ")" : "");

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref charges, "charges");
            Scribe_Values.Look(ref chargeTick, "chargeTick");
            Scribe_Values.Look(ref sharkSkinUntil, "sharkSkinUntil", -1);
            Scribe_Values.Look(ref rewrap, "rewrap", -1f);
            grant.ExposeData();
        }
    }

    /// <summary>The charge readout in the command bar: one slot per charge, and Shark Skin's time left.</summary>
    [StaticConstructorOnStartup]
    public sealed class Gizmo_SamehadaCharges : Gizmo
    {
        private static readonly Texture2D Full = SolidColorMaterials.NewSolidColorTexture(new Color(0.55f, 0.78f, 1f));
        private static readonly Texture2D Empty = SolidColorMaterials.NewSolidColorTexture(new Color(0.12f, 0.14f, 0.22f));
        private static readonly Texture2D Flared = SolidColorMaterials.NewSolidColorTexture(new Color(0.66f, 0.36f, 0.70f));
        private readonly CompSamehada blade;

        public Gizmo_SamehadaCharges(CompSamehada blade)
        {
            this.blade = blade;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            CompProperties_Samehada props = blade.Props;
            int charges = blade.Charges, most = Mathf.Max(1, props.maxCharges);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), blade.SharkSkinActive ? "Shark Skin " + blade.SharkSkinTicksLeft.ToStringSecondsFromTicks("F0") : "Charges");
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), charges + " / " + most);
            Text.Anchor = TextAnchor.UpperLeft;
            float gap = 3f, w = (inner.width - gap * (most - 1)) / most;
            for (int i = 0; i < most; i++)
            {
                var slot = new Rect(inner.x + i * (w + gap), inner.y + 30f, w, 22f);
                GUI.DrawTexture(slot, i < charges ? (blade.SharkSkinActive ? Flared : Full) : Empty);
            }
            TooltipHandler.TipRegion(rect, "Charges " + charges + " / " + most + ". Each melee hit on a living creature adds " + props.chargesPerHit
                + ", heals the wielder " + props.healPerHit.ToString("0.#") + " and drains the target (one Drained stack, up to " + props.maxDrainedStacks
                + ", for " + props.drainedSeconds.ToString("0") + " s). Each charge adds " + props.damagePerCharge.ToString("0.#")
                + " melee damage. One charge is lost after " + props.secondsPerChargeLost.ToString("0") + " s without a hit.");
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
