using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Paradise Lost's numbers on top of the Corrosion ones (docs/ego-weapons.md, Weapon 4). The range is the verb's, the
    /// shot interval the verb's warmup plus the weapon's RangedWeapon_Cooldown, the damage to a single pawn the round's
    /// (AG_EgoParadiseLost_Round), all in the weapon's def, so the info card shows them. The slow's speed factor is the
    /// AG_EgoParadiseLostSlow hediff's MoveSpeed factor.
    /// </summary>
    public class CompProperties_EgoParadiseLost : CompProperties_EgoWeapon
    {
        /// <summary>Damage to each pawn when a room hit strikes 2 to <see cref="fewMax"/> pawns, and to each when it strikes more.</summary>
        public float damageFew = 12f;
        public float damageMany = 9f;
        public int fewMax = 5;
        /// <summary>Outdoors (the aimed pawn's room touches the map edge): every hostile there within this many cells of the aimed pawn.</summary>
        public float outdoorRadius = 6f;
        /// <summary>Seconds every pawn a room hit or a ring strikes is slowed.</summary>
        public float slowSeconds = 1f;
        /// <summary>Mood per hostile a room hit strikes, the most the thought holds, and the game hours it lasts after the last hit.</summary>
        public int sanityPerHit = 1;
        public int sanityCap = 10;
        public float sanityHours = 2f;
        /// <summary>
        /// WhiteNight's ring: the first ring's radius, the cells each later ring of the same corrosion adds, and its damage.
        /// Overclock's rings stay at ringRadius.
        /// </summary>
        public float ringRadius = 6f;
        public float ringGrowth = 3f;
        public float ringDamage = 12f;
        /// <summary>Played at the wielder when a ring fires; none when unset.</summary>
        public SoundDef ringSound;

        public CompProperties_EgoParadiseLost()
        {
            compClass = typeof(CompEgoParadiseLost);
        }
    }

    /// <summary>
    /// The staff's state: how many rings the running corrosion has fired, which sets the next ring's radius. Its holder is
    /// registered with <see cref="GameComponent_EgoParadiseLost"/>, which draws the staff instead of Core. The shooting is
    /// <see cref="Verb_EgoParadiseLost"/>, the corroded attack <see cref="EgoParadiseLostCorrosion"/>.
    /// </summary>
    public class CompEgoParadiseLost : CompEgoWeapon
    {
        /// <summary>Rings fired since the corrosion started; 0 outside one.</summary>
        public int rings;

        public new CompProperties_EgoParadiseLost Props => (CompProperties_EgoParadiseLost)props;

        public new static CompEgoParadiseLost HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompEgoParadiseLost>();

        /// <summary>The radius of the next corroded ring: ringRadius, plus ringGrowth for each ring before it.</summary>
        public float NextRingRadius => Props.ringRadius + Props.ringGrowth * rings;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            GameComponent_EgoParadiseLost.Instance?.Register(pawn);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            GameComponent_EgoParadiseLost.Instance?.Unregister(pawn);
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn holder = Holder;
            if (holder != null && holder.IsColonistPlayerControlled && holder.needs?.mood != null) yield return EgoParadiseLostCommands.Sanity(this, holder);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref rings, "rings");
        }
    }

    /// <summary>Sanity in the command bar: the mood the hits have given, out of the cap.</summary>
    [StaticConstructorOnStartup]
    public static class EgoParadiseLostCommands
    {
        private static readonly Texture2D Fill = SolidColorMaterials.NewSolidColorTexture(new Color(0.96f, 0.76f, 0.26f));

        public static Gizmo_Meter Sanity(CompEgoParadiseLost staff, Pawn holder)
        {
            CompProperties_EgoParadiseLost p = staff.Props;
            return new Gizmo_Meter("Sanity", () => EgoParadiseLost.Sanity(holder), p.sanityCap, Fill)
            {
                valueLabel = () => "+" + EgoParadiseLost.Sanity(holder) + " / " + p.sanityCap,
                tip = () => "Mood from Paradise Lost's hits: +" + p.sanityPerHit + " for each hostile a shot strikes, up to +" + p.sanityCap
                    + ". It lasts " + p.sanityHours.ToString("0.#") + " h after the last hit. The ring gives none.",
            };
        }
    }
}
