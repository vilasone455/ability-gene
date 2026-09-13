using System;
using System.Collections.Generic;
using System.Linq;
using RimArt;

// Checks the retrieval hook's game-free rules: target eligibility, the 20 kg boundary, stack
// conservation, wound choice, the severity cap and the once-only penalty. The game-facing code
// fills in these facts from real pawns and items and must be tested in game; see
// docs/retrieval-hook-belt.md.

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception("FAILED: " + message);
}

// ---------------------------------------------------------------- pawn eligibility

HookPawnFacts Downed() => new HookPawnFacts
{
    Spawned = true, Downed = true, Humanlike = true, ColonyMember = true, Reservable = true,
};

// Colony slaves are counted as colony members by RetrievalTargets (Faction.OfPlayer or IsSlaveOfColony).
Check(RetrievalRules.PawnRefusal(Downed()) == null, "downed colonist or colony slave is valid");
var ally = Downed(); ally.ColonyMember = false; ally.AlliedFaction = true;
Check(RetrievalRules.PawnRefusal(ally) == null, "downed ally is valid");

var neutral = Downed(); neutral.ColonyMember = false;
Check(RetrievalRules.PawnRefusal(neutral) != null, "neutral or hostile pawn is refused");
var prisoner = Downed(); prisoner.Prisoner = true;
Check(RetrievalRules.PawnRefusal(prisoner) != null, "prisoner is refused");
var allyPrisoner = ally; allyPrisoner.Prisoner = true;
Check(RetrievalRules.PawnRefusal(allyPrisoner) != null, "prisoner from an allied faction is refused");
var animal = Downed(); animal.Humanlike = false;
Check(RetrievalRules.PawnRefusal(animal) != null, "animal or mechanoid is refused");
var standing = Downed(); standing.Downed = false;
Check(RetrievalRules.PawnRefusal(standing) != null, "standing pawn is refused");
var carried = Downed(); carried.Spawned = false;
Check(RetrievalRules.PawnRefusal(carried) != null, "carried or contained pawn is refused");
var dead = Downed(); dead.Dead = true;
Check(RetrievalRules.PawnRefusal(dead) != null, "dead pawn is refused");
var self = Downed(); self.IsCaster = true;
Check(RetrievalRules.PawnRefusal(self) != null, "caster is refused");
var taken = Downed(); taken.AlreadyRetrieved = true;
Check(RetrievalRules.PawnRefusal(taken) != null, "pawn already on another tether is refused");
var reserved = Downed(); reserved.Reservable = false;
Check(RetrievalRules.PawnRefusal(reserved) != null, "pawn reserved by someone else is refused");

// ---------------------------------------------------------------- item eligibility

HookItemFacts Item(HookItemKind kind, int stack, float mass) => new HookItemFacts
{
    Spawned = true, IsItem = true, Haulable = true, Kind = kind, StackCount = stack, MassPerUnit = mass,
    Reservable = true,
};

foreach (HookItemKind kind in new[] { HookItemKind.Weapon, HookItemKind.Apparel, HookItemKind.Medicine,
             HookItemKind.Food, HookItemKind.Resource, HookItemKind.Minified })
{
    Check(RetrievalRules.ItemRefusal(Item(kind, 1, 1f), out int n) == null && n == 1, kind + " is valid");
}
Check(RetrievalRules.ItemRefusal(Item(HookItemKind.Other, 1, 1f), out _) != null, "other item kinds are refused");
var chunk = Item(HookItemKind.Resource, 1, 15f); chunk.Chunk = true;
Check(RetrievalRules.ItemRefusal(chunk, out _) != null, "chunk is refused");
var corpse = Item(HookItemKind.Food, 1, 60f); corpse.Corpse = true;
Check(RetrievalRules.ItemRefusal(corpse, out _) != null, "corpse is refused");
var building = Item(HookItemKind.Other, 1, 5f); building.IsItem = false;
Check(RetrievalRules.ItemRefusal(building, out _) != null, "installed building or plant is refused");
var contained = Item(HookItemKind.Medicine, 5, 0.5f); contained.Spawned = false;
Check(RetrievalRules.ItemRefusal(contained, out _) != null, "contained item is refused");
var unhaulable = Item(HookItemKind.Resource, 1, 1f); unhaulable.Haulable = false;
Check(RetrievalRules.ItemRefusal(unhaulable, out _) != null, "unhaulable item is refused");
var itemTaken = Item(HookItemKind.Resource, 10, 1f); itemTaken.AlreadyRetrieved = true;
Check(RetrievalRules.ItemRefusal(itemTaken, out _) != null, "item already on another tether is refused");

// ---------------------------------------------------------------- mass boundaries

Check(RetrievalRules.ItemTakeCount(1, 20f, 20f) == 1, "exactly 20 kg is allowed");
Check(RetrievalRules.ItemTakeCount(1, 20.01f, 20f) == 0, "20.01 kg per unit is refused");
Check(RetrievalRules.ItemRefusal(Item(HookItemKind.Minified, 1, 20.01f), out _) != null, "too heavy item is refused with a reason");
Check(RetrievalRules.ItemTakeCount(75, 0.5f, 20f) == 40, "0.5 kg x 40 = 20 kg, not 39");
Check(RetrievalRules.ItemTakeCount(75, 0.1f, 20f) == 75, "light stack under the cap comes whole");
Check(RetrievalRules.ItemTakeCount(500, 0.1f, 20f) == 200, "0.1 kg x 200 = 20 kg, not 199");
Check(RetrievalRules.ItemTakeCount(30, 0.7f, 20f) == 28, "0.7 kg: 28 units = 19.6 kg");
Check(RetrievalRules.ItemTakeCount(12, 0f, 20f) == 12, "massless stack comes whole");
Check(RetrievalRules.ItemTakeCount(0, 1f, 20f) == 0, "empty stack takes nothing");
for (int stack = 1; stack <= 300; stack += 7)
foreach (float mass in new[] { 0.03f, 0.25f, 0.5f, 0.9f, 1f, 3.3f, 6.67f, 19.99f })
{
    int n = RetrievalRules.ItemTakeCount(stack, mass, 20f);
    Check(n >= 0 && n <= stack, "take is within the stack");
    Check(n * mass <= 20f + 0.01f, $"take {n} x {mass} kg is within capacity");
    if (n < stack) Check((n + 1) * mass > 20f - 0.01f, $"take {n} x {mass} kg leaves no room for one more");
}

// ---------------------------------------------------------------- stack conservation

for (int stack = 1; stack <= 75; stack++)
for (int take = 0; take <= 80; take += 3)
{
    RetrievalRules.SplitCounts(stack, take, out int remaining, out int moved);
    Check(remaining + moved == stack, "split conserves the stack");
    Check(moved >= 0 && remaining >= 0, "split never goes negative");
}

// ---------------------------------------------------------------- wound selection

HookWoundFacts Wound(bool tended, bool canBleed) => new HookWoundFacts
{
    PartPresent = true, External = true, Organic = true, Tended = tended, CanBleed = canBleed,
};

var permanent = Wound(true, true); permanent.Permanent = true;
var internalWound = Wound(true, true); internalWound.External = false;
var missing = Wound(true, true); missing.PartPresent = false;
var bionic = Wound(true, true); bionic.Organic = false;
Check(!RetrievalRules.WoundEligible(permanent), "permanent wound (scar) is not eligible");
Check(!RetrievalRules.WoundEligible(internalWound), "internal wound is not eligible");
Check(!RetrievalRules.WoundEligible(missing), "wound on a missing part is not eligible");
Check(!RetrievalRules.WoundEligible(bionic), "wound on an artificial part is not eligible");

var mixed = new List<HookWoundFacts> { Wound(false, true), permanent, Wound(true, false), Wound(true, true), internalWound, Wound(true, true) };
var picks = new HashSet<int>();
for (int r = 0; r < 2; r++) picks.Add(RetrievalRules.PickWound(mixed, n => r % n));
Check(picks.SetEquals(new[] { 3, 5 }), "only tended wounds that can bleed are chosen when any exist, and all of them can be");

var noPreferred = new List<HookWoundFacts> { permanent, Wound(false, true), Wound(true, false), internalWound };
var fallback = new HashSet<int>();
for (int r = 0; r < 2; r++) fallback.Add(RetrievalRules.PickWound(noPreferred, n => r % n));
Check(fallback.SetEquals(new[] { 1, 2 }), "otherwise any eligible wound can be chosen");
Check(RetrievalRules.PickWound(new List<HookWoundFacts> { permanent, internalWound }, n => 0) == -1, "no eligible wound means no penalty");
Check(RetrievalRules.PickWound(new List<HookWoundFacts>(), n => 0) == -1, "injury-free pawn has no penalty");

// ---------------------------------------------------------------- severity cap

float Cap(Func<float, bool> safe) => RetrievalRules.CappedIncrease(RetrievalHookDefaults.MaxSeverityIncrease, RetrievalHookDefaults.SeverityStep, safe);

Check(Math.Abs(Cap(a => true) - 1f) < 1e-6, "full increase of 1 when safe");
// A part with 0.37 health left: anything reaching 0.37 destroys it.
float partLeft = Cap(a => a < 0.37f - 1e-4f);
Check(Math.Abs(partLeft - 0.36f) < 1e-5, "increase stops one step short of destroying the part, got " + partLeft);
Check(Cap(a => false) == 0f, "zero increase when no amount is safe");
Check(Math.Abs(Cap(a => a <= 0.01f + 1e-6f) - 0.01f) < 1e-6, "smallest step is allowed when only it is safe");
var tried = new List<float>();
Cap(a => { tried.Add(a); return false; });
Check(tried.Count == 100 && Math.Abs(tried[0] - 1f) < 1e-6 && Math.Abs(tried[99] - 0.01f) < 1e-6, "tries 1.00 down to 0.01 in 0.01 steps");
Check(tried.Zip(tried.Skip(1), (a, b) => Math.Abs(a - b - 0.01f) < 1e-5).All(x => x), "steps are exactly 0.01 apart");

// ---------------------------------------------------------------- drag path and once-only penalty

var path = RetrievalRules.DragPath(10, 3, 0, 0);
Check(path[0] == (10, 3), "path starts at the target");
Check(!path.Contains((0, 0)), "path stops short of the caster's cell");
Check(Math.Max(Math.Abs(path[^1].x), Math.Abs(path[^1].z)) == 1, "path ends beside the caster");
for (int i = 1; i < path.Count; i++)
    Check(Math.Max(Math.Abs(path[i].x - path[i - 1].x), Math.Abs(path[i].z - path[i - 1].z)) == 1, "path moves one cell at a time");
Check(RetrievalRules.DragPath(1, 1, 0, 0).Count == 1, "adjacent target has nowhere to move");

int Run(DragProgress drag, int count, Func<int, bool> open, out DragEvent end, out int penalties, int maxTicks = 10000)
{
    penalties = 0;
    end = DragEvent.None;
    int ticks = 0;
    while (ticks++ < maxTicks)
    {
        DragEvent e = drag.Tick(count, open, out bool first);
        if (first) penalties++;
        if (e == DragEvent.Arrived || e == DragEvent.Blocked) { end = e; break; }
    }
    return ticks;
}

var clear = new DragProgress();
Run(clear, 11, i => true, out DragEvent clearEnd, out int clearPenalties);
Check(clearEnd == DragEvent.Arrived && clear.index == 10, "clear path arrives at the last cell");
Check(clearPenalties == 1, "penalty is applied exactly once over a full drag");

var blocked = new DragProgress();
Run(blocked, 11, i => i != 6, out DragEvent blockedEnd, out int blockedPenalties);
Check(blockedEnd == DragEvent.Blocked && blocked.index == 5, "blocked path stops at the last valid cell");
Check(blockedPenalties == 1, "blocked drag still applied the penalty once");

var immediate = new DragProgress();
Run(immediate, 11, i => false, out DragEvent immediateEnd, out int immediatePenalties);
Check(immediateEnd == DragEvent.Blocked && immediate.index == 0 && immediatePenalties == 0, "no movement means no penalty");

var adjacent = new DragProgress();
Run(adjacent, 1, i => true, out DragEvent adjacentEnd, out int adjacentPenalties);
Check(adjacentEnd == DragEvent.Arrived && adjacentPenalties == 0, "adjacent target arrives without moving and without penalty");

// Interrupted and saved mid-drag, then resumed from the saved fields: the penalty does not repeat.
var first = new DragProgress();
int firstPenalties = 0;
for (int t = 0; t < RetrievalHookDefaults.DragTicksPerCell * 3 + 2; t++)
{
    first.Tick(11, i => true, out bool f);
    if (f) firstPenalties++;
}
var restored = new DragProgress { index = first.index, stepTicks = first.stepTicks, penaltyApplied = first.penaltyApplied };
Run(restored, 11, i => true, out DragEvent restoredEnd, out int restoredPenalties);
Check(firstPenalties == 1 && restoredPenalties == 0 && restoredEnd == DragEvent.Arrived, "penalty does not repeat after save and load");

// Drawing is continuous: the fraction never jumps backwards within a step.
var smooth = new DragProgress();
float lastAlong = 0f;
for (int t = 0; t < RetrievalHookDefaults.DragTicksPerCell * 5; t++)
{
    smooth.Tick(6, i => true, out _);
    float along = smooth.index - 1 + smooth.StepFraction;
    Check(along + 1e-4f >= lastAlong, "drawn position never moves backwards");
    lastAlong = along;
}

Console.WriteLine("Retrieval hook checks passed: " + checks);
