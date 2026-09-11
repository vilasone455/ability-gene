# Origin: Blade verification

Run from the repository root:

```sh
dotnet build Source/RimArt/RimArt.csproj --no-restore
dotnet run --project Tests/OriginBlade/OriginBlade.csproj
dotnet run --project Tests/OriginBlade/ApiChecks/ApiChecks.csproj
python3 validate.py
```

The behavior harness links the production study, offer, awakening and restriction code
against small game boundary doubles. It checks type eligibility, duplicate studies, individual
progress, skill thresholds, delayed awakening, permanence after skill loss, serialization
fields, and removal of only psycasts, psylinks, and ranged equipment. It also covers the
consent model: that completing the checklist offers rather than grants, that nothing is taken
before the player accepts, that the offer is made once rather than every tick, and that
declining leaves the pawn awakenable through the command instead. It does not simulate
the game's job scheduler, full save loader, UI, or Harmony patch execution.

The API checks load the built mod and the installed RimWorld assembly, resolve each new
Harmony target and injected parameter, and check the trait's generation settings, complete
ability kit, and XML class references. Override `RimWorldManaged` if the game's managed
assemblies live elsewhere. Harmony is read from the package cache used by the main build.

## In-game acceptance checks

1. Add this version to an existing save. Select an undrafted colonist and inspect the
   **Origin: Blade** command. Confirm requirements, progress, and the permanent warning.
2. Right-click an accessible knife and start **Study blade**. Confirm the warning appears
   before starting, the job takes one game hour, and the weapon is not consumed. Repeat
   with a different material/quality knife: it must say the type is already understood.
3. Interrupt a study, reserve its target with another pawn, forbid it during study, or
   destroy it. None should award completion. Save/load midway and verify the active job
   and previously completed types behave correctly.
4. Complete knife, ikwa, spear, gladius, and longsword. With either skill below 14 Melee /
   12 Crafting, no offer appears. Raise the missing skill; the awakening letter should arrive
   within 250 game ticks, and nothing should change until it is answered. Decline it: the
   command becomes **Awaken: Blade** and still works. Accept from either route, then lower the
   skills afterward -- the trait and kit remain.
5. Awaken a pawn holding a gun with a psylink and learned vanilla psycasts. Confirm the
   gun lands intact, psylinks and psycasts disappear, other abilities/hediffs remain,
   psychic sensitivity stays the same, and the awakening letter appears once.
6. Attempt guns, bows, launchers, psytrainers, and psylink neuroformers. Equipping/using
   them must be rejected with a reason before consuming an item. Melee weapons still work.
7. Exercise Rain -> Loose -> Grasp. The trait supplies all three, with their existing
   cooldowns, bodily debt, and permanent longsword output. Save/load and repeat.
8. Repeat without Royalty: the study and trait should work without DLC-related load errors.
   Check generated starting pawns and later arrivals never roll the trait randomly.

Vanilla Psycasts Expanded uses a separate ability system and is not covered by these checks.
