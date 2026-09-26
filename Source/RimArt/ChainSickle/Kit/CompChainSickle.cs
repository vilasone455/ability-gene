using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_ChainSickle : CompProperties
    {
        public List<AbilityDef> abilities;

        /// <summary>The chain's reach, cells: a snag lets go, and a pin ends, when holder and target are further apart.</summary>
        public float chainLength = 7f;
        /// <summary>Seconds a snagged pawn stays snagged after the reel if it is not staked.</summary>
        public float snagSeconds = 10f;

        // The weight rule (ChainSickleRule): ratio = holder carrying capacity / (target mass + gear).
        /// <summary>Cells Snag pulls at ratio 1, and at most.</summary>
        public float fullPullCells = 5f;
        /// <summary>Seconds the reel takes at ratio 1; it takes this / ratio.</summary>
        public float reelSeconds = 1f;
        public float maxReelSeconds = 2f;
        /// <summary>Below this ratio the target is too heavy: the holder is dragged instead, and Stake is refused.</summary>
        public float dragRatio = 0.4f;
        /// <summary>Cells and seconds the holder is dragged toward a too-heavy target.</summary>
        public float dragCells = 2f;
        public float dragSeconds = 1f;
        /// <summary>Stake's pin at ratio 1, and at most, seconds.</summary>
        public float maxPinSeconds = 6f;

        /// <summary>The sickle's melee damage on a staked pawn is multiplied by this.</summary>
        public float stakedMeleeFactor = 1.5f;

        public CompProperties_ChainSickle()
        {
            compClass = typeof(CompChainSickle);
        }

        public ChainSickleNumbers Numbers => new ChainSickleNumbers
        {
            FullPull = fullPullCells, ReelSeconds = reelSeconds, MaxReel = maxReelSeconds, DragRatio = dragRatio,
            DragBack = dragCells, DragTime = dragSeconds, MaxPin = maxPinSeconds,
        };
    }

    /// <summary>
    /// The chain sickle: grants Snag and Stake to whoever holds it, keeps their cooldowns on the
    /// weapon (as CompPowerPole does), and holds the snag: the pawn at the other end of the chain, and
    /// whether it is staked. The link is saved with the weapon; MapComponent_ChainSickle checks it
    /// every tick, draws it and ends it.
    /// </summary>
    public class CompChainSickle : ThingComp
    {
        private Dictionary<AbilityDef, int> readyAtTick = new Dictionary<AbilityDef, int>();
        private List<AbilityDef> scribeDefs;
        private List<int> scribeTicks;

        /// <summary>The snagged pawn, or null.</summary>
        public Pawn snagged;
        /// <summary>The snag lets go at this tick unless the pawn is staked; -1 while the reel is still running.</summary>
        public int snagEndsTick = -1;
        public bool staked;
        /// <summary>The pin ends at this tick.</summary>
        public int pinEndsTick = -1;

        public CompProperties_ChainSickle Props => (CompProperties_ChainSickle)props;

        public static CompChainSickle HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompChainSickle>();

        /// <summary>Who holds this sickle, in or out of a flyer.</summary>
        public Pawn Holder => (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;

        public bool Snags(Pawn pawn) => pawn != null && snagged == pawn;

        public void StartSnag(Pawn target)
        {
            snagged = target;
            snagEndsTick = -1;
            staked = false;
            pinEndsTick = -1;
        }

        /// <summary>The reel is over: the snag's own clock starts.</summary>
        public void ReelDone(int now)
        {
            if (snagged != null && !staked) snagEndsTick = now + Mathf.RoundToInt(Props.snagSeconds * 60f);
        }

        public void StartPin(int now, float seconds)
        {
            staked = true;
            pinEndsTick = now + Mathf.RoundToInt(seconds * 60f);
        }

        public void ClearSnag()
        {
            snagged = null;
            snagEndsTick = -1;
            staked = false;
            pinEndsTick = -1;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
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
            // Putting the sickle down lets go of the chain.
            if (snagged != null) MapComponent_ChainSickle.Release(pawn, this, "the chain sickle was put down");
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

        public override string CompInspectStringExtra()
        {
            if (snagged == null) return null;
            return (staked ? "Staked: " : "Snagged: ") + snagged.LabelShortCap;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref readyAtTick, "readyAtTick", LookMode.Def, LookMode.Value, ref scribeDefs, ref scribeTicks);
            Scribe_References.Look(ref snagged, "snagged");
            Scribe_Values.Look(ref snagEndsTick, "snagEndsTick", -1);
            Scribe_Values.Look(ref staked, "staked");
            Scribe_Values.Look(ref pinEndsTick, "pinEndsTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readyAtTick == null) readyAtTick = new Dictionary<AbilityDef, int>();
        }
    }
}
