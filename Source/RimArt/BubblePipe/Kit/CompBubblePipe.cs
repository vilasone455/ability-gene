using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_BubblePipe : CompProperties
    {
        public List<AbilityDef> abilities;
        /// <summary>Blows the soap jar holds. A new pipe comes full.</summary>
        public int maxBlows = 10;
        /// <summary>Blows put back per second while the holder stands on or next to a water cell.</summary>
        public float refillPerSecond = 1f;

        public CompProperties_BubblePipe()
        {
            compClass = typeof(CompBubblePipe);
        }
    }

    /// <summary>
    /// The bubble pipe: grants its two abilities to whoever holds it, and holds the soap jar they
    /// spend. The jar is part of the pipe, not a separate item: its soap is counted here, it is drawn
    /// on the holder's hip with its level showing (MapComponent_BubblePipe), and it refills at water
    /// (MapComponent_BubblePipe's refill pass), so there is no ammunition and no reload job.
    ///
    /// <see cref="ItemAbilityGrant"/> keeps the cooldowns on the pipe.
    /// </summary>
    public class CompBubblePipe : ThingComp
    {
        /// <summary>Soap in blows; below zero means a pipe that has never been used, which is full.</summary>
        private float soap = -1f;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompProperties_BubblePipe Props => (CompProperties_BubblePipe)props;

        public static CompBubblePipe HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompBubblePipe>();

        public Pawn Holder => (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;

        public float Soap => soap < 0f ? Props.maxBlows : soap;

        /// <summary>Whole blows in the jar.</summary>
        public int Blows => Mathf.FloorToInt(Soap + 0.0001f);

        public bool Full => Soap >= Props.maxBlows;

        public string LabelRemaining => Blows + " / " + Props.maxBlows;

        public void Spend(int blows) => soap = Mathf.Max(0f, Soap - blows);

        public void Refill(float blows) => soap = Mathf.Min(Props.maxBlows, Soap + blows);

        /// <summary>Test tools: set the jar to this many blows.</summary>
        public void SetBlows(float blows) => soap = Mathf.Clamp(blows, 0f, Props.maxBlows);

        public override void PostPostMake()
        {
            base.PostPostMake();
            soap = Props.maxBlows;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            pawn?.MapHeld?.GetComponent<MapComponent_BubblePipe>()?.Track(pawn);
            grant.Give(pawn, Props.abilities);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            grant.Take(pawn, Props.abilities, this);
        }

        public override string CompInspectStringExtra() => "Soap: " + LabelRemaining + " blows";

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref soap, "soap", -1f);
            grant.ExposeData();
        }
    }
}
