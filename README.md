# Horticulture - Novel Seeds

Novel Seeds makes plant discovery part of colony life. Plants can develop unusual traits, and mature discoveries can be preserved as named cultivars that you can select from ordinary growing-zone plant menus.

## How it works

1. Grow plants normally. Mutations are uncommon by default.
2. When a mature plant has a pending discovery, use **Preserve novel seeds** and let it be harvested or cut.
3. Name the cultivar. The seed pack keeps the exact trait combination until you confirm it; cancelling the dialog keeps the pack.
4. Select the cultivar from the plant menu whenever you want to grow it again. No separate seed-stack micromanagement is required.

Cultivars can also arise from wild discoveries and cross-pollination. Hybrids record their parents and can inherit mechanical traits, produce qualities, and visual effects. Traits always show their gameplay effect and relevant tradeoff in the discovery and registry views.

## What you can customize

The settings window starts with the two choices most players need: mutation rate and cross-pollination rate. Open **Advanced settings** for donor requirements, trait balancing, produce visuals, color palettes, resets, and trait defaults. Plant groups and individual plant pages provide targeted overrides without changing the global defaults.

The **Cultivar Registry** lets you browse plants, inspect cultivars, review colony knowledge, compare discoveries, see lineage, favorite or archive records, and locate matching plants or produce on the map.

## Compatibility

Novel Seeds automatically supports most conventional sowable plants, including sowable trees. It reads ordinary plant definitions and uses existing plant menus, harvest hooks, and produce definitions.

Mods with custom planting menus, custom harvest/cutting rules, custom graphics, or custom product-generation systems may need compatibility work. Such a mod can still load safely, but its special behavior may not expose every Novel Seeds feature until an integration is added. See [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md) for the tested compatibility policy.

Required dependencies:

- Harmony
- Insight Canvas 2.1.0
- Knowledge Framework
- Progression: Agriculture

Supported game version: RimWorld 1.6.

## Visuals and performance

The Visual Designer uses the installed Insight Canvas dependency for editor chrome and keeps
the specialized Horticulture plant/produce preview and mask painter authoritative. See
[docs/VISUAL_EDITOR.md](docs/VISUAL_EDITOR.md) for the Plant/Produce channel and inheritance
contract.

Plant colors and masks are generated and cached per discovered texture variant. Manual masks take priority; validated local and bundled masks are used before a finite fallback generation pass. Normal rendering does not generate masks or perform texture readbacks.

The release includes a validated automatic-mask bundle. Low-confidence records remain available for inspection and manual correction but are not applied automatically.

## Feedback

Please include the RimWorld version, active plant/content mods, the affected plant Def if known, the expected and actual behavior, and a save or reproduction sequence when reporting a problem. The beta questionnaire is in [docs/BETA_FEEDBACK.md](docs/BETA_FEEDBACK.md).

For defaults, testing, and release details, see [docs/DEFAULTS.md](docs/DEFAULTS.md), [docs/RC_TEST_PLAN.md](docs/RC_TEST_PLAN.md), and [CHANGELOG.md](CHANGELOG.md).
