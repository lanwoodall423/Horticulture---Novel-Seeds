# Runtime test scenarios

The test implementation is `DevTools/RuntimeTests`, built as a versioned `HorticultureNovelSeeds.RuntimeTests.*.dll`. It is loaded only for a test run and is excluded from the production project and release package. It adds a test-only `GameComponent` and Harmony hooks in that assembly; no test hook is compiled into `HorticultureNovelSeeds.dll`.

## Coordination workflow

The harness uses this sequence:

1. Build the production and test assemblies.
2. Ask `C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd restart` to load the test generation.
3. Read the ready generation and write `DevBridge2/Runtime/Horticulture.RuntimeTest.request.json`.
4. Acquire a lease with `DevBridge.cmd test begin`.
5. Horticulture reads the request from the real game, executes the named scenario, and writes a result JSON atomically.
6. The harness prints the result and releases the exact lease with `DevBridge.cmd test end <lease-id>`.

DevBridge2 coordinates process lifecycle only. It provides no Horticulture RPC, fixtures, assertions, or scenario logic.

## Scenarios

| Scenario | Coverage |
| --- | --- |
| `startup` | Default settings, supported Def discovery, GameComponent creation, Knowledge diagnostics, and initialization readiness. |
| `ordinary-crop` | Real plant spawn/sow/growth callbacks, mutation state, cultivar creation/rename/selection, harvest identity, and inherited produce. |
| `sowable-tree` | Supported tree sowing, mutation, cultivar save/replant, cutting path, and tree identity. |
| `cross-pollination` | Distinct parents, real sow/cross-pollination path with deterministic forced probability, lineage, hybrid identity, and stable breeding selection. |
| `produce-processing` | Multiple inherited ingredients, pigment/trait propagation, processing routing, and unrelated ingredient isolation. |
| `knowledge` | Registration, sow/growth/harvest/cutting/documentation, personal/colony transaction paths, witness-capable routing, and duplicate identity deduplication. |
| `save-reload` | Normal RimWorld save/load request with ordinary, tree, hybrid, trait, palette, and Knowledge state checks after reload. |
| `negative` | Unsupported plant, missing cultivar, empty registry, missing/invalid mask cache, and safe rejection behavior. |
| `long-running` | Repeated plant/event ticks, cache availability, and diagnostic/log growth checks. |
| `complete` | Runs the release smoke journey and includes `save-reload` as an asynchronous final phase. |

An assertion is `PASS`, `FAIL`, or `BLOCKED`. A blocked assertion means the required game state or capability was genuinely unavailable; the result never silently treats an unrun scenario as a pass.

## Result format and location

Results are written outside the mod package to:

```text
C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\Runtime\Horticulture.RuntimeTest.<request-id>.json
```

The JSON contains `schemaVersion`, `suiteVersion`, `requestId`, `scenario`, `status`, Horticulture commit and DLL SHA-256, Knowledge Framework release/API and DLL SHA-256, RimWorld version, start/end/elapsed ticks, assertion counts, failed assertions, exception stack traces, relevant Horticulture diagnostics, and new Horticulture log errors/warnings.

Before a scenario starts, the harness records the current `Player.log` line count. The runner inspects only subsequent Horticulture lines and classifies new exceptions, errors, missing-method/type-load failures, serialization failures, and Harmony patch failures as release-blocking. Historical unrelated log noise is not counted.

## Adding a scenario

Add a deterministic case to `RuntimeScenarioSuite.cs`, use the existing `Check`, `Failure`, `Block`, and fixture-cleanup helpers, add its name to the `ValidateSet` in `Run-RuntimeTests.ps1`, update the table above, build the test assembly, and run the focused scenario through DevBridge2. Do not add scenario classes, request files, or test Defs to `Source` or `1.6/Defs`; the Release DLL must remain free of test fixtures.
