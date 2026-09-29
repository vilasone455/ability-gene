using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.AjinResetDraw;

namespace RimArt
{
    /// <summary>
    /// Picture timings of Satō's Reset, the defaults of the lab sketch (Tools/VfxLab/web/sketches/ajin-reset.js). The
    /// delays themselves (20 s, 6 s for Headshot Reset, one day for the slow reset) are balance and come from the XML.
    /// </summary>
    public static class AjinResetTiming
    {
        /// <summary>The cover grows over the last Rebuild seconds of the delay; after he stands it flakes off over Rise.</summary>
        public const float Rebuild = 1.1f, Rise = 0.9f;
        /// <summary>The timer ring fades over this after he stands.</summary>
        public const float RingFade = 0.3f;
        /// <summary>A piece crumbles to dust in this long.</summary>
        public const float Crumble = 0.5f;
        /// <summary>At an anchor the piece is taken into the shell over this share of Rebuild.</summary>
        public const float TakenIn = 0.4f;
        /// <summary>The seeps' strength ramps up over this at the start of the delay.</summary>
        public const float SeepIn = 0.3f;

        /// <summary>DrawInPlace and DrawAtAnchor draw nothing from this many seconds after the Reset began.</summary>
        public static float End(float delay, float rise) => delay + Mathf.Max(rise, RingFade);
    }

    /// <summary>One frame of the Reset where he fell.</summary>
    public struct ResetInPlaceShot
    {
        /// <summary>The pawn's draw position (Pawn.DrawPos), lying or standing.</summary>
        public Vector3 Body;
        /// <summary>
        /// While lying: the angle PawnRenderer draws the body at (PawnRenderer.BodyAngle, the downed wiggler's
        /// downedAngle), degrees clockwise seen from above; the head points along (sin a, cos a), 270 is head west.
        /// </summary>
        public float BodyAngle;
        /// <summary>He lies (the Reset's delay). In game he stands at once when the delay ends.</summary>
        public bool Lying;
        /// <summary>Seconds since the Reset began.</summary>
        public float Seconds;
        /// <summary>Seconds until he stands: 20 normally, 6 for Headshot Reset, 1000 for the slow one-day reset (XML).</summary>
        public float Delay;
        /// <summary>The cover grows over the last Rebuild seconds of Delay (AjinResetTiming.Rebuild).</summary>
        public float Rebuild;
        /// <summary>After he stands the cover flakes off upward over Rise seconds (AjinResetTiming.Rise).</summary>
        public float Rise;
        /// <summary>Headshot Reset: two temple wounds seep too, and the cover starts at the head.</summary>
        public bool HeadFirst;
        /// <summary>Draw the timer ring, filling over the whole Delay.</summary>
        public bool Timer;
        /// <summary>How many of the three body wound spots to draw (the game passes his injury count, at most 3).</summary>
        public int Wounds;
        /// <summary>
        /// Seconds that drive the flakes' and puffs' cycles; null uses <see cref="Seconds"/>. The previews pass the
        /// sketch's clip time so each flake is where the sketch's is.
        /// </summary>
        public float? Clock;
    }

    /// <summary>One frame of the Reset at an anchor: he rises where a severed piece of him lies.</summary>
    public struct ResetAnchorShot
    {
        /// <summary>The piece's position, which is where he stands when the delay ends (his draw position then).</summary>
        public Vector3 Anchor;
        public AjinPiece Piece;
        /// <summary>The way the piece points, degrees anticlockwise from east (see AjinResetGraphics.DrawPiece).</summary>
        public float PieceAngle;
        /// <summary>Seconds since the Reset began.</summary>
        public float Seconds;
        /// <summary>Seconds until he stands at the anchor (XML).</summary>
        public float Delay;
        /// <summary>The shell builds up over the last Rebuild seconds of Delay; after he stands it peels off over Rise.</summary>
        public float Rebuild, Rise;
        /// <summary>Draw the timer ring round the piece, filling over the whole Delay.</summary>
        public bool Timer;
        /// <summary>
        /// The sun for the growing shell's shadow: GenCelestial's shadow vector times SixPathsSlamGraphics.SunScale, and
        /// 0.32 x GenCelestial.CurShadowStrength (0 draws no shadow).
        /// </summary>
        public Vector2 Sun;
        public float Shadow;
        /// <summary>Seconds that drive the flakes' cycles; null uses <see cref="Seconds"/> (see ResetInPlaceShot.Clock).</summary>
        public float? Clock;
    }

    /// <summary>
    /// Satō's Reset pictures, the port of ajin-reset.js (both scenarios) and of the lying half of
    /// ajin-headshot-reset.js. The game draws the real pawn, lying or standing; these draw only what lies on him or
    /// round him: black matter seeping from his wounds or from the anchor piece, the timer ring, the cover growing over
    /// the lying body (head first for Headshot Reset) and flaking off once he stands, the shell building up at an
    /// anchor and peeling from the head down, the severed pieces and their crumbling.
    ///
    /// Lying overlays are written in the lab's downed layout (head west, body angle 270) round the body's draw
    /// position and turned by the real body angle, so they stay on the body however PawnRenderer lays it. Standing
    /// overlays are the sketch's offsets from the draw position. In game he stands up at once when the delay ends, so
    /// the sketch's lying-to-standing blend is not drawn: from Delay on the cover peels off the standing pawn.
    ///
    /// Not ported: the stand-in pawns, the blood pool under him, and the scorch, blood and dropped gear where the body
    /// was blown apart (vanilla filth and his real gear), and the blood under the pieces (vanilla filth).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AjinResetGraphics
    {
        // The lab's stand-in at real size (lib/ajin.js standInBlend): body and head ovals from the draw position,
        // standing, and lying in the downed layout round the lying body's centre.
        private static readonly Oval StandingBody = new Oval(0f, -0.096f, 0.286f, 0.416f), StandingHead = new Oval(0f, 0.424f, 0.208f, 0.221f);
        private static readonly Oval LyingBody = new Oval(0.065f, -0.01f, 0.416f, 0.26f), LyingHead = new Oval(-0.39f, 0.01f, 0.208f, 0.208f);
        // Wound spots in the downed layout. ajin-reset.js: three on the body. ajin-headshot-reset.js: three on the body,
        // the two from the enemy's shots first (so two spots are the "after Sever" scene), and both temples.
        private static readonly Vector2[] ResetWounds = { new Vector2(-0.055f, 0.03f), new Vector2(0.225f, -0.06f), new Vector2(0.365f, 0.07f) };
        private static readonly Vector2[] HeadshotWounds = { new Vector2(0.1378f, 0.042f), new Vector2(0.0592f, -0.0035f), new Vector2(-0.0224f, -0.0685f) };
        private static readonly Vector2[] TempleWounds = { new Vector2(-0.39f, 0.11f), new Vector2(-0.39f, -0.09f) };
        // The timer ring's centre: lying, from the lying body's centre; standing, from the draw position.
        private static readonly Vector2 LyingRing = new Vector2(0f, 0.02f), StandingRing = new Vector2(0f, -0.15f);
        private const float InPlaceRing = 0.70f, AnchorRing = 0.60f, AnchorRingDrop = 0.1f;
        // The sketches' seep keys' lengths ("reset wound 0", "hs wound 0", "hs head 0", "reset hand").
        private const int ResetWoundSeed = 13, HeadshotWoundSeed = 10, TempleSeed = 9, PieceSeed = 10;

        // The pieces' sizes and Satō's costume (combat look): white shirt, sleeves rolled above the elbow, dark
        // trousers, brown shoes.
        private const float ArmLength = 0.332f, ArmWidth = 0.085f, LegLength = 0.46f, LegWidth = 0.10f;
        private static readonly Color Cuff = new Color(0.74f, 0.74f, 0.71f), Trousers = new Color(0.21f, 0.22f, 0.19f),
            Shoe = new Color(0.36f, 0.24f, 0.14f), SkinShade = new Color(0.66f, 0.54f, 0.40f);
        private static readonly Mesh anchorRing = Ring(0.93f, "Ajin anchor ring");

        /// <summary>
        /// One of Satō's severed parts lying on the floor at item height. <paramref name="angleDeg"/> is the way it points,
        /// degrees anticlockwise from east: a hand's fingers, a finger's tip, an arm's and a leg's far end (hand, foot),
        /// an ear's long axis. The cut end is the other end. Body draws nothing: the body is the real pawn.
        /// </summary>
        public static void DrawPiece(Vector3 center, AjinPiece piece, float angleDeg, float alpha = 1f)
        {
            if (alpha <= 0.001f || piece == AjinPiece.Body) return;
            var c = new Vector2(center.x, center.z);
            Begin(c);
            float layer = ItemLayer;
            Vector2 d = Turn(angleDeg), n = new Vector2(-d.y, d.x);
            switch (piece)
            {
                case AjinPiece.Hand:
                    Hand(c, angleDeg, layer, alpha);
                    break;
                case AjinPiece.Finger:
                    // ajin-reset.js: a skin oval 0.09 long and a red stump at the cut end.
                    DrawMesh(disc, c, layer, 0.045f, 0.016f, -angleDeg, Fade(Skin, alpha), solid);
                    DrawMesh(disc, c - d * 0.04f, layer + 0.001f, 0.018f, 0.016f, -angleDeg, Fade(Blood, alpha), solid);
                    break;
                case AjinPiece.Arm:
                {
                    // The sketch's arm (ajin-headshot-reset.js "after Sever": 0.33 long, 0.085 wide, outlined) in his
                    // shirt: sleeve from the shoulder, the rolled cuff above the elbow, bare forearm and hand.
                    Vector2 cut = c - d * (ArmLength / 2f), tip = c + d * (ArmLength / 2f);
                    Vector2 At(float u) => Vector2.LerpUnclamped(cut, tip, u);
                    Bar(cut, tip, ArmWidth + 0.03f, Fade(Outline, alpha), layer);
                    Bar(cut, At(0.40f), ArmWidth, Fade(Shirt, alpha), layer + 0.00002f);
                    Bar(At(0.40f), tip, ArmWidth * 0.9f, Fade(Skin, alpha), layer + 0.00004f);
                    Bar(At(0.34f), At(0.46f), ArmWidth + 0.02f, Fade(Cuff, alpha), layer + 0.00006f);
                    DrawMesh(disc, cut, layer + 0.0001f, 0.045f, 0.045f, 0f, Fade(Blood, alpha), solid);
                    break;
                }
                case AjinPiece.Leg:
                {
                    // Trouser leg from the hip to the ankle, a brown shoe turned sideways at the end, the red cut at the hip.
                    Vector2 cut = c - d * (LegLength / 2f), tip = c + d * (LegLength / 2f);
                    Vector2 ankle = tip - d * 0.06f, shoe = tip - d * 0.035f + n * 0.035f;
                    Bar(cut, ankle, LegWidth + 0.03f, Fade(Outline, alpha), layer);
                    Bar(cut, ankle, LegWidth, Fade(Trousers, alpha), layer + 0.00002f);
                    DrawMesh(disc, shoe, layer + 0.00004f, 0.09f, 0.06f, -(angleDeg + 90f), Fade(Outline, alpha), solid);
                    DrawMesh(disc, shoe, layer + 0.00006f, 0.075f, 0.045f, -(angleDeg + 90f), Fade(Shoe, alpha), solid);
                    DrawMesh(disc, cut, layer + 0.0001f, 0.055f, 0.055f, 0f, Fade(Blood, alpha), solid);
                    break;
                }
                case AjinPiece.Ear:
                    // A skin oval 0.10 long with a darker rim and inner fold, the red cut along one side.
                    DrawMesh(disc, c, layer, 0.05f, 0.034f, -angleDeg, Fade(SkinShade, alpha), solid);
                    DrawMesh(disc, c + n * 0.003f, layer + 0.00002f, 0.043f, 0.028f, -angleDeg, Fade(Skin, alpha), solid);
                    DrawMesh(disc, c + n * 0.006f + d * 0.004f, layer + 0.00004f, 0.022f, 0.011f, -angleDeg, Fade(SkinShade, alpha), solid);
                    DrawMesh(disc, c - n * 0.026f, layer + 0.00006f, 0.034f, 0.008f, -angleDeg, Fade(Blood, alpha), solid);
                    break;
            }
        }

        /// <summary>The ring round an anchor on the floor (ajin-headshot-reset.js "after Sever"): radius 0.40, pale.</summary>
        public static void DrawAnchorRing(Vector3 center, float alpha) =>
            DrawMesh(anchorRing, new Vector2(center.x, center.z), Floor + 0.02f, 0.40f, 0.40f, 0f, Fade(Slash, 0.45f * alpha), solid);

        /// <summary>
        /// The piece crumbling to dust over AjinResetTiming.Crumble (0.5 s) from <paramref name="seconds"/> 0: it fades
        /// while skin-coloured puffs spread and rise (the sketch's finger, spread along bigger pieces). Nothing after it ends.
        /// </summary>
        public static void DrawCrumble(Vector3 center, AjinPiece piece, float angleDeg, float seconds)
        {
            float u = seconds / AjinResetTiming.Crumble;
            if (u < 0f || u >= 1f || piece == AjinPiece.Body) return;
            DrawPiece(center, piece, angleDeg, 1f - u);
            // A finger's dust is the sketch's; a bigger piece throws more, spread along its length and wider.
            float length = Length(piece), g = Mathf.Sqrt(length / Length(AjinPiece.Finger));
            int count = Round(6f * g);
            var c = new Vector2(center.x, center.z);
            Vector2 d = Turn(angleDeg);
            for (int i = 0; i < count; i++)
            {
                float a = R(i + 70) * Mathf.PI * 2f;
                Vector2 from = c + d * ((R(i + 80) - 0.5f) * (length - Length(AjinPiece.Finger)));
                var at = new Vector2(from.x + Mathf.Cos(a) * u * 0.2f * g, from.y + Mathf.Sin(a) * u * 0.12f * g + u * 0.1f);
                Sprite(at, (0.08f + u * 0.1f) * g, (0.06f + u * 0.08f) * g, Fade(Skin, (1f - u) * 0.45f), Puff, Overhead + 0.01f + i * 0.0003f);
            }
        }

        private static float Length(AjinPiece piece)
        {
            switch (piece)
            {
                case AjinPiece.Leg: return LegLength;
                case AjinPiece.Arm: return ArmLength;
                case AjinPiece.Hand: return 0.2f;
                case AjinPiece.Ear: return 0.1f;
                default: return 0.09f;
            }
        }

        /// <summary>
        /// The Reset where he fell. While he lies: black matter seeps from the wounds (and both temples for Headshot
        /// Reset), the timer ring fills over Delay, and over the last Rebuild seconds the cover grows over him (head
        /// first for Headshot Reset) while the wounds close under it. From Delay he stands and the cover flakes off upward
        /// over Rise; the ring fades over 0.3 s.
        /// </summary>
        public static void DrawInPlace(ResetInPlaceShot s)
        {
            float t = s.Seconds;
            if (t < 0f || t >= AjinResetTiming.End(s.Delay, s.Rise)) return;
            float clock = s.Clock ?? t, buildAt = s.Delay - s.Rebuild;
            float kBuild = Mathf.Clamp01((t - buildAt) / s.Rebuild), kRise = Mathf.Clamp01((t - s.Delay) / s.Rise);
            bool waiting = t < s.Delay;
            var body = new Vector2(s.Body.x, s.Body.z);
            Begin(body);
            AjinFrame f = s.Lying ? AjinFrame.Lying(s.Body, s.BodyAngle) : AjinFrame.Standing(s.Body);
            Oval B = s.Lying ? LyingBody : StandingBody, H = s.Lying ? LyingHead : StandingHead;
            float layer = s.Lying ? OnLyingPawn : OnPawn;

            // Wounds on the lying body, closing under the cover; black matter seeps from them until he stands.
            if (s.Lying)
            {
                float healed = Smooth(kBuild * 1.4f), shrink = 1f - healed * 0.9f;
                Vector2[] spots = s.HeadFirst ? HeadshotWounds : ResetWounds;
                int count = Mathf.Clamp(s.Wounds, 0, spots.Length);
                for (int i = 0; i < count; i++)
                {
                    Vector2 at = f.P(spots[i]);
                    DrawMesh(disc, at, layer + 0.0005f + i * 0.00002f, 0.06f * shrink, 0.045f * shrink, f.turn, Fade(Blood, 1f - healed), solid);
                    if (waiting)
                        Seep(at, clock, Mathf.Min(1f, t / AjinResetTiming.SeepIn) * (1f - kBuild * 0.7f), 1f, s.HeadFirst ? HeadshotWoundSeed : ResetWoundSeed);
                }
                if (s.HeadFirst)
                    for (int i = 0; i < TempleWounds.Length; i++)
                    {
                        Vector2 at = f.P(TempleWounds[i]);
                        DrawMesh(disc, at, layer + 0.0006f + i * 0.00002f, 0.05f * shrink, 0.04f * shrink, f.turn, Fade(Blood, 1f - healed), solid);
                        if (waiting) Seep(at, clock, Mathf.Min(1f, t / 0.25f) * (1f - kBuild * 0.6f) * 1.3f, 1f, TempleSeed);
                    }
            }

            // The cover: grows over the last Rebuild seconds (from 60 % of its size), flakes off as he stands.
            float off = 1f - Smooth(kRise * 1.15f), grow = 0.6f + 0.4f * Smooth(kBuild);
            float bodyCover = s.HeadFirst ? Smooth((kBuild - 0.3f) / 0.7f) * off : Smooth(kBuild) * off;
            float headCover = s.HeadFirst ? Smooth(kBuild * 1.6f) * off : bodyCover;
            Cover(f, B, H, s.Lying ? 0f : 1f, bodyCover, headCover, grow, layer + 0.0025f);
            if (!waiting && kRise < 1f)
            {
                Vector2 b = f.P(B.c);
                EdgeFlakes(new Vector2(b.x, b.y - 0.2f), 0.1f + Smooth(kRise) * 0.6f, 0.6f, clock, 1.6f, 0.8f, Overhead + 0.03f, 1f - kRise * 0.6f);
            }

            if (s.Timer && t < s.Delay + AjinResetTiming.RingFade)
                TimerRing(body + (s.Lying ? LyingRing : StandingRing), InPlaceRing, Mathf.Clamp01(t / s.Delay),
                    1f - Smooth((t - s.Delay) / AjinResetTiming.RingFade));
        }

        /// <summary>
        /// The Reset at an anchor. During the delay black matter seeps from the piece, which twitches, and the timer
        /// ring fills round it. Over the last Rebuild seconds a dark shell in his shape builds up from the floor over the
        /// piece, flakes thrown off its rising edge and ooze strands pouring from the piece into it; the piece is taken in
        /// over the first 40 % of Rebuild. From Delay the real (naked) pawn stands there and the shell peels from the head
        /// down over Rise. The piece is drawn by this call from Seconds 0 until the shell takes it in.
        /// </summary>
        public static void DrawAtAnchor(ResetAnchorShot s)
        {
            float t = s.Seconds;
            if (t < 0f || t >= AjinResetTiming.End(s.Delay, s.Rise)) return;
            float clock = s.Clock ?? t, buildAt = s.Delay - s.Rebuild;
            float kDelay = buildAt > 0f ? Mathf.Clamp01(t / buildAt) : 1f;
            float kBuild = Mathf.Clamp01((t - buildAt) / s.Rebuild), kRise = Mathf.Clamp01((t - s.Delay) / s.Rise);
            var a = new Vector2(s.Anchor.x, s.Anchor.z);
            bool building = t < s.Delay;

            float gone = Smooth(kBuild / AjinResetTiming.TakenIn);
            if (gone < 1f)
                DrawPiece(new Vector3(a.x + Mathf.Sin(clock * 40f) * 0.006f * kDelay, s.Anchor.y, a.y), s.Piece, s.PieceAngle, 1f - gone);
            Begin(a);
            if (t < buildAt + s.Rebuild * 0.5f)
                Seep(a, clock, Mathf.Min(1f, t / AjinResetTiming.SeepIn) * (1f + kBuild), 1f, PieceSeed);

            float full = ShellHi - ShellLo, buildHi = ShellLo + full * Smooth(kBuild), peelHi = ShellHi - full * Smooth(kRise);
            if (t > buildAt)
            {
                float hi = building ? buildHi : peelHi;
                if (building && s.Shadow > 0f)
                    Sprite(new Vector2(a.x + s.Sun.x * 0.5f, a.y - 0.33f + 0.05f + s.Sun.y * 0.5f), 0.95f, 0.42f,
                        Fade(GhostEdge, s.Shadow * 1.4f * Smooth(kBuild)), Puff, Floor + 0.03f);
                Shell(a, hi, OnPawn + 0.001f);
                if (hi > ShellLo + 0.02f && hi < ShellHi - 0.01f)
                    EdgeFlakes(a, hi / Stand, 0.55f, clock, 1.2f, building ? 0.45f : 0.8f, Overhead + 0.03f);
                if (building)
                    Strands(a, a.y + buildHi - 0.02f, clock, 1f - Smooth((t - buildAt - s.Rebuild * 0.6f) / (s.Rebuild * 0.4f)), OnPawn + 0.002f);
            }

            if (s.Timer && t < s.Delay + AjinResetTiming.RingFade)
                TimerRing(new Vector2(a.x, a.y - AnchorRingDrop), AnchorRing, Mathf.Clamp01(t / s.Delay),
                    1f - Smooth((t - s.Delay) / AjinResetTiming.RingFade));
        }
    }
}
