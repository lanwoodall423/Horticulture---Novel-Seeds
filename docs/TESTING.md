# Horticulture testing

Use the local RimTest installation as the workflow entry point. Run commands from the mod root;
the launcher path is only the command location.

## Structural validation

```powershell
dotnet build .\Source\HorticultureNovelSeeds.csproj --configuration Release
dotnet build .\DevTools\BridgeTools\HorticultureNovelSeeds.BridgeTools.csproj --configuration Release
.\DevTools\Test-ReleasePackage.ps1
Get-ChildItem .\DevTools\Verify-*.ps1 | ForEach-Object { & $_.FullName }
```

The bridge companion is a development-only assembly deployed beside RimWorld's global
`BridgeTools` directory. It is never copied into the release package.

## In-game validation

```powershell
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' doctor --json
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' validate --json
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' affected --run --json
```

The repository catalog maps the smoke test to the `horticulture-in-game-suite` DevBridge2 v2
recipe. RimTest delegates execution; DevBridge2 owns the exact profile, restart/readiness
boundary, lease, and authenticated RimBridgeServer call. Horticulture owns the assertions and
temporary fixtures in `DevTools\BridgeTools\HorticultureBridgeTools.cs`.

Use `DevTools\Run-RimBridgeTests.ps1` for an explicit smoke run after building the companion:

```powershell
.\DevTools\Run-RimBridgeTests.ps1 -BuildCompanion
```

After gameplay, Def, Harmony, serialized-type, core, or companion changes, use DevBridge2's
coordinated restart/readiness flow before in-game validation. Never launch RimWorld directly,
edit `ModsConfig`, or use a second in-game test harness.

See [RUNTIME_TESTS.md](RUNTIME_TESTS.md) for the companion boundary, the `knowledge` and
`authority` coverage, and the full scenario list.
