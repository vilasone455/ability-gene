using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// Unlimited Blade Works on its own pocket map: draws the world over the map's terrain with the same
    /// code as the lab's recording (<see cref="UbwWorldGraphics"/>) at the map's own layers, and plays the
    /// world's own timeline. The fire runs out once when the map opens; the world then stands with nothing
    /// on screen but itself until <see cref="Close"/>, when the white closes in behind the wall of fire and
    /// the map is removed. The world's clock is unscaled real time, so the pictures play while the game is
    /// paused, as the castle's do.
    ///
    /// The sun is the sketch's low dusk sun, fixed: the map is roofed and lit by unseen lights, so the
    /// game's own sun says nothing here. Every map has one of these (vanilla makes every MapComponent
    /// everywhere); it does nothing unless its map is a world (<see cref="world"/> is set by the GenStep) and
    /// is the map on screen.
    ///
    /// A world the ability made is <see cref="driven"/>: its clock is game time since everyone was taken
    /// (<see cref="UbwClock"/>), so it stops when the game is paused and keeps step with the home map's ring,
    /// the close is set by the cast (<see cref="CloseAt"/>), and the cast, not this, moves everyone home and
    /// removes the map.
    /// </summary>
    public sealed class MapComponent_UnlimitedBladeWorks : MapComponent
    {
        public bool world;
        /// <summary>The world v2: the plate ground with height and the sky, instead of the flat earth.</summary>
        public bool depth;
        public Map source;
        /// <summary>Made by the ability: the cast runs the clock, the close and the removal.</summary>
        public bool driven;
        /// <summary>The tick everyone was taken in, or -1 before that.</summary>
        private int takenTick = -1;
        /// <summary>The landing spots, in cells from the middle: no sword stands over one.</summary>
        private List<IntVec3> keep = new List<IntVec3>();
        private float seconds;
        /// <summary>When the close began on the world's clock, or -1 while it stands.</summary>
        private float closeAt = -1f;
        private bool closing, shaken;
        private UbwFieldBake bake;
        private UbwTerrainBake terrain;
        /// <summary>The lab scene's shadow vector, made low as the sketch makes it, and its shadow strength.</summary>
        private static readonly Vector2 Sun = new Vector2(-0.45f, -0.32f) * UbwWorldTiming.DuskShadow;
        private const float Strength = 0.32f;
        /// <summary>Under everything, for the far corners at full zoom-out (the generator def turns the grey map-edge frame off).</summary>
        private const float Backstop = 700f;
        private static readonly Color Far = Color.Lerp(UbwGraphics.FarEarth, UbwGraphics.Haze, 0.45f);

        public MapComponent_UnlimitedBladeWorks(Map map) : base(map) { }

        public bool IsWorld => world;

        /// <summary>The middle of the map, where the caster lands.</summary>
        public IntVec3 CentreCell => new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);

        /// <summary>Where the spot <paramref name="i"/> of the landing spots is on this map.</summary>
        public IntVec3 LandingCell(int i) => i >= 0 && i < keep.Count ? CentreCell + keep[i] : CentreCell;

        /// <summary>Called by the GenStep: this map is the world made for these landing spots.</summary>
        public void Begin(List<IntVec3> keepOffsets, bool depth = false)
        {
            world = true;
            this.depth = depth;
            terrain = null;
            keep = keepOffsets ?? new List<IntVec3>();
            seconds = 0f;
            closeAt = -1f;
            closing = false;
            shaken = false;
            bake = null;
        }

        /// <summary>The ability: everyone has landed on these spots (cells from the middle) at this tick; the world's clock starts, and the field is baked again with no sword over them.</summary>
        public void Landed(List<IntVec3> keepOffsets, int tick)
        {
            driven = true;
            keep = keepOffsets ?? new List<IntVec3>();
            takenTick = tick;
            closeAt = -1f;
            shaken = false;
            bake = null;
        }

        /// <summary>The ability: the close begins at <paramref name="worldSeconds"/> on the world's clock, or when the fire has finished running out if that is later. Returns when it begins.</summary>
        public float CloseAt(float worldSeconds)
        {
            if (closeAt < 0f) closeAt = Mathf.Max(worldSeconds, UbwWorldTiming.Swept);
            return closeAt;
        }

        /// <summary>Ends the world: the white closes in behind the wall of fire, then the map is removed.</summary>
        public void Close()
        {
            if (!world || closeAt >= 0f) return;
            closeAt = Mathf.Max(seconds, UbwWorldTiming.Swept);
        }

        private float CloseAtOrNever => closeAt >= 0f ? closeAt : float.PositiveInfinity;

        public override void MapComponentUpdate()
        {
            if (!world || Find.CurrentMap != map) return;
            if (driven) seconds = takenTick < 0 ? 0f : UbwClock.Since(takenTick);
            else seconds += Time.unscaledDeltaTime;
            if (!shaken && seconds >= UbwWorldTiming.Start)
            {
                Find.CameraDriver.shaker.DoShake(UbwWorldTiming.StartShake);
                shaken = true;
            }

            if (bake == null)
            {
                var spots = new UbwXZ[keep.Count];
                for (int i = 0; i < keep.Count; i++) spots[i] = new UbwXZ(keep[i].x, keep[i].z);
                UbwTerrain ground = depth ? UbwTerrainGraphics.For(1) : null;
                bake = UbwWorldGraphics.BakeFor(spots, Sun, ground);
                terrain = ground != null ? UbwTerrainGraphics.BakeFor(ground, Sun) : null;
            }

            var centre = new Vector2(map.Size.x / 2f, map.Size.z / 2f);
            Sprite(centre, Backstop, Backstop, Far, solid, UbwLayers.Pocket.Back - 0.0005f);
            UbwWorldGraphics.Draw(centre, bake, seconds, CloseAtOrNever, UbwLayers.Pocket, Sun, Strength, map, terrain);

            // Past the end of the close: white until the map is gone, and the map goes.
            float end = CloseAtOrNever + UbwWorldTiming.Close + 0.35f;
            if (seconds < end) return;
            Sprite(centre, Backstop, Backstop, UbwGraphics.WhiteHot, solid, Overhead + 0.06f);
            if (closing || driven) return;
            closing = true;
            UnlimitedBladeWorksMap.CloseLater(map);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref world, "ubwWorld");
            Scribe_Values.Look(ref depth, "ubwDepth");
            Scribe_References.Look(ref source, "ubwSource");
            Scribe_Values.Look(ref driven, "ubwDriven");
            Scribe_Values.Look(ref takenTick, "ubwTakenTick", -1);
            Scribe_Collections.Look(ref keep, "ubwKeep", LookMode.Value);
            Scribe_Values.Look(ref seconds, "ubwSeconds");
            Scribe_Values.Look(ref closeAt, "ubwCloseAt", -1f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && keep == null) keep = new List<IntVec3>();
        }
    }
}
