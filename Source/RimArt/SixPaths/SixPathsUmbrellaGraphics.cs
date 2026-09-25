using System;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// Draws the Obsidian Umbrella: the sage's ring and the orbs that leave it, the closed umbrella
    /// in the hand, and the open canopy with its veil, ribs, hit flashes and shards.
    /// SixPathsUmbrellaTiming says where everything is; this only turns it into meshes.
    ///
    /// Panels and veil are see-through so that whoever stands under them stays visible. Panels are
    /// painted back to front by how near the camera their middles are.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsUmbrellaGraphics
    {
        private const int P = SixPathsUmbrellaTiming.Panels, Points = SixPathsUmbrellaTiming.PanelPoints;

        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh ring = VfxDraw.Ring(0.965f, "Six Paths umbrella ring"), shard = Shard();

        // One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh
        // when the frame renders, not when it is called.
        private static readonly SixPathsStrip[] calls = Strips("call", 4, 3), ribs = Strips("rib", P, 3),
            veils = Strips("veil", P, Points), lows = Strips("panel low", P, Points), tops = Strips("panel top", P, Points),
            hems = Strips("hem", P, Points), flashLows = Strips("flash low", P, Points), flashTops = Strips("flash top", P, Points),
            threads = Strips("thread", P * 3, 3);
        private static readonly SixPathsStrip spindleEdge = Strip("closed edge", SixPathsUmbrellaTiming.SpindlePoints),
            spindleDark = Strip("closed dark", SixPathsUmbrellaTiming.SpindlePoints),
            spindleLit = Strip("closed lit", SixPathsUmbrellaTiming.SpindlePoints),
            streak = Strip("thrust streak", 3), shaft = Strip("shaft", 3), ripple = Strip("veil ripple", 3),
            arc = Strip("guard arc", SixPathsUmbrellaTiming.ArcPoints);

        private static readonly Vector2[] three = new Vector2[3], arcPoints = new Vector2[SixPathsUmbrellaTiming.ArcPoints],
            rim = new Vector2[Points], middle = new Vector2[Points], top = new Vector2[Points], hem = new Vector2[Points],
            edgeLeft = new Vector2[SixPathsUmbrellaTiming.SpindlePoints], edgeRight = new Vector2[SixPathsUmbrellaTiming.SpindlePoints],
            left = new Vector2[SixPathsUmbrellaTiming.SpindlePoints], right = new Vector2[SixPathsUmbrellaTiming.SpindlePoints],
            core = new Vector2[SixPathsUmbrellaTiming.SpindlePoints];
        private static readonly UmbrellaPanel[] order = new UmbrellaPanel[P];
        private static readonly Comparison<UmbrellaPanel> farFirst = (a, b) => a.near.CompareTo(b.near);

        private static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        private static readonly Color Slate = new Color(0.26f, 0.21f, 0.38f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Pale = new Color(0.88f, 0.79f, 1f);

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone. <paramref name="sage"/> is
        /// where the pawn stands now and <paramref name="toward"/> the cardinal it faces.
        /// </summary>
        public static void Draw(Vector3 sage, Vector2 toward, UmbrellaMode mode, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= SixPathsUmbrellaTiming.Duration) return;
            if (!sage.ToIntVec3().InBounds(map) || sage.ToIntVec3().Fogged(map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector
                * SixPathsSlamGraphics.SunScale;
            float daylight = GenCelestial.CurShadowStrength(map);
            float openness = SixPathsUmbrellaTiming.Openness(seconds, mode);
            float formed = SixPathsUmbrellaTiming.Formed(seconds);
            bool canopy = mode == UmbrellaMode.Canopy;

            Vector2 apexOpen = canopy
                ? new Vector2(0f, (SixPathsUmbrellaTiming.RimHeight + SixPathsUmbrellaTiming.Dome) * SixPathsHeight.Lift)
                : SixPathsUmbrellaTiming.Rel(toward, SixPathsUmbrellaTiming.GuardReach, 0f, SixPathsUmbrellaTiming.GuardHeight);
            Vector2 baseOpen = canopy ? new Vector2(0f, 0.7f * SixPathsHeight.Lift)
                : SixPathsUmbrellaTiming.Rel(toward, 0.2f, 0f, 0.8f);

            DrawOrbs(sage, toward, mode, seconds, formed, apexOpen, sun, daylight);
            if (formed > 0.05f && openness < 1f) DrawClosed(sage, toward, seconds, formed, openness, apexOpen, baseOpen);
            if (openness > 0.01f) DrawOpen(sage, toward, mode, seconds, openness, baseOpen, sun, daylight);
            DrawShards(sage, toward, mode, seconds);
        }

        /// <summary>The ring, the two orbs on their way to the hand, and the four the canopy calls up.</summary>
        private static void DrawOrbs(Vector3 sage, Vector2 toward, UmbrellaMode mode, float seconds, float formed,
            Vector2 apexOpen, Vector2 sun, float daylight)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
            float called = SixPathsUmbrellaTiming.Called(seconds, mode);
            // Slots 0 and 1 are the umbrella for as long as it is out.
            SixPathsGraphics.DrawCarried(sage, seconds, called > 0f ? 0b111111 : 0b000011, sun, daylight);

            if (called > 0f)
                for (int k = 2; k < SixPathsTiming.Orbs; k++)
                {
                    CarriedSlot slot = SixPathsCarried.Slot(sage, k, seconds);
                    Vector2 home = Home(sage, slot), at = Vector2.Lerp(home, apexOpen, called);
                    // They swell on the way up and are gone into the apex by the time they reach it.
                    float radius = SixPathsCarried.DeployRadius(called * 2f, SixPathsCarried.OnPawn)
                        * (1f - VfxMath.Smooth((called - 0.5f) / 0.5f) * 0.95f);
                    DrawBall(sage, at, called < 0.15f ? SixPathsGraphics.CarriedAltitude(slot) : overhead, radius);
                    if (called >= 1f) continue;
                    three[0] = home; three[1] = (home + at) * 0.5f + new Vector2(0f, 0.15f); three[2] = at;
                    DrawLine(calls[k - 2], three, 0.06f, sage.WithY(overhead), Fade(Rim, 0.4f));
                }

            if (formed >= 1f) return;
            float gone = SixPathsUmbrellaTiming.Gone(seconds), reach = (1f - formed) * 0.7f;
            Vector2 hand = SixPathsUmbrellaTiming.Rel(toward, 0.2f, 0f, 0.8f);
            for (int k = 0; k < 2; k++)
            {
                // Out of the slot in the first half of the morph, then the spiral in to the hand.
                CarriedSlot slot = SixPathsCarried.Slot(sage, k, seconds);
                float angle = seconds * 9f + (k == 1 ? 1.57f : -1.57f);
                Vector2 spiral = hand + new Vector2(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach * 0.5f);
                DrawBall(sage, Vector2.Lerp(Home(sage, slot), spiral, gone),
                    gone < 0.15f ? SixPathsGraphics.CarriedAltitude(slot) : overhead,
                    SixPathsCarried.DeployRadius(gone, SixPathsCarried.OnPawn) * (1f - formed * 0.8f));
            }
        }

        /// <summary>
        /// Closed, in the hand: a tapered spindle. It lunges along the facing for the thrust and
        /// swings to the open state's axis as it opens.
        /// </summary>
        private static void DrawClosed(Vector3 sage, Vector2 toward, float seconds, float formed, float openness,
            Vector2 apexOpen, Vector2 baseOpen)
        {
            float lunge = SixPathsUmbrellaTiming.Lunge(seconds), level = SixPathsUmbrellaTiming.Level(seconds);
            Vector2 hand = SixPathsUmbrellaTiming.Rel(toward, 0.2f + lunge * 0.45f, 0f, Mathf.Lerp(0.55f, 0.7f, level));
            Vector2 tip = Vector2.Lerp(SixPathsUmbrellaTiming.Rel(toward, 0.55f, 0f, 1.6f),
                SixPathsUmbrellaTiming.Rel(toward, 1.5f + lunge * 0.45f, 0f, 0.72f), level);
            tip = Vector2.Lerp(tip, apexOpen, openness);
            Vector2 foot = Vector2.Lerp(hand, baseOpen, openness), along = tip - foot;
            float length = along.magnitude;
            Vector2 side = new Vector2(-along.y, along.x) / (length > 1e-5f ? length : 1f);

            int last = SixPathsUmbrellaTiming.SpindlePoints - 1;
            for (int j = 0; j <= last; j++)
            {
                float u = j / (float)last, width = SixPathsUmbrellaTiming.SpindleWidth(u, formed, openness);
                Vector2 at = foot + along * (u * formed);
                left[j] = at - side * width; right[j] = at + side * width; core[j] = at;
                edgeLeft[j] = at - side * (width + 0.02f); edgeRight[j] = at + side * (width + 0.02f);
            }

            // Facing north it is held in front of the pawn, which is behind it from the camera.
            float layer = toward.y > 0.5f && openness < 0.05f
                ? AltitudeLayer.Pawn.AltitudeFor() - 0.02f : AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            DrawBetween(spindleEdge, edgeLeft, edgeRight, sage.WithY(layer), Fade(Rim, 0.9f));
            DrawBetween(spindleDark, left, core, sage.WithY(layer + 0.001f), Body);
            DrawBetween(spindleLit, core, right, sage.WithY(layer + 0.002f), Color.Lerp(Body, Slate, 0.4f));
            DrawSprite(sage, tip, layer + 0.003f, 0.16f, 0.16f, Fade(Pale, 0.7f * formed), softGlow);
            if (lunge <= 0.2f) return;
            three[0] = SixPathsUmbrellaTiming.Rel(toward, 0.4f, 0f, 0.72f);
            three[1] = SixPathsUmbrellaTiming.Rel(toward, 1.3f, 0f, 0.74f);
            three[2] = tip;
            DrawLine(streak, three, 0.1f, sage.WithY(layer - 0.001f), Fade(Pale, lunge * 0.6f));
        }

        private static void DrawOpen(Vector3 sage, Vector2 toward, UmbrellaMode mode, float seconds, float openness,
            Vector2 baseOpen, Vector2 sun, float daylight)
        {
            bool canopy = mode == UmbrellaMode.Canopy;
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), pawn = AltitudeLayer.Pawn.AltitudeFor();
            float floor = AltitudeLayer.Filth.AltitudeFor(), shadows = AltitudeLayer.Shadows.AltitudeFor();
            float shade = 0.32f * daylight;
            SixPathsUmbrellaTiming.Frame frame = SixPathsUmbrellaTiming.FrameFor(mode, toward, openness);
            Vector2 apex = SixPathsUmbrellaTiming.Show(frame.Apex);

            if (canopy)
            {
                DrawMesh(ring, sage.WithY(floor), SixPathsUmbrellaTiming.Radius, SixPathsUmbrellaTiming.Radius, 0f,
                    Fade(Rim, 0.4f * openness), solid);
                DrawSprite(sage, sun * SixPathsUmbrellaTiming.RimHeight, shadows, frame.radius * 2.3f, frame.radius * 2.3f,
                    Fade(Body, shade * 0.55f * openness), soft);
            }
            else
            {
                // The guarded arc drawn on the floor, at its true width.
                float heading = Mathf.Atan2(toward.y, toward.x);
                for (int j = 0; j < arcPoints.Length; j++)
                {
                    float a = heading + (j / (float)(arcPoints.Length - 1) - 0.5f) * SixPathsUmbrellaTiming.GuardArc * Mathf.Deg2Rad;
                    arcPoints[j] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.25f;
                }
                DrawLine(arc, arcPoints, 0.07f, sage.WithY(floor + 0.01f), Fade(Rim, 0.5f * openness));
                DrawSprite(sage, SixPathsUmbrellaTiming.Rel(toward, 0.6f, 0f) + sun * 0.8f, shadows,
                    1.3f * openness, 0.7f * openness, Fade(Body, shade * 0.5f), soft);
            }

            // Held out to the north, the whole dome is on the far side of the pawn.
            float bottom = !canopy && toward.y > 0.5f ? pawn - 0.03f : overhead + 0.07f;
            three[0] = baseOpen; three[1] = (baseOpen + apex) * 0.5f; three[2] = apex;
            DrawLine(shaft, three, 0.06f, sage.WithY(bottom - 0.002f), Body);

            for (int k = 0; k < P; k++) order[k] = SixPathsUmbrellaTiming.Panel(frame, k);
            Array.Sort(order, farFirst);
            float flash = SixPathsUmbrellaTiming.Flash(seconds) * (canopy ? 0.55f : 0.4f);
            float drop = SixPathsUmbrellaTiming.VeilDrop(seconds, mode);
            for (int rank = 0; rank < P; rank++)
            {
                UmbrellaPanel panel = order[rank];
                int k = panel.index;
                // The canopy's front panel is the one that pays for the hits, and the one that breaks.
                if (canopy && k == 0 && seconds >= SixPathsUmbrellaTiming.BrokenAt) continue;
                float layer = bottom + rank * 0.002f, hemNorth = 0f;
                for (int j = 0; j < Points; j++)
                {
                    float a = panel.centre + (j / (float)(Points - 1) - 0.5f) * Mathf.PI / 3f;
                    Vector3 rim3 = frame.Rim(a, k);
                    rim[j] = SixPathsUmbrellaTiming.Show(rim3);
                    middle[j] = SixPathsUmbrellaTiming.Show(frame.Middle(a));
                    top[j] = apex;
                    hem[j] = Vector2.Lerp(rim[j], new Vector2(rim3.x, rim3.z), drop);
                    if (j == Points / 2) hemNorth = rim3.z;
                }

                if (canopy)
                {
                    // The veil on the south half hangs in front of whoever is inside; the north half behind them.
                    bool south = hemNorth < 0.1f;
                    DrawBetween(veils[k], rim, hem, sage.WithY(south ? overhead + 0.065f : pawn - 0.01f),
                        Fade(Rim, 0.2f * SixPathsUmbrellaTiming.Veil));
                    for (int n = 0; n < 3; n++)
                    {
                        int j = 1 + n * 3;
                        three[0] = rim[j]; three[1] = (rim[j] + hem[j]) * 0.5f; three[2] = hem[j];
                        DrawLine(threads[k * 3 + n], three, 0.03f, sage.WithY(south ? overhead + 0.066f : pawn - 0.009f),
                            Fade(Rim, 0.55f * SixPathsUmbrellaTiming.Veil));
                    }
                }

                // The inside of the shell is darker than the outside.
                float tone = panel.outside ? 0.22f + 0.4f * panel.lit : 0.06f;
                DrawBetween(lows[k], middle, rim, sage.WithY(layer),
                    Fade(Color.Lerp(Body, Slate, tone), SixPathsUmbrellaTiming.Opacity));
                DrawBetween(tops[k], top, middle, sage.WithY(layer + 0.0002f),
                    Fade(Color.Lerp(Body, Slate, tone + (panel.outside ? 0.12f : 0.04f)), SixPathsUmbrellaTiming.Opacity));
                DrawLine(hems[k], rim, 0.05f, sage.WithY(layer + 0.0004f), Fade(Rim, 0.9f));

                // A blocked hit lights what paid for it: one panel overhead, the whole guard in front.
                if (flash <= 0f || canopy && k != 0) continue;
                DrawBetween(flashLows[k], middle, rim, sage.WithY(layer + 0.0006f), Fade(Pale, flash));
                DrawBetween(flashTops[k], top, middle, sage.WithY(layer + 0.0006f), Fade(Pale, flash));
            }

            // Ribs stay even where a panel is gone, like a broken umbrella.
            for (int k = 0; k < P; k++)
            {
                float a = (k + 0.5f) * Mathf.PI / 3f;
                three[0] = apex; three[1] = SixPathsUmbrellaTiming.Show(frame.Middle(a));
                three[2] = SixPathsUmbrellaTiming.Show(frame.Rim(a, k));
                DrawLine(ribs[k], three, 0.04f, sage.WithY(bottom + 0.013f), Fade(Rim, 0.85f));
            }
            DrawSprite(sage, apex, bottom + 0.014f, 0.22f, 0.22f, Fade(Pale, 0.8f * openness), softGlow);

            // Where the hit struck: on the veil, with a ripple up to the rim, or on the dome.
            float struck = SixPathsUmbrellaTiming.Flash(seconds);
            if (struck <= 0f) return;
            Vector2 strike = SixPathsUmbrellaTiming.Strike(mode, toward);
            DrawSprite(sage, strike, overhead + 0.12f, 0.7f, 0.7f, Fade(Pale, struck * 0.9f), softGlow);
            if (!canopy) return;
            three[0] = strike;
            three[1] = SixPathsUmbrellaTiming.Rel(toward, SixPathsUmbrellaTiming.Radius, 0f, SixPathsUmbrellaTiming.ShotHeight
                + (SixPathsUmbrellaTiming.RimHeight - SixPathsUmbrellaTiming.ShotHeight) * (1f - struck));
            three[2] = SixPathsUmbrellaTiming.Show(frame.Rim(0f, 0));
            DrawLine(ripple, three, 0.07f, sage.WithY(overhead + 0.11f), Fade(Pale, struck * 0.8f));
        }

        /// <summary>What broke falls away as shards: one panel, or the whole guard.</summary>
        private static void DrawShards(Vector3 sage, Vector2 toward, UmbrellaMode mode, float seconds)
        {
            float layer = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.13f;
            for (int i = 0; i < SixPathsUmbrellaTiming.Shards(mode); i++)
            {
                ImpactParticle piece = SixPathsUmbrellaTiming.Shard(i, seconds, mode, toward);
                if (piece.alpha <= 0.001f) continue;
                DrawMesh(shard, (sage + new Vector3(piece.x, 0f, piece.z)).WithY(layer), piece.size, piece.size,
                    piece.rotation, Fade(Color.Lerp(Body, Slate, 0.3f), piece.alpha), solid);
            }
        }

        /// <summary>A carried slot as a drawn point relative to the sage, height included.</summary>
        private static Vector2 Home(Vector3 sage, in CarriedSlot slot)
        {
            Vector3 at = SixPathsHeight.Above(slot.ground, slot.height) - sage;
            return new Vector2(at.x, at.z);
        }

        private static void DrawBall(Vector3 sage, Vector2 at, float altitude, float radius) =>
            SixPathsGraphics.DrawBall((sage + new Vector3(at.x, 0f, at.y)).WithY(altitude), radius, 1f, 1f);

        private static void DrawSprite(Vector3 sage, Vector2 at, float altitude, float width, float depth,
            Color colour, Material material) =>
            DrawMesh(MeshPool.plane10, (sage + new Vector3(at.x, 0f, at.y)).WithY(altitude), width, depth, 0f, colour, material);

        private static void DrawBetween(SixPathsStrip strip, Vector2[] a, Vector2[] b, Vector3 at, Color colour)
        {
            strip.Between(a, b);
            DrawMesh(strip.mesh, at, 1f, 1f, 0f, colour, solid);
        }

        private static void DrawLine(SixPathsStrip strip, Vector2[] points, float width, Vector3 at, Color colour)
        {
            strip.Line(points, width);
            DrawMesh(strip.mesh, at, 1f, 1f, 0f, colour, solid);
        }

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth,
            float rotation, Color colour, Material material)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.Euler(0f, rotation, 0f),
                new Vector3(width, 1f, depth)), material, 0, null, 0, properties);
        }

        private static SixPathsStrip Strip(string name, int points) => new SixPathsStrip("Six Paths umbrella " + name, points);

        private static SixPathsStrip[] Strips(string name, int count, int points)
        {
            var made = new SixPathsStrip[count];
            for (int i = 0; i < count; i++) made[i] = Strip(name + " " + i, points);
            return made;
        }

        /// <summary>A thin unit-radius band, for the canopy's true radius on the floor.</summary>

        /// <summary>One unit shard, its corners listed clockwise on screen. It is turned and scaled by the draw.</summary>
        private static Mesh Shard()
        {
            float[] corners = { 0f, 4.1f, 2.2f };
            var vertices = new Vector3[3];
            for (int i = 0; i < 3; i++) vertices[i] = new Vector3(Mathf.Cos(corners[i]), 0f, Mathf.Sin(corners[i]));
            var mesh = new Mesh { name = "Six Paths umbrella shard", vertices = vertices, triangles = new[] { 0, 1, 2 } };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
