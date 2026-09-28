using UnityEngine;
using Verse;
using static RimArt.BlackReceiverTiming;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using G = RimArt.BlackReceiverGraphics;
using P = RimArt.PainGraphics;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt, slowed or pinned. Each entry replays one scenario of the lab's
    /// pain-black-receiver.js at its defaults (aim east, rods shown lasting 5 s) round the clicked cell, which is the
    /// cell the sketch centres on, so the recorder can compare the port with the sketch. The sketch's stand-ins (Pain,
    /// the raiders, Banshō's dent, the stand-in push, the shooter and its tracers) are drawn too, through PainGraphics
    /// and here, because the sketch has no switch to hide them.
    /// </summary>
    public static class DebugActions_BlackReceiverPreview
    {
        [RimArtDebug("Pain", "black receiver: three throws")]
        public static void ThreeThrows() => Play(BlackReceiverScenario.Throws);

        [RimArtDebug("Pain", "black receiver: pinned one stays when Pain pushes")]
        public static void PinnedPush() => Play(BlackReceiverScenario.Push);

        [RimArtDebug("Pain", "black receiver: after Banshō, stabbed face-down")]
        public static void Stabbed() => Play(BlackReceiverScenario.Stab);

        [RimArtDebug("Pain", "black receiver: Pain goes down")]
        public static void PainDown() => Play(BlackReceiverScenario.Down);

        [RimArtDebug("Pain", "black receiver: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_BlackReceiverPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(BlackReceiverScenario scenario, float aim = 0f) =>
            Find.CurrentMap.GetComponent<MapComponent_BlackReceiverPreview>().Play(UI.MouseCell(), scenario, aim);
    }

    /// <summary>The sketch's scenarios: three throws at a raider walking in; the pinned one stays when Pain pushes; stabbed face-down after Banshō; Pain goes down and every rod breaks.</summary>
    public enum BlackReceiverScenario { Throws, Push, Stab, Down }

    /// <summary>
    /// Replays one Black Receiver scenario on its own clock and switches itself off at the sketch's end. Pain stands
    /// 4 cells before the clicked cell's centre along the aim for the throws and 1.5 cells before it otherwise. The
    /// camera shakes where the sketch's events do; the sketch's sounds are not played.
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class MapComponent_BlackReceiverPreview : MapComponent
    {
        private static readonly Color Tracer = new Color(1f, 0.9f, 0.62f);
        private static readonly Color Enemy = P.EnemyColour;
        private static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();

        public bool active;
        /// <summary>Seconds since the clip began: the sketch's s.</summary>
        public float seconds;
        private BlackReceiverScenario mode;
        private IntVec3 cell;
        private float aim;
        private BlackReceiverThrows throws;
        // Camera shakes still to come: time and size, in order.
        private readonly float[] shakeAt = new float[8], shakeSize = new float[8];
        private int shakes, shaken;
        private Vector2 sun;
        private float strength;

        public MapComponent_BlackReceiverPreview(Map map) : base(map) { }

        public float End => mode == BlackReceiverScenario.Throws ? throws.end
            : mode == BlackReceiverScenario.Push ? PushEnd
            : mode == BlackReceiverScenario.Stab ? StabEnd : DownEnd;

        public void Play(IntVec3 at, BlackReceiverScenario scenario, float aimDegrees = 0f)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = scenario;
            aim = aimDegrees;
            throws = new BlackReceiverThrows();
            seconds = 0f;
            shakes = shaken = 0;
            switch (scenario)
            {
                case BlackReceiverScenario.Throws:
                    for (int k = 0; k < 3; k++) AddShake(throws.hits[k], ShakeHit);
                    AddShake(throws.landed, ShakePin);
                    break;
                case BlackReceiverScenario.Push:
                    AddShake(PushAt, ShakePush);
                    break;
                case BlackReceiverScenario.Stab:
                    for (int k = 0; k < 3; k++) AddShake(StabStart(k) + StabIn, k == 2 ? ShakeStabPin : ShakeHit);
                    break;
            }
            active = true;
        }

        private void AddShake(float t, float size)
        {
            int i = shakes++;
            while (i > 0 && shakeAt[i - 1] > t)
            {
                shakeAt[i] = shakeAt[i - 1];
                shakeSize[i] = shakeSize[i - 1];
                i--;
            }
            shakeAt[i] = t;
            shakeSize[i] = size;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            float s = seconds;
            if (s >= End)
            {
                active = false;
                return;
            }
            while (shaken < shakes && s >= shakeAt[shaken]) Find.CameraDriver.shaker.DoShake(shakeSize[shaken++]);

            Vector3 centre = cell.ToVector3Shifted();
            var o = new Vector2(centre.x, centre.z);
            Vector2 dir = Dir(aim);
            sun = G.Sun(map);
            strength = G.ShadowStrength(map);
            Begin(o);
            switch (mode)
            {
                case BlackReceiverScenario.Throws: Throws(s, Ground(o, dir, -throws.distance / 2f, 0f), dir); break;
                case BlackReceiverScenario.Push: Push(s, Ground(o, dir, -1.5f, 0f), dir); break;
                case BlackReceiverScenario.Stab: Stabs(s, Ground(o, dir, -1.5f, 0f), dir); break;
                default: Down(s, Ground(o, dir, -1.5f, 0f), dir); break;
            }
        }

        // ---- three throws -----------------------------------------------------------------------------------------

        private void Throws(float s, Vector2 P0, Vector2 away)
        {
            BlackReceiverThrows t = throws;
            Vector2 toward = -away;
            float a = t.AlongAt(s), walked = t.distance - t.WalkAt(s);   // WalkAt leaves out the knock-back, so this only grows
            bool pinnedNow = s >= t.hits[2] && s < t.breaks[0];
            float shake = pinnedNow && s >= t.landed ? Shudder(s) : 0f;
            Vector2 g = Ground(P0, away, a, shake);
            bool moving = s < t.hits[2] || s >= t.upEnd;
            float bob = moving && t.walk > 0f ? 0.02f * Mathf.Abs(Mathf.Sin(walked * 5.2f)) : 0f;
            Vector2 gl = Ground(P0, away, t.LyingAt, 0f);   // where it lies while pinned

            // Pain, and the raider: standing, falling on its back, pinned, getting up, walking again.
            P.Pain(P0, sun, strength);
            float fallU = FallShare(s - t.hits[2]), riseU = RiseShare(s - t.breaks[0]);
            float lieA = s < t.hits[2] ? 0f : s < t.breaks[0] ? fallU : 1f - riseU;
            if (lieA > 0f) P.Lying(new Vector2(gl.x + shake, gl.y), Enemy, away, sun, strength, lieA);
            if (lieA < 1f) P.Standing(new Vector2(g.x, g.y + bob), Enemy, sun, strength, 1f - lieA);

            // The floor under the pinned body: the rods went through into it.
            if (s >= t.landed) G.Holes(gl, away, s - t.landed, map);
            G.PinDust(gl, s - t.landed, map);

            // Pain's arm: up at the target through the three throws, the fingers round the rod while it grows, a flick
            // forward as it leaves, then down after the last one.
            float armUp = Smooth((s - t.starts[0]) / (t.warm * 0.6f)), armDown = Smooth((s - (t.releases[2] + ArmHold)) / ArmDown);
            float flick = 0f;
            bool holding = false;
            for (int k = 0; k < 3; k++)
            {
                flick += FlickReach * Bump((s - t.releases[k]) / (Flick * 2f));
                holding |= s >= t.starts[k] && s < t.releases[k];
            }
            float handAlong = FromBody + PainGraphics.Reach * armUp * (1f - armDown) + flick;
            if (armUp > 0f && armDown < 1f) G.Arm(P0, aim, handAlong, holding ? 0.55f : 0f);
            G.Eye(P0, s - t.starts[0]);

            for (int k = 0; k < 3; k++)
            {
                if (s < t.starts[k]) continue;
                if (s < t.releases[k])
                {
                    G.InHand(P0, aim, handAlong, Smooth((s - t.starts[k]) / t.warm), map, t.rodLength);
                    continue;
                }
                if (s < t.hits[k])
                {
                    G.Flying(P0, Ground(P0, away, a, 0f), (s - t.releases[k]) / (t.hits[k] - t.releases[k]), k, map, t.rodLength);
                    continue;
                }
                // Stuck: in the standing pawn, carried over on its back while it falls, upright while it is pinned, back
                // in the standing pawn once it is up. A rod breaking while the pawn is down breaks where it stands.
                bool broke = s >= t.breaks[k], lyingBreak = t.breaks[k] < t.upEnd;
                ReceiverPose pinned = OnBack(k, gl, away), standing = InBody(k, Ground(P0, away, a, 0f), toward), pose;
                if (broke && lyingBreak) pose = pinned;
                else if (s < t.hits[2]) pose = standing;
                else if (s < t.landed) pose = FallPose(k, Ground(P0, away, t.WalkAt(t.hits[2]), 0f), gl, toward, s - t.hits[2]);
                else if (s < t.breaks[0]) pose = pinned;
                else if (s < t.upEnd) pose = RisePose(k, gl, Ground(P0, away, a, 0f), toward, s - t.breaks[0]);
                else pose = standing;
                if (pose.lying) pose = pose.Shifted(shake);
                G.Stuck(pose, k, s - t.hits[k], broke ? s - t.breaks[k] : -1f, s, 0f, map);
            }
        }

        // ---- the pinned one stays when Pain pushes ------------------------------------------------------------------

        private void Push(float s, Vector2 P0, Vector2 away)
        {
            Vector2 gl = Ground(P0, away, PinnedAt, 0f);
            float age = s - PushAt;
            float jolt = age >= 0f && age < 0.3f ? 0.025f * Mathf.Sin(age * 90f) * (1f - age / 0.3f) : 0f;
            P.Pain(P0, sun, strength);
            G.Holes(gl, away, s, map);
            P.Lying(new Vector2(gl.x + jolt, gl.y), Enemy, away, sun, strength);
            float flare = G.Flare(age);
            for (int k = 0; k < 3; k++) G.Stuck(OnBack(k, gl, away).Shifted(jolt), k, -1f, -1f, s, flare, map);
            G.PushDust(gl, age, map);

            // Two raiders walk up on Pain's left (north for aim east) and are thrown 5 cells out; they land and lie stunned.
            for (int n = 0; n < 2; n++)
            {
                float side = n == 0 ? -1f : 1f;
                Vector2 d = Turned(away, PushFrom[side > 0f ? 1 : 0]);
                float r = age < 0f ? Mathf.Lerp(PushWalkFrom, PushWalkTo, Mathf.Clamp01(s / PushArrive)) : PushWalkTo + PushOut * EaseOut(age / PushFly);
                Vector2 g = P0 + d * r;
                if (age < 0f) P.Standing(new Vector2(g.x, g.y + (s < PushArrive ? 0.02f * Mathf.Abs(Mathf.Sin(s * 10f)) : 0f)), Enemy, sun, strength);
                else if (age < PushFly)
                {
                    float h = 0.35f * Bump(age / PushFly);
                    Sprite(g + sun * (0.3f + h), 0.8f, 0.4f, Fade(Ink, strength / (1f + h)), soft, ShadowLayer);
                    P.Standing(new Vector2(g.x, g.y + h * PainGraphics.Lift), Enemy, Vector2.zero, 0f);
                }
                else
                {
                    P.Lying(g, Enemy, d, sun, strength);
                    GokuGraphics.StunStars(new Vector2(g.x + d.x * 0.4f, g.y + d.y * 0.4f + 0.08f - 0.84f), s, Mathf.Clamp01((age - PushFly) / 0.15f));
                    if (age < PushFly + 0.5f) ChainSickleGraphics.Kick(g, age - PushFly, 1f - (age - PushFly) / 0.5f, side > 0f ? 40 : 50);
                }
            }
            // The push itself, a stand-in (not the Shinra Tensei look): a flash at Pain, a soft glow 4 cells out, dust
            // streaks running outward on the floor.
            if (age >= 0f && age < 0.45f)
            {
                float u = age / 0.45f, wide = 8f * EaseOut(Mathf.Clamp01(age / 0.2f));
                Sprite(new Vector2(P0.x, P0.y + 0.35f), 2.2f, 2.2f, Fade(PainGraphics.PaleBlue, 0.7f * Mathf.Clamp01(1f - age / 0.15f)), glow, Overhead + 0.1f);
                Sprite(new Vector2(P0.x, P0.y + 0.2f), wide, wide, Fade(PainGraphics.PaleBlue, 0.22f * (1f - u)), glow, Overhead + 0.09f);
                for (int i = 0; i < 16; i++)
                {
                    float ang = i / 16f * 360f + Rand(i + 300) * 12f;
                    Vector2 d = Dir(ang);
                    float r0 = 0.6f + EaseOut(u) * 3.2f * (0.7f + Rand(i + 301) * 0.3f), r1 = r0 + 0.5f + Rand(i + 302) * 0.5f;
                    Streak(P0 + d * r0, P0 + d * r1, 0.06f, Fade(PainGraphics.DustC, 0.45f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), solid,
                        Floor + 0.016f + i * 0.0001f, 4);
                }
            }
        }

        // ---- after Banshō, stabbed face-down ----------------------------------------------------------------------

        private readonly ReceiverPose[] finals = new ReceiverPose[3];

        private void Stabs(float s, Vector2 P0, Vector2 away)
        {
            Vector2 toward = -away, gl = Ground(P0, away, StabBody, 0f);
            float in2 = StabStart(2) + StabIn, shake = s >= in2 ? Shudder(s) : 0f;
            P.Pain(P0, sun, strength);
            // Banshō's slam left a dent and cracks under it (a stand-in for that picture).
            Sprite(gl, 1.2f, 0.9f, Fade(PainGraphics.Core, 0.25f), soft, Floor + 0.02f);
            ChainSickleGraphics.Crack(gl, 1.3f, 17);
            P.Lying(new Vector2(gl.x + shake, gl.y), Enemy, toward, sun, strength);
            GokuGraphics.StunStars(new Vector2(gl.x + toward.x * 0.4f, gl.y + toward.y * 0.4f + 0.08f - 0.84f), s, Mathf.Clamp01((1.1f - s) / 0.3f));

            for (int k = 0; k < 3; k++) finals[k] = OnBack(k, gl, toward, toward);
            bool held = false;
            Vector2 hand = default;
            float grip = 0.3f;
            for (int k = 0; k < 3; k++)
            {
                float st = StabStart(k), inAt = st + StabIn;
                if (s < st) continue;
                if (s < inAt)
                {
                    if (G.StabRod(P0, gl, toward, k, s - st, map, out hand))
                    {
                        held = true;
                        grip = 0.7f;
                    }
                    continue;
                }
                if (s < inAt + Hold)
                {
                    hand = G.KnobDrawn(finals[k]);
                    held = true;
                    grip = Mathf.Lerp(0.7f, 0.3f, (s - inAt) / Hold);
                }
                G.Stuck(finals[k].Shifted(shake), k, s - inAt, -1f, s, 0f, map);
                if (k == 2 && s - inAt < LandDust) ChainSickleGraphics.Kick(gl, s - inAt, 1f - (s - inAt) / LandDust, 60);
            }
            // Outside the stabs the hand travels: up from rest to the first rod, from each rod to the next one's start,
            // and back down after the last.
            if (!held)
            {
                Vector2 rest = PainGraphics.Place(P0, away, FromBody + PainGraphics.Reach * RestReach, Across, PainGraphics.HandH);
                for (int leg = 0; leg < 4 && !held; leg++)
                {
                    float t0 = leg == 0 ? StabStart(0) - StabApproach : StabStart(leg - 1) + StabIn + Hold;
                    float t1 = leg < 3 ? StabStart(leg) : t0 + StabReturn;
                    if (s < t0 || s >= t1) continue;
                    Vector2 from = leg == 0 ? rest : G.KnobDrawn(finals[leg - 1]);
                    Vector2 to = leg < 3 ? G.KnobDrawn(Raised(finals[leg], StabLift)) : rest;
                    hand = Vector2.Lerp(from, to, Smooth((s - t0) / (t1 - t0)));
                    held = true;
                }
            }
            if (held) G.ArmTo(P0, away, hand, grip);
            G.Eye(P0, s - StabStart(0));
        }

        // ---- Pain goes down, every rod breaks -------------------------------------------------------------------

        private void Down(float s, Vector2 P0, Vector2 away)
        {
            Vector2 gl = Ground(P0, away, DownPinnedAt, 0f), shooter = Ground(P0, away, 6f, 3.4f);
            float free = DownAt + FreeDelay, shake = s < DownAt ? Shudder(s) : 0f;
            G.Holes(gl, away, s, map);

            // Pain is shot three times and goes down; he falls away from the shooter.
            float downU = Smooth((s - DownAt) / 0.2f);
            Vector2 fall = (P0 - shooter).normalized;
            if (downU < 0.5f) P.Pain(P0 + fall * (0.15f * downU), sun, strength);
            else P.PainDown(P0 + fall * 0.25f, fall, sun, strength);
            P.Standing(shooter, Enemy, sun, strength);
            for (int i = 0; i < 3; i++)
            {
                float age = s - (ShotsAt + i * ShotGap);
                if (age < 0f || age > 0.1f) continue;
                var from = new Vector2(shooter.x, shooter.y + 0.3f);
                var to = new Vector2(P0.x + (Rand(i + 70) - 0.5f) * 0.15f, P0.y + 0.3f);
                float fade = 1f - age / 0.1f;
                Streak(from, to, 0.09f, Fade(Tracer, fade), whiteGlow, Overhead + 0.05f + i * 0.0001f, 6);
                Sprite(to, 0.5f, 0.5f, Fade(Tracer, 0.9f * fade), glow, Overhead + 0.051f);
                Sprite(from, 0.3f, 0.3f, Fade(Tracer, 0.8f * fade), glow, Overhead + 0.0512f);
            }

            // The pinned raider: pinned until Pain goes down, then every rod breaks at once and it gets up.
            float riseU = RiseShare(s - free), walk = s > free + GetUp ? Mathf.Min(1.2f, (s - free - GetUp) * Walk * 0.6f) : 0f;
            if (riseU < 1f) P.Lying(new Vector2(gl.x + shake, gl.y), Enemy, away, sun, strength, 1f - riseU);
            if (riseU > 0f) P.Standing(Ground(P0, away, DownPinnedAt - walk, 0f), Enemy, sun, strength, riseU);
            for (int k = 0; k < 3; k++)
                G.Stuck(OnBack(k, gl, away).Shifted(shake), k, -1f, s - DownAt - k * BreakStagger, s, 0f, map);
            G.AllBreak(gl, s - DownAt, map);
        }
    }
}
