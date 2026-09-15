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
static int EdgesShowing(Vector2[] c, int sides)
{
    int count = 0;
    for (int edge = 0; edge < SixPathsSlab.EdgeCount; edge++) if (SixPathsSlab.SidesShown(c, edge) == sides) count++;
    return count;
}
static (float north, float south, float east, float west) Bounds(Vector2[] points)
{
    float north = float.MinValue, south = float.MaxValue, east = float.MinValue, west = float.MaxValue;
    foreach (Vector2 point in points)
    {
        north = Math.Max(north, point.y); south = Math.Min(south, point.y);
        east = Math.Max(east, point.x); west = Math.Min(west, point.x);
    }
    return (north, south, east, west);
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

// Standing on the ground at the yaw it is drawn at, which is square to the camera.
Check(SixPathsSlamTiming.Yaw == 0f, "These checks are written for the block square to the camera");
SixPathsSlab.Project(corners, SixPathsSlamTiming.Yaw, SixPathsSlab.Rest, 1f);
Check(VisibleFaces(corners) == 2, "Square to the camera a box shows its front and its top");
Check(SixPathsSlab.Visible(corners, Top) && SixPathsSlab.Visible(corners, South),
    "Those two are the top and the front");
Check(SixPathsSlab.Front(corners, SixPathsSlamTiming.Yaw) == South, "The south face is the one drawn at the front value");
Check(!SixPathsSlab.Visible(corners, Bottom), "The underside is never drawn");
Check(Near(Math.Abs(SixPathsSlab.Facing(corners, South)) * 0.5f,
    SixPathsSlab.Width * SixPathsSlab.Height * SixPathsHeight.Lift),
    "The front face is exactly width by height by lift: 1.8 by 6 by 0.60");
Check(EdgesShowing(corners, 1) == 6 && EdgesShowing(corners, 2) == 1,
    "Six silhouette edges get the outline, and the one fold between top and front the inner line");

var (north, south, east, west) = Bounds(corners);
Check(Near(east - west, SixPathsSlab.Width), "The block is 1.8 cells wide");
Check(Near(north - south, SixPathsSlab.Depth + SixPathsSlab.Height * SixPathsHeight.Lift),
    "and stands its six cells of height up the screen, over the two cells it covers");

var footprint = new Vector2[SixPathsSlab.CornerCount];
SixPathsSlab.Footprint(footprint, SixPathsSlamTiming.Yaw, 1f);
(north, south, east, west) = Bounds(footprint);
Check(Near(east - west, SixPathsSlab.Width) && Near(north - south, SixPathsSlab.Depth),
    "The ground under it is the 1.8x2 cells it stands on, with no height folded in");

// Sunk two cells: the base stays on the ground and the top comes down, because the part under
// the floor is not drawn.
SixPathsSlab.Project(corners, SixPathsSlamTiming.Yaw, SixPathsSlab.Rest - 2f, 1f);
Check(Near(Math.Abs(SixPathsSlab.Facing(corners, South)) * 0.5f,
    SixPathsSlab.Width * (SixPathsSlab.Height - 2f) * SixPathsHeight.Lift),
    "A block sunk two cells shows four cells of front face");
for (int i = 0; i < SixPathsSlab.CornerCount; i++)
    Check(corners[i].y >= footprint[i].y - 0.0001f, "No corner is drawn below the ground");

// The projection has to hold at any yaw, not only the one in use: a fourth face visible at once
// means it has folded inside out, and the top or the underside swapping sides means the winding
// is wrong.
var hull = new Vector2[SixPathsSlab.CornerCount * 2];
var scratch = new Vector2[SixPathsSlab.CornerCount];
for (float yaw = -180f; yaw <= 180f; yaw += 0.9f)
{
    SixPathsSlab.Project(corners, yaw, 5f, 1f);
    int faces = VisibleFaces(corners);
    Check(faces >= 2 && faces <= 3, $"A standing box shows two or three faces, not {faces} at yaw {yaw}");
    Check(!SixPathsSlab.Visible(corners, Bottom), "The underside is never drawn");
    Check(SixPathsSlab.Visible(corners, Top), "The top is always drawn");
    for (int face = 0; face < SixPathsSlab.FaceCount; face += 2)
        Check(!(SixPathsSlab.Visible(corners, face) && SixPathsSlab.Visible(corners, face + 1)),
            "Opposite faces cannot both face the camera");
    Check(EdgesShowing(corners, 1) == 6, $"The silhouette of a box is always six edges: yaw {yaw}");
    Check(EdgesShowing(corners, 2) == (faces == 2 ? 1 : 3), $"Folds: one between two faces, three among three: yaw {yaw}");

    int front = SixPathsSlab.Front(corners, yaw);
    Check(front >= 0 && front != Top && front != Bottom && SixPathsSlab.Visible(corners, front),
        $"Some visible upright face is the front: yaw {yaw}");
    for (int face = 0; face < SixPathsSlab.FaceCount; face++)
        Check(SixPathsSlamLook.FaceValue(face, front) == (face == Top ? SixPathsSlamLook.Top
            : face == front ? SixPathsSlamLook.Front : SixPathsSlamLook.Side), "Each face gets its role's value");

    // The sun shadow's hull: convex, counter-clockwise, and holding every cast corner.
    SixPathsSlab.Cast(corners, yaw, SixPathsSlab.Rest, 1f, new Vector2(-0.45f, -0.32f));
    for (int i = 0; i < corners.Length; i++) scratch[i] = corners[i];
    int size = SixPathsSlab.Hull(scratch, corners.Length, hull);
    Check(size >= 4 && size <= 8, $"A cast box shadow has four to eight hull points, not {size}: yaw {yaw}");
    for (int i = 0; i < size; i++)
    {
        Vector2 a = hull[i], b = hull[(i + 1) % size];
        Check(SixPathsSlab.Cross(a, b, hull[(i + 2) % size]) > 0f, $"The hull turns left at every point: yaw {yaw}");
        foreach (Vector2 point in corners)
            Check(SixPathsSlab.Cross(a, b, point) >= -0.0001f, $"Every cast corner is inside the hull: yaw {yaw}");
    }
}

// The cast shadow starts at the base and leans away from the sun by the block's height.
SixPathsSlab.Cast(corners, 0f, SixPathsSlab.Rest, 1f, new Vector2(-0.5f, 0f));
(north, south, east, west) = Bounds(corners);
Check(Near(east, SixPathsSlab.HalfWidth) && Near(west, -SixPathsSlab.HalfWidth - SixPathsSlab.Height * 0.5f),
    "A shadow of half a cell per cell of height runs three cells west of a six-cell block");

// Rand is the lab's standins.js hash. These are its outputs for the same arguments.
Check(Near(SixPathsSlamTiming.Rand(0, 1), 0.11986944f, 1e-6f) && Near(SixPathsSlamTiming.Rand(39, 35), 0.84886343f, 1e-6f),
    "Rand matches the lab's hash, so the scatter lands where it did in the sketch");

// The sequence itself.
float step = 0.004f;
float previousHeight = SixPathsSlamTiming.Apex, previousRing = 0f, previousContact = -1f;
// The fastest the block moves is the end of the fall, 2 x drop / fall time.
float fastest = 2f * (SixPathsSlamTiming.Apex - SixPathsSlab.Rest) / SixPathsSlamTiming.Fall * step;
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
            $"The block holds one attitude from the sky into the ground: {time:0.000}s");
        Check(Math.Abs(pose.height - previousHeight) <= fastest + 0.001f,
            $"The block's motion must be continuous frame to frame: {time:0.000}s");
        previousHeight = pose.height;
    }

    if (time < SixPathsSlamTiming.LandAt && pose.alpha > 0f)
        Check(SixPathsSlab.Lowest(pose.height, SixPathsSlamTiming.DrawScale(pose)) >= -0.0001f,
            $"Until it lands the block is above the floor, not through it: {time:0.000}s");

    if (time >= SixPathsSlamTiming.LandAt)
    {
        Check(Near(SixPathsSlamTiming.Clearance(pose), 0f) && pose.scale == 1f,
            $"Once landed there is no daylight under the block: {time:0.000}s");
        Check(pose.height <= SixPathsSlab.Rest + 0.0001f && pose.height >= SixPathsSlab.Rest - SixPathsSlab.Height - 0.0001f,
            "Landed, the block is somewhere between standing and fully sunk");
        if (time < SixPathsSlamTiming.ExitAt)
            Check(pose.height >= SixPathsSlab.Rest - SixPathsSlamTiming.Sink - 0.0001f,
                "Before the exit it is sunk by the impact and no further");
        Check(pose.alpha == 1f || pose.height + SixPathsSlab.HalfHeight <= 0.0101f,
            "It stays solid while it sinks, until less than 0.01 cells of it are above ground");
    }

    float contact = SixPathsSlamLook.ContactStrength(SixPathsSlamTiming.Clearance(pose));
    if (time >= SixPathsSlamTiming.FallAt && time <= SixPathsSlamTiming.LandAt)
    {
        Check(contact >= previousContact - 0.0001f, "The shadow below darkens as the block comes down");
        previousContact = contact;
    }

    float ring = SixPathsSlamTiming.RingRadius(time);
    Check(ring >= previousRing - 0.0001f && ring <= SixPathsSlamTiming.RingReach + 0.0001f,
        "The shock ring only travels outward, and only so far");
    previousRing = ring;

    foreach (float alpha in new[] { SixPathsSlamTiming.FuseFlash(time), SixPathsSlamTiming.ImpactFlash(time),
        SixPathsSlamTiming.RingAlpha(time), SixPathsSlamTiming.MarksAlpha(time), SixPathsSlamTiming.SeamWeight(time) })
        Check(float.IsFinite(alpha) && alpha >= 0f && alpha <= 1f, "Opacity must stay valid");

    Check(SixPathsSlamTiming.FuseFlash(time) == 0f || time >= SixPathsSlamTiming.FuseAt,
        "Nothing flashes before the orbs have met");
    bool landed = time >= SixPathsSlamTiming.LandAt;
    Check(landed || (SixPathsSlamTiming.ImpactFlash(time) == 0f && SixPathsSlamTiming.RingAlpha(time) == 0f
        && SixPathsSlamTiming.MarksAlpha(time) == 0f), "Nothing happens on the floor before the block reaches it");

    for (int i = 0; i < SixPathsSlamTiming.Puffs; i++)
    {
        ImpactParticle puff = SixPathsSlamTiming.Puff(i, time);
        Check(float.IsFinite(puff.x) && float.IsFinite(puff.z) && puff.alpha >= 0f && puff.alpha <= 0.55f,
            "A puff stays finite and faint");
        Check(landed || puff.alpha == 0f, "No dust before the landing");
        if (puff.alpha > 0f)
            Check(puff.x * puff.x + puff.z * puff.z / 0.64f >= 0.99f * 0.55f * 0.55f * SixPathsSlab.Depth * SixPathsSlab.Depth,
                "Dust starts at the block's edge, not inside it");
    }
    for (int i = 0; i < SixPathsSlamTiming.Debris; i++)
    {
        ImpactParticle chunk = SixPathsSlamTiming.Chunk(i, time);
        Check(float.IsFinite(chunk.height) && chunk.height >= 0f && chunk.height < 1.2f,
            "Debris flies no higher than 7^2 / 44 = 1.11 cells and never below the ground");
        Check(landed || chunk.alpha == 0f, "No debris before the landing");
        Check(time < SixPathsSlamTiming.LandAt + 0.65f || chunk.height < 0.001f, "Every chunk is back down within 0.64 s");
    }
    for (int i = 0; i < SixPathsSlamTiming.SkirtPuffs; i++)
    {
        ImpactParticle skirt = SixPathsSlamTiming.SkirtPuff(i, time);
        Check(skirt.alpha >= 0f && skirt.alpha <= 0.45f, "The skirt stays faint");
        Check(skirt.alpha == 0f || (time >= SixPathsSlamTiming.ExitAt && time < SixPathsSlamTiming.GoneAt),
            "The skirt is there only while the block sinks");
    }
}

// Cracks run outward from the block's edge, and only as far as they have grown.
for (int crack = 0; crack < SixPathsSlamTiming.Cracks; crack++)
{
    SixPathsSlamTiming.CrackSegment(crack, 0, 0f, out Vector2 start, out Vector2 stillborn);
    Check(Near(start.x, stillborn.x) && Near(start.y, stillborn.y), "An ungrown crack has no length");
    float travelled = 0f;
    Vector2 end = start;
    for (int s = 0; s < 4; s++)
    {
        SixPathsSlamTiming.CrackSegment(crack, s, 1f, out Vector2 from, out Vector2 to);
        Check(Near(from.x, end.x) && Near(from.y, end.y), "Crack segments join end to end");
        travelled += (to - from).magnitude;
        end = to;
    }
    Check(travelled >= 1.2f - 0.001f && travelled <= 2.8f + 0.001f, "A grown crack is 1.2 to 2.8 cells long");
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

// The impact and the exit.
Check(Near(SixPathsSlamTiming.Slab(SixPathsSlamTiming.LandAt + SixPathsSlamTiming.SinkTime).height,
    SixPathsSlab.Rest - SixPathsSlamTiming.Sink), "The impact drives it 0.2 cells in within 0.06 s");
Check(Near(SixPathsSlamTiming.Slab(SixPathsSlamTiming.ExitAt).height, SixPathsSlab.Rest - SixPathsSlamTiming.Sink),
    "and it stands there until the exit");
Check(Near(SixPathsSlamTiming.Slab(SixPathsSlamTiming.ExitAt + SixPathsSlamTiming.ExitSeconds * 0.5f).height,
    SixPathsSlab.Rest - SixPathsSlamTiming.Sink - (SixPathsSlab.Height - SixPathsSlamTiming.Sink) * 0.5f),
    "Halfway through the exit it is halfway down");
Check(SixPathsSlamTiming.Slab(SixPathsSlamTiming.GoneAt).alpha == 0f, "Its top is level with the ground at the end of the exit");
Check(SixPathsSlamTiming.MarksAlpha(SixPathsSlamTiming.GoneAt) == 1f && SixPathsSlamTiming.MarksAlpha(SixPathsSlamTiming.Duration) == 0f,
    "The cracks and debris outlast the block, then fade by the end");
Check(Near(SixPathsSlamTiming.SeamWeight(SixPathsSlamTiming.FuseAt), 1f)
    && Near(SixPathsSlamTiming.SeamWeight(SixPathsSlamTiming.FuseAt + 1.2f), 0.35f), "The seams thin from full to 0.35 over 1.2 s");

// It ends: the last frame is empty, or the preview never puts itself away.
Check(SixPathsSlamTiming.Slab(SixPathsSlamTiming.Duration).alpha <= 0.0001f, "The block is gone by the end");
Check(SixPathsSlamTiming.Duration > SixPathsSlamTiming.GoneAt
    && SixPathsSlamTiming.LandAt < 3f, "The block lands inside three seconds of the cast");

Console.WriteLine("Six Paths slam checks passed.");
