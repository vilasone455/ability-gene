using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Runs the <see cref="RimArtTestAttribute"/> tests when the game is started with
    /// <c>-quicktest -rimarttest=&lt;filter&gt;</c>, writes the results file, and quits the game.
    ///
    /// Results go to <c>&lt;save data folder&gt;/RimArtTests/results.txt</c> (on the Mac
    /// ~/Library/Application Support/RimWorld/RimArtTests), written line by line so a crash still
    /// leaves the trace. The last line is <c>DONE PASS n/n</c> or <c>DONE FAIL k/n</c>. Screenshots
    /// go to the same folder. Without the argument this component does nothing.
    ///
    /// Each test is stepped from GameComponentTick, so its waits are exact game ticks; a screenshot
    /// pauses the game and is taken from GameComponentUpdate a few frames later.
    /// </summary>
    public class GameComponent_RimArtTests : GameComponent
    {
        public GameComponent_RimArtTests(Game game) { }

        private static RimArtTestRunner runner;

        public override void GameComponentUpdate()
        {
            if (runner == null)
            {
                if (!GenCommandLine.TryGetCommandLineArg("rimarttest", out string filter)) return;
                if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
                runner = new RimArtTestRunner(filter);
            }
            runner.Update();
        }

        public override void GameComponentTick() => runner?.Tick();
    }

    internal class RimArtTestRunner
    {
        private readonly List<(RimArtTestAttribute test, MethodInfo method)> tests;
        private readonly string folder;
        private readonly StreamWriter results;
        private int index = -1, passed;
        private bool finished;

        private RimArtTestContext context;
        private IEnumerator<int> steps;
        private int waitUntil, startedTick;
        private readonly List<string> errors = new List<string>();

        private string pendingShot;
        private int shotFrames;
        private TimeSpeed speed = TimeSpeed.Superfast;

        public RimArtTestRunner(string filter)
        {
            folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimArtTests");
            Directory.CreateDirectory(folder);
            foreach (string old in Directory.GetFiles(folder)) File.Delete(old);
            results = new StreamWriter(Path.Combine(folder, "results.txt")) { AutoFlush = true };

            bool all = string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase);
            tests = GenTypes.AllTypes
                .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Select(m => (test: m.GetCustomAttribute<RimArtTestAttribute>(), method: m))
                .Where(x => x.test != null && (all || x.test.FullLabel.StartsWith(filter, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.test.FullLabel)
                .ToList();
            Write("RimArt tests, filter \"" + filter + "\": " + tests.Count + " found");
            Application.logMessageReceived += OnLog;
            Prefs.DevMode = true;
            Find.TickManager.CurTimeSpeed = speed;
            Next();
        }

        internal void Write(string line) => results.WriteLine(line);

        /// <summary>Melee Animation's own diagnostics (a pawn in one of its animations got a job it did not expect), logged as errors.</summary>
        private const string MeleeAnimationTag = "<color=#66ffb5>[MeleeAnim]</color>";

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (steps == null || (type != LogType.Error && type != LogType.Exception)) return;
            string line = message.Split('\n')[0];
            // Written down, not failed: raiders grapple and duel with Melee Animation in any fight.
            if (line.StartsWith(MeleeAnimationTag)) Write("    note: " + line);
            else errors.Add(line);
        }

        private void Next()
        {
            steps = null;
            index++;
            if (index >= tests.Count)
            {
                Finish();
                return;
            }
            var (test, method) = tests[index];
            Write("");
            Write("TEST " + test.FullLabel);
            errors.Clear();
            context = new RimArtTestContext(this, Find.CurrentMap);
            startedTick = Find.TickManager.TicksGame;
            try
            {
                steps = ((IEnumerable<int>)method.Invoke(null, new object[] { context })).GetEnumerator();
            }
            catch (Exception e)
            {
                End("threw: " + (e.InnerException ?? e));
                return;
            }
            waitUntil = startedTick;
        }

        public void Tick()
        {
            if (steps == null || pendingShot != null) return;
            int now = Find.TickManager.TicksGame;
            if (now - startedTick > tests[index].test.timeoutTicks)
            {
                End("timed out after " + tests[index].test.timeoutTicks + " ticks");
                return;
            }
            while (steps != null && pendingShot == null && now >= waitUntil)
            {
                bool more;
                try
                {
                    more = steps.MoveNext();
                }
                catch (Exception e)
                {
                    End("threw: " + e);
                    return;
                }
                if (!more)
                {
                    End(null);
                    return;
                }
                if (steps.Current == RimArtTestContext.Shot)
                {
                    pendingShot = context.shotName ?? ("shot" + now);
                    context.shotName = null;
                    speed = Find.TickManager.CurTimeSpeed;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    Find.CameraDriver.SetRootPosAndSize(context.center.ToVector3Shifted(), 10f);
                    shotFrames = 0;
                }
                else waitUntil = now + Math.Max(0, steps.Current);
            }
        }

        public void Update()
        {
            if (finished) return;
            if (pendingShot != null)
            {
                // Two frames for the camera to settle, capture, two frames for the file to be written.
                shotFrames++;
                if (shotFrames == 3)
                {
                    string file = (index + 1).ToString("00") + "-" + pendingShot + ".png";
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, file));
                    Write("    shot " + file);
                }
                else if (shotFrames >= 6)
                {
                    pendingShot = null;
                    Find.TickManager.CurTimeSpeed = speed;
                }
                return;
            }
            // Letters and dialogs can pause the game; keep it running while a test is.
            if (steps != null && Find.TickManager.Paused) Find.TickManager.CurTimeSpeed = speed;
            Find.WindowStack.TryRemove(typeof(Dialog_MessageBox), false);
        }

        private void End(string problem)
        {
            steps = null;
            if (problem != null) context.failures.Add(problem);
            foreach (string error in errors) context.failures.Add("error logged: " + error);
            int ticks = Find.TickManager.TicksGame - startedTick;
            if (context.failures.Count == 0)
            {
                passed++;
                Write("PASS " + tests[index].test.FullLabel + " (" + ticks + " ticks)");
            }
            else
            {
                Write("FAIL " + tests[index].test.FullLabel + " (" + ticks + " ticks)");
                foreach (string f in context.failures) Write("  - " + f);
            }
            Next();
        }

        private void Finish()
        {
            finished = true;
            Application.logMessageReceived -= OnLog;
            Write("");
            Write("DONE " + (passed == tests.Count ? "PASS" : "FAIL") + " " + passed + "/" + tests.Count);
            results.Close();
            if (!GenCommandLine.CommandLineArgPassed("rimarttest-stay")) Root.Shutdown();
        }
    }
}
