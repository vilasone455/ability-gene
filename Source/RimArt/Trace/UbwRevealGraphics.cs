using System.Collections.Generic;
using UnityEngine;
using static RimArt.UbwGraphics;
using B = RimArt.UbwBackdropGraphics;
using C = RimArt.UbwCrest;
using R = RimArt.UbwRevealTiming;

namespace RimArt
{
    /// <summary>
    /// Unlimited Blade Works' reveal shot, one frame at a time, as data for a cutscene camera (<see cref="UbwShot"/>): the
    /// camera at head height looking up at the gears, tilting down to the plain while the fire runs out and the swords
    /// rise, craning up, and blending into the game view, where the map camera takes over at 4.2 s. The port of
    /// Tools/VfxLab/web/sketches/trace-ubw-reveal.js and drawWorld in lib/ubw-reveal.js. The world's time goes on into
    /// the world v4 after the hand-over: it is <see cref="WorldTime"/>.
    ///
    /// Drawn far to near with no depth test (the game's shaders may write no depth): the sky, the plain, the backdrop,
    /// the smoke and embers behind the crest, the crack floor, the plates, the crest, the light on the ground, the
    /// swords north first with the figures among them, the fire, the embers in the air. The fire's flames are added
    /// light drawn over the swords; the lab hides those behind a nearer sword.
    /// </summary>
    internal static class UbwRevealGraphics
    {
        /// <summary>The smoke bands (far to near) stand SmokeBehind cells behind the crest's mean top, the rising embers EmberBehind behind the top above them; the embers in the air are 0.4 to 3.6 cells up.</summary>
        private static readonly float[] SmokeBehind = { 1.2f, 0.7f, 0.3f };
        private const float EmberBehind = 0.3f;
        private static readonly Color WhiteHot = new Color(1f, 0.98f, 0.94f), Bars = new Color(0f, 0f, 0f, 1f);

        /// <summary>The world's time at t seconds into the shot: v4's at the hand-over is <see cref="UbwWorldTiming.Swept"/> (the fire has run out), so gears, clouds, smoke and embers carry straight on.</summary>
        public static float WorldTime(float t) => UbwWorldTiming.Swept + (t - R.HandOver);

        /// <summary>
        /// Frame t of the shot for the world round <paramref name="caster"/> (the cell corner v4 draws on), with
        /// <paramref name="figures"/> standing in it. False from the hand-over on: the map camera draws the world then,
        /// and the shot only adds the bars.
        /// </summary>
        public static bool Shot(UbwShot shot, UbwRevealScene W, Vector2 caster, float t, List<UbwRevealFigure> figures)
        {
            shot.Clear();
            shot.GameCentre = new Vector2(caster.x + W.View.Cx, caster.y + W.View.Cz);
            shot.CellsTall = R.CellsTall;
            Overlays(shot, t);
            if (t >= R.HandOver)
            {
                shot.Flat = true;
                return false;
            }
            R.Pose q = R.Along(Mathf.Min(t, R.HandOver));
            shot.Flat = false;
            shot.Eye = new Vector3(caster.x + q.X, q.Y, caster.y + q.Z);
            shot.Pitch = q.Pitch;
            shot.Fov = q.Fov;
            shot.Near = 0.3f;
            shot.Far = 9000f;
            shot.Blend = R.BlendAt(t);
            float fire = R.FireAt(t);
            World(shot, W, caster, WorldTime(t), fire, R.FlamesAt(fire), shot.Blend, figures);
            return true;
        }

        private static readonly UbwShot previewShot = new UbwShot();

        /// <summary>
        /// The preview: the shot over the map on screen, for the world centred 2 cells north of the chosen cell as the
        /// sketch is, with the sketch's landing spots and stand-ins, under the map's sun made low; from the hand-over
        /// the world v4 drawn by the map camera, standing. Sent to <see cref="UbwShot.Sink"/>; the game's cutscene
        /// camera draws it, and the lab's recorder records it.
        /// </summary>
        public static void DrawPreview(Vector3 centre, float t, Verse.Map map)
        {
            var o = new Vector2(centre.x, centre.z + UbwWorldGraphics.SceneNorth);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            sun = UbwCrestWorld.LowSun(sun);
            UbwXZ[] keep = UbwWorldGraphics.PreviewKeep();
            UbwRevealScene W = UbwRevealScene.For(keep, sun, Verse.Find.Camera.aspect);
            W.Strength = strength;
            if (!Shot(previewShot, W, o, t, UbwRevealGround.StandIns(W)))
            {
                UbwCrestWorld world = UbwCrestWorld.For(sun);
                UbwFieldBake bake = UbwWorldGraphics.BakeFor(keep, sun, world.Terrain.Terrain);
                UbwWorldGraphics.Draw(o, bake, WorldTime(t), float.PositiveInfinity, UbwLayers.Preview, sun, strength, map, world);
            }
            UbwShot.Sink?.Invoke(previewShot);
        }

        /// <summary>The white of the take fading out, the black bars leaving.</summary>
        private static void Overlays(UbwShot shot, float t)
        {
            float white = R.WhiteAt(t), bars = R.BarsAt(t);
            if (white > 0f) shot.Fill(0f, 0f, 1f, 1f, VfxDraw.Fade(WhiteHot, white));
            if (bars <= 0f) return;
            shot.Fill(0f, 0f, 1f, bars, Bars);
            shot.Fill(0f, 1f - bars, 1f, bars, Bars);
        }

        /// <summary>The world at world time s with the fire run out to <paramref name="fire"/> (infinity: every sword stands), its flames at <paramref name="flames"/>, blended so far into the game view.</summary>
        public static void World(UbwShot shot, UbwRevealScene W, Vector2 caster, float s, float fire, float flames, float blend, List<UbwRevealFigure> figures)
        {
            var at = new Vector3(caster.x, 0f, caster.y);
            UbwRevealSky.Sky(shot, W, at, s);
            UbwRevealSky.PlainDraw(shot, W, at, blend);
            UbwRevealSky.BackdropDraw(shot, W, at, fire, blend);
            BehindCrest(shot, W, at, s);

            // The map: crack floor, plates and their shadows; the crest; the light on the ground; the swords and figures.
            shot.Draw(UbwRevealGround.Floor(W), B.Base, VfxDraw.solid, at);
            shot.Draw(W.Ground.Ground3, W.Tint, UbwTerrainGraphics.atlas, at);
            shot.Draw(W.Ground.Shadows3, VfxDraw.Fade(Black, 0.4f * W.Strength / 0.32f), VfxDraw.solid, at, UbwDepth.Over);
            UbwRevealGround.CrestDraw(shot, W, at, fire);
            UbwRevealGround.Flats(shot, W, at, s, fire);
            UbwRevealGround.FieldDraw(shot, W, at, fire, figures);
            if (!float.IsPositiveInfinity(fire)) FireRing(shot, W, at, fire, s, flames);

            // The camera's haze over the ground (faded in with the blend), the embers in the air, the ash in front.
            if (blend > 0f && B.DepthHazeQuad(Vector2.zero, W.North + W.K.MeanTop, W.View, (float)C.Haze * blend, out B.Quad4 haze, out float hazeAlpha))
                shot.Draw(ScreenQuads(W, "haze", haze), VfxDraw.Fade(B.Fog, hazeAlpha), B.depthMat, at, UbwDepth.Over, 1f, true);
            Embers(shot, W, at, s);
            if (blend > 0f)
            {
                B.ForegroundQuads(Vector2.zero, s, W.View, ash, front);
                shot.Draw(ScreenQuads(W, "ash", ash), VfxDraw.Fade(B.Ash, 0.3f * blend), VfxDraw.soft, at, UbwDepth.Over, 1f, true);
                shot.Draw(ScreenQuads(W, "front embers", front), VfxDraw.Fade(B.Spark, 0.34f * blend), VfxDraw.glow, at, UbwDepth.Over, 1f, true);
            }
        }

        private static readonly List<B.Quad4> body = new List<B.Quad4>(), top = new List<B.Quad4>(), ash = new List<B.Quad4>(), front = new List<B.Quad4>(), one = new List<B.Quad4>();
        private static readonly List<B.Quad4>[] rising = { new List<B.Quad4>(), new List<B.Quad4>(), new List<B.Quad4>(), new List<B.Quad4>() };
        private static readonly UbwBuilder3 scratch = new UbwBuilder3();

        /// <summary>The smoke drifting over the crest and the embers rising from behind it, standing just behind its top.</summary>
        private static void BehindCrest(UbwShot shot, UbwRevealScene W, Vector3 at, float s)
        {
            for (int j = 0; j < 3; j++)
            {
                B.SmokeQuads(Vector2.zero, s, W.View, W.K, j, body, top);
                float zp = W.North + W.K.MeanGround + SmokeBehind[j];
                shot.Draw(Standing(W, "smoke " + j, body, q => zp), B.SmokeColour(j), VfxDraw.soft, at, UbwDepth.NoWrite);
                shot.Draw(Standing(W, "smoke light " + j, top, q => zp), B.SmokeLight, VfxDraw.glow, at, UbwDepth.NoWrite);
            }
            B.UpdraftQuads(Vector2.zero, s, W.View, W.K, C.Updraft, rising);
            for (int b = 0; b < 4; b++)
                shot.Draw(Standing(W, "updraft " + b, rising[b], q => W.North + (float)C.GroundOf(q.X) + EmberBehind), B.UpdraftColour(b), VfxDraw.glow, at, UbwDepth.NoWrite);
        }

        private static UbwMesh3 Standing(UbwRevealScene W, string key, List<B.Quad4> quads, System.Func<B.Quad4, float> zp)
        {
            scratch.Clear();
            foreach (B.Quad4 q in quads) scratch.Standing(q.X, q.Z, q.W, q.H, zp(q));
            return scratch.Into(UbwRevealSky.Live(W, key));
        }

        private static UbwMesh3 ScreenQuads(UbwRevealScene W, string key, B.Quad4 quad)
        {
            one.Clear();
            one.Add(quad);
            return ScreenQuads(W, key, one);
        }

        /// <summary>Quads drawn where the game view draws them (their 3D place is never used).</summary>
        private static UbwMesh3 ScreenQuads(UbwRevealScene W, string key, List<B.Quad4> quads)
        {
            scratch.Clear();
            foreach (B.Quad4 q in quads) UbwRevealGround.GroundQuad(scratch, q.X, q.Z, q.W, q.H);
            return scratch.Into(UbwRevealSky.Live(W, key));
        }

        /// <summary>v4's embers in the air round the caster, each at its own height 0.4 to 3.6 cells, standing where v4 draws it once the height rule is taken off.</summary>
        private static void Embers(UbwShot shot, UbwRevealScene W, Vector3 at, float s)
        {
            for (int k = 0; k < 3; k++)
            {
                scratch.Clear();
                for (int i = 0; i < 140; i++)
                {
                    UbwWorldGraphics.Ember1 e = UbwWorldGraphics.EmberAt(i, s);
                    if (e.Bright != k) continue;
                    float y = 0.4f + 3.2f * (float)UbwTerrain.Hash(e.I, 9, 61);
                    int b = scratch.Count;
                    for (int c = 0; c < 4; c++)
                    {
                        float cx = c < 2 ? -0.5f : 0.5f, cz = c == 1 || c == 2 ? 0.5f : -0.5f;
                        scratch.Vertex(new Vector3(e.X + cx * e.Size, y + cz * e.Size, e.Z - y * Lift), new Vector2(e.X + cx * e.Size, e.Z + cz * e.Size), new Vector2(cx + 0.5f, cz + 0.5f));
                    }
                    scratch.Triangle(b, b + 1, b + 2);
                    scratch.Triangle(b, b + 2, b + 3);
                }
                shot.Draw(scratch.Into(UbwRevealSky.Live(W, "embers " + k)), UbwWorldGraphics.EmberColour(k, 1f), VfxDraw.glow, at, UbwDepth.NoWrite);
            }
        }

        /// <summary>
        /// A ring of flames r cells round the caster, standing up and facing the camera (1.2 to 2.9 cells tall), with a
        /// glow on the ground inside it. The glow fades out as the ring passes the crest (past the map it would light
        /// the crest's face and the backdrop).
        /// </summary>
        private static void FireRing(UbwShot shot, UbwRevealScene W, Vector3 at, float r, float s, float alpha)
        {
            if (r <= 0.2f || alpha <= 0f) return;
            float floor = alpha * (1f - R.Smooth((r - W.North) / 4f));
            if (floor > 0f)
            {
                // The lab's fadeRing: opacity 0 at r - 2.5 rising to 1 at r, through the fade gradient across it.
                scratch.Clear();
                float r0 = Mathf.Max(0f, r - 2.5f);
                const int n = 96;
                for (int i = 0; i <= n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    scratch.Vertex(new Vector3(ca * r0, 0f, sa * r0), new Vector2(ca * r0, sa * r0), new Vector2(0.02f, 0.5f));
                    scratch.Vertex(new Vector3(ca * r, 0f, sa * r), new Vector2(ca * r, sa * r), new Vector2(0.98f, 0.5f));
                    if (i == 0) continue;
                    int b = i * 2;
                    scratch.Triangle(b - 2, b, b - 1);
                    scratch.Triangle(b - 1, b, b + 1);
                }
                shot.Draw(scratch.Into(UbwRevealSky.Live(W, "fire floor")), VfxDraw.Fade(FireOuter, 0.3f * floor), fadeMat, at, UbwDepth.Over);
            }
            int count = Mathf.Min(1400, Mathf.Max(24, Mathf.CeilToInt(Mathf.PI * 2f * r / 0.45f)));
            for (int pass = 0; pass < 2; pass++)
            {
                scratch.Clear();
                for (int i = 0; i < count; i++)
                {
                    float a = (i + VfxMath.Rand(i * 7 + 3) * 0.6f) / count * Mathf.PI * 2f, x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    float w = 0.6f * (0.8f + 0.4f * VfxMath.Rand(i * 5 + 2)), h = Flick(i, s, 2.1f);
                    if (pass == 1) { z -= 0.02f; w *= 0.5f; h *= 0.6f; }
                    int b = scratch.Count;
                    for (int c = 0; c < 4; c++)
                    {
                        float cx = c < 2 ? -0.5f : 0.5f, y = c == 1 || c == 2 ? 1f : 0f;
                        scratch.Vertex(new Vector3(x + cx * w, y * h, z), new Vector2(x + cx * w, z + y * h * Lift), new Vector2(cx + 0.5f, y));
                    }
                    scratch.Triangle(b, b + 1, b + 2);
                    scratch.Triangle(b, b + 2, b + 3);
                }
                shot.Draw(scratch.Into(UbwRevealSky.Live(W, pass == 0 ? "fire" : "fire core")), VfxDraw.Fade(pass == 0 ? FireOuter : FireCore, 0.5f * alpha), flameGlow, at, UbwDepth.NoWrite);
            }
        }
    }
}
