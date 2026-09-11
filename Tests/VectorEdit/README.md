# Vector manipulation verification

Run from the repository root:

```sh
dotnet build Source/RimArt/RimArt.csproj --no-restore
dotnet run --project Tests/VectorEdit/VectorEdit.csproj
dotnet run --project Tests/OriginBlade/ApiChecks/ApiChecks.csproj
python3 validate.py
```

The harness links the production `CapturedProjectile`, `VectorEditGroup`, `VectorEditRegistry`
and `VectorEditDefaults` against small game boundary doubles. The field names on the stub
`Projectile` are load-bearing: the production code reaches them through Harmony field refs by
name, exactly as it does against the real engine.

It checks the range table (5 / 10 / 20 / 40 cells), that force scales speed so a doubled round
covers twice the distance in the same time, that rotation is applied to each round's captured
heading and preserves a volley's angular spread, that rounds are turned where they stand rather
than moved, that travel is clipped at the map edge and the tick count is clipped with it, that
force is absolute so re-editing at ×2 stays ×2, what does and does not count as an edit,
that commit writes the flight counters and credits the manipulating pawn, that ×1 leaves no
registry entry, the strain cost table, and that group membership is exclusive and releasable.

It does not simulate the editor window, map input, Harmony patch execution, the tick-rate
slow, pause handling, strain decay, or save/load.

## In-game acceptance checks

1. Install a reflex booster. Confirm it grants three gizmos - **reflex surge**, **vector
   manipulation** and **vector shove** - and that manipulation has no targeting cursor.
2. Press **reflex surge**. Confirm the whole world drops to a quarter speed and still moves
   smoothly rather than stuttering, that pressing Fast or Superfast cannot speed it back up,
   that spacebar still pauses, that the hediff's **End surge** gizmo returns it to normal
   immediately, and that it expires on its own after 5 seconds of game time.
3. During a surge, with rounds in the air within twelve cells, click **vector manipulation**.
   The game must pause in the same frame and the panel must appear, with a message naming how
   many rounds were caught. Confirm the reach ring, the enlarged markers, the original heading
   stubs and the drag box all draw, and that dragging over the map selects rounds instead of
   selecting pawns or issuing orders. Clicking with nothing in reach must print a message,
   start no cooldown, and not pause.
4. Build four groups. Confirm a fifth drag is refused, shift-drag adds to the selected group,
   alt-drag removes from it, right-click deselects, Delete and **Release** free a group's rounds
   for reselection, and no round is ever in two groups.
5. Turn a group with the on-map handle and with the slider; confirm both agree and that the
   preview fan keeps its spread. Step through all four forces and confirm the preview line
   length and the endpoint marker match the range table.
6. Press **Apply and resume**. Confirm every group commits together, the rounds fly the
   previewed lines at the previewed speeds, the clock returns to the speed it was at, and the
   and any surge still running is untouched. Activate while already paused and confirm Apply
   resumes at normal speed instead.
7. Cancel and press Escape; both must discard the draft and leave the pause state as it was
   found. Confirm Apply is disabled when nothing would change.
8. Watch the strain readout: current, added, projected, and the collapse warning. Apply a
   four-group edit and confirm the cost and the overload — the carrier goes down and stands up
   again as strain sheds.
9. Confirm exclusions: mortar shells and rockets are never caught, nor rounds held by a phase
   barrier or frozen in a stasis field, nor anything behind a wall or in fog.
10. Save during an edited round's flight and reload. The round must still be flying at its
   edited speed and land for its edited damage.
11. Load a save made with the previous Reflex Booster. The new command must appear, the old
    reflection hediff must be gone, the carrier must be able to move and be tended, and any
    inherited cooldown must be at most five seconds.
