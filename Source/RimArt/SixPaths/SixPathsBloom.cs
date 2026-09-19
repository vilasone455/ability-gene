using System;
using UnityEngine;

namespace RimArt
{
    /// <summary>How open, how shut and how far gone one petal is, each 0 to 1.</summary>
    public struct PetalPose
    {
        public float angle, grow, shut, dissolve;
    }

    /// <summary>One cross-section of a petal, in cells east and north of the patient, height already drawn in.</summary>
    public struct PetalRow
    {
        public Vector2 left, right, spine, seam, shadowLeft, shadowRight;
    }

    /// <summary>
    /// Pure clock for Obsidian Bloom: one orb leaves the sage's ring, flies to a patient and sinks
    /// under it, six petals unfurl from the floor, poise, fold shut over the patient into a bud,
    /// hold with one seam pulse per heal tick, uncurl, and the orb goes back to its slot. Seconds
    /// in, geometry out; nothing here draws or touches the map.
    ///
    /// It closes slowly and without a camera shake: a fast snap reads as an attack.
    ///
    /// The numbers are the ones picked in the VFX lab's Obsidian Bloom sketch
    /// (Tools/VfxLab/web/sketches/six-paths-bloom.js). There is no ability behind it yet.
    /// </summary>
    public static class SixPathsBloomTiming
    {
        public const float Sink = 0.40f, Unfurl = 0.65f, Poise = 0.30f, Fold = 0.50f, Hold = 6f,
            Uncurl = 0.65f, Reform = 0.50f;
        /// <summary>Seconds between heal ticks while the bud is shut.</summary>
        public const float Interval = 1f;
        /// <summary>Open petal reach and closed bud height, in cells.</summary>
        public const float Radius = 1.4f, Height = 2f;
        /// <summary>Degrees the flower is turned, so that no petal points straight at the camera.</summary>
        public const float Spin = 15f;
        public const int Petals = 6, Rows = 33, Motes = 10, TrailPoints = 14;
        public const float MoteStrength = 0.7f;
        /// <summary>The share of <see cref="Sink"/> the orb spends flying; the rest it spends going under.</summary>
        public const float FlightShare = 0.7f;
        /// <summary>Cells the orb's arc rises above the straight line, and seconds of path its trail covers.</summary>
        public const float Arc = 0.8f, TrailSeconds = 0.14f;

        public static float UnfurlAt => Sink;
        public static float PoiseAt => UnfurlAt + Unfurl;
        public static float FoldAt => PoiseAt + Poise;
        public static float ShutAt => FoldAt + Fold;
        public static float UncurlAt => ShutAt + Hold;
        public static float ReturnAt => UncurlAt + Uncurl;
        public static float Duration => ReturnAt + Reform;

        private static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);

        /// <summary>
        /// 0 to 1, fixed per index. The same sine hash as the lab's six-paths-impact.js, in double
        /// precision as it is there, so the motes rise where they did in the sketch.
        /// </summary>
        public static float Rand(int index)
        {
            double n = Math.Sin(index * 127.1 + 17) * 43758.5453;
            return (float)(n - Math.Floor(n));
        }

        /// <summary>How far under the patient the orb has spread, which is what the floor darkens by.</summary>
        public static float Sunk(float seconds) => Smooth(seconds / Sink);

        /// <summary>The bud: 0 open, 1 shut.</summary>
        public static float Shut(float seconds) =>
            Smooth((seconds - FoldAt) / Fold) * (1f - Smooth((seconds - UncurlAt) / Uncurl));

        /// <summary>0 to 1 as the petals go and the orb flies home.</summary>
        public static float Reformed(float seconds) => Smooth((seconds - ReturnAt) / Reform);

        /// <summary>Heal tick strength: 1 at each tick, decaying before the next.</summary>
        public static float Pulse(float seconds) => seconds >= ShutAt && seconds < UncurlAt
            ? Mathf.Exp(-((seconds - ShutAt) % Interval) / Interval * 4f) : 0f;

        public static PetalPose Petal(int index, float seconds) => new PetalPose
        {
            angle = index / (float)Petals * Mathf.PI * 2f + Spin * Mathf.Deg2Rad,
            grow = Smooth((seconds - Sink - index * 0.025f) / (Unfurl - 0.125f)),
            shut = Shut(seconds),
            dissolve = 1f - Reformed(seconds),
        };

        /// <summary>Row <paramref name="row"/> of <see cref="Rows"/>, from the petal's root to its tip.</summary>
        public static PetalRow Row(in PetalPose pose, int row)
        {
            float u = row / (float)(Rows - 1), radius = Radius * pose.grow * pose.dissolve;
            // Sin of pi is a hair below zero in single precision, and a power of that is not a number.
            float arch = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
            // At closure the tip returns over the centre while the belly bulges outward.
            float reach = Mathf.Lerp(0.18f + radius * u, 0.28f * (1f - u) + radius * 0.47f * arch, pose.shut);
            float height = Mathf.Lerp(arch * 0.25f * pose.grow, Height * u * pose.grow, pose.shut) * pose.dissolve;
            float width = Mathf.Pow(arch, 0.85f) * radius * 0.40f * (1f - pose.shut * 0.2f);
            var along = new Vector2(Mathf.Cos(pose.angle), Mathf.Sin(pose.angle));
            var across = new Vector2(-along.y, along.x);
            Vector2 centre = along * reach, lift = new Vector2(0f, height * SixPathsHeight.Lift);
            return new PetalRow
            {
                left = centre + across * width + lift,
                right = centre - across * width + lift,
                spine = centre + new Vector2(0f, (height + arch * 0.14f) * SixPathsHeight.Lift),
                seam = centre + across * (width * 0.96f) + lift,
                shadowLeft = centre + across * width,
                shadowRight = centre - across * width,
            };
        }

        /// <summary>
        /// The orb's flight, <paramref name="flown"/> 0 at its ring slot and 1 at the patient. Both
        /// ends are drawn points, height included; the patient is the origin.
        /// </summary>
        public static Vector2 Path(Vector2 slot, float flown) =>
            slot * (1f - flown) + new Vector2(0f, Mathf.Sin(flown * Mathf.PI) * Arc * SixPathsHeight.Lift);

        /// <summary>How far along <see cref="Path"/> the outbound orb is.</summary>
        public static float FlownOut(float seconds) => Smooth(seconds / (Sink * FlightShare));

        /// <summary>0 to 1 as the outbound orb goes under the floor.</summary>
        public static float Drop(float seconds) => Smooth((seconds - Sink * FlightShare) / (Sink * (1f - FlightShare)));

        /// <summary>One rising heal mote, relative to the patient; alpha 0 when it is not showing.</summary>
        public static ImpactParticle Mote(int index, float seconds)
        {
            float life = 1.4f + Rand(index) * 0.8f, age = (seconds - ShutAt) / life + Rand(index + 9);
            if (age < 0f) return default;
            float u = age % 1f, angle = Rand(index + 30) * Mathf.PI * 2f, reach = Radius * 0.42f * (1f - u * 0.5f);
            return new ImpactParticle
            {
                x = Mathf.Cos(angle) * reach, z = Mathf.Sin(angle) * reach * 0.6f,
                height = Height * (0.25f + u * 0.95f), size = 0.12f,
                alpha = Mathf.Sin(u * Mathf.PI) * MoteStrength * Shut(seconds),
            };
        }
    }
}
