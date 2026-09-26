using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>One cone cell of Release, in cost order.</summary>
    public struct FlameConeCell
    {
        /// <summary>The cell's centre on the ground (x east, y north).</summary>
        public Vector2 At;
        /// <summary>The sketch's row along the aim (1 = next to the wearer) and column across it (-1, 0, 1).</summary>
        public int Row, Across;
    }

    /// <summary>One Release as the picture needs it.</summary>
    public sealed class FlameReleaseShot
    {
        /// <summary>The wearer's feet.</summary>
        public Vector2 Caster;
        /// <summary>Degrees of the cone's axis, 0 east, 90 north.</summary>
        public float Aim;
        public float StartHeat, MaxHeat = FlameGauntletTiming.ScriptMaxHeat, OverheatAt = FlameGauntletTiming.ScriptOverheatAt;
        /// <summary>The cone's length and width in cells, for the floor outline.</summary>
        public int Length = FlameReleaseTiming.ScriptLength, Width = FlameReleaseTiming.ScriptWidth;
        /// <summary>The cells that light, in cost order: nearest row first, the centre of a row before its sides.</summary>
        public readonly List<FlameConeCell> Cells = new List<FlameConeCell>();
        /// <summary>How many of <see cref="Cells"/> light. 0: refused, "too cold".</summary>
        public int Lit;
        public float Hold = FlameReleaseTiming.Hold;
        /// <summary>
        /// Previews only: the lit cells keep their picture fire to the end, as in the sketch. In game
        /// vanilla fires take over, so a cell's picture fire fades out after its flare.
        /// </summary>
        public bool DrawFires;
        /// <summary>Previews only: a pawn standing in this cone cell index catches fire when it lights; -1 none.</summary>
        public int PawnCell = -1;
        public Vector2? CasterAt;
    }

    /// <summary>
    /// Timing of Release: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/flame-gauntlet-release.js; the constants are that sketch's defaults.
    ///   0.00 rest, 0.20 wind-up 0.3 s, 0.50 release (flash, licks from the fist land 0.8 cells ahead
    ///   in 0.05 s), 0.55 the ground wave runs at length / 0.6 s cells a second and lights each cell
    ///   as it passes (a flare 1.5x settling over 0.6 s, a sheet of flame 1.4 s), it sinks where the
    ///   lit cells end (0.2 s), then the hold and the tail. Under the minimum heat nothing lights.
    /// In game the picture's clock starts at <see cref="FlameGauntletTiming.Lead"/> when the 0.3 s
    /// warmup begins, so the release is when the ability is applied.
    /// </summary>
    public static class FlameReleaseTiming
    {
        public const float Windup = 0.3f, Spread = 0.6f, Hold = 1.6f;
        /// <summary>Cells along the aim where the licks land and the wave starts.</summary>
        public const float Start = 0.8f;
        /// <summary>The middle of the front runs this far ahead of its edges.</summary>
        public const float Bow = 0.3f;
        /// <summary>A cell lights when the front is this far short of its centre.</summary>
        public const float Catch = 0.3f;
        /// <summary>A lit cell's fire starts 1 + Flare tall and settles over FlareFor.</summary>
        public const float Flare = 0.5f, FlareFor = 0.6f;
        public const float Sheet = 1.4f, Sink = 0.2f, Close = 0.3f;
        public const float JetLand = 0.05f, JetLife = 0.32f;
        public const float Shake = 0.05f;
        /// <summary>The sketch's cone and minimum heat; XML fields in game.</summary>
        public const int ScriptLength = 6, ScriptWidth = 3, ScriptMinHeat = 5;

        public static float Go => FlameGauntletTiming.Lead + Windup;
        public static float Speed(FlameReleaseShot shot) => shot.Length / Spread;

        /// <summary>The cone's cells as (row, across), in cost order: row 1 is the single cell next to the wearer, then rows of <paramref name="width"/>.</summary>
        public static List<Vector2Int> Pattern(int length, int width)
        {
            var cells = new List<Vector2Int>();
            int half = (width - 1) / 2;
            for (int row = 1; row <= length; row++)
            {
                cells.Add(new Vector2Int(row, 0));
                if (row == 1) continue;
                for (int k = 1; k <= half; k++)
                {
                    cells.Add(new Vector2Int(row, -k));
                    cells.Add(new Vector2Int(row, k));
                }
            }
            return cells;
        }

        /// <summary>How far the lit cells reach along the aim, cells: the last lit row's far edge.</summary>
        public static float Reach(FlameReleaseShot shot)
        {
            int row = 0;
            for (int i = 0; i < shot.Lit; i++) row = Mathf.Max(row, shot.Cells[i].Row);
            return shot.Lit > 0 ? row + 0.5f : 0f;
        }

        /// <summary>The front stops at the end of the lit cells.</summary>
        public static float Stop(FlameReleaseShot shot) =>
            shot.Lit > 0 ? Go + JetLand + (Reach(shot) - Start) / Speed(shot) : Go;

        public static float Result(FlameReleaseShot shot) => shot.Lit > 0 ? Stop(shot) + Sink : Go + 0.3f;
        public static float End(FlameReleaseShot shot) => Result(shot) + shot.Hold + FlameGauntletTiming.Tail;

        /// <summary>When cone cell <paramref name="i"/> lights: the bowed front comes within Catch of its centre.</summary>
        public static float EruptAt(FlameReleaseShot shot, int i)
        {
            FlameConeCell c = shot.Cells[i];
            float edge = (shot.Width - 1) / 2f + 0.5f, bow = Bow * (c.Across / edge) * (c.Across / edge);
            return Go + JetLand + Mathf.Max(0f, c.Row - Catch + bow - Start) / Speed(shot);
        }

        /// <summary>The meter at <paramref name="s"/>: one heat off per cell over 0.15 s from its light.</summary>
        public static float HeatAt(FlameReleaseShot shot, float s)
        {
            float heat = shot.StartHeat;
            for (int i = 0; i < shot.Lit; i++) heat -= Mathf.Clamp01((s - EruptAt(shot, i)) / 0.15f);
            return heat;
        }
    }
}
