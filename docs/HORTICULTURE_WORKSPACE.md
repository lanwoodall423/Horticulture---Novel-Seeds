# Horticulture field-guide workspace

The Cultivar Registry MainTab is an Insight Canvas workspace for browsing Horticulture data.
`MainTabWindow_CultivarRegistry` is deliberately a lifecycle shell. Persistent gameplay data
continues to belong to `GameComponent_NovelSeeds`; `HorticultureWorkspaceDocument` owns only the
presentation document, transient selections, filters, snapshots, and overlay lifecycle.

## Pages

| Page | Purpose |
| --- | --- |
| Overview | Actionable entry points and empty-safe counts for plants, cultivars, breeding, and Knowledge. |
| Plants | Growable plant field guide with search, discovery filter, masking, availability, and rank-gated facts. |
| Cultivars | Searchable collection with archive, favorite, origin, balance, and produce-effect filters; inspector actions and contextual Compare. |
| Breeding | Faithful read-only presentation of serialized `BreedingProgramRecord` values, desired roots, matches, notifications, and active state. |
| Knowledge | Adapter-backed personal/colonist and colony scope, evidence, stage, confidence, and unavailable/incompatible guidance. |

Compare is opened from the Cultivars collection after selecting at least two records. It accepts
at most eight stable cultivar IDs, repairs removed IDs during refresh, and leaves unknown
evidence visible as unknown rather than guessing. It delegates structured comparison to
`HorticultureKnowledgeAdapter` when that optional Knowledge capability is available.

## Data and lifecycle boundaries

The document is created once per MainTab window lifecycle. `PreOpen` and the pre-frame refresh
perform selection repair, map availability scans, Knowledge menu/snapshot reads, comparison
queries, breeding summaries, and lineage graph construction. `DoWindowContents` only applies
responsive layout and draws the already-composed host. `PostClose` forwards to
`InsightUiHost.PostClose` to release focus, transient overlays, and owner scope.

The workspace never creates or deletes breeding programs and does not alter cultivar mechanics.
Rename, favorite, archive, and locate actions write through the existing gameplay objects and
preserve stable IDs, discovery metadata, parent IDs, and save keys. Breeding records remain
load-only legacy data. Knowledge is read through the Horticulture adapter; normal UI text does
not expose framework IDs, API generations, migration details, or compatibility internals.

## Bounded presentation

Plants, cultivars, breeding programs, Knowledge rows, and lineage navigation use Insight Canvas
`VirtualList` elements with explicit overscan and cache limits. Repeated rows use deterministic
`Scope` IDs. Trait chips are semantic badges and are hidden or replaced by an explicit unknown
state until the selected Knowledge rank permits trait disclosure. Wide layouts use horizontal
list/inspector splits; below 820px they become vertical. High contrast, reduced motion, and
compact density are document-local settings.

Lineage uses only existing `parentVarietyIds`. `InsightIds.Stable` produces deterministic node
IDs, missing parents are explicit informational nodes, cycles are path/expansion protected, and
`InsightModel.Validate` diagnostics are retained. Traversal is capped at 128 nodes, 256 edges,
and 12 useful levels before `InsightGraphLayout.Compute` runs. Known nodes can navigate back to
their cultivar inspector; missing-parent nodes are not actionable.

## Validation

Static checks:

```powershell
dotnet build .\Source\HorticultureNovelSeeds.csproj --configuration Release
.\DevTools\Verify-InsightCanvasUI.ps1
.\DevTools\Verify-KnowledgeCultivarUI.ps1
.\DevTools\Test-ReleasePackage.ps1
```

Runtime UI and lifecycle checks are owned by Horticulture and executed by the RimTest smoke
recipe through DevBridge2:

```powershell
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd' run horticulture-in-game-smoke --json
```

The complete companion suite checks startup, UX discovery, Knowledge, gameplay, automatic masks,
and save/reload on the real quicktest map. Focused workspace assertions remain companion-owned
and must be exposed through a RimTest catalog/DevBridge recipe rather than a second runner.
