# Release-candidate test plan

All Horticulture assertions and temporary live-game fixtures belong to the RimBridge companion.
RimTest selects and aggregates results; DevBridge2 owns project profiles, lifecycle, readiness,
leases, and authenticated RimBridgeServer routing.

## Automated checks

```powershell
dotnet build .\Source\HorticultureNovelSeeds.csproj --configuration Release
dotnet build .\DevTools\BridgeTools\HorticultureNovelSeeds.BridgeTools.csproj --configuration Release
.\DevTools\Verify-RimBridgeTesting.ps1
.\DevTools\Build-ReleasePackage.ps1
```

Run the authoritative in-game smoke workflow from the repository root:

```powershell
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' doctor --json
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' affected --run --json
```

The `smoke` fallback is non-empty and runs `complete`, which covers startup, UX discovery,
ordinary crops, sowable trees, cross-pollination, produce processing, Knowledge, negative paths,
long-running cache use, automatic-mask safety, and save/reload. A conservative affected result
must use that fallback; zero selected tests is not a pass.

## Manual matrix

- Open settings on a clean install; verify compact defaults, advanced disclosure, and Save Seeds.
- Exercise naming cancel/X, registry empty/knowledge-gated/lineage/comparison states, and large lists.
- Check vanilla crops and trees plus one conventional modded plant.
- Save, reload, reopen the registry, and inspect cultivar, plant, produce, and Knowledge state.
- Confirm no new Horticulture errors or warnings appear in the live-game evidence.
