using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>The charge and its rules, as XML fields on the weapon. The beam's numbers are on AG_LastPrism_Fire (<see cref="CompProperties_LastPrismFire"/>).</summary>
    public class CompProperties_LastPrism : CompProperties
    {
        public List<AbilityDef> abilities;

        /// <summary>Seconds of beam the prism holds. A new prism comes full.</summary>
        public float store = 12f;
        /// <summary>Seconds of full sun (sky glow 1) that add one second of beam; at sky glow 0.5 it takes twice as long.</summary>
        public float sunSecondsPerBeamSecond = 20f;

        public CompProperties_LastPrism()
        {
            compClass = typeof(CompLastPrism);
        }
    }

    /// <summary>
    /// The Last Prism: grants Fire to whoever holds it and holds the charge Fire spends, in seconds of beam.
    /// <see cref="GameComponent_LastPrism"/> fills it every 250 ticks (<see cref="Charge"/>) while it is held or lies on
    /// a map, and a firing cast drains it 1/60 s a tick.
    ///
    /// It is the weapon's CompEquippable (the def lists its comps with Inherit="False"), as CompFlameGauntlet is,
    /// because Core asks only the CompEquippable for a held weapon's extra buttons: the meter, and Retarget and Stop
    /// while the beam fires.
    /// </summary>
    public class CompLastPrism : CompEquippable
    {
        private float level;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();
        /// <summary>The tick the last beam from this prism stopped, for <see cref="CompProperties_LastPrismFire.rejoinSeconds"/>.</summary>
        public int lastReleaseTick = -999999;
        /// <summary>The last <see cref="Charge"/> added to it: the picture's flash as each second fills. Not saved.</summary>
        public bool chargingNow;

        public CompProperties_LastPrism Props => (CompProperties_LastPrism)props;

        public static CompLastPrism HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompLastPrism>();

        /// <summary>Seconds of beam in the prism.</summary>
        public float Level
        {
            get => level;
            set => level = Mathf.Clamp(value, 0f, Props.store);
        }

        public bool Full => level >= Props.store - 0.0001f;

        /// <summary>
        /// <paramref name="seconds"/> of game time at <paramref name="cell"/> of <paramref name="map"/>: adds
        /// seconds x sky glow / sunSecondsPerBeamSecond. Nothing under a roof or on a pocket map. The sky glow is the
        /// solar generator's (SkyManager.CurSkyGlow: 0 at night and in an eclipse, lower in some weather); sun lamps
        /// light the glow grid, not the sky, so they never count. Returns what it added.
        /// </summary>
        public float Charge(Map map, IntVec3 cell, float seconds)
        {
            float light = SunOn(map, cell);
            float before = level;
            Level = level + seconds * light / Mathf.Max(0.01f, Props.sunSecondsPerBeamSecond);
            chargingNow = level > before;
            return level - before;
        }

        /// <summary>The sky's light on <paramref name="cell"/> as the prism takes it: the sky glow, or 0 under a roof or on a pocket map.</summary>
        public static float SunOn(Map map, IntVec3 cell)
        {
            if (map == null || map.IsPocketMap || !cell.InBounds(map) || cell.Roofed(map)) return 0f;
            return Mathf.Clamp01(map.skyManager.CurSkyGlow);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            level = Props.store;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            grant.Give(pawn, Props.abilities);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            GameComponent_LastPrism.Instance?.Stop(pawn);
            grant.Take(pawn, Props.abilities, this);
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn holder = Holder;
            if (holder == null || !holder.IsColonistPlayerControlled) yield break;
            yield return LastPrismCommands.Meter(this);
            LastPrismCast cast = GameComponent_LastPrism.Instance?.FiringBy(holder);
            if (cast == null) yield break;
            yield return LastPrismCommands.Retarget(cast);
            yield return LastPrismCommands.Stop(cast);
        }

        public override string CompInspectStringExtra() => "Sunlight: " + level.ToString("0.0") + " / " + Props.store.ToString("0") + " s of beam";

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref level, "level");
            Scribe_Values.Look(ref lastReleaseTick, "lastReleaseTick", -999999);
            grant.ExposeData();
        }
    }

    /// <summary>The buttons the prism adds: the sunlight meter always, and Retarget and Stop while it fires.</summary>
    [StaticConstructorOnStartup]
    public static class LastPrismCommands
    {
        private static readonly Texture2D Fill = SolidColorMaterials.NewSolidColorTexture(LastPrismGraphics.Sunlight);
        private static readonly Texture2D RetargetIcon = ContentFinder<Texture2D>.Get("RimArt/LastPrism/IconRetarget");

        /// <summary>One segment per second of beam, as the sketch's meter over the wielder's head.</summary>
        public static Gizmo_Meter Meter(CompLastPrism prism)
        {
            CompProperties_LastPrism props = prism.Props;
            return new Gizmo_Meter("Sunlight", () => prism.Level, props.store, Fill)
            {
                unitTicks = true,
                valueLabel = () => prism.Level.ToString("0.0") + " / " + props.store.ToString("0") + " s",
                tip = () => "Seconds of beam stored: " + prism.Level.ToString("0.0") + " of " + props.store.ToString("0") + ". Firing spends 1 a second. It fills 1 per "
                    + props.sunSecondsPerBeamSecond.ToString("0") + " s of full sun while held or lying on open ground, less when the sky is dimmer; not at night, under a roof, in a pocket dimension, in a bag or while firing."
                    + (prism.chargingNow ? "\n\nFilling now." : ""),
            };
        }

        public static Command_Target Retarget(LastPrismCast cast)
        {
            float range = cast.Props.Range;
            return new Command_Target
            {
                defaultLabel = "Retarget",
                defaultDesc = "Turn the beam to another person or a spot within " + range.ToString("0") + " tiles, at " + cast.Props.turnDegreesPerSecond.ToString("0")
                              + " degrees a second. The joined beam stays joined. On a spot it holds there and keeps firing until stopped or out of charge.",
                icon = RetargetIcon,
                groupable = false,
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true, canTargetPawns = true, canTargetAnimals = true, canTargetMechs = true, canTargetBuildings = false,
                    canTargetItems = false, mapObjectTargetsMustBeAutoAttackable = false,
                    validator = t => t.IsValid && cast.caster != null && t.Cell.InBounds(cast.caster.Map) && t.Cell.DistanceTo(cast.caster.Position) <= range && t.Thing != cast.caster,
                },
                action = t => cast.Retarget(t),
            };
        }

        public static Command_Action Stop(LastPrismCast cast) => new Command_Action
        {
            defaultLabel = "Stop",
            defaultDesc = "Stop the beam. The charge left stays in the prism.",
            icon = TexCommand.ClearPrioritizedWork,
            groupable = false,
            action = () => GameComponent_LastPrism.Instance?.Stop(cast.caster),
        };
    }
}
