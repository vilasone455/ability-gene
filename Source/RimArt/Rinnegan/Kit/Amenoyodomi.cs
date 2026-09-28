using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public enum HoldMode
    {
        Off,
        Hang,
        Drift
    }

    /// <summary>Amenoyodomi's numbers (rinnegan-amenoyodomi.js header). The ability never casts, so there is no effect.</summary>
    public class CompProperties_Amenoyodomi : CompProperties_AbilityEffect
    {
        /// <summary>Most weapons held at once; the next throw at a cell is a normal throw.</summary>
        public int maxHeld = 5;
        /// <summary>Share of its flight speed a held weapon moves on at: hang and drift.</summary>
        public float hangShare = 0.01f;
        public float driftShare = 0.1f;
        /// <summary>A held weapon drops after this long.</summary>
        public int holdTicks = 3600;
        /// <summary>How far a let-go weapon flies on from where it hung.</summary>
        public float kunaiRange = 14.9f;
        public float fumaRange = 12f;
        /// <summary>While manifested, the worn kunai belt regains one conjured kunai this often.</summary>
        public int kunaiRefillTicks = 300;

        public CompProperties_Amenoyodomi()
        {
            compClass = typeof(CompAbilityEffect_Amenoyodomi);
        }

        public float Share(HoldMode mode) => mode == HoldMode.Drift ? driftShare : hangShare;
    }

    /// <summary>Holds the numbers only: Amenoyodomi's button never casts (<see cref="Ability_Amenoyodomi"/>).</summary>
    public class CompAbilityEffect_Amenoyodomi : CompAbilityEffect
    {
    }

    /// <summary>
    /// Amenoyodomi as an ability: its button is a toggle, not a cast, so switching it never starts a job or stops
    /// Sasuke where he is. Off, hang and drift in turn; Let go shows while he holds anything. The state lives here,
    /// so a revert (which takes the ability away) turns it off, and <see cref="GameComponent_Rinnegan"/> drops what
    /// was held.
    ///
    /// The kunai supply ticks here too, because it runs exactly while Sasuke has this ability: his worn kunai belt
    /// regains one conjured kunai every <see cref="CompProperties_Amenoyodomi.kunaiRefillTicks"/> until full.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Ability_Amenoyodomi : Ability
    {
        private static readonly Texture2D LetGoIcon = ContentFinder<Texture2D>.Get("RimArt/Rinnegan/IconLetGo");

        public HoldMode mode;
        private int nextRefillTick = -1;

        public Ability_Amenoyodomi(Pawn pawn) : base(pawn) { }
        public Ability_Amenoyodomi(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public CompProperties_Amenoyodomi Props => VergilKit.Props<CompProperties_Amenoyodomi>(def);

        public void SetMode(HoldMode next)
        {
            if (next == mode) return;
            mode = next;
            GameComponent_Rinnegan rinnegan = GameComponent_Rinnegan.Instance;
            if (rinnegan == null) return;
            rinnegan.Notify_ModeChanged(this);
            if (pawn.Spawned) RinneganPictures.EyeStar(pawn);
        }

        public static HoldMode Next(HoldMode mode) => mode == HoldMode.Off ? HoldMode.Hang : mode == HoldMode.Hang ? HoldMode.Drift : HoldMode.Off;

        public override IEnumerable<Command> GetGizmos()
        {
            var toggle = new Command_Action
            {
                defaultLabel = def.LabelCap + ": " + ModeLabel(mode),
                defaultDesc = def.description,
                icon = def.uiIcon,
                Order = def.uiOrder,
                action = () => SetMode(Next(mode))
            };
            if (pawn.Downed || !pawn.Awake()) toggle.Disable(pawn.LabelShortCap + " must be awake and standing.");
            yield return toggle;

            int held = GameComponent_Rinnegan.Instance?.CountHeldBy(pawn) ?? 0;
            if (held > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Let go (" + held + ")",
                    defaultDesc = "Every weapon Amenoyodomi holds flies on at full speed along its heading, up to its range from where it hung "
                        + "(kunai " + Props.kunaiRange + " cells, Fūma " + Props.fumaRange + "). A kunai hits the first pawn on its line, allies included; "
                        + "the Fūma cuts every pawn on its line.",
                    icon = LetGoIcon,
                    Order = def.uiOrder + 0.1f,
                    action = () => GameComponent_Rinnegan.Instance?.LetGo(pawn)
                };
            }
        }

        /// <summary>What throw kunai's cursor says over a cell: whether Amenoyodomi will catch the kunai there.</summary>
        public static string CellLabel(Pawn caster)
        {
            Ability_Amenoyodomi ability = SasukeKit.Amenoyodomi(caster);
            if (ability == null || ability.mode == HoldMode.Off) return null;
            int held = GameComponent_Rinnegan.Instance?.CountHeldBy(caster) ?? 0;
            int most = ability.Props.maxHeld;
            return held < most ? "Amenoyodomi holds it here (" + held + " / " + most + ")" : "Lands here: Amenoyodomi holds " + most + " already";
        }

        public static string ModeLabel(HoldMode mode) => mode == HoldMode.Off ? "off" : mode == HoldMode.Hang ? "hang" : "drift";

        public override void AbilityTick()
        {
            base.AbilityTick();
            if (mode != HoldMode.Off) GameComponent_Rinnegan.Instance?.Notify_On(this);
            Refill();
        }

        private void Refill()
        {
            CompApparelReloadable belt = KunaiBelt.WornBy(pawn);
            if (belt == null || belt.RemainingCharges >= belt.MaxCharges)
            {
                nextRefillTick = -1;
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (nextRefillTick < 0)
            {
                nextRefillTick = now + Props.kunaiRefillTicks;
                return;
            }
            if (now < nextRefillTick) return;
            KunaiConjure.AddOne(belt);
            nextRefillTick = now + Props.kunaiRefillTicks;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mode, "amenoyodomiMode", HoldMode.Off);
            Scribe_Values.Look(ref nextRefillTick, "kunaiRefillTick", -1);
        }
    }

    /// <summary>
    /// One kunai or Fūma Shuriken Amenoyodomi holds in the air. The projectile stays spawned and off the engine's
    /// clock (Patches_Amenoyodomi): it is reported at <see cref="at"/> and drawn by <see cref="RinneganPictures"/>.
    /// <see cref="at"/> is its ground point; it is drawn Hold cells up.
    /// </summary>
    public class HeldWeapon : IExposable
    {
        public Projectile shot;
        public Pawn caster;
        public Vector3 at;
        /// <summary>Unit direction it was flying, on the ground plane. A let-go goes on along it.</summary>
        public Vector3 heading;
        /// <summary>Its full flight speed in cells per tick.</summary>
        public float speed;
        public int caughtTick;
        /// <summary>Throw order (the projectile's id, given at launch): Raikō Kusari links weapons in this order.</summary>
        public int order;
        /// <summary>Degrees the Fūma has turned while held (it turns at the hold share of its spin).</summary>
        public float turned;
        /// <summary>Cells it has moved on since it was caught (the picture turns the dots under it by this).</summary>
        public float crept;
        public bool burning;
        public int litTick = -1;

        public bool IsFuma => shot is Projectile_Fuma;
        public bool Conjured => shot?.def == SasukeDefOf.AG_KunaiProjectileConjured;

        public void ExposeData()
        {
            Scribe_References.Look(ref shot, "shot");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref at, "at");
            Scribe_Values.Look(ref heading, "heading");
            Scribe_Values.Look(ref speed, "speed");
            Scribe_Values.Look(ref caughtTick, "caughtTick");
            Scribe_Values.Look(ref order, "order");
            Scribe_Values.Look(ref turned, "turned");
            Scribe_Values.Look(ref crept, "crept");
            Scribe_Values.Look(ref burning, "burning");
            Scribe_Values.Look(ref litTick, "litTick", -1);
        }
    }

    /// <summary>
    /// A weapon let go (or dropped) that still carries something from the Rinnegan: Raikō Kusari's charge (stuns what
    /// it hits) or Amaterasu's fire (lights what it hits, and the cell it lands on). Read when it hits.
    /// </summary>
    public class FlyingOn : IExposable
    {
        public Projectile shot;
        public Pawn caster;
        public bool charged;
        public bool burning;
        /// <summary>For the picture: when it was lit and let go, and the point it was let go from (its drawn point).</summary>
        public int litTick = -1;
        public int letGoTick;
        public Vector3 from;

        public void ExposeData()
        {
            Scribe_References.Look(ref shot, "shot");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref charged, "charged");
            Scribe_Values.Look(ref burning, "burning");
            Scribe_Values.Look(ref litTick, "litTick", -1);
            Scribe_Values.Look(ref letGoTick, "letGoTick");
            Scribe_Values.Look(ref from, "from");
        }
    }
}
