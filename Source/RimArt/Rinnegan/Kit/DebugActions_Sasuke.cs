using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Debug window kit "Sasuke": make a Host ready to play (manifested, full pool, kunai belt, Fūma Shuriken), refill
    /// his belt, and put three kunai in the air round the mouse for trying Raikō Kusari, Amaterasu and Amenotejikara
    /// without throwing by hand. The picture previews are in Source/RimArt/Rinnegan/DebugActions_*Preview.cs.
    /// </summary>
    public static class DebugActions_Sasuke
    {
        [RimArtDebug("Sasuke", "make Host (manifested, belt, Fūma)", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn)
        {
            if (pawn?.RaceProps?.Humanlike != true) return;
            EchoRecord record = EchoUtility.ForceHost(SasukeDefOf.AG_Echo_Sasuke, pawn);
            if (record == null) return;
            GameComponent_Echoes.Get.charge = GameComponent_Echoes.Get.MaxCharge;
            if (!record.manifested) EchoUtility.Manifest(record);
            if (KunaiBelt.WornBy(pawn) == null) pawn.apparel?.Wear((Apparel)ThingMaker.MakeThing(KunaiDefOf.AG_KunaiBelt));
            if (pawn.equipment != null && pawn.equipment.Primary?.def != FumaDefOf.AG_FumaShuriken)
            {
                if (pawn.equipment.Primary != null) pawn.equipment.TryTransferEquipmentToContainer(pawn.equipment.Primary, pawn.inventory.innerContainer);
                pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(FumaDefOf.AG_FumaShuriken,
                    GenStuff.DefaultStuffFor(FumaDefOf.AG_FumaShuriken)));
            }
        }

        [RimArtDebug("Sasuke", "refill kunai belt (real kunai)", RimArtDebugKind.Pawn)]
        private static void Refill(Pawn pawn)
        {
            CompApparelReloadable belt = KunaiBelt.WornBy(pawn);
            if (belt == null) return;
            Thing kunai = ThingMaker.MakeThing(KunaiDefOf.AG_Kunai);
            kunai.stackCount = belt.MaxCharges;
            belt.ReloadFrom(kunai);
        }

        /// <summary>
        /// The manifested Sasuke on this map throws three kunai at cells round the mouse cell (north, middle, south,
        /// 2 cells apart) with Amenoyodomi turned to hang: the throws go through the belt's ability, so they fly and
        /// are caught as in play.
        /// </summary>
        [RimArtDebug("Sasuke", "hold 3 kunai round here")]
        private static void HoldThree()
        {
            Map map = Find.CurrentMap;
            Pawn sasuke = map?.mapPawns.AllPawnsSpawned.FirstOrDefault(p => SasukeKit.Amenoyodomi(p) != null);
            if (sasuke == null)
            {
                Messages.Message("No manifested Sasuke on this map.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            SasukeKit.Amenoyodomi(sasuke).SetMode(HoldMode.Hang);
            Ability throwKunai = sasuke.abilities.GetAbility(KunaiDefOf.AG_ThrowKunai);
            if (throwKunai == null) return;
            IntVec3 mouse = UI.MouseCell();
            foreach (int dz in new[] { 2, 0, -2 })
            {
                IntVec3 cell = mouse + new IntVec3(0, 0, dz);
                if (!cell.InBounds(map)) continue;
                var shot = (Projectile)GenSpawn.Spawn(ThingMaker.MakeThing(KunaiDefOf.AG_KunaiProjectile), sasuke.Position, map);
                shot.Launch(sasuke, sasuke.DrawPos, cell, cell, ProjectileHitFlags.NonTargetWorld);
            }
        }
    }
}
