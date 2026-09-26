using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One research tier of the resonance device. The tier in force is the last one whose research
    /// is finished (a tier with no research is the base). All four numbers are balance.
    /// </summary>
    public class EchoDeviceTier
    {
        public ResearchProjectDef research;
        public float maxCharge = 100f;
        public float refillPerHour = 5f;
        public int heroCap = 2;
    }

    public class CompProperties_EchoDevice : CompProperties
    {
        public List<EchoDeviceTier> tiers = new List<EchoDeviceTier>();

        public CompProperties_EchoDevice() => compClass = typeof(CompEchoDevice);

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (tiers.NullOrEmpty()) yield return "no tiers";
        }
    }

    /// <summary>
    /// The resonance device. It holds no state of its own: the charge pool is the colony's
    /// (GameComponent_Echoes), and a device only matters for whether it is there and powered, which
    /// is what refills the pool.
    /// </summary>
    public class CompEchoDevice : ThingComp
    {
        public bool Working => parent.Spawned && (parent.TryGetComp<CompPowerTrader>()?.PowerOn ?? true)
            && !parent.IsBrokenDown();

        public override string CompInspectStringExtra()
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            if (echoes == null) return null;
            return "AG_EchoDeviceInspect".Translate(echoes.charge.ToString("0"), echoes.MaxCharge.ToString("0"),
                Working ? echoes.RefillPerHour.ToString("0.#") : "0", echoes.HostCount, echoes.HeroCap);
        }
    }

    public static class EchoDevice
    {
        private static CompProperties_EchoDevice props;

        public static CompProperties_EchoDevice Props =>
            props ?? (props = EchoDefOf.AG_EchoDevice.GetCompProperties<CompProperties_EchoDevice>());

        public static EchoDeviceTier CurrentTier
        {
            get
            {
                List<EchoDeviceTier> tiers = Props.tiers;
                EchoDeviceTier current = tiers[0];
                for (int i = 0; i < tiers.Count; i++)
                    if (tiers[i].research == null || tiers[i].research.IsFinished) current = tiers[i];
                return current;
            }
        }

        public static EchoDeviceTier NextTier
        {
            get
            {
                EchoDeviceTier current = CurrentTier;
                List<EchoDeviceTier> tiers = Props.tiers;
                int index = tiers.IndexOf(current);
                return index + 1 < tiers.Count ? tiers[index + 1] : null;
            }
        }

        /// <summary>Set by game tests, which have no power grid: true or false stands in for the search.</summary>
        internal static bool? workingForTests;

        /// <summary>Any working device on any player home map.</summary>
        public static bool AnyWorking()
        {
            if (workingForTests.HasValue) return workingForTests.Value;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                if (!maps[m].IsPlayerHome) continue;
                foreach (Building building in maps[m].listerBuildings.AllBuildingsColonistOfDef(EchoDefOf.AG_EchoDevice))
                    if (building.TryGetComp<CompEchoDevice>()?.Working == true) return true;
            }
            return false;
        }
    }
}
