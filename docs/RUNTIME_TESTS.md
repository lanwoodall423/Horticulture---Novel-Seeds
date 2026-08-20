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
`cross-pollination`, `produce-processing`, `knowledge`, `authority`, `negative`, `long-running`,
`auto-mask-suite`, and `save-reload`. The catalog smoke recipe runs `complete`; its `knowledge`
phase covers the existing event/registration path, while its `authority` phase is the focused
authority-boundary equivalent of the former workspace/knowledge checks. Focused scenario
selection remains a companion/recipe change and must stay behind the same authenticated
RimTest/DevBridge workflow.

The `knowledge` phase asserts, on the live game thread:

- a new document exposes Overview only until evidence or an explicit route exists;
- explicit plant routing adds only the Plants page and Compare never becomes persistent navigation;
- cultivar traits are unknown before `CultivarDocumented` and become visible only from the
  documented cultivar claim;
- lineage uses the registered Knowledge relation and never renders a raw parent identifier; and
- claim-backed presentation remains available as an honest semantic unknown when a fact is not
  authorized.

The `authority` phase adds the ten minimum disclosure checks:

- fresh Overview-only navigation with no pre-snapshot plant catalog or advanced side-channel filters;
- first sow/germination/growth evidence making Plants relevant through bounded projections;
- high species knowledge remaining separate from an unclaimed cultivar;
- precise documented trait identity without fabricated aggregate modifiers;
- Progression: Agriculture capability remaining separate from biological Knowledge;
- hidden-trait search and comparison refusing unauthorized classification or differences;
- serialized breeding intent remaining visible while raw matching stays unknown;
- semantic authorized lineage with bounded deterministic cycle/missing-parent diagnostics;
- conservative gameplay and presentation behavior on the unavailable-Knowledge path; and
- runtime breeding-page removal repairing active page, selection, focus, IDs, and diagnostics.

Fixtures are created on the live quicktest map and cleaned with vanish semantics. Save/reload
uses RimBridgeServer's authenticated save/load tools. Evidence and failure identity are returned
through the DevBridge recipe result; no repository request, checkpoint, or result files are used.

If `affected --run` reports a conservative selection, it must execute the configured non-empty
`smoke` fallback. A zero-test execution is not validation.
