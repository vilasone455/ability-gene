using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.HollowPurple;

namespace RimArt
{
    /// <summary>
    /// Hollow Purple's numbers (AG_GojoHollowPurple). The ability is never cast: it holds Purple's cooldown (1 day,
    /// the def's cooldownTicksRange) and these fields, and Red makes Purple when it meets a Blue (<see cref="RedShot"/>).
    /// </summary>
    public class CompProperties_GojoHollowPurple : CompProperties_AbilityEffect
    {
        /// <summary>Red makes Purple when its centre comes this close to the centre of an active Blue Gojo cast.</summary>
        public float blueRadius = 1f;
        /// <summary>The sphere's radius and speed (cells/s), and its longest run in cells (it also stops at the map edge).</summary>
        public float radius = 1.5f, speed = 6f, maxTravel = 30f;
        /// <summary>A pawn whose cell centre is this close to the path is erased; the others the sphere touches take damage (erasure, ignores armour).</summary>
        public float centreRow = 0.5f, damage = 60f;

        public CompProperties_GojoHollowPurple()
        {
            compClass = typeof(CompAbilityEffect_GojoHollowPurple);
        }
    }

    /// <summary>Hollow Purple's button: it shows the cooldown and cannot be clicked.</summary>
    public class CompAbilityEffect_GojoHollowPurple : CompAbilityEffect
    {
        public const string HowTo = "Fire Red through an active Blue.";

        public override bool GizmoDisabled(out string reason)
        {
            reason = HowTo;
            return true;
        }
    }

    /// <summary>What Purple did to one pawn (for the tests' trace). Not saved.</summary>
    public struct PurpleHit
    {
        public Pawn pawn;
        public bool erased;
        /// <summary>For a pawn it struck: the damage dealt, and the part it hit (null if none).</summary>
        public float dealt;
        public BodyPartRecord part;
    }

    /// <summary>
    /// One Hollow Purple, from the tick Red met Blue (the picture's Contact) to the end of its picture. The sphere
    /// forms at Blue's centre (<see cref="start"/>) and travels along Red's way (<see cref="dir"/>): it waits out the
    /// picture's merge and growth (0.65 s), then runs <see cref="travel"/> cells at <see cref="speed"/>. Each tick,
    /// every cell whose centre is within <see cref="radius"/> of the stretch the centre ran is under the sphere:
    /// <list type="bullet">
    /// <item>a pawn there (not Gojo), once each: erased if its cell centre is within <see cref="centreRow"/> of the path
    /// and it is not a boss (dies with no corpse, its apparel, weapon and inventory destroyed, nothing left, the kill
    /// Gojo's); otherwise it takes <see cref="damage"/> erasure damage that ignores armour, and a part it destroys does
    /// not bleed;</item>
    /// <item>every other thing there that can be destroyed (buildings, walls, rock and ore, plants, items, corpses,
    /// filth, blueprints and frames) vanishes: no drops, refund or chunks. A pawn inside a building (a casket, a bed
    /// holder) is erased with it. Pawns in flight, drop pods and motes are left.</item>
    /// </list>
    /// Behind it, as the centre passes each cell whose centre is within the radius of the path run so far (the
    /// picture's lane), built floors are removed without refund and the ground becomes Erased ground (water, and
    /// ground nobody can stand on, stay as they are), and the roof is removed, thick rock roof too.
    /// </summary>
    public sealed class PurpleRun : IExposable
    {
        public Pawn caster;
        public int comboTick;
        /// <summary>Blue's centre, Red's unit way, and Gojo's point when Red left (where the arm is drawn).</summary>
        public Vector2 start, dir, armAt;
        /// <summary>Blue's centre along Red's line from Gojo's point, and the run (cells).</summary>
        public float blueDist, travel;
        public float radius = T.Radius, speed = T.Speed, centreRow = T.CentreRow, damage = 60f;
        /// <summary>Cells the centre has run from Blue's centre, -1 before it starts to move.</summary>
        public float run = -1f;
        private HashSet<int> lane = new HashSet<int>();
        private HashSet<int> touched = new HashSet<int>();

        // The picture's and the tests' records, not saved.
        public readonly List<HollowPurpleTouch> touches = new List<HollowPurpleTouch>();
        public readonly List<HollowPurpleCut> cuts = new List<HollowPurpleCut>();
        public readonly List<PurpleHit> hits = new List<PurpleHit>();
        private HollowPurpleTouch[] touchArray;
        private HollowPurpleCut[] cutArray;
        private int shaken;
        private float lastRumble;
        private static readonly List<Thing> buffer = new List<Thing>();

        /// <summary>At most this many touched things are drawn breaking up.</summary>
        private const int MostTouches = 160;

        public Vector2 End => start + dir * travel;
        public HollowPurpleTimes Times => T.TimesFor(blueDist, travel, GojoRed.Charge, T.Merge, speed);
        public int MoveTick => comboTick + Mathf.RoundToInt((Times.Move - Times.Contact) * 60f);
        public int EndTick => comboTick + Mathf.CeilToInt((Times.End - Times.Contact) * 60f);
        public bool Stopped => run >= travel;
        public float Seconds(int now) => Times.Contact + (now - comboTick) / 60f;
        public bool Erased(IntVec3 cell, Map map) => lane.Contains(map.cellIndices.CellToIndex(cell));

        /// <summary>Cells from <paramref name="from"/> along <paramref name="way"/> to the edge of the map.</summary>
        public static float ToEdge(Map map, Vector2 from, Vector2 way)
        {
            float t = float.MaxValue;
            if (way.x > 1e-5f) t = Mathf.Min(t, (map.Size.x - from.x) / way.x);
            else if (way.x < -1e-5f) t = Mathf.Min(t, -from.x / way.x);
            if (way.y > 1e-5f) t = Mathf.Min(t, (map.Size.z - from.y) / way.y);
            else if (way.y < -1e-5f) t = Mathf.Min(t, -from.y / way.y);
            return Mathf.Max(0f, t);
        }

        /// <summary>One game tick. False once its picture has ended.</summary>
        public bool Tick(Map map, int now)
        {
            if (now >= MoveTick && !Stopped)
            {
                float from = Mathf.Max(0f, run), to = Mathf.Min(travel, (now - MoveTick) / 60f * speed);
                Sweep(map, from, to, Seconds(now));
                run = to;
            }
            return now <= EndTick;
        }

        private static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float k = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * k)).magnitude;
        }

        private float Across(Vector2 p) => Vector2.Dot(p - start, new Vector2(-dir.y, dir.x));

        private void Sweep(Map map, float from, float to, float s)
        {
            Vector2 a = start + dir * from, b = start + dir * to;
            float pad = radius + 1f;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - pad), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + pad);
            int z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - pad), z1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + pad);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    var c = new IntVec3(x, 0, z);
                    if (!c.InBounds(map)) continue;
                    Vector2 p = GojoKit.Ground(c);
                    if (ToSegment(p, a, b) <= radius + 1e-3f) Touch(map, c, p, s);
                    int index = map.cellIndices.CellToIndex(c);
                    if (lane.Contains(index)) continue;
                    float along = Vector2.Dot(p - start, dir);
                    if (!T.InLane(along, Across(p), 0f, to, travel, radius)) continue;
                    lane.Add(index);
                    Scar(map, c);
                }
        }

        // ---- under the sphere -------------------------------------------------------------------------------------------

        private void Touch(Map map, IntVec3 c, Vector2 p, float s)
        {
            buffer.Clear();
            buffer.AddRange(c.GetThingList(map));
            foreach (Thing thing in buffer)
            {
                if (thing.Destroyed || !thing.Spawned) continue;
                if (thing is Pawn pawn)
                {
                    TouchPawn(pawn, p, s);
                    continue;
                }
                if (!Erasable(thing)) continue;
                bool edifice = thing.def.IsEdifice();
                if (!(thing is Filth) && !(thing is Blueprint)) Record(thing, p, s, edifice ? HollowPurpleTouchKind.Building : HollowPurpleTouchKind.Thing);
                EraseHeld(thing);
                thing.Destroy(DestroyMode.Vanish);
                if (edifice) CutFaces(map, c, p, s);
            }
            buffer.Clear();
        }

        /// <summary>What the sphere takes: anything destroyable but pawns, pawns in flight, drop pods and motes.</summary>
        private static bool Erasable(Thing thing) => thing.def.destroyable && !(thing is PawnFlyer) && !(thing is Skyfaller) && !(thing is Mote);

        private void TouchPawn(Pawn pawn, Vector2 cell, float s)
        {
            if (pawn == caster || pawn.Dead || touched.Contains(pawn.thingIDNumber)) return;
            touched.Add(pawn.thingIDNumber);
            Vector2 ground = GojoKit.Ground(pawn.DrawPos);
            Color colour = Tint(pawn);
            bool boss = pawn.kindDef?.isBoss == true;
            if (ToSegment(cell, start, End) <= centreRow + 1e-4f && !boss)
            {
                Record(ground, Across(ground), s, HollowPurpleTouchKind.Erased, colour);
                Erase(pawn);
                return;
            }
            Record(ground, Across(ground), s, HollowPurpleTouchKind.Struck, colour);
            Strike(pawn);
        }

        /// <summary>
        /// Erased: its apparel, weapon, inventory and what it carries are destroyed (a carried pawn is put down, for
        /// the sphere to meet on its own), then it is killed by Gojo with no leavings and no death action (no
        /// boomalope blast), and its corpse vanishes.
        /// </summary>
        private void Erase(Pawn pawn)
        {
            if (pawn.carryTracker?.CarriedThing is Pawn)
                pawn.carryTracker.TryDropCarriedThing(pawn.PositionHeld, ThingPlaceMode.Near, out _);
            pawn.carryTracker?.DestroyCarriedThing();
            pawn.apparel?.DestroyAll();
            pawn.equipment?.DestroyAllEquipment();
            pawn.inventory?.DestroyAll();
            Kill(pawn);
            hits.Add(new PurpleHit { pawn = pawn, erased = true });
        }

        private void Kill(Pawn pawn)
        {
            var dinfo = new DamageInfo(GojoKitDefOf.AG_Erasure, 99999f, 999f, GojoKit.Angle(dir), caster);
            Patch_GojoErasureLeavings.suppress = true;
            try
            {
                pawn.Kill(dinfo);
            }
            finally
            {
                Patch_GojoErasureLeavings.suppress = false;
            }
            if (!pawn.Dead) return;
            Corpse corpse = pawn.Corpse;
            if (corpse != null && !corpse.Destroyed) corpse.Destroy(DestroyMode.Vanish);
        }

        /// <summary>A building holding pawns (a casket, a shuttle): each inside is erased with it.</summary>
        private void EraseHeld(Thing thing)
        {
            if (!(thing is IThingHolder holder) || !(thing is Building)) return;
            ThingOwner held = holder.GetDirectlyHeldThings();
            if (held == null) return;
            foreach (Pawn inside in held.OfType<Pawn>().ToList())
            {
                if (inside.Dead || inside == caster) continue;
                touched.Add(inside.thingIDNumber);
                inside.apparel?.DestroyAll();
                inside.equipment?.DestroyAllEquipment();
                inside.inventory?.DestroyAll();
                Kill(inside);
                hits.Add(new PurpleHit { pawn = inside, erased = true });
            }
        }

        /// <summary>Struck: the erasure damage, ignoring armour; a part it destroyed is gone and does not bleed.</summary>
        private void Strike(Pawn pawn)
        {
            var dinfo = new DamageInfo(GojoKitDefOf.AG_Erasure, damage, 0f, GojoKit.Angle(dir), caster);
            dinfo.SetIgnoreArmor(true);
            DamageWorker.DamageResult result = pawn.TakeDamage(dinfo);
            if (result?.hediffs != null)
                foreach (Hediff hediff in result.hediffs)
                    if (hediff is Hediff_MissingPart missing) missing.IsFresh = false;
            hits.Add(new PurpleHit { pawn = pawn, dealt = result?.totalDamageDealt ?? 0f, part = result?.LastHitPart });
        }

        /// <summary>The face of each standing wall cell next to an erased one, cut where the lane's edge runs.</summary>
        private void CutFaces(Map map, IntVec3 c, Vector2 p, float s)
        {
            for (int i = 0; i < 4; i++)
            {
                IntVec3 off = GenAdj.CardinalDirections[i], n = c + off;
                if (!n.InBounds(map) || n.GetEdifice(map) == null) continue;
                if (ToSegment(GojoKit.Ground(n), start, End) <= radius + 1e-3f) continue;
                Vector2 mid = p + new Vector2(off.x, off.z) * 0.5f, edge = new Vector2(-off.z, off.x) * 0.5f;
                cuts.Add(new HollowPurpleCut { From = mid - edge, To = mid + edge, At = s });
                cutArray = null;
            }
        }

        private void Record(Thing thing, Vector2 cell, float s, HollowPurpleTouchKind kind) =>
            Record(cell, Across(cell), s, kind, thing is Plant ? Leaves : thing.DrawColor);

        private void Record(Vector2 ground, float across, float s, HollowPurpleTouchKind kind, Color colour)
        {
            if (touches.Count >= MostTouches) return;
            touches.Add(new HollowPurpleTouch { Ground = ground, Across = across, At = s, Kind = kind, Colour = colour });
            touchArray = null;
        }

        private static readonly Color Leaves = new Color(0.25f, 0.45f, 0.2f);

        /// <summary>A pawn's specks: the outermost thing worn on the torso, else its skin, else its body's colour.</summary>
        private static Color Tint(Pawn pawn)
        {
            if (pawn.apparel != null)
                for (int i = pawn.apparel.WornApparel.Count - 1; i >= 0; i--)
                {
                    Apparel worn = pawn.apparel.WornApparel[i];
                    if (worn.def.apparel?.bodyPartGroups?.Any(g => g.defName == "Torso") == true) return worn.DrawColor;
                }
            if (pawn.story != null) return pawn.story.SkinColor;
            return pawn.ageTracker?.CurKindLifeStage?.bodyGraphicData?.color ?? Color.gray;
        }

        // ---- behind it: the lane --------------------------------------------------------------------------------------

        /// <summary>
        /// One cell of the lane: temporary ground that is not water goes, built floors and foundations are removed
        /// with no refund, and the ground becomes Erased ground unless it is water (or temporary water) or ground
        /// nobody can stand on; the roof is removed.
        /// </summary>
        private static void Scar(Map map, IntVec3 c)
        {
            TerrainGrid grid = map.terrainGrid;
            map.roofGrid.SetRoof(c, null);
            TerrainDef temp = grid.TempTerrainAt(c);
            if (temp != null)
            {
                if (temp.IsWater) return;
                grid.RemoveTempTerrain(c);
            }
            for (int guard = 0; guard < 4 && (grid.UnderTerrainAt(c) != null || grid.FoundationAt(c) != null); guard++)
                grid.RemoveTopLayer(c, doLeavings: false);
            TerrainDef ground = grid.TerrainAt(c);
            if (ground == GojoKitDefOf.AG_ErasedGround || ground.IsWater || ground.passability == Traversability.Impassable
                || (TerrainDefOf.Space != null && ground == TerrainDefOf.Space)) return;
            grid.SetTerrain(c, GojoKitDefOf.AG_ErasedGround);
        }

        // ---- the picture ----------------------------------------------------------------------------------------------

        public void Draw(Map map)
        {
            HollowPurpleTimes t = Times;
            float s = t.Contact + UbwClock.Since(comboTick);
            Shake(t, s);
            touchArray ??= touches.ToArray();
            cutArray ??= cuts.ToArray();
            float aim = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            HollowPurpleGraphics.Draw(new HollowPurpleShot
            {
                Feet = start - dir * blueDist, ArmAt = armAt, Aim = aim, BlueDist = blueDist, Travel = travel,
                Charge = GojoRed.Charge, Merge = T.Merge, Speed = speed, Radius = radius, Dim = T.Dim, Trench = true,
                Seconds = s, Touched = touchArray, Cuts = cutArray, Sleeve = GojoKit.Sleeve(caster), Skin = GojoKit.Skin(caster),
            }, map);
        }

        /// <summary>The sketch's shakes: Red reaching Blue, the ignition, a rumble every 0.5 s of travel.</summary>
        private void Shake(HollowPurpleTimes t, float s)
        {
            if (shaken == 0)
            {
                shaken = 1;
                if (s < t.Contact + 0.25f) Find.CameraDriver.shaker.DoShake(T.ContactShake);
            }
            if (shaken == 1 && s >= t.Ignite)
            {
                shaken = 2;
                lastRumble = t.Move;
                if (s < t.Ignite + 0.25f) Find.CameraDriver.shaker.DoShake(T.IgniteShake);
            }
            if (shaken == 2 && s < t.Stop && s >= lastRumble + T.RumbleEvery)
            {
                lastRumble += T.RumbleEvery * Mathf.Floor((s - lastRumble) / T.RumbleEvery);
                Find.CameraDriver.shaker.DoShake(T.RumbleShake);
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref comboTick, "comboTick");
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref dir, "dir");
            Scribe_Values.Look(ref armAt, "armAt");
            Scribe_Values.Look(ref blueDist, "blueDist");
            Scribe_Values.Look(ref travel, "travel");
            Scribe_Values.Look(ref radius, "radius", T.Radius);
            Scribe_Values.Look(ref speed, "speed", T.Speed);
            Scribe_Values.Look(ref centreRow, "centreRow", T.CentreRow);
            Scribe_Values.Look(ref damage, "damage", 60f);
            Scribe_Values.Look(ref run, "run", -1f);
            Scribe_Collections.Look(ref lane, "lane", LookMode.Value);
            Scribe_Collections.Look(ref touched, "touched", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lane ??= new HashSet<int>();
                touched ??= new HashSet<int>();
                shaken = 3;
            }
        }
    }

    /// <summary>
    /// While Hollow Purple erases a pawn, its death leaves nothing: no blood, no debris, no killed-leavings, and
    /// (<see cref="Patch_GojoErasureDeathAction"/>) no death action.
    /// </summary>
    [HarmonyPatch(typeof(GenLeaving), nameof(GenLeaving.DoLeavingsFor), new[] { typeof(Thing), typeof(Map), typeof(DestroyMode),
        typeof(CellRect), typeof(Predicate<IntVec3>), typeof(List<Thing>) })]
    public static class Patch_GojoErasureLeavings
    {
        public static bool suppress;

        public static bool Prefix() => !suppress;
    }

    /// <summary>
    /// While Hollow Purple erases a pawn, its race's death action does not run: a boomalope or boomrat does not
    /// explode, nothing leaves a toxic cloud, nothing splits. Pawn.Kill reads RaceProps.DeathActionWorker and
    /// calls its PawnDied; during the erasure that worker is one that does nothing.
    /// </summary>
    [HarmonyPatch(typeof(RaceProperties), nameof(RaceProperties.DeathActionWorker), MethodType.Getter)]
    public static class Patch_GojoErasureDeathAction
    {
        private static readonly DeathActionWorker Nothing = new DeathActionWorker_Simple();

        public static void Postfix(ref DeathActionWorker __result)
        {
            if (Patch_GojoErasureLeavings.suppress) __result = Nothing;
        }
    }
}
