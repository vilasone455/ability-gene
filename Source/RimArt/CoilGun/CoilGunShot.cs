using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Timing of the Coil Gun's normal shot as the picture needs it: the bolt is the projectile's
    /// own drawing, so in game its position comes from the projectile, and only the preview uses the
    /// flight times here. The numbers match AG_CoilGun_Bolt and the gun's verb: speed 80 (0.8 cells a
    /// tick, 48 a second), a 2-round burst 8 ticks apart.
    /// </summary>
    public static class CoilGunShotTiming
    {
        /// <summary>Cells a second: the projectile's speed 80 is 0.8 cells a tick.</summary>
        public const float Speed = 48f;
        /// <summary>The trail behind the head, cells.</summary>
        public const float Trail = 1.5f;
        /// <summary>A scorch stays this long after a hit, then fades 0.6 s.</summary>
        public const float ScorchStay = 0.8f;
        public const float ImpactEnd = ScorchStay + 0.6f;

        // The preview's script: two rounds 8 ticks apart at a pawn 8 cells east of the shooter.
        public const float ScriptLead = 0.2f, ScriptGap = 8f / 60f, ScriptDistance = 8f;
        public const int ScriptRounds = 2;
        /// <summary>The preview's centre is the middle of the line from the shooter's feet to the target.</summary>
        public const float ScriptCentreAlong = ScriptDistance / 2f;

        public static float Fired(int round) => ScriptLead + round * ScriptGap;
        public static float Flight(float cells) => cells / Speed;
        public static float Impact(int round) => Fired(round) + Flight(ScriptDistance);
        public static float ScriptEnd => Impact(ScriptRounds - 1) + ImpactEnd;
    }
}
