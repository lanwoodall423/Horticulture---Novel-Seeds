# Performance notes

The release candidate keeps work out of the render path:

- automatic mask generation and texture readbacks run during the initialization/cache path;
- validated local and bundled records are looked up without regenerating them during drawing;
- registry rows use bounded lists and deterministic ordering;
- cultivar IDs resolve through the registry cache;
- produce inheritance is stored on the existing Thing rather than creating a ThingDef per cultivar.

## RC measurements

The Horticulture companion records live-game mask and registry checks as part of the RimTest
smoke evidence:

| Measurement | Workload | Acceptance budget |
| --- | --- | ---: |
| Registry display ordering | Bounded cultivar rows with deterministic ordering | No unbounded work |
| Registry ID lookup | Cached cultivar identity lookups | No repeated linear scan |
| Automatic-mask lookup | Validated cache lookup without paint-time generation | No render-path generation |

The companion evidence is produced through the authenticated RimBridge operation; it is a
development safety signal, not a player-facing frame-rate promise.

## Manual performance checks

Before a public release, inspect a save with a large cultivar registry and a map containing several growing zones. Open the registry, switch all four pages, open a cultivar detail view, open plant selection, and scroll the settings pages. Confirm that no repeated texture generation, long UI stall, or log error occurs. Record the save size and mod list with the beta feedback form if a stall is reproducible.
