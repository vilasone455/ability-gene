using System;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A projected hemisphere: its highlights rise north of its elliptical ground contact.
    /// Projection is drawn at stable map altitudes to avoid clipping a tall 3D sphere into
    /// RimWorld's close camera. Independent ribbons and debris supply the motion/depth cues.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ShinraVfxGraphics
    {
        private static Material shell, dust, glow, ribbon, shadow;
        private static Material[] rocks;
        private static Mesh arc;
        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        private static readonly Particle[] Dust = MakeParticles(76, 1701);
        private static readonly Particle[] Debris = MakeParticles(30, 1702);

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
            shadow = MaterialPool.MatFrom("RimArt/Shinra/Shadow", ShaderDatabase.Transparent);
            rocks = new Material[3];
            for (int i = 0; i < rocks.Length; i++)
                rocks[i] = MaterialPool.MatFrom("RimArt/Shinra/Rock" + i, ShaderDatabase.Transparent);
            arc = MakeArc();
        }

        public static void Draw(Vector3 centre, float time, Map map)
        {
            EnsureMaterials();
            float radius = ShinraVfxTiming.ShellRadius(time);
            float alpha = ShinraVfxTiming.ShellAlpha(time);
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();

            DrawCharge(centre, time, altitude);
            DrawDust(centre, time, radius, altitude, map);
            DrawDebris(centre, time, altitude, map, true);

            if (alpha > 0f)
            {
                // Texture coordinates span [-1.15, 1.15] in both projected axes.
                Plane(shell, At(centre, altitude + 0.020f), radius * 2.3f, radius * 2.3f,
                    0f, new Color(1f, 1f, 1f, alpha));

                for (int i = 0; i < 4; i++)
                {
                    float height = 0.20f + i * 0.18f + 0.065f * Mathf.Sin(time * 2.8f + i);
                    float width = Mathf.Sqrt(1f - height * height) * radius;
                    Vector3 position = At(centre, altitude + 0.025f + i * 0.001f);
                    position.z += height * ShinraVfxTiming.HeightLift * radius;
                    // The ribbon moves up the shell; rotating its ellipse would tilt the dome.
                    DrawMesh(arc, ribbon, position, width, width * ShinraVfxTiming.GroundDepth,
                        0f, new Color(0.94f, 0.97f, 1f, alpha * (0.38f - i * 0.045f)));
                }
            }

            DrawDebris(centre, time, altitude, map, false);
        }

        private static void DrawCharge(Vector3 centre, float time, float altitude)
        {
            float charge = ShinraVfxTiming.Progress(time, 0f, ShinraVfxTiming.ChargeEnd);
            float flash = 1f - ShinraVfxTiming.Progress(time, ShinraVfxTiming.ChargeEnd, 0.64f);
            float opacity = ShinraVfxTiming.Smooth(charge) * flash;
            if (opacity <= 0f) return;
            float size = Mathf.Lerp(2.8f, 0.65f, charge);
            Plane(glow, At(centre, altitude + 0.01f), size, size, 0f,
                new Color(1f, 0.98f, 0.94f, opacity * 0.8f));
            DrawMesh(arc, ribbon, At(centre, altitude + 0.012f), size * 0.6f,
                size * 0.6f * ShinraVfxTiming.GroundDepth, 0f, new Color(1f, 1f, 1f, opacity * 0.6f));
        }

        private static Vector3 GroundOffset(float angle, float distance) =>
            new Vector3(Mathf.Cos(angle) * distance, 0f,
                Mathf.Sin(angle) * distance * ShinraVfxTiming.GroundDepth);

        private static void DrawDust(Vector3 centre, float time, float radius, float altitude, Map map)
        {
            for (int i = 0; i < Dust.Length; i++)
            {
                Particle p = Dust[i];
                float age = time - p.delay;
                float alpha = ShinraVfxTiming.DustAlpha(time, p.delay);
                if (alpha <= 0f) continue;
                float trail = ShinraVfxTiming.Progress(age, 0.7f, ShinraVfxTiming.Duration);
                float distance = radius * (0.94f + p.spread * 0.12f) + trail * (0.6f + p.spread);
                Vector3 position = centre + GroundOffset(p.angle, distance);
                if (!Visible(position, map)) continue;
                position.z += trail * (0.2f + p.lift * 0.65f);
                bool front = Mathf.Sin(p.angle) < 0f;
                position.y = altitude + (front ? 0.04f : 0f);
                float size = (0.55f + p.size * 0.85f + trail * 1.25f) *
                    Mathf.Lerp(0.3f, 1f, ShinraVfxTiming.Expansion(age));
                Plane(dust, position, size, size * 0.85f, p.spin + trail * 24f,
                    new Color(0.76f, 0.70f, 0.59f, alpha * (0.38f + p.size * 0.2f)));
            }
        }

        private static void DrawDebris(Vector3 centre, float time, float altitude, Map map, bool behind)
        {
            for (int i = 0; i < Debris.Length; i++)
            {
                Particle p = Debris[i];
                if ((Mathf.Sin(p.angle) > 0f) != behind) continue;
                float t = ShinraVfxTiming.DebrisProgress(time, p.delay);
                if (t <= 0f || t >= 1f) continue;
                float alpha = ShinraVfxTiming.Smooth(t / 0.08f) *
                    (1f - ShinraVfxTiming.Smooth((t - 0.78f) / 0.22f));
                float distance = ShinraVfxTiming.Radius * (0.22f + p.spread * 0.65f) *
                    (0.25f + 0.85f * (1f - (1f - t) * (1f - t)));
                Vector3 ground = centre + GroundOffset(p.angle, distance);
                if (!Visible(ground, map)) continue;
                float height = Mathf.Sin(t * Mathf.PI) * (0.7f + p.lift * 1.8f);
                float size = 0.13f + p.size * p.size * 0.44f;
                Plane(shadow, At(ground, AltitudeLayer.Shadows.AltitudeFor()),
                    size * (1.2f + height * 0.45f), size * 0.6f, 0f,
                    new Color(1f, 1f, 1f, alpha * 0.42f / (1f + height * 0.3f)));

                Vector3 airborne = At(ground, altitude + (behind ? 0.01f : 0.05f));
                airborne.z += height * ShinraVfxTiming.HeightLift;
                Plane(rocks[i % rocks.Length], airborne, size, size,
                    p.spin + time * (p.spread - 0.5f) * 150f, new Color(1f, 1f, 1f, alpha));
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

        private static Mesh MakeArc()
        {
            const int segments = 96;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                // A broken sweep across the near surface, rather than a cage of full rings.
                float angle = Mathf.Lerp(0.78f * Mathf.PI, 2.10f * Mathf.PI, t);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * 0.986f;
                vertices[i * 2 + 1] = direction * 1.014f;
                uv[i * 2] = new Vector2(t, 0f);
                uv[i * 2 + 1] = new Vector2(t, 1f);
                if (i == segments) continue;
                int v = i * 2, j = i * 6;
                triangles[j] = v; triangles[j + 1] = v + 2; triangles[j + 2] = v + 1;
                triangles[j + 3] = v + 1; triangles[j + 4] = v + 2; triangles[j + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Shinra pressure sweep", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
