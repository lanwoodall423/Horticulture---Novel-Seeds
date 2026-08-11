# Horticulture Novel Seeds

- Package ID: `lan.horticulture.novelseeds`.
- Production build: `dotnet build Source\HorticultureNovelSeeds.csproj --configuration Release`.
- Structural validation: `DevTools\Test-ReleasePackage.ps1` plus the focused `DevTools\Verify-*.ps1` scripts.
- Runtime tests: `DevTools\Run-RuntimeTests.ps1`; the test implementation is built from `DevTools\RuntimeTests` and is not part of the Release DLL.
- DevBridge2 is a process/readiness coordinator only. Horticulture owns and executes all Horticulture-specific tests.
- Never launch, kill, or restart RimWorld directly. Use `C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd` for status, leases, restart, and readiness.
- Gameplay, Defs, Harmony, serialized types, or core changes require a DevBridge2 restart before runtime tests.
- Full testing workflow: `docs\TESTING.md` and `docs\RUNTIME_TESTS.md`.
