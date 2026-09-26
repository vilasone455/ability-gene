using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    // Feed: every melee hit made with Samehada that lands on a pawn. Verb_MeleeAttack.TryCastShot calls
    // ApplyMeleeDamageToTarget only after the hit and dodge rolls pass, so a postfix on it sees landed hits
    // and nothing else. Weapon tools use Verb_MeleeAttackDamage; it returns at the first check for every
    // other weapon. Shark Skin's sweep goes through the same method, so its extra hits feed too.
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class Patch_Samehada_Feed
    {
        /// <summary>Landed hits with the blade on any pawn since the game started, fed or not; the tests read it.</summary>
        public static int LandedHits;

        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            ThingWithComps weapon = __instance.EquipmentSource;
            if (weapon == null || weapon.def != SamehadaDefOf.AG_Samehada || !(target.Thing is Pawn victim)) return;
            LandedHits++;
            Pawn holder = __instance.CasterPawn;
            CompSamehada blade = weapon.GetComp<CompSamehada>();
            if (holder == null || blade == null || victim == holder) return;
            SamehadaFeeding.Feed(holder, blade, victim);
        }
    }

    // Shark Skin: every melee attack with the blade while it lasts also hits the hostile pawns in the cells
    // in front (SamehadaFeeding.Sweep), whether the attack's own roll hit or missed. The prefix notes where
    // the attack was aimed before the hit can kill or move the target.
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_Samehada_Sweep
    {
        private sealed class Aim
        {
            public Verb_MeleeAttack verb;
            public IntVec3 from, cell;
            public Thing target;
        }

        /// <summary>The attack the prefix saw, for its postfix. Melee attacks run on the main thread, one at a time.</summary>
        private static Aim pending;

        public static void Prefix(Verb_MeleeAttack __instance)
        {
            pending = null;
            ThingWithComps weapon = __instance.EquipmentSource;
            if (weapon == null || weapon.def != SamehadaDefOf.AG_Samehada) return;
            Pawn holder = __instance.CasterPawn;
            CompSamehada blade = weapon.GetComp<CompSamehada>();
            if (holder == null || !holder.Spawned || blade == null || !blade.SharkSkinActive) return;
            LocalTargetInfo target = __instance.CurrentTarget;
            if (!target.IsValid) return;
            pending = new Aim { verb = __instance, from = holder.Position, cell = target.Cell, target = target.Thing };
        }

        public static void Postfix(Verb_MeleeAttack __instance)
        {
            Aim aim = pending;
            pending = null;
            if (aim == null || aim.verb != __instance) return;
            Pawn holder = __instance.CasterPawn;
            if (holder == null || !holder.Spawned) return;
            SamehadaFeeding.Sweep(holder, __instance, aim.from, aim.cell, aim.target);
        }
    }

    // Each charge adds damagePerCharge to the blade's melee hits (the sweep's included). A prefix on
    // Pawn.PreApplyDamage, as Patch_ChainSickle_StakedCut is; a melee hit carries its Tool, and it returns
    // at the first compare for every hit that is not from this weapon.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_Samehada_ChargeDamage
    {
        public static void Prefix(ref DamageInfo dinfo)
        {
            if (dinfo.Weapon != SamehadaDefOf.AG_Samehada || dinfo.Tool == null || !(dinfo.Instigator is Pawn holder)) return;
            CompSamehada blade = CompSamehada.HeldBy(holder);
            if (blade == null) return;
            int charges = blade.Charges;
            if (charges > 0) dinfo.SetAmount(dinfo.Amount + blade.Props.damagePerCharge * charges);
        }
    }

    // The held blade is drawn from code, not from the item texture: its length and its bandage follow the
    // charges, and Shark Skin spreads it (SamehadaHeld). Core calls DrawEquipmentAiming both for a carried
    // weapon and for one aimed at a melee target; this replaces the texture in both. Melee Animation draws
    // melee weapons itself only when it has tweak data for them, which it has none of for this one, so it
    // leaves it to this method (Patch_PawnRenderer_DrawEquipment in its assembly).
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_Samehada_HeldDrawing
    {
        public static bool Prefix(Thing eq)
        {
            if (eq.def != SamehadaDefOf.AG_Samehada) return true;
            Pawn pawn = (eq.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            CompSamehada blade = eq.TryGetComp<CompSamehada>();
            if (pawn == null || blade == null || !pawn.Spawned) return true;
            SamehadaHeld.Draw(pawn, blade);
            return false;
        }
    }

    /// <summary>
    /// The held blade: where it is and how it is drawn when no cast picture is drawing it, and the pose
    /// the in-game cast pictures draw it in. The grip sits where Core puts a held melee weapon
    /// (PawnRenderUtility.DrawEquipmentAndApparelExtras, DrawCarriedWeapon): carried, at the pawn's draw
    /// position plus (0, -0.11) facing north, (0.22, -0.22) east, (0, -0.22) south, (-0.22, -0.22) west,
    /// pointing at 143 degrees (217 facing west) on Core's clock (0 north, clockwise), which is -53
    /// (-127) here; aimed at a melee target, 0.4 + equippedDistanceOffset cells toward it, pointing at it.
    /// Both are scaled by the life stage's equipmentDrawDistanceFactor. The blade goes behind the pawn
    /// when it faces north, as Core draws equipment. The previews keep the sketches' hand.
    /// </summary>
    public static class SamehadaHeld
    {
        /// <summary>Altitude of the held blade from the pawn's: behind the pawn facing north, over its headgear and the shark form otherwise.</summary>
        public const float Behind = -0.016f, InFront = 0.040f;
        /// <summary>The held blade's parts are this share of the pictures' altitude steps apart, so all of it stays inside the pawn's layers.</summary>
        public const float Step = 0.2f;
        /// <summary>Core's carried-weapon offsets by facing (north, east, south, west) and angles, and the aimed distance.</summary>
        private static readonly Vector3[] EqLoc = { new Vector3(0f, 0f, -0.11f), new Vector3(0.22f, 0f, -0.22f), new Vector3(0f, 0f, -0.22f), new Vector3(-0.22f, 0f, -0.22f) };
        public const float CarryAngle = 143f, CarryAngleWest = 217f, AimDistance = 0.4f;

        /// <summary>A pawn's facing as the pictures' degrees: 0 east, 90 north.</summary>
        public static float Facing(Pawn pawn) => 90f - pawn.Rotation.AsAngle;

        /// <summary>
        /// Where the grip is drawn and the blade's direction (0 east, 90 north). <paramref name="aim"/> false
        /// gives the carried pose whatever the pawn is doing (the cast pictures). The returned hand is the
        /// ground point under the grip: the pictures raise it by HandH, so the drawn grip lands on Core's point.
        /// </summary>
        public static void Pose(Pawn pawn, bool aim, out Vector2 hand, out float deg, out bool behind)
        {
            Vector3 at = pawn.DrawPos, anchor;
            float factor = pawn.ageTracker?.CurLifeStage?.equipmentDrawDistanceFactor ?? 1f, compass;
            behind = pawn.Rotation == Rot4.North;
            Vector3 run = Vector3.zero;
            if (aim && pawn.stances?.curStance is Stance_Busy busy && !busy.neverAimWeapon && busy.focusTarg.IsValid)
                run = (busy.focusTarg.HasThing ? busy.focusTarg.Thing.DrawPos : busy.focusTarg.Cell.ToVector3Shifted()) - at;
            if (run.MagnitudeHorizontalSquared() > 0.001f)
            {
                compass = run.AngleFlat();
                float distance = AimDistance + (pawn.equipment?.Primary?.def.equippedDistanceOffset ?? 0f);
                anchor = at + new Vector3(0f, 0f, distance).RotatedBy(compass) * factor;
            }
            else
            {
                compass = pawn.Rotation == Rot4.West ? CarryAngleWest : CarryAngle;
                anchor = at + EqLoc[pawn.Rotation.AsInt] * factor;
            }
            deg = 90f - compass;
            hand = new Vector2(anchor.x, anchor.z - SamehadaGraphics.HandH * SamehadaGraphics.Lift);
        }

        /// <summary>The held blade's tip as drawn, where a drain ends.</summary>
        public static Vector2 Tip(Pawn pawn, CompSamehada blade)
        {
            Pose(pawn, true, out Vector2 hand, out float deg, out _);
            var grip = new Vector2(hand.x, hand.y + SamehadaGraphics.HandH * SamehadaGraphics.Lift);
            return grip + VfxDraw.Turn(deg) * (SamehadaGraphics.GripLength + SamehadaGraphics.BladeLength(blade.ShownCharges));
        }

        /// <summary>The altitude the blade is drawn at for this pawn.</summary>
        public static float Layer(Pawn pawn, bool behind) => pawn.DrawPos.y + (behind ? Behind : InFront);

        public static void Draw(Pawn pawn, CompSamehada blade)
        {
            Map map = pawn.Map;
            if (map == null) return;
            Pose(pawn, true, out Vector2 hand, out float deg, out bool behind);
            VfxDraw.Begin(hand);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            float charges = blade.ShownCharges;
            SamehadaGraphics.Blade(hand, deg, charges, sun, strength, blade.Flare, blade.TearAt(charges), blade.Hot,
                1f, Layer(pawn, behind), Step, blade.Props.maxCharges);
        }
    }
}
