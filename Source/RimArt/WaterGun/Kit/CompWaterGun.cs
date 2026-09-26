using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_WaterGun : CompProperties
    {
        public List<AbilityDef> abilities;
        /// <summary>Units the bag holds. A new gun comes full.</summary>
        public int capacity = 30;
        /// <summary>Units a second while the holder stands on or next to a water cell.</summary>
        public float refillPerSecondNearWater = 10f;
        /// <summary>Units a second while the holder stands unroofed in rain (1 unit per 5 s).</summary>
        public float refillPerSecondInRain = 0.2f;
        /// <summary>The map's rain rate must be at least this for rain to count.</summary>
        public float minRainRate = 0.1f;

        public CompProperties_WaterGun()
        {
            compClass = typeof(CompWaterGun);
        }
    }

    /// <summary>
    /// The water gun: grants Stream Shot and Hydro Pump to whoever holds it, and holds the water bag
    /// they spend, as CompTagScroll holds its tags. The bag refills on its own (MapComponent_WaterGun,
    /// once a second): fast next to water, slowly in rain, not at all on a dry map. The bag is drawn
    /// on the holder's back with its level showing (WaterGunGraphics.IdleBag).
    ///
    /// <see cref="ItemAbilityGrant"/> keeps the cooldowns on the gun.
    /// </summary>
    public class CompWaterGun : ThingComp
    {
        private float water = -1f;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_WaterGun Props => (CompProperties_WaterGun)props;

        public static CompWaterGun HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompWaterGun>();

        /// <summary>Water in the bag, with the fraction a slow refill has built up.</summary>
        public float Water => water < 0f ? Props.capacity : water;
        /// <summary>Whole units, what a cast can spend.</summary>
        public int Units => Mathf.FloorToInt(Water + 0.0001f);
        public string LabelRemaining => Units + " / " + Props.capacity;
        /// <summary>The fill level as the picture draws it: units out of the picture's 30-unit bag.</summary>
        public float DrawnUnits => Water / Mathf.Max(1, Props.capacity) * WaterGunGraphics.BagCap;

        public void Spend(int units) => water = Mathf.Max(0f, Water - units);

        public void Add(float units) => water = Mathf.Min(Props.capacity, Water + units);

        public void Fill() => water = Props.capacity;

        public override void PostPostMake()
        {
            base.PostPostMake();
            water = Props.capacity;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            pawn?.MapHeld?.GetComponent<MapComponent_WaterGun>()?.Register(pawn);
            grant.Give(pawn, Props.abilities);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            grant.Take(pawn, Props.abilities, this);
        }

        public override string CompInspectStringExtra() => "Water: " + LabelRemaining;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref water, "water", -1f);
            grant.ExposeData();
        }
    }
}
