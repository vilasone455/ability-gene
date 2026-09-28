using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_Amaterasu : CompProperties_AbilityEffect
    {
        /// <summary>How long the black flame burns on a pawn it is cast on or a lit weapon hits.</summary>
        public int durationTicks = 1200;
        /// <summary>How long a lit weapon that came down on the ground burns there.</summary>
        public int groundBurnTicks = 1200;
        /// <summary>The price: the casting eye bleeds; casting again while it does blinds.</summary>
        public HediffDef bleedingEye;
        public HediffDef blindness;

        public CompProperties_Amaterasu()
        {
            compClass = typeof(CompAbilityEffect_Amaterasu);
        }
    }

    /// <summary>
    /// Amaterasu (rinnegan-amaterasu.js header): black flames on one pawn, or on every weapon Amenoyodomi holds (click
    /// where one hangs). The flame on a pawn is the AG_AmaterasuFlame hediff. Lit weapons carry the fire when let go
    /// (<see cref="FlyingOn"/>): a kunai lights the pawn it hits or the cell it lands on, the Fūma every pawn it cuts
    /// and itself where it lies. Sasuke pays with a bleeding eye; a second cast while it bleeds blinds him.
    /// </summary>
    public class CompAbilityEffect_Amaterasu : CompAbilityEffect
    {
        public new CompProperties_Amaterasu Props => (CompProperties_Amaterasu)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (Target(target, out _, out _) == null)
            {
                if (throwMessages)
                    Messages.Message("Target a pawn, or a weapon Amenoyodomi holds.", parent.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        /// <summary>What the click means: a pawn (not Sasuke), or the caster's held weapons. Null if neither.</summary>
        private object Target(LocalTargetInfo target, out Pawn pawn, out bool heldWeapons)
        {
            Pawn caster = parent.pawn;
            pawn = null;
            heldWeapons = false;
            if (!target.IsValid || caster.Map == null) return null;
            if (target.Thing is Pawn clicked)
            {
                if (clicked == caster || SasukeKit.IsSasuke(clicked)) return null;
                pawn = clicked;
                return pawn;
            }
            if (target.HasThing || !target.Cell.InBounds(caster.Map)) return null;
            if (GameComponent_Rinnegan.Instance?.HeldNear(caster, target.Cell) != null)
            {
                heldWeapons = true;
                return caster;
            }
            if (target.Cell.GetFirstPawn(caster.Map) is Pawn standing && standing != caster && !SasukeKit.IsSasuke(standing))
            {
                pawn = standing;
                return pawn;
            }
            return null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (Target(target, out Pawn pawn, out bool heldWeapons) == null) return;
            PayPrice(caster);
            RinneganPictures.Ignition(caster, pawn);
            if (pawn != null)
            {
                Amaterasu.Ignite(pawn, caster, Props.durationTicks, AmaterasuCatch.Cast);
                return;
            }
            if (!heldWeapons || GameComponent_Rinnegan.Instance == null) return;
            int now = Find.TickManager.TicksGame;
            foreach (HeldWeapon w in GameComponent_Rinnegan.Instance.HeldBy(caster))
            {
                if (w.burning) continue;
                w.burning = true;
                w.litTick = now;
                RinneganPictures.WeaponLit(w);
            }
        }

        private void PayPrice(Pawn caster)
        {
            HediffSet hediffs = caster.health.hediffSet;
            if (Props.bleedingEye != null && hediffs.HasHediff(Props.bleedingEye))
            {
                if (Props.blindness != null) caster.health.AddHediff(Props.blindness);
                return;
            }
            if (Props.bleedingEye != null) caster.health.AddHediff(Props.bleedingEye);
        }
    }

    /// <summary>Amaterasu's second button: Release puts out every black flame this Sasuke lit, all at once.</summary>
    [StaticConstructorOnStartup]
    public class Ability_Amaterasu : Ability
    {
        private static readonly Texture2D ReleaseIcon = ContentFinder<Texture2D>.Get("RimArt/Rinnegan/IconRelease");

        public Ability_Amaterasu(Pawn pawn) : base(pawn) { }
        public Ability_Amaterasu(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override IEnumerable<Command> GetGizmos()
        {
            foreach (Command command in base.GetGizmos()) yield return command;
            if (GameComponent_Rinnegan.Instance?.AnyBurningBy(pawn) != true) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Release",
                defaultDesc = "Put out every black flame Sasuke has lit: on pawns, on the ground and on held weapons.",
                icon = ReleaseIcon,
                Order = def.uiOrder + 0.1f,
                action = () => GameComponent_Rinnegan.Instance?.Release(pawn)
            };
        }
    }

    /// <summary>Lighting pawns with the black flame.</summary>
    public static class Amaterasu
    {
        /// <summary>
        /// Sets <paramref name="pawn"/> alight for <paramref name="ticks"/>, or tops up a flame it already has. Never
        /// Sasuke. <paramref name="how"/> and <paramref name="from"/> (the burning pawn it caught from, for a spread) are
        /// for the picture: the first second of each looks different.
        /// </summary>
        public static bool Ignite(Pawn pawn, Pawn caster, int ticks, AmaterasuCatch how = AmaterasuCatch.Cast, Pawn from = null)
        {
            if (pawn == null || pawn.Dead || pawn == caster || SasukeKit.IsSasuke(pawn) || ticks <= 0) return false;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(SasukeDefOf.AG_AmaterasuFlame);
            HediffComp_Amaterasu flame = existing?.TryGetComp<HediffComp_Amaterasu>();
            if (flame != null)
            {
                flame.ticksLeft = Mathf.Max(flame.ticksLeft, ticks);
                return true;
            }
            Hediff hediff = HediffMaker.MakeHediff(SasukeDefOf.AG_AmaterasuFlame, pawn);
            flame = hediff.TryGetComp<HediffComp_Amaterasu>();
            if (flame == null) return false;
            flame.caster = caster;
            flame.ticksLeft = ticks;
            flame.lit = Find.TickManager.TicksGame;
            flame.how = how;
            flame.from = from != null ? from.DrawPos : pawn.DrawPos;
            pawn.health.AddHediff(hediff);
            return true;
        }

        public static bool Burning(Pawn pawn) => pawn?.health?.hediffSet?.HasHediff(SasukeDefOf.AG_AmaterasuFlame) == true;
    }

    public class HediffCompProperties_Amaterasu : HediffCompProperties
    {
        public float burnDamage = 4f;
        public int burnIntervalTicks = 60;
        public DamageDef damageDef;
        /// <summary>Chance per second that the flame jumps to each adjacent pawn.</summary>
        public float spreadChancePerSecond = 0.1f;

        public HediffCompProperties_Amaterasu()
        {
            compClass = typeof(HediffComp_Amaterasu);
        }
    }

    /// <summary>
    /// The black flame on one pawn: burn damage every interval while time is left, and each second a chance to jump to
    /// every adjacent pawn (not Sasuke), carrying the time left. It is not vanilla fire, so nothing puts it out but its
    /// time and Release.
    /// </summary>
    public class HediffComp_Amaterasu : HediffComp
    {
        public Pawn caster;
        public int ticksLeft;
        /// <summary>The tick it was lit, how it caught and where the pawn it caught from stood (DrawPos): for the picture.</summary>
        public int lit;
        public AmaterasuCatch how;
        public Vector3 from;
        private int age;

        public HediffCompProperties_Amaterasu Props => (HediffCompProperties_Amaterasu)props;

        public override bool CompShouldRemove => base.CompShouldRemove || ticksLeft <= 0;

        public override string CompLabelInBracketsExtra => (ticksLeft / 60f).ToString("0") + " s";

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            GameComponent_Rinnegan.Instance?.Register(this);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            GameComponent_Rinnegan.Instance?.Unregister(this);
            // Its time ran out or Release: the flames sink, the smoke rises and the stain stays a while.
            if (Pawn != null && Pawn.Spawned && !Pawn.Dead) AmaterasuPictures.FlameOut(this);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (ticksLeft <= 0) return;
            GameComponent_Rinnegan.Instance?.Register(this);
            int before = age;
            age += delta;
            ticksLeft -= delta;
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead) return;
            int interval = Mathf.Max(1, Props.burnIntervalTicks);
            for (int burns = age / interval - before / interval; burns > 0 && !pawn.Dead; burns--)
                pawn.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Burn, Props.burnDamage, 0f, -1f, caster));
            for (int seconds = age / 60 - before / 60; seconds > 0 && ticksLeft > 0; seconds--) Spread(pawn);
        }

        private void Spread(Pawn pawn)
        {
            if (!pawn.Spawned || pawn.Dead) return;
            Map map = pawn.Map;
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(pawn))
            {
                if (!cell.InBounds(map)) continue;
                List<Thing> things = cell.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    if (!(things[i] is Pawn other) || other.Dead || Amaterasu.Burning(other)) continue;
                    if (Rand.Chance(Props.spreadChancePerSecond)) Amaterasu.Ignite(other, caster, ticksLeft, AmaterasuCatch.Spread, pawn);
                }
            }
        }

        /// <summary>Release: the flame goes out now.</summary>
        public void PutOut()
        {
            ticksLeft = 0;
            Pawn pawn = Pawn;
            if (pawn?.health != null && pawn.health.hediffSet.hediffs.Contains(parent)) pawn.health.RemoveHediff(parent);
            GameComponent_Rinnegan.Instance?.Unregister(this);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft");
            Scribe_Values.Look(ref lit, "lit");
            Scribe_Values.Look(ref how, "how", AmaterasuCatch.Cast);
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref age, "age");
        }
    }
}
