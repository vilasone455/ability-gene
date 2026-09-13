using System.Runtime.CompilerServices;

namespace RimArt
{
    /// <summary>
    /// The aim correction for each playing throw animation, keyed by Melee Animation's renderer.
    ///
    /// Written by <see cref="ThrowAnimation.TryThrow"/> right after the clip starts, and read every
    /// frame by RimArt.MeleeAnimation.ThrowAimWorker, which rotates the throwing hand and the held
    /// item by this angle. The two sides live in different assemblies - this one must load without
    /// Melee Animation, the worker cannot - so this table is how they meet. Keys are held weakly:
    /// a finished animation's entry goes when its renderer is collected.
    /// </summary>
    public static class ThrowAim
    {
        private sealed class Box
        {
            public float Degrees;
        }

        private static readonly ConditionalWeakTable<object, Box> offsets = new ConditionalWeakTable<object, Box>();

        /// <summary>Degrees counter-clockwise seen from above, from the clip's direction to the target.</summary>
        public static void Set(object renderer, float degrees)
        {
            if (renderer == null) return;
            offsets.GetOrCreateValue(renderer).Degrees = degrees;
        }

        public static bool TryGet(object renderer, out float degrees)
        {
            degrees = 0f;
            if (renderer == null || !offsets.TryGetValue(renderer, out Box box)) return false;
            degrees = box.Degrees;
            return true;
        }
    }
}
