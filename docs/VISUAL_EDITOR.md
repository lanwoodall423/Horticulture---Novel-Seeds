# Horticulture Visual Designer

The Visual Designer is an Insight Canvas document wrapped by the existing
`Dialog_TraitVisualDesigner` window. The window remains the compatibility boundary for
settings integrations; the document owns navigation, section tabs, accessibility state,
focus, split orientation, controls, feedback, and diagnostics.

## Editor model

The editor has explicit Plant and Produce modes and semantic mask channels. Plant channels
are Produce, Leaves, and Stem. Produce channels are Produce, Leaves, and Container. The UI
uses player-facing labels and never exposes the serialized channel indexes.

Visual values continue to be read and written through `VisualSettingsRecord`,
`GlobalTraitSettingsRecord`, `PlantSettingsRecord`, and `OptionWeightRecord`. Opening an
editor does not create an override. The override toggle, Restore inherited action, XML/default
reset, section resets, and current-mask reset preserve the existing inheritance and cache
invalidation rules.

## Preview and masks

The preview is an Insight Canvas `Custom` surface that delegates to Horticulture's existing
plant mesh, produce texture, and mask compositor. It does not generate masks, perform texture
readbacks, or rebuild preview textures from a paint callback. Existing cache keys, manual-mask
precedence, low-confidence safety, and `ProduceMaskRenderer.ClearAll` cleanup remain owned by
the gameplay renderer and settings authority.

`Dialog_PlantMasks` remains the specialized brush/editor authority because its Add, Remove,
Replace, Original/Mask/Final views, Plant/Produce/Leaves/Stem or Container channels,
projection validation, bounded undo history, and review callback are gameplay-facing behavior.
The surrounding review queue is an Insight Canvas document with searchable, bounded rows,
confidence/status badges, and explicit Open Painter actions.

## Related player surfaces

Naming, plant groups, plant tags, trait groups, trait tags, exclusive trait tags, mask review,
and embedded PlantVarietyTab/ProduceVarietyTab inspectors use document-owned Insight Canvas
chrome. Existing callbacks remain the authority for `UnlockVariety`, `RenameVariety`, group/tag
normalization, trait reset, mask scanning, and lineage navigation. Collection documents cap
presentation rows at 1,000, use stable scoped row IDs, and refresh snapshots before painting.

All migrated documents enable duplicate-ID tracking, support high contrast/reduced motion and
compact density, switch list/inspector splits below the 820px breakpoint, and call
`InsightUiHost.PostClose` when their compatibility window/tab closes.

## Verification

Use the RimTest-managed Horticulture smoke recipe for document isolation, Plant/Produce channels,
section controls, inheritance/reset paths, accessibility, preview delegation, bounded collection
chrome, and diagnostics. DevBridge2 coordinates only readiness, leases, restart, and evidence
collection.
