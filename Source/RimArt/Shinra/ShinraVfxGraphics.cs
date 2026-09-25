using System;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A projected hemisphere: its highlights rise north of its elliptical ground contact.
    /// Projection is drawn at stable map altitudes to avoid clipping a tall 3D sphere into
    /// RimWorld's close camera. Independent ribbons and ground dust supply the depth cues.
    /// Sprite sizes and travel distances are fractions of ShinraVfxTiming.Radius, so changing
    /// that radius scales the whole effect instead of shrinking only the shell.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ShinraVfxGraphics
    {
        private static Material shell, dust, glow, ribbon, distortion;
        private static Mesh arc, ring;
        private static bool distortionResolved;
        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        private static readonly MaterialPropertyBlock WarpProperties = new MaterialPropertyBlock();
        // About 1.5 sprites per cell of circumference at the dust's furthest reach, so a smaller
        // wave gets a shorter ring rather than the same sprite count packed tighter around it.
        private static readonly Particle[] Dust = MakeParticles(
            (int)Math.Round(1.51 * 2.0 * Math.PI * ShinraVfxTiming.Radius * ShinraVfxTiming.RingSpan), 1701);

        private struct Particle
        {
            public float angle, spread, size, delay, spin, lift;
        }

        private static Particle[] MakeParticles(int count, int seed)
        {
            // Private RNG: previewing an effect must not change the game's random sequence.
            var random = new System.Random(seed);
            var particles = new Particle[count];
            for (int i = 0; i < count; i++)
                particles[i] = new Particle
                {
                    angle = (i + (float)random.NextDouble() * 0.8f) * Mathf.PI * 2f / count,
                    spread = (float)random.NextDouble(),
                    size = (float)random.NextDouble(),
                    delay = (float)random.NextDouble() * 0.23f,
                    spin = (float)random.NextDouble() * 360f,
                    lift = (float)random.NextDouble()
                };
            return particles;
        }

        private static void EnsureMaterials()
        {
            if (shell != null) return;
            shell = MaterialPool.MatFrom("RimArt/Shinra/Shell", ShaderDatabase.Transparent);
            dust = MaterialPool.MatFrom("RimArt/Shinra/Dust", ShaderDatabase.Transparent);
            glow = MaterialPool.MatFrom("RimArt/Shinra/Glow", ShaderDatabase.Transparent);
            ribbon = MaterialPool.MatFrom("RimArt/Shinra/Ribbon", ShaderDatabase.Transparent);
            arc = MakeArc();
            ring = MakeRing();
        }

        /// <summary>
        /// RimWorld's own screen-warp shader, declared by Core rather than a DLC, driven here
        /// with the core distortion and noise maps it expects. Everything about it is optional:
        /// a missing shader or texture disables only the warp and leaves the rest of the wave.
        /// </summary>
        private static Material Distortion()
        {
            if (distortionResolved) return distortion;
            distortionResolved = true;
            try
            {
                Shader shader = DefDatabase<ShaderTypeDef>.GetNamedSilentFail("MoteLargeDistortionWave")?.Shader;
                Texture2D currents = ContentFinder<Texture2D>.Get("Things/Mote/PsychicDistortionCurrents", false);
                Texture2D noise = ContentFinder<Texture2D>.Get("Things/Mote/PsycastNoise", false);
                Texture2D mask = ContentFinder<Texture2D>.Get("RimArt/Shinra/Distort", false);
                if (shader == null || currents == null || noise == null || mask == null)
                {
                    Log.Warning("[RimArt] Shinra Tensei found no distortion shader or maps; drawing without the warp.");
                    return null;
                }
                distortion = new Material(shader) { mainTexture = mask };
                distortion.SetTexture("_DistortionTex", currents);
                distortion.SetTexture("_NoiseTex", noise);
                distortion.SetFloat("_distortionIntensity", 0.12f);
                return distortion;
            }
            catch (Exception e)
            {
                Log.Warning("[RimArt] Shinra Tensei could not build its distortion material: " + e);
                return null;
            }
        }

        /// <summary>The wave is centred on the caster and radial in every direction, so nothing
        /// here reads the caster's facing.</summary>
        public static void Draw(Vector3 centre, float time, Map map)
        {
            EnsureMaterials();
            float radius = ShinraVfxTiming.ShellRadius(time);
            float alpha = ShinraVfxTiming.ShellAlpha(time);
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();

            DrawCharge(centre, time, altitude);
            DrawGroundRing(centre, time, ShinraVfxTiming.RingRadius(time));
            DrawDust(centre, time, altitude, map);
            DrawDistortion(centre, time, radius, altitude);

            if (alpha > 0f)
            {
                // Texture coordinates span [-1.15, 1.15] in both projected axes.
                Plane(shell, At(centre, altitude + 0.020f), radius * 2.3f, radius * 2.3f,
                    0f, new Color(1f, 1f, 1f, alpha));

                // One sweep up the shell as it expands. A continuous oscillation here reads as
                // ribbons floating in place rather than a surface being driven outward.
                float sweep = VfxMath.Smooth(ShinraVfxTiming.Progress(time,
                    ShinraVfxTiming.ChargeEnd, ShinraVfxTiming.ExpansionEnd + 0.30f));
                for (int i = 0; i < 4; i++)
                {
                    float height = Mathf.Lerp(0.06f + i * 0.05f, 0.20f + i * 0.18f, sweep);
                    float width = Mathf.Sqrt(1f - height * height) * radius;
                    Vector3 position = At(centre, altitude + 0.025f + i * 0.001f);
                    position.z += height * ShinraVfxTiming.HeightLift * radius;
                    // The ribbon moves up the shell; rotating its ellipse would tilt the dome.
                    DrawMesh(arc, ribbon, position, width, width * ShinraVfxTiming.GroundDepth,
                        0f, new Color(0.94f, 0.97f, 1f, alpha * (0.38f - i * 0.045f)));
                }
            }

            DrawImpacts(centre, time, altitude);

            float flash = ShinraVfxTiming.ReleaseFlash(time);
            if (flash > 0f)
            {
                float size = radius * 2f;
                Vector3 position = At(centre, altitude + 0.030f);
                position.z += 0.25f * ShinraVfxTiming.HeightLift * radius;
                Plane(glow, position, size, size * ShinraVfxTiming.GroundDepth, 0f,
                    new Color(1f, 0.99f, 0.95f, flash * 0.5f));
            }
        }

        private static void DrawCharge(Vector3 centre, float time, float altitude)
        {
            float charge = ShinraVfxTiming.Progress(time, 0f, ShinraVfxTiming.ChargeEnd);
            float flash = 1f - ShinraVfxTiming.Progress(time, ShinraVfxTiming.ChargeEnd, 0.64f);
            float opacity = VfxMath.Smooth(charge) * flash;
            if (opacity <= 0f) return;
            float size = ShinraVfxTiming.Radius * Mathf.Lerp(0.35f, 0.081f, charge);
            Plane(glow, At(centre, altitude + 0.01f), size, size, 0f,
                new Color(1f, 0.98f, 0.94f, opacity * 0.8f));
            DrawMesh(arc, ribbon, At(centre, altitude + 0.012f), size * 0.6f,
                size * 0.6f * ShinraVfxTiming.GroundDepth, 0f, new Color(1f, 1f, 1f, opacity * 0.6f));
        }

        /// <summary>The warp sits under the dome art, so the shell's own highlights stay crisp
        /// on top of a background that is being pulled around.</summary>
        private static void DrawDistortion(Vector3 centre, float time, float radius, float altitude)
        {
            float intensity = ShinraVfxTiming.DistortionIntensity(time);
            if (intensity <= 0f) return;
            Material material = Distortion();
            if (material == null) return;
            WarpProperties.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, intensity));
            WarpProperties.SetFloat(ShaderPropertyIDs.AgeSecs, time);
            float size = radius * 2.3f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(At(centre, altitude + 0.018f),
                Quaternion.identity, new Vector3(size, 1f, size)), material, 0, null, 0, WarpProperties);
        }

        /// <summary>Waves born at the core and dying against the shell. A dome that only sits
        /// there reads as a bubble; the impacts are what make it read as a blast being held.</summary>
        private static void DrawImpacts(Vector3 centre, float time, float altitude)
        {
            float core = 0f;
            for (int i = 0; i < ShinraVfxTiming.ImpactPulses; i++)
            {
                float alpha = ShinraVfxTiming.ImpactAlpha(i, time);
                if (alpha <= 0f) continue;
                if (alpha > core) core = alpha;
                float radius = ShinraVfxTiming.ImpactRadius(i, time);

                DrawMesh(ring, ribbon, At(centre, altitude + 0.026f + i * 0.001f),
                    radius, radius * ShinraVfxTiming.GroundDepth, 0f,
                    new Color(0.90f, 0.96f, 1f, alpha * 0.75f));

                // A second ring halfway up the same wave, so it reads as a sphere leaving the
                // core rather than a flat ripple on the floor.
                const float height = 0.5f;
                float width = Mathf.Sqrt(1f - height * height) * radius;
                Vector3 lifted = At(centre, altitude + 0.027f + i * 0.001f);
                lifted.z += height * ShinraVfxTiming.HeightLift * radius;
                DrawMesh(ring, ribbon, lifted, width, width * ShinraVfxTiming.GroundDepth, 0f,
                    new Color(0.90f, 0.96f, 1f, alpha * 0.55f));
            }
            if (core <= 0f) return;
            float glowSize = ShinraVfxTiming.Radius * 0.5f;
            Plane(glow, At(centre, altitude + 0.029f), glowSize, glowSize * ShinraVfxTiming.GroundDepth,
                0f, new Color(1f, 0.99f, 0.96f, core * 0.35f));
        }

        /// <summary>A flat ring on the terrain under pawns, travelling past the dome. The dome
        /// alone leaves the ground passive, which reads as a bubble appearing rather than
        /// pressure leaving the caster.</summary>
        private static void DrawGroundRing(Vector3 centre, float time, float radius)
        {
            float alpha = ShinraVfxTiming.GroundRingAlpha(time);
            if (alpha <= 0f) return;
            DrawMesh(ring, ribbon, At(centre, AltitudeLayer.MoteLow.AltitudeFor()),
                radius, radius * ShinraVfxTiming.GroundDepth, 0f,
                new Color(1f, 0.97f, 0.90f, alpha * 0.55f));
        }

        private static Vector3 GroundOffset(float angle, float distance) =>
            new Vector3(Mathf.Cos(angle) * distance, 0f,
                Mathf.Sin(angle) * distance * ShinraVfxTiming.GroundDepth);

        private static void DrawDust(Vector3 centre, float time, float altitude, Map map)
        {
            // Dust rides the outward ring. The dome holds one size, so it can no longer carry
            // the sense of anything travelling away from the caster.
            float radius = ShinraVfxTiming.RingRadius(time);
            float scale = ShinraVfxTiming.Radius * ShinraVfxTiming.RingSpan;
            for (int i = 0; i < Dust.Length; i++)
            {
                Particle p = Dust[i];
                float age = time - p.delay;
                float alpha = ShinraVfxTiming.DustAlpha(time, p.delay);
                if (alpha <= 0f) continue;
                float trail = ShinraVfxTiming.Progress(age, 0.7f, ShinraVfxTiming.Duration);
                float distance = radius * (0.94f + p.spread * 0.12f)
                    + scale * trail * (0.075f + 0.125f * p.spread);
                Vector3 position = centre + GroundOffset(p.angle, distance);
                if (!Visible(position, map)) continue;
                position.z += trail * (0.2f + p.lift * 0.65f);
                bool front = Mathf.Sin(p.angle) < 0f;
                position.y = altitude + (front ? 0.04f : 0f);
                float size = scale * (0.069f + 0.106f * p.size + 0.156f * trail) *
                    Mathf.Lerp(0.3f, 1f, ShinraVfxTiming.Expansion(age));
                Plane(dust, position, size, size * 0.85f, p.spin + trail * 24f,
                    new Color(0.76f, 0.70f, 0.59f, alpha * (0.38f + p.size * 0.2f)));
            }
        }

        private static bool Visible(Vector3 position, Map map)
        {
            IntVec3 cell = position.ToIntVec3();
            return cell.InBounds(map) && !cell.Fogged(map);
        }

        private static Vector3 At(Vector3 position, float altitude)
        {
            position.y = altitude;
            return position;
        }

        private static void Plane(Material material, Vector3 position, float width, float depth,
            float angle, Color colour) => DrawMesh(MeshPool.plane10, material, position, width, depth, angle, colour);

        private static void DrawMesh(Mesh mesh, Material material, Vector3 position, float width,
            float depth, float angle, Color colour)
        {
            Properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.Euler(0f, angle, 0f),
                new Vector3(width, 1f, depth)), material, 0, null, 0, Properties);
        }

        private static Mesh Band(string name, float from, float to, float inner, float outer, bool taper)
        {
            const int segments = 96;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(from, to, t);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;
                // The ribbon texture tapers to nothing along its length. A closed ring samples
                // one fixed column at full strength instead, or the seam would show as a gap.
                float along = taper ? t : 0.5f;
                uv[i * 2] = new Vector2(along, 0f);
                uv[i * 2 + 1] = new Vector2(along, 1f);
                if (i == segments) continue;
                int v = i * 2, j = i * 6;
                triangles[j] = v; triangles[j + 1] = v + 2; triangles[j + 2] = v + 1;
                triangles[j + 3] = v + 1; triangles[j + 4] = v + 2; triangles[j + 5] = v + 3;
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A broken sweep across the near surface, rather than a cage of full rings.
        private static Mesh MakeArc() =>
            Band("Shinra pressure sweep", 0.78f * Mathf.PI, 2.10f * Mathf.PI, 0.986f, 1.014f, true);

        private static Mesh MakeRing() =>
            Band("Shinra ground ring", 0f, 2f * Mathf.PI, 0.955f, 1.045f, false);
    }
}
