using System;
using RimArt;

// The mimic beacon's taunt is one number, and the number is only correct relative to a formula
// that lives in the game rather than in this mod. This harness holds a copy of that formula and
// asserts the three relationships the design depends on, so that changing MimicDefaults.
// PriorityFactor fails here rather than quietly changing how raiders behave.
//
// Transcribed from Verse.AI.AttackTargetFinder.GetShootingTargetScore, RimWorld 1.6:
//
//     float num = 60f;
//     num -= Mathf.Min(distance, 40f);
//     if (target.TargetCurrentlyAimingAt == searcher.Thing) num += 10f;
//     if (searcher.LastAttackedTarget == target.Thing
//         && TicksGame - searcher.LastAttackTargetTick <= 300) num += 40f;
//     num -= CoverUtility.CalculateOverallBlockChance(...) * 10f;
//     ... Pawn-only terms ...
//     return num * target.TargetPriorityFactor;
//
// If this stops matching the game, the numbers below are the ones to re-derive.

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

const float EngagedBonus = 40f;      // fired on within the last 300 ticks
const float AimingAtBonus = 10f;     // the target is aiming back at the shooter
const float MaxCoverPenalty = 10f;   // full cover between shooter and target

static float Score(float distance, float priorityFactor,
                   bool engaged = false, bool aimingBack = false, float coverBlockChance = 0f)
{
    float score = 60f - Math.Min(distance, 40f);
    if (aimingBack) score += AimingAtBonus;
    if (engaged) score += EngagedBonus;
    score -= coverBlockChance * MaxCoverPenalty;
    return score * priorityFactor;
}

float F = MimicDefaults.PriorityFactor;
Console.WriteLine($"TargetPriorityFactor = {F}");

Check(F > 1f, "A factor at or below 1 is not a taunt at all");

// Both bounds are written for the case that is worst for the decoy, not the average one, and
// they are checked at every distance rather than at a few round ones - the upper bound is
// hardest at point blank and the lower bound is hardest at the 40-cell clamp, so a test that
// samples only the middle passes values that break at both ends. 1.75 is such a value.
for (int d = 0; d <= 40; d++)
{
    // Worst case for beating an unengaged target: it is shooting back, so it holds the +10.
    float idle = Score(d, 1f, aimingBack: true);
    float decoy = Score(d, F);
    Check(decoy > idle,
        $"At {d} cells the decoy must out-score an unengaged colonist ({decoy:0.00} vs {idle:0.00})");

    // Worst case for losing to an engaged target: it is NOT shooting back, so it holds only
    // the +40. This is the end of "strong preference, not compulsion" - a raider who has
    // already reached somebody must not turn around.
    float engaged = Score(d, 1f, engaged: true);
    Check(decoy < engaged,
        $"At {d} cells the decoy must NOT out-score an engaged colonist ({decoy:0.00} vs {engaged:0.00})");
}

foreach (float d in new[] { 0f, 10f, 20f, 30f, 40f })
{
    Console.WriteLine($"  {d,2} cells: decoy {Score(d, F),6:0.0} | idle colonist "
        + $"{Score(d, 1f, aimingBack: true),6:0.0} | engaged {Score(d, 1f, engaged: true),6:0.0}");
}

// The window the two bounds leave, printed so a future change can see how much room it has.
double lower = 0, upper = double.MaxValue;
for (int d = 0; d <= 40; d++)
{
    lower = Math.Max(lower, 1 + 10.0 / (60 - d));
    upper = Math.Min(upper, 1 + 40.0 / (60 - d));
}
Console.WriteLine($"  valid range for the factor: ({lower:0.000}, {upper:0.000})");
Check(F > lower && F < upper, "Factor must sit inside the window both bounds leave");

// How far forward the beacon has to be thrown, which falls out of the same two terms and is
// the one piece of this that a player has to know.
//
// Distance is worth a point a cell, so a decoy further from the shooter than the colonist it is
// covering is spending its multiplier on making up that gap. Solving
//
//     (60 - d - k) * F  >  (60 - d) + 10
//
// for k gives the number of cells the decoy may sit *behind* the colonist and still take the
// shot. It shrinks as the fight gets longer-ranged, and past about 43 cells it goes negative -
// at which point the decoy has to be nearer the shooter than the person it is protecting.
foreach (int d in new[] { 5, 10, 15, 20, 25, 30 })
{
    int margin = 0;
    while (Score(d + margin + 1, F) > Score(d, 1f, aimingBack: true)) margin++;
    Console.WriteLine($"  colonist at {d,2} cells: decoy may be up to {margin} cells further back");
    // Four cells is the floor this keeps across the band a firefight actually happens in.
    // In practice the beacon is thrown *toward* the enemy from behind the line, which spends
    // the 12.9-cell throw range closing the gap rather than opening it, so the slack below is
    // the pessimistic reading - it is the case where somebody lobs one sideways.
    Check(margin >= 4,
        $"A decoy covering a colonist at {d} cells should have at least 4 cells of slack, got {margin}");
}

// The case the design accepts as a loss: a colonist in heavy cover who is already engaged, at
// the same distance as the decoy. Recorded rather than asserted away - if this ever needs to
// stop happening, lower the factor rather than adding a rule.
float covered = Score(10f, 1f, engaged: true, aimingBack: true, coverBlockChance: 1f);
float sameDistanceDecoy = Score(10f, F);
Console.WriteLine(sameDistanceDecoy > covered
    ? $"  note: decoy {sameDistanceDecoy:0.0} still beats an engaged colonist in full cover {covered:0.0}"
    : $"  note: an engaged colonist in full cover {covered:0.0} holds off the decoy {sameDistanceDecoy:0.0}");

Console.WriteLine("Mimic beacon targeting checks passed.");
