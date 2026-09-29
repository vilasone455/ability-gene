using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// One round in a changed group, as the Apply picture needs it. Points are where the round is
    /// drawn: in game the round's own position (a vanilla bullet is drawn at its ExactPosition, which
    /// is about chest height on the shooter and the target); in the preview the sketch's ground point
    /// plus Chest, as the sketch flies its rounds at chest height.
    /// </summary>
    public struct VectorApplyRound
    {
        /// <summary>Where it was caught (CapturedProjectile.Position); the corner is drawn here.</summary>
        public Vector2 Caught;
        /// <summary>Unit headings before and after Apply (CapturedProjectile.Heading and HeadingAfter).</summary>
        public Vector2 OldHeading, NewHeading;
        /// <summary>The group's force (0.25, 0.5, 1 or 2): the trail is <see cref="VectorApply.TrailPerForce"/> x force cells.</summary>
        public float Force;
        /// <summary>The round's speed after Apply, in cells per second on the picture's clock.</summary>
        public float Speed;
        /// <summary>Where the round is now, updated each frame while it exists; after that, the last point it had.</summary>
        public Vector2 Live;
        /// <summary>Whether the real round still exists. After it is gone the trail's tail runs in to <see cref="Live"/> at <see cref="Speed"/>.</summary>
        public bool Exists;
        /// <summary>Varies the star's spikes between rounds (the sketch's round index).</summary>
        public int Seed;
    }

    /// <summary>
    /// The manipulation's Apply, as the picture needs it: filled when Apply is pressed, with one entry
    /// per round in a changed group (rounds in no group get nothing). The game updates each round's
    /// Live and Exists every frame.
    /// </summary>
    public struct VectorApplyShot
    {
        /// <summary>Accelerator's ground point (his DrawPos on the map).</summary>
        public Vector2 Feet;
        /// <summary>The strain this Apply spent (VectorEditDefaults.StrainCosts: 0.08, 0.24, 0.48, 0.80): the strain ring's size.</summary>
        public float Strain;
        /// <summary>The catch radius in cells (VectorEditDefaults.ScanRadiusCells, 12): the ring that flashes.</summary>
        public float Reach;
        public VectorApplyRound[] Rounds;
    }

    /// <summary>
    /// Times and sizes of the Apply picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/accelerator-vector-apply.js). Seconds in, numbers out, no drawing.
    ///
    /// The clock starts at Apply. The corner strokes, the reach ring and the strain ring last
    /// <see cref="FlashLife"/>, the stars <see cref="StarLife"/>; each trail follows its round until
    /// the round is gone and its tail has run in. None of this is balance: range, force, strain and
    /// the catch radius are in VectorEditDefaults.
    /// </summary>
    public static class VectorApply
    {
        /// <summary>The trail is TrailPerForce x force cells long (0.5 / 1 / 2 / 4), so the force can be read from it.</summary>
        public const float TrailPerForce = 2f;
        /// <summary>The previews hold this long after the last trail has run in.</summary>
        public const float Tail = 0.6f;
        /// <summary>Strokes and rings last FlashLife; the star StarLife; the stroke's new leg grows over Grow.</summary>
        public const float FlashLife = 0.3f, StarLife = 0.15f, Grow = 0.06f;
        /// <summary>The stroke's legs along the old and the new heading, in cells.</summary>
        public const float StrokeIn = 0.8f, StrokeOut = 0.8f;
        /// <summary>The star's spikes.</summary>
        public const int Spikes = 7;
        /// <summary>The strain ring grows to StrainBase + StrainPer x strain cells; it rises from HeadUp above his ground point (the sketch's head).</summary>
        public const float StrainBase = 0.4f, StrainPer = 1.6f, HeadUp = 0.6f;
        /// <summary>The reach ring swells by this share as it fades.</summary>
        public const float ReachSwell = 0.03f;
        /// <summary>Camera shake at Apply.</summary>
        public const float Shake = 0.025f;

        public static float TrailLength(float force) => TrailPerForce * force;

        /// <summary>How far along its new heading the round is (cells from the catch point), and where the trail's tail is.</summary>
        public static void Trail(in VectorApplyRound round, float seconds, out float head, out float tail)
        {
            head = Mathf.Max(0f, Vector2.Dot(round.Live - round.Caught, round.NewHeading));
            float length = TrailLength(round.Force);
            // While the round flies the trail keeps its length; once it is gone the tail runs on at the round's speed.
            tail = round.Exists ? Mathf.Max(0f, head - length) : Mathf.Clamp(Mathf.Max(head - length, seconds * round.Speed - length), 0f, head);
        }

        /// <summary>True once the flashes are over and every round is gone with its trail run in: the caller can stop drawing.</summary>
        public static bool Finished(in VectorApplyShot shot, float seconds)
        {
            if (seconds < FlashLife) return false;
            if (shot.Rounds == null) return true;
            for (int i = 0; i < shot.Rounds.Length; i++)
            {
                if (shot.Rounds[i].Exists) return false;
                Trail(shot.Rounds[i], seconds, out float head, out float tail);
                if (tail < head) return false;
            }
            return true;
        }
    }
}
