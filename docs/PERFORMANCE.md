# Performance notes

The release candidate keeps work out of the render path:

- automatic mask generation and texture readbacks run during the initialization/cache path;
- validated local and bundled records are looked up without regenerating them during drawing;
- registry rows use bounded lists and deterministic ordering;
- cultivar IDs resolve through the registry cache;
- produce inheritance is stored on the existing Thing rather than creating a ThingDef per cultivar.

## RC measurements

The Horticulture-owned `rc-performance` scenario records these measurements in its runtime report:

| Measurement | Workload | Acceptance budget |
| --- | --- | ---: |
| Registry display ordering | Synthetic 100, 500, and 1,000 cultivar rows | 5 seconds per case |
| Registry ID lookup | 1,000 cached ID lookups | 5 seconds |
| Automatic-mask lookup | 1,000 validated cache lookups, no generation | 5 seconds |

The budgets are safety limits for a loaded development quicktest, not player-facing frame-rate promises. The exact elapsed milliseconds, assembly hash, game version, and dependency hash are archived with the runtime report and copied into the release manifest.

## Manual performance checks

Before a public release, inspect a save with a large cultivar registry and a map containing several growing zones. Open the registry, switch all four pages, open a cultivar detail view, open plant selection, and scroll the settings pages. Confirm that no repeated texture generation, long UI stall, or log error occurs. Record the save size and mod list with the beta feedback form if a stall is reproducible.
