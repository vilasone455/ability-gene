using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What a manifested Host holds (docs/hero-echo.md, Manifest weapon). An Echo can force its own
    /// weapon or empty hands. The weapon the Host held waits in their inventory and is equipped again
    /// on revert. The hero weapon is not loot: whenever it leaves the hand (dropped, downed, disarmed,
    /// sucked away) it vanishes, and it comes back after the Echo's weaponReturnTicks while the Host is
    /// still manifested and not downed.
    /// </summary>
    public static class EchoWeapon
    {
        /// <summary>The manifested record whose Echo decides what this pawn holds, or null.</summary>
        public static EchoRecord Forcing(Pawn pawn)
        {
            EchoRecord record = GameComponent_Echoes.Get?.HostRecord(pawn);
            return record != null && record.manifested && record.def.ForcesHands ? record : null;
        }

        /// <summary>The record whose hero weapon this is, or null.</summary>
        public static EchoRecord OwnerOf(Thing thing)
        {
            if (thing == null) return null;
            return GameComponent_Echoes.Get?.Manifested.FirstOrDefault(r => r.heroWeapon == thing);
        }

        public static void Manifest(EchoRecord record, Pawn pawn)
        {
            if (!record.def.ForcesHands || pawn.equipment == null) return;
            Stow(record, pawn);
            record.weaponGone = false;
            Give(record, pawn);
        }

        public static void Revert(EchoRecord record, Pawn pawn)
        {
            record.weaponGone = false;
            ThingWithComps hero = record.heroWeapon;
            record.heroWeapon = null;
            if (hero != null && !hero.Destroyed)
            {
                if (pawn.equipment != null && pawn.equipment.Contains(hero)) pawn.equipment.DestroyEquipment(hero);
                else hero.Destroy();
            }

            ThingWithComps stored = record.storedWeapon;
            record.storedWeapon = null;
            // A downed Host drops its whole inventory (vanilla), so the stored weapon may be on the ground.
            if (stored == null || stored.Destroyed || pawn.Dead || pawn.Downed) return;
            if (pawn.equipment == null || pawn.equipment.Primary != null) return;
            if (pawn.inventory == null || !pawn.inventory.innerContainer.Contains(stored)) return;
            pawn.inventory.innerContainer.Remove(stored);
            pawn.equipment.AddEquipment(stored);
        }

        /// <summary>
        /// Runs every pool interval for a manifested Host whose Echo forces the hands: catches a hero
        /// weapon that left the hand by a path the drop patch does not see, stows any other weapon that
        /// reached the hand, and brings the hero weapon back when its time is up.
        /// </summary>
        public static void Tick(EchoRecord record)
        {
            Pawn pawn = record.host;
            if (pawn == null || pawn.Dead || pawn.equipment == null) return;
            ThingWithComps hero = record.heroWeapon;
            if (hero != null && (hero.Destroyed || !pawn.equipment.Contains(hero))) Vanish(record, hero);
            if (pawn.equipment.Primary != null && pawn.equipment.Primary != record.heroWeapon) Stow(record, pawn);
            if (record.def.manifestWeapon == null || record.heroWeapon != null || pawn.Downed) return;
            if (record.weaponGone && Find.TickManager.TicksGame < record.weaponBackTick) return;
            record.weaponGone = false;
            Give(record, pawn);
        }

        /// <summary>The hero weapon left the hand: it is destroyed wherever it went and comes back later.</summary>
        public static void Vanish(EchoRecord record, ThingWithComps hero)
        {
            if (record.heroWeapon == hero) record.heroWeapon = null;
            if (!hero.Destroyed)
            {
                Pawn pawn = record.host;
                if (pawn?.equipment != null && pawn.equipment.Contains(hero)) pawn.equipment.DestroyEquipment(hero);
                else hero.Destroy();
            }
            if (!record.manifested) return;
            record.weaponGone = true;
            record.weaponBackTick = Find.TickManager.TicksGame + record.def.weaponReturnTicks;
        }

        /// <summary>
        /// Moves the weapon in hand to the inventory. Only a pawn without an inventory drops it at its
        /// feet: the weapon's mass already counts toward what the Host carries.
        /// </summary>
        private static void Stow(EchoRecord record, Pawn pawn)
        {
            ThingWithComps held = pawn.equipment.Primary;
            if (held == null || held == record.heroWeapon) return;
            if (pawn.inventory != null && pawn.equipment.TryTransferEquipmentToContainer(held, pawn.inventory.innerContainer))
            {
                if (record.storedWeapon == null) record.storedWeapon = held;
                return;
            }
            if (pawn.SpawnedOrAnyParentSpawned) pawn.equipment.TryDropEquipment(held, out _, pawn.PositionHeld, forbid: false);
        }

        /// <summary>A fresh hero weapon in hand: default stuff and normal quality, never a roll.</summary>
        private static void Give(EchoRecord record, Pawn pawn)
        {
            ThingDef def = record.def.manifestWeapon;
            if (def == null || pawn.equipment.Primary != null) return;
            var weapon = (ThingWithComps)ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, null);
            pawn.equipment.AddEquipment(weapon);
            record.heroWeapon = weapon;
        }

        /// <summary>Why a manifested Host cannot equip or drop: "Goku fights with empty hands".</summary>
        public static string Reason(EchoRecord record) =>
            record.def.emptyHands
                ? "AG_EchoHandsEmpty".Translate(record.def.LabelCap)
                : "AG_EchoHandsWeapon".Translate(record.def.LabelCap, record.def.manifestWeapon.label);

        /// <summary>Clears references that would not load: a hero weapon already destroyed, a stored weapon no longer carried.</summary>
        public static void CleanForSave(EchoRecord record)
        {
            if (record.heroWeapon != null && record.heroWeapon.Destroyed) record.heroWeapon = null;
            ThingWithComps stored = record.storedWeapon;
            if (stored != null && (stored.Destroyed || record.host?.inventory == null || !record.host.inventory.innerContainer.Contains(stored)))
                record.storedWeapon = null;
        }
    }

    /// <summary>A manifested Host cannot equip a weapon other than the one its Echo forces.</summary>
    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip),
        new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    static class Patch_CanEquip_EchoWeapon
    {
        static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (!__result || thing?.def.equipmentType != EquipmentType.Primary) return;
            EchoRecord record = EchoWeapon.Forcing(pawn);
            if (record == null || thing == record.heroWeapon) return;
            __result = false;
            cantReason = EchoWeapon.Reason(record);
        }
    }

    /// <summary>
    /// Every vanilla drop (the drop order, downing, death, Disarm, Chain Sickle's Stake, Inumaki's
    /// "drop") comes here: the hero weapon vanishes instead of landing.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
    static class Patch_TryDropEquipment_EchoWeapon
    {
        static bool Prefix(ThingWithComps eq, out ThingWithComps resultingEq, ref bool __result)
        {
            resultingEq = null;
            EchoRecord record = EchoWeapon.OwnerOf(eq);
            if (record == null) return true;
            EchoWeapon.Vanish(record, eq);
            __result = true;
            return false;
        }
    }

    /// <summary>The gear tab's drop button refuses the hero weapon.</summary>
    [HarmonyPatch(typeof(ITab_Pawn_Gear), "InterfaceDrop")]
    static class Patch_GearTabDrop_EchoWeapon
    {
        static bool Prefix(Thing t)
        {
            EchoRecord record = EchoWeapon.OwnerOf(t);
            if (record == null) return true;
            Messages.Message(EchoWeapon.Reason(record), record.host, MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }

    /// <summary>The right-click "Drop" option is shown greyed out for the hero weapon.</summary>
    [HarmonyPatch(typeof(FloatMenuOptionProvider_DropEquipment), "GetSingleOptionFor", typeof(Pawn), typeof(FloatMenuContext))]
    static class Patch_DropMenu_EchoWeapon
    {
        static void Postfix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (__result == null) return;
            ThingWithComps primary = clickedPawn?.equipment?.Primary;
            EchoRecord record = EchoWeapon.OwnerOf(primary);
            if (record == null) return;
            __result = new FloatMenuOption("CannotDrop".Translate(primary.Label, primary) + ": " + EchoWeapon.Reason(record), null);
        }
    }
}
