using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.BlackGhostMatter;

namespace RimArt
{
    /// <summary>
    /// The torn enemy at one moment of a Tear: what the game applies to the real enemy pawn. Offsets are on the map, x
    /// east and z north (screen up); the lift is drawn as +z.
    /// </summary>
    public struct TearEnemyPose
    {
        /// <summary>Draw offset for the enemy pawn: along the ghost's direction (pulled 0.06 in, shaken, the stagger or limp) plus the lift, minus the leg's sink.</summary>
        public Vector3 Offset;
        /// <summary>Draw tilt in degrees, clockwise seen from above as the game turns a pawn: up to 6 while it struggles, 20 toward the missing side after a leg.</summary>
        public float Angle;
        /// <summary>Cells of height inside Offset: up to <see cref="TearGraphics.LiftHeight"/>, plus the limp's 0.04 bob.</summary>
        public float Lift;
        /// <summary>Offset along the floor only, without the lift and sink: where its shadow stays.</summary>
        public Vector3 FloorOffset;
        /// <summary>The shadow's size and strength factors: 1 on the floor, 0.75 and 0.65 at the top of the lift.</summary>
        public float ShadowScale, ShadowAlpha;
        /// <summary>True from the grab until the ghost lets go (<see cref="TearGraphics.DropAt"/>).</summary>
        public bool Held;
        internal float Along, Sink;
    }

    /// <summary>What <see cref="TearGraphics.DrawTear"/> needs.</summary>
    public struct TearShot
    {
        /// <summary>
        /// The ghost's shot this frame, the same one passed to <see cref="BlackGhostGraphics.DrawGhost"/>: its TearSeconds
        /// (negative draws nothing), TearTarget and TearArm say where the tear is, the rest where the ghost's claws are.
        /// </summary>
        public BlackGhostShot Ghost;
    }

    /// <summary>
    /// Tear, the Black Ghost's order: the port of the tear scenario of Tools/VfxLab/web/sketches/ajin-black-ghost-v2.js.
    /// Seconds after the grab: grab 0-0.3 (in the side views the ghost steps into the target's cell, a draw offset, and
    /// both claws close on the body); lift 0.3-0.6 (0.35 cells, the jaw opens); shake to 1.0 (it struggles, the ghost
    /// leans back 0.12); rip at 1.0 (the near claw rakes down the side, six streaks, blood bursts, the camera shakes 0.07,
    /// the limb comes off in the claw); throw 1.0-1.5 (the claw swings up and back and lets go at 1.25, the limb lands
    /// about a cell behind the ghost at 1.65); the ghost steps back to its own cell by 1.9. The enemy is let go at 1.05
    /// and drops in 0.2 s. Arm: it lands on its feet and staggers back 0.2 cells in 0.35 s. Leg: it lands tilted 20
    /// degrees toward the missing side and limps 0.3 cells in 1.5 s, to 2.75. The weapon drop is the game's (the real
    /// weapon), not drawn here. Positions in, drawing out; no map access.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TearGraphics
    {
        /// <summary>The beats (s) and the lift (cells), the sketch's defaults.</summary>
        public const float Grab = 0.3f, Lift = 0.3f, Hold = 1.0f, Throw = 0.5f, Release = 0.25f, Drop = 0.2f, LiftHeight = 0.35f;
        /// <summary>Fully lifted; the struggle is at full strength from here.</summary>
        public const float LiftEnds = Grab + Lift;
        /// <summary>The limb comes off: the game removes the body part now, and shakes the camera by RipShake.</summary>
        public const float RipAt = Hold, RipShake = 0.07f;
        /// <summary>The claw lets go of the limb; it flies for 0.4 s and lands at LimbLandsAt, where the game puts the piece on the floor.</summary>
        public const float LetGoAt = RipAt + Release, LimbLandsAt = LetGoAt + Flight;
        /// <summary>The enemy is let go at DropAt and is on its feet at DroppedAt.</summary>
        public const float DropAt = RipAt + 0.05f, DroppedAt = DropAt + Drop;
        /// <summary>The throw ends; the ghost steps back to its own cell over StepBack.</summary>
        public const float ThrowEnds = RipAt + Throw, StepBack = 0.4f;
        /// <summary>After the drop: an arm's stagger (0.2 cells in 0.35 s), a leg's limp (0.3 cells in 1.5 s).</summary>
        public const float Stagger = 0.35f, Limp = 1.5f;
        /// <summary>The whole Tear, to the end of a leg's limp. The blood pool has grown by 2.7 s.</summary>
        public const float Duration = DroppedAt + Limp;
        /// <summary>The angle (degrees on screen, 0 east) the landed piece lies at; pass it to <see cref="DrawTornPiece"/>.</summary>
        public const float TornPieceAngle = 20f;

        private const float Flight = 0.4f, LeanBack = 0.12f;
        // How far short of the target's feet the ghost is drawn at the grab: 0.2 cells in the side views (the enemy in
        // front of its body), 0.6 facing north or south (the sketch's spacing there; in game it is 1 cell away).
        private const float InSide = 0.2f, InFrontBack = 0.6f;
        private const float ArmLen = 0.40f, ArmW = 0.10f, LegLen = 0.38f, LegW = 0.12f;
        // Denim trousers and brown boots, so a torn leg reads on brown ground.
        private static readonly Color Pants = new Color(0.28f, 0.33f, 0.45f), Boot = new Color(0.18f, 0.14f, 0.11f);

        /// <summary>Everything the ghost and the tear draw at one moment, from the two positions.</summary>
        internal struct TearFrame
        {
            /// <summary>Unit direction ghost to enemy; true when that is straight north or south (the front and back views).</summary>
            internal Vector2 D;
            internal bool NS;
            /// <summary>The torn side on screen: +1 east (facing east, north or south), -1 west.</summary>
            internal float Sx;
            internal Vector2 E0, G, TearO, HoldO, A0, Away;
            internal TearEnemyPose E, ER;
        }

        private static Vector2 Dir(Vector2 ghost, Vector2 enemy)
        {
            Vector2 d = enemy - ghost;
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.right;
        }

        /// <summary>
        /// The enemy pawn's draw offset and tilt <paramref name="seconds"/> after the grab (negative: none).
        /// <paramref name="ghostToEnemy"/> is the enemy's DrawPos minus the ghost's.
        /// </summary>
        public static TearEnemyPose Enemy(float seconds, bool arm, Vector3 ghostToEnemy)
        {
            Vector2 d = Dir(Vector2.zero, new Vector2(ghostToEnemy.x, ghostToEnemy.z));
            return Pose(seconds, arm, d, Mathf.Abs(d.x) > 0.2f ? Mathf.Sign(d.x) : 1f);
        }

        internal static TearEnemyPose Pose(float t, bool arm, Vector2 d, float sx)
        {
            float liftK = Smooth((t - Grab) / Lift), fall = Mathf.Clamp01((t - DropAt) / Drop);
            bool held = t >= 0f && t < DropAt;
            float shake = held ? (t < LiftEnds ? 0.4f * liftK : 1f) : 0f;
            float lift = t < DropAt ? LiftHeight * liftK : LiftHeight * (1f - fall * fall), sink = 0f, rot = shake * Mathf.Sin(t * 23f) * 6f;
            // Pulled toward the ghost while held, and struggling.
            float along = (held ? -0.06f * Smooth(t / Grab) : 0f) + shake * Mathf.Sin(t * 70f) * 0.02f;
            float after = t - DroppedAt;
            if (arm && after > 0f) along += 0.2f * Smooth(after / Stagger);
            if (!arm && t >= DropAt)
            {
                // Lands tilted to the missing side, then limps.
                float k = Smooth(fall);
                rot = 20f * sx * k;
                sink = 0.08f * k;
                if (after > 0f)
                {
                    float u = Mathf.Clamp01(after / Limp);
                    along += 0.3f * u;
                    lift += u < 1f ? 0.04f * Mathf.Abs(Mathf.Sin(after * 9f)) : 0f;
                }
            }
            float lk = lift / LiftHeight;
            return new TearEnemyPose
            {
                Offset = new Vector3(d.x * along, 0f, d.y * along + lift - sink),
                FloorOffset = new Vector3(d.x * along, 0f, d.y * along),
                Angle = rot, Lift = lift, ShadowScale = 1f - 0.25f * lk, ShadowAlpha = 1f - 0.35f * lk, Held = held, Along = along, Sink = sink,
            };
        }

        /// <summary>A point on the enemy, given from its DrawPos with x toward the torn side, turned and lifted with it.</summary>
        internal static Vector2 OnEnemy(Vector2 g, in TearEnemyPose e, Vector2 o)
        {
            // A screen offset turned clockwise by the angle, the way a draw angle turns a sprite.
            float r = e.Angle * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(g.x + o.x * c + o.y * s, g.y - o.x * s + o.y * c + e.Lift - e.Sink);
        }

        internal static TearFrame Frame(Vector2 ghost, Vector2 enemy, float t, bool arm)
        {
            var f = new TearFrame { D = Dir(ghost, enemy), E0 = enemy };
            f.NS = Mathf.Abs(f.D.x) <= 0.2f;
            f.Sx = f.NS ? 1f : Mathf.Sign(f.D.x);
            f.E = Pose(t, arm, f.D, f.Sx);
            f.ER = Pose(RipAt, arm, f.D, f.Sx);
            f.G = enemy + f.D * f.E.Along;
            // Where the limb leaves the body (the far side in the side views, east from north or south) and where the
            // holding claw grips (the west side in the front and back views).
            f.TearO = arm ? new Vector2(0.26f * f.Sx, 0.12f) : new Vector2(0.13f * f.Sx, -0.44f);
            f.HoldO = f.NS ? new Vector2(-0.27f, 0.08f) : arm ? new Vector2(0.12f * f.Sx, 0.16f) : new Vector2(0.20f * f.Sx, 0.14f);
            f.A0 = OnEnemy(enemy + f.D * f.ER.Along, f.ER, f.TearO);
            f.Away = new Vector2(-f.D.x + (f.NS ? 0.3f : 0f), -f.D.y * 0.8f + 0.25f).normalized;
            return f;
        }

        /// <summary>The tearing claw after the rip: yanked 0.45 cells back past the ghost in 0.12 s, then swung up (0.7) and over for the throw.</summary>
        internal static Vector2 Hand(float t, in TearFrame f)
        {
            float y = Smooth((t - RipAt) / 0.12f), v = Mathf.Clamp01((t - RipAt - 0.12f) / (Throw - 0.12f)), k = 0.45f * y + 0.15f * v;
            return new Vector2(f.A0.x + f.Away.x * k, f.A0.y + f.Away.y * k + 0.7f * Mathf.Sin(v * Mathf.PI));
        }

        /// <summary>How far each claw has closed: the holding one lets go at the drop (0.25 s), the tearing one after the throw (0.3 s).</summary>
        internal static void Hands(float t, out float kh, out float kt)
        {
            float kIn = Smooth(t / Grab);
            kh = t < DropAt ? kIn : 1f - Smooth((t - DropAt) / 0.25f);
            kt = t < ThrowEnds ? kIn : 1f - Smooth((t - ThrowEnds) / 0.3f);
        }

        /// <summary>
        /// Where the ghost is drawn from its own position <paramref name="ghost"/>: into the target's cell over the grab
        /// (short of its feet by 0.2, or 0.6 facing north or south), leaning back 0.12 while it lifts and shakes, back out
        /// over 0.4 s after the throw. The cells stepped (for the stride) and how much it walks while stepping.
        /// </summary>
        internal static void Step(Vector2 ghost, Vector2 enemy, float t, out Vector2 at, out float stepCells, out float stepWalk)
        {
            Vector2 d = Dir(ghost, enemy), inStop = enemy - d * (Mathf.Abs(d.x) <= 0.2f ? InFrontBack : InSide);
            float back = t < RipAt ? LeanBack * Smooth((t - Grab) / (RipAt - Grab)) : LeanBack * (1f - Smooth((t - RipAt) / StepBack));
            float into = t < ThrowEnds ? Smooth(t / Grab) : 1f - Smooth((t - ThrowEnds) / StepBack);
            float stepping = t < ThrowEnds ? Mathf.Clamp01(t / Grab) : Mathf.Clamp01((t - ThrowEnds) / StepBack), stepLen = (inStop - ghost).magnitude;
            stepCells = stepLen * (t < ThrowEnds ? into : 2f - into);
            at = Vector2.Lerp(ghost, inStop, into) - d * back;
            stepWalk = stepLen > 0.1f ? Bump(stepping) * 0.8f : 0f;
        }

        /// <summary>
        /// The ghost's hands and jaw for the Tear: both wrists to their spots on the enemy (in the figure's (u, h) from
        /// <paramref name="feet"/>), the tearing one following the limb after the rip; the jaw opens to 0.45 over the lift,
        /// wide at the rip, and shuts 0.35-0.65 s after it; no tongue.
        /// </summary>
        internal static void Grip(Vector2 ghost, Vector2 enemy, float t, bool arm, Vector2 feet, float mirror, ref BlackGhostMotion m)
        {
            TearFrame f = Frame(ghost, enemy, t, arm);
            Hands(t, out m.Kh, out m.Kt);
            Vector2 tearQ = t >= RipAt ? Hand(t, f) : OnEnemy(f.G, f.E, f.TearO), holdQ = OnEnemy(f.G, f.E, f.HoldO);
            m.Grip = true;
            m.Hold = new Vector2((holdQ.x - feet.x) / mirror, (holdQ.y - feet.y) / Stand);
            m.Tear = new Vector2((tearQ.x - feet.x) / mirror, (tearQ.y - feet.y) / Stand);
            m.Jaw = t >= RipAt ? 1f - Smooth((t - RipAt - 0.35f) / 0.3f) : 0.45f * Smooth((t - Grab) / Lift);
            m.Tongue = 0f;
        }

        /// <summary>Where the thrown limb lands: 0.9 cells behind the ghost and 0.5 to its left (0.9 facing north or south).</summary>
        public static Vector3 LimbLanding(Vector3 ghostPos, Vector3 enemyPos)
        {
            Vector2 land = Landing(new Vector2(ghostPos.x, ghostPos.z), new Vector2(enemyPos.x, enemyPos.z));
            return new Vector3(land.x, ghostPos.y, land.y);
        }

        private static Vector2 Landing(Vector2 ghost, Vector2 enemy)
        {
            Vector2 d = Dir(ghost, enemy);
            var left = new Vector2(-d.y, d.x);
            return ghost - d * 0.9f + left * (Mathf.Abs(d.x) <= 0.2f ? 0.9f : 0.5f);
        }

        /// <summary>
        /// What Tear draws besides the ghost: from the rip, six claw streaks raking down the side (fade by 0.5 s), a burst of
        /// 16 blood drops from where the limb left, the stump spurting 8 times, a blood pool growing under the enemy from
        /// the drop over 1.5 s, drops along a leg's limp; the torn limb hanging from the claw until the let-go and
        /// tumbling through the air (540 degrees) until it lands; and the gripping claws drawn again over the enemy. The
        /// landed limb is <see cref="DrawTornPiece"/>'s, from <see cref="LimbLandsAt"/>. Call it every frame the ghost is
        /// drawn with a tear; the pool and drops show only while it is called.
        /// </summary>
        public static void DrawTear(TearShot s)
        {
            BlackGhostShot g = s.Ghost;
            float t = g.TearSeconds;
            if (t < 0f) return;
            Reset();
            var ghost = new Vector2(g.Pos.x, g.Pos.z);
            var e0 = new Vector2(g.TearTarget.x, g.TearTarget.z);
            Begin(e0);
            bool arm = g.TearArm, ripped = t >= RipAt;
            TearFrame f = Frame(ghost, e0, t, arm);
            bool shown = BlackGhostGraphics.Joints(g, out bool dissolving);

            if (ripped)
            {
                float age = t - RipAt;
                Vector2 st = OnEnemy(f.G, f.E, f.TearO);
                // Six claw streaks raking down the side at the rip.
                Vector2 v = new Vector2(f.Away.x, f.Away.y - 0.9f).normalized, n = new Vector2(-v.y, v.x);
                float grow = Mathf.Clamp01((age + 0.02f) / 0.08f), fade = 1f - Smooth((age - 0.1f) / 0.4f);
                if (fade > 0f)
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 a = f.A0 + n * ((i - 2.5f) * 0.045f) - v * 0.2f;
                        Vector2[] pts = Buf(3);
                        pts[0] = a; pts[1] = a + v * (0.25f * grow); pts[2] = a + v * (0.5f * grow);
                        Trail(pts, 0.028f, Fade(Slash, 0.95f * fade), Overhead + 0.06f + i * 0.0004f);
                    }
                // Blood: a burst from where the limb left, the stump spurting for about 1.3 s, a pool that grows where it
                // was torn, and for a leg drops along the limp.
                float awayAngle = Mathf.Atan2(f.Away.y, f.Away.x);
                for (int i = 0; i < 16; i++)
                {
                    float life = 0.35f + R(i + 400) * 0.3f, u = age / life;
                    if (u > 1f) continue;
                    float th = awayAngle + (R(i + 410) - 0.5f) * 1.8f, r = u * (0.25f + R(i + 420) * 0.5f);
                    Disc(new Vector2(f.A0.x + Mathf.Cos(th) * r, f.A0.y + Mathf.Sin(th) * r * 0.7f + 0.3f * Mathf.Sin(u * Mathf.PI)),
                        Overhead + 0.05f + i * 0.0002f, 0.035f, 0.025f, 0f, Fade(Blood, 1f - u * 0.4f));
                }
                for (int k = 0; k < 8; k++)
                {
                    float u = (age - 0.1f - k * 0.13f) / 0.3f;
                    if (u < 0f || u > 1f) continue;
                    Disc(new Vector2(st.x + f.Sx * u * 0.16f, st.y + 0.1f * Mathf.Sin(u * Mathf.PI) - u * 0.12f), Overhead + 0.045f,
                        0.025f, 0.02f, 0f, Fade(Blood, 1f - u));
                }
                float pg = Smooth((age - Drop) / 1.5f);
                Disc(new Vector2(e0.x + f.Sx * 0.12f, e0.y - 0.36f), Floor + 0.006f, 0.28f * pg + 0.04f, 0.14f * pg + 0.03f, 0f, Fade(Blood, 0.8f));
                if (!arm)
                    for (int k = 0; k < 6; k++)
                    {
                        Vector2 at = e0 + f.D * (0.3f * (k + 0.5f) / 6f);
                        if (age - 0.05f - Drop - 1.5f * (k + 0.5f) / 6f > 0f)
                            Disc(new Vector2(at.x + f.Sx * 0.1f + (R(k + 430) - 0.5f) * 0.08f, at.y - 0.38f), Floor + 0.007f, 0.05f, 0.03f, 0f, Fade(Blood, 0.8f));
                    }

                // The torn piece: in the claw, hanging from its torn end; thrown at the let-go, tumbling until it lands.
                if (t < LimbLandsAt)
                {
                    float len = arm ? ArmLen : LegLen, w = arm ? ArmW : LegW;
                    Color col = arm ? BlackGhostMatter.Enemy : Pants, tip = arm ? Skin : Boot;
                    if (t < LetGoAt)
                    {
                        Vector2 c = shown ? BlackGhostFigure.Wrists[1] : Hand(t, f), hang = new Vector2(f.Sx * 0.35f, -1f).normalized;
                        LimbSeg(c, c + hang * len, w, col, tip, Overhead + 0.01f);
                        Disc(c, Overhead + 0.012f, 0.05f, 0.04f, 0f, Blood);
                    }
                    else
                    {
                        Vector2 from = Hand(LetGoAt, f), land = Landing(ghost, e0);
                        float u = Mathf.Clamp01((t - LetGoAt) / Flight), rd = ((1f - u) * 540f + TornPieceAngle) * Mathf.Deg2Rad;
                        Vector2 c = Vector2.Lerp(from, land, u) + new Vector2(0f, 0.6f * Mathf.Sin(u * Mathf.PI));
                        var half = new Vector2(Mathf.Cos(rd) * len / 2f, Mathf.Sin(rd) * len / 2f * 0.6f);
                        LimbSeg(c - half, c + half, w, col, tip, Overhead + 0.01f);
                        Disc(c - half, Overhead + 0.012f, 0.045f, 0.035f, 0f, Blood);
                    }
                }
            }

            // The claws that grip are drawn again over the enemy (and over the torn piece they hold).
            if (shown && !dissolving)
            {
                Hands(t, out float kh, out float kt);
                Alt = Overhead + 0.014f;
                if (kh > 0.5f) BlackGhostFigure.Claws(BlackGhostFigure.Elbows[0], BlackGhostFigure.Wrists[0], BlackGhostFigure.ClawLen, -0.6f, 1f);
                if (kt > 0.5f) BlackGhostFigure.Claws(BlackGhostFigure.Elbows[1], BlackGhostFigure.Wrists[1], BlackGhostFigure.ClawLen, -0.6f, 1f);
            }
        }

        /// <summary>
        /// A torn limb lying on the floor at <paramref name="at"/>: sleeve and hand (arm) or trouser leg and boot (leg),
        /// 0.4 / 0.38 cells long, lying along <paramref name="angleDeg"/> (screen degrees, 0 east, squashed to 0.6 north-south),
        /// its torn end bloody, a small blood pool under it. Just under the pawn layer.
        /// </summary>
        public static void DrawTornPiece(Vector3 at, bool arm, float angleDeg, float alpha = 1f)
        {
            if (alpha <= 0f) return;
            Reset();
            var c = new Vector2(at.x, at.z);
            Begin(c);
            float len = arm ? ArmLen : LegLen, w = arm ? ArmW : LegW, rd = angleDeg * Mathf.Deg2Rad;
            var half = new Vector2(Mathf.Cos(rd) * len / 2f, Mathf.Sin(rd) * len / 2f * 0.6f);
            Disc(new Vector2(c.x, c.y - 0.03f), Floor + 0.01f, 0.17f, 0.09f, 0f, Fade(Blood, 0.8f * alpha));
            LimbSeg(c - half, c + half, w, arm ? BlackGhostMatter.Enemy : Pants, arm ? Skin : Boot, PawnLayer - 0.004f, alpha);
            Disc(c - half, PawnLayer - 0.003f, 0.045f, 0.035f, 0f, Fade(Blood, alpha));
        }

        /// <summary>
        /// The sketch's shadow for the lifted enemy: it stays on the floor under <paramref name="enemyPos"/> plus the pose's
        /// FloorOffset and shrinks to 0.75 (strength 0.65) at the top of the lift. For the game to draw if the pawn's own
        /// shadow is lifted with its draw offset. The lab stand-in's size: 0.95 x 0.42, 0.28 cells below DrawPos.
        /// </summary>
        public static void DrawFloorShadow(Vector3 enemyPos, TearEnemyPose pose, Vector2 sun, float strength)
        {
            var g = new Vector2(enemyPos.x + pose.FloorOffset.x, enemyPos.z + pose.FloorOffset.z);
            Sprite(new Vector2(g.x + sun.x * 0.5f, g.y - 0.28f + sun.y * 0.5f), 0.95f * pose.ShadowScale, 0.42f * pose.ShadowScale,
                Fade(ShadowBody, strength * 1.4f * pose.ShadowAlpha), soft, ShadowLayer);
        }
    }
}
