using UnityEngine;
using Verse;
using static RimArt.GokuGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using G = RimArt.GokuTiming;
using T = RimArt.GokuInstantTransmissionTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Instant Transmission: the glint at the forehead and the rings closing on the head while
    /// the caster feels for ki, the floor rippling at the destination and the pawns there glowing,
    /// the glow on the passenger where the caster's hand touches it, the vanish (the body in ki
    /// slices that slide apart and thin away) and the arrival (the same, closing), the speed lines,
    /// ring and dust left at both places, and a hostile passenger's stun stars. There is no line or
    /// light between the two places. The slices always slide east-west on screen, so there is no
    /// per-facing method.
    ///
    /// Not drawn (the sketch's stand-ins): the pawns and their shadows, the flicker before the
    /// vanish (a pawn's alpha), the arms (fingers to the forehead, the hand on the passenger), the
    /// passenger's white outline, and the bed and rooms.
    /// </summary>
    public static class GokuInstantTransmissionGraphics
    {
        private static readonly Vector2[] waiting = new Vector2[4];

        /// <summary>The preview. <paramref name="centre"/> is halfway between the two places, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, TransmissionScene scene, float seconds, Map map)
        {
            var o = new Vector2(centre.x, centre.z);
            Vector2 dest = G.Place(o, toward, T.ScriptDistance / 2f);
            Vector2[] local = T.Waiting(scene);
            for (int i = 0; i < local.Length; i++) waiting[i] = G.Place(dest, toward, local[i].x, local[i].y);
            Draw(new TransmissionShot
            {
                Home = G.Place(o, toward, -T.ScriptDistance / 2f), Dest = dest, Toward = toward, Seconds = seconds, Plan = T.Plan(),
                Carries = scene != TransmissionScene.Alone, Hostile = scene == TransmissionScene.Kidnap, Lying = scene == TransmissionScene.Rescue,
                Waiting = waiting, WaitingCount = local.Length,
            }, map);
        }

        public static void Draw(in TransmissionShot shot, Map map)
        {
            TransmissionPlan t = shot.Plan;
            float s = shot.Seconds;
            Vector2 home = shot.Home, dest = shot.Dest;
            if (s < 0f || s >= t.End || !Shown(home, map) && !Shown(dest, map)) return;
            Begin(home);
            float sensing = T.Sensing(s, t);

            // --- the destination answers while the caster feels for it ---
            if (sensing > 0f)
                for (int n = 0; n < 3; n++)
                {
                    float v = ((s - t.Cast) * 1.6f + n / 3f) % 1f;
                    PaperBombGraphics.RingAt(dest, 0.2f + v * 0.9f, Fade(KiIce, 0.55f * (1f - v) * sensing), Floor + 0.02f);
                }
            for (int i = 0; i < shot.WaitingCount; i++)
            {
                Vector2 p = shot.Waiting[i];
                Sprite(new Vector2(p.x, p.y + 0.3f), 1.1f, 1.3f, Fade(KiSky, 0.3f * sensing * (0.7f + 0.3f * Mathf.Sin(s * 14f + i))), glow, Overhead + 0.01f);
            }

            // --- the caster and the passenger slicing away and closing up ---
            float gone = Mathf.Clamp01((s - t.Go) / t.Vanish), back = 1f - Mathf.Clamp01((s - t.Arrive) / t.Vanish);
            bool there = s >= t.Arrive, shown = s < t.Gone || there;
            float u = there ? back : gone;
            Vector2 casterAt = there ? dest : home, riderAt = G.Place(casterAt, shot.Toward, 0f, T.Side);
            if (shown)
            {
                Sliced(casterAt, u, hair: true);
                if (shot.Carries) Sliced(riderAt, u, lie: shot.Lying);
            }
            if (shot.Hostile && s >= t.Landed)
                StunStars(riderAt, s, Mathf.Clamp01((s - t.Landed) / 0.15f) * Mathf.Clamp01((t.Landed + T.HostileStun - s) / 0.2f));

            // --- the glint at the forehead, the rings closing on the head, the touch on the passenger ---
            if (s >= t.Cast && s < t.Go)
            {
                float w = (s - t.Cast) / t.Warm, touch = shot.Carries ? Smooth((s - t.Cast) / 0.2f) : 0f;
                Glint(new Vector2(home.x + 0.07f, home.y + 0.62f), 0.1f + 0.06f * Mathf.Sin(s * 30f), 0.9f * sensing, White, s * 90f);
                for (int n = 0; n < 2; n++)
                {
                    float v = (w * 2.5f + n / 2f) % 1f;
                    PaperBombGraphics.RingAt(new Vector2(home.x, home.y + 0.6f), 0.7f * (1f - v) + 0.08f, Fade(KiIce, 0.7f * v * sensing), Overhead + 0.03f);
                }
                if (touch > 0f)
                {
                    Vector2 hand = G.Place(home, shot.Toward, 0f, T.Side * 0.62f * touch);
                    Sprite(new Vector2(hand.x, hand.y + 0.36f), 0.45f, 0.45f, Fade(KiIce, 0.7f * touch * (0.7f + 0.3f * Mathf.Sin(s * 25f))), glow, Overhead + 0.02f);
                }
            }

            // --- what is left behind, and what announces the arrival ---
            Blink(home, s - t.Go - t.Vanish * 0.5f, T.BlinkLife);
            Blink(dest, s - t.Arrive, T.BlinkLife);
            if (shot.Carries)
            {
                Blink(G.Place(home, shot.Toward, 0f, T.Side), s - t.Go - t.Vanish * 0.5f, T.BlinkLife);
                Blink(G.Place(dest, shot.Toward, 0f, T.Side), s - t.Arrive, T.BlinkLife);
            }
        }
    }
}
