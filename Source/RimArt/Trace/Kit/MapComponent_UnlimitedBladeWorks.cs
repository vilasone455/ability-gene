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
    /// game's own sun says nothing here. The world v4 (<see cref="crest"/>) hangs its backdrop on the
    /// camera, so it is drawn for wherever the player looks. Every map has one of these (vanilla makes every MapComponent
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
        /// <summary>The world v4: the plate ground, the sword crest past the north edge and the sky behind it, instead of the flat earth (v1).</summary>
        public bool crest;
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
        /// <summary>Seconds added to the world's clock (game time since the take): the reveal shot's world time while it plays, then where it ended, so the world stands as the shot left it (the fire already run out).</summary>
        private float offset;
        private bool closing, shaken;
        private UbwFieldBake bake;
        private UbwCrestWorld crestWorld;
        /// <summary>The lab scene's shadow vector, made low as the sketch makes it (v4 its own way), and its shadow strength.</summary>
        private static readonly Vector2 SceneSun = new Vector2(-0.45f, -0.32f);
        private static readonly Vector2 FlatSun = SceneSun * UbwWorldTiming.DuskShadow, CrestSun = UbwCrestWorld.LowSun(SceneSun);
        private const float Strength = 0.32f;
        /// <summary>Under everything, for the far corners at full zoom-out (the generator def turns the grey map-edge frame off).</summary>
        private const float Backstop = 700f;
        private static readonly Color Far = Color.Lerp(UbwGraphics.FarEarth, UbwGraphics.Haze, 0.45f);

        public MapComponent_UnlimitedBladeWorks(Map map) : base(map) { }

        public bool IsWorld => world;

        /// <summary>The world's clock as last drawn (seconds; the fire has run out at <see cref="UbwWorldTiming.Swept"/>).</summary>
        public float WorldSeconds => seconds;

        /// <summary>Where the caster lands: the middle across, <see cref="UbwCrest.North"/> cells south of the north edge.</summary>
        public IntVec3 CentreCell => new IntVec3(map.Size.x / 2, 0, map.Size.z - UbwCrest.North);

        /// <summary>The point the world is drawn round: the caster's cell's corner, as the sketches put it.</summary>
        public Vector2 Origin => new Vector2(map.Size.x / 2f, map.Size.z - UbwCrest.North);

        /// <summary>The reveal shot sets the world's clock to its own world time (seconds); the clock goes on from there.</summary>
        public void RevealAt(float worldSeconds)
        {
            offset = worldSeconds - (driven && takenTick >= 0 ? UbwClock.Since(takenTick) : 0f);
            shaken = true;
        }

        /// <summary>The world v4 for the reveal shot: these landing spots, the world's sun, this screen shape.</summary>
        internal UbwRevealScene RevealScene(float aspect)
        {
            var spots = new UbwXZ[keep.Count];
            for (int i = 0; i < keep.Count; i++) spots[i] = new UbwXZ(keep[i].x, keep[i].z);
            return UbwRevealScene.For(spots, CrestSun, aspect);
        }

        /// <summary>Where the spot <paramref name="i"/> of the landing spots is on this map.</summary>
        public IntVec3 LandingCell(int i) => i >= 0 && i < keep.Count ? CentreCell + keep[i] : CentreCell;

        /// <summary>Called by the GenStep: this map is the world made for these landing spots.</summary>
        public void Begin(List<IntVec3> keepOffsets, bool crest = true)
        {
            world = true;
            this.crest = crest;
            crestWorld = null;
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
            offset = 0f;
            bake = null;
        }

        /// <summary>The ability: the close begins at <paramref name="gameSeconds"/> of game time since the take, or when the fire has finished running out if that is later. Returns when it begins, in game time since the take.</summary>
        public float CloseAt(float gameSeconds)
        {
            if (closeAt < 0f) closeAt = Mathf.Max(gameSeconds + offset, UbwWorldTiming.Swept);
            return closeAt - offset;
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
            if (driven) seconds = (takenTick < 0 ? 0f : UbwClock.Since(takenTick)) + offset;
            else seconds += Time.unscaledDeltaTime;
            if (!shaken && seconds >= UbwWorldTiming.Start)
            {
                Find.CameraDriver.shaker.DoShake(UbwWorldTiming.StartShake);
                shaken = true;
            }

            Vector2 sun = crest ? CrestSun : FlatSun;
            if (bake == null)
            {
                var spots = new UbwXZ[keep.Count];
                for (int i = 0; i < keep.Count; i++) spots[i] = new UbwXZ(keep[i].x, keep[i].z);
                crestWorld = crest ? UbwCrestWorld.For(sun) : null;
                bake = UbwWorldGraphics.BakeFor(spots, sun, crestWorld?.Terrain.Terrain);
            }

            // The caster's cell's corner, as the sketches put the world on a cell's corner.
            var centre = new Vector2(map.Size.x / 2f, map.Size.z - UbwCrest.North);
            Sprite(centre, Backstop, Backstop, Far, solid, UbwLayers.Pocket.Back - 0.0005f);
            UbwWorldGraphics.Draw(centre, bake, seconds, CloseAtOrNever, UbwLayers.Pocket, sun, Strength, map, crestWorld);

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
            Scribe_Values.Look(ref crest, "ubwCrest", true);
            Scribe_References.Look(ref source, "ubwSource");
            Scribe_Values.Look(ref driven, "ubwDriven");
            Scribe_Values.Look(ref takenTick, "ubwTakenTick", -1);
            Scribe_Collections.Look(ref keep, "ubwKeep", LookMode.Value);
            Scribe_Values.Look(ref seconds, "ubwSeconds");
            Scribe_Values.Look(ref closeAt, "ubwCloseAt", -1f);
            Scribe_Values.Look(ref offset, "ubwOffset");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && keep == null) keep = new List<IntVec3>();
        }
    }
}
