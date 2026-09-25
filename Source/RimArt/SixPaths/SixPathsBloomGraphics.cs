using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// Draws Obsidian Bloom: the sage's ring with one slot empty, that orb's flight out and back,
    /// the six petals with their shadows, and the heal pulses. SixPathsBloomTiming says where
    /// everything is; this only turns it into meshes.
    ///
    /// A petal is four strips and a line, rebuilt every frame it moves. Its height is drawn north
    /// by SixPathsHeight.Lift, and its shadow stays at its root.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsBloomGraphics
    {
        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh ring = VfxDraw.Ring(0.965f, "Six Paths bloom ring");

        // One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh
        // when the frame renders, not when it is called.
        private static readonly Petal[] petals = NewPetals();
        private static readonly SixPathsStrip trail = new SixPathsStrip("Six Paths bloom orb trail", SixPathsBloomTiming.TrailPoints);
        private static readonly Vector2[] trailPoints = new Vector2[SixPathsBloomTiming.TrailPoints];

        // The petals' near-black is a shade up from the orb's, so the dark face and the lit face
        // of a petal are both still darker than the rim.
        private static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        private static readonly Color Lit = new Color(0.075f, 0.055f, 0.105f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Pale = new Color(0.88f, 0.79f, 1f);

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone. <paramref name="patient"/>
        /// and <paramref name="sage"/> are where the two stand.
        /// </summary>
        public static void Draw(Vector3 patient, Vector3 sage, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= SixPathsBloomTiming.Duration) return;
            if (!Shown(patient, map) || !Shown(sage, map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector
                * SixPathsSlamGraphics.SunScale;

            // Slot 0 is the orb this costs. It is away for the whole of the effect.
            SixPathsGraphics.DrawCarried(sage, seconds, 1, sun, GenCelestial.CurShadowStrength(map));
            DrawFloor(patient, seconds);
            DrawOrb(patient, sage, seconds);
            float pulse = SixPathsBloomTiming.Pulse(seconds);
            for (int i = 0; i < SixPathsBloomTiming.Petals; i++) DrawPetal(petals[i], i, patient, seconds, pulse);
            DrawHeal(patient, seconds, pulse);
        }

        private static void DrawFloor(Vector3 patient, float seconds)
        {
            float floor = AltitudeLayer.Filth.AltitudeFor(), sunk = SixPathsBloomTiming.Sunk(seconds);
            float reach = SixPathsBloomTiming.Radius * 2f * sunk;
            DrawMesh(MeshPool.plane10, patient.WithY(floor), reach, reach, 0f,
                Fade(Body, 0.3f * (1f - SixPathsBloomTiming.Reformed(seconds))), soft);
            float open = 1f - VfxMath.Smooth((seconds - SixPathsBloomTiming.FoldAt) / SixPathsBloomTiming.Fold);
            DrawRing(patient.WithY(floor + 0.001f), SixPathsBloomTiming.Radius * 0.95f, Fade(Rim, 0.2f * sunk * open));
        }

        /// <summary>The orb between its ring slot and the patient, outbound at the start and homebound at the end.</summary>
        private static void DrawOrb(Vector3 patient, Vector3 sage, float seconds)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
            CarriedSlot slot = SixPathsCarried.Slot(sage, 0, seconds);
            Vector3 home = SixPathsHeight.Above(slot.ground, slot.height) - patient;
            var from = new Vector2(home.x, home.z);

            if (seconds < SixPathsBloomTiming.Sink)
            {
                float flown = SixPathsBloomTiming.FlownOut(seconds), drop = SixPathsBloomTiming.Drop(seconds);
                DrawBall(patient, SixPathsBloomTiming.Path(from, flown),
                    flown > 0f ? overhead : SixPathsGraphics.CarriedAltitude(slot),
                    SixPathsCarried.DeployRadius(flown, SixPathsCarried.Field) * (1f - drop * 0.9f), 1f - drop * 0.5f);
                for (int j = 0; j < trailPoints.Length; j++)
                    trailPoints[j] = SixPathsBloomTiming.Path(from, SixPathsBloomTiming.FlownOut(seconds - Lag(j)));
                DrawTrail(patient.WithY(overhead), Fade(Rim, 0.5f * (1f - drop)));
            }

            float reformed = SixPathsBloomTiming.Reformed(seconds);
            if (reformed <= 0f) return;
            // It comes up out of the floor at cast size, then shrinks into its slot on the way.
            DrawBall(patient, SixPathsBloomTiming.Path(from, 1f - reformed),
                reformed < 1f ? overhead : SixPathsGraphics.CarriedAltitude(slot),
                SixPathsCarried.DeployRadius(1f - reformed, SixPathsCarried.Field) * Mathf.Min(1f, reformed * 4f), 1f);
            for (int j = 0; j < trailPoints.Length; j++)
                trailPoints[j] = SixPathsBloomTiming.Path(from,
                    1f - SixPathsBloomTiming.Reformed(Mathf.Max(SixPathsBloomTiming.ReturnAt, seconds - Lag(j))));
            float fade = 1f - VfxMath.Smooth((seconds - SixPathsBloomTiming.Duration + 0.15f) / 0.15f);
            DrawTrail(patient.WithY(overhead), Fade(Rim, 0.5f * fade));
        }

        /// <summary>Seconds behind the orb that trail point <paramref name="j"/> sits; the last point is the orb.</summary>
        private static float Lag(int j) =>
            (1f - j / (float)(SixPathsBloomTiming.TrailPoints - 1)) * SixPathsBloomTiming.TrailSeconds;

        private static void DrawBall(Vector3 patient, Vector2 at, float altitude, float radius, float stretch) =>
            SixPathsGraphics.DrawBall((patient + new Vector3(at.x, 0f, at.y)).WithY(altitude), radius, stretch, 1f);

        private static void DrawTrail(Vector3 at, Color colour)
        {
            trail.Line(trailPoints, 0.08f);
            DrawMesh(trail.mesh, at, 1f, 1f, 0f, colour, solid);
        }

        private static void DrawPetal(Petal petal, int index, Vector3 patient, float seconds, float pulse)
        {
            PetalPose pose = SixPathsBloomTiming.Petal(index, seconds);
            if (pose.grow <= 0f || pose.dissolve <= 0f) return;
            petal.Rebuild(pose, 0.025f + pulse * 0.035f);

            // Petals on the north side go under the ones on the south side.
            float layer = AltitudeLayer.MoteOverhead.AltitudeFor() + (1f - Mathf.Sin(pose.angle)) * 0.035f;
            DrawMesh(petal.shadow.mesh, patient.WithY(AltitudeLayer.Filth.AltitudeFor() + 0.002f), 1f, 1f, 0f,
                Fade(Body, 0.28f * pose.dissolve), solid);
            DrawMesh(petal.dark.mesh, patient.WithY(layer), 1f, 1f, 0f, Fade(Body, pose.dissolve), solid);
            DrawMesh(petal.lit.mesh, patient.WithY(layer + 0.001f), 1f, 1f, 0f, Fade(Lit, pose.dissolve), solid);
            DrawMesh(petal.seam.mesh, patient.WithY(layer + 0.003f), 1f, 1f, 0f,
                Fade(Rim, (0.65f + pulse * 0.35f) * pose.dissolve), solid);
            DrawMesh(petal.spine.mesh, patient.WithY(layer + 0.004f), 1f, 1f, 0f,
                Fade(Rim, (0.25f + pulse * 0.65f) * pose.dissolve), solid);
        }

        /// <summary>Heal ticks: a glow at the bud's tip, a soft ring on the floor, and motes that rise off the seams.</summary>
        private static void DrawHeal(Vector3 patient, float seconds, float pulse)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), shut = SixPathsBloomTiming.Shut(seconds);
            float size = 0.9f + pulse * 0.4f;
            DrawMesh(MeshPool.plane10, SixPathsHeight.Above(patient, SixPathsBloomTiming.Height).WithY(overhead + 0.12f),
                size, size, 0f, Fade(Pale, (0.15f + pulse * 0.55f) * shut), softGlow);
            if (pulse > 0f)
                DrawRing(patient.WithY(AltitudeLayer.Filth.AltitudeFor() + 0.003f),
                    SixPathsBloomTiming.Radius * (0.55f + (1f - pulse) * 0.5f), Fade(Pale, pulse * 0.45f));
            if (shut <= 0.5f) return;
            for (int i = 0; i < SixPathsBloomTiming.Motes; i++)
            {
                ImpactParticle mote = SixPathsBloomTiming.Mote(i, seconds);
                if (mote.alpha <= 0.001f) continue;
                Vector3 at = SixPathsHeight.Above(patient + new Vector3(mote.x, 0f, mote.z), mote.height);
                DrawMesh(MeshPool.plane10, at.WithY(overhead + 0.13f), mote.size, mote.size, 0f,
                    Fade(Pale, mote.alpha), softGlow);
            }
        }

        private static void DrawRing(Vector3 at, float radius, Color colour)
        {
            if (radius > 0f) DrawMesh(ring, at, radius, radius, 0f, colour, solid);
        }

        private static bool Shown(Vector3 at, Map map) => at.ToIntVec3().InBounds(map) && !at.ToIntVec3().Fogged(map);

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth,
            float rotation, Color colour, Material material)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.Euler(0f, rotation, 0f),
                new Vector3(width, 1f, depth)), material, 0, null, 0, properties);
        }

        private static Petal[] NewPetals()
        {
            var made = new Petal[SixPathsBloomTiming.Petals];
            for (int i = 0; i < made.Length; i++) made[i] = new Petal(i);
            return made;
        }

        /// <summary>A thin unit-radius band, for the rings on the floor.</summary>

        /// <summary>One petal's meshes: its ground shadow, its dark and lit halves, the seam on its edge and the line down its spine.</summary>
        private sealed class Petal
        {
            public readonly SixPathsStrip shadow, dark, lit, seam, spine;
            private readonly Vector2[] left = new Vector2[SixPathsBloomTiming.Rows], right = new Vector2[SixPathsBloomTiming.Rows],
                middle = new Vector2[SixPathsBloomTiming.Rows], edge = new Vector2[SixPathsBloomTiming.Rows],
                shadowLeft = new Vector2[SixPathsBloomTiming.Rows], shadowRight = new Vector2[SixPathsBloomTiming.Rows];

            public Petal(int index)
            {
                string name = "Six Paths bloom petal " + index;
                shadow = new SixPathsStrip(name + " shadow", SixPathsBloomTiming.Rows);
                dark = new SixPathsStrip(name + " dark", SixPathsBloomTiming.Rows);
                lit = new SixPathsStrip(name + " lit", SixPathsBloomTiming.Rows);
                seam = new SixPathsStrip(name + " seam", SixPathsBloomTiming.Rows);
                spine = new SixPathsStrip(name + " spine", SixPathsBloomTiming.Rows);
            }

            public void Rebuild(in PetalPose pose, float spineWidth)
            {
                for (int j = 0; j < SixPathsBloomTiming.Rows; j++)
                {
                    PetalRow row = SixPathsBloomTiming.Row(pose, j);
                    left[j] = row.left; right[j] = row.right; middle[j] = row.spine; edge[j] = row.seam;
                    shadowLeft[j] = row.shadowLeft; shadowRight[j] = row.shadowRight;
                }
                shadow.Between(shadowLeft, shadowRight);
                dark.Between(left, middle);
                lit.Between(middle, right);
                seam.Between(left, edge);
                spine.Line(middle, spineWidth);
            }
        }
    }
}
