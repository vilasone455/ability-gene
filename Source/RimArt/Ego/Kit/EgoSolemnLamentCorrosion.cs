using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Solemn Lament's corroded attack, the coffin (docs/ego-weapons.md, Weapon 2): it rises where the corrosion starts
    /// and stays there; the butterfly cloud circles the wielder, who walks to the nearest pawn of any faction (downed
    /// ones too) and stays next to it. Each firing (every corrodedInterval) puts cloudStacks on every other living pawn
    /// within coffinRadius of the wielder. Corroded, a pawn keeps taking stacks past the cap and dies at funeralStacks
    /// (the funeral). Overclock holds still, hits only hostiles that are standing, and never puts on more than the cap,
    /// so it downs only a pawn the guns already brought near it. The wielder takes none.
    /// </summary>
    public class EgoSolemnLamentCorrosion : EgoCorrosionAction
    {
        private static readonly List<Pawn> inCloud = new List<Pawn>();

        public override bool TakesTarget => false;

        public override bool WalksToNearest => true;

        public override void Begin(Pawn wielder, CompEgoWeapon weapon, bool overclock)
        {
            if (weapon is CompEgoSolemnLament gun && wielder.Spawned)
                GameComponent_EgoSolemnLament.Instance?.BeginCoffin(wielder, gun.Props.coffinRadius, overclock);
        }

        public override void End(Pawn wielder, CompEgoWeapon weapon, bool overclock) => GameComponent_EgoSolemnLament.Instance?.EndCoffin(wielder);

        public override void Fire(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly)
        {
            GameComponent_EgoSolemnLament game = GameComponent_EgoSolemnLament.Instance;
            if (!(weapon is CompEgoSolemnLament gun) || game == null || !wielder.Spawned) return;
            CompProperties_EgoSolemnLament p = gun.Props;
            EgoSolemnLamentCoffinCast coffin = game.CoffinOf(wielder) ?? game.BeginCoffin(wielder, p.coffinRadius, hostilesOnly);
            int now = Find.TickManager.TicksGame, cap = EgoButterflyExtension.Of.cap;
            int limit = hostilesOnly ? cap : p.funeralStacks, funeral = hostilesOnly ? 0 : p.funeralStacks;
            inCloud.Clear();
            IReadOnlyList<Pawn> pawns = wielder.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == wielder || pawn.Dead || !pawn.Position.InHorDistOf(wielder.Position, p.coffinRadius)) continue;
                if (hostilesOnly && (pawn.Downed || !pawn.HostileTo(wielder))) continue;
                inCloud.Add(pawn);
            }
            // Copied first: a funeral kills a pawn and takes it off the map's list.
            foreach (Pawn pawn in inCloud)
            {
                int had = EgoButterfly.Stacks(pawn), add = System.Math.Min(p.cloudStacks, limit - had);
                if (add <= 0) continue;
                EgoSolemnLamentMarked marked = game.Mark(pawn);
                for (int k = 0; k < add; k++)
                {
                    EgoSolemnLamentPoint from = coffin.Dive(now, out int slot);
                    marked.Dive(now, had + k, slot, from, funeral);
                }
                if (funeral > 0 && had + add >= funeral) marked.funeralBy = coffin;
                EgoButterfly.Add(pawn, add, limit, funeral);
            }
        }
    }
}
