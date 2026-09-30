using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Vector shove picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/accelerator-vector-shove.js). Seconds in, numbers out, no drawing.
    ///
    /// The picture's clock is 0 at the touch. The chosen line and the arm start <see cref="LineLead"/>
    /// and <see cref="ArmLead"/> before it; the throw starts <see cref="Touch"/> after it and moves at
    /// <see cref="Speed"/> cells/s, so it arrives at <see cref="Arrive"/> and the picture ends
    /// <see cref="Tail"/> later. The mace-hit flash and the force-returned window ring are on their own
    /// clocks (<see cref="HitFlashLife"/>, <see cref="Window"/>). None of this is balance: distances,
    /// damage, stun and the window's length in the rule are the ability's XML.
    /// </summary>
    public static class VectorShove
    {
        /// <summary>Touch to throw; the force-returned window; the picture after the arrival.</summary>
        public const float Touch = 0.12f, Window = 1f, Tail = 2f;
        /// <summary>Cells per second of the flying body and its speed lines.</summary>
        public const float Speed = 20f;
        /// <summary>A pawn in the path slides Knock cells sideways over KnockTime s.</summary>
        public const float Knock = 1f, KnockTime = 0.2f;
        /// <summary>The line shows from LineLead s before the touch and the arm from ArmLead s; the arm reaches ArmReach cells.</summary>
        public const float LineLead = 0.2f, ArmLead = 0.1f, ArmReach = 0.5f;
        /// <summary>The touch flash: its life, and how far from Accelerator the contact is, along the way to the target.</summary>
        public const float TouchLife = 0.3f, Contact = 0.75f;
        /// <summary>The mace-hit flash: its life, and how far from Accelerator toward the attacker.</summary>
        public const float HitFlashLife = 0.25f, HitFlashAt = 0.3f;
        /// <summary>The window ring: radius at the start, how much it closes, its centre above the pawn's point.</summary>
        public const float WindowRadius = 0.75f, WindowClose = 0.35f, WindowUp = 0.3f;
        /// <summary>A thrown body stops with its back this far short of the wall face, and lies this far back from where it stopped.</summary>
        public const float StopShort = 0.15f, LieBack = 0.25f;
        /// <summary>The sketch's squash on the wall face and slide down it: the stun stars start after both. The squash itself is not drawn.</summary>
        public const float Squash = 0.1f, Drop = 0.15f;
        /// <summary>The sketch shakes the wall cells this much for 0.15 s. Not drawn: a real wall cannot be moved.</summary>
        public const float WallShake = 0.05f;
        public const int Cracks = 5, Chips = 6, BonusChips = 12, ChunkChips = 4, SpeedLines = 5, StartDust = 8;
        public const int WallDust = 10, BonusWallDust = 16, Lines = 4, BonusLines = 8, Puffs = 5, BonusPuffs = 7, ChunkPuffs = 6;
        /// <summary>Impact lines and dust puffs last this long.</summary>
        public const float ImpactLife = 0.22f, PuffLife = 0.6f;
        /// <summary>The thrown chunk: its size, the arc's top, and how far to the side of what it hit it lies.</summary>
        public const float ChunkSize = 0.8f, ChunkArc = 1.1f, ChunkAside = 0.4f;
        /// <summary>What stays (the dent, cracks, chips, landing dust) fades over the picture's last FadeOut s.</summary>
        public const float FadeOut = 0.5f;
        /// <summary>Camera shakes: the mace hit, the throw, the landing, the wall slam, the wall slam with force returned.</summary>
        public const float MaceShake = 0.03f, ThrowShake = 0.03f, LandShake = 0.06f, SlamShake = 0.1f, BonusSlamShake = 0.14f;

        public static readonly Color WallDustColour = new Color(0.74f, 0.72f, 0.69f);

        // The preview's script, the sketch's defaults: the mace lands Lead s in, the touch follows React
        // later (ChunkReact for the chunk, which nobody hit); a pawn is thrown Cells, a chunk ChunkCells.
        // The wall is WallAt cells from the target (face half a cell nearer), the shooter ShooterAt, the
        // chunk stops ChunkShort before him. Liners: cells along, across, side knocked to.
        public const float Lead = 0.3f, React = 0.4f, ClosedReact = 1.2f, ChunkReact = 0.2f;
        public const float Cells = 8f, ChunkCells = 12f, WallAt = 5f, ShooterAt = 10f, ChunkShort = 0.6f;
        public static readonly float[,] Liners = { { 3f, 0.2f, 1f }, { 5.5f, -0.25f, -1f } };
        /// <summary>The sketch's chosen cell is the middle of the throw: Accelerator stands this far behind it.</summary>
        public const float Half = 4f, ChunkHalf = 6f;

        /// <summary>When a throw of <paramref name="stop"/> cells arrives, on the picture's clock.</summary>
        public static float Arrive(float stop) => Touch + stop / Speed;

        public static float End(float stop) => Arrive(stop) + Tail;

        /// <summary>Cells the body has flown at <paramref name="s"/>.</summary>
        public static float Flown(float s, float stop) => s < Touch ? 0f : Mathf.Min(stop, (s - Touch) * Speed);

        /// <summary>When the body passes a pawn <paramref name="along"/> cells down the line.</summary>
        public static float PassAt(float along) => Touch + along / Speed;

        /// <summary>How far, 0 to 1, a pawn <paramref name="along"/> cells down the line has been knocked aside.</summary>
        public static float Knocked(float s, float along) => s < PassAt(along) ? 0f : VfxMath.Smooth((s - PassAt(along)) / KnockTime);

        /// <summary>A pawn thrown <paramref name="cells"/> toward a wall face <paramref name="face"/> cells away stops here.</summary>
        public static float Stop(float cells, float face) => Mathf.Min(cells, face - StopShort);

        public static bool ReachesWall(float cells, float face) => cells >= face - StopShort;

        /// <summary>The target hit Accelerator <paramref name="react"/> s before the touch: the force is returned.</summary>
        public static bool Returned(float react) => react <= Window;

        /// <summary>The chosen line: 0 to 1.</summary>
        public static float LineShow(float s) =>
            s < -LineLead || s >= Touch + 0.25f ? 0f : VfxMath.Smooth((s + LineLead) / 0.12f) * (1f - VfxMath.Smooth((s - Touch - 0.05f) / 0.2f));

        /// <summary>The arm's reach in cells.</summary>
        public static float ArmOut(float s) =>
            s < -ArmLead || s >= Touch + 0.3f ? 0f : ArmReach * VfxMath.Smooth((s + ArmLead) / 0.1f) * (1f - VfxMath.Smooth((s - Touch - 0.1f) / 0.2f));

        /// <summary>What stays is drawn at this strength: 1, then down to 0 over the picture's last <see cref="FadeOut"/> s.</summary>
        public static float Linger(float s, float stop) => 1f - VfxMath.Smooth((s - (End(stop) - FadeOut)) / FadeOut);

        /// <summary>The camera shake at the arrival.</summary>
        public static float ArriveShake(bool wall, bool returned) => wall ? (returned ? BonusSlamShake : SlamShake) : LandShake;
    }
}
