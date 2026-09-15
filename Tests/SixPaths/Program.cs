using System;
using RimArt;
using UnityEngine;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static bool Near(float a, float b, float tolerance = 0.0005f) => Math.Abs(a - b) < tolerance;

var corners = new Vector2[SixPathsSlab.CornerCount];
const int Top = 2, Bottom = 3, South = 5;

static int VisibleFaces(Vector2[] c)
{
    int count = 0;
    for (int face = 0; face < SixPathsSlab.FaceCount; face++) if (SixPathsSlab.Visible(c, face)) count++;
    return count;
}
static int VisibleEdges(Vector2[] c)
{
    int count = 0;
    for (int edge = 0; edge < SixPathsSlab.EdgeCount; edge++) if (SixPathsSlab.EdgeVisible(c, edge)) count++;
    return count;
}

// The twelve edges, each shared by exactly two faces, each face bordered by four of them. A
// mistake in the corner table shows up here rather than as a block missing a side in game.
Check(SixPathsSlab.EdgeCorners.Length == 12, "A box has twelve edges");
for (int edge = 0; edge < SixPathsSlab.EdgeCount; edge++)
{
    Check(SixPathsSlab.EdgeFaces[edge][0] != SixPathsSlab.EdgeFaces[edge][1],
        "An edge must lie between two different faces");
    Check(SixPathsSlab.EdgeCorners[edge][0] != SixPathsSlab.EdgeCorners[edge][1],
        "An edge must join two different corners");
}
for (int face = 0; face < SixPathsSlab.FaceCount; face++)
{
    int bordering = 0;
    for (int edge = 0; edge < SixPathsSlab.EdgeCount; edge++)
        if (SixPathsSlab.EdgeFaces[edge][0] == face || SixPathsSlab.EdgeFaces[edge][1] == face) bordering++;
    Check(bordering == 4, "Every face is bordered by four edges");
}

// Standing on the ground, square to the camera. Six cells of height have to arrive as a front
// face 3.6 cells tall, because that face is the whole difference between a solid and a rectangle.
SixPathsSlab.Project(corners, 0f, SixPathsSlab.Rest, 1f);
Check(VisibleFaces(corners) == 2, "Square to the camera a box shows its front and its top");
Check(SixPathsSlab.Visible(corners, Top) && SixPathsSlab.Visible(corners, South),
    "Those two are the top and the front");
Check(!SixPathsSlab.Visible(corners, Bottom), "The underside is never drawn");
Check(Near(Math.Abs(SixPathsSlab.Facing(corners, South)) * 0.5f,
    SixPathsSlab.Width * SixPathsSlab.Height * SixPathsHeight.Lift),
    "The front face is exactly width by height by lift: 2 by 6 by 0.60");

float north = float.MinValue, south = float.MaxValue, east = float.MinValue, west = float.MaxValue;
foreach (Vector2 point in corners)
{
    north = Math.Max(north, point.y); south = Math.Min(south, point.y);
    east = Math.Max(east, point.x); west = Math.Min(west, point.x);
}
Check(Near(east - west, SixPathsSlab.Width), "The block is two cells wide");
Check(Near(north - south, SixPathsSlab.Depth + SixPathsSlab.Height * SixPathsHeight.Lift),
    "and stands its six cells of height up the screen, over the two cells it covers");

var footprint = new Vector2[SixPathsSlab.CornerCount];
SixPathsSlab.Footprint(footprint, 0f, 1f);
north = float.MinValue; south = float.MaxValue; east = float.MinValue; west = float.MaxValue;
foreach (Vector2 point in footprint)
{
    north = Math.Max(north, point.y); south = Math.Min(south, point.y);
    east = Math.Max(east, point.x); west = Math.Min(west, point.x);
}
Check(Near(east - west, SixPathsSlab.Width) && Near(north - south, SixPathsSlab.Depth),
    "The shadow it casts is the 2x2 cells it stands on, with no height folded in");

// The angle it is actually drawn at has to be one that shows three faces. Two is a billboard.
SixPathsSlab.Project(corners, SixPathsSlamTiming.Yaw, SixPathsSlab.Rest, 1f);
Check(VisibleFaces(corners) == 3, "At its own yaw the block shows three faces");
Check(VisibleEdges(corners) == 9, "and nine of its twelve edges");

// The block is only ever drawn at one angle, but the projection has to hold at any of them: a
// fourth face visible at once means it has folded inside out, and the top or the underside
// swapping sides means the winding is wrong.
for (float yaw = -180f; yaw <= 180f; yaw += 0.9f)
{
    SixPathsSlab.Project(corners, yaw, 5f, 1f);
    int faces = VisibleFaces(corners), edges = VisibleEdges(corners);
    Check(faces >= 2 && faces <= 3, $"A standing box shows two or three faces, not {faces} at yaw {yaw}");
    Check(!SixPathsSlab.Visible(corners, Bottom), "The underside is never drawn");
    Check(SixPathsSlab.Visible(corners, Top), "The top is always drawn");
    for (int face = 0; face < SixPathsSlab.FaceCount; face += 2)
        Check(!(SixPathsSlab.Visible(corners, face) && SixPathsSlab.Visible(corners, face + 1)),
            "Opposite faces cannot both face the camera");
    Check(edges == 3 * faces, $"Each visible face brings four edges, shared in pairs: {edges} for {faces}");

    // Lighting has to separate the visible planes, or three black quads read as one blob. The
    // worst case over a full turn is what matters: the block holds one angle for a whole cast,
    // and a pose where its faces match in value is a pose where it stops being a solid.
    float brightest = 0f, dimmest = 1f;
    for (int face = 0; face < SixPathsSlab.FaceCount; face++)
    {
        if (!SixPathsSlab.Visible(corners, face)) continue;
        float light = SixPathsSlab.Lambert(face, yaw);
        Check(light >= 0f && light <= 1f, "Lighting stays within range");
        brightest = Math.Max(brightest, light); dimmest = Math.Min(dimmest, light);
    }
    Check(brightest - dimmest > 0.20f, $"The visible faces must differ in value: {brightest - dimmest:0.00} at yaw {yaw}");
}

// The sequence itself.
float step = 0.004f;
float previousHeight = SixPathsSlamTiming.Apex, previousRing = 0f;
var previousOrb = new GatherStep[SixPathsTiming.Orbs];
for (int i = 0; i < previousOrb.Length; i++) previousOrb[i] = SixPathsSlamTiming.Orb(i, previousOrb.Length, 0f);

for (float time = 0f; time <= SixPathsSlamTiming.Duration + 0.2f; time += step)
{
    for (int i = 0; i < previousOrb.Length; i++)
    {
        GatherStep orb = SixPathsSlamTiming.Orb(i, previousOrb.Length, time);
        Check(float.IsFinite(orb.radius) && float.IsFinite(orb.height) && float.IsFinite(orb.angle),
            "An orb's path must stay finite");
        Check(orb.radius >= 0f && orb.radius <= SixPathsSlamTiming.StartRadius + 0.0001f,
            "An orb only ever comes inward");
        Check(orb.height >= SixPathsSlamTiming.StartHeight - 0.0001f
            && orb.height <= SixPathsSlamTiming.Apex + 0.0001f, "An orb only ever climbs");
        Check(orb.radius <= previousOrb[i].radius + 0.0001f, "The gather never widens");
        Check(orb.height >= previousOrb[i].height - 0.0001f, "The gather never sinks");
        Check(time < SixPathsSlamTiming.FuseAt ? orb.alpha == 1f : orb.alpha == 0f,
            "The orbs are there until the fuse and gone after it");
        previousOrb[i] = orb;
    }

    SlabPose pose = SixPathsSlamTiming.Slab(time);
    Check(float.IsFinite(pose.height) && float.IsFinite(pose.yaw) && float.IsFinite(pose.scale),
        "The block's pose must stay finite");
    Check(pose.alpha >= 0f && pose.alpha <= 1f, "Opacity must stay valid");
    Check(pose.scale >= 0f && pose.scale <= 1.3f, "The block forms at its own size, give or take the overshoot");

    if (pose.alpha > 0f && pose.scale > 0f)
    {
        Check(pose.yaw == SixPathsSlamTiming.Yaw,
            $"The block holds one attitude from the sky to the floor: {time:0.000}s");
        Check(SixPathsSlab.Lowest(pose.height, SixPathsSlamTiming.DrawScale(pose)) >= -0.0001f,
            $"The block stands on the floor rather than through it: {time:0.000}s");
        Check(Math.Abs(pose.height - previousHeight) < 0.3f,
            $"The fall must be continuous frame to frame: {time:0.000}s");
        previousHeight = pose.height;
    }

    if (time >= SixPathsSlamTiming.LandAt)
        Check(Near(pose.height, SixPathsSlab.Rest) && Near(SixPathsSlamTiming.Clearance(pose), 0f),
            "Once landed the block stands on the ground and stays there");

    float ring = SixPathsSlamTiming.RingRadius(time);
    Check(ring >= previousRing - 0.0001f && ring <= SixPathsSlamTiming.RingReach + 0.0001f,
        "The shock ring only travels outward, and only so far");
    previousRing = ring;

    foreach (float alpha in new[] { SixPathsSlamTiming.FuseFlash(time), SixPathsSlamTiming.ImpactFlash(time),
        SixPathsSlamTiming.RingAlpha(time), SixPathsSlamTiming.DustAlpha(time) })
        Check(float.IsFinite(alpha) && alpha >= 0f && alpha <= 1f, "Opacity must stay valid");

    Check(SixPathsSlamTiming.FuseFlash(time) == 0f || time >= SixPathsSlamTiming.FuseAt,
        "Nothing flashes before the orbs have met");
    Check(SixPathsSlamTiming.ImpactFlash(time) == 0f || time >= SixPathsSlamTiming.LandAt,
        "Nothing flashes on the floor before the block reaches it");
    Check(SixPathsSlamTiming.DustAlpha(time) == 0f || time >= SixPathsSlamTiming.LandAt,
        "Nothing is thrown off the floor before the block reaches it");
}

// The handover: orbs at the apex, block at the apex, no frame with both and no frame with neither.
GatherStep arrived = SixPathsSlamTiming.Orb(0, SixPathsTiming.Orbs, SixPathsSlamTiming.Gather);
Check(Near(arrived.radius, 0f) && Near(arrived.height, SixPathsSlamTiming.Apex),
    "The orbs finish the gather stacked on one point at the apex");
Check(SixPathsSlamTiming.Slab(SixPathsSlamTiming.FuseAt - 0.001f).alpha == 0f, "No block before the fuse");
Check(SixPathsSlamTiming.Slab(SixPathsSlamTiming.FuseAt + 0.05f).alpha > 0f, "A block straight after it");
Check(Near(SixPathsSlamTiming.Slab(SixPathsSlamTiming.FuseAt + 0.001f).height, SixPathsSlamTiming.Apex, 0.01f),
    "The block forms where the orbs met, not somewhere else");
Check(SixPathsSlamTiming.FuseFlash(SixPathsSlamTiming.FuseAt + 0.02f) > 0.1f,
    "The swap is covered by a flash");

// It ends: the last frame is empty, or the preview never puts itself away.
Check(SixPathsSlamTiming.Slab(SixPathsSlamTiming.Duration).alpha <= 0.0001f, "The block is gone by the end");
Check(SixPathsSlamTiming.Duration > SixPathsSlamTiming.LandAt
    && SixPathsSlamTiming.LandAt < 3f, "The block lands inside three seconds of the cast");

Console.WriteLine("Six Paths slam checks passed.");
