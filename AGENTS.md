# Horticulture Novel Seeds

- Package ID: `lan.horticulture.novelseeds`.
- Production build: `dotnet build Source\HorticultureNovelSeeds.csproj --configuration Release`.
- RimTest catalog: `TestCatalog\rimtest.catalog.json`; in-game validation runs through the authenticated RimBridge companion.
- Structural validation: `DevTools\Test-ReleasePackage.ps1` plus the focused `DevTools\Verify-*.ps1` scripts.
- In-game tests: `C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd run horticulture-in-game-smoke --json`.
- Companion build: `dotnet build DevTools\BridgeTools\HorticultureNovelSeeds.BridgeTools.csproj --configuration Release`.
- DevBridge2 is a process/readiness coordinator only. Horticulture owns and executes all Horticulture-specific tests.
- Never launch, kill, or restart RimWorld directly. Use `C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd` for status, leases, restart, and readiness.
- Gameplay, Defs, Harmony, serialized types, core, or companion changes require a DevBridge2 restart before in-game tests.
- Full testing workflow: `docs\TESTING.md` and `docs\RUNTIME_TESTS.md`.
