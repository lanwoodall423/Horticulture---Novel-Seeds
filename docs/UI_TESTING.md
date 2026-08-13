# Insight Canvas UI testing

Horticulture owns the UI assertions. Insight Canvas supplies composable rendering, focus,
diagnostics, and virtualization primitives; DevBridge2 only coordinates the RimWorld process,
readiness generation, and test lease.

## Static verification

Run the package check and the UI-specific contract check from the mod root:

```powershell
dotnet build .\Source\HorticultureNovelSeeds.csproj --configuration Release
.\DevTools\Verify-InsightCanvasUI.ps1
.\DevTools\Test-ReleasePackage.ps1
```

The contract check verifies the dependency and portable reference, exact framework provenance,
absence of a bundled `InsightCanvas.dll`, document ownership, all five pages, stable scopes,
direct bindings, bounded virtualization, responsive splits, accessibility options, local
feedback/confirmation paths, and removal of legacy static Widgets layout state.

## Runtime discovery

The `ux-discovery` scenario constructs independent `InsightSettingsDocument` instances,
checks their document state/focus/toast isolation, verifies duplicate-ID diagnostics, confirms
all navigation pages and bounded virtual-list limits, exercises workspace tabs/search,
plant/trait selection, group creation, direct authoritative mutation bindings, dependent
balance controls, wide/narrow split orientation, high-contrast/reduced-motion/compact-density
state, and zero pre-render diagnostics. It also preserves the existing progressive-settings
and cultivar-comparison checks. It is run through the normal Horticulture-owned harness:

```powershell
.\DevTools\Run-RuntimeTests.ps1 -Scenario ux-discovery
```

The full scenario includes the same UI discovery before gameplay, serialization, Knowledge,
mask, and save/reload coverage:

```powershell
.\DevTools\Run-RuntimeTests.ps1 -Scenario complete
```

The `workspace` scenario covers the Cultivar Registry replacement independently. It constructs
isolated `HorticultureWorkspaceDocument` instances, verifies the five field-guide pages and
`PostClose` lifecycle, exercises navigation/search and external plant/Knowledge entry points,
checks wide/narrow splits and document-local accessibility state, verifies empty and safe 1,000
item virtual-list bounds, comparison limits, semantic trait chips, Knowledge availability
guidance, and deterministic bounded lineage handling for missing parents and cycles:

```powershell
.\DevTools\Run-RuntimeTests.ps1 -Scenario workspace
```

The existing `registry-scale` scenario remains the gameplay-data stress test for 100, 500, and
1,000 cultivar records and 1,000 stable `GetVariety` identity lookups. `knowledge` and
`save-reload` continue to validate adapter authority, event observation, legacy migration, and
serialized variety/breeding compatibility. Run the workspace scenario after a DevBridge2 restart
when production gameplay or serialized types have changed; use DevBridge2 only for status,
restart, readiness, test leases, and lease release.

During interactive QA, inspect wide and narrow settings windows, normal and compact density,
high contrast, reduced motion, navigation wrapping, the Groups/Plants/Traits split layout,
search filtering, virtualized selections, keyboard Tab/Shift+Tab and Enter/Space activation,
text-field ownership, Escape, tooltips, focus rings, confirmation dialogs, toasts, and the
Advanced diagnostics counters. Also inspect the field-guide Overview empty state, masked and
known Plants, Cultivars filters and trait badges, contextual Compare, read-only Breeding, both
Knowledge scopes/unavailable guidance, and lineage missing-parent/cycle states. A
zero-render-error result and zero duplicate-ID paths are required for both documents.

The `visual-designer` scenario adds the Prompt 3 player-surface contract: isolated Visual
Designer documents, three section tabs, Plant/Produce modes, semantic mask channels,
inheritance/reset controls, bounded dialog collections, naming chrome, embedded inspector
documents, accessibility state, and duplicate/render diagnostics. Run it after visual,
dialog, inspector, or mask-editor changes:

```powershell
.\DevTools\Run-RuntimeTests.ps1 -Scenario visual-designer
```

Interactive Prompt 3 QA must also inspect a live cached plant preview, produce preview,
Original/Mask/Final mask views, Add/Remove/Replace brush input, review queue filtering, naming
validation, group/tag/trait reset actions, Plant/Produce inspector tabs, keyboard focus, high
contrast, reduced motion, compact density, and 1,000-row collection behavior. A preview frame
must not generate masks or rebuild textures; manual masks, semantic channels, inheritance, and
save/load identities must remain unchanged.

Focused dialog QA also covers mask import/export selection and error states, breeding-mix search
and minimum-two validation, profile-name validation and replacement confirmation, and the
Canvas-hosted mask-color preview at wide and narrow widths. The developer-only unlock window is
debug-only and is excluded from the normal player journey; the lineage compatibility shim is
covered by workspace navigation instead of a second renderer.
