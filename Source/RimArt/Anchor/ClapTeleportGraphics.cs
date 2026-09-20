using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using T = RimArt.ClapTeleport;

namespace RimArt
{
    /// <summary>
    /// Draws the clap teleport: at each end a level ring of playing cards that comes up out of the
    /// floor before the palms meet, a red puff that covers the cell while the occupants change, a
    /// gold flash and sparkles, and the cards scattering, falling and fading on the floor. Also the
    /// mark cards themselves and the small star at the carrier's palms. The port of the lab's
    /// anchor-clap-teleport.js; the stand-in pawns of the sketch are not drawn here.
    ///
    /// A level ring stays a circle from every side and only shifts north with height, so there is
    /// one drawing for every facing. A standing card is one quad whose width is the cosine of its
    /// turn. Every function takes times and keeps no state.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ClapTeleportGraphics
    {
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly Material[] pip =
        {
            MaterialPool.MatFrom("RimArt/Anchor/SuitSpade", ShaderDatabase.Transparent),
            MaterialPool.MatFrom("RimArt/Anchor/SuitHeart", ShaderDatabase.Transparent),
            MaterialPool.MatFrom("RimArt/Anchor/SuitClub", ShaderDatabase.Transparent),
        };

        private static readonly Color Paper = new Color(0.96f, 0.94f, 0.88f), CardInk = new Color(0.10f, 0.08f, 0.09f);
        private static readonly Color Red = new Color(0.69f, 0.125f, 0.18f), RedLit = new Color(0.86f, 0.27f, 0.30f),
            RedDark = new Color(0.42f, 0.06f, 0.11f);
        private static readonly Color CardGold = new Color(0.88f, 0.69f, 0.25f), GoldPale = new Color(1f, 0.93f, 0.66f);

        private static readonly float Shadows = AltitudeLayer.Shadows.AltitudeFor();
        /// <summary>The north half of a ring is behind whoever stands in it.</summary>
        private static readonly float UnderPawn = AltitudeLayer.Pawn.AltitudeFor() - 0.02f;
        // Two draws at one altitude have no fixed order, so each card takes its own step and its
        // three quads sit inside that step.
        private const float CardStep = 0.0004f, PartStep = 0.0001f;
        // Tile outline: east offset, north offset, width, depth.
        private static readonly float[,] OutlineSides = { { 0f, 0.5f, 1f, 0.03f }, { 0f, -0.5f, 1f, 0.03f }, { 0.5f, 0f, 0.03f, 1f }, { -0.5f, 0f, 0.03f, 1f } };

        /// <summary>
        /// One end of a clap at <paramref name="seconds"/> after the warmup began, with the swap at
        /// <paramref name="contact"/>. <paramref name="index"/> is 0 or 1 and only keeps the two ends
        /// from moving in step. <paramref name="drawMark"/> draws the end's mark card until the contact.
        /// </summary>
        public static void DrawEnd(ClapEnd end, int index, float seconds, float contact, bool drawMark, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration(contact) || !Shown(end.ground, map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
            float strength = 0.32f * GenCelestial.CurShadowStrength(map);
            float riseAt = contact - T.Rise, age = seconds - contact;
            bool swapped = age >= 0f;
            float up = Smooth((seconds - riseAt) / T.Rise), gone = Smooth((age - (T.Fade - T.FloorFade)) / T.FloorFade);

            if (drawMark && !swapped) Mark(end.ground, end.mark, end.suit, index, seconds, seconds - riseAt);
            if (end.mark == ClapMark.Tile) TileOutline(end.ground, 0.5f * (1f - gone));

            int count = end.ring ? T.Cards : 1;
            for (int i = 0; i < count; i++)
            {
                int k = index * 40 + i;
                float slot = i / (float)count * Mathf.PI * 2f + index * 0.7f, step = (index * 9 + i) * CardStep;
                if (!swapped)
                {
                    if (!end.ring || up <= 0f) continue;
                    float a = slot + T.Spin * Mathf.Deg2Rad * age, r = T.Radius * (0.6f + 0.4f * up), h = T.Height * up;
                    var ground = new Vector2(end.ground.x + Mathf.Cos(a) * r, end.ground.y + Mathf.Sin(a) * r);
                    bool behind = Mathf.Sin(a) > 0f;
                    Sprite(ground + sun * h, 0.2f, 0.1f, Fade(CardInk, strength * 0.6f * up), soft, Shadows + step);
                    // Facing outward: the south half shows faces, the north half shows backs.
                    Card(new Vector2(ground.x, ground.y + h * T.Lift), 1f, Mathf.Acos(Mathf.Clamp(-Mathf.Sin(a), -1f, 1f)), 0f, end.suit,
                        Mathf.Clamp01(up * 4f), (behind ? UnderPawn : Overhead + 0.03f) + step);
                    continue;
                }

                float fall = 0.55f + 0.3f * Rand(k), u = Mathf.Clamp01(age / fall), reached = 1f - (1f - u) * (1f - u);
                float angle = slot + (Rand(k + 7) - 0.5f) * 0.5f;
                float far = end.ring ? Mathf.Lerp(T.Radius + 0.3f, T.Scatter, Rand(k + 3)) : 0.25f * Rand(k + 3);
                float radius = Mathf.Lerp(end.ring ? T.Radius : 0f, far, reached);
                float height = (end.ring ? T.Height : 0.7f) * Mathf.Pow(1f - u, 1.5f);
                float sway = Mathf.Sin(age * 9f + k) * 0.09f * (1f - u);
                var under = new Vector2(end.ground.x + Mathf.Cos(angle) * radius + sway, end.ground.y + Mathf.Sin(angle) * radius);
                bool landed = u >= 1f, faceUp = Rand(k + 11) > 0.4f;
                float alpha = 1f - gone;
                if (!landed) Sprite(under + sun * height, 0.2f, 0.1f, Fade(CardInk, strength * 0.6f * alpha), soft, Shadows + step);
                Card(new Vector2(under.x, under.y + height * T.Lift), end.ring ? 1f : T.MarkScale,
                    landed ? (faceUp ? 0f : Mathf.PI) : age * T.Flip + k,
                    landed ? Rand(k + 5) * 360f : Mathf.Sin(age * 7f + k) * 25f, end.suit, alpha,
                    (landed ? Floor + 0.03f : Overhead + 0.03f) + step);
            }

            Cover(end.ground, index, age);
            if (swapped && age < T.FlashLife)
                Sprite(T.Above(end.ground, 0f, 0f, 0.5f), 1.8f, 1.8f, Fade(GoldPale, 0.7f * (1f - age / T.FlashLife)), glow, Overhead + 0.19f + index * 0.001f);
            if (swapped && age < T.SparkleLife)
            {
                float u = age / T.SparkleLife;
                for (int i = 0; i < T.Sparkles; i++)
                {
                    float a = (i * 60f + 20f + index * 30f) * Mathf.Deg2Rad, d = 0.3f + 0.75f * (1f - (1f - u) * (1f - u));
                    Sparkle(T.Above(end.ground, Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.6f, 0.5f + 0.6f * u), 0.16f * (1f - u * 0.6f), 1f - u,
                        45f + 90f * u, Overhead + 0.2f + (index * T.Sparkles + i) * 0.001f);
                }
            }
        }

        /// <summary>
        /// A mark's card. Over a pawn it floats above the head; on a tile it lies on the cell inside
        /// the gold outline. <paramref name="rising"/> is seconds since the cards of a clap against
        /// this mark began to rise, negative while no clap is under way: the card then flips fast,
        /// and a tile's card lifts to the middle of the ring.
        /// </summary>
        public static void Mark(Vector2 ground, ClapMark kind, int suit, int index, float seconds, float rising = -1f)
        {
            if (kind == ClapMark.None) return;
            float up = Smooth(rising / T.Rise);
            float turn = rising < 0f ? Mathf.Sin(seconds * 2.2f + index) * 0.5f : rising * T.Flip * 2f;
            float step = (20 + index) * CardStep;
            if (kind == ClapMark.Pawn)
                Card(T.Above(ground, 0f, 0f, T.MarkHeight + 0.05f * Mathf.Sin(seconds * 3f + index)), T.MarkScale, turn, 0f, suit, 1f, Overhead + 0.05f + step);
            else
                Card(T.Above(ground, 0f, 0f, T.Height * 0.7f * up), T.MarkScale, up > 0f ? turn : 0f, Mathf.Lerp(-14f, 0f, up), suit, 1f,
                    (up > 0f ? Overhead + 0.05f : Floor + 0.02f) + step);
        }

        /// <summary>The gold outline of a marked tile.</summary>
        public static void TileOutline(Vector2 ground, float alpha)
        {
            for (int i = 0; i < 4; i++)
                Sprite(new Vector2(ground.x + OutlineSides[i, 0], ground.y + OutlineSides[i, 1]), OutlineSides[i, 2], OutlineSides[i, 3],
                    Fade(CardGold, alpha), solid, Floor + 0.01f + i * PartStep);
        }

        /// <summary>The 4-point star where the palms met, <paramref name="age"/> seconds after they did.</summary>
        public static void PalmStar(Vector2 palms, float age, int index = 0)
        {
            float u = age / T.PalmStarLife;
            if (u < 0f || u >= 1f) return;
            Sparkle(palms, 0.3f * (0.6f + 0.4f * u), (1f - u) * (1f - u), 45f * u, Overhead + 0.22f + index * 0.001f);
        }

        // turn is the angle about the card's own upright axis (0 face on, pi back on); tilt turns
        // it on the screen, for a card lying on the floor.
        private static void Card(Vector2 at, float scale, float turn, float tilt, int suit, float alpha, float altitude)
        {
            float c = Mathf.Cos(turn), width = Mathf.Max(0.012f, T.CardWidth * scale * Mathf.Abs(c)), height = T.CardHeight * scale;
            Sprite(at, width + T.Edge, height + T.Edge, Fade(CardInk, alpha * 0.85f), solid, altitude, tilt);
            if (c >= 0f)
            {
                Sprite(at, width, height, Fade(Paper, alpha), solid, altitude + PartStep, tilt);
                Sprite(at, width * 0.62f, height * 0.46f, Fade(suit == T.Heart ? Red : CardInk, alpha), pip[suit], altitude + PartStep * 2f, tilt);
            }
            else
            {
                Sprite(at, width, height, Fade(CardGold, alpha), solid, altitude + PartStep, tilt);
                Sprite(at, width * 0.78f, height * 0.84f, Fade(Red, alpha), solid, altitude + PartStep * 2f, tilt);
            }
        }

        // Two crossed additive slivers over a soft spot.
        private static void Sparkle(Vector2 at, float size, float alpha, float turn, float altitude)
        {
            if (alpha <= 0f) return;
            Sprite(at, size * 1.3f, size * 1.3f, Fade(CardGold, alpha * 0.55f), glow, altitude);
            Sprite(at, size * 2f, size * 0.13f, Fade(GoldPale, alpha), whiteGlow, altitude + 0.0003f, turn);
            Sprite(at, size * 0.13f, size * 2f, Fade(GoldPale, alpha), whiteGlow, altitude + 0.0006f, turn);
        }

        // The red puff: opaque from PuffIn before the contact until Cover after it, then thinning.
        private static void Cover(Vector2 ground, int index, float age)
        {
            float puffAge = age + T.PuffIn;
            if (puffAge < 0f || age >= T.Cover + T.PuffFade) return;
            float alpha = Mathf.Clamp01(puffAge / T.PuffIn) * (1f - Smooth((age - T.Cover) / T.PuffFade)), grow = Smooth(puffAge / 0.5f);
            float altitude = Overhead + 0.10f + index * 0.03f;
            void Blob(float east, float north, float height, float size, Color colour)
            {
                Sprite(T.Above(ground, east, north, height), size * 1.25f, size, Fade(colour, alpha), puff, altitude);
                altitude += 0.0005f;
            }

            // Two wide blobs sit on the pawn itself (ground to head is 0 to 0.75 on screen). The Puff
            // texture is thin at its rim, so they are oversize, and each is drawn twice so the
            // alpha stacks to opaque over the body.
            for (int i = 0; i < 2; i++) Blob(0f, -0.05f, 0.2f, T.Puff * 2.3f * (0.85f + 0.25f * grow), RedDark);
            for (int i = 0; i < 2; i++) Blob(0f, 0f, 0.55f, T.Puff * 2.0f * (0.85f + 0.25f * grow), Red);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + index, d = T.Puff * (0.35f + 0.35f * grow);
                Blob(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.5f, 0.1f + 0.8f * Rand(index * 9 + i) + 0.25f * grow,
                    T.Puff * (0.9f + 0.4f * Rand(index * 9 + i + 3)) * (0.8f + 0.4f * grow), Red);
            }
            for (int i = 0; i < 3; i++) Blob(-0.18f + i * 0.16f, 0f, 0.7f + 0.12f * i + 0.3f * grow, T.Puff * 0.55f * (0.8f + 0.4f * grow), RedLit);
        }
    }
}
