using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Timing and geometry of Paper Shroud: seconds in, numbers out, no drawing and no map. The port
    /// of Tools/VfxLab/web/sketches/paper-bomb-shroud.js; the constants are that sketch's defaults.
    /// How long the target is held and the radius are the ability's (XML).
    /// </summary>
    public static class PaperBombShroudTiming
    {
        public const int Sheets = 6;
        /// <summary>The first tag leaves the hand, as in the RimArt_TagFan clip; one tag's flight; the gap between tags.</summary>
        public const float Wind = 0.35f, Fly = 0.55f, Stagger = 0.09f;
        public const float HandOut = 0.35f, HandHeight = 0.4f, Arc = 0.9f;

        /// <summary>
        /// The RimArt_TagFan clip's hand as the fan sweeps and the tags leave (make_paper_bomb_anim.py): seconds, reach,
        /// side, screen lift, and the body's lean. In game each tag leaves from the hand at its LeaveAt.
        /// </summary>
        private static readonly float[,] ClipSweep =
        {
            { 0.35f, 0.16f, 0.10f, 0.20f, 0.00f }, { 0.50f, 0.42f, 0.00f, 0.28f, 0.05f },
            { 0.65f, 0.48f, -0.12f, 0.27f, 0.06f }, { 0.80f, 0.40f, -0.22f, 0.20f, 0.07f },
        };

        /// <summary>The fan's hand at <paramref name="seconds"/>: straight lines between the clip's poses.</summary>
        public static void ClipHandAt(float seconds, out float reach, out float side, out float lift)
        {
            int last = ClipSweep.GetLength(0) - 1, i = 0;
            while (i < last - 1 && seconds > ClipSweep[i + 1, 0]) i++;
            float u = Mathf.Clamp01((seconds - ClipSweep[i, 0]) / (ClipSweep[i + 1, 0] - ClipSweep[i, 0]));
            reach = Mathf.Lerp(ClipSweep[i, 1] + ClipSweep[i, 4], ClipSweep[i + 1, 1] + ClipSweep[i + 1, 4], u);
            side = Mathf.Lerp(ClipSweep[i, 2], ClipSweep[i + 1, 2], u);
            lift = Mathf.Lerp(ClipSweep[i, 3], ClipSweep[i + 1, 3], u);
        }
        /// <summary>Tag scale once stuck (0.34 x 0.12 cells), and how much bigger the flash of six tags is than one.</summary>
        public const float OnBody = 0.48f, Power = 1.5f;
        public const float Aftermath = 2.2f, BurstShake = 0.22f;

        /// <summary>Where each tag sticks on a pawn, as drawn offsets from its feet, and its angle: head, chest, belly, two sides, legs.</summary>
        public static readonly Vector3[] Slots =
        {
            new Vector3(0f, 0.6f, 8f), new Vector3(-0.09f, 0.36f, -38f), new Vector3(0.09f, 0.22f, 24f),
            new Vector3(-0.13f, 0.1f, 68f), new Vector3(0.13f, 0.44f, -62f), new Vector3(0f, -0.02f, 4f),
        };
        /// <summary>How far each tag's path swings to the side at a throw of 5 cells.</summary>
        public static readonly float[] Bulge = { 1.3f, -1.1f, 0.7f, -1.6f, 1.7f, -0.5f };

        // The preview's script.
        public const float ScriptDistance = 5f, ScriptHeld = 2f, ScriptRadius = 0.9f;

        public static float LeaveAt(int sheet) => Wind + sheet * Stagger;
        public static float FirstLand => Wind + Fly;
        public static float LastLand => FirstLand + (Sheets - 1) * Stagger;
        public static float BurstAt(float held) => FirstLand + held;
        public static float SealAt(float held) => BurstAt(held) - PaperBombGraphics.Burn - PaperBombGraphics.Seal;
        public static float Duration(float held) => BurstAt(held) + Aftermath;
    }
}
