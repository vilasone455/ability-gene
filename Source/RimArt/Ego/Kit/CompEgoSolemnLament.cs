using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Solemn Lament's numbers on top of the Corrosion ones (docs/ego-weapons.md, Weapon 2). The guns' range, the seconds
    /// between shots (ticksBetweenBurstShots) and the black shot's damage (AG_EgoSolemnLament_Round) are the verb's and the
    /// round's, in the weapon's def; Butterfly's cap, consciousness and fade are on the AG_EgoButterfly hediff def.
    /// </summary>
    public class CompProperties_EgoSolemnLament : CompProperties_EgoWeapon
    {
        /// <summary>Butterfly stacks a white shot and a black shot put on, never past the cap.</summary>
        public int whiteStacks = 2;
        public int blackStacks = 1;
        /// <summary>Shots in the pool, and seconds to reload when it is empty.</summary>
        public int ammo = 20;
        public float reloadSeconds = 3f;
        /// <summary>The coffin: cells round the wielder its cloud covers, and stacks it puts on each pawn in it per firing.</summary>
        public float coffinRadius = 3f;
        public int cloudStacks = 1;
        /// <summary>The funeral: a pawn the corroded cloud brings to this many stacks dies. Overclock stops at the cap.</summary>
        public int funeralStacks = 20;

        public CompProperties_EgoSolemnLament()
        {
            compClass = typeof(CompEgoSolemnLament);
        }
    }

    /// <summary>
    /// The pair's state, kept on the weapon: which gun fires next (they take turns across bursts, white first) and the
    /// ammo pool, refilled reloadSeconds after it runs dry. The shooting is <see cref="Verb_EgoSolemnLament"/>.
    /// </summary>
    public class CompEgoSolemnLament : CompEgoWeapon
    {
        public bool nextWhite = true;
        private int ammo = -1;
        /// <summary>The tick the pool is full again, while it reloads.</summary>
        public int reloadUntilTick = -1;

        public new CompProperties_EgoSolemnLament Props => (CompProperties_EgoSolemnLament)props;

        public new static CompEgoSolemnLament HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompEgoSolemnLament>();

        public bool Reloading => Find.TickManager.TicksGame < reloadUntilTick;

        /// <summary>Shots left. A new gun comes full; an empty one fills when its reload is over.</summary>
        public int Ammo
        {
            get
            {
                if (ammo < 0 || (ammo == 0 && !Reloading)) ammo = Props.ammo;
                return Reloading ? 0 : ammo;
            }
        }

        public int ReloadTicksLeft => Mathf.Max(0, reloadUntilTick - Find.TickManager.TicksGame);

        /// <summary>One shot went off: the pool drops by one and the reload starts when it is empty. Returns whether the shot was white.</summary>
        public bool Spend()
        {
            bool white = nextWhite;
            nextWhite = !nextWhite;
            ammo = Ammo - 1;
            if (ammo <= 0)
            {
                ammo = 0;
                reloadUntilTick = Find.TickManager.TicksGame + Props.reloadSeconds.SecondsToTicks();
            }
            return white;
        }

        /// <summary>A test fills the pool now.</summary>
        public void Refill()
        {
            reloadUntilTick = -1;
            ammo = Props.ammo;
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn holder = Holder;
            if (holder != null && holder.IsColonistPlayerControlled) yield return EgoSolemnLamentCommands.Pool(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextWhite, "nextWhite", true);
            Scribe_Values.Look(ref ammo, "ammo", -1);
            Scribe_Values.Look(ref reloadUntilTick, "reloadUntilTick", -1);
        }
    }

    /// <summary>The ammo pool in the command bar: one segment per shot, a reload countdown when empty.</summary>
    [StaticConstructorOnStartup]
    public static class EgoSolemnLamentCommands
    {
        private static readonly Texture2D Fill = SolidColorMaterials.NewSolidColorTexture(new Color(0.86f, 0.86f, 0.90f));
        private static readonly Texture2D ReloadFill = SolidColorMaterials.NewSolidColorTexture(new Color(0.30f, 0.30f, 0.34f));

        public static Gizmo_Meter Pool(CompEgoSolemnLament gun)
        {
            CompProperties_EgoSolemnLament p = gun.Props;
            return new Gizmo_Meter("Rounds", () => gun.Reloading ? p.ammo * (1f - gun.ReloadTicksLeft / (float)p.reloadSeconds.SecondsToTicks()) : gun.Ammo, p.ammo, Fill)
            {
                hot = () => gun.Reloading,
                hotFill = ReloadFill,
                valueLabel = () => gun.Reloading ? "reload " + gun.ReloadTicksLeft.ToStringSecondsFromTicks() : gun.Ammo + " / " + p.ammo,
                tip = () => "Shots left in the pair: " + gun.Ammo + " of " + p.ammo + ". The guns take turns, white then black. A white shot puts "
                    + p.whiteStacks + " Butterfly stacks on the target and does no harm; a black shot does damage and puts on " + p.blackStacks
                    + ". The guns never put on more than the cap. Empty, the pair reloads for " + p.reloadSeconds.ToString("0.#") + " s.",
            };
        }
    }
}
