# ProjectNexus onboarding

> See [ROADMAP.md](../ROADMAP.md) for the standalone 1.7 target. The current preflight still reports the old dependencies as blockers; the migration plans to remove Knowledge Framework and Insight Canvas rather than port them, with optional Progression: Agriculture proposed.

Project ID: `horticulture`. The repository's current release supports RimWorld
1.6. Native 1.7 migration is a development candidate, not a release.

## Development layout

Canonical checkout: `C:/Projects/Horticulture---Novel-Seeds`. New worktrees:
`C:/Projects/Worktrees/Horticulture/<task>`. Keep generated evidence out of
tracked source and preserve existing managed workspace locations.

## Native testing (Innovations reference)

Routine configuration is AllPerformance, with UltraBoost for gameplay fixtures
where supported. Run this read-only preflight from an exact candidate:

```powershell
powershell.exe -NoProfile -File DevTools/Get-NativeTestLoadout.ps1
```

The preflight checks the installed game, Harmony and optimizer package IDs,
version folders and SHA-256 hashes. It separately reports whether the 1.7
candidate/dependencies are ready; PASS means only the performance loadout
is available, not a game run. Loadout Minimal excludes all three performance
mods. BothLoading excludes Missile Girl for diagnostic comparisons.

| Order | Package | Notes |
| --- | --- | --- |
| 1 | brrainz.harmony | 1.7 assembly |
| 2 | aruclya.rimworldloadingoptimizer | Workshop 3817423183 |
| 3 | taranchuk.fastergameloading | Workshop 3652938473 |
| 4 | ludeon.rimworld | Core |
| 5 | Horticulture dependencies, then exact Horticulture candidate | Verify actual dependencies before launch |
| Last | vr.missilegirl | Workshop 3712928623, owner-qualified 1.6-declared exception |

Missile Girl must match the exact assembly qualified by Innovations or be
requalified. At runtime confirm active package order, loaded assembly hash
and registered Missile Girl Harmony patches. Do not change Workshop metadata.
Retain a minimal reference run for unexplained failures and shader regressions.

Use the native workflow from Innovations: source regressions; .NET 10 reference
build; exact Git-stamped package; guarded staged deployment only when all
RimWorld processes have closed; isolated Quicktest with a unique save-data
folder, explicit ModsConfig and Prefs; per-run evidence and process-owned
cleanup. No separate simultaneous instance, no global kill, no modifications
to player saves/config/preferences. The normal game log is shared by the engine.
Verify the candidate actually loaded before accepting evidence. Native runtime
fixture and 1.7 build are not implemented by this preflight.

## Validation profiles

The ProjectNexus manifest records source regression, native runtime smoke and
Waylith qualification, plus optional legacy RimTest integration. Existing
PowerShell scripts are invoked directly by an authorized developer/host agent;
they are not disguised as managed legacy check executables. Record individual
exit codes. Preserve the existing 1.6 content and test catalogs.

Knowledge Framework and Insight Canvas still need 1.7 qualification.
Progression: Agriculture declares 1.7, but its actual load folders and API
compatibility also require validation. Do not force-load old assemblies or
change support metadata simply to make a test start.
