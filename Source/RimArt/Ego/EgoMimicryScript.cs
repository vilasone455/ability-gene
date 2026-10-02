using System.Collections.Generic;
using UnityEngine;
using static RimArt.VfxMath;
using T = RimArt.EgoMimicryTiming;

namespace RimArt
{
    /// <summary>The sketch's four showcases.</summary>
    public enum EgoMimicryMode { Swings, Grown, Corroded, Overclock }

    /// <summary>One pawn of the scenario: where it stands (cells from the wielder's start), the hits it takes, when it goes down.</summary>
    public sealed class EgoMimicryPerson
    {
        public Vector2 Off;
        /// <summary>Hits to go down.</summary>
        public int Hp;
        public bool Hostile, Shooter;
        /// <summary>Times and directions (the swing's aim) of the hits taken.</summary>
        public readonly List<float> HitTimes = new List<float>();
        public readonly List<Vector2> HitDirs = new List<Vector2>();
        /// <summary>Which of <see cref="EgoMimicryScript.Actions"/> landed each hit: a grown hit draws no cut.</summary>
        public readonly List<bool> HitGrown = new List<bool>();
        /// <summary>When it went down; negative while standing.</summary>
        public float DownAt = -1f;
    }

    /// <summary>
    /// One action: T when it is chosen, <see cref="Start"/> when the swing starts (after a 0.2 s lunge from
    /// <see cref="From"/> to <see cref="To"/> when <see cref="Lunge"/>), <see cref="Hit"/> when it lands; <see cref="Aim"/>
    /// toward <see cref="Target"/> (an index into EgoMimicryScript.People).
    /// </summary>
    public struct EgoMimicryAction
    {
        public float T, Start, Hit, Aim;
        public bool Grown, Lunge;
        public int Target;
        public Vector2 From, To;
    }

    /// <summary>The arm going to <see cref="To"/> stages from time T over <see cref="Dur"/> s.</summary>
    public struct EgoMimicryStageStep
    {
        public float T, To, Dur;
    }

    /// <summary>The colonist's shot that tears a stage off: fired at T, hits at <see cref="HitT"/>, the wielder at <see cref="At"/> aiming <see cref="Aim"/>.</summary>
    public struct EgoMimicryShot
    {
        public float T, HitT, Aim;
        public Vector2 From, At;
    }

    /// <summary>
    /// The preview's script, the sketch's plan(): who stands where, who the wielder swings at and when, the
    /// arm's stages and the shot. Built once per preview; every query is a pure function of the time. Positions
    /// are cells from the wielder's starting point, turned with the aim.
    ///
    /// Swings: three swings <see cref="Spacing"/> s apart at a raider 1 cell off. Grown swing: a swing, then the
    /// grown swing at 1.2 s; the raider goes down. Corroded (the rules' hunt): an ally 2 cells off, a raider behind
    /// it, a colonist with a rifle behind the wielder; four actions 1.5 s apart at the nearest pawn of any faction,
    /// downed ones included, with a lunge of up to 2 cells first when nobody is within
    /// <see cref="EgoMimicryTiming.Adjacent"/>: the wielder lunges at the ally, downs it with the second hit and keeps
    /// cutting it. Each hit adds an arm stage; the colonist's shot 0.75 s after the third hit takes one off.
    /// Overclock: five actions 1 s apart, standing still, at standing hostiles within reach only: the ally next to the
    /// wielder and the raider 3 cells off are never chosen; the first raider goes down at its third hit and the
    /// second takes the rest. The state ends 0.6 intervals after the last hit and the arm recedes in 1 s.
    /// </summary>
    public sealed class EgoMimicryScript
    {
        /// <summary>The showcase's swing spacing, the first swing's delay, and how long the result shows.</summary>
        public const float Spacing = 0.9f, Lead = 0.3f, Hold = 1.2f;

        public readonly EgoMimicryMode Mode;
        public readonly float Aim;
        public readonly EgoMimicryPerson[] People;
        public readonly EgoMimicryAction[] Actions;
        public readonly EgoMimicryStageStep[] Stages;
        public readonly EgoMimicryShot[] Shots;
        /// <summary>When the corroded state ends (negative in the swing showcases), and when the preview ends.</summary>
        public readonly float ExitAt = -1f, End;

        /// <summary>Corroded or overclocked: the arm and the floor glow are drawn.</summary>
        public bool Armed => Mode == EgoMimicryMode.Corroded || Mode == EgoMimicryMode.Overclock;

        public EgoMimicryScript(EgoMimicryMode mode, float aim)
        {
            Mode = mode;
            Aim = aim;
            Vector2 d = T.Dir(aim), across = T.Side(d);
            Vector2 F(float along, float side) => d * along + across * side;
            var people = new List<EgoMimicryPerson>();
            var actions = new List<EgoMimicryAction>();
            var stages = new List<EgoMimicryStageStep>();
            var shots = new List<EgoMimicryShot>();
            void Person(Vector2 off, int hp, bool hostile, bool shooter = false) =>
                people.Add(new EgoMimicryPerson { Off = off, Hp = hp, Hostile = hostile, Shooter = shooter });
            void HitOn(EgoMimicryPerson q, float t, Vector2 dir, bool grown)
            {
                q.HitTimes.Add(t);
                q.HitDirs.Add(dir);
                q.HitGrown.Add(grown);
                if (q.HitTimes.Count >= q.Hp && q.DownAt < 0f) q.DownAt = t;
            }

            if (mode == EgoMimicryMode.Swings || mode == EgoMimicryMode.Grown)
            {
                bool swings = mode == EgoMimicryMode.Swings;
                Person(F(1f, 0f), swings ? 99 : 2, true);
                int n = swings ? 3 : 2;
                for (int i = 0; i < n; i++)
                {
                    bool grown = !swings && i == 1;
                    float t = Lead + i * Spacing, hit = t + (grown ? T.SlamAt : T.HitAt);
                    actions.Add(new EgoMimicryAction { T = t, Start = t, Hit = hit, Aim = aim, Grown = grown, Target = 0 });
                    HitOn(people[0], hit, d, grown);
                }
                EgoMimicryAction last = actions[n - 1];
                End = last.Start + (last.Grown ? T.GrownLength : T.SwingLength) + Hold;
            }
            else
            {
                bool corroded = mode == EgoMimicryMode.Corroded;
                if (corroded)
                {
                    Person(F(1.9f, 0.6f), 2, false);
                    Person(F(3.6f, -0.4f), 2, true);
                    Person(F(-3f, -1.6f), 99, false, shooter: true);
                }
                else
                {
                    Person(F(0f, 1f), 2, false);
                    Person(F(1f, -0.25f), 3, true);
                    Person(F(-0.3f, -1.05f), 2, true);
                    Person(F(3.1f, -0.6f), 2, true);
                }
                float every = corroded ? T.CorrodedInterval : T.OverclockInterval;
                int n = corroded ? T.CorrodedActions : T.OverclockActions;
                Vector2 w = Vector2.zero;
                float stage = 1f;
                stages.Add(new EgoMimicryStageStep { T = 0f, To = 1f, Dur = T.Enter });
                for (int k = 0; k < n; k++)
                {
                    float t = T.Enter + k * every;
                    // Corroded: the nearest pawn of any faction, downed ones too. Overclock: the nearest standing hostile in reach.
                    int pick = -1;
                    float best = float.MaxValue;
                    for (int i = 0; i < people.Count; i++)
                    {
                        EgoMimicryPerson q = people[i];
                        float dist = (q.Off - w).magnitude;
                        if (!corroded && (!q.Hostile || (q.DownAt >= 0f && q.DownAt <= t) || dist > T.Adjacent)) continue;
                        if (dist < best) { best = dist; pick = i; }
                    }
                    if (pick < 0) break;
                    EgoMimicryPerson target = people[pick];
                    Vector2 v = target.Off - w, u = T.Unit(v);
                    float lunge = corroded && best > T.Adjacent ? Mathf.Min(T.LungeCells, best - 1f) : 0f;
                    Vector2 to = w + u * lunge;
                    float start = t + (lunge > 0f ? T.LungeTime : 0f), hit = start + T.HitAt;
                    actions.Add(new EgoMimicryAction { T = t, Start = start, Hit = hit, Aim = T.DegOf(u), Target = pick, From = w, To = to, Lunge = lunge > 0f });
                    HitOn(target, hit, u, false);
                    stage = Mathf.Min(T.ArmStages, stage + 1f);
                    stages.Add(new EgoMimicryStageStep { T = hit, To = stage, Dur = T.StageGrow });
                    w = to;
                    if (corroded && k == 2)
                    {
                        EgoMimicryPerson shooter = people.Find(q => q.Shooter);
                        float st = hit + 0.5f * every;
                        shots.Add(new EgoMimicryShot { T = st, HitT = st + 0.05f, From = shooter.Off, At = w, Aim = T.DegOf(u) });
                        stage = Mathf.Max(0f, stage - 1f);
                        stages.Add(new EgoMimicryStageStep { T = st + 0.05f, To = stage, Dur = T.StageShrink });
                    }
                }
                ExitAt = actions[actions.Count - 1].Hit + 0.6f * every;
                stages.Add(new EgoMimicryStageStep { T = ExitAt, To = 0f, Dur = T.Exit });
                End = ExitAt + T.Exit + Hold;
            }
            People = people.ToArray();
            Actions = actions.ToArray();
            Stages = stages.ToArray();
            Shots = shots.ToArray();
        }

        /// <summary>The arm's stage at <paramref name="s"/>, 0 to 4, fractional while it grows or shrinks.</summary>
        public float StageAt(float s)
        {
            float from = 0f, cur = 0f;
            foreach (EgoMimicryStageStep e in Stages)
            {
                if (s < e.T) break;
                cur = Mathf.Lerp(from, e.To, Smooth((s - e.T) / e.Dur));
                from = e.To;
            }
            return cur;
        }

        /// <summary>The wielder's offset from its start at <paramref name="s"/> (lunges are 0.2 s, eased).</summary>
        public Vector2 WielderAt(float s)
        {
            Vector2 w = Vector2.zero;
            foreach (EgoMimicryAction a in Actions)
                if (a.Lunge && s >= a.T) w = Vector2.Lerp(a.From, a.To, Smooth((s - a.T) / T.LungeTime));
            return w;
        }

        /// <summary>The last action chosen by <paramref name="s"/>, or -1.</summary>
        public int ActionAt(float s)
        {
            int at = -1;
            for (int i = 0; i < Actions.Length; i++)
                if (Actions[i].T <= s) at = i;
            return at;
        }

        /// <summary>Seconds since the last hit that landed by <paramref name="s"/>, or -1.</summary>
        public float FeedAge(float s)
        {
            float age = -1f;
            foreach (EgoMimicryAction a in Actions)
                if (a.Hit <= s) age = s - a.Hit;
            return age;
        }

        /// <summary>How far a pawn has rocked away from its hits at <paramref name="s"/>: 0.07 cells over 0.25 s per hit.</summary>
        public Vector2 Rock(EgoMimicryPerson q, float s)
        {
            Vector2 off = Vector2.zero;
            for (int j = 0; j < q.HitTimes.Count; j++)
            {
                float a = s - q.HitTimes[j];
                if (a >= 0f && a < 0.25f) off += q.HitDirs[j] * (0.07f * T.Bump(a / 0.25f));
            }
            return off;
        }

        /// <summary>The blade's eyes opened wide by the corroded state: over 0.6 s from its start, closed over the arm's exit.</summary>
        public float Open(float s) => Armed ? Smooth(s / 0.6f) * (1f - Smooth((s - ExitAt) / T.Exit)) : 0f;

        /// <summary>The camera shakes, as (time, amount) in time order: 0.012 per hit, 0.07 for the slam, 0.01 per lunge, 0.015 for the shot.</summary>
        public List<Vector2> Shakes()
        {
            var shakes = new List<Vector2>();
            foreach (EgoMimicryAction a in Actions)
            {
                shakes.Add(new Vector2(a.Hit, a.Grown ? 0.07f : 0.012f));
                if (a.Lunge) shakes.Add(new Vector2(a.T, 0.01f));
            }
            foreach (EgoMimicryShot sh in Shots) shakes.Add(new Vector2(sh.HitT, 0.015f));
            shakes.Sort((x, y) => x.x.CompareTo(y.x));
            return shakes;
        }
    }
}
