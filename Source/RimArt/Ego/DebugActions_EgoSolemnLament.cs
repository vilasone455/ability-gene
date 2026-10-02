using System.Collections.Generic;
using UnityEngine;
using Verse;
using T = RimArt.EgoSolemnLamentTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: there is no weapon, no def and no pawn rule behind any of this; nobody takes a stack, is
    /// hurt or goes down, and no pawn is drawn. Each entry plays a Solemn Lament picture with the wielder on the
    /// chosen cell, which is the cell the lab's sketch (ego-solemn-lament.js) centres on, on the sketch's
    /// clock, so the recorder can compare the port with it.
    /// </summary>
    public static class DebugActions_EgoSolemnLament
    {
        [RimArtDebug("E.G.O.", "solemn lament: burst")]
        public static void Burst() => Play(EgoSolemnLamentScene.Burst, 0f);

        [RimArtDebug("E.G.O.", "solemn lament: burst, aim 90")]
        public static void Burst90() => Play(EgoSolemnLamentScene.Burst, 90f);

        [RimArtDebug("E.G.O.", "solemn lament: burst, aim 160")]
        public static void Burst160() => Play(EgoSolemnLamentScene.Burst, 160f);

        [RimArtDebug("E.G.O.", "solemn lament: burst, aim 250")]
        public static void Burst250() => Play(EgoSolemnLamentScene.Burst, 250f);

        [RimArtDebug("E.G.O.", "solemn lament: corroded, the coffin")]
        public static void Coffin() => Play(EgoSolemnLamentScene.Corroded, 0f);

        [RimArtDebug("E.G.O.", "solemn lament: corroded, the coffin, aim 200")]
        public static void Coffin200() => Play(EgoSolemnLamentScene.Corroded, 200f);

        [RimArtDebug("E.G.O.", "solemn lament: corroded, the funeral")]
        public static void Funeral() => Play(EgoSolemnLamentScene.Funeral, 0f);

        [RimArtDebug("E.G.O.", "solemn lament: overclock, the coffin, hostiles only")]
        public static void Overclock() => Play(EgoSolemnLamentScene.Overclock, 0f);

        [RimArtDebug("E.G.O.", "solemn lament: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_EgoSolemnLamentPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(EgoSolemnLamentScene scene, float aim) =>
            Find.CurrentMap.GetComponent<MapComponent_EgoSolemnLamentPreview>().Play(UI.MouseCell(), scene, aim);
    }

    /// <summary>
    /// The sketch's modes: the burst at a raider, the corroded coffin, the corroded coffin long enough for the
    /// funeral (both pawns go down and die), the overclocked coffin (hostiles only, no face).
    /// </summary>
    public enum EgoSolemnLamentScene { Burst, Corroded, Funeral, Overclock }

    /// <summary>
    /// Plays Solemn Lament on the sketch's clock with the sketch's defaults. The script is built on Play by
    /// <see cref="EgoSolemnLamentScript"/>: the burst's eight planned shots at a raider 5 cells off (it stops at
    /// the 7th, which reaches the cap), or the coffin's 4 s of cloud (23 s for the funeral, where both pawns go
    /// down at the cap and die at the death count). Corroded, the wielder walks along the aim to
    /// the nearest pawn, an ally 5 cells off, and stops 1 cell short (<see cref="EgoSolemnLamentScript.WielderAt"/>);
    /// a raider stands 2.6 cells from where the walk ends at -70 degrees from the aim, so both are outside the
    /// radius at the start and inside on arrival; the coffin stays on the chosen cell. Overclock holds, with an
    /// ally 1.8 cells off at 150 degrees from the aim (skipped) and raiders 2.4 cells off at 0 and 4.2 off at -55
    /// (outside the radius). The stand-ins' sway, flinches and fall (<see cref="EgoSolemnLamentMark.Now"/>,
    /// <see cref="EgoSolemnLamentMark.Fall"/>) move the butterflies resting on them.
    /// </summary>
    public sealed class MapComponent_EgoSolemnLamentPreview : MapComponent
    {
        public bool active;
        private float seconds, aim, end, last, downAt, walk, home;
        private EgoSolemnLamentScene scene;
        private IntVec3 cell;
        private int shaken, shotCount, peopleCount;
        private readonly EgoSolemnLamentShot[] shots = new EgoSolemnLamentShot[T.ScriptShots];
        private EgoSolemnLamentMark target;
        private readonly EgoSolemnLamentMark[] people = new EgoSolemnLamentMark[3];
        private readonly bool[] skip = new bool[3];
        private readonly List<int> diveSlots = new List<int>();
        private readonly List<float> diveTimes = new List<float>(), diveFlys = new List<float>();

        // Overclock's people: degrees from the aim, cells from the wielder, an ally.
        private static readonly float[] PeopleTurn = { 150f, 0f, -55f }, PeopleDist = { 1.8f, 2.4f, 4.2f };
        private static readonly bool[] PeopleAlly = { true, false, false };
        /// <summary>Corroded: the raider stands RaiderOff cells from where the walk ends, RaiderTurn degrees from the aim.</summary>
        private const float RaiderOff = 2.6f, RaiderTurn = -70f;

        public MapComponent_EgoSolemnLamentPreview(Map map) : base(map) { }

        /// <summary>The burst's plan with the sketch's defaults: how many shots, the cap's hit (or -1), the last hit.</summary>
        private static int BurstPlan(EgoSolemnLamentShot[] into, out float down, out float lastHit)
        {
            int n = EgoSolemnLamentScript.Plan(into, T.ScriptShots, T.Lead, T.Interval, T.ScriptDist, T.WhiteStacks, T.BlackStacks, T.Cap, out down);
            lastHit = down >= 0f ? down : into[n - 1].Hit;
            return n;
        }

        /// <summary>Cells the preview's wielder walks: corroded, to 1 cell short of the ally ScriptDist off; Overclock holds.</summary>
        private static float Walk(EgoSolemnLamentScene scene) => scene == EgoSolemnLamentScene.Overclock ? 0f : Mathf.Max(0f, T.ScriptDist - T.Beside);

        /// <summary>Seconds of cloud the preview shows.</summary>
        private static float Cloud(EgoSolemnLamentScene scene) => scene == EgoSolemnLamentScene.Funeral ? T.ScriptFuneralCloud : T.ScriptCloud;

        /// <summary>The death count: corroded, a downed pawn keeps taking stacks up to it; Overclock stops at the cap (0).</summary>
        private static int Death(EgoSolemnLamentScene scene) => scene == EgoSolemnLamentScene.Overclock ? 0 : Mathf.Max(T.Death, T.Cap + 1);

        /// <summary>The cloud's flight back into the coffin from where the preview's wielder stopped.</summary>
        private static float Home(EgoSolemnLamentScene scene, float aim)
        {
            Vector2 stop = EgoSolemnLamentScript.WielderAt(Vector2.zero, EgoSolemnLamentGraphics.Dir(aim), Walk(scene), Cloud(scene), float.MaxValue);
            return T.FarFly((stop - new Vector2(T.CoffinBackX, T.CoffinBackZ)).magnitude, T.ReturnTime);
        }

        public static float Duration(EgoSolemnLamentScene scene, float aim)
        {
            if (scene != EgoSolemnLamentScene.Burst) return T.CoffinEnd(Cloud(scene), Home(scene, aim), T.ScriptHold);
            BurstPlan(new EgoSolemnLamentShot[T.ScriptShots], out float down, out float lastHit);
            return lastHit + T.ScriptHold + (down < 0f ? 0.3f : 0.4f);
        }

        /// <summary>The sketch's timeline markers.</summary>
        public static (string name, float seconds)[] Phases(EgoSolemnLamentScene scene, float aim)
        {
            var phases = new List<(string, float)>();
            if (scene == EgoSolemnLamentScene.Burst)
            {
                var plan = new EgoSolemnLamentShot[T.ScriptShots];
                int n = BurstPlan(plan, out float down, out float lastHit);
                phases.Add(("Draw", 0f));
                phases.Add(("Shot 1 (white)", T.Lead));
                if (n > 1) phases.Add(("Shot 2 (black)", plan[1].Fire));
                if (down >= 0f) phases.Add(($"Stack {T.Cap}: down", down));
                phases.Add(("Result", lastHit + 0.6f));
                return phases.ToArray();
            }
            phases.Add(("Coffin rises", 0f));
            phases.Add(("Opens", T.Rise));
            if (Walk(scene) > 0f) phases.Add(("Walks", T.WalkFrom));
            float cloud = Cloud(scene);
            if (T.Ticks(cloud, T.CloudTick) > 0) phases.Add(("Stack 1", T.TickAt(1, T.CloudTick)));
            var marks = new EgoSolemnLamentMark[3];
            int count = Build(scene, aim, Vector2.zero, marks, new bool[3], new List<int>(), new List<float>(), new List<float>());
            float firstDown = float.MaxValue, firstDead = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (marks[i].Down) firstDown = Mathf.Min(firstDown, marks[i].DownAt);
                if (marks[i].Dead) firstDead = Mathf.Min(firstDead, marks[i].DeadAt);
            }
            if (firstDown < float.MaxValue) phases.Add(($"Stack {T.Cap}: down", firstDown));
            if (firstDead < float.MaxValue) phases.Add(($"Stack {Death(scene)}: dead", firstDead));
            phases.Add(("Returns", T.CloudEnd(cloud) + 0.1f));
            phases.Add(("Result", T.SinkAt(cloud, Home(scene, aim)) + T.Sink));
            return phases.ToArray();
        }

        public void Play(IntVec3 at, EgoSolemnLamentScene play, float degrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            scene = play;
            aim = degrees;
            seconds = 0f;
            shaken = 0;
            end = Duration(play, degrees);
            Vector2 o = Centre, d = EgoSolemnLamentGraphics.Dir(aim);
            if (play == EgoSolemnLamentScene.Burst)
            {
                shotCount = BurstPlan(shots, out downAt, out last);
                Vector2 home = o + d * T.ScriptDist;
                target = new EgoSolemnLamentMark(home, T.Cap, d.x >= 0f ? 90f : -90f, 1.3f);
                EgoSolemnLamentScript.Fill(target, shots, shotCount, d, downAt);
            }
            else
            {
                walk = Walk(play);
                home = Home(play, degrees);
                peopleCount = Build(play, degrees, o, people, skip, diveSlots, diveTimes, diveFlys);
            }
            active = true;
        }

        /// <summary>
        /// The coffin scenes' people and dives with the wielder's cell at <paramref name="o"/>: corroded, the ally the
        /// walk ends beside and the raider beyond it; Overclock, its three. Returns how many people there are.
        /// </summary>
        private static int Build(EgoSolemnLamentScene play, float aim, Vector2 o, EgoSolemnLamentMark[] people, bool[] skip,
            List<int> diveSlots, List<float> diveTimes, List<float> diveFlys)
        {
            Vector2 d = EgoSolemnLamentGraphics.Dir(aim);
            float walk = Walk(play), cloud = Cloud(play);
            int count;
            if (play == EgoSolemnLamentScene.Overclock)
            {
                count = PeopleTurn.Length;
                for (int i = 0; i < count; i++)
                    Mark(people, skip, i, o + EgoSolemnLamentGraphics.Dir(aim + PeopleTurn[i]) * PeopleDist[i], o, PeopleAlly[i]);
            }
            else
            {
                count = 2;
                Mark(people, skip, 0, o + d * T.ScriptDist, o, false);
                Mark(people, skip, 1, o + d * walk + EgoSolemnLamentGraphics.Dir(aim + RaiderTurn) * RaiderOff, o, false);
            }
            EgoSolemnLamentScript.Dives(people, skip, count, cloud, T.CloudTick, Death(play), t => EgoSolemnLamentScript.WielderAt(o, d, walk, cloud, t),
                new Vector2(o.x + T.CoffinBackX, o.y + T.CoffinBackZ), T.CloudRadius, diveSlots, diveTimes, diveFlys);
            return count;
        }

        /// <summary>Stand-in <paramref name="i"/> at <paramref name="home"/>; it falls away from the wielder's cell; Overclock skips it if <paramref name="skipped"/>.</summary>
        private static void Mark(EgoSolemnLamentMark[] people, bool[] skip, int i, Vector2 home, Vector2 o, bool skipped)
        {
            people[i] = new EgoSolemnLamentMark(home, T.Cap, home.x >= o.x ? 90f : -90f, i * 2.1f);
            skip[i] = skipped;
        }

        private Vector2 Centre
        {
            get
            {
                Vector3 c = cell.ToVector3Shifted();
                return new Vector2(c.x, c.z);
            }
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            if (seconds >= end)
            {
                active = false;
                return;
            }
            float s = seconds;
            Vector2 o = Centre;
            if (scene == EgoSolemnLamentScene.Burst)
            {
                // The sketch's events(): a small shake with each shot.
                while (shaken < shotCount && s >= shots[shaken].Fire)
                {
                    shaken++;
                    Find.CameraDriver.shaker.DoShake(0.012f);
                }
                Vector2 d = EgoSolemnLamentGraphics.Dir(aim);
                float fall = target.Fall(s);
                EgoSolemnLamentButterflies.DrawMark(target, s, target.Now(s), fall, target.FallTurn * fall, T.Span, map);
                EgoSolemnLamentGraphics.DrawBurst(new EgoSolemnLamentBurst
                {
                    Wielder = o - d * EgoSolemnLamentScript.RockBack(shots, shotCount, s), Aim = aim, Target = target.Home,
                    Shots = shots, ShotCount = shotCount, Last = last, DownAt = downAt,
                }, s, map);
            }
            else
            {
                // The sketch's events(): a shake as the coffin stands up.
                if (shaken == 0 && s >= T.Rise)
                {
                    shaken = 1;
                    Find.CameraDriver.shaker.DoShake(0.03f);
                }
                float cloud = Cloud(scene);
                // The funeral's cover flies into the coffin's mouth as it is now.
                EgoSolemnLamentPoint mouth = T.Mouth(new Vector2(o.x + T.CoffinBackX, o.y + T.CoffinBackZ),
                    T.CoffinRise(s, T.SinkAt(cloud, Mathf.Max(T.ReturnTime, home))));
                for (int i = 0; i < peopleCount; i++)
                {
                    float fall = people[i].Fall(s);
                    people[i].Mouth = mouth;
                    EgoSolemnLamentButterflies.DrawMark(people[i], s, people[i].Now(s), fall, people[i].FallTurn * fall, T.Span, map, i * 0.00001f);
                }
                EgoSolemnLamentGraphics.DrawCoffin(new EgoSolemnLamentCoffin
                {
                    Wielder = EgoSolemnLamentScript.WielderAt(o, EgoSolemnLamentGraphics.Dir(aim), walk, cloud, s), Aim = aim, Coffin = o,
                    Radius = T.CloudRadius, Cloud = cloud, Home = home, Face = scene != EgoSolemnLamentScene.Overclock,
                    DiveSlots = diveSlots, DiveTimes = diveTimes, DiveFlys = diveFlys,
                }, s, map);
            }
        }
    }
}
