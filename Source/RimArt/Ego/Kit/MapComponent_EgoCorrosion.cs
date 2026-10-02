using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws each weapon's corroded look (<see cref="EgoCorrosionAction.DrawCorroded"/>) over every pawn on the shown map
    /// that is corroded or overclocking. It scans the spawned pawns each frame instead of keeping a list, so nothing has
    /// to be rebuilt after a load. Its tick corrodes the pawns whose roll passed once their burst is over
    /// (<see cref="EgoCorrosion.Tick"/>).
    /// </summary>
    public class MapComponent_EgoCorrosion : MapComponent
    {
        public MapComponent_EgoCorrosion(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            EgoCorrosion.Tick(map);
        }

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap) return;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.MentalState is MentalState_EgoCorroded state)
                {
                    CompEgoWeapon comp = state.Comp;
                    comp?.Props.Action.DrawCorroded(pawn, comp, state.Seconds, overclock: false);
                }
                else if (pawn.jobs?.curDriver is JobDriver_EgoOverclock overclock)
                {
                    CompEgoWeapon comp = overclock.Weapon;
                    comp?.Props.Action.DrawCorroded(pawn, comp, overclock.Seconds, overclock: true);
                }
            }
        }
    }
}
