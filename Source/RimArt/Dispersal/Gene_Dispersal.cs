using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The organ. It holds a small number of dispersals and grows them back slowly.
    ///
    /// State lives on the gene rather than on a hediff for the same reason the anchor organ's
    /// marks do: what is held belongs to a person. It saves and loads with them, it travels
    /// between maps with them, and it is gone the moment the gene is.
    ///
    /// Nothing here does the scattering. This class only answers "is there one left", spends
    /// it when told, and shows the player how many are in hand - the dispersal itself is in
    /// <see cref="Scatter"/>, reached from a Thing.TakeDamage prefix, while the player controls whether automatic scatter is enabled.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Gene_Dispersal : Gene
    {
        private int charges = -1;
        private int rechargeProgress;
        private bool autoScatter = true;
        private bool staleAbilitiesChecked;

        public bool AutoScatter => autoScatter;

        /// <summary>
        /// Guards the recharge against a carrier running accelerated ticks.
        ///
        /// Same problem and same answer as <see cref="HediffComp_TemporalStrain"/>: a pawn under
        /// the time lattice ticks their genes several times inside one game tick, and without
        /// this the plexus would refill at the multiplier. Squaring your own reflexes should
        /// not also be a way to buy charges.
        /// </summary>
        private int lastGameTickProcessed = -1;

        private static Texture2D icon;

        private DispersalGeneExtension Ext => def.GetModExtension<DispersalGeneExtension>();

        public int MaxCharges
        {
            get
            {
                DispersalGeneExtension ext = Ext;
                return ext != null ? ext.maxCharges : 3;
            }
        }

        public int RechargeTicks
        {
            get
            {
                DispersalGeneExtension ext = Ext;
                return ext != null ? ext.rechargeTicks : 2500;
            }
        }

        public float MinimumDamage
        {
            get
            {
                DispersalGeneExtension ext = Ext;
                return ext != null ? ext.minimumDamage : 6f;
            }
        }

        public float BloodLossPerScatter
        {
            get
            {
                DispersalGeneExtension ext = Ext;
                return ext != null ? ext.bloodLossPerScatter : 0.04f;
            }
        }

        public int MinRange
        {
            get
            {
                DispersalGeneExtension ext = Ext;
                return ext != null ? ext.minRange : 5;
            }
        }

        public int MaxRange
        {
            get
            {
                DispersalGeneExtension ext = Ext;
                return ext != null ? ext.maxRange : 10;
            }
        }

        /// <summary>Charges in hand. Starts full, which is why the backing field is -1 until
        /// the first read: MaxCharges is not readable until the def is attached.</summary>
        public int Charges
        {
            get
            {
                if (charges < 0) charges = MaxCharges;
                return charges;
            }
        }

        public bool HasCharge => Charges > 0;

        /// <summary>How far through growing the next one, 0 to 1. Only meaningful below max.</summary>
        public float RechargeFraction
        {
            get
            {
                int span = RechargeTicks;
                return span <= 0 ? 1f : Mathf.Clamp01(rechargeProgress / (float)span);
            }
        }

        public void Spend()
        {
            charges = Mathf.Max(0, Charges - 1);
        }

        /// <summary>Back to full. Only the debug actions call this.</summary>
        public void Refill()
        {
            charges = MaxCharges;
            rechargeProgress = 0;
        }

        public override void PostAdd()
        {
            base.PostAdd();
            charges = MaxCharges;
            rechargeProgress = 0;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (pawn == null || pawn.Dead || !Active)
            {
                DispersalRegistry.Drop(this);
                return;
            }

            DispersalRegistry.Report(this);

            // The gene granted Murder and Carrion itself until 2026-09-27; now Itachi's Echo does,
            // while he is manifested. A save from before that still holds them on the pawn, so
            // once after loading they are taken back unless something else grants them.
            if (!staleAbilitiesChecked && pawn.abilities != null)
            {
                staleAbilitiesChecked = true;
                if (!EchoUtility.GeneActive(pawn, def))
                {
                    foreach (AbilityDef ability in new[] { ItachiDefOf.AG_DispersalMurder, ItachiDefOf.AG_DispersalCarrion })
                    {
                        if (pawn.abilities.GetAbility(ability) == null) continue;
                        if (TraitAbilityUtility.GrantedByOtherSource(pawn, ability, null, null)) continue;
                        pawn.abilities.RemoveAbility(ability);
                    }
                }
            }

            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            if (Charges >= MaxCharges)
            {
                rechargeProgress = 0;
                return;
            }

            rechargeProgress += Mathf.Max(1, delta);
            if (rechargeProgress < RechargeTicks) return;

            rechargeProgress = 0;
            charges = Charges + 1;

            if (pawn.IsColonistPlayerControlled)
            {
                Messages.Message("AG_DispersalRecharged".Translate(pawn.LabelShort, charges.ToString()),
                    pawn, MessageTypeDefOf.SilentInput, false);
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            DispersalRegistry.Drop(this);
        }

        /// <summary>
        /// Toggle automatic scatter while showing the shared charge pool. Disabling it reserves
        /// charges for Murder without stopping recharge. Existing saves default to enabled.
        /// </summary>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            // Gene.GetGizmos returns null, not an empty sequence - Pawn_GeneTracker null-checks
            // the result rather than iterating it blind, and every vanilla override replaces it
            // instead of chaining to it. Calling base here throws once a frame, and because
            // gene gizmos and ability gizmos come out of the same enumerator on Pawn.GetGizmos,
            // that took every other command on the pawn down with it.
            if (pawn == null || !pawn.IsColonistPlayerControlled) yield break;
            // Echo-only: the toggle, like Scatter itself, exists only in hero form.
            if (!EchoUtility.GeneActive(pawn, def)) yield break;

            if (icon == null)
            {
                // Resolved from the draw path, which is always the main thread.
                icon = ContentFinder<Texture2D>.Get("RimArt/Dispersal/IconGene", false);
            }

            string desc = Charges >= MaxCharges
                ? "AG_DispersalFullDesc".Translate(Charges.ToString())
                : "AG_DispersalRechargingDesc".Translate(
                    Charges.ToString(), (RechargeFraction * 100f).ToString("F0"));

            yield return new Command_Toggle
            {
                defaultLabel = "AG_DispersalLabel".Translate(Charges.ToString(), MaxCharges.ToString()),
                defaultDesc = desc + "\n\n" + "AG_DispersalToggleDesc".Translate(),
                icon = icon,
                isActive = () => autoScatter,
                toggleAction = () => autoScatter = !autoScatter
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref charges, "charges", -1);
            Scribe_Values.Look(ref rechargeProgress, "rechargeProgress", 0);
            Scribe_Values.Look(ref autoScatter, "autoScatter", true);
        }
    }
}
