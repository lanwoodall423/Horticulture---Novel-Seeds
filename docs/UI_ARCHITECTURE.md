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
Visuals provides high-level palette/mask actions and opens the existing focused mask and trait
editors. Profiles provides a list/card workflow for apply, save, update, delete, reset, and
publisher-default export. Advanced contains tags, accessibility, cache/mask diagnostics,
framework metadata, compatibility text, and full reset.

Repeated rows use `Scope` with deterministic IDs. Virtual lists use explicit overscan and
cache limits; no list callback scans the map or rebuilds the settings model. Page content is
composed once and dynamic labels/bindings reread authoritative values on later frames.

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
| Framework release | `2.0.0` |
| Framework checkout | `93a09005fa15190009daee625352cf4004974472` |
| Reference DLL SHA-256 | `DFEC9DB76B6ABD7442E82A5029005CE09DECC281CC34FB37C080FD015458A613` |
