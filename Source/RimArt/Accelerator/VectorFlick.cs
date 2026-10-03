using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// One Vector Flick as the picture needs it. The game fills it when the warm-up starts (and moves
    /// <see cref="Target"/> each frame); the preview fills it from the sketch's layout. Several can be
    /// drawn at once: each call draws one shot and nothing is kept between calls.
    /// </summary>
    public struct VectorFlickShot
    {
        /// <summary>Accelerator's ground point (his DrawPos on the map), and the aim in degrees (0 east, 90 north).</summary>
        public Vector2 Feet;
        public float Aim;
        /// <summary>Cells from <see cref="Feet"/> to the target; it times the hit.</summary>
        public float Distance;
        /// <summary>Seconds of warm-up (the kick lands at its end), and the pebble's speed in cells per second.</summary>
        public float Warmup, Speed;
        /// <summary>The drawn leg's trouser and shoe colours (the preview uses the sketch's).</summary>
        public Color Pants, Shoe;
        /// <summary>The target's live ground point, so the hit lands on the real pawn; null uses <see cref="Feet"/> + aim x <see cref="Distance"/>.</summary>
        public Vector2? Target;
        /// <summary>Varies the dust, bits and sparks between flicks (the sketch's kick index).</summary>
        public int Seed;

        public float KickAt => Warmup;
        public float HitAt => VectorFlick.HitAt(Warmup, Distance, Speed);
        public float Duration => HitAt + VectorFlick.Tail;
    }

    /// <summary>
    /// Times and sizes of the Vector Flick picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/accelerator-vector-flick.js). Seconds in, numbers out, no drawing.
    ///
    /// The clock starts with the warm-up: the pebble pops out of the floor, his grip closes on it
    /// <see cref="Grip"/> before the kick, the kick lands at the warm-up's end and the pebble flies at
    /// the speed to the target's chest. Heights are the lab's (drawn x Lift north). None of this is
    /// balance: range, damage, warm-up, speed and cooldown are on the AbilityDef; the warm-up and speed
    /// here are the sketch's defaults for the previews.
    /// </summary>
    public static class VectorFlick
    {
        public const float Warmup = 0.3f, Speed = 42f;
        /// <summary>The pebble takes Pop to reach knee height; the picture holds Tail after the hit and fades its last FadeOut.</summary>
        public const float Pop = 0.15f, Tail = 1.5f, FadeOut = 0.4f;
        /// <summary>The pit is Foot cells along the aim; the pebble rises to Knee + Apex; the chest is Chest 0.3 / Lift 0.6 up.</summary>
        public const float Foot = 0.35f, Knee = 0.35f, Apex = 0.05f, ChestUp = 0.5f;
        /// <summary>Trail length in cells, the pebble's size, the bits it breaks into, the sparks at the hit.</summary>
        public const float TrailCells = 3f, Pebble = 0.16f;
        public const int Bits = 4, Sparks = 6;
        /// <summary>The grip ring closes over this long before the kick; the kick flash and the hit last this long.</summary>
        public const float Grip = 0.06f, KickLife = 0.3f, HitLife = 0.5f, PathFade = 0.4f;
        /// <summary>The leg shows until this long after the kick and fades over LegFade.</summary>
        public const float LegHold = 0.3f, LegFade = 0.1f;
        /// <summary>Camera shakes at the kick and the hit.</summary>
        public const float KickShake = 0.02f, HitShake = 0.03f;
        /// <summary>The sketch's range and cooldown, for the previews' layout.</summary>
        public const float MaxRange = 24.9f, Cooldown = 2f;

        /// <summary>When the pebble reaches a target <paramref name="distance"/> cells away.</summary>
        public static float HitAt(float warmup, float distance, float speed) => warmup + Mathf.Max(0f, distance - Foot) / Mathf.Max(0.01f, speed);

        /// <summary>The pebble's height (lab units) before the kick, <paramref name="age"/> s into the warm-up.</summary>
        public static float PopHeight(float age, float warmup) =>
            Knee * VfxMath.Smooth(age / Pop) + Apex * Mathf.Clamp01((age - Pop) / Mathf.Max(0.01f, warmup - Pop));

        // The kicking leg, [seconds, reach along the aim, lift]; reach is negative behind him. Two keys
        // are set from the warm-up: the draw-back ends 0.04 s before the kick and the kick lands at it.
        private static readonly float[,] LegKeys =
        {
            { 0f, 0f, 0f }, { Pop * 0.5f, 0.3f, 0.03f }, { Pop, 0.22f, 0.1f }, { -0.04f, -0.3f, 0.16f },
            { 0f, Foot, 0.36f }, { 0.05f, 0.62f, 0.44f }, { 0.14f, 0.6f, 0.42f }, { 0.34f, 0.05f, 0f },
        };

        /// <summary>
        /// The shoe <paramref name="age"/> s into the warm-up: how far along the aim (negative is behind
        /// him) and how high (lab units). A smooth step between keys, as the sketch's keyed().
        /// </summary>
        public static void Leg(float age, float warmup, out float reach, out float lift)
        {
            int n = LegKeys.GetLength(0);
            float Key(int i) => i < 3 ? LegKeys[i, 0] : warmup + LegKeys[i, 0];
            if (age <= Key(0)) { reach = LegKeys[0, 1]; lift = LegKeys[0, 2]; return; }
            for (int i = 1; i < n; i++)
            {
                float t1 = Key(i);
                if (age > t1) continue;
                float t0 = Key(i - 1), u = VfxMath.Smooth((age - t0) / Mathf.Max(1e-4f, t1 - t0));
                reach = Mathf.Lerp(LegKeys[i - 1, 1], LegKeys[i, 1], u);
                lift = Mathf.Lerp(LegKeys[i - 1, 2], LegKeys[i, 2], u);
                return;
            }
            reach = LegKeys[n - 1, 1];
            lift = LegKeys[n - 1, 2];
        }

        /// <summary>How much of the leg shows, <paramref name="age"/> s into the warm-up.</summary>
        public static float LegShown(float age, float warmup) =>
            age < 0f || age >= warmup + LegHold + LegFade ? 0f : 1f - VfxMath.Smooth((age - warmup - LegHold) / LegFade);
    }
}
