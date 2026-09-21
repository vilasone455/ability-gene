using UnityEngine;
using Verse;
using static RimArt.ShadowPlexusGraphics;
using static RimArt.ThunderGodGraphics;
using P = RimArt.ShadowPlexusTiming;
using T = RimArt.ShadowGraspTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Shadow grasp: the tendril from the carrier to the thing, a flat hand of shadow opening
    /// under it and curling over it, a thin feeler running ahead along the path, the slide with the
    /// tendril bending at the pick-up cell and following the hand, and the let-go with the tendril
    /// running back along both legs. All of it is flat on the floor; the curled fingers rise over what
    /// they hold. No pawn, grenade or explosion is drawn.
    /// </summary>
    public static class ShadowGraspGraphics
    {
        private static readonly Vector2[] leg = new Vector2[13], feeler = new Vector2[9];

        /// <summary>The preview. <paramref name="centre"/> is the thing's cell, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, Vector2 toward, GraspScene scene, float seconds, Map map)
        {
            GraspShot shot = T.Script(scene, seconds, out float duration);
            if (seconds < 0f || seconds >= duration) return;
            var o = new Vector2(centre.x, centre.z);
            var left = new Vector2(-toward.y, toward.x);
            Vector2 Place(Vector2 local) => o + toward * local.x + left * local.y;
            shot.Carrier = Place(shot.Carrier);
            shot.Item = Place(shot.Item);
            shot.Dest = Place(shot.Dest);
            shot.Aim = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
            shot.Range = P.Range(T.FullRange, P.Level(false));
            Draw(shot, map);

            // The body is dragged over the ground: dust along the path.
            if (scene == GraspScene.Rescue)
                for (int k = 0; shot.SlideStart + k * T.ScuffEvery < Mathf.Min(seconds, shot.Arrive); k++)
                {
                    float at = shot.SlideStart + k * T.ScuffEvery;
                    Scuff(P.PointOn(shot.Item, shot.Dest, shot.SlidAt(at)), seconds - at, 1.3f);
                }
        }

        public static void Draw(in GraspShot shot, Map map)
        {
            float s = shot.Seconds;
            if (s < 0f || !Shown(shot.Carrier, map)) return;
            Begin(shot.Carrier);
            Vector2 carrier = shot.Carrier, item = shot.Item;
            float slid = shot.SlidAt(s);
            Vector2 at = P.PointOn(item, shot.Dest, slid), stop = P.PointOn(item, shot.Dest, shot.Share);
            if (shot.Range > 0f) RangeRing(carrier, shot.Range, 1f - P.Smooth((s - shot.Arrive) / 0.4f));

            // The tendril: carrier to the pick-up cell, then on to the hand. It runs back along both legs.
            float leg1 = Vector2.Distance(carrier, item), leg2 = Vector2.Distance(item, shot.Dest) * slid;
            float back = P.Smooth((s - shot.Arrive - T.LetGo) / T.Back);
            float left = (s < shot.Cast ? P.EaseOut(s / shot.Cast) : 1f - back) * (leg1 + (s < shot.Cast ? 0f : leg2));
            // Blunt while it ends in the wrist.
            Line(carrier, item, 0f, Mathf.Clamp01(left / leg1), s, shot.Sway, shot.Width, point: s < shot.Cast || back > 0f);
            if (left > leg1)
            {
                Pool(item, shot.Width * 0.8f, 1f, s);
                P.Path(leg, item, at, 0f, (left - leg1) / (leg2 > 0f ? leg2 : 1f), s, 0.04f);
                ShadowLine(leg, shot.Width, 1f, s, flare: false, point: back > 0f);
            }
            Pool(carrier, 0.28f * Mathf.Clamp01(left * 3f), 1f, s);

            // The feeler: the path the thing will take. A pawn standing on it stops the thing there.
            float feel = P.Smooth((s - shot.Cast) / T.Open) * (1f - P.Smooth((s - shot.Arrive) / 0.1f));
            if (feel > 0f)
            {
                P.Path(feeler, at, shot.Share < 1f ? stop : shot.Dest, 0f, feel, s, 0f);
                ShadowLine(feeler, 0.05f, 0.6f, s, flare: false);
            }

            // The hand. The wrist stays on the tendril: it turns as the second leg grows.
            float open = P.Smooth((s - shot.Cast) / T.Open) * (1f - P.Smooth((s - shot.Arrive - T.LetGo) / 0.15f));
            float curl = P.Smooth((s - shot.Cast - T.Open) / T.Curl) * (1f - P.Smooth((s - shot.Arrive) / T.LetGo));
            Hand(at, shot.Aim + shot.Turn * P.Smooth(leg2 / T.TurnOver), open, curl, shot.HandSize);
            Shreds(at, s - shot.Arrive - T.LetGo, 6, 0.35f, 0.5f);
        }
    }
}
