using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// Draws Repulse Step: the sage's ring with two slots empty, those orbs' flights to the post
    /// anchors, the two padded posts with their feet and bindings, the five ropes, the flash at
    /// the release, the slipstream and dust of the flight, and the orbs' flight after the sage.
    /// SixPathsRepulseTiming says where everything is; this only turns it into meshes.
    ///
    /// Launching east or west the ropes draw over the posts, so all five ends stay visible; north
    /// or south they draw under them.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsRepulseGraphics
    {
        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh disc = VfxDraw.Ring(0f, "Six Paths repulse disc");

        // One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh
        // when the frame renders, not when it is called.
        private static readonly Post[] posts = { new Post("west"), new Post("east") };
        private static readonly Rope[] ropes = NewRopes();
        private static readonly SixPathsStrip[] gatherTrails = Strips("gather", 2, SixPathsRepulseTiming.GatherPoints),
            recallTrails = Strips("recall", 2, SixPathsRepulseTiming.RecallPoints),
            slipstreams = Strips("slipstream", SixPathsRepulseTiming.Slipstreams, SixPathsRepulseTiming.SlipstreamPoints);
        private static readonly Vector2[] gatherPoints = new Vector2[SixPathsRepulseTiming.GatherPoints],
            recallPoints = new Vector2[SixPathsRepulseTiming.RecallPoints],
            slipstreamPoints = new Vector2[SixPathsRepulseTiming.SlipstreamPoints];

        private static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Pale = new Color(0.88f, 0.79f, 1f);
        private static readonly Color PadSide = new Color(0.16f, 0.12f, 0.22f);
        private static readonly Color Foot = new Color(0.12f, 0.085f, 0.17f);
        private static readonly Color Hole = new Color(0.018f, 0.014f, 0.024f);
        private static readonly Color Dust = new Color(0.56f, 0.49f, 0.40f);

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone. <paramref name="launch"/>
        /// is the cell the sage starts on, <paramref name="sage"/> is where it is now, and
        /// <paramref name="toward"/> is the unit launch direction.
        /// </summary>
        public static void Draw(Vector3 launch, Vector3 sage, Vector2 toward, float seconds, Map map)
        {
            if (seconds < 0f || seconds > SixPathsRepulseTiming.Duration) return;
            if (!Shown(launch, map) || !Shown(sage, map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector
                * SixPathsSlamGraphics.SunScale;
            float daylight = GenCelestial.CurShadowStrength(map), strength = 0.32f * daylight;
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();

            // Slots 0 and 1 are the posts. The ring rides with the sage.
            SixPathsGraphics.DrawCarried(sage, seconds, 0b11, sun, daylight);
            float growth = SixPathsRepulseTiming.Growth(seconds);
            for (int i = 0; i < 2; i++)
            {
                DrawGather(i, launch, sage, toward, seconds);
                if (growth > 0.001f) posts[i].Draw(launch, toward, i == 0 ? -1 : 1, seconds, sun, strength);
            }
            if (growth > 0.001f)
                for (int i = 0; i < ropes.Length; i++) ropes[i].Draw(i, launch, toward, seconds, sun, strength);

            float age = SixPathsRepulseTiming.Age(seconds);
            if (age >= 0f && age < 0.16f)
            {
                Vector2 contact = SixPathsRepulseTiming.Contact(toward, seconds).At;
                DrawMesh(MeshPool.plane10, (launch + Flat(contact)).WithY(overhead + 0.03f), 0.7f, 0.85f,
                    Fade(Pale, (1f - age / 0.16f) * 0.75f), softGlow);
            }
            if (age >= 0f) DrawFlight(launch, toward, seconds);
            for (int i = 0; i < 2; i++) DrawRecall(i, launch, sage, toward, seconds);
        }

        private static Vector3 Flat(Vector2 at) => new Vector3(at.x, 0f, at.y);

        /// <summary>One orb from its ring slot to its post anchor, where it lengthens into the post.</summary>
        private static void DrawGather(int index, Vector3 launch, Vector3 sage, Vector2 toward, float seconds)
        {
            float formed = SixPathsRepulseTiming.Formed(seconds);
            if (seconds >= SixPathsRepulseTiming.LoadAt && formed >= 1f) return;
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), gathered = SixPathsRepulseTiming.Gathered(seconds);
            CarriedSlot slot = SixPathsCarried.Slot(sage, index, seconds);
            Vector3 home = SixPathsHeight.Above(slot.ground, slot.height) - launch;
            var from = new Vector2(home.x, home.z);
            Vector2 anchor = SixPathsRepulseTiming.Anchor(toward, index == 0 ? -1 : 1, SixPathsRepulseTiming.CentreHeight).At;
            if (formed < 1f)
                SixPathsGraphics.DrawBall(
                    (launch + Flat(Vector2.LerpUnclamped(from, anchor, gathered)))
                        .WithY((gathered > 0f ? overhead : SixPathsGraphics.CarriedAltitude(slot)) + index * 0.004f),
                    SixPathsCarried.DeployRadius(gathered, SixPathsCarried.Field) * (1f - formed), 1f + formed * 2f, 1f);
            if (seconds >= SixPathsRepulseTiming.LoadAt) return;
            for (int j = 0; j < gatherPoints.Length; j++)
                gatherPoints[j] = Vector2.LerpUnclamped(from, anchor, SixPathsRepulseTiming.GatherTrail(j, seconds));
            gatherTrails[index].Line(gatherPoints, 0.085f);
            // Under both orbs, as in the sketch, where an orb's body covers the head of its trail.
            DrawMesh(gatherTrails[index].mesh, launch.WithY(overhead - 0.002f + index * 0.0002f), 1f, 1f, Fade(Rim, (1f - formed) * 0.5f), solid);
        }

        /// <summary>Three lines of slipstream behind the sage, and dust that stays where it was kicked up.</summary>
        private static void DrawFlight(Vector3 launch, Vector2 toward, float seconds)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), alpha = SixPathsRepulseTiming.SlipstreamAlpha(seconds);
            for (int i = 0; i < slipstreams.Length; i++)
            {
                for (int j = 0; j < slipstreamPoints.Length; j++)
                    slipstreamPoints[j] = SixPathsRepulseTiming.Slipstream(toward, i, j, seconds).At;
                slipstreams[i].Line(slipstreamPoints, 0.07f);
                DrawMesh(slipstreams[i].mesh, launch.WithY(overhead + 0.02f + i * 0.0002f), 1f, 1f, Fade(Pale, alpha), solid);
            }
            for (int i = 0; i < SixPathsRepulseTiming.DustPuffs; i++)
            {
                SlingDust puff = SixPathsRepulseTiming.Dust(toward, i, seconds);
                if (puff.alpha <= 0f) continue;
                DrawMesh(MeshPool.plane10, (launch + Flat(puff.at)).WithY(AltitudeLayer.Shadows.AltitudeFor() + 0.02f + i * 0.0001f),
                    puff.width, puff.depth, Fade(Dust, puff.alpha), soft);
            }
        }

        /// <summary>One orb from where its post stood to its ring slot on the sage, shrinking on the way.</summary>
        private static void DrawRecall(int index, Vector3 launch, Vector3 sage, Vector2 toward, float seconds)
        {
            float recalled = SixPathsRepulseTiming.Recalled(seconds);
            if (recalled <= 0f) return;
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
            CarriedSlot slot = SixPathsCarried.Slot(sage, index, seconds);
            Vector3 home = SixPathsHeight.Above(slot.ground, slot.height) - launch;
            var to = new Vector2(home.x, home.z);
            Vector2 anchor = SixPathsRepulseTiming.Anchor(toward, index == 0 ? -1 : 1, SixPathsRepulseTiming.CentreHeight).At;
            SixPathsGraphics.DrawBall(
                (launch + Flat(Vector2.LerpUnclamped(anchor, to, recalled)))
                    .WithY((recalled < 1f ? overhead : SixPathsGraphics.CarriedAltitude(slot)) + index * 0.004f),
                SixPathsCarried.DeployRadius(1f - recalled, SixPathsCarried.Field) * Mathf.Min(1f, recalled * 4f), 1f, 1f);
            for (int j = 0; j < recallPoints.Length; j++)
                recallPoints[j] = Vector2.LerpUnclamped(anchor, to, SixPathsRepulseTiming.RecallTrail(j, seconds));
            recallTrails[index].Line(recallPoints, 0.065f);
            DrawMesh(recallTrails[index].mesh, launch.WithY(overhead - 0.002f + index * 0.0002f), 1f, 1f, Fade(Rim, (1f - recalled) * 0.5f), solid);
        }

        private static Rope[] NewRopes()
        {
            var made = new Rope[SixPathsRepulseTiming.Ropes];
            for (int i = 0; i < made.Length; i++) made[i] = new Rope(i);
            return made;
        }

        private static SixPathsStrip[] Strips(string name, int count, int points)
        {
            var made = new SixPathsStrip[count];
            for (int i = 0; i < count; i++) made[i] = new SixPathsStrip("Six Paths repulse " + name + " " + i, points);
            return made;
        }

        private static bool Shown(Vector3 at, Map map) => at.ToIntVec3().InBounds(map) && !at.ToIntVec3().Fogged(map);

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth, Color colour, Material material)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(width, 1f, depth)),
                material, 0, null, 0, properties);
        }

        /// <summary>One post's meshes: shadow, the pad with its side wall and bevel, the flared foot with its lip, and two bindings.</summary>
        private sealed class Post
        {
            private const int Rows = SixPathsRepulseTiming.PostRows;
            private readonly SixPathsStrip shadow, side, pad, bevel, foot, lip;
            private readonly SixPathsStrip[] bindings = new SixPathsStrip[2];
            private readonly Vector2[] left = new Vector2[Rows], right = new Vector2[Rows], inset = new Vector2[Rows],
                wall = new Vector2[Rows], shadowLeft = new Vector2[Rows], shadowRight = new Vector2[Rows],
                footLeft = new Vector2[2], footRight = new Vector2[2], three = new Vector2[3];

            public Post(string name)
            {
                name = "Six Paths repulse post " + name;
                shadow = new SixPathsStrip(name + " shadow", Rows);
                side = new SixPathsStrip(name + " side", Rows);
                pad = new SixPathsStrip(name + " pad", Rows);
                bevel = new SixPathsStrip(name + " bevel", Rows);
                foot = new SixPathsStrip(name + " foot", 2);
                lip = new SixPathsStrip(name + " lip", 3);
                for (int k = 0; k < bindings.Length; k++) bindings[k] = new SixPathsStrip(name + " binding " + k, 3);
            }

            public void Draw(Vector3 launch, Vector2 toward, int which, float seconds, Vector2 sun, float strength)
            {
                float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), shadows = AltitudeLayer.Shadows.AltitudeFor();
                float growth = SixPathsRepulseTiming.Growth(seconds), alpha = 1f - SixPathsRepulseTiming.Recalled(seconds);
                // The east post's pieces go a hair over the west post's at each step.
                float east = which > 0 ? 0.0002f : 0f;
                const float rim = SixPathsRepulseTiming.Pad;
                for (int j = 0; j < Rows; j++)
                {
                    SlingPoint q = SixPathsRepulseTiming.PostRow(toward, which, j, seconds, out float bulge);
                    // Pad width is across the screen on purpose: launching east or west the post must keep its mass.
                    Vector2 at = q.At, half = new Vector2(bulge * 0.5f, 0f), cast = q.ground + sun * q.height;
                    left[j] = at - half; right[j] = at + half;
                    inset[j] = left[j] + new Vector2(0.035f, 0f);
                    wall[j] = right[j] + new Vector2(0.075f * growth, -0.045f * growth);
                    shadowLeft[j] = cast - half; shadowRight[j] = cast + half;
                }
                shadow.Between(shadowLeft, shadowRight);
                side.Between(right, wall);
                pad.Between(left, right);
                bevel.Between(left, inset);
                DrawMesh(shadow.mesh, launch.WithY(shadows + east), 1f, 1f, Fade(Body, strength * alpha * 0.65f), solid);
                DrawMesh(side.mesh, launch.WithY(overhead + 0.018f + east), 1f, 1f, Fade(PadSide, alpha), solid);
                DrawMesh(pad.mesh, launch.WithY(overhead + 0.020f + east), 1f, 1f, Fade(Body, alpha), solid);
                DrawMesh(bevel.mesh, launch.WithY(overhead + 0.022f + east), 1f, 1f, Fade(Rim, alpha * 0.75f), solid);

                // The post reaches a fixed ground socket; a dense contact patch and a flared foot
                // join the pad to the terrain. Launching east or west the far foot lands inside the
                // rope fan, so both feet shrink.
                float planted = SixPathsRepulseTiming.Planted(seconds), size = SixPathsRepulseTiming.SideView(toward) ? 0.6f : 1f;
                float flare = rim * 0.80f * size;
                Vector3 root = launch + Flat(SixPathsRepulseTiming.Anchor(toward, which, 0f).ground);
                DrawMesh(MeshPool.plane10, root.WithY(shadows + 0.004f + east), 0.72f * size, 0.38f * size, Fade(Body, strength * 0.85f * planted), soft);
                DrawMesh(disc, root.WithY(shadows + 0.005f + east), rim * 1.04f * size, 0.12f * size, Fade(Hole, planted), solid);
                Vector2 low = SixPathsRepulseTiming.Anchor(toward, which, 0.025f).At, high = SixPathsRepulseTiming.Anchor(toward, which, 0.22f).At;
                footLeft[0] = low - new Vector2(flare, 0f); footLeft[1] = high - new Vector2(rim * 0.43f, 0f);
                footRight[0] = low + new Vector2(flare, 0f); footRight[1] = high + new Vector2(rim * 0.43f, 0f);
                foot.Between(footLeft, footRight);
                DrawMesh(foot.mesh, launch.WithY(overhead + 0.025f + east), 1f, 1f, Fade(Foot, planted), solid);
                three[0] = footLeft[0]; three[1] = low + new Vector2(0f, 0.025f); three[2] = footRight[0];
                lip.Line(three, 0.032f);
                DrawMesh(lip.mesh, launch.WithY(overhead + 0.026f + east), 1f, 1f, Fade(Rim, planted * 0.6f), solid);

                // Small violet bindings make the ends feel padded rather than metallic.
                for (int k = 0; k < bindings.Length; k++)
                {
                    Vector2 at = SixPathsRepulseTiming.Anchor(toward, which, SixPathsRepulseTiming.BindingHeight(k, seconds)).At;
                    three[0] = at - new Vector2(rim * 0.48f, 0f); three[1] = at + new Vector2(0f, 0.016f); three[2] = at + new Vector2(rim * 0.48f, 0f);
                    bindings[k].Line(three, 0.048f);
                    DrawMesh(bindings[k].mesh, launch.WithY(overhead + 0.024f + east + k * 0.0001f), 1f, 1f, Fade(Rim, alpha * 0.5f), solid);
                }
            }
        }

        /// <summary>One rope's meshes: its ground shadow, its violet edge, its body and the shine along its top.</summary>
        private sealed class Rope
        {
            private const int Points = SixPathsRepulseTiming.RopePoints;
            private readonly SixPathsStrip shadow, edge, body, shine;
            private readonly Vector2[] line = new Vector2[Points], cast = new Vector2[Points], top = new Vector2[Points];

            public Rope(int index)
            {
                string name = "Six Paths repulse rope " + index;
                shadow = new SixPathsStrip(name + " shadow", Points);
                edge = new SixPathsStrip(name + " edge", Points);
                body = new SixPathsStrip(name + " body", Points);
                shine = new SixPathsStrip(name + " shine", Points);
            }

            public void Draw(int index, Vector3 launch, Vector2 toward, float seconds, Vector2 sun, float strength)
            {
                float growth = SixPathsRepulseTiming.Growth(seconds), alpha = 1f - SixPathsRepulseTiming.Recalled(seconds);
                for (int j = 0; j < Points; j++)
                {
                    SlingPoint q = SixPathsRepulseTiming.RopePoint(toward, index, j, seconds);
                    line[j] = q.At;
                    cast[j] = q.ground + sun * q.height;
                    top[j] = line[j] + new Vector2(0f, 0.020f);
                }
                shadow.Line(cast, 0.072f);
                edge.Line(line, 0.085f * growth);
                body.Line(line, 0.050f * growth);
                shine.Line(top, 0.015f * growth);
                // Each rope whole over the one below it; over the posts launching east or west, under them otherwise.
                float layer = AltitudeLayer.MoteOverhead.AltitudeFor() + (SixPathsRepulseTiming.SideView(toward) ? 0.030f : 0.006f) + index * 0.002f;
                DrawMesh(shadow.mesh, launch.WithY(AltitudeLayer.Shadows.AltitudeFor() + 0.002f + index * 0.0001f), 1f, 1f,
                    Fade(Body, strength * alpha * 0.6f), solid);
                DrawMesh(edge.mesh, launch.WithY(layer), 1f, 1f, Fade(Rim, alpha * 0.85f), solid);
                DrawMesh(body.mesh, launch.WithY(layer + 0.0006f), 1f, 1f, Fade(Body, alpha), solid);
                DrawMesh(shine.mesh, launch.WithY(layer + 0.0012f), 1f, 1f,
                    Fade(Pale, alpha * (0.24f + SixPathsRepulseTiming.Loaded(seconds) * 0.32f)), solid);
            }
        }
    }
}
