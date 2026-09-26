using UnityEngine;

namespace RimArt
{
    /// <summary>One pawn Chain Arc hits, as the picture needs it.</summary>
    public struct CoilArcHop
    {
        /// <summary>The pawn's feet (its DrawPos on the ground) when the chain was decided.</summary>
        public Vector2 At;
        /// <summary>Where it is now, for the bolt and the flash. Null uses <see cref="At"/>.</summary>
        public Vector2? LiveAt;
        public bool Soaked, Mech;
        /// <summary>Cells the next jump may reach from this pawn: the jump radius, doubled when it is Soaked. 0 for the last.</summary>
        public float Reach;
    }

    /// <summary>One Chain Arc as the picture needs it.</summary>
    public struct CoilArcShot
    {
        public Vector2 Muzzle;
        /// <summary>Seconds on the picture's clock when the gun lets go: <see cref="CoilGunArcTiming.Lead"/> plus the warmup.</summary>
        public float Fire;
        /// <summary>The pawns hit, in order. Empty while the charge is still building.</summary>
        public CoilArcHop[] Hops;
        public int Seed;
    }

    /// <summary>
    /// Timing of Chain Arc: seconds in, times out, no drawing and no map. There is no sketch; these are
    /// the picture's own constants. The charge takes the ability's warmup (0.5 s in the XML and in the
    /// preview). Then the first bolt reaches target 1 in <see cref="Strike"/>; each next jump leaves
    /// <see cref="Hop"/> after the one before. A bolt burns bright for <see cref="Lit"/> after it
    /// arrives and fades over <see cref="Fade"/>; the scorch marks stay <see cref="ScorchStay"/>.
    /// </summary>
    public static class CoilGunArcTiming
    {
        public const float Lead = 0.15f, ScriptCharge = 0.5f;
        public const float Hop = 0.08f, Strike = 0.03f, Lit = 0.14f, Fade = 0.14f;
        public const float ScorchStay = 1.2f;
        public const float HitShake = 0.02f;
        /// <summary>After the last hit the caster may go this much later: the last bolt has faded.</summary>
        public const float HoldAfterLastHit = 0.2f;

        public static float Start(float fire, int i) => fire + i * Hop;
        public static float Hit(float fire, int i) => Start(fire, i) + Strike;
        public static float LastHit(float fire, int hops) => Hit(fire, Mathf.Max(0, hops - 1));
        public static float Holds(float fire, int hops) => LastHit(fire, hops) + HoldAfterLastHit;
        public static float End(float fire, int hops) => LastHit(fire, hops) + ScorchStay + 0.7f;

        // ------------------------------------------------------------------ the preview's script

        /// <summary>
        /// The preview: the caster's feet at 0 along the aim, four targets further out (aim frame,
        /// along and across). Target 2 is Soaked, so the jump from it to target 3 (4.3 cells) is allowed
        /// only because its reach is doubled to 6; the other jumps are under 3 cells.
        /// </summary>
        public static readonly Vector2[] ScriptTargets =
        {
            new Vector2(4.5f, 0.2f),
            new Vector2(6.6f, -1.2f),
            new Vector2(9.4f, 2.0f),
            new Vector2(11.2f, 0.5f),
        };
        public const int ScriptSoaked = 1;
        public const float ScriptJump = 3f, ScriptSoakedFactor = 2f;
        /// <summary>The preview's centre is this far along from the caster's feet: the middle of the chain.</summary>
        public const float ScriptCentreAlong = 5.6f;
        public static float ScriptFire => Lead + ScriptCharge;
        public static float ScriptEnd => End(ScriptFire, ScriptTargets.Length);

        public static CoilArcShot Script(Vector2 centre, float aimDegrees)
        {
            Vector2 along = CoilGunGraphics.Dir(aimDegrees), across = new Vector2(-along.y, along.x);
            Vector2 feet = centre - along * ScriptCentreAlong;
            var hops = new CoilArcHop[ScriptTargets.Length];
            for (int i = 0; i < hops.Length; i++)
            {
                bool soaked = i == ScriptSoaked;
                hops[i] = new CoilArcHop
                {
                    At = feet + along * ScriptTargets[i].x + across * ScriptTargets[i].y,
                    Soaked = soaked,
                    Reach = i == hops.Length - 1 ? 0f : ScriptJump * (soaked ? ScriptSoakedFactor : 1f),
                };
            }
            // In game the gun points at target 1, so the muzzle is along that line.
            Vector2 toFirst = (hops[0].At - feet).normalized;
            return new CoilArcShot { Muzzle = feet + toFirst * CoilGunGraphics.MuzzleAlong, Fire = ScriptFire, Hops = hops, Seed = 17 };
        }
    }
}
