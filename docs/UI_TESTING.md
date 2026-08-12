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

During interactive QA, inspect wide and narrow settings windows, normal and compact density,
high contrast, reduced motion, navigation wrapping, the Groups/Plants/Traits split layout,
search filtering, virtualized selections, keyboard Tab/Shift+Tab and Enter/Space activation,
text-field ownership, Escape, tooltips, focus rings, confirmation dialogs, toasts, and the
Advanced diagnostics counters. A zero-render-error result and zero duplicate-ID paths are
required.
