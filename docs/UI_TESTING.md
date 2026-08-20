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

The companion's `knowledge` and `authority` scenarios include the workspace authority checks.
`authority` covers the ten required boundaries: overview-only startup, first evidence,
species/cultivar separation, documentation precision, Progression capability, hidden filters
and comparison, breeding intent, lineage bounds, unavailable Knowledge, and runtime page
removal with focus/selection/diagnostic repair. Other focused scenario names are documented in
[RUNTIME_TESTS.md](RUNTIME_TESTS.md); any new focused entry point must remain a RimTest
catalog/DevBridge recipe operation.

The UI authority matrix is intentionally small:

| Surface | Allowed source | Unknown behavior |
| --- | --- | --- |
| Plants | Relevant records, explicit route, species claims/facets, progression availability | No full DefDatabase catalog; no raw growth/yield fallback |
| Cultivars | Real/explicit records, cultivar claims/facets, disclosed relations | No species-rank inheritance; no raw trait, product, origin, generation, or parent fallback |
| Breeding | Serialized read-only program plus authorized cultivar claims | Known matches plus “additional matches unknown”; never raw `Matches` totals |
| Compare | Selected stable IDs and authorized per-field values | Unknown fields are omitted from differences |
| Lineage | `parent_lineage` claim and revealed structural relations | Semantic “Unknown parent”; raw IDs never become labels |

During interactive QA, inspect wide and narrow settings windows, compact density, high contrast,
reduced motion, navigation wrapping, Groups/Plants/Traits splits, search filtering, bounded
selections, keyboard focus, confirmation dialogs, toasts, empty states, lineage, comparison,
and the Visual Designer's Plant/Produce mask views. Preview painting must not generate masks or
mutate settings.
