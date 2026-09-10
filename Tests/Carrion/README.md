# Carrion lifecycle checks

Run `dotnet run --project Tests/Carrion/Carrion.csproj` with the .NET 8 SDK.

This executable compiles the production `CarrionRun` and `MapComponent_Carrion` sources against small game-boundary stubs. It checks delayed and single recovery, healing limits, corpse-size scaling, cancellation, charge spending, corpse claims, and saving/loading the run's fields before and after consumption. It does not simulate Unity rendering, RimWorld's serializer/reference resolution, or the ability targeting UI.

In RimWorld, verify:

- An existing dispersal carrier has the Carrion command after loading the updated mod.
- Target a fresh human or animal corpse within 10 cells. The pawn stays visible; crows fly out, peck the corpse, destroy it, and return before health changes.
- Rotten, dessicated and mech corpses, fogged targets and targets beyond range are rejected.
- Another caster cannot claim the same corpse; the first caster cannot launch a second flock.
- Save and reload during outbound travel, feeding and return. The body is consumed once and recovery arrives once.
- Haul or destroy the corpse while feeding. No recovery occurs and the command becomes available again.
- Move the caster during the return flight, and use Murder while a flock is away. The crows return to the caster's current position after landing.
- Check a wounded pawn and a Hemogen carrier. Scars, missing parts, disease and nonbleeding injuries remain unchanged; blood loss and Hemogen never exceed their bounds.
