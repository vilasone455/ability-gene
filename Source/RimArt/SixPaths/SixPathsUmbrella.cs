using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>The umbrella's two open states: overhead for everyone under it, or held out in front.</summary>
    public enum UmbrellaMode { Canopy, Guard }

    /// <summary>One of the canopy's six panels, as the camera sees it this frame.</summary>
    public struct UmbrellaPanel
    {
        public int index;
        /// <summary>Radians about the canopy's axis to the panel's middle.</summary>
        public float centre;
        /// <summary>Lower is further from the camera and is painted first.</summary>
        public float near;
        /// <summary>The camera sees the shell's outside through this panel, not its ribbed inside.</summary>
        public bool outside;
        public float lit;
    }

    /// <summary>
    /// Pure clock and geometry for the Obsidian Umbrella: two orbs leave the sage's ring and form
    /// a closed umbrella in the hand, it thrusts once, opens, holds, folds and is held again.
    /// Opened overhead it is a six-panel canopy with a veil from its rim to the floor, and the
    /// other four orbs fly up into it. Opened in front it is a smaller dome along the pawn's
    /// facing, and those four stay in the ring. Seconds in, geometry out; nothing here draws.
    ///
    /// The canopy is built in three dimensions about an axis, up or along the facing, and then
    /// projected, so one routine serves both states. Overhead looks the same for every facing.
    /// In front does not: south shows the outside of the dome over the pawn, north shows the
    /// ribbed inside behind it, and east and west show a profile.
    ///
    /// The numbers are the ones picked in the VFX lab's Obsidian Umbrella sketch
    /// (Tools/VfxLab/web/sketches/six-paths-umbrella.js). There is no ability behind it yet.
    /// </summary>
    public static class SixPathsUmbrellaTiming
    {
        public const float Morph = 0.50f, Thrust = 0.80f, Open = 0.45f, Close = 0.40f, Tail = 0.60f;
        /// <summary>Seconds the preview holds it open. The sketch's canopy is meant to last 12.</summary>
        public const float Hold = 5f;

        /// <summary>Canopy radius and rim height, in cells, and how see-through the panels and the veil are.</summary>
        public const float Radius = 1.6f, RimHeight = 2.4f, Opacity = 0.68f, Veil = 0.5f;
        /// <summary>Apex above the rim, the middle ring as a share of the radius, and how deep each panel's hem is scalloped.</summary>
        public const float Dome = 0.75f, Mid = 0.62f, Scallop = 0.06f;
        public const float GuardRadius = 0.85f, GuardReach = 0.95f, GuardDepth = 0.55f, GuardHeight = 1.0f, GuardArc = 120f;
        public const int Panels = 6, PanelPoints = 9, SpindlePoints = 13, ArcPoints = 17;

        // The preview's script. In play these come from the pawn and from what hits it.
        public const float WalkSpeed = 0.6f, StartBack = 1.5f;
        public const float ShotGap = 0.55f, ShotFlight = 0.30f, ShotHeight = 0.9f, FlashSeconds = 0.30f;
        /// <summary>Blocked hits before it breaks: one panel of the canopy, or the whole guard.</summary>
        public const int BreakAt = 3;
        public const float ShardSeconds = 0.70f;

        public static float ThrustAt => Morph;
        public static float OpenAt => ThrustAt + Thrust;
        public static float UpAt => OpenAt + Open;
        public static float CloseAt => UpAt + Hold;
        public static float ClosedAt => CloseAt + Close;
        public static float Duration => ClosedAt + Tail;

        /// <summary>Hits that land while it is open, one every <see cref="ShotGap"/>.</summary>
        public static int Shots
        {
            get
            {
                int count = 0;
                while (UpAt + 0.3f + count * ShotGap + ShotFlight < CloseAt) count++;
                return count;
            }
        }

        public static float ShotLands(int shot) => UpAt + 0.3f + shot * ShotGap + ShotFlight;

        public static float BrokenAt => Shots >= BreakAt ? ShotLands(BreakAt - 1) : float.PositiveInfinity;

        /// <summary>0 to 1 for the flash of the blocked hit that landed most recently, 0 when none is showing.</summary>
        public static float Flash(float seconds)
        {
            for (int shot = 0; shot < Shots && shot < BreakAt; shot++)
            {
                float age = seconds - ShotLands(shot);
                if (age >= 0f && age <= FlashSeconds) return (1f - age / FlashSeconds) * (1f - age / FlashSeconds);
            }
            return 0f;
        }

        /// <summary>Cells the sage has walked along its facing.</summary>
        public static float Walked(float seconds) => Mathf.Clamp01((seconds - UpAt) / Hold) * Hold * WalkSpeed;

        /// <summary>The two orbs have become the closed umbrella.</summary>
        public static float Formed(float seconds) => Smooth(seconds / Morph);

        /// <summary>How far each of the two is from its ring slot to its spiral about the hand.</summary>
        public static float Gone(float seconds) => Smooth(seconds / (Morph * 0.5f));

        public static float Openness(float seconds, UmbrellaMode mode)
        {
            float open = Smooth((seconds - OpenAt) / Open) * (1f - Smooth((seconds - CloseAt) / Close));
            return mode == UmbrellaMode.Guard ? open * (1f - Smooth((seconds - BrokenAt - 0.05f) / 0.15f)) : open;
        }

        /// <summary>How far the four free orbs are from their slots to the apex. Only the canopy calls them.</summary>
        public static float Called(float seconds, UmbrellaMode mode) => mode != UmbrellaMode.Canopy ? 0f
            : Smooth((seconds - OpenAt) / (Open * 0.7f)) * (1f - Smooth((seconds - CloseAt - Close * 0.3f) / (Close * 0.7f)));

        /// <summary>The veil's hem, 0 at the rim and 1 on the floor.</summary>
        public static float VeilDrop(float seconds, UmbrellaMode mode) =>
            Smooth((seconds - OpenAt - Open * 0.5f) / (Open * 0.5f)) * Openness(seconds, mode);

        public static float Lunge(float seconds) =>
            Mathf.Sin(Mathf.Clamp01((seconds - ThrustAt - 0.25f) / 0.3f) * Mathf.PI);

        /// <summary>The closed umbrella brought down level for the thrust, and back up.</summary>
        public static float Level(float seconds) =>
            Smooth((seconds - ThrustAt) / 0.25f) * (1f - Smooth((seconds - ThrustAt - 0.55f) / 0.25f));

        /// <summary>Half the closed umbrella's width, <paramref name="u"/> of the way from hand to tip.</summary>
        public static float SpindleWidth(float u, float formed, float openness) =>
            (0.02f + 0.085f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Min(1f, u * 1.25f) * Mathf.PI)), 0.7f))
                * formed * (1f - openness * 0.7f);

        /// <summary>
        /// A drawn point in the sage's own frame: <paramref name="forward"/> cells toward the facing,
        /// <paramref name="across"/> cells to its left, <paramref name="height"/> cells up.
        /// </summary>
        public static Vector2 Rel(Vector2 toward, float forward, float across, float height = 0f) => new Vector2(
            toward.x * forward - toward.y * across,
            toward.y * forward + toward.x * across + height * SixPathsHeight.Lift);

        public static Vector2 Show(Vector3 point) => new Vector2(point.x, point.z + point.y * SixPathsHeight.Lift);

        /// <summary>The canopy's frame. <see cref="Vector3.y"/> is height here, not altitude.</summary>
        public struct Frame
        {
            public Vector3 axis, u, v, centre;
            public float depth, radius, fold;

            public Vector3 Point(float angle, float reach, float along)
            {
                Vector3 p = centre + u * (Mathf.Cos(angle) * reach) + v * (Mathf.Sin(angle) * reach) + axis * along;
                return new Vector3(p.x, Mathf.Max(0.02f, p.y), p.z);
            }

            public Vector3 Apex => Point(0f, 0f, depth);
            public Vector3 Rim(float angle, int panel)
            {
                float scallop = Mathf.Cos(3f * (angle - panel * Mathf.PI / 3f));
                return Point(angle, radius * (1f - Scallop * scallop * scallop), fold * depth * 0.6f);
            }
            public Vector3 Middle(float angle) => Point(angle, radius * Mid, depth * 0.78f);
        }

        /// <summary><see cref="Frame.u"/> points at the facing overhead, so panel 0 is the one a shot from the front hits.</summary>
        public static Frame FrameFor(UmbrellaMode mode, Vector2 toward, float openness)
        {
            var ahead = new Vector3(toward.x, 0f, toward.y);
            var left = new Vector3(-toward.y, 0f, toward.x);
            return mode == UmbrellaMode.Canopy
                ? new Frame { axis = Vector3.up, u = ahead, v = left, centre = new Vector3(0f, RimHeight, 0f),
                    depth = Dome, radius = Radius * openness, fold = 1f - openness }
                : new Frame { axis = ahead, u = left, v = Vector3.up,
                    centre = ahead * (GuardReach - GuardDepth) + new Vector3(0f, GuardHeight, 0f),
                    depth = GuardDepth, radius = GuardRadius * openness, fold = 1f - openness };
        }

        public static UmbrellaPanel Panel(in Frame frame, int index)
        {
            float c = index * Mathf.PI / 3f;
            Vector3 middle = frame.Point(c, frame.radius * 0.7f, frame.depth * 0.6f);
            Vector3 normal = (frame.u * Mathf.Cos(c) + frame.v * Mathf.Sin(c)) * 0.8f + frame.axis * 0.6f;
            return new UmbrellaPanel
            {
                index = index, centre = c, near = middle.y - 0.6f * middle.z,
                outside = normal.y - 0.6f * normal.z > 0f,
                lit = Mathf.Max(0f, normal.y * 0.5f - normal.z * 0.8f - normal.x * 0.35f),
            };
        }

        /// <summary>Where a blocked hit strikes, in the sage's frame: the veil overhead, the dome in front.</summary>
        public static Vector2 Strike(UmbrellaMode mode, Vector2 toward) => mode == UmbrellaMode.Canopy
            ? Rel(toward, Radius, 0f, ShotHeight) : Rel(toward, GuardReach, 0f, GuardHeight);

        /// <summary>One shard of what broke, relative to the sage; alpha 0 when none are falling.</summary>
        public static ImpactParticle Shard(int index, float seconds, UmbrellaMode mode, Vector2 toward)
        {
            float since = seconds - BrokenAt;
            if (since < 0f || since >= ShardSeconds) return default;
            float u = since / ShardSeconds, fall = 1f - u * u;
            Vector2 at;
            if (mode == UmbrellaMode.Canopy)
            {
                float angle = Mathf.Atan2(toward.y, toward.x) + VfxMath.Rand(index) - 0.5f;
                float reach = Radius * (0.5f + VfxMath.Rand(index + 4) * 0.5f) + u * 0.5f;
                at = new Vector2(Mathf.Cos(angle) * reach,
                    Mathf.Sin(angle) * reach + (RimHeight + Dome * 0.4f) * fall * SixPathsHeight.Lift);
            }
            else
                at = Rel(toward, GuardReach - 0.3f + u * 0.6f * VfxMath.Rand(index + 2),
                    (VfxMath.Rand(index) - 0.5f) * 1.6f * (1f + u * 0.5f),
                    (GuardHeight + (VfxMath.Rand(index + 7) - 0.5f) * 1.4f) * fall);
            return new ImpactParticle
            {
                x = at.x, z = at.y, size = 0.13f * (1f - u * 0.4f),
                rotation = -since * (4f + index) * Mathf.Rad2Deg, alpha = 1f - u,
            };
        }

        public static int Shards(UmbrellaMode mode) => mode == UmbrellaMode.Canopy ? 8 : 12;
    }
}
