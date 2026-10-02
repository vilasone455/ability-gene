using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Every Last Prism beam in the game (GameComponent_Vergil's shape) and the prisms' charging: ticks the beams on
    /// game time, fills every prism every <see cref="ChargeEvery"/> ticks, and once a frame draws the beams and the
    /// idle prism of each holder on the map on screen.
    ///
    /// Who holds a prism is kept in <see cref="holders"/> (MapComponent_Vacuum's shape), so a frame costs one pass over
    /// the holders, not over every pawn on the map: the comp registers on equip and unequip, and a rescan of every map
    /// once a second (<see cref="RescanEvery"/>) catches pawns that arrive already holding one and drops the dead.
    /// </summary>
    public sealed class GameComponent_LastPrism : GameComponent
    {
        /// <summary>Ticks between charge steps: Core's rare tick. A step adds ChargeEvery/60 s x sky glow / sunSecondsPerBeamSecond.</summary>
        public const int ChargeEvery = 250;
        /// <summary>Ticks between rescans of who holds a prism.</summary>
        public const int RescanEvery = 60;
        private List<LastPrismCast> casts = new List<LastPrismCast>();
        private readonly HashSet<Pawn> holders = new HashSet<Pawn>();
        private readonly List<Pawn> holderList = new List<Pawn>();
        private readonly HashSet<Pawn> drawn = new HashSet<Pawn>();

        public GameComponent_LastPrism(Game game) { }

        public static GameComponent_LastPrism Instance => Current.Game?.GetComponent<GameComponent_LastPrism>();

        public IReadOnlyList<LastPrismCast> Casts => casts;

        /// <summary>A cast job's warmup began: the cast starts, replacing one this caster never fired.</summary>
        public void Begin(LastPrismCast cast)
        {
            casts.RemoveAll(c => c.caster == cast.caster && !c.Fired);
            casts.Add(cast);
        }

        /// <summary>This caster's beam that is firing now, or null. The cast job holds the caster while there is one.</summary>
        public LastPrismCast FiringBy(Pawn caster)
        {
            if (caster == null) return null;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Firing) return casts[i];
            return null;
        }

        /// <summary>This caster's cast waiting for its fire tick, or null.</summary>
        public LastPrismCast Waiting(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster && !casts[i].Fired) return casts[i];
            return null;
        }

        /// <summary>The latest cast by this caster, fired or not.</summary>
        public LastPrismCast Latest(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster) return casts[i];
            return null;
        }

        /// <summary>A cast this caster began at or after <paramref name="sinceTick"/> has fired.</summary>
        public bool FiredSince(Pawn caster, int sinceTick)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Fired && casts[i].startTick >= sinceTick) return true;
            return false;
        }

        /// <summary>Stop, a move order, the job ending or the prism put away: the beam stops and fades; an unfired cast is dropped.</summary>
        public void Stop(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                LastPrismCast cast = casts[i];
                if (cast.caster != caster) continue;
                if (!cast.Fired) casts.RemoveAt(i);
                else cast.Release(now, false);
            }
        }

        /// <summary>
        /// The cast an ability's Apply works on: the one its job began, or, when it fired without the job (a test, a
        /// direct Activate), a new one whose warmup ended now.
        /// </summary>
        public static LastPrismCast For(Ability ability, LocalTargetInfo target)
        {
            GameComponent_LastPrism prisms = Instance;
            if (prisms == null) return null;
            LastPrismCast cast = prisms.Waiting(ability.pawn);
            if (cast == null)
            {
                cast = Make(ability.pawn, target, Find.TickManager.TicksGame - Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f));
                if (cast == null) return null;
                prisms.Begin(cast);
            }
            cast.target = target;
            return cast;
        }

        /// <summary>A cast of the prism <paramref name="caster"/> holds, its clock at 0 at <paramref name="startTick"/>, aimed at the target; null without a prism.</summary>
        public static LastPrismCast Make(Pawn caster, LocalTargetInfo target, int startTick)
        {
            CompLastPrism comp = CompLastPrism.HeldBy(caster);
            if (comp == null || !caster.Spawned) return null;
            Vector2 from = LastPrismBeam.Ground(caster.DrawPos);
            return new LastPrismCast
            {
                caster = caster, prism = comp.parent, home = caster.Map, startTick = startTick, target = target,
                aim = LastPrismBeam.Toward(from, LastPrismBeam.Point(target), (90f - caster.Rotation.AsAngle) * Mathf.Deg2Rad),
            };
        }

        /// <summary>For game tests: drops every beam and forgets every holder (the test's pawns are gone; its new ones register as they equip).</summary>
        public void ResetForTests()
        {
            casts.Clear();
            holders.Clear();
        }

        /// <summary>The comp's Notify_Equipped: <paramref name="pawn"/> holds a prism from now.</summary>
        public void Register(Pawn pawn)
        {
            if (pawn != null) holders.Add(pawn);
        }

        /// <summary>The comp's Notify_Unequipped.</summary>
        public void Unregister(Pawn pawn)
        {
            if (pawn != null) holders.Remove(pawn);
        }

        /// <summary>Every spawned pawn on any map holding a prism: equipment loaded with a save sends no Notify_Equipped, and a pawn can arrive on a map already holding one.</summary>
        private void Rescan()
        {
            holders.Clear();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = maps[m].mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                    if (CompLastPrism.HeldBy(pawns[i]) != null) holders.Add(pawns[i]);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Rescan();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
                if (i < casts.Count && !casts[i].Tick(now)) casts.RemoveAt(i);
            if (now % RescanEvery == 0) Rescan();
            if (now % ChargeEvery == 0) ChargeAll(ChargeEvery / 60f);
        }

        /// <summary>
        /// One charge step for every prism on every map: lying on the ground (spawned) or in a spawned holder's hands and
        /// not firing. A prism in an inventory, being carried, in a container or on a caravan is on none of these lists,
        /// so it does not fill.
        /// </summary>
        public void ChargeAll(float seconds)
        {
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                List<Thing> lying = map.listerThings.ThingsOfDef(LastPrismDefOf.AG_LastPrism);
                for (int i = 0; i < lying.Count; i++) lying[i].TryGetComp<CompLastPrism>()?.Charge(map, lying[i].Position, seconds);
            }
            foreach (Pawn pawn in holders)
            {
                CompLastPrism comp = pawn.Spawned ? CompLastPrism.HeldBy(pawn) : null;
                if (comp == null) continue;
                if (FiringBy(pawn) != null) comp.chargingNow = false;
                else comp.Charge(pawn.Map, pawn.Position, seconds);
            }
        }

        public override void GameComponentUpdate()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            drawn.Clear();
            for (int i = 0; i < casts.Count; i++)
            {
                LastPrismCast cast = casts[i];
                if (cast.caster?.Map != map) continue;
                cast.Draw();
                drawn.Add(cast.caster);
            }
            // Idle holders: the prism at the chest wherever Core would show a held weapon (drafted, aiming); the patch
            // keeps Core from drawing the weapon's texture as well. Copied first: drawing never changes the set, but a
            // Notify_Unequipped from a draw-time recache would.
            int clockBase = Find.TickManager.TicksGame / 36000 * 36000;
            holderList.Clear();
            holderList.AddRange(holders);
            for (int i = 0; i < holderList.Count; i++)
            {
                Pawn pawn = holderList[i];
                if (!pawn.Spawned || pawn.Map != map || drawn.Contains(pawn) || !PawnRenderUtility.CarryWeaponOpenly(pawn)) continue;
                CompLastPrism comp = CompLastPrism.HeldBy(pawn);
                if (comp != null) DrawIdle(pawn, comp, map, PictureClock.Since(clockBase));
            }
        }

        private static void DrawIdle(Pawn pawn, CompLastPrism comp, Map map, float s)
        {
            CompProperties_LastPrismFire p = CompProperties_LastPrismFire.Of;
            var shot = new LastPrismShot
            {
                Wielder = LastPrismBeam.Ground(pawn.DrawPos), Aim = 90f - pawn.Rotation.AsAngle,
                Join = p.joinSeconds, Fan = p.fanDegrees, Range = p.Range, Width = p.width, HitReach = p.fanReach,
                Roofed = pawn.Position.Roofed(map), Charging = comp.chargingNow, Level = comp.Level, Store = comp.Props.store,
            };
            LastPrismGraphics.Draw(shot, s, map);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "lastPrismCasts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<LastPrismCast>();
                // A cast that had not fired has no job left to fire it.
                casts.RemoveAll(c => c == null || c.caster == null || !c.Fired);
            }
        }
    }
}
