using UnityEngine;

namespace RimArt
{
    /// <summary>One Stake as the picture needs it. Ground points are x east, y north.</summary>
    public struct ChainStakeShot
    {
        /// <summary>The holder's feet now.</summary>
        public Vector2 Caster;
        /// <summary>The pinned pawn's ground point at the yank; the stake is placed from it.</summary>
        public Vector2 Target;
        /// <summary>Where the pinned pawn is now. Null: the target point moved by <see cref="Strain"/>.</summary>
        public Vector2? BodyAt;
        /// <summary>Degrees from the holder to the target at the cast, 0 east, 90 north.</summary>
        public float Aim;
        /// <summary>Cells the pinned pawn leans away from the holder now (the preview's script).</summary>
        public float Strain;
        /// <summary>0 the chain is taut (holder 2 cells away or more), 1 slack (holder next to the pawn).</summary>
        public float Slack;
        public float Size;
        /// <summary>The pin in seconds; the floor ring shrinks over it.</summary>
        public float Pin;
        /// <summary>When the preview's sickle cut starts, seconds; negative draws no cut.</summary>
        public float SwingAt;
    }

    /// <summary>
    /// Timing of Stake: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/chain-sickle-stake.js; the constants are that sketch's defaults.
    ///   0.00 snagged, 0.30 yank (the coil tightens, the weight drops), 0.45 staked (weight in the
    ///   floor, crack, weapon dropped, the pin ring starts shrinking over the pin).
    /// The preview's script then has the pawn strain away at 1.2 and 2.4 s, the holder step in at
    /// 3.15 s and cut at 3.4 s; in game the cut is the holder's own melee attack.
    /// </summary>
    public static class ChainSickleStakeTiming
    {
        /// <summary>Snagged before the yank; the weight's fall; the coil closing on the chest.</summary>
        public const float Lead = 0.3f, Yank = 0.15f, Tighten = 0.2f;
        /// <summary>Walking to 1 cell; the cut.</summary>
        public const float StepIn = 0.25f, Swing = 0.12f;
        /// <summary>How far a pinned pawn leans away before the chain stops it, cells.</summary>
        public const float Strain = 0.18f;
        public const float StakeShake = 0.04f, CutShake = 0.05f;
        /// <summary>The pin ring's length when the rule gives no pin (the sketch's fallback).</summary>
        public const float NoPin = 1.5f;

        // The preview's script: the sketch's defaults.
        public const float ScriptDistance = 2f, ScriptSwingAt = 3.4f, ScriptHold = 1.0f;
        private static readonly float[] StrainAt = { 1.2f, 2.4f };

        public static float Yank0 => Lead;
        public static float Staked => Lead + Yank;
        public static float Step0(float swingAt) => swingAt - StepIn;
        public static float Cut(float swingAt) => swingAt + Swing * 0.6f;
        public static float ScriptEnd => ScriptSwingAt + Swing + ScriptHold + ChainSickleGraphics.Tail;

        /// <summary>The preview's straining pawn: how far it leans away now.</summary>
        public static float ScriptStrain(float s)
        {
            float strain = 0f;
            for (int i = 0; i < StrainAt.Length; i++)
            {
                float a = s - StrainAt[i];
                if (a >= 0f && a < 0.5f) strain += Strain * (a < 0.3f ? ChainSickleGraphics.SmoothStep(a / 0.3f) : 1f - ChainSickleGraphics.SmoothStep((a - 0.3f) / 0.12f));
            }
            return strain;
        }

        /// <summary>The preview's holder stepping in: 0 before, 1 once at 1 cell.</summary>
        public static float ScriptStep(float s, float swingAt) =>
            s < Step0(swingAt) ? 0f : ChainSickleGraphics.SmoothStep((s - Step0(swingAt)) / StepIn);
    }
}
