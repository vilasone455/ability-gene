using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Every blade currently standing, and who it came out of.
    ///
    /// Self-healing in the same way <see cref="TimeAlterRegistry"/> is: blades add themselves on
    /// spawn and take themselves out on despawn, and anything that got lost in between - a map
    /// discarded, a save reloaded, a blade destroyed by something that did not go through
    /// DeSpawn - is pruned on the next read rather than tracked with a callback that may not
    /// fire.
    ///
    /// It exists so the debt can be recomputed without walking a map, and so loose does not have
    /// to ask the game what is standing near a cell.
    /// </summary>
    public static class PanoplyRegistry
    {
        private static readonly List<PlantedBlade> blades = new List<PlantedBlade>();

        public static void Register(PlantedBlade blade)
        {
            if (blade == null || blades.Contains(blade)) return;
            blades.Add(blade);
            PanoplyUtility.UpdateDebt(blade.Owner);
        }

        public static void Deregister(PlantedBlade blade)
        {
            if (blade == null) return;
            if (blades.Remove(blade)) PanoplyUtility.UpdateDebt(blade.Owner);
        }

        /// <summary>Live blades belonging to one carrier. Prunes as it goes.</summary>
        public static List<PlantedBlade> BladesOf(Pawn owner)
        {
            List<PlantedBlade> found = new List<PlantedBlade>();
            if (owner == null) return found;

            for (int i = blades.Count - 1; i >= 0; i--)
            {
                PlantedBlade blade = blades[i];
                if (blade == null || blade.Destroyed || !blade.Spawned)
                {
                    blades.RemoveAt(i);
                    continue;
                }
                if (blade.Owner == owner) found.Add(blade);
            }
            return found;
        }

        public static int CountOf(Pawn owner)
        {
            return BladesOf(owner).Count;
        }
    }
}
