using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Magic Bullet's numbers on top of the Corrosion ones (docs/ego-weapons.md, Weapon 1). The line's range is the verb's
    /// range and a shot's damage is the round's (AG_EgoMagicBullet_Round), both in the weapon's def, so the info card shows
    /// them; the seventh's damage and its cooldown are here.
    /// </summary>
    public class CompProperties_EgoMagicBullet : CompProperties_EgoWeapon
    {
        /// <summary>Damage of the seventh shot to everyone on its line, times the weapon's ranged damage multiplier.</summary>
        public float seventhDamage = 30f;
        /// <summary>Seconds after the seventh before the gun fires shot one: no shot, corroded firing or Overclock.</summary>
        public float seventhCooldown = 20f;
        /// <summary>Seconds a corroded or Overclock firing aims before the line goes off (a shot the player orders aims for the verb's warmup).</summary>
        public float actionAim = 0.45f;

        public CompProperties_EgoMagicBullet()
        {
            compClass = typeof(CompEgoMagicBullet);
        }
    }

    /// <summary>
    /// The count of Der Freischütz's bullets, kept on the gun so it survives a drop, a new wielder and a save: shots one to
    /// six go where they are aimed, the seventh goes to the pawn the shooter loves most (<see cref="EgoMagicBullet.Beloved"/>),
    /// then the count starts again after <see cref="CompProperties_EgoMagicBullet.seventhCooldown"/>. Player shots and
    /// corroded firings count; Overclock does not, so it never fires the seventh at an ally.
    /// </summary>
    public class CompEgoMagicBullet : CompEgoWeapon
    {
        /// <summary>Shots fired since the last seventh, 0 to 6.</summary>
        public int count;
        /// <summary>The tick the gun may fire again after a seventh.</summary>
        public int cooldownUntilTick = -1;

        public new CompProperties_EgoMagicBullet Props => (CompProperties_EgoMagicBullet)props;

        public new static CompEgoMagicBullet HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompEgoMagicBullet>();

        /// <summary>The number the next counted shot carries, 1 to 7.</summary>
        public int NextShot => count + 1;

        public bool NextIsSeventh => NextShot >= EgoMagicBulletTiming.Shots;

        public bool CoolingDown => Find.TickManager.TicksGame < cooldownUntilTick;

        public int CooldownTicksLeft => Mathf.Max(0, cooldownUntilTick - Find.TickManager.TicksGame);

        /// <summary>One counted shot went off: the seventh resets the count and starts the cooldown.</summary>
        public void Counted(bool seventh)
        {
            if (seventh)
            {
                count = 0;
                cooldownUntilTick = Find.TickManager.TicksGame + Props.seventhCooldown.SecondsToTicks();
            }
            else count = Mathf.Min(count + 1, EgoMagicBulletTiming.Shots - 1);
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            Pawn holder = Holder;
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra())
            {
                if (gizmo is Command_EgoOverclock overclock && CoolingDown)
                    overclock.Disable("The seventh was fired: " + CooldownTicksLeft.ToStringSecondsFromTicks() + " before the gun fires again.");
                yield return gizmo;
            }
            if (holder != null && holder.IsColonistPlayerControlled) yield return EgoMagicBulletCommands.Counter(this, holder);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref cooldownUntilTick, "cooldownUntilTick", -1);
        }
    }

    /// <summary>The count in the command bar: one segment per shot, and on six the name of the pawn the seventh will hit.</summary>
    [StaticConstructorOnStartup]
    public static class EgoMagicBulletCommands
    {
        private static readonly Texture2D Fill = SolidColorMaterials.NewSolidColorTexture(EgoMagicBulletGraphics.CircleBlue);
        private static readonly Texture2D SeventhFill = SolidColorMaterials.NewSolidColorTexture(new Color(0.85f, 0.18f, 0.20f));

        public static Gizmo_Meter Counter(CompEgoMagicBullet gun, Pawn holder)
        {
            int shots = EgoMagicBulletTiming.Shots;
            CompProperties_EgoMagicBullet p = gun.Props;
            return new Gizmo_Meter("Shots", () => gun.CoolingDown ? shots * gun.CooldownTicksLeft / (float)p.seventhCooldown.SecondsToTicks() : gun.count, shots, Fill)
            {
                unitTicks = true,
                hot = () => gun.NextIsSeventh || gun.CoolingDown,
                hotFill = SeventhFill,
                valueLabel = () => gun.CoolingDown ? gun.CooldownTicksLeft.ToStringSecondsFromTicks()
                    : gun.NextIsSeventh ? "7th: " + Name(EgoMagicBullet.Beloved(holder), holder)
                    : "next " + gun.NextShot + " / " + shots,
                tip = () => "Shots since the last seventh: " + gun.count + " of " + (shots - 1) + ". Shots one to six go where they are aimed. "
                    + "The seventh cannot be aimed: it goes to the pawn on the map " + holder.LabelShort + " has the highest opinion of "
                    + "(else a bonded animal, else " + holder.LabelShort + "), through everyone between, for " + p.seventhDamage.ToString("0")
                    + " damage. Then the gun does not fire for " + p.seventhCooldown.ToString("0") + " s. Corroded firings count; Overclock does not."
                    + (gun.NextIsSeventh ? "\n\nThe next shot is the seventh. It will hit " + Name(EgoMagicBullet.Beloved(holder), holder) + "." : "")
                    + (gun.CoolingDown ? "\n\nThe seventh was fired: " + gun.CooldownTicksLeft.ToStringSecondsFromTicks() + " left." : ""),
            };
        }

        private static string Name(Pawn beloved, Pawn holder) => beloved == null || beloved == holder ? holder.LabelShort + " (self)" : beloved.LabelShort;
    }
}
