# Insight Canvas settings architecture

The production settings surface is `InsightSettingsDocument` in
`Source/InsightSettingsUI.cs`. `HorticultureNovelSeedsMod` creates one document for the
live `NovelSeedsSettings` authority and keeps it until that authority changes. The old
`NovelSeedsSettingsUI` type is now only a compatibility facade for integrations that still
read `showAdvancedGeneralSettings` or `CurrentPlantPreview`; it does not own navigation,
selection, searches, scroll positions, expansion, or layout state.

## Ownership boundary

`NovelSeedsSettings` and its records remain authoritative for serialized gameplay data:

- ordinary mutation, cross-pollination, wild variation, trait balance, produce and palette values;
- plant, group, trait, tag, mask, and profile records;
- normalization, cache invalidation, reset behavior, Scribe keys, and map-facing semantics.

`InsightSettingsDocument` owns presentation state only: active page and workspace tab,
selected plant/group/trait/profile, search queries, virtual-list state, split orientation,
document density, high contrast, reduced motion, focus, transient effects, toasts, and
diagnostics. Plant, trait, group, and profile collections shown in the UI are immutable
display snapshots refreshed explicitly before a frame; they do not duplicate save data or
perform map/world scans during paint.

Ordinary controls use Insight Canvas direct getter/setter bindings. A setter mutates the
existing authority, clears the same visual/produce caches as `WriteSettings`, schedules
normalization before the next snapshot refresh, and invalidates the document. Destructive
actions use document-local confirmation dialogs. Profile operations continue to use
`SettingsProfileManager` and its existing Scribe snapshot path.

## Pages and responsive composition

The document has five navigation pages: Gameplay, Plants & Traits, Visuals, Profiles, and
Advanced. Navigation uses Insight Canvas responsive `Navigation`; it becomes a wrapped top
navigation at narrow widths. Plants & Traits uses Groups, Plants, and Traits tabs with
searchable bounded `VirtualList` registries and a responsive draggable `Split` inspector.
Gameplay keeps advanced inheritance-slot probabilities behind a document-owned expander;
trait-balancing strength and allowed-imbalance controls follow the authoritative balancing
toggle so dependent settings cannot be edited while balancing is disabled. Visuals provides
high-level palette/mask actions and opens the existing focused mask and trait
editors. Profiles provides a list/card workflow for apply, save, update, delete, reset, and
publisher-default export. Advanced contains tags, accessibility, cache/mask diagnostics,
framework metadata, compatibility text, and full reset.

Repeated rows use `Scope` with deterministic IDs. Virtual lists use explicit overscan and
cache limits; no list callback scans the map or rebuilds the settings model. Page content is
composed once and dynamic labels/bindings reread authoritative values on later frames.

## Horticulture field-guide workspace

`MainTabWindow_CultivarRegistry` is a lifecycle shell only. It creates one
`HorticultureWorkspaceDocument` for the lifetime of the MainTab window, forwards `PreOpen`,
`DoWindowContents`, and `PostClose`, and preserves `OpenPlant`, `OpenCultivar`, `OpenLineage`,
and `OpenKnowledge` for external integrations. `PostClose` always reaches
`InsightUiHost.PostClose`, so focus, popovers, toasts, and overlay ownership cannot leak into a
later window.

The workspace has five persistent pages: Overview, Plants, Cultivars, Breeding, and Knowledge.
Compare is a contextual Cultivars surface and is never a permanent navigation page. Plants and
Cultivars use searchable bounded `VirtualList` collections with responsive draggable `Split`
inspectors. Overview remains actionable when every collection is empty. Cultivars expose
semantic trait badges, favorite/archive/origin/balance/produce filters, locate/rename actions,
and a bounded comparison selection of two to eight records. Breeding presents the existing
`BreedingProgramRecord` values read-only; it does not invent create/delete mechanics. Knowledge
uses `HorticultureKnowledgeAdapter` and `HorticultureKnowledgeSnapshots` for personal versus
colony scope, with explicit unavailable/incompatible guidance and no framework internals in the
normal UI.

All map scans, Knowledge queries, comparison requests, graph traversal, and selection repair
occur during the document's pre-frame refresh, never inside paint callbacks. User-initiated
rename/favorite/archive/locate actions remain explicit controls that write through the existing
gameplay authority; ordinary painting only reads immutable presentation summaries and stable
`Scope` IDs. Lineage uses
existing `parentVarietyIds`, deterministic `InsightIds.Stable` node IDs, explicit missing-parent
entities, cycle protection, a 128-node/256-edge/12-level budget, model validation, and bounded
`InsightGraphLayout`. Selecting a known graph node returns to its cultivar inspector; missing
nodes remain informational and cannot mutate game state.

The document tracks Knowledge revision changes, repairs stale selections and comparison IDs, and
keeps per-document accessibility state (normal/compact density, high contrast, reduced motion)
and responsive split orientation. `HasIsolatedPresentationState`, duplicate-ID diagnostics, and
the `workspace` runtime scenario cover lifecycle isolation and rendering contracts separately
from settings.

## Theme, accessibility, and diagnostics

The document clones `InsightTheme.Default` into a charcoal botanical theme with a restrained
green accent, neutral text, semantic warning/destructive colors, compact spacing, small corner
radius, and subtle elevation. It never mutates `GUI.skin` or a global framework theme.

`TrackDuplicateIds` is enabled on every Horticulture document. High contrast, reduced motion,
and density are document-local settings. Insight Canvas owns focus order and Tab/Shift+Tab
traversal, Enter/Space activation, text ownership, focus rings, and transient cleanup.
Render errors and duplicate-ID paths remain available through the document diagnostics surface.

## Dependency provenance

This mod compiles against the installed framework at
`C:\Games\Steam\steamapps\common\RimWorld\Mods\InsightCanvas\1.6\Assemblies\InsightCanvas.dll`.
The reference is portable and `Private=false`; the DLL is intentionally not copied into this
mod's release package. RimWorld's normal dependency loader rejects a missing
`lan.insightcanvas` installation; Horticulture has no fallback or duplicate Widgets renderer.

| Field | Value |
| --- | --- |
| Package ID | `lan.insightcanvas` |
| Framework release | `2.1.0` |
| Framework checkout | `93a09005fa15190009daee625352cf4004974472` |
| Reference DLL SHA-256 | `E8D163B6A2B39EB80BBF8A5EA5AA0B8A80481D69A8CEE1D74526548D0A28C011` |

## Visual Designer and player dialogs

`HorticultureVisualDesignerDocument` applies the same lifecycle boundary to the remaining
player-facing visual editor. It owns Plant/Produce mode, semantic mask-channel selection,
Color/Shape/Effects tabs, sliders, toggles, expanders, callouts, badges, toasts, focus and
accessibility state. Below 820px its preview/inspector `Split` becomes vertical. The
compatibility `Dialog_TraitVisualDesigner` supplies the authority bridge and delegates its
`Custom` preview surface to the existing cached plant/produce renderer.

The bridge preserves inheritance, override, reset, normalization, cache invalidation, and
`ProduceMaskRenderer` cleanup. It never performs mask generation from a repaint. The specialized
`Dialog_PlantMasks` brush/editor remains the authoritative semantic three-layer editor; only
its review/editor chrome is represented by Insight Canvas. Review rows are built and validated
outside paint, capped at 1,000, and expose confidence/origin/status labels rather than raw
internal mask data.

Naming, group, tag, trait, review, and embedded inspector surfaces use the shared bounded
collection/naming documents. They retain existing callbacks and save keys, including
`UnlockWithName`, `RenameVariety`, plant/tag normalization, trait reset, and lineage navigation.
PlantVarietyTab and ProduceVarietyTab refresh immutable row summaries before drawing and keep
their specialized gameplay actions explicit. `docs/VISUAL_EDITOR.md` records the channel and
inheritance contract.

The remaining focused player dialogs use the same Canvas boundary: mask import/export and
breeding-mix selection use bounded searchable collection documents, profile naming uses the
input document, and mask-color preview uses a Canvas split with a custom Horticulture preview
surface. The custom preview still owns texture readback, tint composition, and cleanup; opening
it cannot mutate masks or settings. The developer-only unlock window remains a debug action with
its existing selection workflow, and `Dialog_VarietyLineage` is an obsolete compatibility shim
that immediately routes callers into the workspace. Neither is part of the normal player UI.
