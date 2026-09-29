using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Shinra Tensei's numbers, on AG_ShinraTensei. One button: a tap is the quick version; a hold charges, and
    /// letting go before the first size's charge also gives the quick version. The charged release's Echo cost is the
    /// Echo's own cast cost (AG_Echo_Pain); the quick version pays <see cref="tapEchoCost"/> instead.
    /// </summary>
    public class ShinraTuning : DefModExtension
    {
        // The quick version.
        public float tapRadius = 2.5f;
        public float tapPush = 4f;
        public float tapWallDamage = 10f;
        public float tapShotLimit = 20f;
        public float tapDeflectSeconds = 0.45f;
        public float tapCooldownSeconds = 8f;
        public float tapEchoCost = 3f;

        // The charged version: power = seconds charged / fullChargeSeconds, 0 to 1.
        public float fullChargeSeconds = 3f;
        public float pushLow = 3f, pushHigh = 7f;
        public float wallDamageLow = 8f, wallDamageHigh = 20f;
        public float shotLimitLow = 12f, shotLimitHigh = 60f;
        public float deflectSeconds = 0.75f;
        /// <summary>From the smallest: each size is reached after its chargeSeconds of charge.</summary>
        public List<ShinraSize> sizes = new List<ShinraSize>();

        // Both.
        public float staggerSeconds = 0.5f;

        private static ShinraTuning cached;
        public static ShinraTuning Get => cached ??= PainDefOf.AG_ShinraTensei.GetModExtension<ShinraTuning>() ?? new ShinraTuning();

        public int FullChargeTicks => (int)(fullChargeSeconds * 60f);

        /// <summary>The size reached after <paramref name="seconds"/> of charge; -1 when it is short of the first.</summary>
        public int SizeAfter(float seconds)
        {
            int size = -1;
            for (int i = 0; i < sizes.Count; i++)
                if (seconds + 0.0001f >= sizes[i].chargeSeconds) size = i;
            return size;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (sizes.Count == 0) yield return "ShinraTuning needs at least one size";
            for (int i = 1; i < sizes.Count; i++)
                if (sizes[i].chargeSeconds <= sizes[i - 1].chargeSeconds) yield return "ShinraTuning sizes must grow in chargeSeconds";
            if (sizes.Count > 0 && sizes[sizes.Count - 1].chargeSeconds > fullChargeSeconds)
                yield return "ShinraTuning: the last size needs more charge than fullChargeSeconds";
        }
    }

    public class ShinraSize
    {
        public float chargeSeconds;
        public float radius;
        public float cooldownSeconds;
    }
}
