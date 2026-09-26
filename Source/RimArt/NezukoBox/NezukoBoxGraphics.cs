using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using CS = RimArt.ChainSickleGraphics;

namespace RimArt
{
    /// <summary>How the box looks in one frame (lib/nezuko-box.js drawBox's options).</summary>
    public struct NezukoBoxLook
    {
        /// <summary>0..1 how far the door has swung open (1 = 150 degrees, folded against the side).</summary>
        public float Door;
        /// <summary>0..1 how far the top lid has flipped up (1 = 110 degrees, over toward the wearer).</summary>
        public float Lid;
        /// <summary>0..1 someone asleep inside: the box breathes, the door seam glows pink, z marks rise.</summary>
        public float Sleeping;
        /// <summary>Sideways jolt in cells (the rumble before a burst, the latch closing).</summary>
        public float Shake;
        /// <summary>0..1 light spilling out of the open door or lid.</summary>
        public float Inner;
        /// <summary>The clock the breathing and the z marks run on, seconds.</summary>
        public float T;
    }

    /// <summary>Where the box was drawn: the door's and the top's centres on the ground and on screen.</summary>
    public struct NezukoBoxDrawn
    {
        public Vector2 DoorGround, DoorScreen, TopGround, TopScreen, Centre;
        /// <summary>The top's height above the floor, cells (scaled).</summary>
        public float TopH;
        /// <summary>The altitude the box started drawing at.</summary>
        public float Layer;
        /// <summary>The box stands north of the wearer (facing south) and draws under the pawn.</summary>
        public bool Behind;
    }

    /// <summary>
    /// Nezuko's box: the wooden box worn on the back, its door, the top lid, the straps, the sleeping
    /// marks, the wood dust and the daze marks. The port of Tools/VfxLab/web/sketches/lib/nezuko-box.js;
    /// its numbers are that file's.
    ///
    /// The box is a true cuboid behind the wearer's feet along its facing, projected the way the kit
    /// projects height (a point h cells up is drawn h x Lift further north). Every side face whose
    /// outward normal points south is drawn between its base edge at H0 and its top edge at H1, the top
    /// face last, so one routine covers every facing and there is no per-facing method. Facing south
    /// the box stands north of the pawn and draws under the pawn layer; otherwise over it.
    ///
    /// Everything is laid out on the lab's stand-in pawn and takes a <c>scale</c>: 1 in the previews,
    /// <see cref="FitScale"/> on a real pawn, whose feet are moved by <see cref="FitDrop"/> first
    /// (<see cref="Feet"/>), as the Samehada port fits its shark form. The stand-in pawns are not drawn.
    /// Strips and sprites come from VfxDraw: call its Begin first.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class NezukoBoxGraphics
    {
        // Palette, from the anime: orange-brown planks, black iron, pink light.
        internal static readonly Color Skin = new Color(0.83f, 0.70f, 0.54f), Pale = new Color(1f, 0.96f, 0.90f);
        internal static readonly Color Wood = new Color(0.70f, 0.37f, 0.17f), WoodLit = new Color(0.86f, 0.52f, 0.27f), WoodDark = new Color(0.42f, 0.19f, 0.08f);
        internal static readonly Color Grain = new Color(0.36f, 0.15f, 0.06f), Iron = new Color(0.09f, 0.09f, 0.10f), IronLit = new Color(0.30f, 0.30f, 0.33f);
        internal static readonly Color Strap = new Color(0.16f, 0.12f, 0.09f), Inside = new Color(0.10f, 0.03f, 0.04f);
        internal static readonly Color Pink = new Color(1f, 0.55f, 0.72f), Dust = new Color(0.78f, 0.62f, 0.45f), Blood = new Color(0.44f, 0.06f, 0.06f);
        internal static readonly Color Body = CS.Body;

        /// <summary>Box centre behind the feet, its half depth (along the facing) and half width, cells.</summary>
        internal const float Back = -0.20f, HalfDepth = 0.13f, HalfWidth = 0.25f;
        /// <summary>Bottom and top of the box above the floor, cells.</summary>
        internal const float H0 = 0.25f, H1 = 1.05f;
        /// <summary>The door's share of the outer face.</summary>
        internal const float DoorU0 = 0.14f, DoorU1 = 0.86f, DoorV0 = 0.08f, DoorV1 = 0.92f;
        internal const float Lift = SixPathsHeight.Lift;
        /// <summary>A real pawn draws about 1.3x the stand-in and 0.33 cells lower (as SamehadaGraphics.FitScale measured).</summary>
        internal const float FitScale = 1.3f, FitDrop = -0.33f;
        /// <summary>The breathing period, seconds.</summary>
        internal const float Breath = 2.6f;

        internal static float Y => CS.Y;
        internal static float ShadowLayer => CS.ShadowLayer;
        internal static float PawnLayer => CS.PawnLayer;

        internal static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);

        private static readonly Vector2[] Corner = new Vector2[4], Normal = new Vector2[4];
        private static readonly int[] Faces = new int[4];
        private static Vector2 F, N, C, Origin;
        private static float Scale, ShakeX;
        private static float layerBase;
        private static int layerStep;

        internal static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Sin(x * Mathf.PI) : 0f;

        /// <summary>A real pawn's feet as the pictures place them.</summary>
        internal static Vector2 Feet(Vector3 drawPos) => new Vector2(drawPos.x, drawPos.z + FitDrop);

        /// <summary>A screen point: ground point g lifted h cells.</summary>
        internal static Vector2 Up(Vector2 g, float h) => new Vector2(g.x, g.y + h * Lift);

        private static float L() => layerBase + (layerStep++) * 0.0012f;

        private static Vector2 At(float along, float across) =>
            new Vector2(Origin.x + (along * F.x + across * N.x) * Scale + ShakeX, Origin.y + (along * F.y + across * N.y) * Scale);

        /// <summary>The box's frame for a wearer at <paramref name="feet"/> facing <paramref name="deg"/> (0 east, 90 north).</summary>
        private static void Frame(Vector2 feet, float deg, float scale, float shake)
        {
            float a = deg * Mathf.Deg2Rad;
            F = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            N = new Vector2(-F.y, F.x);
            Origin = feet;
            Scale = scale;
            ShakeX = shake * scale;
            C = At(Back, 0f);
            // Base corners counter-clockwise seen from above: back-right, back-left, front-left,
            // front-right, where "back" is the outer face away from the wearer. Faces are edges i to
            // i + 1; outward normals: 0 outer (-f), 1 left (+n), 2 inner (+f), 3 right (-n).
            Corner[0] = At(Back - HalfDepth, -HalfWidth);
            Corner[1] = At(Back - HalfDepth, HalfWidth);
            Corner[2] = At(Back + HalfDepth, HalfWidth);
            Corner[3] = At(Back + HalfDepth, -HalfWidth);
            Normal[0] = -F; Normal[1] = N; Normal[2] = F; Normal[3] = -N;
        }

        /// <summary>A quad: a0 to a1 on one side, b0 to b1 on the other (the lab's band with two points a side).</summary>
        internal static void Quad(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, Color colour, float altitude)
        {
            if (colour.a <= 0.001f) return;
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = a0; a[1] = a1; b[0] = b0; b[1] = b1;
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A straight bar of constant width from a to b (the lab's seg).</summary>
        internal static void Seg(Vector2 a, Vector2 b, float w, Color colour, float altitude)
        {
            float dx = b.x - a.x, dz = b.y - a.y, len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len <= 0f) len = 1f;
            var off = new Vector2(-dz / len * w / 2f, dx / len * w / 2f);
            Quad(a + off, b + off, a - off, b - off, colour, altitude);
        }

        /// <summary>
        /// A ribbon through <paramref name="count"/> points, widest in the middle (the lab's trail).
        /// </summary>
        internal static void Trail(Vector2[] pts, int count, float width, Color colour, float altitude)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < count; i++)
            {
                Vector2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(count - 1, i + 1)];
                float dx = next.x - prev.x, dz = next.y - prev.y, len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len <= 0f) len = 1f;
                float w = Mathf.Sin(i / (float)(count - 1) * Mathf.PI) * width / 2f;
                a[i] = new Vector2(pts[i].x - dz / len * w, pts[i].y + dx / len * w);
                b[i] = new Vector2(pts[i].x + dz / len * w, pts[i].y - dx / len * w);
            }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A quad on side face i between shares u0..u1 of its width and v0..v1 of its height.</summary>
        private static void FaceQuad(int i, float u0, float u1, float v0, float v1, Color colour, float altitude, float h0, float h1)
        {
            Vector2 p = Corner[i], q = Corner[(i + 1) % 4];
            Vector2 g0 = Vector2.Lerp(p, q, u0), g1 = Vector2.Lerp(p, q, u1);
            float lo = Mathf.Lerp(h0, h1, v0), hi = Mathf.Lerp(h0, h1, v1);
            Quad(Up(g0, lo), Up(g1, lo), Up(g0, hi), Up(g1, hi), colour, altitude);
        }

        private static Vector2 TopPoint(float u, float w, float h)
        {
            Vector2 a = Vector2.Lerp(Corner[0], Corner[1], u), d = Vector2.Lerp(Corner[3], Corner[2], u);
            return Up(Vector2.Lerp(a, d, w), h);
        }

        /// <summary>A quad on the top face between shares of its two edges (u along edge 0, w toward edge 2).</summary>
        private static void TopQuad(float u0, float u1, float w0, float w1, Color colour, float altitude, float h)
        {
            Quad(TopPoint(u0, w0, h), TopPoint(u1, w0, h), TopPoint(u0, w1, h), TopPoint(u1, w1, h), colour, altitude);
        }

        /// <summary>How lit a face is: the sun comes from the side opposite the shadow.</summary>
        private static float Shade(Vector2 normal, Vector2 sun)
        {
            float len = sun.magnitude;
            if (len <= 0f) len = 1f;
            return Mathf.Clamp01(0.5f + 0.5f * (normal.x * -sun.x / len + normal.y * -sun.y / len));
        }

        /// <summary>
        /// The box, its straps and its door for one frame. <paramref name="scale"/> is 1 in the previews and
        /// <see cref="FitScale"/> on a real pawn; <paramref name="pawnLayer"/> is the wearer's altitude,
        /// under which the box draws when it stands behind the wearer (null: the picture's pawn layer).
        /// </summary>
        internal static NezukoBoxDrawn Box(Vector2 feet, float deg, in NezukoBoxLook look, Vector2 sun, float strength, float scale = 1f, float? pawnLayer = null)
        {
            Frame(feet, deg, scale, look.Shake);
            float S = scale, t = look.T, sleeping = look.Sleeping;
            float breathe = 1f + 0.018f * sleeping * Mathf.Sin(t * Mathf.PI * 2f / Breath);
            float h0 = H0 * S, h1 = (H0 + (H1 - H0) * breathe) * S;
            // Behind the wearer (north of it on screen) the box draws under the pawn so the pawn stands in
            // front; otherwise over it.
            bool behind = C.y > feet.y + 0.05f * S;
            float pawnAlt = pawnLayer ?? PawnLayer;
            layerBase = behind ? pawnAlt - 0.06f : Y + 0.002f;
            layerStep = 0;
            var drawn = new NezukoBoxDrawn { Centre = C, Behind = behind, Layer = layerBase };

            Sprite(new Vector2(C.x + sun.x * h1 * 0.55f, C.y + sun.y * h1 * 0.55f), 0.75f * S, 0.5f * S, Fade(Body, strength), soft, ShadowLayer);

            // Side faces facing south, north first so nearer ones cover them.
            int faces = 0;
            for (int i = 0; i < 4; i++) if (Normal[i].y < -1e-3f) Faces[faces++] = i;
            for (int i = 1; i < faces; i++)
                for (int j = i; j > 0 && EdgeZ(Faces[j]) > EdgeZ(Faces[j - 1]); j--)
                {
                    int x = Faces[j]; Faces[j] = Faces[j - 1]; Faces[j - 1] = x;
                }
            bool doorFound = false;
            for (int k = 0; k < faces; k++)
            {
                int i = Faces[k];
                float lit = Shade(Normal[i], sun);
                Color face = Color.Lerp(WoodDark, WoodLit, 0.25f + 0.6f * lit);
                FaceQuad(i, 0f, 1f, 0f, 1f, face, L(), h0, h1);
                for (int g = 1; g <= 3; g++) FaceQuad(i, g / 4f - 0.008f, g / 4f + 0.008f, 0.03f, 0.97f, Fade(Grain, 0.55f), L(), h0, h1);
                if (i == 0)
                {
                    // The door: a seam, a latch, and when open the dark inside with light spilling out.
                    FaceQuad(0, DoorU0 - 0.02f, DoorU1 + 0.02f, DoorV0 - 0.02f, DoorV1 + 0.02f, Fade(Grain, 0.9f), L(), h0, h1);
                    FaceQuad(0, DoorU0, DoorU1, DoorV0, DoorV1, face, L(), h0, h1);
                    if (look.Door > 0.02f)
                    {
                        FaceQuad(0, DoorU0, DoorU1, DoorV0, DoorV1, Inside, L(), h0, h1);
                        if (look.Inner > 0.01f) FaceQuad(0, DoorU0 + 0.05f, DoorU1 - 0.05f, DoorV0 + 0.05f, DoorV1 - 0.05f, Fade(Pink, 0.55f * look.Inner), L(), h0, h1);
                    }
                    else if (sleeping > 0.01f)
                    {
                        float pulse = 0.65f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f / Breath);
                        FaceQuad(0, DoorU0 - 0.045f, DoorU1 + 0.045f, DoorV0 - 0.04f, DoorV1 + 0.04f, Fade(Pink, pulse * sleeping), L(), h0, h1);
                        FaceQuad(0, DoorU0, DoorU1, DoorV0, DoorV1, face, L(), h0, h1);
                    }
                    if (look.Door <= 0.02f) FaceQuad(0, DoorU1 - 0.09f, DoorU1 - 0.02f, 0.45f, 0.58f, Iron, L(), h0, h1);
                }
                for (int v = 0; v < 3; v++)
                {
                    float at = v == 0 ? 0.12f : v == 1 ? 0.5f : 0.86f;
                    FaceQuad(i, 0f, 1f, at - 0.035f, at + 0.035f, Iron, L(), h0, h1);
                }
                FaceQuad(i, 0f, 0.1f, 0.88f, 1f, Iron, L(), h0, h1);
                FaceQuad(i, 0.9f, 1f, 0.88f, 1f, Iron, L(), h0, h1);
                FaceQuad(i, 0f, 0.1f, 0f, 0.12f, Iron, L(), h0, h1);
                FaceQuad(i, 0.9f, 1f, 0f, 0.12f, Iron, L(), h0, h1);
                if (i == 0)
                {
                    Vector2 g = Vector2.Lerp(Corner[0], Corner[1], (DoorU0 + DoorU1) / 2f);
                    drawn.DoorGround = g;
                    drawn.DoorScreen = Up(g, Mathf.Lerp(h0, h1, (DoorV0 + DoorV1) / 2f));
                    doorFound = true;
                }
            }

            // The top: planks, an iron frame and a band; with the lid up, the dark inside and light
            // spilling out, and the lid standing on its hinge at the inner edge (the wearer's side).
            if (look.Lid <= 0.02f)
            {
                TopQuad(0f, 1f, 0f, 1f, WoodLit, L(), h1);
                for (int g = 1; g <= 3; g++) TopQuad(g / 4f - 0.01f, g / 4f + 0.01f, 0.05f, 0.95f, Fade(Grain, 0.5f), L(), h1);
                TopQuad(0.47f, 0.53f, 0f, 1f, IronLit, L(), h1);
            }
            else
            {
                TopQuad(0f, 1f, 0f, 1f, Inside, L(), h1);
                if (look.Inner > 0.01f) TopQuad(0.12f, 0.88f, 0.15f, 0.85f, Fade(Pink, 0.6f * look.Inner), L(), h1);
            }
            TopQuad(0f, 1f, 0f, 0.12f, Iron, L(), h1);
            TopQuad(0f, 1f, 0.88f, 1f, Iron, L(), h1);
            TopQuad(0f, 0.06f, 0f, 1f, Iron, L(), h1);
            TopQuad(0.94f, 1f, 0f, 1f, Iron, L(), h1);
            if (look.Lid > 0.02f) Lid(look.Lid, h1, S);
            drawn.TopGround = C;
            drawn.TopScreen = Up(C, h1);
            drawn.TopH = h1;

            // The door when it is open: a panel swinging on its hinge at the outer face's u1 edge.
            if (!doorFound)
            {
                Vector2 g = Vector2.Lerp(Corner[0], Corner[1], (DoorU0 + DoorU1) / 2f);
                drawn.DoorGround = g;
                drawn.DoorScreen = Up(g, Mathf.Lerp(h0, h1, 0.5f));
            }
            if (look.Door > 0.02f) DoorPanel(look.Door, h0, h1, S, behind, sun);

            // Shoulder straps: from the box's inner face at shoulder height over the shoulders, down the
            // chest to the hip. Shown when the wearer faces south, east or west; facing north the chest is
            // away from the camera.
            if (F.y <= 0.7f)
            {
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    if (Mathf.Abs(F.x) > 0.7f && sd * N.y > 0f) continue;   // side view: only the near strap
                    Vector2 top = At(Back + HalfDepth, sd * 0.15f), chest = At(0.06f, sd * 0.12f), hip = At(0.03f, sd * 0.12f);
                    Seg(Up(top, 0.62f * S), Up(chest, 0.45f * S), 0.055f * S, Strap, Y + 0.004f);
                    Seg(Up(chest, 0.45f * S), Up(hip, 0.28f * S), 0.055f * S, Strap, Y + 0.0041f);
                }
            }

            // Sleeping marks: small "z" shapes that rise from the top and fade, one every 1.3 s.
            if (sleeping > 0.01f)
            {
                for (int j = 0; j < 3; j++)
                {
                    float u = Mathf.Repeat(t + j * 1.3f, 3.9f) / 3.9f, z = (0.05f + 0.04f * u) * S, a = Mathf.Sin(u * Mathf.PI) * 0.9f * sleeping;
                    Vector2 o = Up(new Vector2(C.x + (0.12f + u * 0.25f + Mathf.Sin(u * 6f + j) * 0.04f) * S, C.y), h1 + (0.15f + u * 0.7f) * S);
                    Color c = Fade(Pale, a);
                    float l = Y + 0.03f + j * 0.002f, w = 0.022f * S;
                    Seg(new Vector2(o.x - z, o.y + z), new Vector2(o.x + z, o.y + z), w, c, l);
                    Seg(new Vector2(o.x + z, o.y + z), new Vector2(o.x - z, o.y - z), w, c, l + 0.0005f);
                    Seg(new Vector2(o.x - z, o.y - z), new Vector2(o.x + z, o.y - z), w, c, l + 0.001f);
                }
            }
            return drawn;
        }

        private static float EdgeZ(int i) => Corner[i].y + Corner[(i + 1) % 4].y;

        /// <summary>
        /// The top lid, hinged on the inner edge (the wearer's side). Its depth is the box's; at 0 it lies
        /// over the top, opening lifts the outer edge up and over toward the wearer, to 110 degrees.
        /// </summary>
        private static void Lid(float lid, float h1, float S)
        {
            float th = lid * 110f * Mathf.Deg2Rad, D = 2f * HalfDepth * S;
            Vector2 hinge0 = Corner[3], hinge1 = Corner[2];
            var off = new Vector2(-F.x * D * Mathf.Cos(th), -F.y * D * Mathf.Cos(th));
            float rise = D * Mathf.Sin(th);
            Color lidLit = th < Mathf.PI / 2f ? WoodLit : WoodDark;
            float lidLayer = L() + 0.004f;
            Quad(LidEdge(hinge0, hinge1, off, rise, h1, 0f, 0f), LidEdge(hinge0, hinge1, off, rise, h1, 1f, 0f),
                LidEdge(hinge0, hinge1, off, rise, h1, 0f, 1f), LidEdge(hinge0, hinge1, off, rise, h1, 1f, 1f), lidLit, lidLayer);
            for (int k = 0; k < 2; k++)
            {
                float w = k == 0 ? 0f : 0.88f;
                Quad(LidEdge(hinge0, hinge1, off, rise, h1, 0f, w), LidEdge(hinge0, hinge1, off, rise, h1, 1f, w),
                    LidEdge(hinge0, hinge1, off, rise, h1, 0f, w + 0.12f), LidEdge(hinge0, hinge1, off, rise, h1, 1f, w + 0.12f), Iron, lidLayer + 0.0005f);
            }
            Quad(LidEdge(hinge0, hinge1, off, rise, h1, 0.47f, 0f), LidEdge(hinge0, hinge1, off, rise, h1, 0.53f, 0f),
                LidEdge(hinge0, hinge1, off, rise, h1, 0.47f, 1f), LidEdge(hinge0, hinge1, off, rise, h1, 0.53f, 1f), IronLit, lidLayer + 0.0006f);
        }

        private static Vector2 LidEdge(Vector2 hinge0, Vector2 hinge1, Vector2 off, float rise, float h1, float u, float w)
        {
            Vector2 h = Vector2.Lerp(hinge0, hinge1, u);
            return Up(new Vector2(h.x + off.x * w, h.y + off.y * w), h1 + rise * w);
        }

        /// <summary>The open door: a panel swinging on its hinge at the outer face's u1 edge, turning outward (-f).</summary>
        private static void DoorPanel(float door, float h0, float h1, float S, bool behind, Vector2 sun)
        {
            float phi = door * 150f * Mathf.Deg2Rad, w = (DoorU1 - DoorU0) * 2f * HalfWidth * S;
            Vector2 hinge = Vector2.Lerp(Corner[0], Corner[1], DoorU1);
            // At phi = 0 the panel runs from the hinge back toward u0 (along -n from the hinge side).
            float dx = -N.x * Mathf.Cos(phi) - F.x * Mathf.Sin(phi), dz = -N.y * Mathf.Cos(phi) - F.y * Mathf.Sin(phi);
            var free = new Vector2(hinge.x + dx * w, hinge.y + dz * w);
            float lo = Mathf.Lerp(h0, h1, DoorV0), hi = Mathf.Lerp(h0, h1, DoorV1);
            float panelLayer = free.y > C.y + 0.02f * S && behind ? layerBase - 0.004f : L();
            float lit = Shade(new Vector2(-dz, dx), sun);
            Quad(Up(hinge, lo), Up(free, lo), Up(hinge, hi), Up(free, hi), Color.Lerp(WoodDark, Wood, 0.3f + 0.6f * lit), panelLayer);
            for (int k = 0; k < 2; k++)
            {
                float v = k == 0 ? 0.2f : 0.8f, a = Mathf.Lerp(lo, hi, v - 0.04f), c = Mathf.Lerp(lo, hi, v + 0.04f);
                Quad(Up(hinge, a), Up(free, a), Up(hinge, c), Up(free, c), Iron, panelLayer + 0.0005f);
            }
        }

        /// <summary>Wood dust thrown off the box: puffs that fly out from <paramref name="at"/> over <paramref name="life"/> seconds and settle, and splinters.</summary>
        internal static void WoodDust(Vector2 at, float age, float amount = 1f, float life = 0.5f, int seed = 0, float scale = 1f)
        {
            if (age < 0f || age > life * 1.6f) return;
            float S = scale;
            for (int i = 0; i < 8; i++)
            {
                float u = Mathf.Clamp01(age / (life * (0.7f + Rand(seed + i + 10) * 0.6f)));
                if (u >= 1f) continue;
                float th = Rand(seed + i) * Mathf.PI * 2f, far = u * (0.25f + Rand(seed + i + 20) * 0.45f) * amount * S;
                Sprite(new Vector2(at.x + Mathf.Cos(th) * far, at.y + Mathf.Sin(th) * far * 0.7f + u * 0.15f * S), (0.18f + u * 0.3f) * amount * S, (0.15f + u * 0.25f) * amount * S,
                    Fade(Dust, Mathf.Sin(u * Mathf.PI) * 0.6f), puff, Y + 0.02f + i * 0.0005f);
            }
            for (int i = 0; i < 6; i++)   // splinters: short dark flecks that fall and stay a moment
            {
                float life2 = 0.35f + Rand(seed + i + 40) * 0.2f, u = age / life2;
                if (u > 2.2f) continue;
                float th = Rand(seed + i + 50) * Mathf.PI * 2f, far = Mathf.Min(u, 1f) * (0.3f + Rand(seed + i + 60) * 0.4f) * amount * S;
                float h = u < 1f ? Mathf.Max(0f, 0.35f * Mathf.Sin(u * Mathf.PI)) * S : 0f;
                var g = new Vector2(at.x + Mathf.Cos(th) * far, at.y + Mathf.Sin(th) * far * 0.7f - 0.15f * Mathf.Min(u, 1f) * S);
                CS.Disc(new Vector2(g.x, g.y + h * Lift), u < 1f ? Y + 0.025f : Floor + 0.03f, 0.035f * S, 0.018f * S, Fade(WoodDark, u < 1f ? 1f : 1f - (u - 1f) / 1.2f));
            }
        }

        /// <summary>Three pale marks circling over a pawn's head: stunned. <paramref name="s"/> turns them.</summary>
        internal static void DazeMarks(Vector2 feet, float s, float age, float length, float scale = 1f)
        {
            if (age < 0f || age > length + 0.3f) return;
            float fade = 1f - Smooth((age - length) / 0.3f);
            for (int i = 0; i < 3; i++)
            {
                float turn = s * 5f + i * 2.094f;
                Sprite(new Vector2(feet.x + Mathf.Cos(turn) * 0.2f * scale, feet.y + (0.86f + Mathf.Sin(turn) * 0.06f) * scale), 0.08f * scale, 0.08f * scale,
                    Fade(Pale, 0.9f * fade), soft, Y + 0.03f + i * 0.0005f);
            }
        }
    }
}
