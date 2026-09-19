using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws Black rods v2: the sage's ring with every slot empty, the six orbs' flights out and
    /// back, the crack along the line, and the twelve rods with their holes, thrown dirt, shadows
    /// and tip glows. SixPathsRodsTiming says where everything is; this only turns it into meshes.
    ///
    /// A rod is four strips, rebuilt every frame it shows. Rods further north draw first, so
    /// nearer ones overlap them.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsRodsGraphics
    {
        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh disc = SixPathsBurstGraphics.Band(0f, "Six Paths rods disc");

        // One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh
        // when the frame renders, not when it is called.
        private static readonly RodMeshes[] rods = NewRods();
        private static readonly SixPathsStrip[] trails = NewTrails();
        private static readonly Vector2[] trailPoints = new Vector2[SixPathsRodsTiming.TrailPoints];
        private static readonly SixPathsStrip crackDark = new SixPathsStrip("Six Paths rods crack dark", SixPathsRodsTiming.CrackPoints);
        private static readonly SixPathsStrip crackEdge = new SixPathsStrip("Six Paths rods crack edge", SixPathsRodsTiming.CrackPoints);
        private static readonly Vector2[] crackPoints = new Vector2[SixPathsRodsTiming.CrackPoints];

        private static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        private static readonly Color Face = new Color(0.085f, 0.062f, 0.12f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Pale = new Color(0.88f, 0.79f, 1f);
        private static readonly Color Dirt = new Color(0.34f, 0.26f, 0.19f);
        private static readonly Color Hole = new Color(0.018f, 0.014f, 0.024f);
        private static readonly Color Dust = new Color(0.56f, 0.49f, 0.40f);

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone. <paramref name="middle"/>
        /// is the middle of the wall, <paramref name="sage"/> is where the caster stands, and
        /// <paramref name="toward"/> is the unit cast direction; the wall runs across it.
        /// </summary>
        public static void Draw(Vector3 middle, Vector3 sage, Vector2 toward, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= SixPathsRodsTiming.Duration) return;
            if (!Shown(middle, map) || !Shown(sage, map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector
                * SixPathsSlamGraphics.SunScale;
            float daylight = GenCelestial.CurShadowStrength(map), strength = 0.32f * daylight;

            // All six orbs are the cost. Every slot is empty for the whole of the effect.
            SixPathsGraphics.DrawCarried(sage, seconds, 0b111111, sun, daylight);
            DrawCrack(middle, toward, seconds);
            for (int k = 0; k < SixPathsTiming.Orbs; k++) DrawOrb(k, middle, sage, toward, seconds);
            for (int i = 0; i < SixPathsRodsTiming.Rods; i++) rods[i].Draw(i, middle, toward, seconds, sun, strength);
        }

        /// <summary>The crack telegraphs the line, glows while the wall stands, and closes after it.</summary>
        private static void DrawCrack(Vector3 middle, Vector2 toward, float seconds)
        {
            float closing = SixPathsRodsTiming.Closing(seconds);
            if (seconds < SixPathsRodsTiming.CrackAt || closing <= 0f) return;
            for (int j = 0; j < crackPoints.Length; j++) crackPoints[j] = SixPathsRodsTiming.CrackPoint(toward, j, seconds);
            crackDark.Line(crackPoints, 0.16f);
            crackEdge.Line(crackPoints, 0.05f);
            float floor = AltitudeLayer.Filth.AltitudeFor();
            DrawMesh(crackDark.mesh, middle.WithY(floor + 0.008f), 1f, 1f, Fade(Body, 0.8f * closing), solid);
            DrawMesh(crackEdge.mesh, middle.WithY(floor + 0.009f), 1f, 1f, Fade(Rim, SixPathsRodsTiming.CrackGlow(seconds)), solid);
        }

        /// <summary>One orb between its ring slot and its socket, outbound at the start and homebound at the end.</summary>
        private static void DrawOrb(int index, Vector3 middle, Vector3 sage, Vector2 toward, float seconds)
        {
            if (!SixPathsRodsTiming.OrbsShown(seconds)) return;
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
            CarriedSlot slot = SixPathsCarried.Slot(sage, index, seconds);
            Vector3 home = SixPathsHeight.Above(slot.ground, slot.height) - middle;
            Vector2 from = new Vector2(home.x, home.z), socket = SixPathsRodsTiming.Socket(toward, index);

            bool outbound = seconds < SixPathsRodsTiming.Sink;
            float flown = SixPathsRodsTiming.Flown(seconds), drop = outbound ? SixPathsRodsTiming.Drop(seconds) : 0f;
            // Coming back, the orb rises out of the floor before it sets off.
            float emerge = outbound ? 1f : Mathf.Min(1f, (1f - flown) * 4f);
            Vector2 at = SixPathsRodsTiming.Path(from, socket, index, flown);
            SixPathsGraphics.DrawBall(
                (middle + new Vector3(at.x, 0f, at.y)).WithY((flown > 0f ? overhead : SixPathsGraphics.CarriedAltitude(slot)) + index * 0.004f),
                SixPathsCarried.DeployRadius(flown, SixPathsCarried.Field) * (1f - drop) * emerge, 1f - drop * 0.5f, 1f);

            float start = outbound ? 0f : SixPathsRodsTiming.GoneAt;
            for (int j = 0; j < trailPoints.Length; j++)
            {
                float lag = (1f - j / (float)(trailPoints.Length - 1)) * SixPathsRodsTiming.TrailSeconds;
                trailPoints[j] = SixPathsRodsTiming.Path(from, socket, index, SixPathsRodsTiming.Flown(Mathf.Max(start, seconds - lag)));
            }
            trails[index].Line(trailPoints, 0.07f);
            // Under every orb, as in the sketch, where an orb's body covers the head of its trail.
            DrawMesh(trails[index].mesh, middle.WithY(overhead - 0.002f + index * 0.0002f), 1f, 1f, Fade(Rim, 0.45f * (1f - drop)), solid);
        }

        private static RodMeshes[] NewRods()
        {
            var made = new RodMeshes[SixPathsRodsTiming.Rods];
            for (int i = 0; i < made.Length; i++) made[i] = new RodMeshes(i);
            return made;
        }

        private static SixPathsStrip[] NewTrails()
        {
            var made = new SixPathsStrip[SixPathsTiming.Orbs];
            for (int i = 0; i < made.Length; i++) made[i] = new SixPathsStrip("Six Paths rods orb trail " + i, SixPathsRodsTiming.TrailPoints);
            return made;
        }

        private static bool Shown(Vector3 at, Map map) => at.ToIntVec3().InBounds(map) && !at.ToIntVec3().Fogged(map);

        private static Color Fade(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth, Color colour, Material material)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(width, 1f, depth)),
                material, 0, null, 0, properties);
        }

        /// <summary>One rod's meshes: its ground shadow, its violet edge, and its dark and lit faces.</summary>
        private sealed class RodMeshes
        {
            private const int Rows = SixPathsRodsTiming.Rows;
            private readonly SixPathsStrip shadow, edge, dark, lit;
            private readonly Vector2[] left = new Vector2[Rows], right = new Vector2[Rows], spine = new Vector2[Rows],
                edgeLeft = new Vector2[Rows], edgeRight = new Vector2[Rows], shadowLeft = new Vector2[Rows], shadowRight = new Vector2[Rows];

            public RodMeshes(int index)
            {
                string name = "Six Paths rods rod " + index;
                shadow = new SixPathsStrip(name + " shadow", Rows);
                edge = new SixPathsStrip(name + " edge", Rows);
                dark = new SixPathsStrip(name + " dark", Rows);
                lit = new SixPathsStrip(name + " lit", Rows);
            }

            public void Draw(int index, Vector3 middle, Vector2 toward, float seconds, Vector2 sun, float strength)
            {
                RodPose rod = SixPathsRodsTiming.Rod(toward, index, seconds);
                float shadows = AltitudeLayer.Shadows.AltitudeFor(), overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
                Vector3 root = middle + new Vector3(rod.root.x, 0f, rod.root.y);
                const float width = SixPathsRodsTiming.Width;

                float holed = SixPathsRodsTiming.Holed(rod, seconds);
                if (holed > 0f)
                {
                    DrawMesh(MeshPool.plane10, root.WithY(shadows + 0.003f), width * 2.6f, width * 1.4f, Fade(Dirt, 0.55f * holed), soft);
                    DrawMesh(disc, root.WithY(shadows + 0.005f), width * 0.75f, width * 0.4f, Fade(Hole, holed), solid);
                }
                // Dirt thrown as the rod breaks the surface: one puff and three chunks.
                if (rod.age >= 0f && rod.age < SixPathsRodsTiming.DirtSeconds)
                {
                    float u = rod.age / SixPathsRodsTiming.DirtSeconds;
                    DrawMesh(MeshPool.plane10, (root + new Vector3(0f, 0f, u * 0.25f)).WithY(shadows + 0.02f),
                        0.35f + u * 0.6f, 0.25f + u * 0.4f, Fade(Dust, Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * 0.35f), soft);
                    for (int c = 0; c < SixPathsRodsTiming.Chunks; c++)
                    {
                        ImpactParticle chunk = SixPathsRodsTiming.Chunk(index, c, rod.age);
                        DrawMesh(disc, SixPathsHeight.Above(root + new Vector3(chunk.x, 0f, chunk.z), chunk.height).WithY(overhead + 0.02f),
                            chunk.size, 0.04f, Fade(Dirt, chunk.alpha), solid);
                    }
                }
                if (rod.height <= 0.001f) return;

                for (int j = 0; j < Rows; j++)
                {
                    RodRow row = SixPathsRodsTiming.Row(rod, j);
                    left[j] = row.at - rod.across * row.half;
                    right[j] = row.at + rod.across * row.half;
                    spine[j] = row.at - rod.across * (row.half * 0.15f);
                    edgeLeft[j] = row.at - rod.across * (row.half + 0.022f);
                    edgeRight[j] = row.at + rod.across * (row.half + 0.022f);
                    Vector2 cast = row.ground + sun * row.height;
                    shadowLeft[j] = cast - new Vector2(row.half, 0f);
                    shadowRight[j] = cast + new Vector2(row.half, 0f);
                }
                shadow.Between(shadowLeft, shadowRight);
                edge.Between(edgeLeft, edgeRight);
                dark.Between(left, spine);
                lit.Between(spine, right);

                // Rods further north go under nearer ones.
                float layer = overhead + 0.06f - rod.root.y * 0.006f;
                DrawMesh(shadow.mesh, middle.WithY(shadows + 0.001f), 1f, 1f, Fade(Body, strength * 0.7f * SixPathsRodsTiming.Closing(seconds)), solid);
                DrawMesh(edge.mesh, middle.WithY(layer), 1f, 1f, Fade(Rim, 0.85f), solid);
                DrawMesh(dark.mesh, middle.WithY(layer + 0.0001f), 1f, 1f, Body, solid);
                DrawMesh(lit.mesh, middle.WithY(layer + 0.0002f), 1f, 1f, Face, solid);
                RodRow top = SixPathsRodsTiming.Row(rod, Rows - 1);
                DrawMesh(MeshPool.plane10, (middle + new Vector3(top.at.x, 0f, top.at.y)).WithY(layer + 0.0003f), 0.3f, 0.3f,
                    Fade(Pale, SixPathsRodsTiming.TipGlow(rod)), softGlow);
            }
        }
    }
}
