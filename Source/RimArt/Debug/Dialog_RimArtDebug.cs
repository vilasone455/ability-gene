using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The mod's own debug window: every [RimArtDebug] method, filed by kit, behind one line in
    /// the game's debug menu. Modelled on Melee Animation's Dialog_AnimationDebugger: it stays
    /// open, drags, resizes, and leaves the camera and the map alone, so an entry can be run
    /// again and again without going back through the menu.
    ///
    /// A Cell or Pawn entry arms one of the game's own debug tools, the same DebugTool a ToolMap
    /// [DebugAction] makes: left click uses it, right click puts it away.
    /// </summary>
    public sealed class Dialog_RimArtDebug : Window
    {
        private sealed class Entry
        {
            public RimArtDebugAttribute info;
            public Action run;
            public Action<Pawn> runOnPawn;
        }

        private const float KitColumn = 170f, RowHeight = 28f, Gap = 4f;

        private static List<Entry> entries;
        private static List<string> kits;
        // Kept between openings, so the window comes back where it was left.
        private static string selectedKit, search = "";

        private Vector2 kitScroll, entryScroll;

        public override Vector2 InitialSize => new Vector2(560f, 520f);

        [DebugAction("RimArts", "Open debug window", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Open()
        {
            if (!Find.WindowStack.IsOpen<Dialog_RimArtDebug>()) Find.WindowStack.Add(new Dialog_RimArtDebug());
        }

        public Dialog_RimArtDebug()
        {
            optionalTitle = "RimArts debug";
            doCloseX = true;
            draggable = true;
            resizeable = true;
            preventCameraMotion = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            absorbInputAroundWindow = false;
            onlyOneOfTypeAllowed = true;
            drawInScreenshotMode = false;
            if (entries == null) Collect();
        }

        private static void Collect()
        {
            const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            entries = new List<Entry>();
            foreach (MethodInfo method in typeof(Dialog_RimArtDebug).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(anyStatic)).OrderBy(method => method.MetadataToken))
            {
                var info = method.GetCustomAttribute<RimArtDebugAttribute>();
                if (info == null) continue;
                var entry = new Entry { info = info };
                if (info.kind == RimArtDebugKind.Pawn)
                    entry.runOnPawn = (Action<Pawn>)Delegate.CreateDelegate(typeof(Action<Pawn>), method);
                else
                    entry.run = (Action)Delegate.CreateDelegate(typeof(Action), method);
                entries.Add(entry);
            }
            kits = entries.Select(entry => entry.info.kit).Distinct().OrderBy(kit => kit).ToList();
            if (selectedKit == null || !kits.Contains(selectedKit)) selectedKit = kits.FirstOrDefault();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            var top = new Rect(inRect.x, inRect.y, inRect.width, RowHeight);
            DoSearch(top);

            var body = new Rect(inRect.x, top.yMax + Gap * 2f, inRect.width, inRect.height - top.height - Gap * 2f);
            bool searching = !search.NullOrEmpty();
            if (!searching) DoKits(new Rect(body.x, body.y, KitColumn, body.height));
            float left = searching ? 0f : KitColumn + Gap * 2f;
            DoEntries(new Rect(body.x + left, body.y, body.width - left, body.height), searching);
        }

        private static void DoSearch(Rect rect)
        {
            DebugTool armed = DebugTools.curTool;
            float stop = armed != null ? 110f : 0f;
            search = Widgets.TextField(new Rect(rect.x, rect.y, rect.width - stop - (stop > 0f ? Gap : 0f), rect.height), search ?? "");
            if (search.NullOrEmpty())
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x + 6f, rect.y, rect.width, rect.height), "Search every kit...");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            if (armed != null && Widgets.ButtonText(new Rect(rect.xMax - stop, rect.y, stop, rect.height), "Put tool away"))
                DebugTools.curTool = null;
        }

        private void DoKits(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, kits.Count * (RowHeight + Gap));
            Widgets.BeginScrollView(rect, ref kitScroll, view);
            float y = 0f;
            foreach (string kit in kits)
            {
                var row = new Rect(0f, y, view.width, RowHeight);
                if (kit == selectedKit) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + 6f, row.y, row.width - 36f, row.height), kit);
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(row.x, row.y, row.width - 6f, row.height),
                    entries.Count(entry => entry.info.kit == kit).ToString());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(row))
                {
                    selectedKit = kit;
                    entryScroll = Vector2.zero;
                }
                y += RowHeight + Gap;
            }
            Widgets.EndScrollView();
        }

        private void DoEntries(Rect rect, bool searching)
        {
            List<Entry> shown = entries.Where(entry => searching
                ? entry.info.FullLabel.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                : entry.info.kit == selectedKit).ToList();
            var view = new Rect(0f, 0f, rect.width - 16f, shown.Count * (RowHeight + Gap));
            Widgets.BeginScrollView(rect, ref entryScroll, view);
            float y = 0f;
            foreach (Entry entry in shown)
            {
                var row = new Rect(0f, y, view.width, RowHeight);
                if (Widgets.ButtonText(row, ""))
                    Run(entry);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + 8f, row.y, row.width - 70f, row.height),
                    searching ? entry.info.FullLabel : entry.info.label.CapitalizeFirst());
                // What the entry waits for once pressed.
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(row.x, row.y, row.width - 8f, row.height),
                    entry.info.kind == RimArtDebugKind.Pawn ? "click a pawn"
                    : entry.info.kind == RimArtDebugKind.Cell ? "click a cell" : "");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                y += RowHeight + Gap;
            }
            Widgets.EndScrollView();
        }

        private static void Run(Entry entry)
        {
            switch (entry.info.kind)
            {
                case RimArtDebugKind.Now:
                    entry.run();
                    break;
                case RimArtDebugKind.Cell:
                    DebugTools.curTool = new DebugTool(entry.info.FullLabel, entry.run);
                    break;
                default:
                    // As DebugActionNode does for ToolMapForPawns: once for each pawn in the clicked cell.
                    DebugTools.curTool = new DebugTool(entry.info.FullLabel, () =>
                    {
                        Map map = Find.CurrentMap;
                        if (map == null || !UI.MouseCell().InBounds(map)) return;
                        foreach (Pawn pawn in UI.MouseCell().GetThingList(map).OfType<Pawn>().ToList())
                            entry.runOnPawn(pawn);
                    });
                    break;
            }
        }
    }
}
