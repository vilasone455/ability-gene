using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// What a <see cref="RimArtTestAttribute"/> test works with: a cleared arena on the current map,
    /// pawn setup, a trace written to the results file, and checks. A failed check does not stop the
    /// test; the test fails if any check failed, it threw, it timed out, or an error was logged while
    /// it ran.
    /// </summary>
    public class RimArtTestContext
    {
        /// <summary>Yield this to take a screenshot (named by <see cref="shotName"/>) before the next step.</summary>
        public const int Shot = -1;

        public readonly Map map;
        public readonly IntVec3 center;
        public readonly List<string> failures = new List<string>();
        internal string shotName;
        internal readonly RimArtTestRunner runner;

        /// <summary>Half the side of the square <see cref="Clear"/> clears.</summary>
        public const int ArenaRadius = 12;

        internal RimArtTestContext(RimArtTestRunner runner, Map map)
        {
            this.runner = runner;
            this.map = map;
            center = map.Center;
        }

        public int Now => Find.TickManager.TicksGame;

        public void Log(string line) => runner.Write("    " + line);

        public bool Check(bool ok, string what)
        {
            runner.Write("    " + (ok ? "ok   " : "FAIL ") + what);
            if (!ok) failures.Add(what);
            return ok;
        }

        /// <summary>Names the screenshot the next <c>yield return Shot</c> takes.</summary>
        public int ShotAs(string name)
        {
            shotName = name;
            return Shot;
        }

        /// <summary>
        /// Makes the square around the map centre a flat, lit, unroofed, unfogged floor with nothing on
        /// it, and removes every other pawn on the map so no one walks in or fights.
        /// </summary>
        public void Clear()
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList()) pawn.Destroy();
            CellRect rect = CellRect.CenteredOn(center, ArenaRadius).ClipInsideMap(map);
            foreach (IntVec3 c in rect)
            {
                foreach (Thing thing in c.GetThingList(map).ToList())
                    if (thing.def.destroyable) thing.Destroy();
                map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil);
                map.roofGrid.SetRoof(c, null);
                map.fogGrid.Unfog(c);
            }
        }

        public Pawn Colonist(IntVec3 at)
        {
            var request = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, mustBeCapableOfViolence: true, forceNoGear: true);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, at, map);
            pawn.drafter.Drafted = true;
            return pawn;
        }

        /// <summary>A hostile humanlike from an enemy faction, told to stand still.</summary>
        public Pawn Enemy(IntVec3 at, bool armed = true)
        {
            Faction faction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            PawnKindDef kind = faction?.def.basicMemberKind ?? PawnKindDefOf.Villager;
            var request = new PawnGenerationRequest(kind, faction, mustBeCapableOfViolence: true, dontGiveWeapon: !armed);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, at, map);
            Hold(pawn);
            return pawn;
        }

        /// <summary>Starts a long Wait job so the pawn stands where it is.</summary>
        public static void Hold(Pawn pawn)
        {
            Job wait = JobMaker.MakeJob(JobDefOf.Wait, 6000);
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
        }

        public void Equip(Pawn pawn, ThingDef weapon)
        {
            if (pawn.equipment.Primary != null) pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(weapon));
        }

        /// <summary>One line on a pawn: where it is, its job and toil, stance, stun, and whether it is inside a flyer.</summary>
        public static string Describe(Pawn pawn)
        {
            if (pawn == null) return "null";
            var sb = new StringBuilder();
            sb.Append(pawn.LabelShort);
            if (pawn.Dead) return sb.Append(" dead").ToString();
            if (pawn.Spawned) sb.Append(" at ").Append(pawn.Position);
            else if (pawn.ParentHolder is PawnFlyer flyer) sb.Append(" in flyer at ").Append(flyer.Position);
            else sb.Append(" not spawned (holder ").Append(pawn.ParentHolder?.GetType().Name ?? "none").Append(")");
            if (pawn.Downed) sb.Append(" DOWNED");
            Job job = pawn.CurJob;
            sb.Append(" job=").Append(job == null ? "none" : job.def.defName);
            if (pawn.jobs?.curDriver != null) sb.Append("#").Append(pawn.jobs.curDriver.CurToilIndex);
            Stance stance = pawn.stances?.curStance;
            if (stance != null && !(stance is Stance_Mobile)) sb.Append(" stance=").Append(stance.GetType().Name);
            if (pawn.stances?.stunner != null && pawn.stances.stunner.Stunned) sb.Append(" stunned=").Append(pawn.stances.stunner.StunTicksLeft);
            if (pawn.pather != null && pawn.pather.Moving) sb.Append(" moving");
            if (pawn.jobs != null && pawn.jobs.jobQueue.Count > 0) sb.Append(" queue=").Append(pawn.jobs.jobQueue.Count);
            return sb.ToString();
        }
    }
}
