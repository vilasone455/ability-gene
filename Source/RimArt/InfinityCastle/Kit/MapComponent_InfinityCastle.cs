using UnityEngine;
using Verse;
using T = RimArt.InfinityCastleInsideTiming;

namespace RimArt
{
    /// <summary>
    /// Draws the Infinity Castle on its own pocket map, with the same code as the lab's recordings: the
    /// void and its rooms at other depths, the rooms at rest (baked), the lantern glows, and the castle's
    /// timeline at the map's own layers (<see cref="CastleLayers.Pocket"/>). It plays the arrival doors
    /// once when the castle opens, waits in the hold with nothing but the castle on screen, and plays
    /// Release when asked; when Release has faded to black the map is closed.
    ///
    /// Every map has one of these (vanilla makes every MapComponent everywhere); it does nothing unless
    /// its map is a castle (a seed is set) and is the map on screen.
    /// </summary>
    public sealed class MapComponent_InfinityCastle : MapComponent
    {
        public int seed, rooms;
        public Map source;
        private float seconds, releasedAt = -1f;
        private bool closing;
        private CastleLayout castle;

        public MapComponent_InfinityCastle(Map map) : base(map) { }

        public bool IsCastle => seed > 0;
        internal CastleLayout Castle => castle ?? (castle = IsCastle ? CastleRoomGraphics.LayoutFor(seed, rooms) : null);

        /// <summary>The carrier's cell on the dais.</summary>
        public IntVec3 DaisCell
        {
            get
            {
                var (x, z) = CastleLayout.SeatOf(Castle.Biwa);
                return new IntVec3((int)x, 0, (int)z);
            }
        }

        /// <summary>Called by the GenStep: this map is the castle for this seed and room count.</summary>
        public void Begin(int seed, int rooms)
        {
            this.seed = seed;
            this.rooms = rooms;
            castle = null;
            seconds = 0f;
            releasedAt = -1f;
            closing = false;
        }

        /// <summary>Plays Release: every room answers the strum, the doors, the fade; then the map closes.</summary>
        public void Release()
        {
            if (IsCastle && releasedAt < 0f) releasedAt = seconds;
        }

        public override void MapComponentUpdate()
        {
            if (!IsCastle || closing || Find.CurrentMap != map) return;
            // Unscaled, as the previews: the lanterns and the drift go on while the game is paused.
            seconds += Time.unscaledDeltaTime;
            float timeline = releasedAt < 0f ? Mathf.Min(seconds, T.Quiet) : T.Release + (seconds - releasedAt);
            if (timeline >= T.Duration)
            {
                closing = true;
                InfinityCastleMap.CloseLater(map);
                return;
            }
            InfinityCastleInsideGraphics.Draw(Castle, Vector2.zero, timeline, seconds, false, CastleLayers.Pocket, Find.CameraDriver.CurrentViewRect);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref rooms, "rooms");
            Scribe_References.Look(ref source, "source");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // A loaded castle opens in its hold: the arrival doors are not played again.
                castle = null;
                seconds = T.Quiet;
                releasedAt = -1f;
                closing = false;
            }
        }
    }
}
