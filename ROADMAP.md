# Horticulture - Novel Seeds: development roadmap

**Updated:** 2026-11-25
**Target:** standalone RimWorld 1.7 / Waylith development
**Status:** standalone .NET 10 candidate compiles; native UI and journal implemented in first pass; five-stage native gameplay fixture and full preservation receipt passed; isolated whole-plant and RGB semantic-mask shader proofs passed GPU swatch parity and field rendering; manual masking retired in native candidate; automatic-only 72-plant garden passed; five mask-free procedural traits passed a separate 72-plant visual garden; complete initial standing-plant appearance catalog passed a 108-plant, 90-case native garden with six labeled screenshots; 20 additional creative cosmetic candidates passed a separate 72-plant, mask-free comparison garden; all 25 explicit procedural motifs then received a botanical refinement pass and passed a 90-plant, 75-cultivar comparison garden; 15 additional organ-selective botanical candidates passed a separate 54-plant, 45-cultivar native garden; independent nonlinear sprite-mesh morphology passed a 36-plant, 30-cultivar garden with five forms alone and combined with pigmentation; localized organ compositing passed a six-species, 36-plant garden with 24 rendered reproductive variants and six explicit stem fallbacks; developmental pigmentation passed a same-plant, three-growth-stage garden with 45 verified GPU states and 30 growth-setter mesh refreshes; independent genetic wind response passed a 12-phase, 18-plant garden with 180 verified material-phase states and actual animated screenshot replay; environment-responsive surface finish passed a four-environment, 18-plant garden with 60 verified material states and stable material count; classifier quality, production shader integration and full runtime/rendering qualification pending. See [native development notes](docs/NATIVE17.md).

This is the canonical forward-looking development plan. [The release checklist](RELEASE_CHECKLIST.md), [current architecture](architect.md), [compatibility policy](docs/COMPATIBILITY.md), and [RC test plan](docs/RC_TEST_PLAN.md) describe the existing 1.6 release baseline unless explicitly updated for 1.7. Do not interpret this roadmap as a support declaration, completed implementation, or permission to publish a release.

## 1. Product direction and decisions

Horticulture is a plant genetics, cultivation, and visual phenotype mod. Its core loop is growing plants, encountering unusual variation, preserving novel seeds, naming cultivars, and breeding plants with desirable combinations of traits. Keep that loop accessible through ordinary RimWorld farming rather than introducing seed-stack micromanagement or a generalized knowledge simulation.

- Remove Knowledge Framework and Insight Canvas as required dependencies for the 1.7 implementation. Do not port either framework merely to keep Horticulture running, vendor their code, or create substitute general-purpose frameworks.
- Replace framework knowledge with a small colony cultivation journal; use vanilla Plants skill rather than a separate personal expertise system.
- Replace framework UI with focused native RimWorld windows, inspectors, settings and lightweight Horticulture-specific drawing helpers.
- Proposed additional dependency reduction: make Progression: Agriculture optional, retaining its crop progression integration when supported. Harmony should be the only mandatory external mod dependency after qualification. The performance mods below are test tools, not player requirements.
- Keep existing genetics, trait effects, seed preservation, cross-pollination, produce inheritance, cultivar identity, palettes, growing-zone selection and breeding-mix behavior. Changes to these mechanics require explicit justification and tests.
- Prioritize shader-based visual phenotypes with optional automatically generated semantic masks. Retire manual painting, manual mask files, and shared manual masks for 1.7. Preserve historical artwork in the 1.6 baseline and Git history. Do not make universal plant anatomy detection a prerequisite for visual variety.
- Adopt Innovations-style native tests rather than wait for RimTest. RimTest/RimLiaison remains optional; preserve its historical catalogs for possible future integration.
- Preserve the 1.6 release source, metadata, build artifacts and migration reference. Keep 1.7 work isolated until qualification; decide release branching/package layout before changing common metadata. Do not publish or declare 1.7 support prematurely.

### Verified starting point

| Area | Evidence / current state |
| --- | --- |
| Source | Existing 1.6 implementation at the start of the migration, with native-testing configuration commit `81b04037ccf29cc8ac14369bececf54b9c5bc567`. |
| Dependency surface | 11 top-level C# files import Knowledge Framework, 8 import Insight Canvas; 18 unique directly affected files, 8,052 of 28,260 lines in the measured source snapshot. These are affected-file counts, not a deletion estimate. Indirect callers, tests, metadata and documents also need work. |
| Save authority | Horticulture already owns cultivar IDs, traits, names, parents, generation, discovery metadata, flags, grower assignments, species palettes, plant/produce components and seed packs. |
| Toolchain | Installed game was `1.7.5014 rev1195`; .NET SDK 10.0.404 available at inspection. Innovations has a successful native test workflow. Re-probe the current game/reference versions before building. |
| Test preflight | AllPerformance, BothLoading and Minimal preflight passed; all three performance-mod assembly and About hashes matched Innovations' successful Agriculture receipt. No Horticulture 1.7 runtime pass has been established. |
| Remaining blockers | Horticulture has not been ported. Knowledge Framework and Insight Canvas still declare 1.6; removing them is the chosen path. Installed Progression: Agriculture declares 1.7 but its load folders and actual runtime/API compatibility are unqualified. Exact Waylith shader/mesh/batching contracts must be verified. |

## 2. Knowledge and discovery

### Replace the knowledge system

Keep discovery meaningful without replicating facets, claims, uncertainty, confidence, provenance, context hierarchies, witness transmission, decay, milestones, rank-based disclosure, or personal/colony scope machinery. Retire per-pawn Horticulture expertise, the pawn Bio knowledge surface, and the extra knowledge work-speed bonuses for sowing, harvest/cutting and produce recipes. This is a deliberate balance change; preserve trait-specific work modifiers and vanilla skill effects. Do not remove RimWorld's own tutorial/knowledge API.

Retain a simple, explicit disclosure model:

| State | Player-facing information |
| --- | --- |
| Unobserved species | No invented observations; ordinary vanilla availability/info remains distinct from Horticulture discovery. |
| Observed / cultivated species | Basic identity and recorded cultivation activity; no fabricated measurements. |
| Pending mutation / unregistered seed pack | Novel appearance and discovery opportunity; no hidden trait IDs, exact undocumented modifiers or hidden donor identity. |
| Named, player-visible registered cultivar | Documented traits, descriptions and genetic modifiers; known parent/origin information. |
| Cultivated named cultivar | Add measured performance (where actually recorded), independent of its genetic modifiers. |

Preserve the actual preserve-seeds request, successful mature harvest/cutting and naming/registration transaction. Canceling the naming dialog must leave the pack intact. Clear pending discovery at the correct successful gameplay boundary, not when a Knowledge Framework observation returns true. Prevent duplicate discovery, duplicate seeds, lost packs, repeat notifications, NPC/unsupported-plant events and partial failures; failed harvests must not count as successful ones. Preserve perennial harvest cycles and resource payment semantics. Audit current patch/event ordering and use stable event/cycle identities where needed.

### Lightweight journal

Own journal data in Horticulture's game component (or a small dedicated component). Use stable species def names and existing cultivar IDs; save only useful bounded aggregates. Proposed minimum:

- Species: first observed/cultivated date, first successful harvest, successful harvest count, documented cultivar count derived from registry.
- Cultivar: optional successful harvest count, actual yield minimum/maximum and count (mean if useful), first/last observed harvest, clearly labeled observations. Capture actual product output rather than treating pre-harvest predicted yield as a measurement.
- Only retain additional environmental facts if they are meaningful and genuinely observed; distinguish observed temperature from a calculated tolerance. No invented growth duration or yield data.
- Derive stages/milestones from completed events, not an independent XP system. Do not collect every plant's growth buckets or scan maps on each tick; update on genuine completed player actions, load/finalization, or bounded explicitly requested refreshes. No unbounded event history. Preserve player faction and supported-crop checks.
- Keep knowledge optional to core mechanics: journal failures may be reported but must not prevent normal mutation, registration, sowing, harvesting or rendering. Avoid duplicate observations across retries, reloads and repeated harvests.

### Replace source boundaries

Remove the framework registration/capability/readiness/migration/snapshot/relations adapters, framework-only Harmony hooks and retry tick loop. Replace the event router and presentation policy with small gameplay event and authorized display projections; remove the external types from `PlantKnowledge`, `CultivarRegistry`, `ModCore`, `Patches` and dependent windows. Keep useful event identity/deduplication mechanisms independently of the framework. Preserve `VarietyRecord` and `GameComponent_NovelSeeds` as the cultivar authority.

Audit all disclosure paths, not only the registry: plant/produce inspectors, seed and naming dialogs, growing-zone selector, breeding mix, Nice Plants Menu integration, cooking filters, labels, tooltips, search, comparisons, favorites, archive and lineage. No hidden donor, transient genotype, missing record or raw internal ID may leak through an alternate surface. A known visible parent may be navigable; missing/hidden parents display a semantic unknown state. Do not equate archive with hidden or undocumented.

## 3. Native UI and editor

Use native `Widgets`, `Listing_Standard`, `Window`, `ITab` and small Horticulture-specific helpers; verify their 1.7 shim behavior. The pre-Canvas UI in Git history (e.g. `a85088e`) is a reference for layout and interactions, not a source rollback. Preserve features added after that revision. Keep stable public open routes and class names where feasible for integrations and saved UI probes.

| Surface | Target |
| --- | --- |
| Registry / field guide | Overview, Plants, Cultivars, Breeding. Progressive navigation where useful; contextual comparisons (2-8 cultivars), no separate Knowledge page. |
| Cultivar details | Visible name, authorized traits/modifiers, discovery date and origin, known pedigree, actual cultivation history; rename, favorite/archive, locate. |
| Comparison and lineage | Direct comparison of documented genetic effects; unknown is not zero or a difference. Pedigree reads stored parent IDs, with cycle protection, missing nodes and bounded traversal (existing 128-node / 256-edge / 12-level ceilings or stricter). |
| Growing and breeding | Preserve selection, deterministic multi-variety mix, seed-pack naming/cancel, and supported optional menu integration. Retain or deliberately retire read-only legacy breeding-program presentation; do not confuse it with active breeding mixes. |
| Settings | Simple mutation/cross-pollination controls first; advanced trait balancing, donor requirements, plant/group/trait/tag overrides, palettes, produce, profiles, automatic-anatomy controls and diagnostics remain accessible. |
| Plant / produce inspectors | Clear documented traits, inherited produce effects and source cultivar navigation; preserve gameplay actions. |
| Focused dialogs | Native naming/rename, plant/trait groups and tags, exclusive tags, trait families, breeding mix, profile naming and preview. |
| Visual Designer | Native Plant/Produce modes, live phenotype preview, Color/Shape/Pattern/Effects controls, automatic semantic region selection, inheritance/override/reset and regional color controls. |
| Automatic anatomy | Remove the manual painter, brushes, projections, mask file import/export and review queue. Keep validated automatic segmentation, per-species opt-out, whole-item produce styling and graceful fallbacks. |

Preserve non-retired settings data, Scribe keys, normalization, invalidation and profile management. Legacy manual mask fields are intentionally ignored by 1.7; do not modify original 1.6 files. Opening an editor must not create an override; reset and restore inherited remain different operations. Bound searchable lists and cache display snapshots before paint; no per-frame map scan, mask generation or texture readback. Use per-window transient state, stable selection, keyboard/focus behavior, readable contrast, scroll and narrow-window layout, safe close/cancel and cleanup; preserve useful high contrast/reduced motion/compactness where practical without recreating Canvas. Localize player-facing strings and avoid leaking framework internals. Keep native UI functional before shader redesign; build specialized preview and shader controls against shared phenotype parameters.

## 4. Optional integrations and 1.7 build

- Replace the two direct Progression: Agriculture use sites (crop unlock and crop availability) with a narrow optional compatibility boundary, likely guarded reflection against confirmed API. If absent use vanilla research, skill and crop availability rules; if present but incompatible, report and degrade safely without silently bypassing its restrictions. Test absent, supported and incompatible cases; no forced loading of old binaries or fake 1.7 metadata. Maintain existing optional Wildlife / Nice Plants Menu hooks and relevant custom plant exclusions without introducing mandatory dependencies.
- Build a separate 1.7 candidate targeting .NET 10 against exact installed or compatible 1.7 references. Innovations is a practical build/package reference; its observed Krafs reference and Harmony versions are starting points, not permanent pins. Check installed assemblies and Harmony patch signatures, especially sowing closures, PlantCollected, work calculations, graphic/atlas/section rendering, world/tile types, and Unity shim APIs. Avoid hard-coded obsolete Unity paths.
- Audit `About/About.xml`, `LoadFolders.xml`, project references, source imports/types, assembly references, test companion references, release scripts and docs. The eventual 1.7 assembly/package must have no mandatory Knowledge Framework, Insight Canvas or Progression: Agriculture assembly dependency. Keep 1.6 artifacts and version-specific metadata intact; do not overwrite the tracked 1.6 DLL. Compile and validate small slices rather than wait for an entire rewrite.

## 5. Save migration and release preservation

Make migration explicit, versioned, idempotent, and tested on copies. Existing records remain authoritative. Preserve serialized names/class identities for `unlockedVarieties`, stable cultivar IDs, traits/Def references, parent IDs, generation/origin, names, discovery metadata, hidden/favorite/archive flags, `speciesColorPalettes`, growing-zone selection and breeding-mix dictionaries, next-ID counters, pending discovery/resource/harvest cycles, seed packs, inherited produce and non-retired settings/profiles. Manual mask payloads are retired and are not imported into the active native configuration. Do not regenerate existing palettes or rewrite stable genetic identity.

- Existing visible named cultivars are documented on import; hidden/unregistered records remain undisclosed. Do not fabricate observed yield or environmental history. Resolve missing Defs/parents safely; preserve IDs where possible and handle corrupt/duplicate records with diagnostics rather than silent loss.
- Retire personal knowledge/XP and recipe work bonuses intentionally; detailed historical framework observations are nonessential. Optional one-time 1.6 export of meaningful colony observations is preferable to a hard dependency in 1.7. If importing, validate domain, subject, numeric bounds and schema, avoid double import, and report what was dropped.
- Older `PlantKnowledgeRecord` derives from a framework class; newer observations live in the framework's game component. Audit Scribe and missing-class behavior using representative saves before claiming safe removal. If needed, supply a legacy reader or explicit offline export/conversion with backups and no framework dependency in the final 1.7 package. Do not erase the framework's data for other mods.
- Legacy BreedingProgram records are currently read-only/load-only and may be omitted on subsequent saves. Decide preservation or explicit retirement; active cultivar selection/breeding mix is independent and must survive.
- Test new game and representative old saves on disposable copies, repeated save/load and migration retries. Game-version upgrade compatibility and removal of other mods are separate concerns; document unsupported combinations. No real player save may be used as a mutable test artifact.

## 6. Waylith phenotype rendering

The engine migration makes shader-driven expression practical. Shaders can apply visual effects efficiently; they do not automatically identify semantic anatomy. Validate against the actual installed 1.7 renderer and official modder primer before deciding the precise material/mesh ABI.

### Recommended approach: hybrid visual backend

Keep genetics and existing `PlantVisualParameters` / `PlantVisualColorUtility` semantics authoritative. Keep geometry effects (scale, width/height, density, spread, rotation, offset, shadow) separate from color/effect composition unless measured vertex displacement is appropriate. Preserve tint, hue, saturation, brightness, contrast, opacity, dullness, radiance/gloom and overlay patterns; validate outline protection and alpha.

| Priority | Option | Implementation intent |
| --- | --- | --- |
| P0 | Isolated vanilla shader prototype | Prove ShaderDef/MaterialDef, GLSL compilation, uniforms, original cutout/lighting, texture sampling, growth, wind, shadow, snow, UV/atlas and vanilla fallback on one crop. |
| P0 | Shader color compositor | Replace recurring CPU-styled texture variants/readbacks with shared base textures and parameterized shader materials where possible. Port existing color math and protect outlines/alpha. |
| P0 | Existing semantic masks | Use validated automatic plant masks as optional shader inputs, packed R=Produce, G=Leaves, B=Stem, alpha reserved. Maintain neutral unassigned pixels and identity validation. Harvested items use whole-item produce color until a separate automatic item classifier is qualified. |
| P1 | Procedural mask-free phenotype patterns | Deterministic per-cultivar/plant seed and UV/noise for pigmentation gradients, mottling, speckling, stripes, edge color, variegation, glaucous/golden/anthocyanin effects, subtle radiance or darkening. Do not force every mechanical trait to have an added color. |
| P1 | Conservative maskless targeting | Use color/chroma/value/alpha, produce signature and UV cues only where reliable; uniform green foliage vs green fruit, leafless plants, structural harvests and unusual art are ambiguous. Never label heuristic regions as authoritative anatomy. |
| P1 | Unified plant/produce/preview parameters | Share effect semantics across field rendering, harvested products and native Visual Designer preview. Preserve plant/produce distinctions; simplify controls around effects and automatically identified regions. |
| P2 | Batching and GPU parameter storage | Begin with cached materials per phenotype; profile actual section batching, material counts and atlas behavior. Investigate shared material + phenotype index in verified available mesh payload + parameter texture/SSBO only if justified. Existing vertex colors encode wind and UVZ payload is already used; do not overwrite them or assume a free attribute. |
| P2 | GPU bake fallback / geometry | Consider bounded GPU texture baking for thumbnails or unsupported runtime paths; independent region scale/offset may still need multiple layers or image warps. Investigate vertex deformation after basic compositor correctness. |
| P3 | GPU-assisted segmentation / custom renderer | Experimental only; no verified mod-facing compute path or need for a replacement instanced renderer yet. Retain the CPU automatic classifier and conservative fallback. |

Region selection precedence: validated local/bundled automatic mask > bounded validated generation where applicable > conservative procedural approximation > whole-plant/vanilla fallback. Do not apply low-confidence masks automatically; no generation/readback from draw/repaint or routine Print/material cache misses. Respect semantic absence and neutral regions. Preserve mask identity across texture variants, growth state, orientation and source changes. Keep the identity-validated automatic bundle; do not require a mask for every plant. When no safe targeted effect exists, use a non-targeted effect only if semantically safe, otherwise render vanilla.

Keep the current CPU renderer as a reference/fallback where practical; choose backend per game version and capability. Preserve independent geometry layers where needed, wind exposure colors, atlas/UV conventions, random subvariants/flips, growth placement, lighting, cutout, snow and shadows. Translucency may require a different shader and wind handling; validate explicitly. Do not replace vanilla plant rendering indiscriminately.

### Performance qualification

Compare baseline, current CPU-style approach where available, and candidate on the same game build, graphics settings, seed, plant composition, field size and cultivar diversity. Measure startup/mask generation, frame timing (CPU/GPU where available), draw/material/batch count, texture/GPU memory, mesh regeneration/cache hit rates, and UI preview stalls. Test 100 / 1,000 / 10,000 plants and varying distinct cultivar counts. Example memory reasoning only: 8 variants x 100 cultivars x 256x256 RGBA = 200 MiB of unique styled textures versus 4 MiB for 16 shared source+mask textures (before mipmaps/overheads); actual gains depend on implementation and measurements.

Use minimal optimizer-free baseline for renderer comparisons; AllPerformance remains the routine test loadout. Avoid conflating UltraBoost simulation speed with rendering performance. Select representative rice, corn, strawberry, potato, cotton, psychoid, healroot, oak, conventional modded fruit trees, green fruit, leafless/growth variations, and custom graphics with safe fallback. Inspect visuals in visible or validated `-NoWindow` graphics mode; `-Headless` cannot qualify shaders. Capture screenshot and profiler evidence, check shader errors and texture identities. Verify Waylith launch flags on the installed version. Do not infer GPU improvements from source design alone.

## 7. Native development and evidence

Follow [the ProjectNexus guide](docs/PROJECTNEXUS.md) and Innovations' standalone test architecture, not its gameplay-specific test code wholesale.

- ProjectNexus coordinates work; GitHub holds source; canonical repos under `C:/Projects`, new disposable worktrees under `C:/Projects/Worktrees/Horticulture/<task>`, generated evidence outside tracked source (e.g. `C:/Projects/Artifacts/Horticulture/<task>` or ignored test results). Preserve existing managed workspaces; don't move active ones.
- Use .NET 10 build and standalone rule tests plus existing source regressions. Stamp the exact clean Git commit into build/package; if dirty, label it accurately and don't qualify it as a clean release candidate. Verify assembly hashes and loaded identity. Guard staged package install with backup/rollback and hash receipt; never overwrite a live mod installation or deploy while RimWorld is running.
- AllPerformance default: Harmony, RimWorld Loading Optimizer (`3817423183`, verified 5.4.0), Faster Game Loading (`3652938473`, verified 2026.11.15.1), Core, required test candidate/optional dependencies, then Missile Girl (`3712928623`, exact Innovations-qualified 1.6-declared assembly exception). Verify actual package IDs/order, hashes, game version and Missile Girl patches; requalify changed builds rather than modify Workshop metadata. BothLoading and Minimal are diagnostic profiles. UltraBoost for routine gameplay fixtures where supported, not required for visual or performance reference runs.
- Only start a native Quicktest when the player has closed RimWorld and no other instance exists. No second installation, sandbox, concurrent session, global kill or touching normal saves/preferences/mod configuration. Unique disposable save-data root and explicit ModsConfig/Prefs; preserve normal config/prefs/saves by pre/post inventories, collect log/evidence for the owned run (engine log is shared), stop only the exact owned process on timeout or completion. No manual game/assembly shortcuts without evidence safeguards.
- Adapt existing Horticulture companion assertions and regression cases into standalone and native fixtures. Replace source-shape checks that mandate Framework/Canvas types with behavior tests; keep legacy tests for 1.6 and preserve trait, inheritance, cross-pollination, auto-mask classification, event routing, immutable Def and faction safety coverage.
- Produce structured per-run receipts with exact source/build/deployment identity, loaded packages and versions, pass/fail/skipped counts, actual game actions, save/reload outcomes, timing, screenshots/diagnostics when relevant, and cleanup/preservation checks. Missing candidate, dependency, fixture or evidence is blocked, not passed. Test-only code/Defs must not ship in public release.
- Optional RimTest integration remains separate; a broken promoted toolchain is not a blocker for native development. Do not treat the existing loadout preflight as a native runner or in-game result.

| Fixture | Required evidence |
| --- | --- |
| Startup | Exact package loads without the two frameworks; Harmony targets and XML/Def initialization are valid; no new relevant startup errors. |
| Genetics | Deterministic rules, mutation/trait balance, color inheritance, cross-pollination, perennial/resource behaviors, player-faction restrictions. |
| Agriculture | Real sow/harvest/cut jobs, actual yield, preservation, cancel/retry, naming/unlock, duplicate prevention, cultivar selection and breeding mix. |
| Journal / disclosure | Actual completed events update bounded records; no phantom or duplicate events, no hidden data leaks, no gameplay dependence on journal/UI. |
| UI | Empty and populated registry, filtering, compare, lineage, all settings/editor actions, native input/close and no paint-time mutation. |
| Produce | Inherited effects/colors, recipe/product behavior, mixed stacks, correct source cultivar display; no retired expertise multiplier. |
| Persistence | New and migrated saves reload; IDs, traits, lineage, assignments, palettes and non-retired settings preserved; journal/migration idempotent. |
| Rendering | Controlled field grid and produce/preview snapshots with varied phenotypes, fallback, wind/growth/lighting/snow/shadow and GPU shader diagnostics. |
| Performance | Matched baselines, fields and cultivar diversity with metrics; no unbounded knowledge polling or recurring CPU texture generation. |
| Compatibility / release | Minimal Core+Harmony candidate; optional integration and supported modded plants; release package excludes test artifacts and has verifiable manifest. |

## 8. Phased delivery and exit gates

Native loadout preflight, initial compilation and a five-stage native gameplay fixture are established (see docs/NATIVE17.md for exact evidence and limitations). Full exit gates remain open until runtime, UI, save, compatibility and rendering evidence is recorded. Do not mark phases complete without evidence.

| Phase | Work | Exit gate / dependency |
| --- | --- | --- |
| 0 - Baseline | Preserve 1.6, document current behavior, capture dependency/save contracts, establish separate worktree and reference build plan. | Existing source and preflight evidence captured; no destructive release changes. |
| 1 - Knowledge decoupling | Simplify discovery transactions, journal and presentation, remove extra expertise effects and framework polling; migration design. | Core rule/event tests, no hard Knowledge Framework use in candidate; hidden data preserved. |
| 2 - Native UI | Native settings, registry, inspectors, focused dialogs, automatic anatomy controls and native visual preview; incremental compilation. | All essential player/editor actions accessible; no Insight Canvas imports or assembly references; authority preserved. |
| 3 - Optional agriculture | Isolate compatibility, audit all package/reference/load metadata, preserve vanilla behavior. | Core+Harmony target builds; no mandatory Progression: Agriculture dependency if optional proposal implemented. |
| 4 - 1.7 baseline and native runner | .NET 10 / actual 1.7 APIs, guarded candidate deployment, isolated Quicktest and exact receipts. Start compilation and runner scaffolding earlier rather than wait for UI completion. | First genuine startup and gameplay smoke pass without frameworks; migration tests on disposable copies. |
| 5 - Shader proof and compositor | Isolated vanilla proof (can be spiked early), port color math, mask input, fallback, field/produce/preview semantics. | Verified shader compilation, screenshots, actual render correctness and no regressions to normal plants. |
| 6 - Procedural phenotypes and optimization | Mask-free patterns, conservative targeting, unify editor controls, profile batching/memory; consider later GPU/geometry options only with evidence. | Representative visuals and measured performance under reproducible workloads; no unsafe masks. |
| 7 - Qualification and release readiness | Full native fixtures, old/new save tests, optional integrations, large registry/UI, release packaging and docs. | All acceptance gates satisfied, limitations disclosed, owner review before release/publishing. |

Incremental implementation should prefer small coherent changes and evidence per phase. The initial 1.7 goal is a usable standalone mod; advanced GPU tables, compute segmentation, custom renderer, and full historical knowledge import are deferred unless justified. Do not let these experiments delay a working core mod.

### Final acceptance

A 1.7 candidate is ready for release review only when it demonstrably loads with neither framework installed, preserves core genetic behavior and gameplay authority, provides usable native UI and editor controls, protects hidden information, safely persists/restores documented cultivars and saved settings, handles optional integrations conservatively, renders meaningful visual phenotypes with vanilla fallback, and has reproducible native gameplay, visual and performance evidence. Update support metadata, dependency declarations, architecture, compatibility, testing, release checklist and packaging only to match actual qualified behavior. No automatic publication.

## 9. Open decisions and explicit non-goals

- Optional Progression: Agriculture integration is a recommendation beyond the requested two-framework removal. Confirm fallback semantics and test actual availability before shipping.
- Historical detailed knowledge import is optional; decide whether its limited gameplay value warrants a 1.6 export tool. Preserve essential data regardless. Decide how to present or retire old breeding-program records.
- Exact shader ABI, batching strategy, texture packing details, dependency/API compatibility and old-save upgrade behavior are unverified. Revisit with actual build/runtime evidence.
- Do not implement concurrent player/test RimWorld instances at this time. Do not require RimTest repair for this migration.
- No universal automatic segmentation promise, no new generalized framework, no unverified performance claims, and no changes to unrelated mods.
