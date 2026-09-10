using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Holds the flocks currently in the air on this map and ticks them.
    ///
    /// Deliberately not scribed, unlike <see cref="MapComponent_Arc"/>. An arc is a sequence
    /// being played at a pawn that has to survive a save taken in the middle of it; a flock is
    /// a picture of something that has already finished happening, and the carrier is standing
    /// at the far end of it either way.
    /// </summary>
    public class MapComponent_Dispersal : MapComponent
    {
        private readonly List<ScatterFlight> flights = new List<ScatterFlight>();

        public MapComponent_Dispersal(Map map) : base(map) { }

        public static void Begin(Map map, Vector3 from, Vector3 to)
        {
            MapComponent_Dispersal component = map?.GetComponent<MapComponent_Dispersal>();
            if (component == null) return;

            component.flights.Add(new ScatterFlight(from, to));
        }

        public override void MapComponentTick()
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                if (!flights[i].Tick(map)) flights.RemoveAt(i);
            }
        }

        public override void MapComponentUpdate()
        {
            for (int i = 0; i < flights.Count; i++)
            {
                flights[i].Draw();
            }
        }
    }
}
