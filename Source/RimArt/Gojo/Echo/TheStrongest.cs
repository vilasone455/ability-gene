using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The Strongest's age limits, on the trait (1.6/Defs/TraitDefs/AG_Gojo.xml). Young is a child, or younger than
    /// <see cref="youngShare"/> of the race's life expectancy (24 for a human); old is past <see cref="oldShare"/>
    /// (56 for a human). The opinion and mood numbers are on the thoughts (AG_Gojo_Thoughts.xml).
    /// </summary>
    public class StrongestExtension : DefModExtension
    {
        public float youngShare = 0.3f;
        public float oldShare = 0.7f;
    }

    [DefOf]
    public static class GojoEchoDefOf
    {
        public static TraitDef AG_TheStrongest;

        static GojoEchoDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(GojoEchoDefOf));
    }

    /// <summary>Who The Strongest counts as young or old: humanlike pawns of the holder's own faction.</summary>
    public static class TheStrongest
    {
        private static StrongestExtension Ages =>
            GojoEchoDefOf.AG_TheStrongest.GetModExtension<StrongestExtension>() ?? new StrongestExtension();

        public static bool Has(Pawn pawn) => pawn?.story?.traits?.HasTrait(GojoEchoDefOf.AG_TheStrongest) == true;

        /// <summary>A living humanlike of <paramref name="holder"/>'s faction other than the holder.</summary>
        public static bool SameColony(Pawn holder, Pawn other) =>
            other != holder && other.RaceProps.Humanlike && !other.Dead && other.Faction != null && other.Faction == holder.Faction;

        public static bool IsYoung(Pawn pawn) =>
            !pawn.DevelopmentalStage.Adult()
            || pawn.ageTracker.AgeBiologicalYearsFloat < Ages.youngShare * pawn.RaceProps.lifeExpectancy;

        public static bool IsOld(Pawn pawn) =>
            pawn.DevelopmentalStage.Adult()
            && pawn.ageTracker.AgeBiologicalYearsFloat > Ages.oldShare * pawn.RaceProps.lifeExpectancy;
    }

    /// <summary>The Strongest's opinion of a young colonist. The holder's trait is the thought's requiredTraits.</summary>
    public class ThoughtWorker_StrongestOfYoung : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other) =>
            TheStrongest.SameColony(p, other) && TheStrongest.IsYoung(other);
    }

    /// <summary>The Strongest's opinion of an old colonist.</summary>
    public class ThoughtWorker_StrongestOfOld : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other) =>
            TheStrongest.SameColony(p, other) && TheStrongest.IsOld(other);
    }

    /// <summary>A young colonist's opinion of a colonist with The Strongest.</summary>
    public class ThoughtWorker_YoungOfStrongest : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other) =>
            TheStrongest.Has(other) && TheStrongest.SameColony(p, other) && TheStrongest.IsYoung(p);
    }

    /// <summary>An old colonist's opinion of a colonist with The Strongest.</summary>
    public class ThoughtWorker_OldOfStrongest : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other) =>
            TheStrongest.Has(other) && TheStrongest.SameColony(p, other) && TheStrongest.IsOld(p);
    }

    /// <summary>
    /// The Strongest's mood: stage 0 while no other Host is awakened in the colony, stage 1 once another is. A Host is
    /// a pawn whose Echo is awakened (manifested or not); the holder does not count himself.
    /// </summary>
    public class ThoughtWorker_StrongestAlone : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            if (echoes == null) return ThoughtState.Inactive;
            int others = echoes.HostCount - (echoes.HostRecord(p) != null ? 1 : 0);
            return ThoughtState.ActiveAtStage(others > 0 ? 1 : 0);
        }
    }
}
