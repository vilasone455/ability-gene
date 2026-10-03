using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using Inside = RimArt.UnlimitedVoidInsideTiming;

namespace RimArt
{
    /// <summary>
    /// Makes and removes Unlimited Void's pocket map. The cast (<see cref="UnlimitedVoidCast"/>) moves everyone
    /// in and out itself. Removal runs as a long event, never from inside a map's own update (as
    /// UnlimitedBladeWorksMap).
    /// </summary>
    public static class UnlimitedVoidMap
    {
        /// <summary>Makes a <paramref name="size"/> x <paramref name="size"/> void beside <paramref name="source"/>. Null if the game would not make it.</summary>
        public static Map Make(Map source, int size)
        {
            Map map = PocketMapUtility.GeneratePocketMap(new IntVec3(size, 1, size), VoidDefOf.AG_UnlimitedVoid, null, source);
            if (map == null) return null;
            map.GetComponent<MapComponent_UnlimitedVoid>().source = source;
            return map;
        }

        /// <summary>Takes the camera back to the map the void was opened from if it is on the void, then removes the void and everything still in it.</summary>
        public static void Close(Map map)
        {
            if (map == null || !Find.Maps.Contains(map)) return;
            Map home = map.GetComponent<MapComponent_UnlimitedVoid>()?.source;
            if (home == null || !Find.Maps.Contains(home)) home = Find.AnyPlayerHomeMap;
            if (home != null && home != map && Find.CurrentMap == map) CameraJumper.TryJump(new GlobalTargetInfo(home.Center, home));
            PocketMapUtility.DestroyPocketMap(map);
        }

        /// <summary>Close, as a long event: from a map's own update or tick the map must not be removed in place.</summary>
        public static void CloseLater(Map map) =>
            LongEventHandler.QueueLongEvent(() => Close(map), "AG_UnlimitedVoidClosing", false, null);
    }

    /// <summary>
    /// Builds the void's body, not its look: the void floor on every cell (walkable, nothing can be built on it;
    /// its texture is only a fallback under <see cref="MapComponent_UnlimitedVoid"/>'s drawing), thick roof
    /// everywhere and an unseen white light every <see cref="lightSpacing"/> cells, so the light is the same at
    /// every hour and the space keeps its colours, and no fog.
    /// </summary>
    public class GenStep_UnlimitedVoid : GenStep
    {
        public TerrainDef floor;
        /// <summary>An unseen glower, placed on a grid over the whole map.</summary>
        public ThingDef light;
        public int lightSpacing = 6;

        public override int SeedPart => 0x6070F0;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;
            RoofDef roof = RoofDefOf.RoofRockThick;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (floor != null) map.terrainGrid.SetTerrain(cell, floor);
                map.roofGrid.SetRoof(cell, roof);
            }
            if (light != null && lightSpacing > 0)
            {
                int first = lightSpacing / 2;
                for (int x = first; x < map.Size.x; x += lightSpacing)
                    for (int z = first; z < map.Size.z; z += lightSpacing)
                        GenSpawn.Spawn(ThingMaker.MakeThing(light), new IntVec3(x, 0, z), map);
            }
            // A pocket map generated behind the player's back would otherwise open fogged.
            map.fogGrid.ClearAllFog();
            map.GetComponent<MapComponent_UnlimitedVoid>().isVoid = true;
        }
    }

    /// <summary>
    /// Unlimited Void on its own pocket map: draws the inside picture (<see cref="UnlimitedVoidInsideGraphics"/>)
    /// as the map's look at <see cref="VoidLayers.Pocket"/>, over the terrain and under everything on it, round
    /// the middle where Gojo lands; plays the camera push the cast started; and draws the spared pawns' blue ring.
    /// The clock is game time since the take (<see cref="PictureClock"/>), so it stops when the game is paused; the
    /// collapse starts when the cast ends the domain. Every map has one of these (vanilla makes every
    /// MapComponent everywhere); it does nothing unless its map is a void and the map on screen.
    /// </summary>
    public sealed class MapComponent_UnlimitedVoid : MapComponent
    {
        public bool isVoid;
        public Map source;
        /// <summary>The picture's own clock for a void no domain holds (it then stands open with no end).</summary>
        private float seconds;
        private float reach = -1f;

        public MapComponent_UnlimitedVoid(Map map) : base(map) { }

        public override void MapComponentUpdate()
        {
            if (!isVoid || Find.CurrentMap != map) return;
            UnlimitedVoidCast cast = GameComponent_UnlimitedVoid.Instance?.ForPocket(map);
            var o = new Vector2(map.Size.x / 2 + 0.5f, map.Size.z / 2 + 0.5f);
            float s, hold = float.PositiveInfinity;
            if (cast != null && cast.takeTick >= 0)
            {
                s = PictureClock.Since(cast.takeTick);
                if (cast.endTick >= 0) hold = (cast.endTick - cast.takeTick) / 60f;
            }
            else s = seconds += Time.unscaledDeltaTime;

            // White from the full white of the collapse until the map is gone.
            if (s >= hold + Inside.Collapse)
            {
                GojoGraphics.WhiteView(1f);
                return;
            }
            ref float r = ref (cast != null ? ref cast.reach : ref reach);
            if (r < 0f) r = VoidSpaceGraphics.ReachFor(Inside.PointFor(o));
            cast?.push?.Apply(o, s);
            UnlimitedVoidInsideGraphics.Draw(o, r, VoidLayers.Pocket, s, map, hold);
            if (cast != null) DrawSpared(cast);
        }

        /// <summary>
        /// The lab's touchPulse on every spared pawn (lib/unlimited-void.js): a blue ring opening from the head
        /// and a glow, 0.5 s, then a faint blue ring left on the ground under it for the rest of the domain.
        /// </summary>
        private void DrawSpared(UnlimitedVoidCast cast)
        {
            foreach (VoidTaken t in cast.taken)
            {
                if (!t.Spared || t.pawn == null || !t.pawn.Spawned || t.pawn.Map != map) continue;
                Vector3 at = t.pawn.DrawPos;
                var pos = new Vector2(at.x, at.z);
                float age = PictureClock.Since(t.sparedTick);
                if (age < 0.5f)
                {
                    float f = age / 0.5f;
                    PaperBombGraphics.RingAt(new Vector2(pos.x, pos.y + 0.35f), 0.2f + 0.8f * f, Fade(GojoGraphics.EyeBlue, 0.9f * (1f - f)), Overhead + 0.05f, true, whiteGlow);
                    Sprite(new Vector2(pos.x, pos.y + 0.4f), 0.9f, 0.9f, Fade(GojoGraphics.EyeBlue, 0.5f * (1f - f)), glow, Overhead + 0.049f);
                }
                PaperBombGraphics.RingAt(pos, 0.38f, Fade(GojoGraphics.EyeBlue, 0.35f * Mathf.Clamp01(age / 0.3f)), Floor + 0.02f, false, whiteGlow);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isVoid, "gojoVoid");
            Scribe_References.Look(ref source, "gojoVoidSource");
        }
    }
}
