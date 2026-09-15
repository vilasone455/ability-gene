using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Height, faked. RimWorld's camera looks straight down, so nothing drawn on the map has an
    /// altitude: a thing nine cells up and a thing lying on the floor land on exactly the same
    /// pixels. Every height in this effect is therefore two things instead -- a northward offset
    /// of 0.60 cells per cell up, and a small size gain -- with the shadow left behind on the
    /// ground. The shadow is the half that matters. Offset and size alone are equally readable as
    /// a large object further north; only a shadow that stays put says the object left the floor.
    /// </summary>
    public static class SixPathsHeight
    {
        public const float Lift = 0.60f;
        public const float Gain = 0.030f;
        /// <summary>The clearance at which the gain and the shadow reach their extremes.</summary>
        public const float Ceiling = 9f;

        public static Vector3 Above(Vector3 ground, float height) =>
            ground + new Vector3(0f, 0f, height * Lift);

        public static float Scale(float clearance) => 1f + clearance * Gain;

        public static float ShadowSize(float clearance) =>
            Mathf.Lerp(1f, 0.52f, Mathf.Clamp01(clearance / Ceiling));

        public static float ShadowAlpha(float clearance) =>
            Mathf.Lerp(0.42f, 0.09f, Mathf.Clamp01(clearance / Ceiling));
    }

    /// <summary>
    /// The block the six orbs fuse into: two cells wide, two deep and six cells tall. It stands.
    /// Six is its height off the ground, not a length along it, and it keeps that attitude from
    /// the moment it forms to well after it lands -- a dropped refrigerator, which arrives in the
    /// pose it left in and does not tip over to show you its top.
    ///
    /// That is the whole reason for the box maths. A block that lands flat presents its top face
    /// to a top-down camera, and a top face is a rectangle: no amount of shading makes it a solid.
    /// A block that lands standing presents its front, its top and one side at once, three planes
    /// at different angles to the light, and reads as a solid from any distance.
    ///
    /// Eight corners go through one rotation about the vertical and an oblique projection. The
    /// projection is oblique rather than perspective: ground coordinates pass through untouched
    /// and only height displaces a point, by <see cref="SixPathsHeight.Lift"/> north per cell, so
    /// the 2x2 cells the block covers stay honestly 2x2 while six cells of height draw as 3.6
    /// cells of front face.
    ///
    /// Nothing here touches Verse: it is eight points and some arithmetic, so the whole of it is
    /// checked in Tests/SixPaths.
    /// </summary>
    public static class SixPathsSlab
    {
        public const float Width = 2f, Depth = 2f, Height = 6f;
        public const float HalfWidth = Width * 0.5f, HalfDepth = Depth * 0.5f,
            HalfHeight = Height * 0.5f;
        public const int CornerCount = 8, FaceCount = 6, EdgeCount = 12;

        /// <summary>Centre height at which the block stands on the ground rather than through it.</summary>
        public const float Rest = HalfHeight;

        /// <summary>
        /// Corner index bits: 1 is +x (east), 2 is +y (up), 4 is +z (north).
        /// </summary>
        public static Vector3 Corner(int index) => new Vector3(
            (index & 1) == 0 ? -HalfWidth : HalfWidth,
            (index & 2) == 0 ? -HalfHeight : HalfHeight,
            (index & 4) == 0 ? -HalfDepth : HalfDepth);

        /// <summary>
        /// The six faces as +x, -x, +y, -y, +z, -z, each wound counter-clockwise about its own
        /// outward normal. Under the projection below that winding reads clockwise on screen for
        /// a face that points at the camera, so the sign of a face's projected area is the whole
        /// of the visibility test -- see <see cref="Facing"/>.
        /// </summary>
        public static readonly int[][] FaceCorners =
        {
            new[] { 7, 5, 1, 3 }, new[] { 2, 0, 4, 6 },
            new[] { 7, 3, 2, 6 }, new[] { 4, 0, 1, 5 },
            new[] { 7, 6, 4, 5 }, new[] { 1, 0, 2, 3 },
        };

        public static readonly Vector3[] FaceNormals =
        {
            new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 1f, 0f), new Vector3(0f, -1f, 0f),
            new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f),
        };

        /// <summary>The twelve edges, and the two faces meeting along each.</summary>
        public static readonly int[][] EdgeCorners = new int[EdgeCount][];
        public static readonly int[][] EdgeFaces = new int[EdgeCount][];

        // Mostly overhead, leaning south-west. The horizontal part is what separates the two
        // sides the camera can see, and the south of it keeps the front face -- the one the player
        // looks at for the whole of the effect -- off the body colour. It stays small: push the
        // light much further over and the front comes up to meet the top in value, which is the
        // same as having no top face at all.
        public static readonly Vector3 LightDirection = new Vector3(-0.30f, 0.90f, -0.30f).normalized;

        static SixPathsSlab()
        {
            int next = 0;
            for (int corner = 0; corner < CornerCount; corner++)
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((corner & bit) != 0) continue;
                    EdgeCorners[next] = new[] { corner, corner | bit };
                    EdgeFaces[next] = Adjacent(corner, corner | bit);
                    next++;
                }
        }

        private static int[] Adjacent(int a, int b)
        {
            var faces = new int[2];
            int found = 0;
            for (int face = 0; face < FaceCount && found < 2; face++)
                if (Holds(face, a) && Holds(face, b)) faces[found++] = face;
            return faces;
        }

        private static bool Holds(int face, int corner)
        {
            int[] corners = FaceCorners[face];
            for (int i = 0; i < 4; i++) if (corners[i] == corner) return true;
            return false;
        }

        /// <summary>
        /// Turn a point of the block about the vertical. This is the only rotation the block has:
        /// it never pitches or rolls, so nothing about the shape changes between the sky and the
        /// ground and the fall is a translation.
        /// </summary>
        public static Vector3 Orient(Vector3 local, float yaw)
        {
            float y = yaw * Mathf.Deg2Rad, cy = Mathf.Cos(y), sy = Mathf.Sin(y);
            return new Vector3(local.x * cy + local.z * sy, local.y, local.z * cy - local.x * sy);
        }

        /// <summary>
        /// The eight corners in map space, relative to the cell the block is falling onto.
        /// One call fills the array every frame; the faces, the edges and the visibility test all
        /// read out of it rather than each repeating the rotation.
        /// </summary>
        public static void Project(Vector2[] into, float yaw, float height, float scale)
        {
            for (int i = 0; i < CornerCount; i++)
            {
                Vector3 point = Orient(Corner(i) * scale, yaw);
                into[i] = new Vector2(point.x, point.z + (point.y + height) * SixPathsHeight.Lift);
            }
        }

        /// <summary>The same eight corners dropped straight down, which is the block's shadow.</summary>
        public static void Footprint(Vector2[] into, float yaw, float scale)
        {
            for (int i = 0; i < CornerCount; i++)
            {
                Vector3 point = Orient(Corner(i) * scale, yaw);
                into[i] = new Vector2(point.x, point.z);
            }
        }

        /// <summary>
        /// Twice the signed area of a projected face. Negative is a face turned toward the camera.
        /// A box is convex, so the faces that survive this test never overlap one another on
        /// screen and need no depth sorting -- they are drawn at one altitude in any order.
        /// </summary>
        public static float Facing(Vector2[] corners, int face)
        {
            int[] index = FaceCorners[face];
            float area = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[index[i]], b = corners[index[(i + 1) & 3]];
                area += a.x * b.y - b.x * a.y;
            }
            return area;
        }

        public static bool Visible(Vector2[] corners, int face) => Facing(corners, face) < 0f;

        /// <summary>An edge is drawn when either face along it is turned toward the camera.</summary>
        public static bool EdgeVisible(Vector2[] corners, int edge) =>
            Visible(corners, EdgeFaces[edge][0]) || Visible(corners, EdgeFaces[edge][1]);

        /// <summary>
        /// How strongly a face catches the light, 0 to 1. The block is black and carries no
        /// interior detail, so this and the lit edges are the only things making it a solid.
        ///
        /// The light wraps right around rather than clipping at the terminator: a face turned away
        /// from it still returns something, because a face at exactly the body colour is a hole in
        /// the block and not a plane of it. Three planes have to differ in value from each other,
        /// which is the whole job of this number.
        /// </summary>
        public static float Lambert(int face, float yaw) =>
            0.5f + 0.5f * Vector3.Dot(Orient(FaceNormals[face], yaw), LightDirection);

        /// <summary>The underside of the block, in cells above the ground.</summary>
        public static float Lowest(float height, float scale) => height - HalfHeight * scale;
    }
}
