# Insight Canvas UI testing

Horticulture owns the UI assertions. Insight Canvas supplies rendering, focus, diagnostics,
virtualization, and accessibility primitives; DevBridge2 only coordinates RimWorld readiness.

## Static verification

```powershell
dotnet build .\Source\HorticultureNovelSeeds.csproj --configuration Release
.\DevTools\Verify-InsightCanvasUI.ps1
.\DevTools\Test-ReleasePackage.ps1
```

## In-game verification

The authenticated RimBridge companion's `complete` smoke suite includes startup, UX discovery,
gameplay, Knowledge, automatic-mask, and save/reload assertions. Run it through RimTest from
the repository root:

```powershell
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' run horticulture-in-game-smoke --json
```

The companion also exposes `ux-discovery`, `workspace`-adjacent UI checks, and the other focused
scenario names documented in [RUNTIME_TESTS.md](RUNTIME_TESTS.md); any new focused entry point
must remain a RimTest catalog/DevBridge recipe operation.

During interactive QA, inspect wide and narrow settings windows, compact density, high contrast,
reduced motion, navigation wrapping, Groups/Plants/Traits splits, search filtering, bounded
selections, keyboard focus, confirmation dialogs, toasts, empty states, lineage, comparison,
and the Visual Designer's Plant/Produce mask views. Preview painting must not generate masks or
mutate settings.
