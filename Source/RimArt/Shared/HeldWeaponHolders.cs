using System;
using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Who holds one kit weapon, on every map, for a GameComponent that draws or charges the weapon itself (Last Prism,
    /// Paradise Lost), so a frame costs one pass over the holders, not over every pawn. The weapon's comp calls
    /// <see cref="Add"/> from Notify_Equipped and <see cref="Remove"/> from Notify_Unequipped. The component calls
    /// <see cref="Rescan"/> in FinalizeInit and every <see cref="RescanEvery"/> ticks: equipment loaded with a save sends no
    /// Notify_Equipped, a pawn can arrive on a map already holding one, and a dead holder is not spawned, so it drops out.
    /// </summary>
    public sealed class HeldWeaponHolders
    {
        /// <summary>Ticks between rescans.</summary>
        public const int RescanEvery = 60;

        private readonly Func<Pawn, bool> holds;
        private readonly HashSet<Pawn> holders = new HashSet<Pawn>();
        private readonly List<Pawn> copy = new List<Pawn>();

        /// <param name="holds">Whether a pawn holds the weapon now: its comp's HeldBy is not null.</param>
        public HeldWeaponHolders(Func<Pawn, bool> holds) => this.holds = holds;

        public void Add(Pawn pawn)
        {
            if (pawn != null) holders.Add(pawn);
        }

        public void Remove(Pawn pawn)
        {
            if (pawn != null) holders.Remove(pawn);
        }

        public bool Contains(Pawn pawn) => holders.Contains(pawn);

        /// <summary>For game tests: forgets every holder (the test's new pawns are added as they equip).</summary>
        public void Clear() => holders.Clear();

        /// <summary>Lets a loop that never changes the set go over the holders without a copy.</summary>
        public HashSet<Pawn>.Enumerator GetEnumerator() => holders.GetEnumerator();

        /// <summary>
        /// The holders copied into one reused list, for a loop whose body can change the set: a draw-time recache can
        /// unequip. Good until the next call.
        /// </summary>
        public List<Pawn> Copy()
        {
            copy.Clear();
            copy.AddRange(holders);
            return copy;
        }

        /// <summary>Every spawned pawn on any map that holds the weapon, and no one else.</summary>
        public void Rescan()
        {
            holders.Clear();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = maps[m].mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                    if (holds(pawns[i])) holders.Add(pawns[i]);
            }
        }
    }
}
