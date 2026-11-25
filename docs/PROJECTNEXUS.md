# ProjectNexus onboarding

Project ID: `horticulture`. The repository remains a RimWorld 1.6 content mod; this registration does not port it to 1.7.

## Development layout

Keep the canonical checkout under `C:/Projects/Horticulture---Novel-Seeds`. Put new disposable checkouts under `C:/Projects/Worktrees/Horticulture/<task>` and generated evidence under `C:/Projects/Artifacts/Horticulture/<task>`. Preserve ProjectNexus-managed workspace locations. Never relocate an active checkout or manually deploy assemblies.

## Validation

The tracked ProjectNexus manifest defines three procedure-based profiles: source regression, RimTest runtime smoke, and future Waylith qualification. Windows PowerShell scripts remain directly runnable by an authorized developer or host agent; ProjectNexus's legacy managed check declaration does not accept shell executables. The manifest intentionally does not disguise those scripts as an executable managed check or grant trust. Record each exit code against the exact candidate. No source check is runtime or visual evidence.

Run `rimtest doctor --json` and then `rimtest affected --run --json` using the supported RimLiaison launcher as required by AGENTS.md. A promoted toolchain must be present; if doctor fails, stop runtime validation. The existing test catalog and artifact freshness requirements remain authoritative. Older dependency-layout-sensitive scripts need matching external sources; do not silently link or copy dependencies just to make them pass.

The installed game is not proof that the mod is compatible. Do not change supported versions until actual 1.7 migration and validation.
