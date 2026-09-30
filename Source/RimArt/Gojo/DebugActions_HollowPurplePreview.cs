using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using T = RimArt.HollowPurple;

namespace RimArt
{
    /// <summary>
    /// Previews only. There is no combo, no ability and no def behind any of this: nothing is erased, nobody
    /// is hurt and no pawn, tree, wall or crate is drawn. Each entry plays the Hollow Purple picture round the
    /// chosen cell, which is the middle of Gojo and the end of Purple's run, as in the lab's sketch, so the
    /// recorder can compare the port with gojo-purple.js. "wall and raiders" plays the sketch's script: what
    /// the sphere would touch on the way breaks into specks, the erased raider and the struck ally flash, the
    /// ally keeps a glowing cut, the wall's cut faces glow. "open field" is Purple alone.
    /// </summary>
    public static class DebugActions_HollowPurplePreview
    {
        [RimArtDebug("Gojo", "purple: wall and raiders")]
        public static void WallAndRaiders() => Play(0f, true);

        [RimArtDebug("Gojo", "purple: wall and raiders, aim 30")]
        public static void WallAndRaiders30() => Play(30f, true);

        [RimArtDebug("Gojo", "purple: wall and raiders, aim 90")]
        public static void WallAndRaiders90() => Play(90f, true);

        [RimArtDebug("Gojo", "purple: open field")]
        public static void OpenField() => Play(0f, false);

        [RimArtDebug("Gojo", "purple: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_HollowPurplePreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(float aim, bool script) =>
            Find.CurrentMap.GetComponent<MapComponent_HollowPurplePreview>().Play(UI.MouseCell(), aim, script);
    }

    /// <summary>
    /// Plays Hollow Purple on its own clock, which is the sketch's, with the sketch's default sliders. The
    /// preview's script lives here, not in <see cref="HollowPurpleGraphics"/>: where the sketch's tree, raiders,
    /// ally, wall and crate stand, when the sphere reaches each, and what it does to them, handed to the picture
    /// as touched things and cut faces. The Erased ground lane is drawn here too, as a stand-in for the terrain
    /// the combo will lay.
    /// </summary>
    public sealed class MapComponent_HollowPurplePreview : MapComponent
    {
        /// <summary>The sketch's stand-ins' colours: raider, ally, leaves, crate, wall stone.</summary>
        private static readonly Color Raider = new Color(0.55f, 0.38f, 0.27f), Ally = new Color(0.39f, 0.58f, 0.65f),
            Leaves = new Color(0.25f, 0.45f, 0.2f), Crate = new Color(0.55f, 0.4f, 0.22f), Stone = new Color(0.5f, 0.48f, 0.46f);
        /// <summary>The Erased ground terrain's colour in the sketch: smooth, pale violet-grey.</summary>
        private static readonly Color ErasedGround = new Color(0.58f, 0.54f, 0.63f);

        public bool active;
        private float seconds, aim;
        private IntVec3 cell;
        private HollowPurpleTimes times;
        private HollowPurpleTouch[] touched;
        private HollowPurpleCut[] cuts;
        private readonly List<Vector2> shakes = new List<Vector2>();
        private int shaken;

        public MapComponent_HollowPurplePreview(Map map) : base(map) { }

        public void Play(IntVec3 at, float degrees, bool script)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            aim = degrees;
            seconds = 0f;
            times = T.Default;
            // The sketch's events(): Red leaving, Red reaching Blue, the ignition, a rumble every 0.5 s of travel.
            shakes.Clear();
            shaken = 0;
            shakes.Add(new Vector2(times.Fire, T.FireShake));
            shakes.Add(new Vector2(times.Contact, T.ContactShake));
            shakes.Add(new Vector2(times.Ignite, T.IgniteShake));
            for (float r = times.Move + T.RumbleEvery; r < times.Stop; r += T.RumbleEvery) shakes.Add(new Vector2(r, T.RumbleShake));
            touched = null;
            cuts = null;
            if (script) Script();
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes.Count && seconds >= shakes[shaken].x) Find.CameraDriver.shaker.DoShake(shakes[shaken++].y);
            Vector2 feet = Feet();
            if (seconds < times.End) DrawLane(feet, seconds);
            HollowPurpleGraphics.Draw(new HollowPurpleShot
            {
                Feet = feet, Aim = aim, BlueDist = T.BlueDist, Travel = Mathf.Min(T.Travel, T.MaxTravel), Charge = T.Charge, Merge = T.Merge,
                Speed = T.Speed, Radius = T.Radius, Dim = T.Dim, Trench = true, Seconds = seconds, Touched = touched, Cuts = cuts,
                Sleeve = GojoGraphics.Uniform, Skin = GojoGraphics.Skin,
            }, map);
            if (seconds >= times.End) active = false;
        }

        /// <summary>
        /// The preview's stand-in for the Erased ground terrain the combo will lay: every cell whose centre is within
        /// the radius of the path run so far turns pale violet-grey as the sphere's centre passes it. It follows the
        /// cells, so it steps on a slant.
        /// </summary>
        private void DrawLane(Vector2 feet, float s)
        {
            if (s < times.Move || !Shown(feet, map)) return;
            Vector2 toward = Turn(aim);
            float B = T.BlueDist, travel = Mathf.Min(T.Travel, T.MaxTravel), R = T.Radius, pad = R + 1f;
            float centre = T.Centre(times, s, B, travel, T.Speed);
            Vector2 start = GokuTiming.Place(feet, toward, B), end = GokuTiming.Place(feet, toward, B + travel);
            int x0 = Mathf.FloorToInt(Mathf.Min(start.x, end.x) - pad), x1 = Mathf.CeilToInt(Mathf.Max(start.x, end.x) + pad);
            int z0 = Mathf.FloorToInt(Mathf.Min(start.y, end.y) - pad), z1 = Mathf.CeilToInt(Mathf.Max(start.y, end.y) + pad);
            for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                {
                    var c = new IntVec3(cx, 0, cz);
                    if (!c.InBounds(map) || c.Fogged(map)) continue;
                    float dx = cx + 0.5f - feet.x, dz = cz + 0.5f - feet.y;
                    if (!T.InLane(dx * toward.x + dz * toward.y, dz * toward.x - dx * toward.y, B, centre, travel, R)) continue;
                    Sprite(new Vector2(cx + 0.5f, cz + 0.5f), 1f, 1f, ErasedGround, solid, Floor + 0.0105f);
                }
        }

        /// <summary>
        /// The sketch's "wall and raiders": the wall's middle cells it erases and the cut faces left beside them, then
        /// the tree, the raider on the centre row, the ally on a side row and the crate, north first as the sketch
        /// draws them. The raider 2.4 cells off the line is never touched and draws nothing.
        /// </summary>
        private void Script()
        {
            Vector2 feet = Feet(), toward = Turn(aim);
            float B = T.BlueDist, R = T.Radius, wall = B + T.ScriptWallAt;
            var hit = new List<HollowPurpleTouch>();
            var faces = new List<HollowPurpleCut>();
            int[] cells = T.ScriptWallCells;
            foreach (int k in cells)
            {
                float at = T.HitAt(times, B, T.Speed, R, wall, k);
                if (!float.IsInfinity(at))
                {
                    hit.Add(new HollowPurpleTouch { Ground = GokuTiming.Place(feet, toward, wall, k), Across = k, At = at, Kind = HollowPurpleTouchKind.Building, Colour = Stone });
                    continue;
                }
                // A standing cell next to an erased one: its face toward the path is cut.
                int inner = k - (k > 0 ? 1 : k < 0 ? -1 : 0);
                if (k == 0 || System.Array.IndexOf(cells, inner) < 0) continue;
                float cutAt = T.HitAt(times, B, T.Speed, R, wall, inner);
                if (float.IsInfinity(cutAt)) continue;
                float face = k - (k > 0 ? 0.5f : -0.5f);
                faces.Add(new HollowPurpleCut { From = GokuTiming.Place(feet, toward, wall - 0.5f, face), To = GokuTiming.Place(feet, toward, wall + 0.5f, face), At = cutAt });
            }
            var things = new List<HollowPurpleTouch>();
            for (int i = 0; i < T.ScriptAlong.Length; i++)
            {
                float along = B + T.ScriptAlong[i], across = T.ScriptAcross[i], at = T.HitAt(times, B, T.Speed, R, along, across);
                if (float.IsInfinity(at)) continue;
                HollowPurpleScriptThing kind = T.ScriptKinds[i];
                bool pawn = kind == HollowPurpleScriptThing.Raider || kind == HollowPurpleScriptThing.Ally;
                things.Add(new HollowPurpleTouch
                {
                    Ground = GokuTiming.Place(feet, toward, along, across), Across = across, At = at,
                    Kind = !pawn ? HollowPurpleTouchKind.Thing : Mathf.Abs(across) <= T.CentreRow ? HollowPurpleTouchKind.Erased : HollowPurpleTouchKind.Struck,
                    Colour = kind == HollowPurpleScriptThing.Tree ? Leaves : kind == HollowPurpleScriptThing.Crate ? Crate : kind == HollowPurpleScriptThing.Ally ? Ally : Raider,
                });
            }
            things.Sort((m, n) => n.Ground.y.CompareTo(m.Ground.y));
            hit.AddRange(things);
            touched = hit.ToArray();
            cuts = faces.ToArray();
        }

        /// <summary>Gojo's feet: half the whole span (Blue's distance plus the run) back from the chosen cell along the aim.</summary>
        private Vector2 Feet()
        {
            Vector3 centre = cell.ToVector3Shifted();
            return new Vector2(centre.x, centre.z) - Turn(aim) * ((T.BlueDist + Mathf.Min(T.Travel, T.MaxTravel)) / 2f);
        }
    }
}
