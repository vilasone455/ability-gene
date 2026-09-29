using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Pain's sleeves while a Shinra Tensei clip plays. Melee Animation draws his two hands and nothing between them
    /// and the body, so a hand held out (the burst's T, the charged hold's raised Y) floats in the air. Each frame
    /// this reads where the clip draws BodyA, HandA and HandB and draws <see cref="PainGraphics.Sleeve"/> from the
    /// shoulder on the hand's side to just short of the hand, as the sketch does (pain-shinra-tensei.js, "shinra
    /// sleeve"). It follows whichever Shinra clip plays. Drawing only; nothing is kept.
    /// </summary>
    public static class ShinraSleeves
    {
        /// <summary>
        /// The shoulder from the body's centre: the sketch's 0.13 across, and 0.02 north (its shoulder is 0.32 over
        /// the ground point, which the game puts 0.3 under the body's centre, VergilKit.FeetBelowDrawPos).
        /// </summary>
        private const float ShoulderAcross = 0.13f, ShoulderNorth = 0.02f;
        /// <summary>The sleeve stops this far short of the hand's centre, so its cuff shows at the hand's edge (Melee Animation's hand is 0.175 across).</summary>
        private const float WristGap = 0.05f;
        /// <summary>
        /// The sleeve is drawn this far under its hand: over every layer of the pawn (at most 0.037 over the root,
        /// PawnRenderUtility.AltitudeForLayer(100)) and under the hands, which the clips put 0.05 and 0.055 over it.
        /// </summary>
        private const float UnderHand = 0.004f;
        private static readonly string[] Hands = { "HandA", "HandB" };

        public static void Draw(CastClips.Handle clip)
        {
            if (clip == null || !clip.TryPart("BodyA", out Vector3 body)) return;
            VfxDraw.Begin(new Vector2(body.x, body.z));
            foreach (string name in Hands)
            {
                if (!clip.TryPart(name, out Vector3 hand)) continue;
                float side = hand.x < body.x ? -1f : 1f;
                var shoulder = new Vector2(body.x + side * ShoulderAcross, body.z + ShoulderNorth);
                var palm = new Vector2(hand.x, hand.z);
                Vector2 d = palm - shoulder;
                if (d.magnitude <= WristGap) continue;
                PainGraphics.Sleeve(shoulder, palm - d.normalized * WristGap, hand.y - UnderHand);
            }
        }
    }
}
