# Plant Color Inheritance Architecture

## Coordinated knowledge boundary

Knowledge Framework owns Plants domain registration, stable subject resolution, colony knowledge,
personal knowledge, overall pawn expertise, reveal thresholds, immutable queries, gain events, and
save persistence. Horticulture owns plant event meanings, XP amounts, bounded work/reveal effects,
and registry presentation. The former `horticultureKnowledge` save list is load-only migration input:
personal records import by pawn and crop, colony values import as per-crop sums, and expertise imports
as each pawn's highest old crop XP. Import is idempotent and does not emit rank notifications.

The retired Breeding Program records are also load-only compatibility data. New saves do not write
them, new varieties do not run program matching or notifications, and the controlled cultivar-mix
selection dictionaries remain independent and supported.

## Stable compatibility boundary

Existing varieties and plants continue to store generated `VarietyTraitDef` references. Existing
produce continues to store its packed RGB and mask colors. No shared plant, produce, or product
definition is recolored, cloned, or given save-specific state.

Variety-specific sow skill and work are substituted at RimWorld's field-read boundary. The target
closures are discovered by their captured `JobDriver_PlantSow` field, signatures, and `sowWork`
reads rather than compiler-generated names. Mutation assignment, contact effects, and knowledge are
committed only when the sow tick crosses its completion threshold. Perennial harvest substitutes
the collected plant instance's destructive-harvest decision and then changes only that instance's growth, while Nice Plants Menu shows variety deltas in
its dedicated panel without rewriting shared labels, stats, or `PlantProperties` during drawing.
Because Nice Plants Menu snapshots selected growers inside its constructor, the compatibility helper
temporarily substitutes the requested growers in the selector's object list only for that synchronous
constructor call and restores the exact prior selection in `finally` before the window is opened.

## Color flow

1. `GameComponent_NovelSeeds` owns one persisted `SpeciesColorPaletteRecord` per eligible crop.
2. `SpeciesColorPaletteUtility` deterministically builds missing records from the world seed, crop
   def name, configured base graphic color, global limits, and the crop's restricted/unrestricted
   setting.
3. `ColorTraitFactory.Select` selects generated color traits only from that crop's saved palette.
4. Cross-pollination replaces each shared color family with one trait whose color is produced by
   `PigmentColorUtility` and projected back into the crop's allowed palette.
5. The resulting color trait remains the single inherited datum consumed by existing plant and
   harvested-produce visual paths.
6. Recipe products retain the existing inherited-produce component and use the same pigment mixer
   when multiple colored ingredient stacks contribute to a supported material product.

## Save compatibility

All old scribe keys are retained. Palette records are an additive collection under
`speciesColorPalettes`; absence means an old save and triggers deterministic initialization during
post-load/finalization. Existing varieties are not rewritten. New color settings are additive and
have defaults matching the normal restricted mode.

## Hybrid behavior

When a variety is unlocked for a crop and its recorded parents belong to other crop defs, the
crop's palette is derived from the parent palettes (including pigment blends) and persisted. Normal
same-species crosses use that species' existing palette.

## Validation contract

`DevTools/Verify-ColorInheritance.ps1` checks persistence keys, preservation of legacy keys,
palette-constrained mutation/cross paths, pigment use in recipe inheritance, and debug exposure.
The production assembly must also compile against RimWorld 1.6.

## Shared horticulture knowledge

- `GameComponent_NovelSeeds` owns additive `horticultureKnowledge` records keyed by pawn and
  crop def name. Existing variety, palette, grower-selection, and legacy component keys remain
  unchanged.
- Sowing, harvesting, cutting, fertilizing, seed discovery, and recipes using inherited produce
  all feed the same ledger. Novice, Adept, Expert, and Master are supplied by
  `KnowledgeFramework.dll`.
- Rank improves only work performed by that colonist on the known plant species, including sowing,
  harvesting, cutting, fertilizing, and supported produce recipes. Knowledge and expertise never
  modify mutation or cross-pollination probability.
- The pawn Bio row opens the Knowledge page inside the existing Cultivar Registry.
- Knowledge gain identifies player pawns from their own faction definition. It never queries the
  global player faction, because that lookup logs an error during startup and game transitions when
  no player faction exists yet.
- The Cultivar Registry follows the Aquaculture Field Journal's header, page navigation, two-pane
  list/detail structure, sorting, filtering, search, selection, and knowledge presentation. Its
  pages are Discovered Plants, Cultivars, Knowledge, and Compare. The old Breeding Program screen
  is absent, while the underlying breeding records, notifications, mutation, and inheritance
  mechanics remain save-compatible and operational through gameplay.
- Colony mode aggregates per-crop knowledge from existing `horticultureKnowledge` records without
  persisting aggregate state. It does not derive, store, label, rank, or display expertise.
  Colonist mode retains species knowledge, expertise rank/progress, and personal work effects.
- Compare selection is transient window state. At least two discovered cultivars are required;
  comparison fields remain gated by the selected scope's plant knowledge.

## Knowledge and registry validation

`DevTools/Verify-KnowledgeCultivarUI.ps1` checks the shared Colony invariant, all three knowledge
adapters, mutation decoupling, personal plant-work effects, the Field Journal page structure,
comparison gating, removal of Breeding Program UI, and preservation of save keys and mechanics.

## Automatic plant-mask fallback

### Backward-compatible boundary

- `PlantSettingsRecord.plantMaskLayers` and `plantMaskVariations` remain the authoritative manual
  mask data and retain their existing Scribe keys, three-layer format, 256px resolution, import,
  export, rendering, and configuration behavior.
- `PlantMaskUtility` remains the renderer-facing lookup. It preserves the old variation resolver:
  an explicit painted variation wins, and a missing variation record inherits the painted base
  manual mask unchanged. Only a variation whose resolved manual layers have no painted data may
  fall back to `PlantAutoMaskCache`, which supplies the same `VisualMaskLayerRecord` shape.
- Variation discovery reads already-loaded `Texture2D` assets through `ContentFinder` and never
  accesses `GraphicData.Graphic` or `GraphicDatabase`; startup mask generation therefore cannot
  instantiate unrelated plant materials or apply their shader parameters.
- Automatic masks are inert until an active trait resolves to a mask-targeted color change. Merely
  having a cached mask never moves a gameplay-only variety onto the custom multi-layer renderer.
- Collection assets are collapsed by resolved texture identity before variation records are built,
  preventing duplicate mod-content entries from multiplying masks and render-cache work.
- Automatic masks are stored separately under RimWorld's Config directory. Cache records are
  versioned and keyed by plant, texture variation, texture identity/size, generator version, and
  harvested-produce identity/size. Paired immature and leafless references are fingerprinted too,
  so changes to semantic reference art invalidate dependent automatic masks. They are not added to
  mod settings or mask-library exports.
- Editing an automatic mask copies it into the existing manual record before the first change.
  From then on normal settings persistence and export behavior apply, and the manual copy overrides
  every cached automatic result. Reset stores an explicit empty later-variation record when needed
  so that variation can use Auto instead of inheriting a base manual mask; regeneration only
  replaces the automatic cache entry and never writes through a manual record.
- With Dev mode enabled, selecting any spawned plant exposes a direct mask-editor gizmo. It resolves
  the selected plant's current texture back to its variation record. The editor's Move tool limits
  reassignment to the clicked source layer's connected, color-similar region, allowing a misplaced
  branch to move from Leaves to Stem without repainting the canopy; the operation promotes Auto to
  Manual and participates in the existing undo/redo history.
- Automatic masks treat Produce, Leaves, and Stem as optional semantic layers. Eligibility comes
  from the texture state, harvested-product semantics, and structural evidence: leafless and stump
  graphics may have no Leaves, structural/material harvests do not imply visible Produce, and a
  narrow rooted component is required before a non-tree receives Stem. Pixels without sufficient
  evidence remain unassigned and therefore retain the plant's neutral/base appearance. When a
  harvested-product icon is unavailable, compact repeated color-distinct regions can still establish
  visible Produce on an eligible food plant; uniform foliage cannot.
- The mask editor keeps absent layers selectable for correction but labels automatic absence as
  "absent" (with a detection tooltip) and a manually cleared layer as "empty".

### Generation and rendering

- Generation runs once after assets finish loading, or explicitly from the mask development UI.
  Cached entries are reused across sessions; the normal draw path only performs dictionary lookup.
- Automatic cache identity includes the source mod package, discovered texture variant, sampled texture
  content, aligned state-reference content, harvested-product signature, and generator version. Once a
  record is validated for the session, render lookups do not re-read or regenerate its texture.
- The classifier reads source alpha and HSV features, but semantic assignment is hierarchical rather
  than a per-pixel score contest. Produce requires a harvested-item texture resolved from the def's
  declared graphic path and grows only into bounded, coherent color-matched components. Stem starts
  from the strongest lower-central structural color and may propagate only through a narrow
  root-connected network; broad same-colored canopy regions stop propagation. Leaves receive the
  remaining eligible silhouette, while unsupported semantics stay neutral. Every classified pixel
  keeps the source texture's original alpha during existing recoloring.
- Low-confidence automatic records remain available to the editor and diagnostics but resolve to no
  renderable semantic layers until manually promoted. Recoloring uses one HSV transform across runtime
  and previews: hue/saturation can change while source value supplies the shading, and very dark outline
  pixels receive sharply reduced color strength. All styled textures/materials are derived and cached;
  source `Texture2D`, `Material`, and `Graphic` instances are never modified.
- Classification contains no playthrough or load-order randomness: identical source/reference pixels and
  def metadata produce identical layer hashes and confidence. A repeated-classification regression protects
  this invariant.
- The manual painter continues to edit the same three binary `VisualMaskLayerRecord` channels and the same
  `plantMaskLayers` / `plantMaskVariations` save keys. Fast operations are transient editor commands over
  those records: morphology, color-bounded region selection, component cleanup, smart expansion, validation,
  and alpha-bounds projection between discovered texture variations. Channel locks, preview mode, brush mode,
  and validation overlays are editor-session state and do not change the renderer or serialized format.
- The random-variety grid dev action consumes the registry's visible, non-archived `VarietyRecord` values
  rather than manufacturing transient cultivar state. It groups records by crop, shuffles each species pass,
  and exhausts that pass before repeating a crop; counts therefore differ by at most one even when fewer than
  100 species are available. It then chooses a random cultivar within the selected species and initializes the
  existing `CompPlantVariety` before spawn. Fresh saves use the normal trait generator and registry unlock path
  to create one `DEV grid` cultivar for missing random species, up to the 100-cell capacity. Registry and save
  formats remain unchanged. Placement counts only stable spawned occupants; a species rejected by its own
  terrain-specific `SpawnSetup` is removed from that grid's candidate set and replaced from the least-used
  remaining species, without hard-coded plant exceptions.
- Where the def provides aligned state art, mature-versus-immature difference gates Produce and the
  mature/leafless alpha intersection supplies a higher-confidence structural reference before the
  morphology fallback runs.
- Coordinate-wise state differencing is enabled only when source/reference alpha IoU is at least
  0.88. Palette-only Produce results covering more than 24% of the opaque plant are rejected as
  ambiguous instead of recoloring broad foliage.
- All discovered growth-state, random-collection, and directional textures receive independent
  variation entries. Plants without harvested produce use morphology and color separation alone.
- Confidence is derived from score separation and structural coverage. Low-confidence cache entries
  are surfaced for manual review in the mask editor and batch-generation summary.

### Compatibility verification

`DevTools/Verify-AutoPlantMasks.ps1` checks the manual-first lookup, unchanged manual Scribe keys and
renderer contract, persistent versioned cache, required classifier evidence, variation support,
editor promotion/reset/regeneration actions, low-confidence flagging, and batch non-overwrite rule.
