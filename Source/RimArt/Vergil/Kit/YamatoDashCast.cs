using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.YamatoDashTiming;

namespace RimArt
{
    public class CompProperties_YamatoDash : CompProperties_AbilityEffect
    {
        public float dashSeconds = 0.15f;
        /// <summary>From stopping to the click, when every mark resolves.</summary>
        public float sheatheSeconds = 0.4f;
        /// <summary>How wide the marking path is, centred on the line.</summary>
        public float pathWidth = 1f;
        public float damage = 24f;
        public DamageDef damageDef;
        public float armorPenetration = 0.4f;
        public float styleGainPerMark = 3f;

        public CompProperties_YamatoDash()
        {
            compClass = typeof(CompAbilityEffect_YamatoDash);
        }
    }

    /// <summary>
    /// Yamato Dash. The target must be a standable cell with nobody on it, reached along a straight line of
    /// walkable cells (other pawns do not block: he passes through them). On the fire tick the dash starts.
    /// </summary>
    public class CompAbilityEffect_YamatoDash : CompAbilityEffect_Vergil
    {
        public new CompProperties_YamatoDash Props => (CompProperties_YamatoDash)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string problem = Problem(parent.pawn, target.Cell);
            if (problem == null) return base.Valid(target, throwMessages);
            if (throwMessages) Messages.Message(problem, parent.pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        /// <summary>Why the dash cannot go to <paramref name="dest"/>, or null.</summary>
        public static string Problem(Pawn pawn, IntVec3 dest)
        {
            Map map = pawn?.Map;
            if (map == null || !dest.IsValid || !dest.InBounds(map)) return "Out of bounds.";
            if (dest == pawn.Position) return "Already there.";
            if (!dest.Standable(map)) return "Cannot stand there.";
            Pawn standing = dest.GetFirstPawn(map);
            if (standing != null && standing != pawn) return "Someone is standing there.";
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(pawn.Position, dest))
                if (!cell.InBounds(map) || !cell.Walkable(map)) return "The path is blocked.";
            return null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            YamatoDashCast cast = VergilCasts.For<YamatoDashCast>(parent, target);
            if (cast == null) return;
            cast.Launch(target.Cell, Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One Yamato Dash (the sketch vergil-yamato-dash.js). The warmup is the prepare: Vergil sinks a little,
    /// his hand goes to the hilt, an aura stands up. The fire tick launches the dash: every hostile within half
    /// the path's width of the line is marked as he passes, and he is drawn along the line with the blade out;
    /// his cell changes once, on arrival. He stands still for the sheathe while the job holds him, and on the
    /// click every mark takes its cut. Allies are never marked.
    /// </summary>
    public sealed class YamatoDashCast : VergilCast
    {
        public IntVec3 from, dest;
        private List<Pawn> marks = new List<Pawn>();
        /// <summary>For each mark, how far along the path it stood (0 to 1): the carrier passes it then.</summary>
        private List<float> along = new List<float>();
        private bool arrived, clicked, aborted;

        /// <summary>How opaque an afterimage is when it appears; it fades to nothing over its life.</summary>
        private const float GhostAlpha = 0.85f;

        private CompProperties_YamatoDash Props => VergilKit.Props<CompProperties_YamatoDash>(VergilDefOf.AG_VergilYamatoDash) ?? new CompProperties_YamatoDash();
        private DashTimes Times
        {
            get
            {
                CompProperties_YamatoDash props = Props;
                return new DashTimes(VergilDefOf.AG_VergilYamatoDash.verbProperties.warmupTime, props.dashSeconds, props.sheatheSeconds);
            }
        }
        public override AbilityDef Def => VergilDefOf.AG_VergilYamatoDash;
        protected override float Lead => T.Lead;
        protected override float FireAt => Times.LaunchAt;

        private Vector2 Line => new Vector2(dest.x - from.x, dest.z - from.z);
        private float Aim => Mathf.Atan2(Line.y, Line.x) * Mathf.Rad2Deg;

        public void Launch(IntVec3 target, int now)
        {
            from = caster.Position;
            dest = target;
            MarkFired(now);
            marks.Clear();
            along.Clear();
            Map map = caster.Map;
            if (map == null) return;
            VergilSound.Play(VergilSoundDefOf.AG_VergilDash, map, from);
            KeepDash();
            CompProperties_YamatoDash props = Props;
            Vector2 a = new Vector2(from.x, from.z), line = Line;
            float length = line.magnitude;
            if (length < 0.01f) return;
            Vector2 d = line / length;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!VergilKit.Foe(caster, pawn)) continue;
                Vector2 at = new Vector2(pawn.Position.x, pawn.Position.z) - a;
                float forward = Vector2.Dot(at, d), side = Mathf.Abs(d.x * at.y - d.y * at.x);
                if (forward < 0f || forward > length + 0.5f || side > props.pathWidth / 2f) continue;
                marks.Add(pawn);
                along.Add(Mathf.Clamp01(forward / length));
            }
        }

        /// <summary>He stands until just after the click, unless the job was already broken off.</summary>
        public override bool Holds(int now) => Fired && !aborted && now < TickAt(Times.ClickAt + 0.1f);
        public override Rot4 Facing(int now) => Rot4.FromAngleFlat(90f - Aim);

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            DashTimes times = Times;
            // Kept every tick of the dash, so a cast loaded mid-dash draws him moving again (the runs are not saved).
            if (!arrived && !aborted && now < TickAt(times.ArriveAt)) KeepDash();
            if (!arrived && now >= TickAt(times.ArriveAt)) Arrive();
            if (!clicked && now >= TickAt(times.ClickAt)) Click();
            return Seconds(now) < times.Duration;
        }

        /// <summary>He is drawn along the line, at one speed, from the launch to the arrival (<see cref="PawnDash"/>).</summary>
        private void KeepDash()
        {
            DashTimes times = Times;
            int launch = TickAt(times.LaunchAt);
            PawnDash.Keep(caster, from, dest, launch, Mathf.Max(1, TickAt(times.ArriveAt) - launch), eased: false);
        }

        /// <summary>The dash is over: the cell changes, once (<see cref="PawnDash.Arrive"/>).</summary>
        private void Arrive()
        {
            arrived = true;
            PawnDash.Arrive(caster, home, dest, needEmpty: false);
        }

        /// <summary>The blade clicks home: every mark takes its cut. The cuts are heard only when there were some.</summary>
        private void Click()
        {
            clicked = true;
            CompProperties_YamatoDash props = Props;
            int cut = 0;
            for (int i = 0; i < marks.Count; i++)
            {
                Pawn pawn = marks[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned) continue;
                VergilKit.Cut(pawn, caster, props.damageDef, props.damage, props.armorPenetration, 90f - Aim);
                VergilStyle.Hit(caster, pawn, props.styleGainPerMark);
                cut++;
            }
            IntVec3 at = caster?.PositionHeld ?? dest;
            VergilSound.Play(VergilSoundDefOf.AG_VergilSheathe, home, at);
            if (cut > 0) VergilSound.Play(VergilSoundDefOf.AG_VergilDashCuts, home, at);
        }

        /// <summary>Downed or killed mid-dash: nothing resolves. He stays where the game left him.</summary>
        public override void JobEnded(int now)
        {
            PawnDash.Stop(caster);
            arrived = clicked = aborted = true;
            marks.Clear();
            along.Clear();
        }

        public override void Pose(float s)
        {
            if (caster == null || !caster.Spawned) return;
            DashTimes times = Times;
            float warm = VfxMath.Smooth((s - T.Lead) / times.Warm), finish = Mathf.Clamp01((s - times.ArriveAt) / times.Sheathe);
            float hand = VfxMath.Smooth(warm * 1.5f) * (1f - VfxMath.Smooth((s - times.ClickAt) / 0.2f));
            if (hand <= 0f && s > times.ClickAt) return;
            VergilLook look = VergilLooks.For(caster);
            look.hand = hand;
            look.crouch = warm * (1f - VfxMath.Smooth(finish));
            if (!Fired) return;
            float travel = times.Travel(s);
            bool dashing = s >= times.LaunchAt && s < times.ArriveAt;
            look.aim = Aim;
            look.blade = s < times.LaunchAt ? 0f : s < times.ArriveAt ? VfxMath.Smooth(travel / 0.18f) : 1f - VfxMath.Smooth((finish - 0.2f) / 0.8f);
            look.hot = dashing ? 0.85f : 0.25f * (1f - finish);
            // His drawn position along the line is PawnDash's, kept from Tick.
            if (dashing)
            {
                look.tint = VergilGraphics.Ice;
                look.tintAmount = 0.45f;
            }

            // Marked pawns turn a little blue once he has passed them, until the click.
            for (int i = 0; i < marks.Count; i++)
                if (s >= times.LaunchAt + along[i] * times.Dash && s < times.ClickAt && marks[i] != null)
                    VergilLooks.Tint(marks[i], VergilGraphics.Blue, 0.2f);
        }

        public override void Draw(float s)
        {
            if (home == null || caster == null) return;
            DashTimes times = Times;
            Vector2 start = Fired ? VergilKit.Ground(from) : VergilKit.Ground(caster.Position);
            Vector2 end = Fired ? VergilKit.Ground(dest) : start + new Vector2(1f, 0f);
            Vector2 hilt = caster.Spawned ? YamatoDraw.Guard(caster) : start + new Vector2(0f, 0.46f);
            if (!Fired)
            {
                // The prepare before the fire: only the aura and the hilt glint, round where he stands.
                if (caster.Spawned) YamatoDashGraphics.Draw(VergilKit.Ground(caster.DrawPos), VergilKit.Ground(caster.DrawPos) + Vector2.right * 0.01f, hilt, times, Mathf.Min(s, times.LaunchAt - 0.001f), home);
                return;
            }
            YamatoDashGraphics.Draw(start, end, hilt, times, s, home);
            // His afterimages where he passed, his own body (the graphics draw only the line of light behind each).
            if (caster.Spawned && caster.Map == home)
                for (int i = 0; i < T.Ghosts.Length; i++)
                {
                    float age = s - (times.LaunchAt + T.Ghosts[i] * times.Dash);
                    if (age >= 0f && age < T.GhostLife)
                        VergilGhost.Draw(caster, Vector2.Lerp(start, end, T.Ghosts[i]), VergilGhost.Facing(end - start), GhostAlpha * (1f - age / T.GhostLife));
                }

            // A mark keeps a quiet dark seam across its chest; on the click it opens into a hot cut.
            for (int i = 0; i < marks.Count; i++)
            {
                Pawn pawn = marks[i];
                if (pawn == null || !pawn.Spawned || pawn.Map != home) continue;
                Vector2 ground = VergilKit.Ground(pawn.DrawPos);
                if (s >= times.LaunchAt + along[i] * times.Dash && s < times.ClickAt)
                    VfxDraw.Sprite(new Vector2(ground.x, ground.y + 0.31f), 0.28f, 0.022f, VfxDraw.Fade(VergilGraphics.Deep, 0.75f), VfxDraw.solid,
                        VfxDraw.Overhead + 0.035f, -Aim - 45f);
                VergilGraphics.HitCut(ground, Aim + 45f, s - times.ClickAt);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref dest, "dest");
            Scribe_Values.Look(ref arrived, "arrived");
            Scribe_Values.Look(ref clicked, "clicked");
            Scribe_Values.Look(ref aborted, "aborted");
            Scribe_Collections.Look(ref marks, "marks", LookMode.Reference);
            Scribe_Collections.Look(ref along, "along", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (marks == null) marks = new List<Pawn>();
                if (along == null) along = new List<float>();
                while (along.Count < marks.Count) along.Add(0f);
            }
        }
    }
}
