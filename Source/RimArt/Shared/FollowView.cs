using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The player's view across a move between maps. Made before the move: if the player was looking at the map
    /// the pawns leave, <see cref="Follow"/> takes the camera to where they went and keeps the moved pawns selected.
    /// </summary>
    public readonly struct FollowView
    {
        /// <summary>The player was looking at the map the pawns leave.</summary>
        public readonly bool watching;
        private readonly HashSet<Pawn> selected;

        public FollowView(Map leaving)
        {
            watching = Find.CurrentMap == leaving;
            selected = new HashSet<Pawn>(Find.Selector.SelectedPawns);
        }

        public void Follow(GlobalTargetInfo look, IEnumerable<Pawn> moved)
        {
            if (!watching) return;
            CameraJumper.TryJump(look);
            foreach (Pawn p in moved) if (selected.Contains(p)) Find.Selector.Select(p, false, false);
        }
    }
}
