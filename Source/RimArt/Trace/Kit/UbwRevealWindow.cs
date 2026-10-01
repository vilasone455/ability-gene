using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using R = RimArt.UbwRevealTiming;

namespace RimArt
{
    /// <summary>
    /// Unlimited Blade Works' reveal shot in play (<see cref="UbwRevealGraphics"/>), opened at the take when the player
    /// was watching and the setting is on (<see cref="RimArtSettings.ubwRevealShot"/>). A window over the whole screen:
    /// it pauses the game as the gravship cutscene does (the game stays paused while a window forces it), hides the
    /// UI (screenshot mode, put back as it was), takes the camera to the world's usual framing (36 cells tall, 7 north
    /// of the caster) for the hand-over, and plays the shot on real time; any key or click skips to the end. The
    /// world's own clock follows the shot's world time and goes on from where the shot ends, standing (the fire ran
    /// out in the shot), and the world's timer only starts when the game runs again.
    ///
    /// The pawns stand in the shot as their portraits (PortraitsCache, facing as they face), upright on their plates
    /// from the feet, 2 cells wide; in the game view they are the 2 x 2 cells the portrait covers round the pawn.
    /// </summary>
    public sealed class UbwRevealWindow : Window
    {
        /// <summary>The portrait's area round the pawn (PortraitsCache at zoom 1 covers 2 x 2 cells); the feet are FeetBelow under the pawn's draw position.</summary>
        private const float PortraitCells = 2f, FeetBelow = 0.4f;
        private const int PortraitPx = 256;

        private readonly Map world;
        private readonly MapComponent_UnlimitedBladeWorks component;
        private readonly List<Pawn> pawns;
        private readonly UbwShot shot = new UbwShot();
        private readonly Dictionary<Pawn, Material> portraits = new Dictionary<Pawn, Material>();
        private float t;
        private bool screenshotWas;

        /// <summary>Tests turn the shot off: it pauses the game for 4.6 s of real time.</summary>
        internal static bool offForTests;
        /// <summary>For the tests: how many shots have opened, and how long the last one ran (real seconds).</summary>
        internal static int opened;
        internal static float lastRan;

        /// <summary>Whether the take should open the shot.</summary>
        internal static bool Wanted => RimArtSettings.Get.ubwRevealShot && !offForTests;

        public UbwRevealWindow(Map world, List<Pawn> pawns)
        {
            this.world = world;
            this.pawns = pawns;
            component = world.GetComponent<MapComponent_UnlimitedBladeWorks>();
            forcePause = true;
            absorbInputAroundWindow = true;
            preventCameraMotion = true;
            doWindowBackground = false;
            drawShadow = false;
            doCloseX = false;
            closeOnClickedOutside = false;
            layer = WindowLayer.Super;
            drawInScreenshotMode = true;
        }

        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);
        protected override float Margin => 0f;

        public float Seconds => t;

        public override void PreOpen()
        {
            base.PreOpen();
            windowRect = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight);
            opened++;
            screenshotWas = Find.UIRoot.screenshotMode.Active;
            Find.UIRoot.screenshotMode.Active = true;
            Vector2 o = component.Origin;
            Find.CameraDriver.SetRootPosAndSize(new Vector3(o.x, 0f, o.y + R.GameNorth), R.CellsTall / 2f);
            component.RevealAt(UbwRevealGraphics.WorldTime(0f));
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            // Real time: the game is paused. A long frame (the world being baked) does not jump the shot.
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (t >= R.End || world == null || !Find.Maps.Contains(world) || Find.CurrentMap != world)
            {
                Close();
                return;
            }
            component.RevealAt(UbwRevealGraphics.WorldTime(t));
            UbwRevealScene W = component.RevealScene((float)UI.screenWidth / Mathf.Max(1, UI.screenHeight));
            UbwRevealGraphics.Shot(shot, W, component.Origin, t, Figures(W));
            UbwShot.Sink?.Invoke(shot);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Any key or click skips the shot (Escape and Enter close the window themselves).
            if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.MouseDown)
            {
                Event.current.Use();
                Close();
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            Find.UIRoot.screenshotMode.Active = screenshotWas;
            lastRan = t;
            // Skipped early or played out: the world stands from where the shot ends.
            component?.RevealAt(Mathf.Max(UbwRevealGraphics.WorldTime(Mathf.Max(t, R.End)), UbwWorldTiming.Swept));
        }

        private readonly List<UbwRevealFigure> figures = new List<UbwRevealFigure>();

        /// <summary>Every pawn taken, still in the world, as its portrait standing on its plate.</summary>
        private List<UbwRevealFigure> Figures(UbwRevealScene W)
        {
            figures.Clear();
            Vector2 o = component.Origin;
            foreach (Pawn p in pawns)
            {
                if (p == null || p.Dead || !p.Spawned || p.Map != world) continue;
                Vector3 at = p.DrawPos;
                float x = at.x - o.x, z = at.z - o.y, h = (float)W.T.HeightAt(x, z);
                if (!portraits.TryGetValue(p, out Material mat)) portraits[p] = mat = new Material(ShaderDatabase.Transparent);
                mat.mainTexture = PortraitsCache.Get(p, new Vector2(PortraitPx, PortraitPx), p.Rotation, default, 1f);
                var fig = new UbwRevealFigure { X = x, Z = z };
                fig.Parts.Add((Shadow(UbwRevealSky.Live(W, "portrait shadow " + p.thingIDNumber), x + W.Sun.x * 0.45f, z + W.Sun.y * 0.45f, h), VfxDraw.Fade(VfxDraw.Ink, W.Strength), VfxDraw.soft, UbwDepth.Over));
                fig.Parts.Add((Billboard(UbwRevealSky.Live(W, "portrait " + p.thingIDNumber), x, z, h), Color.white, mat, UbwDepth.Write));
                figures.Add(fig);
            }
            return figures;
        }

        /// <summary>The portrait from the feet up, standing on the plane through the pawn (3D), and lying where the game draws it.</summary>
        private static UbwMesh3 Billboard(UbwMesh3 m, float x, float z, float h)
        {
            var b = new UbwBuilder3();
            float half = PortraitCells / 2f, v0 = (half - FeetBelow) / PortraitCells, tall = half + FeetBelow;
            b.Vertex(new Vector3(x - half, h, z), new Vector2(x - half, z - FeetBelow), new Vector2(0f, v0));
            b.Vertex(new Vector3(x - half, h + tall, z), new Vector2(x - half, z + half), new Vector2(0f, 1f));
            b.Vertex(new Vector3(x + half, h + tall, z), new Vector2(x + half, z + half), new Vector2(1f, 1f));
            b.Vertex(new Vector3(x + half, h, z), new Vector2(x + half, z - FeetBelow), new Vector2(1f, v0));
            b.Triangle(0, 1, 2);
            b.Triangle(0, 2, 3);
            return b.Into(m);
        }

        /// <summary>A soft shadow under the pawn on its plate, along the low sun (the lab stand-in's, 0.85 x 0.4).</summary>
        private static UbwMesh3 Shadow(UbwMesh3 m, float x, float z, float h)
        {
            var b = new UbwBuilder3();
            for (int k = 0; k < 4; k++)
            {
                float cx = k < 2 ? -0.5f : 0.5f, cz = k == 1 || k == 2 ? 0.5f : -0.5f;
                b.Vertex(new Vector3(x + cx * 0.85f, h, z + cz * 0.4f), new Vector2(x + cx * 0.85f, z + cz * 0.4f), new Vector2(cx + 0.5f, cz + 0.5f));
            }
            b.Triangle(0, 1, 2);
            b.Triangle(0, 2, 3);
            return b.Into(m);
        }
    }
}
