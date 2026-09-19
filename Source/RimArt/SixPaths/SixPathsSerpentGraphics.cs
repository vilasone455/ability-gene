using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws Black Serpent: the sage's ring with one slot empty, that orb's flight to the floor and
    /// back, the ribbon it feeds along the ground and round the target, the catch ring and the glint
    /// at the ribbon's tip. SixPathsSerpentTiming says where everything is; this only turns it into
    /// meshes.
    ///
    /// The ribbon is one line of points shared out between two pairs of strips: the tether and the
    /// north half of the coil go under the pawn layer, the south half of the coil goes over it, so
    /// the target's legs sit inside the coil.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsSerpentGraphics
    {
        private const int Points = SixPathsSerpentTiming.Points;

        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh catchRing = SixPathsBurstGraphics.Band(0.95f, "Six Paths serpent catch ring");

        // One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh
        // when the frame renders, not when it is called.
        private static readonly SixPathsStrip backCore = new SixPathsStrip("Six Paths serpent back", Points),
            backRim = new SixPathsStrip("Six Paths serpent back rim", Points),
            frontCore = new SixPathsStrip("Six Paths serpent front", Points),
            frontRim = new SixPathsStrip("Six Paths serpent front rim", Points),
            flight = new SixPathsStrip("Six Paths serpent orb flight", 3);
        private static readonly SerpentPoint[] middle = new SerpentPoint[Points];
        private static readonly Vector2[] left = new Vector2[Points], right = new Vector2[Points],
            rimLeft = new Vector2[Points], rimRight = new Vector2[Points], flightPoints = new Vector2[3];
        private static readonly bool[] notBack = new bool[Points], notFront = new bool[Points];

        // The ribbon keeps the sketch's own ink and edge, which are a shade off the orb's.
        private static readonly Color Ink = new Color(0.025f, 0.022f, 0.036f);
        private static readonly Color Edge = new Color(0.62f, 0.53f, 0.79f);
        private static readonly Color Pale = new Color(0.87f, 0.83f, 0.96f);

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone. <paramref name="target"/>
        /// is where the restrained pawn stands and <paramref name="sage"/> is where the caster stands.
        /// </summary>
        public static void Draw(Vector3 target, Vector3 sage, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= SixPathsSerpentTiming.Duration) return;
            if (!Shown(target, map) || !Shown(sage, map)) return;
            var gap = new Vector2(target.x - sage.x, target.z - sage.z);
            float range = gap.magnitude - SixPathsSerpentTiming.Behind;
            if (range <= 0f) return;
            Vector2 toward = gap.normalized;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector
                * SixPathsSlamGraphics.SunScale;

            // Slot 0 is the orb this costs. It is away for the whole of the effect.
            SixPathsGraphics.DrawCarried(sage, seconds, 1, sun, GenCelestial.CurShadowStrength(map));
            DrawOrb(target, sage, toward, range, seconds);
            DrawRibbon(target, toward, range, seconds);

            float age = seconds - SixPathsSerpentTiming.CatchAt;
            if (age >= 0f && age < SixPathsSerpentTiming.CatchSeconds)
            {
                float reach = SixPathsSerpentTiming.Radius + age * 1.5f;
                DrawMesh(catchRing, (target + new Vector3(0f, 0f, 0.1f)).WithY(AltitudeLayer.Filth.AltitudeFor() + 0.01f),
                    reach, reach * SixPathsSerpentTiming.Squash,
                    Fade(Pale, (1f - age / SixPathsSerpentTiming.CatchSeconds) * SixPathsSerpentTiming.CatchPulse), solid);
            }
        }

        /// <summary>Slot 0 flies to the floor in front of the sage during the wake and home during the settle.</summary>
        private static void DrawOrb(Vector3 target, Vector3 sage, Vector2 toward, float range, float seconds)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor(), deployed = SixPathsSerpentTiming.Deployed(seconds);
            CarriedSlot slot = SixPathsCarried.Slot(sage, 0, seconds);
            Vector3 home = SixPathsHeight.Above(slot.ground, slot.height) - target;
            Vector2 from = new Vector2(home.x, home.z), spot = -toward * range;
            Vector2 at = SixPathsSerpentTiming.Path(from, spot, deployed);

            DrawMesh(MeshPool.plane10, (target + new Vector3(spot.x, 0f, spot.y)).WithY(AltitudeLayer.Filth.AltitudeFor()),
                0.95f * deployed, 0.5f * deployed, Fade(Ink, deployed * 0.4f), soft);
            SixPathsGraphics.DrawBall(
                (target + new Vector3(at.x, 0f, at.y)).WithY(deployed > 0f ? overhead + 0.01f : SixPathsGraphics.CarriedAltitude(slot)),
                SixPathsCarried.DeployRadius(deployed, SixPathsCarried.Field) * SixPathsSerpentTiming.Fed(seconds), 1f, 1f);
            if (deployed <= 0f || deployed >= 1f) return;
            flightPoints[0] = from;
            flightPoints[1] = (from + at) / 2f + new Vector2(0f, 0.1f);
            flightPoints[2] = at;
            flight.Line(flightPoints, 0.07f);
            DrawMesh(flight.mesh, target.WithY(overhead), 1f, 1f, Fade(Edge, 0.45f), solid);
        }

        private static void DrawRibbon(Vector3 target, Vector2 toward, float range, float seconds)
        {
            float extent = SixPathsSerpentTiming.Extent(seconds), stage = SixPathsSerpentTiming.Stage(seconds);
            if (extent <= 0.001f) return;
            for (int i = 0; i < Points; i++)
                middle[i] = SixPathsSerpentTiming.Point(extent * i / (Points - 1), seconds, toward, range);
            bool anyFront = false;
            for (int i = 0; i < Points; i++)
            {
                Vector2 along = middle[Mathf.Min(Points - 1, i + 1)].at - middle[Mathf.Max(0, i - 1)].at;
                float length = Mathf.Max(0.00001f, along.magnitude);
                Vector2 across = new Vector2(-along.y, along.x) / length;
                float half = SixPathsSerpentTiming.Half(extent * i / (Points - 1), extent);
                left[i] = middle[i].at + across * half;
                right[i] = middle[i].at - across * half;
                rimLeft[i] = middle[i].at + across * (half + SixPathsSerpentTiming.Edge);
                rimRight[i] = middle[i].at - across * (half + SixPathsSerpentTiming.Edge);
                // The piece from this point to the next goes wherever the next point is.
                if (i + 1 >= Points) continue;
                notFront[i] = !middle[i + 1].front;
                notBack[i] = middle[i + 1].front;
                anyFront |= middle[i + 1].front;
            }

            float back = AltitudeLayer.Pawn.AltitudeFor() - 0.01f, front = AltitudeLayer.MoteOverhead.AltitudeFor();
            backRim.Between(rimLeft, rimRight, notBack);
            backCore.Between(left, right, notBack);
            DrawMesh(backRim.mesh, target.WithY(back), 1f, 1f, Fade(Edge, stage), solid);
            DrawMesh(backCore.mesh, target.WithY(back + 0.002f), 1f, 1f, Fade(Ink, stage), solid);
            if (anyFront)
            {
                frontRim.Between(rimLeft, rimRight, notFront);
                frontCore.Between(left, right, notFront);
                DrawMesh(frontRim.mesh, target.WithY(front), 1f, 1f, Fade(Edge, stage), solid);
                DrawMesh(frontCore.mesh, target.WithY(front + 0.002f), 1f, 1f, Fade(Ink, stage), solid);
            }
            // A restrained edge glint is the active-status cue; the body stays black.
            Vector2 tip = middle[Points - 1].at;
            DrawMesh(MeshPool.plane10, (target + new Vector3(tip.x, 0f, tip.y)).WithY(front + 0.02f), 0.3f, 0.3f,
                Fade(Pale, SixPathsSerpentTiming.Glint(seconds)), softGlow);
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
