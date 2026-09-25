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
    /// Each cooldown is kept here too, as CompPowerPole keeps the pole's, so dropping the pipe and
    /// picking it up again does not skip one.
    /// </summary>
    public class CompBubblePipe : ThingComp
    {
        /// <summary>Soap in blows; below zero means a pipe that has never been used, which is full.</summary>
        private float soap = -1f;
        private Dictionary<AbilityDef, int> readyAtTick = new Dictionary<AbilityDef, int>();
        private List<AbilityDef> scribeDefs;
        private List<int> scribeTicks;

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
            if (pawn?.abilities == null || Props.abilities == null) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < Props.abilities.Count; i++)
            {
                AbilityDef def = Props.abilities[i];
                pawn.abilities.GainAbility(def);
                if (!readyAtTick.TryGetValue(def, out int ready) || ready <= now) continue;
                pawn.abilities.GetAbility(def, true)?.StartCooldown(ready - now);
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (pawn?.abilities == null || Props.abilities == null) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < Props.abilities.Count; i++)
            {
                AbilityDef def = Props.abilities[i];
                Ability ability = pawn.abilities.GetAbility(def, true);
                if (ability != null && ability.CooldownTicksRemaining > 0) readyAtTick[def] = now + ability.CooldownTicksRemaining;
                else readyAtTick.Remove(def);

                if (!TraitAbilityUtility.GrantedByOtherSource(pawn, def, null, this))
                    pawn.abilities.RemoveAbility(def);
            }
        }

        public override string CompInspectStringExtra() => "Soap: " + LabelRemaining + " blows";

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref soap, "soap", -1f);
            Scribe_Collections.Look(ref readyAtTick, "readyAtTick", LookMode.Def, LookMode.Value, ref scribeDefs, ref scribeTicks);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readyAtTick == null) readyAtTick = new Dictionary<AbilityDef, int>();
        }
    }
}
