using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Melee Animation (optional) starts executions and grapples on its own when a melee attack lands, and locks
    /// both pawns in its animation job until it ends. An intangible Obito is neither: nothing can take hold of
    /// him, and he cannot take hold of anyone. The mod keeps public lists of predicates for exactly this
    /// (<c>AM.Controller.ActionController</c>); one is added to each at startup when the mod is loaded.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class KamuiMeleeAnimation
    {
        internal static readonly string[] Lists = { "CanBeExecutedPredicates", "CanBeGrappledPredicates", "CanExecutePredicates" };

        /// <summary>How many of the lists took the rule (3 with Melee Animation loaded, 0 without), for the tests.</summary>
        internal static int Registered { get; private set; }

        static KamuiMeleeAnimation()
        {
            Type controller = AccessTools.TypeByName("AM.Controller.ActionController");
            if (controller == null) return;
            foreach (string name in Lists)
            {
                if (!(AccessTools.Field(controller, name)?.GetValue(null) is List<Predicate<Pawn>> list))
                {
                    Log.Warning("[RimArt] Melee Animation's ActionController." + name + " is gone; a phased Obito can be pulled into its animations.");
                    continue;
                }
                list.Add(pawn => KamuiPhaseRegistry.Intangible(pawn) == null);
                Registered++;
            }
        }
    }
}
