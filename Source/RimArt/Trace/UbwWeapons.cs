using System;

namespace RimArt
{
    /// <summary>
    /// A weapon the swords of Unlimited Blade Works are copies of, as the lab's lib/trace.js Weapons table
    /// describes one: the picture's tip and pommel in uv (v up), the blade's extent across its axis in 16
    /// steps from the tip, and how many cells the whole picture spans at size 1. The axis and the across
    /// direction follow from the tip and the pommel. System only, so Tests/Ubw links it without stubs.
    /// </summary>
    public sealed class UbwWeapon
    {
        public readonly string Name;
        /// <summary>Its row in the atlas: the picture in every shade (see UbwAtlas in UbwGraphics.cs).</summary>
        public readonly int Row;
        public readonly double Image, TipU, TipV, PommelU, PommelV, Length, AxisU, AxisV, AcrossU, AcrossV;
        /// <summary>[k, 0] and [k, 1]: the blade's extent across its axis, in uv, in step k of 16 from the tip.</summary>
        public readonly double[,] Width = new double[16, 2];

        /// <param name="width">32 numbers: lo and hi of each of the 16 steps.</param>
        public UbwWeapon(string name, int row, double image, double tipU, double tipV, double pommelU, double pommelV, double[] width)
        {
            Name = name;
            Row = row;
            Image = image;
            TipU = tipU; TipV = tipV; PommelU = pommelU; PommelV = pommelV;
            for (int k = 0; k < 16; k++) { Width[k, 0] = width[k * 2]; Width[k, 1] = width[k * 2 + 1]; }
            double du = pommelU - tipU, dv = pommelV - tipV;
            Length = Math.Sqrt(du * du + dv * dv);
            AxisU = du / Length; AxisV = dv / Length;
            AcrossU = -dv / Length; AcrossV = du / Length;
        }
    }

    /// <summary>
    /// The weapons the world's swords are mixed from. In the lab they are the six reference weapons of
    /// make_trace_trial_textures.py, with the numbers that script printed (the sketch's table). In game the
    /// kit reads the weapons' own textures at load (Trace/Kit/UbwAtlasBuilder.cs) and makes a set of its own.
    /// </summary>
    public static class UbwWeapons
    {
        /// <summary>The atlas rows: Knife, LongSword, Spear, MonoSword, LargeSword, Wyrmslayer.</summary>
        public static readonly UbwWeapon[] Lab =
        {
            new UbwWeapon("Knife", 0, 1.0, .8337, .4964, .2792, .5031, new[] { -.0251, .0065, -.0247, .0265, -.0356, .0464, -.0586, .0586, -.0661, .0629, -.0659, .0633, -.0652, .0638, -.0651, .0642, -.0645, .0646, -.0835, .0806, -.0834, .081, -.083, .0811, -.0435, .0428, -.0584, .0549, -.0583, .0593, -.054, .0594 }),
            new UbwWeapon("LongSword", 1, 1.2, .9668, .531, .0176, .5312, new[] { -.0451, .0447, -.0646, .0682, -.0685, .0682, -.0685, .0682, -.0685, .0682, -.0685, .0682, -.0685, .0682, -.0685, .0682, -.0685, .0683, -.0685, .0683, -.0685, .0683, -.1114, .1113, -.1114, .1113, -.0528, .0488, -.0645, .0683, -.0645, .0683 }),
            new UbwWeapon("Spear", 2, 1.65, .9981, .5031, .0174, .4988, new[] { -.042, .0439, -.0618, .0632, -.0698, .0631, -.0583, .0551, -.0431, .047, -.0434, .0467, -.0437, .0464, -.044, .0462, -.0442, .0459, -.0445, .0456, -.0448, .0453, -.0451, .0451, -.0453, .0448, -.0456, .0445, -.0459, .0442, -.0461, .044 }),
            new UbwWeapon("MonoSword", 3, 1.18, .9824, .5931, .0303, .3687, new[] { -.1329, -.0084, -.1135, .0531, -.088, .0871, -.0719, .1059, -.0646, .1041, -.0599, .1038, -.0595, .1103, -.0651, .1118, -.0718, .1105, -.0862, .1055, -.1043, .0939, -.1265, .0936, -.1213, .0956, -.1149, .0623, -.0536, .0403, -.0599, .026 }),
            new UbwWeapon("LargeSword", 4, 1.22, .9169, .9035, .0484, .0336, new[] { -.1393, .1396, -.1394, .1396, -.1395, .1396, -.1395, .1395, -.1396, .1394, -.1397, .1394, -.1397, .1393, -.1398, .1393, -.1399, .1392, -.1399, .1391, -.14, .1391, -.1649, .1666, -.0296, .0312, -.0352, .0367, -.0353, .0365, -.0353, .0365 }),
            new UbwWeapon("Wyrmslayer", 5, 1.3, .9818, .9792, .0791, .0534, new[] { -.0746, .0718, -.1199, .1205, -.1264, .1251, -.1301, .1297, -.1339, .1343, -.1405, .1389, -.1443, .1458, -.1508, .1504, -.1546, .155, -.1584, .1596, -.165, .1642, -.1687, .1711, -.1747, .1735, -.0437, .0982, -.0503, .041, -.0589, .0543 }),
        };

        /// <summary>
        /// The mix of the field, by row: three longswords, two spears, two monoswords, a knife, a large sword
        /// and a Wyrmslayer in ten (lib/ubw-pocket.js Mix). A set with fewer rows wraps.
        /// </summary>
        public static readonly int[] Mix = { 1, 1, 1, 2, 2, 3, 3, 0, 4, 5 };

        public static UbwWeapon Pick(UbwWeapon[] set, int mixIndex) => set[Mix[mixIndex] % set.Length];
    }
}
