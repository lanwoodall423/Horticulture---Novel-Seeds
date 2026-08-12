# Release candidate checklist

## Scope and metadata

- [x] Gameplay features are frozen; remaining changes are defects, clarity, compatibility, performance, packaging, or documentation.
- [x] `About/About.xml` has package ID `lan.horticulture.novelseeds`, RimWorld 1.6, required dependencies, precise compatibility wording, and reviewed load-after entries.
- [ ] `About/Preview.png` remains a manual artwork task for the release handoff; no unrelated artwork was generated.
- [x] README is player-focused and makes no universal plant-mod compatibility claim.
- [x] `CHANGELOG.md`, defaults, compatibility, performance, beta feedback, and RC test plan are current.

## Build and package

- [x] `dotnet build Source\HorticultureNovelSeeds.csproj --configuration Release` passes.
- [x] `DevTools\Build-ReleasePackage.ps1` creates the package from its allowlist.
- [x] `DevTools\Test-ReleasePackage.ps1` passes against the staged package.
- [x] Production DLL hash, mask bundle hash, manifest hash, version, commit, and build timestamp are recorded.
- [x] Package contains no source, tooling, runtime test DLL/PDB, bridge code, `bin`, `obj`, staging, caches, request files, result files, or test Defs.

## Automatic tests

- [x] `complete` passes, including clean defaults, UX discovery, registry scale, RC performance, save/reload, and log cleanliness.
- [x] `auto-mask-suite` passes with zero low-confidence publishable records.
- [x] `auto-mask-export` was not required because the bundled mask source was unchanged; the committed bundle and manifest were reviewed.
- [x] Runtime reports are archived and the release manifest lists the exact Horticulture/Knowledge Framework compatibility pair.

## Manual checks

- [ ] First mutation and Save Seeds flow are understandable without reading documentation.
- [ ] Naming cancel/X preserves the pack and the suggested name is useful.
- [ ] Settings open compactly; advanced controls are discoverable and serialized values remain unchanged.
- [ ] Registry empty, knowledge-gated, lineage, comparison, and large-list views are readable.
- [ ] Vanilla crop/tree, conventional modded plant, and conditional custom-system cases were checked.
- [ ] No new Horticulture errors or warnings appear in Player.log.

Do not publish to Steam Workshop or create a final GitHub release from this checklist. The RC handoff is a draft pull request only.
