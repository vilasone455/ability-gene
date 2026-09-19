using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws Devouring Current: the sage's ring with one slot empty, that orb's flight to its hover
    /// spot and back, the orb with its shadow, highlight and inlet crescent, the funnel of wind and
    /// dragged dust while it pulls, the compression lines at its inlet, and the flash, gusts and
    /// dust of the release. SixPathsCurrentTiming says where everything is; this only turns it
    /// into meshes. What the current pulls in and fires out is not drawn here.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsCurrentGraphics
    {
        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh disc = SixPathsBurstGraphics.Band(0f, "Six Paths current disc");

        // One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh
        // when the frame renders, not when it is called.
        private static readonly SixPathsStrip[] parcels = Strips("wind", SixPathsCurrentTiming.Parcels, SixPathsCurrentTiming.ParcelPoints),
            compressions = Strips("compression", SixPathsCurrentTiming.Compressions, SixPathsCurrentTiming.CompressionPoints),
            gusts = Strips("gust", SixPathsCurrentTiming.Gusts, SixPathsCurrentTiming.GustPoints);
        private static readonly SixPathsStrip crescent = new SixPathsStrip("Six Paths current inlet", SixPathsCurrentTiming.CrescentPoints),
            flight = new SixPathsStrip("Six Paths current orb flight", 3);
        private static readonly Vector2[] parcelPoints = new Vector2[SixPathsCurrentTiming.ParcelPoints],
            compressionPoints = new Vector2[SixPathsCurrentTiming.CompressionPoints],
            gustPoints = new Vector2[SixPathsCurrentTiming.GustPoints],
            crescentPoints = new Vector2[SixPathsCurrentTiming.CrescentPoints], flightPoints = new Vector2[3];

        private static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Pale = new Color(0.88f, 0.81f, 1f);
        private static readonly Color Highlight = new Color(0.12f, 0.095f, 0.17f);
        private static readonly Color Dust = new Color(0.56f, 0.49f, 0.40f);

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone. <paramref name="sage"/> is
        /// where the caster stands and <paramref name="toward"/> is the unit cast direction; the orb
        /// works from <see cref="SixPathsCurrentTiming.Behind"/> cells in front of the sage.
        /// </summary>
        public static void Draw(Vector3 sage, Vector2 toward, float seconds, Map map)
        {
            if (seconds < 0f || seconds > SixPathsCurrentTiming.Duration) return;
            Vector3 spot = sage + new Vector3(toward.x, 0f, toward.y) * SixPathsCurrentTiming.Behind;
            if (!Shown(spot, map) || !Shown(sage, map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector
                * SixPathsSlamGraphics.SunScale;
            float daylight = GenCelestial.CurShadowStrength(map);

            // Slot 0 is the orb this costs. It is away for the whole of the effect.
            SixPathsGraphics.DrawCarried(sage, seconds, 1, sun, daylight);
            DrawPull(spot, toward, seconds);
            DrawOrb(spot, sage, toward, seconds, sun, 0.32f * daylight);
            if (SixPathsCurrentTiming.Age(seconds) >= 0f) DrawRelease(spot, toward, seconds);
        }

        private static Vector3 Flat(Vector2 at) => new Vector3(at.x, 0f, at.y);

        /// <summary>Wind parcels down the funnel, dust dragged along the ground, and the short lines running into the inlet.</summary>
        private static void DrawPull(Vector3 spot, Vector2 toward, float seconds)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), shadows = AltitudeLayer.Shadows.AltitudeFor();
            for (int i = 0; i < parcels.Length; i++)
            {
                if (!SixPathsCurrentTiming.Parcel(i, seconds, out float age, out float width, out bool pale, out float alpha)) continue;
                for (int j = 0; j < parcelPoints.Length; j++) parcelPoints[j] = SixPathsCurrentTiming.ParcelPoint(toward, i, j, age);
                parcels[i].Line(parcelPoints, width);
                // Pale and violet parcels cross, so each has its own place in the order.
                DrawMesh(parcels[i].mesh, spot.WithY(overhead + 0.018f + i * 0.00002f), 1f, 1f, Fade(pale ? Pale : Rim, alpha), solid);
            }
            for (int i = 0; i < SixPathsCurrentTiming.PullDust; i++)
                DrawPuff(spot, SixPathsCurrentTiming.DraggedDust(toward, i, seconds), shadows + 0.025f + i * 0.00002f);
            if (!SixPathsCurrentTiming.Compressing(seconds)) return;
            float pressure = SixPathsCurrentTiming.Pressure(seconds);
            for (int i = 0; i < compressions.Length; i++)
            {
                for (int j = 0; j < compressionPoints.Length; j++)
                    compressionPoints[j] = SixPathsCurrentTiming.CompressionPoint(toward, i, j, seconds);
                compressions[i].Line(compressionPoints, 0.032f);
                DrawMesh(compressions[i].mesh, spot.WithY(overhead + 0.025f + i * 0.00002f), 1f, 1f,
                    Fade(Rim, pressure * SixPathsCurrentTiming.Wind * 0.55f), solid);
            }
        }

        /// <summary>Slot 0 flies out to the hover spot during the prepare and home during the settle.</summary>
        private static void DrawOrb(Vector3 spot, Vector3 sage, Vector2 toward, float seconds, Vector2 sun, float strength)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), deployed = SixPathsCurrentTiming.Deployed(seconds);
            float radius = SixPathsCarried.DeployRadius(deployed, SixPathsCarried.Field), along = SixPathsCurrentTiming.OrbAlong(seconds);
            CarriedSlot slot = SixPathsCarried.Slot(sage, 0, seconds);
            Vector3 home = SixPathsHeight.Above(slot.ground, slot.height) - spot;
            var from = new Vector2(home.x, home.z);
            Vector2 at = Vector2.LerpUnclamped(from, SixPathsCurrentTiming.At(toward, along, 0f, SixPathsCurrentTiming.Hover), deployed);

            Vector2 cast = SixPathsCurrentTiming.At(toward, along) + sun * SixPathsCurrentTiming.Hover;
            DrawMesh(MeshPool.plane10, (spot + Flat(cast)).WithY(AltitudeLayer.Shadows.AltitudeFor()),
                0.85f * deployed, 0.57f * deployed, Fade(Body, strength * 0.8f * deployed), soft);
            SixPathsGraphics.DrawBall((spot + Flat(at)).WithY(deployed > 0f ? overhead + 0.04f : SixPathsGraphics.CarriedAltitude(slot)),
                radius, 1f, 1f);
            if (deployed > 0f && deployed < 1f)
            {
                flightPoints[0] = from;
                flightPoints[1] = (from + at) / 2f + new Vector2(0f, 0.1f);
                flightPoints[2] = at;
                flight.Line(flightPoints, 0.07f);
                DrawMesh(flight.mesh, spot.WithY(overhead), 1f, 1f, Fade(Rim, 0.45f), solid);
            }
            if (deployed < 1f) return;
            // A single dark, shaded sphere and a narrow forward crescent stay readable in the wind.
            DrawMesh(disc, (spot + Flat(at) + new Vector3(-radius * 0.22f, 0f, radius * 0.19f)).WithY(overhead + 0.045f),
                radius * 0.59f, radius * 0.65f, Highlight, solid);
            for (int j = 0; j < crescentPoints.Length; j++) crescentPoints[j] = SixPathsCurrentTiming.Crescent(toward, j, radius, seconds);
            crescent.Line(crescentPoints, 0.045f);
            DrawMesh(crescent.mesh, spot.WithY(overhead + 0.05f), 1f, 1f,
                Fade(Pale, 0.25f + SixPathsCurrentTiming.Pressure(seconds) * 0.55f), solid);
        }

        /// <summary>The flash at the orb, nine gusts running out down the line, and the dust they leave.</summary>
        private static void DrawRelease(Vector3 spot, Vector2 toward, float seconds)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), age = SixPathsCurrentTiming.Age(seconds);
            DrawMesh(MeshPool.plane10, (spot + Flat(SixPathsCurrentTiming.At(toward, 0.4f, 0f, SixPathsCurrentTiming.Hover))).WithY(overhead + 0.08f),
                0.65f, 0.9f, Fade(Pale, (1f - Mathf.Clamp01(age / 0.12f)) * 0.8f), softGlow);
            float alpha = SixPathsCurrentTiming.Wind * SixPathsCurrentTiming.TailFade(seconds) * 0.55f;
            for (int i = 0; i < gusts.Length; i++)
            {
                for (int j = 0; j < gustPoints.Length; j++) gustPoints[j] = SixPathsCurrentTiming.GustPoint(toward, i, j, seconds);
                gusts[i].Line(gustPoints, 0.055f);
                DrawMesh(gusts[i].mesh, spot.WithY(overhead + 0.026f + i * 0.00002f), 1f, 1f, Fade(Pale, alpha), solid);
            }
            for (int i = 0; i < SixPathsCurrentTiming.ReleaseDust; i++)
                DrawPuff(spot, SixPathsCurrentTiming.ReleasedDust(toward, i, seconds),
                    AltitudeLayer.Shadows.AltitudeFor() + 0.026f + i * 0.00002f);
        }

        private static void DrawPuff(Vector3 spot, CurrentPuff puff, float altitude)
        {
            if (puff.alpha <= 0f) return;
            DrawMesh(MeshPool.plane10, (spot + Flat(puff.at)).WithY(altitude), puff.width, puff.depth, Fade(Dust, puff.alpha), soft);
        }

        private static SixPathsStrip[] Strips(string name, int count, int points)
        {
            var made = new SixPathsStrip[count];
            for (int i = 0; i < count; i++) made[i] = new SixPathsStrip("Six Paths current " + name + " " + i, points);
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
    }
}
