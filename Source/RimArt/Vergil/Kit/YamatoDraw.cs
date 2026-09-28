using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// Yamato on the real pawn, as the lab sketches draw it on their stand-in (lib/vergil.js carrier and
    /// heldKatana): a black scabbard at the left hip with the gold guard and the dark grip forward; the
    /// right hand going to the grip; the blade out along the aim, a dark under-edge, a steel blade that
    /// lights white, the guard and the hand on the grip; and in the Judgement Cut End kneel, facing the
    /// camera, the scabbard upright in the left hand in front of him with the blade sliding into its mouth.
    ///
    /// The hip needs its own layout per facing, not a mirror (see the per-facing rule in the lab): the left
    /// hip is the near side facing west and the far side facing east, so the scabbard is drawn over the
    /// body facing west and behind it facing east, with only the hilt in front and the end sticking out
    /// behind. Facing south the hilt is in front and the scabbard behind; facing north the scabbard's end
    /// hangs in front (toward the camera) and the hilt is hidden.
    ///
    /// Positions are cells from the pawn's centre, for the Thin body every adult Host has in hero form.
    /// Layers are the render tree's (body 0, coat 29, head 50, north coat 89): a part at layer L is drawn at
    /// the pawn's own altitude plus L, so it sorts with the body the way apparel does.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class YamatoDraw
    {
        internal static readonly Color Scabbard = new Color(0.04f, 0.04f, 0.07f), Lacquer = new Color(0.32f, 0.36f, 0.5f),
            Steel = new Color(0.9f, 0.95f, 1f), Gold = new Color(0.85f, 0.7f, 0.3f), Wrap = new Color(0.55f, 0.6f, 0.75f),
            Knuckle = new Color(0.2f, 0.14f, 0.1f), Skin = new Color(0.83f, 0.7f, 0.54f);
        internal const float BladeLength = 0.75f;
        private const float ScabbardWide = 0.065f, GripWide = 0.055f, UprightScabbard = 0.62f;

        /// <summary>Where the sheathed katana sits for one facing.</summary>
        private struct Wear
        {
            public Vector2 guard, pommel, tip, handRest;
            public float scabbardLayer, hiltLayer, handLayer;
        }

        // North, East, South, West, as Rot4.AsInt.
        private static readonly Wear[] Wears =
        {
            new Wear { guard = new Vector2(-0.14f, -0.08f), pommel = new Vector2(-0.02f, 0.01f), tip = new Vector2(-0.44f, -0.36f),
                handRest = new Vector2(0.2f, -0.12f), scabbardLayer = 95f, hiltLayer = -12f, handLayer = -11f },
            new Wear { guard = new Vector2(0.17f, -0.07f), pommel = new Vector2(0.4f, 0f), tip = new Vector2(-0.52f, -0.24f),
                handRest = new Vector2(0.02f, -0.1f), scabbardLayer = -2f, hiltLayer = 36f, handLayer = 38f },
            new Wear { guard = new Vector2(0.13f, -0.1f), pommel = new Vector2(0f, -0.01f), tip = new Vector2(0.46f, -0.36f),
                handRest = new Vector2(-0.2f, -0.12f), scabbardLayer = -2f, hiltLayer = 36f, handLayer = 38f },
            new Wear { guard = new Vector2(-0.17f, -0.08f), pommel = new Vector2(-0.4f, -0.01f), tip = new Vector2(0.52f, -0.25f),
                handRest = new Vector2(0.02f, -0.1f), scabbardLayer = 36f, hiltLayer = 37f, handLayer = 38f },
        };

        /// <summary>
        /// The kneeling scabbard's mouth, in his left hand a little above his centre, before the squash. He kneels
        /// facing the camera (decided 2026-09-28), so his left hand is on the right of the screen and the scabbard,
        /// the blade and both hands are in front of him. Seen from behind the same scabbard would be on the left
        /// and behind him.
        /// </summary>
        private static Vector2 KneelMouth(Rot4 facing) => new Vector2(facing == Rot4.North ? -0.3f : 0.3f, 0.16f);
        /// <summary>In front of the body and the head (50) facing the camera; behind the body facing away.</summary>
        private static float KneelLayer(Rot4 facing) => facing == Rot4.North ? -9f : 65f;

        // The same squash Patch_PawnRenderer_VergilLook gives the body.
        private static float Squash(VergilLook look) => look?.Squash ?? 1f;
        private static float Widen(VergilLook look) => look?.Widen ?? 1f;

        /// <summary>
        /// In the kneel picture (VergilCoatKneel_Thin) nothing is squashed: his left hand holds the scabbard's mouth
        /// beside his hip, the scabbard's end rests on the floor by his raised knee.
        /// </summary>
        private static readonly Vector2 PictureMouth = new Vector2(0.27f, -0.24f);
        private const float PictureScabbard = 0.34f;

        /// <summary>
        /// Draws Yamato for a pawn holding it. <paramref name="drawPos"/> is the game's weapon point: the
        /// pawn's centre (already moved by the crouch or kneel matrix) raised to the carried layer.
        /// </summary>
        public static void Draw(Pawn pawn, Vector3 drawPos, Rot4 facing, VergilLook look)
        {
            float rootY = drawPos.y - PawnRenderUtility.AltitudeForLayer(facing == Rot4.North ? -10f : 90f);
            float squash = Squash(look), widen = Widen(look);
            Vector2 centre = new Vector2(drawPos.x, drawPos.z);
            Vector2 At(Vector2 local) => centre + new Vector2(local.x * widen, local.y * squash);
            float Layer(float layer) => rootY + PawnRenderUtility.AltitudeForLayer(layer);
            Color skin = pawn.story?.SkinColor ?? Skin;

            if (look != null && look.kneel > 0.5f)
            {
                bool picture = look.kneelPicture != null && facing == Rot4.South;
                Kneeling(picture ? centre + PictureMouth : At(KneelMouth(facing)), look.sheathe, skin, Layer(KneelLayer(facing)),
                    picture ? PictureScabbard : UprightScabbard);
                return;
            }

            Wear wear = Wears[facing.AsInt];
            float blade = look?.blade ?? 0f, hand = look?.hand ?? 0f;
            Vector2 guard = At(wear.guard), tip = At(wear.tip);
            SheathedScabbard(guard, tip, Layer(wear.scabbardLayer), blade <= 0f);
            if (blade <= 0f)
            {
                Hilt(guard, At(wear.pommel), Layer(wear.hiltLayer));
                if (hand > 0f)
                {
                    Vector2 grip = guard + (At(wear.pommel) - guard) * 0.45f;
                    float u = Mathf.Clamp01(hand);
                    Hand(Vector2.Lerp(At(wear.handRest), grip, u * u * (3f - 2f * u)), skin, Mathf.Clamp01(hand * 2.5f), Layer(wear.handLayer));
                }
                return;
            }
            Held(centre, look.aim, blade, look.hot, skin, rootY);
        }

        /// <summary>The guard of the sheathed katana on the map, for glints: the draw and the click happen here.</summary>
        public static Vector2 Guard(Pawn pawn)
        {
            VergilLooks.TryGet(pawn, out VergilLook look);
            Vector3 at = pawn.DrawPos;
            if (look != null && look.kneel > 0.5f) return KneelingMouth(pawn);
            Wear wear = Wears[pawn.Rotation.AsInt];
            return new Vector2(at.x + wear.guard.x * Widen(look), at.z + wear.guard.y * Squash(look));
        }

        /// <summary>The upright scabbard's mouth while kneeling: Judgement Cut End's click glints here.</summary>
        public static Vector2 KneelingMouth(Pawn pawn)
        {
            Vector3 at = pawn.DrawPos;
            VergilLooks.TryGet(pawn, out VergilLook look);
            if (look?.kneelPicture != null && pawn.Rotation == Rot4.South) return new Vector2(at.x, at.z) + PictureMouth;
            const float squash = 1f - VergilLook.KneelSquash, widen = 1f + VergilLook.KneelWiden;
            Vector2 mouth = KneelMouth(pawn.Rotation);
            return new Vector2(at.x + mouth.x * widen, at.z + VergilLook.Feet * (1f - squash) + mouth.y * squash);
        }

        /// <summary>The black scabbard from the mouth at the guard to its end, with a thin line of lacquer shine.</summary>
        private static void SheathedScabbard(Vector2 mouth, Vector2 end, float altitude, bool full)
        {
            Vector2 along = end - mouth;
            float length = along.magnitude;
            if (length < 0.01f) return;
            Vector2 d = along / length, side = new Vector2(-d.y, d.x);
            Bar(mouth, end, ScabbardWide, Scabbard, altitude);
            Bar(mouth + d * 0.05f + side * 0.014f, end - d * 0.06f + side * 0.014f, 0.012f, Fade(Lacquer, 0.8f), altitude + 0.0001f);
            // The end cap, and an empty mouth once the blade is out.
            Bar(end - d * 0.05f, end, ScabbardWide * 1.1f, Knuckle, altitude + 0.0002f);
            if (!full) Bar(mouth, mouth + d * 0.035f, ScabbardWide * 1.15f, Fade(Lacquer, 0.9f), altitude + 0.0002f);
        }

        /// <summary>The gold guard across the grip and the dark grip with its light wrap, up to the pommel.</summary>
        private static void Hilt(Vector2 guard, Vector2 pommel, float altitude)
        {
            Vector2 along = pommel - guard;
            float length = along.magnitude;
            if (length < 0.01f) return;
            Vector2 d = along / length, side = new Vector2(-d.y, d.x);
            Bar(guard, pommel, GripWide, Scabbard, altitude);
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = guard + d * (length * (0.3f + 0.22f * i));
                Bar(at - side * 0.018f, at + side * 0.018f, 0.018f, Fade(Wrap, 0.9f), altitude + 0.0001f);
            }
            Bar(pommel - d * 0.02f, pommel + d * 0.01f, GripWide * 1.15f, Gold, altitude + 0.0002f);
            Bar(guard - side * 0.055f, guard + side * 0.055f, 0.045f, Gold, altitude + 0.0003f);
        }

        /// <summary>
        /// The katana out and held in front of the pawn along <paramref name="aim"/>: the lib's heldKatana. The
        /// grip is 0.22 cells out from the chest; a blade pointing north is drawn behind the body and one
        /// pointing south in front of it, or the guard and hand land on the pawn's own face.
        /// </summary>
        private static void Held(Vector2 chest, float aim, float out01, float hot, Color skin, float rootY)
        {
            Vector2 d = Turn(aim), grip = chest + d * 0.22f;
            float layer = rootY + PawnRenderUtility.AltitudeForLayer(d.y > 0f ? -9f : 96f), length = BladeLength * Mathf.Clamp01(out01);
            Vector2 At(float along, float across = 0f) => grip + d * along + new Vector2(-d.y, d.x) * across;
            Streak(At(-0.26f), At(-0.02f), 0.07f, Scabbard, solid, layer, 2);
            if (length > 0.01f)
            {
                VergilGraphics.Tapered(At(0.02f), d, length, 0.105f, Fade(VergilGraphics.Void, 0.55f), solid, layer + 0.0001f);
                VergilGraphics.Tapered(At(0.02f), d, length, 0.075f, Color.Lerp(Steel, Color.white, hot), solid, layer + 0.0002f);
                if (hot > 0f)
                {
                    Sprite(At(length * 0.5f), length * 1.3f, 0.4f * hot + 0.08f, Fade(VergilGraphics.Ice, 0.55f * hot), glow, layer + 0.0003f, -aim);
                    VergilGraphics.Tapered(At(0.02f), d, length, 0.028f, Fade(Color.white, hot), whiteGlow, layer + 0.0004f);
                }
            }
            Streak(At(0f, -0.11f), At(0f, 0.11f), 0.05f, Gold, solid, layer + 0.0005f, 2);
            Hand(grip, skin, 1f, layer + 0.0006f);
        }

        /// <summary>
        /// The kneel: the scabbard upright in the left hand, the blade above its mouth sliding home as
        /// <paramref name="sheathe"/> goes to 1, the guard, the grip and the right hand on it.
        /// </summary>
        private static void Kneeling(Vector2 mouth, float sheathe, Color skin, float altitude, float scabbard)
        {
            float above = BladeLength * (1f - Mathf.Clamp01(sheathe));
            var up = new Vector2(0f, 1f);
            Bar(mouth, mouth - up * scabbard, ScabbardWide * 0.85f, Scabbard, altitude);
            Bar(mouth - up * (scabbard - 0.05f), mouth - up * scabbard, ScabbardWide * 0.95f, Knuckle, altitude + 0.0001f);
            if (above > 0.005f)
            {
                Bar(mouth, mouth + up * above, 0.03f, Steel, altitude + 0.0001f);
                Sprite(mouth + up * (above / 2f), 0.16f, above + 0.2f, Fade(VergilGraphics.Ice, 0.5f), glow, Overhead + 0.02f);
            }
            Vector2 guard = mouth + up * (above + 0.015f);
            Bar(guard - new Vector2(0.055f, 0f), guard + new Vector2(0.055f, 0f), 0.03f, Gold, altitude + 0.0002f);
            Bar(guard + up * 0.015f, guard + up * 0.2f, 0.04f, Scabbard, altitude + 0.0002f);
            Hand(mouth + up * (above + 0.1f), skin, 1f, altitude + 0.0003f);
            Hand(mouth - up * 0.04f, skin, 1f, altitude + 0.0003f);
        }

        /// <summary>A hand: a round skin-coloured dot with a dark rim, the size Melee Animation's hands are drawn at.</summary>
        private static void Hand(Vector2 at, Color skin, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            DrawMesh(disc, at, altitude, 0.07f, 0.07f, 0f, Fade(Knuckle, alpha), solid);
            DrawMesh(disc, at, altitude + 0.00005f, 0.056f, 0.056f, 0f, Fade(skin, alpha), solid);
        }

        /// <summary>A straight bar of even width with square ends.</summary>
        private static void Bar(Vector2 a, Vector2 b, float width, Color colour, float altitude)
        {
            Vector2 along = b - a;
            float length = along.magnitude;
            if (length < 0.001f || colour.a <= 0.001f) return;
            float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            DrawMesh(MeshPool.plane10, (a + b) / 2f, altitude, length, width, -angle, colour, solid);
        }
    }
}
