# RimTest in-game validation

Horticulture owns the gameplay assertions in the `HorticultureSuite` RimBridge companion.
RimTest selects and runs the suite; DevBridge2 owns project registration, lifecycle, readiness,
generation identity, and leases. The production DLL contains no test `GameComponent`, Harmony
bootstrap, request-directory transport, or test result writer.

## Workflow

Run these commands from the Horticulture repository root:

```powershell
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' doctor --json
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' validate --json
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' run horticulture-in-game-smoke --json
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' affected --run --json
```

The optional wrapper `DevTools\Run-RimBridgeTests.ps1` delegates to the same RimTest catalog;
it does not control RimWorld or acquire a second lease. Build the companion first when its
source changes:

```powershell
dotnet build .\Source\HorticultureNovelSeeds.csproj --configuration Release
dotnet build .\DevTools\BridgeTools\HorticultureNovelSeeds.BridgeTools.csproj --configuration Release
.\DevTools\Run-RimBridgeTests.ps1
```

## Suite scenarios

The companion supports `complete`, `startup`, `ux-discovery`, `ordinary-crop`, `sowable-tree`,
`cross-pollination`, `produce-processing`, `knowledge`, `negative`, `long-running`,
`auto-mask-suite`, and `save-reload`. The catalog smoke recipe runs `complete`; focused
scenario selection remains a companion/recipe change and must stay behind the same authenticated
RimTest/DevBridge workflow.

Fixtures are created on the live quicktest map and cleaned with vanish semantics. Save/reload
uses RimBridgeServer's authenticated save/load tools. Evidence and failure identity are returned
through the DevBridge recipe result; no repository request, checkpoint, or result files are used.

If `affected --run` reports a conservative selection, it must execute the configured non-empty
`smoke` fallback. A zero-test execution is not validation.
