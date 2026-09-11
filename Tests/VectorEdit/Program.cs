// Behaviour checks for the vector manipulation arithmetic, run against the production
// CapturedProjectile, VectorEditGroup, VectorEditRegistry and VectorEditDefaults.
//
// What is covered here is everything the player is promised by the panel and the preview:
// the range table, that force is absolute rather than cumulative, that rotation turns a volley
// without collapsing its spread, that travel stops at the map edge, and what counts as an edit.
using System;
using System.Collections.Generic;
using RimArt;
using UnityEngine;
using Verse;

static class Program
{
    static int checks;

    static void Main()
    {
        RangeTable();
        SpeedAndTicks();
        RotationPreservesSpread();
        RotationIsAppliedToTheCapturedHeading();
        BulletsDoNotMove();
        MapEdgeClipsTravel();
        ReEditingIsAbsolute();
        RenewedRangeCountsAsAnEdit();
        UnchangedRoundIsNotAnEdit();
        CommitRewritesFlightAndLauncher();
        ForceOfOneLeavesNoRegistryEntry();
        StrainCosts();
        GroupMembershipIsExclusive();
        FastRoundsAreCaughtByTheirStep();
        TheCatchIsMeasuredInTicksNotCells();

        Console.WriteLine($"Passed {checks} vector manipulation checks.");
    }

    // ------------------------------------------------------------------ helpers

    static void Check(bool condition, string what)
    {
        checks++;
        if (!condition) throw new Exception("FAILED: " + what);
    }

    static void Near(float actual, float expected, string what, float tolerance = 0.05f)
    {
        checks++;
        if (Math.Abs(actual - expected) > tolerance)
            throw new Exception($"FAILED: {what} -- expected {expected}, got {actual}");
    }

    /// <summary>A round heading north from the middle of the map, 30 cells per second.</summary>
    static Projectile Round(Map map, Vector3 at, Vector3 heading, float speed = 30f)
    {
        Projectile projectile = new Projectile { Map = map };
        projectile.def.projectile.speed = speed;
        projectile.StubLaunch(at, at + heading.normalized * 8f, 16);
        projectile.ExactPosition = at;
        return projectile;
    }

    static Vector3 Middle => new Vector3(125f, 0f, 125f);

    static float AngleFlat(Vector3 v) => (float)(Math.Atan2(v.x, v.z) * 180.0 / Math.PI);

    // ------------------------------------------------------------------ checks

    /// <summary>Twenty cells at x1, and the table the ability's description promises.</summary>
    static void RangeTable()
    {
        Map map = new Map();
        CapturedProjectile caught = new CapturedProjectile(Round(map, Middle, Vector3.forward));

        Near((caught.EndpointFor(0f, 0.25f, map) - Middle).magnitude, 5f, "x0.25 travels 5 cells");
        Near((caught.EndpointFor(0f, 0.5f, map) - Middle).magnitude, 10f, "x0.5 travels 10 cells");
        Near((caught.EndpointFor(0f, 1f, map) - Middle).magnitude, 20f, "x1 travels 20 cells");
        Near((caught.EndpointFor(0f, 2f, map) - Middle).magnitude, 40f, "x2 travels 40 cells");
    }

    /// <summary>
    /// Force scales speed as well as range, so a doubled round covers twice the distance in the
    /// same time and a quartered one takes the same time to go a quarter as far.
    /// </summary>
    static void SpeedAndTicks()
    {
        Map map = new Map();
        CapturedProjectile caught = new CapturedProjectile(Round(map, Middle, Vector3.forward));

        int baseline = caught.TicksFor(caught.EndpointFor(0f, 1f, map), 1f);
        int doubled = caught.TicksFor(caught.EndpointFor(0f, 2f, map), 2f);
        int quartered = caught.TicksFor(caught.EndpointFor(0f, 0.25f, map), 0.25f);

        Check(Math.Abs(baseline - doubled) <= 1, "x2 covers twice the range in the same time");
        Check(Math.Abs(baseline - quartered) <= 1, "x0.25 covers a quarter of it in the same time");
        Near(baseline, 40f, "20 cells at 0.5 cells a tick is 40 ticks", 1f);
    }

    /// <summary>
    /// The whole reason rotation is stored as a delta: a fan of rounds turned ninety degrees is
    /// still a fan, not a single line.
    /// </summary>
    static void RotationPreservesSpread()
    {
        Map map = new Map();
        CapturedProjectile left = new CapturedProjectile(Round(map, Middle, new Vector3(-0.3f, 0f, 1f)));
        CapturedProjectile right = new CapturedProjectile(Round(map, Middle, new Vector3(0.3f, 0f, 1f)));

        float before = AngleFlat(right.Heading) - AngleFlat(left.Heading);
        float after = AngleFlat(right.HeadingAfter(90f)) - AngleFlat(left.HeadingAfter(90f));

        Near(after, before, "a rotated group keeps its angular spread");
        Check(before > 1f, "the two test rounds were actually spread apart");
    }

    static void RotationIsAppliedToTheCapturedHeading()
    {
        Map map = new Map();
        CapturedProjectile caught = new CapturedProjectile(Round(map, Middle, Vector3.forward));

        Near(AngleFlat(caught.HeadingAfter(90f)), 90f, "north turned 90 is east");
        Near(AngleFlat(caught.HeadingAfter(-90f)), -90f, "north turned -90 is west");
        Near(AngleFlat(caught.HeadingAfter(0f)), 0f, "no rotation leaves the heading alone");
    }

    /// <summary>Rotation turns rounds where they stand. It never moves them.</summary>
    static void BulletsDoNotMove()
    {
        Map map = new Map();
        Vector3 at = Middle + new Vector3(3f, 0f, -2f);
        Projectile projectile = Round(map, at, Vector3.forward);
        CapturedProjectile caught = new CapturedProjectile(projectile);

        caught.Commit(137f, 2f, new Pawn(), map);

        Near((projectile.StubOrigin - at).magnitude, 0f, "the round starts again from where it was caught");
    }

    /// <summary>A round turned at the map edge stops at the edge rather than off it.</summary>
    static void MapEdgeClipsTravel()
    {
        Map map = new Map();
        Vector3 nearEdge = new Vector3(125f, 0f, 244f);
        CapturedProjectile caught = new CapturedProjectile(Round(map, nearEdge, Vector3.forward));

        Vector3 endpoint = caught.EndpointFor(0f, 2f, map);

        Check(endpoint.z < map.Size.z, "travel stops inside the map");
        Near(endpoint.z, map.Size.z - 0.5f, "travel reaches the edge and no further");
        Near(endpoint.x, nearEdge.x, "clipping does not bend the line");

        // The clipped flight still has to take the time the shortened distance needs.
        int ticks = caught.TicksFor(endpoint, 2f);
        Near(ticks, (endpoint - nearEdge).magnitude / (caught.BaseSpeed * 2f), "clipped ticks match clipped travel", 1f);
    }

    /// <summary>
    /// Editing the same round twice recalculates from its own baseline. A round set to x2 and
    /// then to x2 again is a x2 round, not a x4 one, and its range is 40 cells both times.
    /// </summary>
    static void ReEditingIsAbsolute()
    {
        Map map = new Map();
        Projectile projectile = Round(map, Middle, Vector3.forward);
        Pawn caster = new Pawn();

        new CapturedProjectile(projectile).Commit(0f, 2f, caster, map);
        Near(VectorEditRegistry.ForceFor(projectile), 2f, "first edit records x2");
        Near((projectile.StubDestination - projectile.StubOrigin).magnitude, 40f, "first edit travels 40 cells");

        Vector3 secondCapture = projectile.StubOrigin + Vector3.forward * 6f;
        projectile.ExactPosition = secondCapture;
        new CapturedProjectile(projectile).Commit(0f, 2f, caster, map);

        Near(VectorEditRegistry.ForceFor(projectile), 2f, "re-editing at x2 is still x2, not x4");
        Near((projectile.StubDestination - projectile.StubOrigin).magnitude, 40f,
            "re-editing recalculates 40 cells from the new capture, it does not extend");

        VectorEditRegistry.Clear();
    }

    /// <summary>
    /// A group left at no rotation and x1 is still an edit whenever the recalculated range
    /// differs from what the round has left, because renewing travel is a real effect.
    /// </summary>
    static void RenewedRangeCountsAsAnEdit()
    {
        Map map = new Map();
        // Launched eight cells, so a fresh twenty is a change even with nothing else touched.
        CapturedProjectile caught = new CapturedProjectile(Round(map, Middle, Vector3.forward));

        Check(caught.WouldChange(0f, 1f, map), "renewing a short round's range counts as an edit");
    }

    /// <summary>And a round that is already flying exactly the new flight is not an edit.</summary>
    static void UnchangedRoundIsNotAnEdit()
    {
        Map map = new Map();
        Projectile projectile = Round(map, Middle, Vector3.forward);
        new CapturedProjectile(projectile).Commit(0f, 1f, new Pawn(), map);

        CapturedProjectile again = new CapturedProjectile(projectile);
        Check(!again.WouldChange(0f, 1f, map), "re-applying the identical flight changes nothing");
        Check(again.WouldChange(0f, 2f, map), "but changing force does");
        Check(again.WouldChange(45f, 1f, map), "and so does turning it");

        VectorEditRegistry.Clear();
    }

    static void CommitRewritesFlightAndLauncher()
    {
        Map map = new Map();
        Projectile projectile = Round(map, Middle, Vector3.forward);
        Pawn caster = new Pawn();

        new CapturedProjectile(projectile).Commit(90f, 0.5f, caster, map);

        Near(AngleFlat(projectile.StubDestination - projectile.StubOrigin), 90f, "commit writes the turned heading");
        Near((projectile.StubDestination - projectile.StubOrigin).magnitude, 10f, "commit writes the x0.5 range");
        Check(ReferenceEquals(projectile.StubLauncher, caster), "the manipulating pawn is credited");
        Check(!projectile.StubPreventFriendlyFire, "an edited round is not exempted from allies");
        Check(projectile.StubTicks == projectile.StubLifetime, "the flight counters are set together");
        Check(projectile.usedTarget.Cell.x == projectile.intendedTarget.Cell.x
              && projectile.usedTarget.Cell.z == projectile.intendedTarget.Cell.z,
            "both targets point at the calculated destination");
        Check(projectile.usedTarget.Cell.x == projectile.StubDestination.ToIntVec3().x,
            "the target is the destination, not a scattered cell");

        VectorEditRegistry.Clear();
    }

    /// <summary>
    /// x1 is the baseline, so it is stored as no entry at all - which is what keeps the two
    /// Harmony getters costing one integer read when nothing has been edited.
    /// </summary>
    static void ForceOfOneLeavesNoRegistryEntry()
    {
        Map map = new Map();
        Projectile projectile = Round(map, Middle, Vector3.forward);
        Pawn caster = new Pawn();

        new CapturedProjectile(projectile).Commit(0f, 2f, caster, map);
        Check(VectorEditRegistry.EditedCount == 1, "an off-baseline round is tracked");

        new CapturedProjectile(projectile).Commit(0f, 1f, caster, map);
        Check(VectorEditRegistry.EditedCount == 0, "editing back to x1 stops tracking it");
        Near(VectorEditRegistry.ForceFor(projectile), 1f, "an untracked round reports the baseline");
    }

    static void StrainCosts()
    {
        Near(VectorEditDefaults.StrainCostFor(0), 0f, "nothing changed costs nothing");
        Near(VectorEditDefaults.StrainCostFor(1), 0.08f, "one group costs 8");
        Near(VectorEditDefaults.StrainCostFor(2), 0.24f, "two groups cost 24");
        Near(VectorEditDefaults.StrainCostFor(3), 0.48f, "three groups cost 48");
        Near(VectorEditDefaults.StrainCostFor(4), 0.80f, "four groups cost 80");
        Near(VectorEditDefaults.StrainCostFor(9), 0.80f, "more than four is still the top of the table");
    }

    /// <summary>
    /// A field has to be tested against the step a round took, not against where it is standing
    /// when it is looked at. These are the speeds that make the difference: a vanilla rifle round
    /// moves 1.2 cells a tick and is sampled several times crossing a four-cell field, a Combat
    /// Extended round moves four, and the fastest CE rounds move sixteen - clean over it.
    /// </summary>
    static void FastRoundsAreCaughtByTheirStep()
    {
        Vector3 carrier = new Vector3(50f, 0f, 50f);
        const float Radius = 3.9f;
        Vector3 entry;

        // Sixteen cells in one tick, straight through the carrier. Neither end of the step is
        // inside the field and a sampled position would have missed it outright.
        Vector3 from = new Vector3(42f, 0f, 50f);
        Vector3 to = new Vector3(58f, 0f, 50f);
        Check(Rounds.SegmentEntersCircle(from, to, carrier, Radius, out entry),
            "a round that crosses the whole field in one tick is still caught");
        Near(entry.x, 50f - Radius, "and is caught where it crossed the boundary, not where it ended up");
        Near(entry.z, 50f, "on the line it was travelling");

        // The reason the entry point matters: measured from the sampled position, this round is
        // past the carrier, and the curve would hold it behind them and recede away.
        Check(entry.x < carrier.x, "the entry point is on the approaching side");

        // A round already inside when the step began is caught where it began.
        Check(Rounds.SegmentEntersCircle(carrier, new Vector3(53f, 0f, 50f), carrier, Radius, out entry),
            "a round already inside the field is caught");
        Near(entry.x, carrier.x, "at the start of its step");

        // A round that passes by outside is not caught, however fast it is going.
        Check(!Rounds.SegmentEntersCircle(new Vector3(42f, 0f, 60f), new Vector3(58f, 0f, 60f),
                carrier, Radius, out entry),
            "a round that passes wide is not caught");

        // Nor is one whose step stops short of the boundary.
        Check(!Rounds.SegmentEntersCircle(new Vector3(40f, 0f, 50f), new Vector3(44f, 0f, 50f),
                carrier, Radius, out entry),
            "a round still short of the field is not caught early");
    }

    /// <summary>
    /// The catch rule, which is the one thing that decides whether the ability can be pressed in
    /// time. A round is caught a fixed number of ticks before it arrives, so every round in the
    /// game gives the player the same window however fast it is going - the fast one is simply
    /// taken hold of further out.
    /// </summary>
    static void TheCatchIsMeasuredInTicksNotCells()
    {
        // A vanilla rifle round at 1.17 cells a tick, a middling Combat Extended one at 2.05, and
        // the fastest rounds CE ships at 16.7. Under the old fixed twelve-cell rule those were
        // ten ticks, six, and under one tick of warning respectively.
        CaughtTheSameNumberOfTicksOut(70f / 60f, "a vanilla rifle round");
        CaughtTheSameNumberOfTicksOut(123f / 60f, "a Combat Extended rifle round");
        CaughtTheSameNumberOfTicksOut(1000f / 60f, "the fastest round CE ships");

        Vector3 carrier = new Vector3(50f, 0f, 50f);
        Vector3 north = new Vector3(0f, 0f, 1f);
        const float Vanilla = 70f / 60f;

        // Direction still decides. A round that never comes inside the reach is somebody else's.
        Check(!VectorEditDefaults.ComesIntoReach(new Vector3(70f, 0f, 26f), north, Vanilla, carrier),
            "a round passing twenty cells wide is not caught however close it starts");

        // A round already in reach is caught whichever way it is pointed, so one that has just
        // gone past can still be taken hold of and thrown back.
        Check(VectorEditDefaults.ComesIntoReach(new Vector3(50f, 0f, 53f), north, Vanilla, carrier),
            "a round already inside the reach and leaving is still caught");

        // A round that has stopped is where it is, and nowhere else.
        Check(VectorEditDefaults.ComesIntoReach(new Vector3(50f, 0f, 55f), north, 0f, carrier),
            "a motionless round inside the reach is caught");
        Check(!VectorEditDefaults.ComesIntoReach(new Vector3(50f, 0f, 20f), north, 0f, carrier),
            "a motionless round outside it is not");
    }

    /// <summary>
    /// Walks one round in head-on from far away and finds the distance at which it is first
    /// caught, then checks that the time that distance buys is the lead the kit promises - the
    /// same figure for every speed, which is the whole point of counting the catch in ticks.
    /// </summary>
    static void CaughtTheSameNumberOfTicksOut(float speedPerTick, string what)
    {
        Vector3 carrier = new Vector3(50f, 0f, 50f);
        Vector3 north = new Vector3(0f, 0f, 1f);

        // The lead reaches the edge of the reach, so the first catch is one reach further out
        // than the lead itself.
        float caughtAt = speedPerTick * VectorEditDefaults.LeadTicks + VectorEditDefaults.ScanRadiusCells;

        Check(VectorEditDefaults.ComesIntoReach(new Vector3(50f, 0f, 50f - caughtAt + 0.1f),
                north, speedPerTick, carrier),
            what + " is caught as its lead reaches the edge of the carrier's reach");
        Check(!VectorEditDefaults.ComesIntoReach(new Vector3(50f, 0f, 50f - caughtAt - 0.1f),
                north, speedPerTick, carrier),
            what + " is not caught one step before that");

        // What the player actually gets: the time from the catch to the round arriving at the
        // edge of the reach, which is the lead and nothing else.
        Near((caughtAt - VectorEditDefaults.ScanRadiusCells) / speedPerTick,
            VectorEditDefaults.LeadTicks, what + " buys the same window as every other round", 0.5f);
    }

    /// <summary>Groups never overlap: a round belongs to one or to none.</summary>
    static void GroupMembershipIsExclusive()
    {
        Map map = new Map();
        CapturedProjectile caught = new CapturedProjectile(Round(map, Middle, Vector3.forward));

        VectorEditGroup first = new VectorEditGroup(0);
        VectorEditGroup second = new VectorEditGroup(1);

        first.Add(caught);
        Check(caught.Group == 0 && first.Members.Count == 1, "a round joins the group it is dragged into");

        first.Remove(caught);
        Check(caught.Group == -1 && first.Members.Count == 0, "removing releases it for reselection");

        second.Add(caught);
        second.Rotation = 45f;
        second.Force = 2f;
        Check(caught.Group == 1, "a released round can be put in another group");
        Near(second.Range, 40f, "a group reports the range its force buys");

        second.Clear();
        Check(caught.Group == -1 && second.Members.Count == 0, "deleting a group releases its rounds");
        Near(second.Rotation, 0f, "and resets its settings");
        Near(second.Force, 1f, "and its force");

        List<CapturedProjectile> members = second.Members;
        Check(members.Count == 0, "an emptied group holds nothing");
    }
}
