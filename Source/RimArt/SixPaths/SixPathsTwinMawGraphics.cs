using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws Twin Maw: the sage's ring with two slots empty, those orbs' flights out and back, the
    /// armed seam and sockets, the four jaw rims with their teeth and shadows, the burst at each
    /// socket when the trap is sprung, and the bite flashes. SixPathsTwinMawTiming says where
    /// everything is; this only turns it into meshes.
    ///
    /// A rim is four strips and a mesh of teeth, rebuilt every frame it shows. The two back rims
    /// draw under the two front rims, and whatever is caught stands between them.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsTwinMawGraphics
    {
        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh armedRing = SixPathsBurstGraphics.Band(0.95f, "Six Paths maw armed ring");
        private static readonly Mesh disc = SixPathsBurstGraphics.Band(0f, "Six Paths maw socket");

        // One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh
        // when the frame renders, not when it is called.
        private static readonly JawRim[] backRims = { new JawRim("back west"), new JawRim("back east") };
        private static readonly JawRim[] frontRims = { new JawRim("front west"), new JawRim("front east") };
        private static readonly SixPathsStrip[] trails =
        {
            new SixPathsStrip("Six Paths maw orb trail west", SixPathsTwinMawTiming.TrailPoints),
            new SixPathsStrip("Six Paths maw orb trail east", SixPathsTwinMawTiming.TrailPoints),
        };
        private static readonly Vector2[] trailPoints = new Vector2[SixPathsTwinMawTiming.TrailPoints];
        private static readonly SixPathsBurstGraphics[] bursts =
            { new SixPathsBurstGraphics("Six Paths maw burst west"), new SixPathsBurstGraphics("Six Paths maw burst east") };
        // The seam never moves, so its two lines are built once.
        private static readonly SixPathsStrip seamDark = Seam("Six Paths maw seam dark", 0.11f);
        private static readonly SixPathsStrip seamEdge = Seam("Six Paths maw seam edge", 0.04f);

        private static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        private static readonly Color Slate = new Color(0.26f, 0.21f, 0.38f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Pale = new Color(0.88f, 0.79f, 1f);
        private static readonly Color Hole = new Color(0.018f, 0.014f, 0.024f);
        private static readonly Color Dust = new Color(0.56f, 0.49f, 0.40f);

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone. <paramref name="tile"/>
        /// is the middle of the trap's cell and <paramref name="sage"/> is where the caster stands.
        /// </summary>
        public static void Draw(Vector3 tile, Vector3 sage, float seconds, Map map)
        {
            if (seconds < 0f || seconds > SixPathsTwinMawTiming.Duration) return;
            if (!Shown(tile, map) || !Shown(sage, map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector
                * SixPathsSlamGraphics.SunScale;
            float daylight = GenCelestial.CurShadowStrength(map), strength = 0.32f * daylight;
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();

            // Slots 0 and 1 are the orbs this costs. They are away for the whole of the effect.
            SixPathsGraphics.DrawCarried(sage, seconds, 0b11, sun, daylight);
            DrawArmed(tile, seconds, strength);
            for (int i = 0; i < 2; i++) DrawOrb(i, tile, sage, seconds);
            for (int i = 0; i < 2; i++)
                bursts[i].Draw(tile + Flat(SixPathsTwinMawTiming.Socket(Side(i))), seconds - SixPathsTwinMawTiming.TriggerAt, 0.7f, 0.5f);

            if (SixPathsTwinMawTiming.Grow(seconds) > 0.001f)
            {
                // West then east at each step, as the sketch orders them, so the east rim's edge
                // still goes under the west rim's body where the tips cross.
                for (int i = 0; i < 2; i++)
                    backRims[i].Draw(tile, Side(i), SixPathsTwinMawTiming.RimDepth, 1f, seconds, sun, strength,
                        overhead + 0.02f + i * 0.0002f, 0.12f);
                for (int i = 0; i < 2; i++)
                    frontRims[i].Draw(tile, Side(i), -SixPathsTwinMawTiming.RimDepth, SixPathsTwinMawTiming.FrontTall, seconds, sun, strength,
                        overhead + 0.09f + i * 0.0002f, 0.2f);
            }

            for (int k = 0; k < SixPathsTwinMawTiming.Bites; k++)
            {
                float flash = SixPathsTwinMawTiming.Bite(k, seconds);
                if (flash <= 0f) continue;
                DrawMesh(MeshPool.plane10, (tile + new Vector3(0f, 0f, 0.42f)).WithY(overhead + 0.12f + k * 0.0002f),
                    1.5f - k * 0.25f, 0.9f - k * 0.15f, Fade(Pale, flash * (k > 0 ? 0.45f : 0.8f)), softGlow);
            }
        }

        private static int Side(int index) => index == 0 ? -1 : 1;

        private static Vector3 Flat(Vector2 at) => new Vector3(at.x, 0f, at.y);

        /// <summary>The armed marker: a faint ring, the toothed seam, and the two sockets the orbs went into.</summary>
        private static void DrawArmed(Vector3 tile, float seconds, float strength)
        {
            float planted = SixPathsTwinMawTiming.Planted(seconds);
            if (planted <= 0.001f) return;
            float floor = AltitudeLayer.Filth.AltitudeFor(), shadows = AltitudeLayer.Shadows.AltitudeFor();
            float armed = planted * (1f - 0.75f * SixPathsTwinMawTiming.Grow(seconds)), pulse = SixPathsTwinMawTiming.Pulse(seconds);
            DrawMesh(armedRing, tile.WithY(floor + 0.008f), SixPathsTwinMawTiming.Reach * 1.18f, 0.52f,
                Fade(Rim, armed * (0.14f + 0.1f * pulse)), solid);
            DrawMesh(seamDark.mesh, tile.WithY(floor + 0.009f), 1f, 1f, Fade(Body, armed * 0.8f), solid);
            DrawMesh(seamEdge.mesh, tile.WithY(floor + 0.010f), 1f, 1f, Fade(Rim, armed * (0.35f + 0.35f * pulse)), solid);
            for (int i = 0; i < 2; i++)
            {
                Vector3 socket = tile + Flat(SixPathsTwinMawTiming.Socket(Side(i)));
                DrawMesh(MeshPool.plane10, socket.WithY(shadows + 0.004f + i * 0.0002f), 0.7f, 0.36f,
                    Fade(Body, strength * 0.85f * planted), soft);
                DrawMesh(disc, socket.WithY(shadows + 0.005f + i * 0.0002f), 0.2f, 0.1f, Fade(Hole, planted), solid);
            }
        }

        /// <summary>One orb between its ring slot and its socket, outbound at the start and homebound at the end.</summary>
        private static void DrawOrb(int index, Vector3 tile, Vector3 sage, float seconds)
        {
            if (!SixPathsTwinMawTiming.OrbsShown(seconds)) return;
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
            CarriedSlot slot = SixPathsCarried.Slot(sage, index, seconds);
            Vector3 home = SixPathsHeight.Above(slot.ground, slot.height) - tile;
            Vector2 from = new Vector2(home.x, home.z), socket = SixPathsTwinMawTiming.Socket(Side(index));

            bool outbound = seconds < SixPathsTwinMawTiming.Arm;
            float flown = SixPathsTwinMawTiming.Flown(seconds);
            float sinking = outbound ? SixPathsTwinMawTiming.Sinking(seconds) : 0f;
            // Coming back, the orb rises out of the socket before it sets off.
            float emerge = outbound ? 1f : Mathf.Min(1f, (1f - flown) * 4f);
            Vector2 at = SixPathsTwinMawTiming.Path(from, socket, flown);
            SixPathsGraphics.DrawBall(
                (tile + Flat(at)).WithY((flown > 0f ? overhead : SixPathsGraphics.CarriedAltitude(slot)) + index * 0.004f),
                SixPathsCarried.DeployRadius(flown, SixPathsCarried.Field) * (1f - sinking) * emerge, 1f - sinking * 0.5f, 1f);

            float start = outbound ? 0f : SixPathsTwinMawTiming.GoneAt;
            for (int j = 0; j < trailPoints.Length; j++)
            {
                float lag = (1f - j / (float)(trailPoints.Length - 1)) * SixPathsTwinMawTiming.TrailSeconds;
                trailPoints[j] = SixPathsTwinMawTiming.Path(from, socket, SixPathsTwinMawTiming.Flown(Mathf.Max(start, seconds - lag)));
            }
            trails[index].Line(trailPoints, 0.08f);
            // Under both orbs, as in the sketch, where an orb's body covers the head of its trail.
            DrawMesh(trails[index].mesh, tile.WithY(overhead - 0.002f + index * 0.0002f), 1f, 1f, Fade(Rim, 0.5f * (1f - sinking)), solid);

            if (outbound && sinking > 0f)
                DrawMesh(MeshPool.plane10, (tile + Flat(socket)).WithY(AltitudeLayer.Shadows.AltitudeFor() + 0.02f + index * 0.0002f),
                    0.5f + sinking * 0.5f, 0.25f + sinking * 0.25f,
                    Fade(Dust, Mathf.Max(0f, Mathf.Sin(sinking * Mathf.PI)) * 0.35f), soft);
        }

        private static SixPathsStrip Seam(string name, float width)
        {
            var points = new Vector2[SixPathsTwinMawTiming.SeamPoints];
            for (int j = 0; j < points.Length; j++) points[j] = SixPathsTwinMawTiming.Seam(j);
            var strip = new SixPathsStrip(name, points.Length);
            strip.Line(points, width);
            return strip;
        }

        private static bool Shown(Vector3 at, Map map) => at.ToIntVec3().InBounds(map) && !at.ToIntVec3().Fogged(map);

        private static Color Fade(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth, Color colour, Material material)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(width, 1f, depth)),
                material, 0, null, 0, properties);
        }

        /// <summary>One rim's meshes: its ground shadow, its violet edge, its body, the lit ridge on its outer side, and its teeth.</summary>
        private sealed class JawRim
        {
            private const int Rows = SixPathsTwinMawTiming.Rows;
            private readonly SixPathsStrip shadow, edge, body, ridge;
            private readonly Mesh teeth;
            private readonly Vector3[] toothVertices = new Vector3[SixPathsTwinMawTiming.Teeth * 3];
            private readonly JawRow[] rows = new JawRow[Rows];
            private readonly Vector2[] outer = new Vector2[Rows], inner = new Vector2[Rows], edgeOuter = new Vector2[Rows],
                edgeInner = new Vector2[Rows], crest = new Vector2[Rows], shadowA = new Vector2[Rows], shadowB = new Vector2[Rows];

            public JawRim(string name)
            {
                name = "Six Paths maw " + name;
                shadow = new SixPathsStrip(name + " shadow", Rows);
                edge = new SixPathsStrip(name + " edge", Rows);
                body = new SixPathsStrip(name + " body", Rows);
                ridge = new SixPathsStrip(name + " ridge", Rows);
                var indices = new int[toothVertices.Length];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                teeth = new Mesh { name = name + " teeth", vertices = toothVertices };
                teeth.triangles = indices;
            }

            public void Draw(Vector3 tile, int side, float depth, float tall, float seconds, Vector2 sun, float strength,
                float layer, float shade)
            {
                SixPathsTwinMawTiming.Rim(side, depth, tall, seconds, rows);
                for (int j = 0; j < Rows; j++)
                {
                    JawRow q = rows[j];
                    float half = q.width / 2f;
                    outer[j] = q.at - q.inward * half;
                    inner[j] = q.at + q.inward * half;
                    edgeOuter[j] = q.at - q.inward * (half + 0.028f);
                    edgeInner[j] = q.at + q.inward * (half + 0.028f);
                    crest[j] = q.at - q.inward * (half * 0.25f);
                    Vector2 cast = q.ground + sun * q.height;
                    shadowA[j] = cast - new Vector2(half, 0f);
                    shadowB[j] = cast + new Vector2(half, 0f);
                }
                shadow.Between(shadowA, shadowB);
                edge.Between(edgeOuter, edgeInner);
                body.Between(outer, inner);
                ridge.Between(outer, crest);

                // The west and east rims share each step of the ladder, west first.
                float east = side > 0 ? 0.0002f : 0f;
                DrawMesh(shadow.mesh, tile.WithY(AltitudeLayer.Shadows.AltitudeFor() + 0.001f + east), 1f, 1f, Fade(Body, strength * 0.6f), solid);
                DrawMesh(edge.mesh, tile.WithY(layer), 1f, 1f, Fade(Rim, 0.9f), solid);
                DrawMesh(body.mesh, tile.WithY(layer + 0.001f), 1f, 1f, Color.Lerp(Body, Slate, shade), solid);
                DrawMesh(ridge.mesh, tile.WithY(layer + 0.002f), 1f, 1f, Color.Lerp(Body, Slate, shade + 0.45f), solid);
                if (RebuildTeeth(side, seconds))
                    DrawMesh(teeth, tile.WithY(layer + 0.003f), 1f, 1f, Fade(Pale, 0.95f), solid);
            }

            /// <summary>False when no tooth is out of the floor yet. A tooth still under it is left with no area.</summary>
            private bool RebuildTeeth(int side, float seconds)
            {
                bool any = false;
                for (int k = 0; k < SixPathsTwinMawTiming.Teeth; k++)
                {
                    int row = SixPathsTwinMawTiming.ToothRow(k, side, seconds, out float length), v = k * 3;
                    if (row < 0)
                    {
                        toothVertices[v] = toothVertices[v + 1] = toothVertices[v + 2] = Vector3.zero;
                        continue;
                    }
                    any = true;
                    JawRow q = rows[row];
                    Vector2 root = q.at + q.inward * (q.width * 0.4f);
                    Vector2 a = root - q.along * 0.075f, b = root + q.along * 0.075f;
                    Vector2 tip = root + q.inward * length - new Vector2(0f, 0.04f);
                    // Clockwise on screen, whichever way the rim runs; the other way is the back side.
                    bool back = (b.x - a.x) * (tip.y - a.y) - (b.y - a.y) * (tip.x - a.x) > 0f;
                    toothVertices[v] = new Vector3(a.x, 0f, a.y);
                    toothVertices[v + 1] = back ? new Vector3(tip.x, 0f, tip.y) : new Vector3(b.x, 0f, b.y);
                    toothVertices[v + 2] = back ? new Vector3(b.x, 0f, b.y) : new Vector3(tip.x, 0f, tip.y);
                }
                teeth.vertices = toothVertices;
                teeth.RecalculateBounds();
                return any;
            }
        }
    }
}
