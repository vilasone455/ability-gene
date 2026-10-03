using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where a real pawn's weapon is, for the pictures drawn on it. The game draws a held weapon's texture flat on
    /// the ground plane, turned and maybe mirrored (PawnRenderUtility.DrawEquipmentAiming); <see cref="PoseAt"/>
    /// turns that into a <see cref="UbwPose"/> at height 0, so the lab's drawing (TraceHandGraphics) lands exactly on
    /// the weapon. <see cref="Drawing"/> remembers where the game drew each watched pawn's weapon this frame; when it
    /// did not draw one (the copy is not made yet, or an undrafted pawn hides its weapon) <see cref="CarryPlace"/>
    /// gives the drafted carry pose.
    /// </summary>
    internal static class TraceHands
    {
        /// <summary>PawnRenderUtility's carry offsets by facing (private there): north, east, south, west.</summary>
        private static readonly Vector3[] EqLoc =
        {
            new Vector3(0f, 0f, -0.11f), new Vector3(0.22f, 0f, -0.22f), new Vector3(0f, 0f, -0.22f), new Vector3(-0.22f, 0f, -0.22f),
        };

        /// <summary>The circuit's lines sit this far above the pawn's DrawPos: over its body and clothes, under the weapon (0.033, layer 90).</summary>
        private const float BodyLift = 0.022f;
        /// <summary>The grip, as a share of the way from the pommel to the point.</summary>
        private const double GripShare = 0.2;

        /// <summary>True while any pawn is watched; the draw prefix does nothing otherwise.</summary>
        public static bool Watching;

        private struct Drawn
        {
            public Thing eq;
            public Vector3 loc;
            public float aim;
            public int frame;
        }

        private static readonly Dictionary<Pawn, Drawn> drawn = new Dictionary<Pawn, Drawn>();

        public static void Forget() => drawn.Clear();

        /// <summary>The game is about to draw <paramref name="eq"/>: remember where. False hides a weapon on its way to the inventory.</summary>
        public static bool Drawing(Thing eq, Vector3 loc, float aim)
        {
            if (!Watching || !(eq?.ParentHolder is Pawn_EquipmentTracker tracker) || tracker.pawn == null) return true;
            GameComponent_Trace trace = GameComponent_Trace.Instance;
            if (trace == null || !trace.Watches(tracker.pawn)) return true;
            if (trace.Stowing(tracker.pawn) == eq) return false;
            drawn[tracker.pawn] = new Drawn { eq = eq, loc = loc, aim = aim, frame = Time.frameCount };
            return true;
        }

        /// <summary>Where the game drew <paramref name="eq"/> for this pawn within the last <paramref name="frames"/> frames.</summary>
        public static bool DrawnAt(Pawn pawn, Thing eq, int frames, out Vector3 loc, out float aim)
        {
            loc = default;
            aim = 0f;
            if (!drawn.TryGetValue(pawn, out Drawn d) || d.eq != eq || Time.frameCount - d.frame > frames) return false;
            loc = d.loc;
            aim = d.aim;
            return true;
        }

        /// <summary>Where the game draws a drafted pawn's weapon (PawnRenderUtility.DrawCarriedWeapon): by the hip, 143 degrees, 217 facing west.</summary>
        public static void CarryPlace(Pawn pawn, out Vector3 loc, out float aim)
        {
            Rot4 facing = pawn.Rotation;
            loc = pawn.DrawPos;
            loc.y += PawnRenderUtility.AltitudeForLayer(facing == Rot4.North ? -10f : 90f);
            loc += EqLoc[facing.AsInt] * (pawn.ageTracker?.CurLifeStage?.equipmentDrawDistanceFactor ?? 1f);
            aim = facing == Rot4.West ? 217f : 143f;
        }

        /// <summary>The plane <paramref name="eq"/> is drawn on at <paramref name="loc"/> aiming <paramref name="aim"/>, as DrawEquipmentAiming turns it.</summary>
        public static UbwPose PoseAt(TraceShape shape, Thing eq, Vector3 loc, float aim)
        {
            float offset = eq.def.equippedAngleOffset, turn = aim - 90f;
            bool flip = false;
            if (aim > 20f && aim < 160f) turn += offset;
            else if (aim > 200f && aim < 340f)
            {
                flip = true;
                turn -= 180f + offset;
            }
            else turn += offset;
            Vector2 size = eq.Graphic?.drawSize ?? Vector2.one;
            double r = turn % 360f * UbwBlade.D2R, c = Math.Cos(r), s = Math.Sin(r);
            // A texture offset (du, dv) on the map: mirrored on a flipped mesh, sized, turned as Unity turns about up.
            UbwV3 Map(double du, double dv)
            {
                double x = size.x * du * (flip ? -1 : 1), z = size.y * dv;
                return new UbwV3(x * c + z * s, 0, -x * s + z * c);
            }
            UbwWeapon w = shape.W;
            UbwV3 tip = UbwV3.Plus(new UbwV3(loc.x, 0, loc.z), Map(w.TipU - 0.5, w.TipV - 0.5));
            UbwV3 along = Map(w.PommelU - w.TipU, w.PommelV - w.TipV);
            double length = Math.Sqrt(along.X * along.X + along.Z * along.Z);
            return UbwBlade.Pose(w, length / w.Length, tip, UbwV3.Unit(along), Map(w.AcrossU, w.AcrossV));
        }

        /// <summary>
        /// The pose of <paramref name="eq"/> in the pawn's hand this frame: where the game drew it, or with
        /// <paramref name="orCarry"/> where it would be carried. <paramref name="altitude"/> is the weapon's.
        /// </summary>
        public static bool Pose(Pawn pawn, Thing eq, TraceShape shape, bool orCarry, out UbwPose pose, out float altitude)
        {
            pose = null;
            altitude = 0f;
            if (shape == null || eq == null) return false;
            if (!DrawnAt(pawn, eq, 1, out Vector3 loc, out float aim))
            {
                if (!orCarry) return false;
                CarryPlace(pawn, out loc, out aim);
            }
            pose = PoseAt(shape, eq, loc, aim);
            altitude = loc.y;
            return true;
        }

        /// <summary>The grip of a held weapon, on the map: where the arm line ends and Trace On's flash shows.</summary>
        public static Vector2 Grip(UbwPose b)
        {
            UbwWeapon w = b.W;
            return TraceHandGraphics.Screen(UbwBlade.At3(b, new UbwUV(w.PommelU + (w.TipU - w.PommelU) * GripShare, w.PommelV + (w.TipV - w.PommelV) * GripShare)));
        }

        /// <summary>
        /// The circuit's place on the pawn: its DrawPos, the table narrowed for a body seen from the side, mirrored to
        /// the side the weapon is on, and run 0 carried on to the weapon's grip when there is one.
        /// </summary>
        public static TraceBody Body(Pawn pawn, UbwPose held)
        {
            Vector3 d = pawn.DrawPos;
            Rot4 facing = pawn.Rotation;
            var body = new TraceBody
            {
                Me = new Vector2(d.x, d.z),
                Side = facing == Rot4.West ? -1f : 1f,
                Width = facing == Rot4.East || facing == Rot4.West ? TraceCircuit.SideWidth : 1f,
                Altitude = d.y + BodyLift,
                Arm = TraceCircuit.Runs[0],
            };
            if (held == null) return body;
            Vector2 grip = Grip(held), middle = TraceHandGraphics.Middle(held);
            float dx = Mathf.Abs(middle.x - body.Me.x) > 0.05f ? middle.x - body.Me.x : grip.x - body.Me.x;
            body.Side = dx < 0f ? -1f : 1f;
            body.Arm = TraceCircuit.ArmTo(body.Table(grip));
            return body;
        }

        /// <summary>Where a pawn's hip is on the plane its weapon is drawn on: where a stowed weapon ends.</summary>
        public static UbwV3 Hip(TraceBody body)
        {
            Vector2 at = PawnFit.At(body.Me, 0.1f * body.Side, 0.032f);
            return new UbwV3(at.x, 0, at.y);
        }

        /// <summary>The way a moving pawn is heading on the map, or zero.</summary>
        public static Vector2 Heading(Pawn pawn)
        {
            if (pawn.pather == null || !pawn.pather.MovingNow) return Vector2.zero;
            Vector3 to = pawn.pather.nextCell.ToVector3Shifted() - pawn.DrawPos;
            var h = new Vector2(to.x, to.z);
            return h.sqrMagnitude < 1e-4f ? Vector2.zero : h.normalized;
        }
    }
}
