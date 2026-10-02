using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// A point in the air: its ground point, its height in cells, and the heading (degrees, 0 east, 90
    /// north) of a butterfly there. It is drawn <see cref="SixPathsHeight.Lift"/> x height cells north of
    /// its ground point, and its shadow falls from the ground point along the sun.
    /// </summary>
    public struct EgoSolemnLamentPoint
    {
        public Vector2 Ground;
        public float Height, Heading;

        public EgoSolemnLamentPoint(Vector2 ground, float height, float heading = 0f)
        {
            Ground = ground;
            Height = height;
            Heading = heading;
        }

        public Vector2 Screen => new Vector2(Ground.x, Ground.y + Height * SixPathsHeight.Lift);
    }

    /// <summary>
    /// Times and sizes of the Solemn Lament pictures (E.G.O. weapon 2, the white and the black pistol), the
    /// defaults of the lab sketch Tools/VfxLab/web/sketches/ego-solemn-lament.js. Seconds in, numbers out,
    /// no drawing and no map.
    ///
    /// The burst: the guns come up from the hips over <see cref="Lead"/> x 0.8 s and fire in turn every
    /// <see cref="Interval"/> s, white first, from <see cref="Lead"/>. Each round flies at <see cref="Speed"/>
    /// cells/s; each hit sends one lace butterfly per Butterfly stack to the target's body (<see cref="FlyTime"/>
    /// s), and at the cap 28 more spiral in over <see cref="SwarmIn"/> s and cover it.
    ///
    /// The coffin (the corrosion action): it rises out of the floor behind the wielder over <see cref="Rise"/>
    /// s, opens over <see cref="LidOpen"/> s, and at <see cref="Open"/> a flash lets out <see cref="CloudN"/>
    /// butterflies that circle inside the radius. Corroded, the wielder then walks to the nearest pawn and the
    /// coffin stays where it rose; the cloud and the floor ring go with the wielder. Every <see cref="CloudTick"/>
    /// s one leaves the cloud for each pawn inside and lands on it in <see cref="DiveTime"/> s, and the coffin
    /// sends a new one to the cloud. At the end the cloud flies back in (<see cref="ReturnTime"/> s, longer from
    /// further off), the lid closes in <see cref="LidClose"/> s and the coffin sinks in <see cref="Sink"/> s.
    ///
    /// The balance numbers (<see cref="Interval"/>, <see cref="WhiteStacks"/>, <see cref="BlackStacks"/>,
    /// <see cref="Cap"/>, <see cref="CloudRadius"/>, <see cref="CloudTick"/>) are the sketch's placeholders from
    /// docs/ego-weapons.md: the weapon will read them from its XML. The picture takes them as data, not as
    /// these constants: shot times and stacks per shot in each <see cref="EgoSolemnLamentShot"/>, the cap in
    /// <see cref="EgoSolemnLamentMark.Cap"/>, the radius in <see cref="EgoSolemnLamentCoffin.Radius"/> and the
    /// tick in the coffin's dive times. Only the preview's script reads the constants. The rest is shape and timing.
    ///
    /// Heights on a pawn are lib/pawn.js's real-size average pawn: on-screen offsets from the pawn's DrawPos
    /// (cell centre), used as they are in game. They do not go through PawnFit, which fits the older
    /// 0.89-tall stand-in to a real pawn and would move these a second time.
    /// </summary>
    public static class EgoSolemnLamentTiming
    {
        // Balance, from the weapon's XML later: seconds between shots, Butterfly stacks a white and a black shot
        // put on, the cap (the pawn goes down at it), the death count (the funeral: a downed pawn left in the
        // corroded cloud dies at it), the coffin's radius (cells) and seconds between its stacks.
        public const float Interval = 0.25f, CloudRadius = 3f, CloudTick = 1f;
        public const int WhiteStacks = 2, BlackStacks = 1, Cap = 10, Death = 20;

        // The preview's script, the sketch's showcase sliders: the target ScriptDist cells off (corroded: the
        // nearest pawn), ScriptShots shots planned (the 7th reaches the cap), the result held ScriptHold s, the
        // coffin's cloud ScriptCloud s of the rule's 30 (ScriptFuneralCloud for the funeral, long enough for two deaths).
        public const float ScriptDist = 5f, ScriptHold = 1.2f, ScriptCloud = 4f, ScriptFuneralCloud = 23f;
        public const int ScriptShots = 8;

        /// <summary>The guns come up over Lead x 0.8 s; the first shot is at Lead s.</summary>
        public const float Lead = 0.35f;
        /// <summary>A butterfly's span in cells: about a quarter of a pawn's height.</summary>
        public const float Span = 0.3f;
        /// <summary>The pistol round, cells/s: 5 cells in 0.08 s.</summary>
        public const float Speed = 60f;
        /// <summary>
        /// The black line fades over TrailLife s after the round arrives; a hit's butterflies fly FlyTime s; the
        /// cap's swarm flies SwarmIn s; the body falls over FallTime s; a cloud butterfly dives DiveTime s.
        /// </summary>
        public const float TrailLife = 0.25f, FlyTime = 0.5f, SwarmIn = 0.45f, FallTime = 0.35f, DiveTime = 0.5f;
        /// <summary>
        /// The kick: the barrel tips KickTilt degrees up in KickUp s, then swings back as a damped wave
        /// (KickDamp per s, KickSwing rad/s), and the gun slides KickSlide cells back over 0.12 s.
        /// </summary>
        public const float KickTilt = 28f, KickUp = 0.04f, KickDamp = 9f, KickSwing = 8f, KickSlide = 0.05f;
        /// <summary>A pistol is GunLen long and GunW high; the hands sit HandAcross either side, HandReach forward when aimed.</summary>
        public const float GunLen = 0.32f, GunW = 0.075f, HandAcross = 0.13f, HandReach = 0.22f;
        /// <summary>The muzzle sits MuzzleUp above the gun's line.</summary>
        public const float MuzzleUp = 0.035f;
        /// <summary>The chest's height above the ground contact in cells, for shadows and the butterflies' start.</summary>
        public const float ChestH = (PawnBody.Chest - PawnBody.Ground) / SixPathsHeight.Lift;
        /// <summary>The white and the black hit last WhiteHit and InkHit s (the floor spatter stays).</summary>
        public const float WhiteHit = 0.35f, InkHit = 0.4f;

        /// <summary>
        /// The coffin: 2.0 tall (1.2 on screen, a little over the pawn's 1.17), its top face CoffinDepth deep, its
        /// foot CoffinBackX east and CoffinBackZ north of where the wielder stood when it rose.
        /// </summary>
        public const float CoffinH = 2f, CoffinDepth = 0.26f, CoffinBackX = -0.18f, CoffinBackZ = 0.32f;
        public const float Rise = 0.5f, LidOpen = 0.3f, Open = 0.6f, ReturnTime = 0.7f, LidClose = 0.2f, Sink = 0.45f;
        /// <summary>
        /// The cloud: CloudN butterflies, CloudBurst of them out in the flash's first 0.12 s. A body has
        /// TorsoSlots resting places on the torso and HeadSlots round the head; the cap's swarm fills the rest.
        /// </summary>
        public const int CloudN = 36, CloudBurst = 24, TorsoSlots = 30, HeadSlots = 8;
        /// <summary>
        /// A slot that dove is refilled from the coffin Refill s later; the new one flies RefillFly s, or longer
        /// from further off (<see cref="FarFly"/>). The slot is not picked again until DiveRest s after it landed.
        /// </summary>
        public const float Refill = 0.25f, RefillFly = 0.7f, DiveRest = 0.25f;
        /// <summary>
        /// The preview's corroded walk (docs/ego-weapons.md, changed 2026-10-02): from WalkFrom s the wielder walks
        /// to the nearest pawn and stops Beside cells from it, eased at both ends, its speed peaking at WalkSpeed
        /// cells/s (a pawn's base move speed). In game the walk is the pawn's own; the picture only follows it.
        /// A butterfly flying between the coffin and a wielder further off flies at FarSpeed cells/s.
        /// </summary>
        public const float WalkFrom = Open + 0.2f, WalkSpeed = 4.6f, Beside = 1f, FarSpeed = 6f;
        /// <summary>
        /// The funeral (docs/ego-weapons.md, changed 2026-10-02): past the cap each stack turns about a tenth of the
        /// white butterflies on the downed body dark over TurnTime s; at the death count the whole cover lifts off
        /// (each within 0.15 s, LiftTime s, 0.6 to 1.4 cells up, turning white) and flies into the coffin's mouth
        /// at FarSpeed, never in under 0.7 s.
        /// </summary>
        public const float TurnTime = 0.3f, LiftTime = 0.6f;
        /// <summary>The cloud's orbits as the sketch draws them for radius 3: 0.7 to 2.8 cells out, 0.3 to 1.4 up.</summary>
        public const float OrbitIn = 0.7f, OrbitSpread = 2.1f, OrbitFor = 3f;
        /// <summary>The face over the corroded wielder's head: its span (cells) and beats per second.</summary>
        public const float FaceSpan = 0.66f, FaceBeat = 0.6f;

        private const float Tau = 6.2831855f;

        /// <summary>0 to 1 and back over 0 to 1, nothing outside it. Single-precision Sin(PI) is slightly negative, so it is held at 0.</summary>
        public static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;

        /// <summary>When cloud butterfly <paramref name="i"/> leaves the coffin.</summary>
        public static float CloudOut(int i) => i < CloudBurst ? Open + Rand(i + 830) * 0.12f : Open + 0.15f + (i - CloudBurst) * 0.05f;

        /// <summary>How long cloud butterfly <paramref name="i"/> takes from the coffin to its orbit.</summary>
        public static float CloudFly(int i) => i < CloudBurst ? 0.55f : 0.7f;

        /// <summary>The barrel's kick <paramref name="age"/> s after a shot, as a share of <see cref="KickTilt"/>: up in KickUp s, then a damped swing back to level.</summary>
        public static float KickAt(float age)
        {
            if (age < 0f) return 0f;
            if (age < KickUp) return Mathf.Sin(age / KickUp * Mathf.PI / 2f);
            float a = age - KickUp;
            return Mathf.Max(0f, Mathf.Exp(-KickDamp * a) * Mathf.Cos(KickSwing * a));
        }

        /// <summary>A wing beat: the share of the full span, from <paramref name="lo"/> to 1, <paramref name="rate"/> beats a second, phase by <paramref name="i"/>.</summary>
        public static float FlapAt(float s, float rate, float lo, int i) => lo + (1f - lo) * Mathf.Abs(Mathf.Cos(Mathf.PI * rate * s + i * 1.7f));

        /// <summary>
        /// Where a stack's butterfly rests on a standing body, from the cell centre (u east, v north) and its
        /// heading: a sunflower spread over lib/pawn.js's torso ellipse from the chest outward, so stacks gather
        /// from the middle, then eight round the head.
        /// </summary>
        public static void SlotLocal(int i, out float u, out float v, out float heading)
        {
            if (i < TorsoSlots)
            {
                float r = Mathf.Sqrt((i + 0.5f) / TorsoSlots) * 0.85f, t = i * 2.39996f + 0.7f;
                u = Mathf.Cos(t) * r * 0.27f;
                v = -0.12f + Mathf.Sin(t) * r * 0.42f;
                heading = 70f + Rand(i + 3000) * 40f;
                return;
            }
            int j = i - TorsoSlots;
            float th = j / (float)HeadSlots * Tau + 0.3f, rr = 0.5f + 0.35f * Rand(j + 3100);
            u = Mathf.Cos(th) * rr * 0.21f;
            v = 0.41f + Mathf.Sin(th) * rr * 0.22f;
            heading = 60f + Rand(j + 3200) * 60f;
        }

        /// <summary>Slot <paramref name="i"/> on a pawn drawn at <paramref name="pos"/>, turned clockwise <paramref name="turn"/> degrees with it as it falls.</summary>
        public static Vector2 SlotAt(Vector2 pos, int i, float turn, out float heading)
        {
            SlotLocal(i, out float u, out float v, out float h);
            float a = turn * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            heading = h - turn;
            return new Vector2(pos.x + u * c + v * s, pos.y - u * s + v * c);
        }

        /// <summary>
        /// A butterfly's flight from its start to <paramref name="to"/>, <paramref name="u"/> 0 to 1: eased out,
        /// bowed out by <paramref name="via"/> (a ground offset at mid-flight) and up by <paramref name="arc"/>,
        /// with a 0.04-cell flutter in height.
        /// </summary>
        public static EgoSolemnLamentPoint FlyAt(Vector2 fromGround, float fromHeight, Vector2 via, float arc, int seed, float u, Vector2 toGround, float toHeight)
        {
            float k = 1f - (1f - u) * (1f - u), b = Bump(u);
            var g = new Vector2(Mathf.Lerp(fromGround.x, toGround.x, k) + via.x * b, Mathf.Lerp(fromGround.y, toGround.y, k) + via.y * b);
            float h = Mathf.Lerp(fromHeight, toHeight, k) + arc * b + 0.04f * Mathf.Sin(u * 16f + seed);
            return new EgoSolemnLamentPoint(g, h);
        }

        /// <summary>
        /// Cloud butterfly <paramref name="i"/>'s orbit round <paramref name="centre"/> at <paramref name="t"/>:
        /// 0.7 to 2.8 cells out for radius 3 (scaled with <paramref name="radius"/>), 0.3 to 1.4 up, four in five
        /// turning anticlockwise at 0.45 to 1.05 rad/s, bobbing 0.12.
        /// </summary>
        public static EgoSolemnLamentPoint Orbit(int i, float t, Vector2 centre, float radius)
        {
            float r = (OrbitIn + OrbitSpread * Rand(i + 4000)) * radius / OrbitFor;
            float w = (0.45f + 0.6f * Rand(i + 4020)) * (Rand(i + 4030) < 0.8f ? 1f : -1f), th = Rand(i + 4040) * Tau + w * t;
            var g = new Vector2(centre.x + Mathf.Cos(th) * r, centre.y + Mathf.Sin(th) * r);
            float h = 0.3f + 1.1f * Rand(i + 4010) + 0.12f * Mathf.Sin(2.3f * t + i);
            return new EgoSolemnLamentPoint(g, h, (th + (w >= 0f ? 1f : -1f) * Mathf.PI / 2f) * Mathf.Rad2Deg);
        }

        /// <summary>Half the coffin front's width <paramref name="h"/> up: 0.2 at the foot, 0.34 at the shoulders (0.7 up), 0.24 at the head.</summary>
        public static float CoffinHalf(float h)
        {
            float k = h / CoffinH;
            return k < 0.7f ? Mathf.Lerp(0.2f, 0.34f, k / 0.7f) : Mathf.Lerp(0.34f, 0.24f, (k - 0.7f) / 0.3f);
        }

        /// <summary>The cloud's last second: it leaves for the coffin 0.1 s after (plus up to 0.15 s per butterfly).</summary>
        public static float CloudEnd(float cloud) => Open + cloud;

        /// <summary>
        /// How long a butterfly takes over <paramref name="distance"/> cells between the coffin and the cloud: at
        /// <see cref="FarSpeed"/>, never less than <paramref name="least"/> (ReturnTime, RefillFly: 0.7 s, which
        /// covers 4.2 cells).
        /// </summary>
        public static float FarFly(float distance, float least) => Mathf.Max(least, distance / FarSpeed);

        /// <summary>How much of the coffin stands above the floor at <paramref name="s"/>: it rises over Rise s and sinks over Sink s from <paramref name="sinkAt"/>.</summary>
        public static float CoffinRise(float s, float sinkAt) => Smooth(s / Rise) * (1f - Smooth((s - sinkAt) / Sink));

        /// <summary>The coffin's mouth (the lit inside, 62 % up its height, where the butterflies come out and go back in) with its foot at <paramref name="foot"/> and <paramref name="rise"/> of it above the floor.</summary>
        public static EgoSolemnLamentPoint Mouth(Vector2 foot, float rise) => new EgoSolemnLamentPoint(foot, Mathf.Max(0f, CoffinH * 0.62f - (1f - rise) * CoffinH));

        /// <summary>The lid starts to close: the cloud is back in, <paramref name="home"/> s after it left (<see cref="FarFly"/>).</summary>
        public static float CloseAt(float cloud, float home) => CloudEnd(cloud) + 0.1f + home;

        /// <summary>The coffin starts to sink.</summary>
        public static float SinkAt(float cloud, float home) => CloseAt(cloud, home) + LidClose;

        /// <summary>The coffin's picture's length, holding the result <paramref name="hold"/> s.</summary>
        public static float CoffinEnd(float cloud, float home, float hold) => SinkAt(cloud, home) + Sink + hold;

        /// <summary>When the cloud's <paramref name="k"/>-th stack falls (k from 1): every CloudTick s after the opening.</summary>
        public static float TickAt(int k, float tick) => Open + k * tick;

        /// <summary>How many stacks the cloud gives in <paramref name="cloud"/> s.</summary>
        public static int Ticks(float cloud, float tick) => Mathf.FloorToInt(cloud / tick + 1e-6f);

        /// <summary>The side of a gun the grip hangs from, toward the viewer: left of the aim facing east, mirrored facing west, as the game flips a gun's sprite.</summary>
        public static Vector2 GunUp(Vector2 u) => u.x >= 0f ? new Vector2(-u.y, u.x) : new Vector2(u.y, -u.x);

        /// <summary>The muzzle of a level gun held at <paramref name="hand"/> (a ground point) and aimed along <paramref name="d"/>.</summary>
        public static Vector2 MuzzleAt(Vector2 hand, Vector2 d)
        {
            Vector2 up = GunUp(d);
            return new Vector2(hand.x + d.x * GunLen + up.x * MuzzleUp, hand.y + PawnBody.Chest + d.y * GunLen + up.y * MuzzleUp);
        }
    }
}
