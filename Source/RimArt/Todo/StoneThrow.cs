using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the stone throw picture (Mark, "stone" in game), the defaults of the lab
    /// sketch (Tools/VfxLab/web/sketches/todo-stone-throw.js). Seconds in, numbers out, no drawing.
    ///
    /// The clock starts with the warmup. The clips are MarkFlick's: the stone leaves the hand at
    /// <see cref="Release"/> and lands as the warmup ends (the AbilityDef's 0.5 s,
    /// <see cref="Place"/>); a stone taken back leaves its cell at the warmup's end and is caught
    /// <see cref="CatchFlight"/> later. None of this is balance: range, cooldown, stones held and
    /// their life are on the AbilityDef and AnchorGeneExtension.
    /// </summary>
    public static class StoneThrow
    {
        public const float Release = MarkFlick.Release, Place = MarkFlick.Place, FlickLength = MarkFlick.FlickLength;
        public const float CatchFlight = 0.12f, CatchLength = MarkFlick.CatchLength;
        /// <summary>The previews hold the result this long after the clip.</summary>
        public const float Tail = 1.4f;
        /// <summary>The stone shows in the hand from here; height of the hand; the arc's height at 5 cells or more.</summary>
        public const float Charge = 0.1f, HandH = 0.4f, Arc = 0.35f;
        /// <summary>The stone on the ground (the item's drawSize) and in the hand.</summary>
        public const float StoneRest = 0.45f, StoneHand = 0.3f;
        public const float LandLife = 0.3f, Breath = 2.4f, GlintEvery = 3f, GlintLife = 0.25f;
        // The aura's tongues flicker Flicker times a second; the release blade lasts BladeLife; the
        // spin ring is SpinRing cells round the stone; a thrown stone touches down Skid cells short of
        // its cell and slides in over SkidTime while its spin dies away (SpinDecay s).
        public const float Flicker = 15f, BladeLife = 0.1f, SpinRing = 0.28f, Skid = 0.18f, SkidTime = 0.2f, SpinDecay = 0.12f, Absorb = 0.14f;
        /// <summary>From here before the warmup's end a stone being taken back flares.</summary>
        public const float FlareLead = 0.2f;
        public const float Spin = 1080f, Streak = 0.35f, Land = 0.6f;
        /// <summary>The preview throws this far, due east of Todo.</summary>
        public const float Distance = 6f;

        public static float Duration(bool catching) => (catching ? CatchLength : FlickLength) + Tail;

        public static float CatchTime(float warmup) => warmup + CatchFlight;

        /// <summary>How high the arc goes for a throw this long.</summary>
        public static float High(float distance) => Arc * Mathf.Min(1f, distance / 5f);

        // Where the clips have the main hand, every 0.05 s, in the aim's frame: reach along the aim,
        // offset to its left, and lift up the screen. Read off the lab's RimArt_MarkFlick and
        // RimArt_MarkCatch facing east and north; those two give all three numbers exactly, and the
        // south clips land on the same numbers. MarkFlick.Hand is the release and catch rows.
        private static readonly float[,] FlickHand =
        {
            { 0.12f, -0.23f, 0f }, { 0.118f, -0.201f, 0.02f }, { 0.107f, -0.136f, 0.058f }, { 0.08f, -0.066f, 0.082f },
            { 0.026f, 0.01f, 0.085f }, { -0.032f, 0.087f, 0.081f }, { -0.055f, 0.12f, 0.08f }, { 0.173f, 0.01f, 0.152f },
            { 0.474f, -0.118f, 0.252f }, { 0.555f, -0.148f, 0.272f }, { 0.52f, -0.15f, 0.25f }, { 0.449f, -0.167f, 0.2f },
            { 0.362f, -0.187f, 0.141f }, { 0.285f, -0.203f, 0.092f }, { 0.21f, -0.216f, 0.049f }, { 0.147f, -0.226f, 0.014f },
            { 0.12f, -0.23f, 0f },
        };
        private static readonly float[,] CatchHand =
        {
            { 0.12f, -0.23f, 0f }, { 0.14f, -0.223f, 0.026f }, { 0.189f, -0.207f, 0.088f }, { 0.253f, -0.187f, 0.161f },
            { 0.317f, -0.17f, 0.22f }, { 0.39f, -0.157f, 0.259f }, { 0.467f, -0.146f, 0.286f }, { 0.517f, -0.139f, 0.302f },
            { 0.533f, -0.138f, 0.307f }, { 0.533f, -0.139f, 0.303f }, { 0.527f, -0.14f, 0.3f }, { 0.514f, -0.14f, 0.301f },
            { 0.492f, -0.14f, 0.301f }, { 0.457f, -0.143f, 0.298f }, { 0.377f, -0.129f, 0.283f }, { 0.259f, -0.087f, 0.267f },
            { 0.153f, -0.059f, 0.239f }, { 0.105f, -0.089f, 0.171f }, { 0.109f, -0.178f, 0.06f }, { 0.12f, -0.23f, 0f },
        };

        /// <summary>
        /// The screen point of Todo's hand <paramref name="seconds"/> into the throw (or the catch) for
        /// Todo standing at <paramref name="stands"/> aiming at <paramref name="target"/>. A westward aim
        /// plays the east clip mirrored, as MarkFlick.Hand works it out.
        /// </summary>
        public static Vector2 Hand(Vector2 stands, Vector2 target, float seconds, bool catching)
        {
            float[,] table = catching ? CatchHand : FlickHand;
            int rows = table.GetLength(0);
            float at = Mathf.Clamp(seconds / 0.05f, 0f, rows - 1);
            int i = Mathf.Min(rows - 2, Mathf.FloorToInt(at));
            float t = at - i;
            float reach = Mathf.Lerp(table[i, 0], table[i + 1, 0], t), side = Mathf.Lerp(table[i, 1], table[i + 1, 1], t),
                lift = Mathf.Lerp(table[i, 2], table[i + 1, 2], t);

            Vector2 aim = target - stands;
            aim = aim.sqrMagnitude < 1e-6f ? Vector2.right : aim.normalized;
            bool mirrored = Mathf.Abs(aim.x) >= Mathf.Abs(aim.y) && aim.x < 0f;
            if (mirrored) aim.x = -aim.x;
            float east = reach * aim.x - side * aim.y, north = reach * aim.y + side * aim.x + lift;
            return new Vector2(stands.x + (mirrored ? -east : east), stands.y + north);
        }
    }
}
