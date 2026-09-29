using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Shinra Tensei's picture on the map (<see cref="ShinraDomeGraphics"/>). While a cast charges: the pressed ground,
    /// the size ring, Pain's sleeves, and the light in his palms where the clip draws his hands. From the burst: a
    /// mark per cast that draws the dome and everything after it on game time, and keeps the scoured ground until it
    /// has faded (<see cref="ShinraDome.MarkSeconds"/>), with the pawns it pushed drawn flying to where the game put them
    /// (<see cref="ShinraFlight"/>, posed through <see cref="PoseFlights"/>). Marks are not saved; they are picture only.
    /// Drawing never advances or commits gameplay. All maps share the game-tick controller.
    /// </summary>
    public class MapComponent_ShinraCasts : MapComponent
    {
        private sealed class Mark
        {
            public Vector2 at;
            public IntVec3 cell;
            public ShinraDomeCast cast;
            public ShinraPalette palette;
            public int burstTick;
            public List<ShinraFlight> flights;
        }

        private readonly List<Mark> marks = new List<Mark>();
        private static float[] sizes;

        public MapComponent_ShinraCasts(Map map) : base(map) { }
        public bool Running(Pawn pawn) => GameComponent_Shinra.Instance.For(pawn).active;

        /// <summary>A cast has burst: its dome and ground marks start now, and the pawns it moved fly there.</summary>
        public void Burst(ShinraPawnState s, List<ShinraFlight> flights = null)
        {
            IntVec3 cell = s.centre.ToIntVec3();
            marks.Add(new Mark
            {
                at = new Vector2(s.centre.x, s.centre.z), cell = cell, cast = s.DomeCast,
                palette = Palette(map, cell), burstTick = Find.TickManager.TicksGame, flights = flights,
            });
        }

        /// <summary>
        /// Draws each pushed pawn along its flight this frame. Called by <see cref="GameComponent_Pain"/> right after it
        /// rebuilds the kit's pawn looks, so these are not cleared before the pawns are drawn.
        /// </summary>
        public void PoseFlights()
        {
            foreach (Mark mark in marks)
            {
                if (mark.flights == null) continue;
                float e = Since(mark);
                foreach (ShinraFlight flight in mark.flights) flight.Pose(e, map);
            }
        }

        /// <summary>True while a pushed pawn is still drawn on its way (tests).</summary>
        public bool Flying(Pawn pawn)
        {
            foreach (Mark mark in marks)
                if (mark.flights != null)
                    foreach (ShinraFlight flight in mark.flights)
                        if (flight.pawn == pawn && Since(mark) < flight.hitAt + flight.fly) return true;
            return false;
        }

        /// <summary>The ground's colours under <paramref name="cell"/>: snow, sand, or soil for everything else.</summary>
        public static ShinraPalette Palette(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return ShinraPalette.Soil;
            if (map.snowGrid.GetDepth(cell) > 0.25f) return ShinraPalette.Snow;
            TerrainDef terrain = cell.GetTerrain(map);
            return terrain != null && terrain.defName.Contains("Sand") ? ShinraPalette.Sand : ShinraPalette.Soil;
        }

        private static float Since(Mark mark) => (Find.TickManager.TicksGame - mark.burstTick) / 60f;

        public override void MapComponentUpdate()
        {
            marks.RemoveAll(m => Since(m) > ShinraDome.MarkSeconds);
            if (Find.CurrentMap != map) return;
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            // A settled mark is still about 420 draws a frame (clods, grit, stones, scrapes): skip the ones out of view.
            CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(7);
            foreach (Mark mark in marks)
            {
                if (!view.Contains(mark.cell) || mark.cell.Fogged(map)) continue;
                float e = Since(mark);
                ShinraDomeGraphics.Burst(mark.at, e, mark.cast, mark.palette, sun);
                if (mark.flights != null)
                    foreach (ShinraFlight flight in mark.flights) flight.Draw(e, ShinraDome.MarkAlpha(e));
            }
            foreach (var s in GameComponent_Shinra.Instance.States)
            {
                if (s.map != map || !s.active || s.centre.ToIntVec3().Fogged(map)) continue;
                ShinraSleeves.Draw(s.animation);
                DrawCharge(s);
            }
        }

        /// <summary>Before the burst: the ground and the palms; in the first 0.1 s after it, the flash on Pain.</summary>
        private void DrawCharge(ShinraPawnState s)
        {
            var o = new Vector2(s.centre.x, s.centre.z);
            ShinraCharge c = s.charge;
            Vector3 body = default, handA = default, handB = default;
            bool parts = s.animation != null && s.animation.TryPart("BodyA", out body)
                && s.animation.TryPart("HandA", out handA) && s.animation.TryPart("HandB", out handB);
            if (c.burst)
            {
                // The tap's flash at its cast hand (HandA, west), the charged one's at the chest. Clip seconds.
                float e = c.time - c.BurstAt;
                if (parts && e < 0.1f) ShinraDomeGraphics.BurstFlash(c.tap ? handA : body, c.tap, 1f - e / 0.1f);
                return;
            }
            ShinraPalette col = Palette(map, s.centre.ToIntVec3());
            if (c.tap)
            {
                ShinraDomeGraphics.TapPress(o, Mathf.Clamp01((c.time - (ShinraCharge.TapBurst - 0.12f)) / 0.12f), col);
                return;
            }
            float[] steps = Sizes(ShinraTuning.Get);
            float held = c.Seconds, power = c.Power;
            ShinraDomeGraphics.Charge(o, held, Mathf.Max(power, 0.05f), col);
            ShinraDomeGraphics.SizeRing(o, held, steps);
            if (!parts) return;
            // The palms and the aura flare at each size step, and fade through the release's snap to the push.
            float fade = c.releasing ? 1f - ShinraDome.Smooth((c.time - (c.BurstAt - ShinraCharge.ReleaseBurst)) / ShinraCharge.ReleaseBurst) : 1f;
            int step = Mathf.Min(steps.Length - 1, Mathf.FloorToInt(held / ShinraDome.SizeStep));
            float since = held - step * ShinraDome.SizeStep, pulse = step > 0 && since < 0.25f ? 1f - since / 0.25f : 0f;
            ShinraDomeGraphics.Palms(body, handA, handB, (0.3f + 0.5f * power + 0.5f * pulse) * fade, (power + 0.6f * pulse) * fade);
        }

        /// <summary>The radius a hold gives by whole seconds: the quick version's, then each size's.</summary>
        private static float[] Sizes(ShinraTuning t)
        {
            if (sizes == null || sizes.Length != t.sizes.Count + 1)
            {
                sizes = new float[t.sizes.Count + 1];
                sizes[0] = t.tapRadius;
                for (int i = 0; i < t.sizes.Count; i++) sizes[i + 1] = t.sizes[i].radius;
            }
            return sizes;
        }

        /// <summary>The charged burst's white-out (Naruto Mobile): the whole screen for 0.12 s, only while the cast is in view.</summary>
        public override void MapComponentOnGUI()
        {
            if (Event.current.type != EventType.Repaint || Find.CurrentMap != map || marks.Count == 0) return;
            CellRect view = Find.CameraDriver.CurrentViewRect;
            float alpha = 0f;
            foreach (Mark mark in marks)
                if (view.Contains(mark.cell)) alpha = Mathf.Max(alpha, ShinraDomeGraphics.Whiteout(Since(mark), mark.cast));
            if (alpha > 0f) Widgets.DrawBoxSolid(new Rect(0f, 0f, UI.screenWidth, UI.screenHeight), new Color(1f, 1f, 1f, alpha));
        }
    }
}
