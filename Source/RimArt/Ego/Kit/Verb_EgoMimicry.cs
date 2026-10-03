using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// Mimicry's melee verb (the AG_EgoMimicrySlash maneuver, so only the sword's AG_EgoMimicryCut tool gets it). Core's melee
    /// attack lands on the tick it fires; this one starts a swing instead (<see cref="GameComponent_EgoMimicry.BeginSwing"/>)
    /// and the hit comes at the swing's contact frame, 0.22 s later, or at the grown swing's slam, 0.7 s later. The grown
    /// swing is chosen when the attack starts, growChance per ordinary swing.
    ///
    /// At contact <see cref="Swing"/> runs Core's own melee attack (Verb_MeleeAttack.TryCastShot: the hit and dodge rolls,
    /// sounds, the battle log, the stagger) with the damage scaled by the arm. <see cref="Slam"/> strikes every pawn under
    /// the grown blade with no rolls. Corroded and Overclock swings are started by <see cref="EgoMimicryCorrosion"/> and land
    /// through the same two.
    /// </summary>
    public class Verb_EgoMimicry : Verb_MeleeAttackDamage
    {
        /// <summary>True while a swing lands: TryCastShot then runs Core's attack instead of starting a swing.</summary>
        private bool landing;
        /// <summary>The landing swing's damage multiple, and what its hit dealt.</summary>
        private float factor = 1f, dealt;

        private static readonly List<Pawn> struck = new List<Pawn>();

        /// <summary>The sword's verb on <paramref name="weapon"/>, or null.</summary>
        public static Verb_EgoMimicry Of(ThingWithComps weapon)
        {
            List<Verb> verbs = weapon?.GetComp<CompEquippable>()?.AllVerbs;
            if (verbs == null) return null;
            for (int i = 0; i < verbs.Count; i++)
                if (verbs[i] is Verb_EgoMimicry verb) return verb;
            return null;
        }

        /// <summary>
        /// The attack starts: a swing begins at the target (Core then puts the wielder in its melee cooldown, 1.2 s, which a
        /// grown swing stretches to its own length). Refused while an earlier swing of the wielder's has not finished, so
        /// swings never overlap.
        /// </summary>
        protected override bool TryCastShot()
        {
            if (landing) return base.TryCastShot();
            Pawn pawn = CasterPawn;
            CompEgoMimicry sword = EquipmentSource?.GetComp<CompEgoMimicry>();
            GameComponent_EgoMimicry game = GameComponent_EgoMimicry.Instance;
            if (sword == null || game == null) return base.TryCastShot();
            Thing target = currentTarget.Thing;
            if (pawn == null || !pawn.Spawned || target == null || pawn.stances.FullBodyBusy || game.Busy(pawn)) return false;
            bool grown = EgoMimicry.SourceOf(pawn, sword) == EgoMimicrySource.Ordinary && Rand.Chance(sword.Props.growChance);
            pawn.rotationTracker.Face(target.DrawPos);
            game.BeginSwing(pawn, sword, target, EgoMimicrySource.Ordinary, grown, surpriseAttack, IntVec3.Invalid);
            return true;
        }

        /// <summary>
        /// A swing reaches its contact frame: Core's melee attack at the target, if the wielder can still reach it; out of
        /// reach it misses and deals nothing. Core's attack refuses a pawn in a busy stance, and the swing's own melee
        /// cooldown is one, so the stance is set aside for the call and put back after. True when the hit landed (not
        /// missed or dodged); <paramref name="damage"/> is what it dealt, 0 when armour turned it.
        /// </summary>
        public bool Swing(EgoMimicryCast cast, CompEgoMimicry sword, out float damage)
        {
            damage = 0f;
            Pawn pawn = CasterPawn;
            if (!EgoMimicry.Reaches(pawn, cast.target)) return false;
            currentTarget = cast.target;
            surpriseAttack = cast.surprise;
            factor = sword.DamageFactor(sword.StageNow, grown: false);
            dealt = 0f;
            Pawn_StanceTracker stances = pawn.stances;
            Stance held = stances.curStance;
            Stance_Mobile free = null;
            if (held is Stance_Busy)
            {
                free = new Stance_Mobile { stanceTracker = stances };
                stances.curStance = free;
            }
            bool hit;
            landing = true;
            try
            {
                hit = TryCastShot();
            }
            finally
            {
                landing = false;
                factor = 1f;
                if (free != null && stances.curStance == free) stances.curStance = held;
            }
            damage = dealt;
            return hit;
        }

        /// <summary>
        /// The grown swing's slam: every pawn under the blade's strip (<see cref="EgoMimicry.Under"/>) takes growDamageFactor
        /// times the swing's damage, once, with no hit or dodge roll; armour still applies. A swing aimed at something that is
        /// not a pawn (a building, a turret) strikes that thing alone, with no lifesteal. Each pawn struck gets its own battle
        /// log line. Returns the pawns struck, with what each took, through <paramref name="each"/>.
        /// </summary>
        public void Slam(EgoMimicryCast cast, CompEgoMimicry sword, Vector2 from, Vector2 to, List<KeyValuePair<Thing, float>> each)
        {
            each.Clear();
            Pawn pawn = CasterPawn;
            Thing target = cast.target;
            float grownFactor = sword.DamageFactor(sword.StageNow, grown: true);
            if (target != null && !(target is Pawn))
            {
                if (EgoMimicry.Reaches(pawn, target))
                {
                    currentTarget = target;
                    DamageWorker.DamageResult result = target.TakeDamage(Damage(target, grownFactor));
                    each.Add(new KeyValuePair<Thing, float>(target, result.totalDamageDealt));
                }
            }
            else
            {
                foreach (Pawn victim in EgoMimicry.Under(pawn, target, from, to, sword.Props.growHalfWidth, struck))
                {
                    if (victim.Dead || !victim.Spawned) continue;
                    currentTarget = victim;
                    BattleLogEntry_MeleeCombat log = CreateCombatLog(m => m.combatLogRulesHit, alwaysShow: true);
                    DamageWorker.DamageResult result = victim.TakeDamage(Damage(victim, grownFactor));
                    result.AssociateWithLog(log);
                    each.Add(new KeyValuePair<Thing, float>(victim, result.totalDamageDealt));
                    if (!victim.Dead && victim.Spawned) victim.stances.stagger.StaggerFor(95);
                }
            }
            if (each.Count > 0) sword.Props.slamSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            currentTarget = target;
        }

        /// <summary>The landing swing's hit, with its damage scaled (Core's own builds the same hit unscaled).</summary>
        protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
        {
            DamageWorker.DamageResult result = target.Thing.TakeDamage(Damage(target.Thing, factor));
            dealt = result.totalDamageDealt;
            return result;
        }

        /// <summary>
        /// One hit of the sword's damage times <paramref name="times"/>, built as Core's Verb_MeleeAttackDamage builds it: the
        /// tool's power with the wielder's and the weapon's multipliers, 0.8 to 1.2 of it at random, the tool's armour
        /// penetration, from the attacker's side. The tool has no extra damages.
        /// </summary>
        public DamageInfo Damage(Thing target, float times)
        {
            Pawn pawn = CasterPawn;
            float amount = verbProps.AdjustedMeleeDamageAmount(this, pawn) * times;
            amount = Rand.Range(amount * 0.8f, amount * 1.2f);
            DamageDef def = verbProps.meleeDamageDef;
            if (amount < 1f)
            {
                amount = 1f;
                def = DamageDefOf.Blunt;
            }
            QualityCategory quality = QualityCategory.Normal;
            ThingDef source = pawn.def;
            if (EquipmentSource != null)
            {
                source = EquipmentSource.def;
                EquipmentSource.TryGetQuality(out quality);
            }
            bool guilty = !pawn.Drafted;
            var dinfo = new DamageInfo(def, amount, verbProps.AdjustedArmorPenetration(this, pawn), -1f, pawn, null, source,
                DamageInfo.SourceCategory.ThingOrUnknown, null, guilty);
            dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
            dinfo.SetWeaponBodyPartGroup(verbProps.AdjustedLinkedBodyPartsGroup(tool));
            dinfo.SetAngle((target.Position - pawn.Position).ToVector3());
            dinfo.SetTool(tool);
            dinfo.SetWeaponQuality(quality);
            return dinfo;
        }
    }
}
