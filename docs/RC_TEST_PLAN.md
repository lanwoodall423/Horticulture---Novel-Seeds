# Release-candidate test plan

All Horticulture assertions, fixtures, mask generation, and result reports belong to `DevTools/RuntimeTests`. DevBridge2 only coordinates restart, readiness, and the shared test lease.

## Automatic matrix

| Scenario | Purpose | Included in `complete` |
| --- | --- | --- |
| `startup` | Game component, settings, supported Defs, and Knowledge readiness | Yes |
| `clean-default` | Bundled defaults match code defaults and clean-start behavior | Yes |
| `ux-discovery` | Keyed first-discovery/Save Seeds guidance, compact settings disclosure, and comparison gate | Yes |
| `registry-scale` | 100/500/1,000-row ordering and cached registry lookup timing | Yes |
| `rc-performance` | 1,000 automatic-mask lookup timing and the registry performance budget | Yes |
| `ordinary-crop` | Sowing, growth, mutation state, cultivar creation/rename/selection, harvest, and produce data | Yes |
| `sowable-tree` | Tree mutation, cultivar save/replant, cutting, and Knowledge identity | Yes |
| `cross-pollination` | Real donor selection, lineage, and deterministic breeding selection | Yes |
| `produce-processing` | Multiple inherited ingredients and isolation of unrelated data | Yes |
| `knowledge` | Registration, evidence, duplicate identity, documentation, and targeted relations | Yes |
| `negative` | Unsupported plants, missing records, and mask/cache safety | Yes |
| `long-running` | Repeated plant/event path and cache stability | Yes |
| `save-reload` | Cultivar IDs, traits, palettes, and Knowledge state across a normal save/reload | Yes, asynchronously |
| `auto-mask-suite` | Precedence, fallback safety, tree morphology, variants, stale/low-confidence handling, and lookup timing | Separate |
| `auto-mask-export` | Horticulture-owned real in-game bundle generation | Separate |

The runner archives each terminal report under `DevTools/Staged/RuntimeResults`. Reports use schema version 2 and suite version 1.1 and include the commit, production DLL hash, Knowledge Framework release/API/hash, RimWorld version, tick timings, assertion counts, log findings, diagnostics, and performance measurements.

## Automatic command plan

```powershell
.\DevTools\Run-RuntimeTests.ps1 -Scenario complete
.\DevTools\Run-RuntimeTests.ps1 -Scenario auto-mask-suite
.\DevTools\Build-ReleasePackage.ps1 -RequireRuntimePass
```

Every command coordinates restart/readiness only through `C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd`; it never launches, kills, or restarts RimWorld directly. The runtime test DLL and request/result files are removed after a restart-backed run.

## Manual matrix

- Open settings on a clean install: confirm the compact page shows the two common rates and an explicit **Show advanced settings** action.
- Open the naming dialog from a first mutation: confirm the suggested name, exact trait summary, preservation note, and cancel behavior.
- Open the registry with no discoveries and with several discoveries: confirm empty states, knowledge-gated text, lineage, comparison, and filters are understandable.
- Test vanilla crops, a vanilla sowable tree, one conventional modded crop, and each installed custom planting/harvest/graphic/product integration.
- Save, reload, reopen the registry, and inspect a cultivar, a plant menu, and a produce stack.
- Verify the About page metadata and manually supply `About/Preview.png` artwork before publication if the project still lacks it.
