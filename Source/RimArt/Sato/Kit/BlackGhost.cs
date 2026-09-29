using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    public class GhostLife
    {
        public AjinPiece piece;
        public float seconds;
    }

    public class CompProperties_BlackGhostSummon : CompProperties_AbilityEffect
    {
        /// <summary>How close to the clicked cell one of his anchors must lie to count as the target.</summary>
        public float anchorSearchRadius = 1.5f;
        /// <summary>It fights enemies within this many cells of its guard spot.</summary>
        public float guardRadius = 9f;
        public List<GhostLife> lifeSeconds = new List<GhostLife>();

        public CompProperties_BlackGhostSummon() { compClass = typeof(CompAbilityEffect_BlackGhostSummon); }

        public float LifeSeconds(AjinPiece piece) => lifeSeconds.FirstOrDefault(l => l.piece == piece)?.seconds ?? 45f;
    }

    /// <summary>
    /// Black Ghost: forms next to Satō (as the body) or at one of his anchors in range, and lives by that piece.
    /// One at a time. The ghost itself is <see cref="CompBlackGhost"/>.
    /// </summary>
    public class CompAbilityEffect_BlackGhostSummon : CompAbilityEffect
    {
        public new CompProperties_BlackGhostSummon Props => (CompProperties_BlackGhostSummon)props;

        /// <summary>Where it forms and from which piece: his own cell counts as the body; else an anchor near the cell.</summary>
        public bool Resolve(LocalTargetInfo target, out AjinPiece piece, out IntVec3 spot)
        {
            Pawn pawn = parent.pawn;
            piece = AjinPiece.Body;
            spot = IntVec3.Invalid;
            if (pawn.Map == null || !target.IsValid) return false;
            if (target.Pawn == pawn || target.Cell == pawn.Position)
            {
                spot = pawn.Position;
                return true;
            }
            AjinAnchor anchor = AjinReset.Anchors(pawn, pawn.Map)
                .Where(a => !a.Flying && a.Position.DistanceTo(target.Cell) <= Props.anchorSearchRadius)
                .OrderBy(a => a.Position.DistanceToSquared(target.Cell)).FirstOrDefault();
            if (anchor == null) return false;
            piece = anchor.piece;
            spot = anchor.Position;
            return true;
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (BlackGhosts.Of(parent.pawn) != null)
            {
                reason = "AG_GhostAlreadyOut".Translate();
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!Resolve(target, out _, out _))
            {
                if (throwMessages) Messages.Message("AG_GhostTargetSelfOrAnchor".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (!Resolve(target, out AjinPiece piece, out IntVec3 spot)) return;
            Map map = pawn.Map;
            if (!CellFinder.TryFindRandomCellNear(spot, map, 2, c => c.Standable(map) && c.GetFirstPawn(map) == null, out IntVec3 cell))
                cell = spot;
            Pawn ghost = PawnGenerator.GeneratePawn(SatoDefOf.AG_BlackGhostKind, pawn.Faction);
            GenSpawn.Spawn(ghost, cell, map, Rot4.South);
            CompBlackGhost comp = ghost.TryGetComp<CompBlackGhost>();
            comp?.Begin(pawn, piece, Props.LifeSeconds(piece).SecondsToTicks(), Props.guardRadius);
        }
    }

    public static class BlackGhosts
    {
        /// <summary>His ghost on any map, if one is out and not dissolving.</summary>
        public static Pawn Of(Pawn owner)
        {
            if (owner == null) return null;
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    if (pawn.def == SatoDefOf.AG_BlackGhost && pawn.TryGetComp<CompBlackGhost>() is CompBlackGhost comp
                        && comp.owner == owner && !comp.Dissolving)
                        return pawn;
            return null;
        }
    }

    public class CompProperties_BlackGhost : CompProperties
    {
        /// <summary>
        /// The picture's size in game, about its feet. The sketch's ghost was drawn next to the lab's small two-disc
        /// stand-ins; real pawns are bigger (heads, fat and hulk bodies), so the ghost is drawn bigger to keep its
        /// size next to them. Tear's hands and the torn limb follow.
        /// </summary>
        public float drawScale = 1f;

        public CompProperties_BlackGhost() { compClass = typeof(CompBlackGhost); }

        public static float Scale => SatoDefOf.AG_BlackGhost.GetCompProperties<CompProperties_BlackGhost>()?.drawScale ?? 1f;
    }

    /// <summary>
    /// The Black Ghost's state: whose it is, the piece it formed at, its life, guard spot, orders and the Tear.
    /// It builds for <see cref="BuildTicks"/> (stunned), fights, then dissolves over <see cref="DissolveTicks"/>
    /// when its life runs out, when it would die or is downed, or when its owner is no longer manifested.
    /// </summary>
    public class CompBlackGhost : ThingComp
    {
        public Pawn owner;
        public AjinPiece piece;
        public int spawnTick, lifeTicks;
        public IntVec3 guard;
        public float guardRadius = 9f;
        public Pawn focus;
        public int dissolveTick = -1;
        public bool tearUsed;
        public int tearStart = -1;
        public Pawn tearTarget;
        public bool tearArm;
        public int swipeTick = -99999;
        /// <summary>What the last claw swing was at, and how many swings so far (odd and even use the two claws).</summary>
        public Thing swipeTarget;
        public int swipes;

        /// <summary>Pawns a ghost is tearing now, so the enemy's draw hooks find their pose without a search.</summary>
        public static readonly Dictionary<Pawn, CompBlackGhost> Tearing = new Dictionary<Pawn, CompBlackGhost>();
        public float walked;
        private Vector3 lastPos;

        public const int BuildTicks = 72, DissolveTicks = 60;

        public Pawn Ghost => (Pawn)parent;
        public bool Dissolving => dissolveTick >= 0;
        public bool Building => Find.TickManager.TicksGame - spawnTick < BuildTicks;
        public bool Busy => Dissolving || Building || tearStart >= 0;
        public float Seconds => (Find.TickManager.TicksGame - spawnTick).TicksToSeconds();
        public float LifeFrac => lifeTicks > 0 ? Mathf.Clamp01((Find.TickManager.TicksGame - spawnTick) / (float)lifeTicks) : 0f;

        public void Begin(Pawn owner, AjinPiece piece, int lifeTicks, float guardRadius)
        {
            this.owner = owner;
            this.piece = piece;
            this.lifeTicks = lifeTicks;
            this.guardRadius = guardRadius;
            spawnTick = Find.TickManager.TicksGame;
            guard = Ghost.Position;
            lastPos = Ghost.DrawPos;
            Ghost.stances?.stunner?.StunFor(BuildTicks, null, addBattleLog: false, showMote: false);
            SatoPictures.GhostSummoned(owner, Ghost);
        }

        public void Dissolve()
        {
            if (Dissolving) return;
            dissolveTick = Find.TickManager.TicksGame;
            Pawn ghost = Ghost;
            if (!ghost.Spawned) return;
            ghost.jobs?.StopAll();
            ghost.pather?.StopDead();
            ghost.stances?.stunner?.StunFor(DissolveTicks + 10, null, addBattleLog: false, showMote: false);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn ghost = Ghost;
            if (!ghost.Spawned) return;
            int now = Find.TickManager.TicksGame;
            if (Dissolving)
            {
                if (now - dissolveTick >= DissolveTicks) ghost.Destroy(DestroyMode.Vanish);
                return;
            }
            if (now - spawnTick >= lifeTicks || ghost.Downed || owner == null || owner.Discarded || !AjinReset.Manifested(owner))
            {
                Dissolve();
                return;
            }
            Vector3 pos = ghost.DrawPos;
            walked += new Vector2(pos.x - lastPos.x, pos.z - lastPos.z).magnitude;
            lastPos = pos;
        }

        public void StartTear(Pawn target, bool arm)
        {
            tearStart = Find.TickManager.TicksGame;
            tearTarget = target;
            tearArm = arm;
            if (target != null) Tearing[target] = this;
        }

        public void EndTear()
        {
            if (tearTarget != null && Tearing.TryGetValue(tearTarget, out CompBlackGhost comp) && comp == this) Tearing.Remove(tearTarget);
            tearStart = -1;
            tearTarget = null;
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            EndTear();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            Pawn ghost = Ghost;
            if (ghost.Faction != Faction.OfPlayer || Dissolving) yield break;

            yield return new Command_Action
            {
                defaultLabel = "AG_GhostGo".Translate(),
                defaultDesc = "AG_GhostGoDesc".Translate(),
                icon = SatoTex.GhostGo,
                action = () => Find.Targeter.BeginTargeting(new TargetingParameters { canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false },
                    t =>
                    {
                        guard = t.Cell;
                        focus = null;
                        Job job = JobMaker.MakeJob(JobDefOf.Goto, t.Cell);
                        job.locomotionUrgency = LocomotionUrgency.Sprint;
                        ghost.jobs.StartJob(job, JobCondition.InterruptForced);
                    }, ghost),
            };
            yield return new Command_Action
            {
                defaultLabel = "AG_GhostAttack".Translate(),
                defaultDesc = "AG_GhostAttackDesc".Translate(),
                icon = SatoTex.GhostAttack,
                action = () => Find.Targeter.BeginTargeting(EnemyParms(ghost), t =>
                {
                    focus = t.Pawn;
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, t.Pawn);
                    job.killIncappedTarget = false;
                    ghost.jobs.StartJob(job, JobCondition.InterruptForced);
                }, ghost),
            };
            var tear = new Command_Action
            {
                defaultLabel = "AG_GhostTear".Translate(),
                defaultDesc = "AG_GhostTearDesc".Translate(),
                icon = SatoTex.GhostTear,
                action = () => Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("AG_GhostTearArm".Translate(), () => OrderTear(true)),
                    new FloatMenuOption("AG_GhostTearLeg".Translate(), () => OrderTear(false)),
                })),
            };
            if (tearUsed) tear.Disable("AG_GhostTearUsed".Translate());
            yield return tear;
        }

        private void OrderTear(bool arm)
        {
            Pawn ghost = Ghost;
            Find.Targeter.BeginTargeting(EnemyParms(ghost, p => BlackGhostTear.CanTear(p, arm)), t =>
            {
                Job job = JobMaker.MakeJob(SatoDefOf.AG_BlackGhostTear, t.Pawn);
                job.count = arm ? 1 : 2;
                ghost.jobs.StartJob(job, JobCondition.InterruptForced);
            }, ghost);
        }

        private static TargetingParameters EnemyParms(Pawn ghost, System.Predicate<Pawn> also = null) => new TargetingParameters
        {
            canTargetPawns = true,
            canTargetBuildings = false,
            canTargetLocations = false,
            validator = t => t.Thing is Pawn p && p != ghost && !p.Dead && p.HostileTo(ghost) && (also == null || also(p)),
        };

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref owner, "owner");
            Scribe_Values.Look(ref piece, "piece");
            Scribe_Values.Look(ref spawnTick, "spawnTick");
            Scribe_Values.Look(ref lifeTicks, "lifeTicks");
            Scribe_Values.Look(ref guard, "guard");
            Scribe_Values.Look(ref guardRadius, "guardRadius", 9f);
            Scribe_References.Look(ref focus, "focus");
            Scribe_Values.Look(ref dissolveTick, "dissolveTick", -1);
            Scribe_Values.Look(ref tearUsed, "tearUsed");
            Scribe_Values.Look(ref walked, "walked");
            // A Tear in progress is not saved: its job ends on load.
        }
    }

    /// <summary>Its own fighting: the pawn it was told to attack, else the nearest standing enemy near its guard spot.</summary>
    public class JobGiver_BlackGhostFight : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompBlackGhost comp = pawn.TryGetComp<CompBlackGhost>();
            if (comp == null || comp.Busy) return null;
            Pawn target = Fightable(pawn, comp.focus) ? comp.focus : null;
            if (target == null)
            {
                comp.focus = null;
                float best = float.MaxValue;
                foreach (Pawn other in pawn.Map.mapPawns.AllPawnsSpawned)
                {
                    if (!Fightable(pawn, other) || !other.Position.InHorDistOf(comp.guard, comp.guardRadius)) continue;
                    float d = other.Position.DistanceToSquared(pawn.Position);
                    if (d < best && pawn.CanReach(other, PathEndMode.Touch, Danger.Deadly))
                    {
                        best = d;
                        target = other;
                    }
                }
            }
            if (target == null) return null;
            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job.killIncappedTarget = false;
            job.expiryInterval = 180;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }

        private static bool Fightable(Pawn ghost, Pawn other) =>
            other != null && other != ghost && other.Spawned && !other.Dead && !other.Downed && other.HostileTo(ghost);
    }

    /// <summary>Nothing to fight: back to the guard spot, then wait there a second and look again.</summary>
    public class JobGiver_BlackGhostGuard : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompBlackGhost comp = pawn.TryGetComp<CompBlackGhost>();
            if (comp != null && !comp.Busy && comp.guard.IsValid && !pawn.Position.InHorDistOf(comp.guard, 1.9f)
                && pawn.CanReach(comp.guard, PathEndMode.OnCell, Danger.Deadly))
            {
                Job go = JobMaker.MakeJob(JobDefOf.Goto, comp.guard);
                go.locomotionUrgency = LocomotionUrgency.Jog;
                go.expiryInterval = 120;
                go.checkOverrideOnExpire = true;
                return go;
            }
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = 60;
            wait.checkOverrideOnExpire = true;
            return wait;
        }
    }

    /// <summary>The ghost has no sprite: its picture is drawn in code in place of the whole pawn renderer.</summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    static class Patch_PawnRenderer_BlackGhost
    {
        static bool Prefix(Pawn ___pawn, Vector3 drawLoc)
        {
            if (___pawn.def != SatoDefOf.AG_BlackGhost) return true;
            CompBlackGhost comp = ___pawn.TryGetComp<CompBlackGhost>();
            if (comp != null) SatoPictures.DrawGhost(___pawn, comp, drawLoc);
            return false;
        }
    }

    /// <summary>A claw swing starts the ghost's swipe picture (vanilla calls this on every melee attack).</summary>
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.Notify_MeleeAttackOn))]
    static class Patch_PawnDrawTracker_BlackGhostSwipe
    {
        static void Postfix(Pawn ___pawn, Thing Target)
        {
            if (___pawn.def != SatoDefOf.AG_BlackGhost) return;
            CompBlackGhost comp = ___pawn.TryGetComp<CompBlackGhost>();
            if (comp == null) return;
            comp.swipeTick = Find.TickManager.TicksGame;
            comp.swipeTarget = Target;
            comp.swipes++;
        }
    }
}
