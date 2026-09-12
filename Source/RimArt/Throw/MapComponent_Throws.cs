using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Holds the grenades mid-wind-up on this map and launches each one when its thrower's hand
    /// opens.
    ///
    /// A map component for the same reason the arc uses one: a throw in progress is not a state
    /// the thrower is in, it is a second of sequence that has to survive them being shot, and
    /// there is nowhere on the pawn to hang it that outlives the cast.
    ///
    /// Nothing here is specific to any one grenade. <see cref="Begin"/> takes a projectile def, so
    /// a second thrown thing - or a patch that puts this animation on the game's own grenades -
    /// is a caller, not a change to this file.
    /// </summary>
    public class MapComponent_Throws : MapComponent
    {
        private List<PendingThrow> pending = new List<PendingThrow>();

        public MapComponent_Throws(Map map) : base(map) { }

        /// <summary>
        /// Starts one throw: plays the animation if Melee Animation is loaded, and launches the
        /// projectile when the hand opens.
        ///
        /// With no animation the grenade is launched on the spot, which is what every thrown
        /// weapon in the game does and is the behaviour this mod has when their mod is absent.
        /// </summary>
        public static void Begin(Pawn thrower, LocalTargetInfo target, ThingDef projectile, string handTexture)
        {
            Map map = thrower?.Map;
            if (map == null || projectile == null || !target.IsValid) return;

            MapComponent_Throws component = map.GetComponent<MapComponent_Throws>();
            if (component == null) return;

            int delay = 0;
            if (ThrowAnimation.TryThrow(thrower, target.Cell, handTexture, out ThrowAnimation.Throw thrown))
                delay = thrown.ReleaseTick;

            component.pending.Add(new PendingThrow(thrower, target, projectile,
                                                   Find.TickManager.TicksGame + delay));
        }

        public override void MapComponentTick()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (!pending[i].Tick(map)) pending.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "pending", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pending == null)
                pending = new List<PendingThrow>();
        }
    }
}
