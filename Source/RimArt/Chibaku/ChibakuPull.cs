using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimArt
{
    /// <summary>
    /// One thing taken by the ball, from the moment the pull holds it until it lands again: a pawn, an item
    /// (a corpse, a weapon, a stack, a chunk), or a tree, which is only a picture (the tree itself is gone).
    /// </summary>
    public sealed class ChibakuHeld : IExposable
    {
        public const int Waiting = 0, Flying = 1, Held = 2, Falling = 3, Landed = 4;

        public Pawn pawn;
        public Thing item;
        public bool tree;
        public Lord lord;
        public int state;
        /// <summary>Seconds on the ball's timeline: when it leaves the ground, and when it lands after the burst.</summary>
        public float liftAt, landAt;
        /// <summary>Where it stood when it was lifted (its draw position), and where it lands.</summary>
        public Vector3 from;
        public IntVec3 landCell;
        /// <summary>Its offset under the ball as it falls out, in cells.</summary>
        public Vector2 drop;
        /// <summary>Its picture and the cells the picture covers; the picture is a render of its own (a pawn, a corpse) or its graphic.</summary>
        internal Texture picture;
        internal Material material;
        internal Vector2 size = Vector2.one;
        internal bool ownsPicture;

        /// <summary>The pawn or item on the map or in the ball (a tree has none).</summary>
        public Thing Thing => (Thing)pawn ?? item;

        public void ExposeData()
        {
            // A lord that has ended is saved nowhere, and a reference to it would not resolve on load.
            if (Scribe.mode == LoadSaveMode.Saving && lord != null && (lord.Map == null || !lord.Map.lordManager.lords.Contains(lord))) lord = null;
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_References.Look(ref item, "item", true);
            Scribe_References.Look(ref lord, "lord");
            Scribe_Values.Look(ref state, "state");
        }
    }

    /// <summary>
    /// The rules of Chibaku Tensei's pull on what stands on the ground (agreed 2026-09-28: everyone is caught,
    /// it may kill, a roof protects, big bodies are caught; the numbers are the ability's XML fields):
    ///
    /// - From the start of the pull until the ball can still take them in, any pawn standing on a plate that
    ///   is pulled (not under a building or a roof) is caught: stunned where it stands, then lifted just
    ///   before its plate tears free and pulled into the core. Colonists are caught too. A pawn that walks
    ///   onto the circle during the pull is caught as well. Pain (the caster) and a pawn Black Receiver pins
    ///   (<see cref="PainKit.Unmovable"/>) are never caught, and the plates they stood on when the ground was
    ///   captured stay.
    /// - When a plate tears free, what lies on it goes with it: items (corpses, weapons, stacks, chunks) are
    ///   pulled into the ball; small plants are gone (they are printed on the plate's picture and on the
    ///   ball); a tree is torn out and flies in as its own piece, gone for good (no wood); filth is gone.
    /// - Lifting takes a pawn or item off the map into the ball (held by the map component, so the game still
    ///   counts it on the map); a pawn keeps ticking (bleeding, needs), cannot act or be targeted, and leaves
    ///   its raid group.
    /// - At the burst everything inside falls out round the spot under the ball (pawns within about 1 cell,
    ///   items within about 2) and lands when it reaches the ground (0.89 s from the default height of 5 cells). A
    ///   pawn takes crushPerSecond blunt for each second it was held plus fallDamage, in hits of up to hitSize, from
    ///   Pain, is stunned stunSeconds and goes back to its raid group (a hostile whose group has ended gets a new
    ///   assault group). Items land unhurt, stacks as they were.
    /// - When the ball is formed, the natural soil of the pulled plates becomes stony soil (only a soil more fertile
    ///   than stony soil changes; built floors keep their plate). At the burst the biggest thrown rocks land as real
    ///   chunks of the map's rock, one for every platesPerChunk plates pulled (minChunks to maxChunks); the other
    ///   rocks leave rock rubble.
    ///
    /// The debug previews have no caster.
    /// </summary>
    public sealed class ChibakuPull
    {
        public const float LeadOfPlate = .12f, FlySeconds = .75f;
        public const float PawnSpread = 2.2f, ItemSpread = 4.4f;

        private readonly MapComponent_ChibakuPlates owner;
        private readonly Map map;
        private readonly ChibakuBall ball;
        private readonly IntVec3 centre;
        /// <summary>Pain, or null in a debug preview: never caught, and the damage is his.</summary>
        private readonly Pawn caster;
        private readonly CompProperties_ChibakuTensei props;
        public readonly List<ChibakuHeld> pawns = new List<ChibakuHeld>();
        private readonly HashSet<ChibakuPlate> cleared = new HashSet<ChibakuPlate>();
        private readonly HashSet<int> rocksLanded = new HashSet<int>();
        private bool burst, soilChanged;
        private List<ThingDef> chunkDefs;

        /// <summary>Cells turned to stony soil, and real chunks put down (for tests).</summary>
        public int SoilChanged { get; private set; }
        public readonly List<Thing> chunks = new List<Thing>();

        public ChibakuPull(MapComponent_ChibakuPlates owner, ChibakuBall ball, IntVec3 centre, Pawn caster, CompProperties_ChibakuTensei props)
        {
            this.owner = owner;
            map = owner.map;
            this.ball = ball;
            this.centre = centre;
            this.caster = caster;
            this.props = props;
        }

        /// <summary>The last moment a pawn can be lifted and still reach the ball before it is formed.</summary>
        public static float LastCatch => ChibakuBall.Formed - FlySeconds;

        public void Tick(float s)
        {
            if (s >= ChibakuBall.Pull && s < LastCatch) Catch(s);
            if (s >= ChibakuBall.Pull) ClearPlates(s);
            foreach (ChibakuHeld h in pawns)
            {
                if (h.state == ChibakuHeld.Waiting)
                {
                    if (h.pawn == null || !h.pawn.Spawned || h.pawn.Map != map) { h.state = ChibakuHeld.Landed; continue; }
                    if (s >= h.liftAt) Lift(h);
                }
                else if (h.state == ChibakuHeld.Flying && s >= h.liftAt + FlySeconds) h.state = ChibakuHeld.Held;
            }
            if (!burst && s >= ball.Burst)
            {
                burst = true;
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(1.2f);
                int k = 0;
                foreach (ChibakuHeld h in pawns)
                {
                    if (h.state != ChibakuHeld.Flying && h.state != ChibakuHeld.Held) continue;
                    if (h.tree) { h.state = ChibakuHeld.Landed; Forget(h); continue; }
                    h.state = ChibakuHeld.Falling;
                    h.drop = new Vector2((float)ChibakuCut.Rand(k * 9 + 401) - .5f, (float)ChibakuCut.Rand(k * 9 + 402) - .5f) * (h.pawn != null ? PawnSpread : ItemSpread);
                    k++;
                    h.landAt = ball.Burst + ball.FallTime;
                    h.landCell = Standable(new IntVec3(Mathf.RoundToInt(centre.x + h.drop.x), 0, Mathf.RoundToInt(centre.z + h.drop.y)));
                }
            }
            if (!soilChanged && s >= ChibakuBall.Formed) ChangeSoil();
            if (s >= ball.Burst) LandRocks(s - ball.Burst);
            var landed = new List<ChibakuHeld>();
            foreach (ChibakuHeld h in pawns)
                if (h.state == ChibakuHeld.Falling && s >= h.landAt) landed.Add(h);
            if (landed.Count > 0) Land(landed, true);
        }

        /// <summary>Every spawned pawn on a pulled plate that is not caught yet is caught now.</summary>
        private void Catch(float s)
        {
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == caster || PainKit.Unmovable(p) || pawns.Any(h => h.pawn == p) || !ball.TryLiftOf(p.Position, out float plateLift)) continue;
                var h = new ChibakuHeld { pawn = p, state = ChibakuHeld.Waiting, liftAt = Mathf.Max(s, plateLift - LeadOfPlate) };
                pawns.Add(h);
                int ticks = Mathf.CeilToInt((h.liftAt - s) * 60f) + 5;
                if (ticks > 0) p.stances?.stunner?.StunFor(ticks, null, false, false);
            }
        }

        /// <summary>Each plate that has torn free takes what lies on it: items into the ball, a tree as a piece; small plants and filth are gone.</summary>
        private void ClearPlates(float s)
        {
            foreach (var (plate, liftAt) in ball.PulledPlates)
            {
                if (s < liftAt || !cleared.Add(plate)) continue;
                foreach (IntVec3 c in plate.cells)
                {
                    if (!c.InBounds(map)) continue;
                    foreach (Thing thing in c.GetThingList(map).ToList())
                    {
                        if (thing.Destroyed || !thing.Spawned) continue;
                        if (thing is Plant plant)
                        {
                            if (plant.def.plant.IsTree) TearOut(plant, s);
                            else plant.Destroy();
                        }
                        else if (thing.def.category == ThingCategory.Filth) thing.Destroy();
                        else if (thing.def.category == ThingCategory.Item) TakeItem(thing, s);
                    }
                }
            }
        }

        /// <summary>
        /// Once, when the ball is formed (under the crater's drawing): the natural soil of every pulled plate that is
        /// more fertile than stony soil becomes stony soil (vanilla Gravel). Sand, marsh, stone and floors are left.
        /// </summary>
        private void ChangeSoil()
        {
            soilChanged = true;
            TerrainDef stony = TerrainDefOf.Gravel;
            foreach (var (plate, _) in ball.PulledPlates)
                foreach (IntVec3 c in plate.cells)
                {
                    if (!c.InBounds(map)) continue;
                    TerrainDef terrain = c.GetTerrain(map);
                    if (terrain == stony || !terrain.IsSoil || terrain.IsFloor || terrain.fertility <= stony.fertility) continue;
                    map.terrainGrid.SetTerrain(c, stony);
                    SoilChanged++;
                }
        }

        /// <summary>
        /// Each thrown rock as it lands: the biggest become real chunks of the map's own rock (one of its natural rock
        /// types, in turn), the rest leave rock rubble.
        /// </summary>
        private void LandRocks(float age)
        {
            if (chunkDefs == null)
            {
                chunkDefs = Find.World.NaturalRockTypesIn(map.Tile).Select(r => r.building?.mineableThing).Where(d => d != null).ToList();
                if (chunkDefs.Count == 0) chunkDefs.Add(DefDatabase<ThingDef>.GetNamed("ChunkGranite"));
            }
            foreach (var (m, land, after, real) in ball.ThrownRocks)
            {
                if (age < after || !rocksLanded.Add(m)) continue;
                var cell = new IntVec3(Mathf.FloorToInt(land.x), 0, Mathf.FloorToInt(land.y));
                if (!cell.InBounds(map)) continue;
                if (real)
                {
                    Thing chunk = ThingMaker.MakeThing(chunkDefs[chunks.Count % chunkDefs.Count]);
                    if (GenPlace.TryPlaceThing(chunk, cell, map, ThingPlaceMode.Near, out Thing placed)) chunks.Add(placed);
                }
                else FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_RubbleRock);
            }
        }

        private void Lift(ChibakuHeld h)
        {
            Pawn p = h.pawn;
            h.from = p.DrawPos;
            h.picture = KamuiBend.Render(p, p.Rotation);
            h.material = new Material(ShaderDatabase.Transparent) { mainTexture = h.picture, name = "RimArt Chibaku pawn" };
            h.ownsPicture = true;
            h.size = Vector2.one * KamuiBend.PawnCells;
            h.lord = p.GetLord();
            h.lord?.RemovePawn(p);
            if (p.carryTracker?.CarriedThing != null) p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
            p.jobs?.StopAll();
            p.pather?.StopDead();
            p.DeSpawn(DestroyMode.WillReplace);
            if (!owner.Inner.TryAdd(p, false))
            {
                GenSpawn.Spawn(p, Standable(p.PositionHeld), map);
                h.state = ChibakuHeld.Landed;
                Forget(h);
                return;
            }
            h.state = ChibakuHeld.Flying;
        }

        /// <summary>An item into the ball: a corpse's picture is its pawn rendered; anything else flies as its own graphic.</summary>
        private void TakeItem(Thing thing, float s)
        {
            var h = new ChibakuHeld { item = thing, state = ChibakuHeld.Flying, liftAt = s, from = thing.DrawPos };
            if (thing is Corpse corpse && corpse.InnerPawn != null)
            {
                h.picture = KamuiBend.Render(corpse.InnerPawn, Rot4.South);
                h.material = new Material(ShaderDatabase.Transparent) { mainTexture = h.picture, name = "RimArt Chibaku corpse" };
                h.ownsPicture = true;
                h.size = Vector2.one * KamuiBend.PawnCells;
            }
            else
            {
                h.material = thing.Graphic?.MatAt(thing.Rotation, thing);
                h.size = thing.Graphic?.drawSize ?? Vector2.one;
            }
            thing.DeSpawn();
            if (!owner.Inner.TryAdd(thing, false))
            {
                GenPlace.TryPlaceThing(thing, Standable(centre), map, ThingPlaceMode.Near);
                Forget(h);
                return;
            }
            pawns.Add(h);
        }

        /// <summary>A tree torn out: gone for good, flying into the ball as its picture.</summary>
        private void TearOut(Plant tree, float s)
        {
            float visual = tree.def.plant.visualSizeRange.LerpThroughRange(tree.Growth);
            pawns.Add(new ChibakuHeld
            {
                tree = true, state = ChibakuHeld.Flying, liftAt = s, from = tree.DrawPos + new Vector3(0f, 0f, visual * .35f),
                material = tree.Graphic?.MatSingleFor(tree), size = tree.def.graphicData.drawSize * visual,
            });
            tree.Destroy();
        }

        /// <summary>
        /// Puts things back on the map. <paramref name="hurt"/>: a landing from the burst (a pawn takes crush and
        /// fall damage and is stunned); otherwise a plain release (the preview stopped early).
        /// </summary>
        public void Land(List<ChibakuHeld> which, bool hurt)
        {
            var assault = new List<Pawn>();
            foreach (ChibakuHeld h in which)
            {
                h.state = ChibakuHeld.Landed;
                Forget(h);
                IntVec3 at = h.landCell.IsValid ? h.landCell : Standable(centre);
                if (h.item != null)
                {
                    if (owner.Inner.Contains(h.item)) owner.Inner.TryDrop(h.item, at, map, ThingPlaceMode.Near, out _);
                    continue;
                }
                Pawn p = h.pawn;
                if (p == null || !owner.Inner.Contains(p)) continue;
                owner.Inner.Remove(p);
                GenSpawn.Spawn(p, at, map);
                if (!hurt) { Rejoin(h, assault); continue; }
                float held = Mathf.Max(0f, ball.Burst - (h.liftAt + FlySeconds));
                float hit = Mathf.Max(1f, props.hitSize);
                for (float left = props.crushPerSecond * held + props.fallDamage; left > 0f && !p.Dead; left -= hit)
                    p.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Mathf.Min(hit, left), 0f, -1f, caster));
                if (p.Dead) continue;
                p.stances?.stunner?.StunFor(Mathf.RoundToInt(props.stunSeconds * 60f), caster, false);
                Rejoin(h, assault);
            }
            foreach (IGrouping<Faction, Pawn> group in assault.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_AssaultColony(group.Key, false, false, false, false, false), map, group);
            // Anything else inside that nothing is waiting for (a pawn that died in there is a corpse now) is dropped under the ball.
            for (int i = owner.Inner.Count - 1; i >= 0; i--)
            {
                Thing inside = owner.Inner[i];
                if (!pawns.Any(h => h.Thing == inside && h.state != ChibakuHeld.Landed))
                    owner.Inner.TryDrop(inside, Standable(centre), map, ThingPlaceMode.Near, out _);
            }
        }

        /// <summary>Everything still up is put down now, unhurt (the preview was stopped).</summary>
        public void ReleaseAll()
        {
            var up = pawns.Where(h => !h.tree && (h.state == ChibakuHeld.Flying || h.state == ChibakuHeld.Held || h.state == ChibakuHeld.Falling)).ToList();
            foreach (ChibakuHeld h in up) h.landCell = Standable(centre);
            Land(up, false);
            foreach (ChibakuHeld h in pawns) Forget(h);
        }

        private void Rejoin(ChibakuHeld h, List<Pawn> assault)
        {
            Pawn p = h.pawn;
            if (h.lord != null && map.lordManager.lords.Contains(h.lord)) h.lord.AddPawn(p);
            else if (p.Faction != null && p.Faction != Faction.OfPlayer && p.HostileTo(Faction.OfPlayer)) assault.Add(p);
        }

        private static void Forget(ChibakuHeld h)
        {
            if (h.ownsPicture)
            {
                if (h.material != null) Object.Destroy(h.material);
                if (h.picture != null) KamuiBend.Free(h.picture);
            }
            h.material = null;
            h.picture = null;
        }

        private IntVec3 Standable(IntVec3 cell) => CompNezukoBox.StandableNear(cell, map, null);
    }
}
