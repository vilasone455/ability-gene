using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One hero a colonist can become (docs/hero-echo.md). The label is the hero's real name;
    /// subtitle is an optional epithet shown under it. Every number here is balance, so it lives in
    /// the XML: upkeep and cast costs are charge from the colony's shared pool, wealth is added to
    /// the Host's market value.
    /// </summary>
    public class EchoDef : Def
    {
        public string subtitle;
        public int order;

        /// <summary>What the candidate must meet before the awakening letter is offered.</summary>
        public List<EchoTrial> trials = new List<EchoTrial>();
        /// <summary>Traits the Host gains on awakening. Conflicting traits are replaced.</summary>
        public List<EchoTraitCost> forcedTraits = new List<EchoTraitCost>();

        /// <summary>Granted while manifested, taken back on revert.</summary>
        public List<AbilityDef> abilities = new List<AbilityDef>();
        /// <summary>Stat offsets and the "fight and move only" work block while manifested.</summary>
        public HediffDef manifestHediff;

        public float upkeepPerHour = 10f;
        public List<EchoCastCost> castCosts = new List<EchoCastCost>();
        public float wealth = 3000f;

        /// <summary>Hair colour in hero form. Alpha 0 leaves the colonist's own colour.</summary>
        public Color hairColor = Color.clear;
        /// <summary>
        /// Body in hero form, on adult humans only, so one costume fits every Host. Other races and
        /// null keep the colonist's own body.
        /// </summary>
        public BodyTypeDef bodyType;

        public float CastCost(AbilityDef ability)
        {
            for (int i = 0; i < castCosts.Count; i++)
                if (castCosts[i].ability == ability) return castCosts[i].cost;
            return 0f;
        }

        public string LabelWithSubtitle => subtitle.NullOrEmpty() ? LabelCap.ToString() : LabelCap + " — " + subtitle;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (manifestHediff == null) yield return "manifestHediff is null";
            if (trials.NullOrEmpty()) yield return "no trials";
            if (upkeepPerHour < 0f) yield return "upkeepPerHour is negative";
            foreach (EchoCastCost cost in castCosts)
                if (cost.ability == null || !abilities.Contains(cost.ability))
                    yield return "castCosts names " + cost.ability?.defName + ", which is not in abilities";
            foreach (EchoTrial trial in trials)
                foreach (string error in trial.ConfigErrors())
                    yield return trial.GetType().Name + ": " + error;
        }
    }

    public class EchoTraitCost
    {
        public TraitDef trait;
        public int degree;

        public string LabelFor(int d) => trait.DataAtDegree(d)?.GetLabelCapFor(null) ?? trait.LabelCap.ToString();
        public string Label => LabelFor(degree);
    }

    public class EchoCastCost
    {
        public AbilityDef ability;
        public float cost;
    }
}
