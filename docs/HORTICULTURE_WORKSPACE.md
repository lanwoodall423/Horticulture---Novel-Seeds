# Horticulture field-guide workspace

The Cultivar Registry MainTab is an Insight Canvas workspace for browsing Horticulture data.
`MainTabWindow_CultivarRegistry` is deliberately a lifecycle shell. Persistent gameplay data
continues to belong to `GameComponent_NovelSeeds`; `HorticultureWorkspaceDocument` owns only the
presentation document, transient selections, filters, snapshots, and overlay lifecycle.

## Pages

| Page | Purpose |
| --- | --- |
| Overview | Always available; exposes small actionable signals and entry points without internal counts or diagnostics. |
| Plants | Relevant/evidenced plants or an explicitly opened plant only. Unsupported definitions are not catalogued. |
| Cultivars | Real or explicitly opened cultivar records, with identity-scoped claims, archive/favorite actions, and contextual Compare. |
| Breeding | Faithful read-only presentation of serialized `BreedingProgramRecord` values; hidden-trait match totals remain unknown. |
| Knowledge | Adapter-backed personal/colonial scope, evidence, stage, confidence, and unavailable/incompatible guidance. |

Navigation is progressive. Overview is the baseline; Plants, Cultivars, Breeding, and Knowledge
are composed only when relevant state exists or an external `OpenPlant`, `OpenCultivar`,
`OpenLineage`, or `OpenKnowledge` route explicitly requests them. Compare is opened from the
Cultivars collection after selecting at least two records, accepts at most eight stable cultivar
IDs, repairs removed IDs during refresh, and never treats unknown values as differences.

## Data and lifecycle boundaries

The document is created once per MainTab window lifecycle. `PreOpen` and the pre-frame refresh
perform selection repair, bounded map availability scans, Knowledge menu/claim snapshot reads,
comparison queries, breeding summaries, and lineage graph construction. `DoWindowContents` only
applies responsive layout and draws the already-composed host. `PostClose` forwards to
`InsightUiHost.PostClose` to release focus, transient overlays, and owner scope.

`HorticulturePresentationPolicy` is a stateless read-only projection layer. It distinguishes
technological availability from biological knowledge, reads species and cultivar subjects
separately, and resolves each displayed fact from its own claim/facet/relation. The policy has no
fallback from unavailable Knowledge Framework data to raw `VarietyRecord` or `ThingDef` values.
`HorticultureKnowledgeSnapshots` bounds repeated facet, subject, and claim reads by framework
revision.

The workspace never creates or deletes breeding programs and does not alter cultivar mechanics.
Rename, favorite, archive, and locate actions write through the existing gameplay objects and
preserve stable IDs, discovery metadata, parent IDs, and save keys. Breeding records remain
load-only legacy data. Progression Agriculture still owns crop unlocks; an unlocked crop does
not imply a Knowledge claim. Knowledge is read through the Horticulture adapter; normal UI text
does not expose framework IDs, API generations, migration details, compatibility internals, raw
trait IDs, balance scores, or pending donor identity.

## Bounded presentation

Plants, cultivars, breeding programs, Knowledge rows, and lineage navigation use Insight Canvas
`VirtualList` elements with explicit overscan and cache limits. Repeated rows use deterministic
`Scope` IDs. Trait chips are semantic badges and are shown only when the cultivar's own trait
claim is present; rank never blanket-authorizes raw values. Exact aggregate modifiers are not
calculated from raw traits. Wide layouts use horizontal list/inspector splits; below 820px they
become vertical. High contrast, reduced motion, and compact density are document-local settings.

Lineage presentation uses Knowledge structural relations and the `parent_lineage` claim. The
existing `revealed=true`/confidence-one pedigree relation assumption is intentional: registering
a serialized parent relationship is treated as a certain relation, while the parent label still
uses a semantic unknown state when the record is not player-visible. `InsightIds.Stable` produces
deterministic node IDs, cycles are path/expansion protected, and `InsightModel.Validate`
diagnostics are retained. Traversal is capped at 128 nodes, 256 edges, and 12 useful levels
before `InsightGraphLayout.Compute` runs. Unknown-parent nodes show no raw ID and are not
actionable.

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
and save/reload on the real quicktest map. The `knowledge` scenario verifies event registration,
pre-documentation unknown cultivar traits, post-documentation claim authorization, relation-backed
lineage without raw IDs, and the overview-only/progressive-navigation baseline. Its `authority`
phase additionally verifies the ten authority boundaries documented in `RUNTIME_TESTS.md`,
including hidden search/comparison, breeding uncertainty, unavailable Knowledge, and page-removal
focus/selection repair. Focused assertions remain companion-owned and execute through the RimTest
catalog/DevBridge recipe rather than a second runner.
