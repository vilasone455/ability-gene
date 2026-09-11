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
